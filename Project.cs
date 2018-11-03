using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.Serialization;

/// <summary>
/// Represents Visual studio project .xml model
/// </summary>
[DebuggerDisplay("{ProjectName}, {RelativePath}, {ProjectGuid}")]
public class Project
{
    /// <summary>
    /// Solution where project is included from. null if project loaded as standalone.
    /// </summary>
    [XmlIgnore]
    public Solution solution;

    /// <summary>
    /// true if it's folder (in solution), false if it's project. (default)
    /// </summary>
    public bool bIsFolder = false;

    /// <summary>
    /// true if it's Android Ant or Gradle packaging project (Set separately from Keyword, because might be parsed out from solution file)
    /// </summary>
    public bool bIsPackagingProject = false;

    /// <summary>
    /// Don't generate project if defined as externalproject
    /// </summary>
    public bool bDefinedAsExternal = true;

    /// <summary>
    /// Made as a property so can be set over reflection.
    /// </summary>
    public String ProjectHostGuid
    {
        get
        {
            if (bIsFolder)
            {
                return "{2150E333-8FDC-42A3-9474-1A3956D46DE8}";
            }
            else
            {
                if (bIsPackagingProject)
                    return "{39E2626F-3545-4960-A6E8-258AD8476CE5}";

                return "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}";
            }
        }
        set
        {
            switch (value)
            {
                case "{2150E333-8FDC-42A3-9474-1A3956D46DE8}": bIsFolder = true; break;
                case "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}": bIsFolder = false; break;
                case "{39E2626F-3545-4960-A6E8-258AD8476CE5}": bIsFolder = false; bIsPackagingProject = true; break;
                default:
                    throw new Exception2("Invalid project host guid '" + value + "'");
            }
        }
    }

    /// <summary>
    /// Visual studio file format version, e.g. 2010, 2012, ...
    /// </summary>
    public int fileFormatVersion = 2015;        // By default generate projects for vs2015

    /// <summary>
    /// Sets project file format version
    /// </summary>
    /// <param name="ver">Visual studio version number</param>
    public void SetFileFormatVersion( int ver )
    {
        fileFormatVersion = ver;
        switch (ver)
        {
            case 2010:
            case 2012:
                ToolsVersion = "4.0";
                break;
            case 2013:
                ToolsVersion = "12.0";
                break;
            case 2015:
                ToolsVersion = "14.0";
                break;
            case 2017:
                ToolsVersion = "15.0";
                break;
            default:
                // Try to predict the future here. :-)
                ToolsVersion = (((ver - 2017) / 2) + 15).ToString() + ".0";
                break;
        } //switch
    } //SetFileFormatVersion


    /// <summary>
    /// "4.0" for vs2010/vs2012, "12.0" for vs2013, "14.0" for vs2015
    /// </summary>
    public String ToolsVersion;

    /// <summary>
    /// Sets tools version, also tried to detect file format version
    /// </summary>
    /// <param name="ver"></param>
    public void setToolsVersion(String ver)
    {
        ToolsVersion = ver;
        double dVer = Double.Parse(ver, CultureInfo.InvariantCulture);

        if (dVer <= 4.0)
            fileFormatVersion = 2010;
        else if(dVer <= 12.0)
            fileFormatVersion = 2012;
        else if (dVer <= 14.0)
            fileFormatVersion = 2015;
        else if (dVer <= 15.0)
            fileFormatVersion = 2017;
        else 
            fileFormatVersion = 2017 + ((int)(dVer) * 2);
    } //setToolsVersion

    public EKeyword Keyword = EKeyword.None;
    public const String keyword_Windows = "windows";
    public const String keyword_Android = "android";
    public const String keyword_AntPackage = "antpackage";
    public const String keyword_GradlePackage = "gradlepackage";

    /// <summary>
    /// Only if Keyword == GradlePackage
    /// </summary>
    GradlePackage gradlePackage;
    public GradlePackage GradlePackage
    { 
        get {
            if( gradlePackage == null )
                gradlePackage = new GradlePackage();

            return gradlePackage;
        }
    
    }

    /// <summary>
    /// Gets target OS based on keyword, null if default. (windows or don't care)
    /// </summary>
    /// <returns></returns>
    public String getOs()
    {
        switch (Keyword)
        {
            case EKeyword.Android:
                return "android";
            case EKeyword.AntPackage:
                return Project.keyword_AntPackage;
            case EKeyword.GradlePackage:
                return Project.keyword_GradlePackage;
            default:
                return null;
        }
    } //getOs

    /// <summary>
    /// Target Platform Version, e.g. "8.1" or "10.0.14393.0"
    /// </summary>
    public String WindowsTargetPlatformVersion;

    [XmlIgnore]
    public List<Project> nodes = new List<Project>();   // Child nodes (Empty folder also does not have any children)
    [XmlIgnore]
    public Project parent;                              // Points to folder which contains given project

    /// <summary>
    /// Project name and it's relative path in form: "subdir\\name"
    /// </summary>
    public string ProjectName;
    
    /// <summary>
    /// Sub-folder and filename of project to save. language defines project file extension
    /// </summary>
    public string RelativePath;

    /// <summary>
    /// if null - RelativePath includes file extension, if non-null - "C++" or "C#" - defines project file extension.
    /// </summary>
    public string language;

    /// <summary>
    /// gets relative path based on programming language
    /// </summary>
    /// <returns></returns>
    public String getRelativePath()
    {
        if (RelativePath == null)
            throw new Exception2("Project '" + ProjectName + "' location was not specified");

        String path = RelativePath.Replace("/", "\\");

        if (bIsFolder)
            return path;

        if (Keyword == EKeyword.AntPackage || Keyword == EKeyword.GradlePackage)
            return path + ".androidproj";

        return path + getProjectExtension();
    } //getRelativePath


    /// <summary>
    /// Gets project extension.
    /// </summary>
    /// <returns>Project extension</returns>
    public String getProjectExtension()
    {
        switch (language)
        {
            default: return ".vcxproj";
            case "C": return ".vcxproj";
            case "C++": return ".vcxproj";
            case "C#": return ".csproj";
        }
    }


    /// <summary>
    /// Gets folder where project will be saved in.
    /// </summary>
    public String getProjectFolder()
    {
        String dir;
        if (solution == null)
            dir = SolutionProjectBuilder.m_workPath;
        else
            dir = Path.GetDirectoryName(solution.path);

        dir = Path.Combine(dir, Path.GetDirectoryName(RelativePath));
        return dir;
    }

    /// <summary>
    /// Gets project full path
    /// </summary>
    /// <returns>Project full path</returns>
    public String getFullPath()
    {
        String dir;
        if (solution == null)
            dir = SolutionProjectBuilder.m_workPath;
        else
            dir = Path.GetDirectoryName(solution.path);

        return Path.Combine(dir, RelativePath + getProjectExtension());
    }

    /// <summary>
    /// Returns true if this is not a project, but solution folder instead.
    /// </summary>
    /// <returns>false - project, true - folder in solution</returns>
    public bool IsSubFolder()
    {
        return bIsFolder;
    }


    /// <summary>
    /// Same amount of configurations as in solution, this however lists project configurations, which correspond to solution configuration
    /// using same index.
    /// </summary>
    public List<String> slnConfigurations = new List<string>();

    /// <summary>
    /// List of supported configuration|platform permutations, like "Debug|Win32", "Debug|x64" and so on.
    /// </summary>
    public List<String> configurations = new List<string>();

    /// <summary>
    /// Updates file configuration array from project configurations
    /// </summary>
    /// <param name="fi">File to which to add configurations</param>
    public void updateFileConfigurations(FileInfo fi)
    {
        while (fi.fileConfig.Count < configurations.Count)
            fi.fileConfig.Add(new FileConfigurationInfo()
                {
                    confName = configurations[fi.fileConfig.Count],
                    Optimization = EOptimization.ProjectDefault
                }
            );
    }

    /// <summary>
    /// Gets list of supported configurations like 'Debug' / 'Release'
    /// </summary>
    public List<String> getConfigurationNames()
    {
        return configurations.Select(x => x.Split('|')[0]).Distinct().ToList();
    }

    /// <summary>
    /// Gets list of supported platforms like 'Win32' / 'x64'
    /// </summary>
    public List<String> getPlatforms()
    {
        return configurations.Select(x => x.Split('|')[1]).Distinct().ToList();
    }


    /// <summary>
    /// true or false whether to build project or not.
    /// </summary>
    public List<bool> slnBuildProject = new List<bool>();

    /// <summary>
    /// true to deploy project, false - not, null - invalid. List is null if not used at all.
    /// </summary>
    public List<bool?> slnDeployProject = null;

    /// <summary>
    /// Project guid, for example "{65787061-7400-0000-0000-000000000000}"
    /// </summary>
    public string ProjectGuid;

    /// <summary>
    /// Without "{}"
    /// </summary>
    public String ProjectGuidShort
    {
        get 
        {
            return ProjectGuid.Substring(1, ProjectGuid.Length - 2);
        }
    }

    /// <summary>
    /// per configuration list
    /// </summary>
    public List<Configuration> projectConfig = new List<Configuration>();

    /// <summary>
    /// Project dependent guids. Set to null if not used.
    /// </summary>
    public List<String> ProjectDependencies { get; set; }

    /// <summary>
    /// This array includes all items from ItemGroup, independently whether it's include file or file to compile, because
    /// visual studio is ordering them alphabetically - we keep same array to be able to sort files.
    /// </summary>
    public List<FileInfo> files = new List<FileInfo>();


    public string AsSlnString()
    {
        return "Project(\"" + ((bIsFolder) ? "Folder" : "Project") + "\") = \"" + ProjectName + "\", \"" + RelativePath + "\", \"" + ProjectGuid + "\"";
    }

    static String[] RegexExract(String pattern, String input)
    {
        Match m = Regex.Match(input, pattern);
        if (!m.Success)
            throw new Exception("Error: Parse failed (input string '" + input + "', regex: '" + pattern + "'");

        return m.Groups.Cast<Group>().Skip(1).Select(x => x.Value).ToArray();
    } //RegexExract


    /// <summary>
    /// Extracts configuration name in readable form.
    /// Condition="'$(Configuration)|$(Platform)'=='Debug|x64'" => "Debug|x64"
    /// </summary>
    /// <returns>null if Condition node does not exists, configuration name otherwise</returns>
    static String getConfiguration(XElement node)
    {
        var n = node.Attribute( "Condition" );
        if( n == null )
            return null;
        
        String config = RegexExract("^ *'\\$\\(Configuration\\)\\|\\$\\(Platform\\)' *== *'(.*)'", n.Value)[0];
        return config;
    }


    void confListInit<T>(ref List<T> list, T defT = default(T))
    {
        if( list == null )
            list = new List<T>(configurations.Count);

        while (list.Count < configurations.Count)
        {
            T t = defT;
            if (t == null)
                t = (T)Activator.CreateInstance(typeof(T));
            list.Add(t);
        }
    }


    /// <summary>
    /// Extracts compilation options for single cpp/cs file.
    /// </summary>
    /// <param name="clCompile">xml node from where to get</param>
    /// <param name="file2compile">compiler options to fill out</param>
    /// <param name="subField">Into which field to enter if non null</param>
    void ExtractCompileOptions(XElement clCompile, FileInfo file2compile, String subField)
    {
        foreach (XElement fileProps in clCompile.Elements())
        {
            int[] confIndexes2Process;

            if (fileProps.Attribute("Condition") == null)
            {
                // This kind of nodes is possible to create only using custom build tools like premake5, Visual studio does not produce such projects.
                confIndexes2Process = new int[configurations.Count];
                for (int i = 0; i < configurations.Count; i++)
                    confIndexes2Process[i] = i;
            }
            else {
                String config = getConfiguration(fileProps);

                int iCfg = configurations.IndexOf(config);
                if (iCfg == -1)
                    continue;           // Invalid configuration string specified.

                confIndexes2Process = new int[] { iCfg };
            } //if-else

            String localName = fileProps.Name.LocalName;
            Type type = typeof(FileConfigurationInfo);
            if (subField != null)
                type = type.GetField(subField).FieldType;       //==typeof(CustomBuildRule)

            FieldInfo fi = null;
            String xmlNodeName = fileProps.Name.LocalName;


            if (xmlNodeName == "AdditionalOptions")
                xmlNodeName = "ClCompile_AdditionalOptions";

            fi = type.GetField(xmlNodeName);
            if (fi == null)
            {
                // Generated by premake5, no clue what this tag is, maybe a bug in premake5 tool.
                if (xmlNodeName == "FileType")
                    continue;

                if (Debugger.IsAttached) Debugger.Break();
                continue;
            }

            //
            // FileConfigurationInfo: PrecompiledHeader, PreprocessorDefinitions, AdditionalIncludeDirectories, ObjectFileName, XMLDocumentationFileName
            // CustomBuildRule: Command, Message, Outputs, AdditionalInputs, DisableSpecificWarnings
            //
            while (file2compile.fileConfig.Count < configurations.Count)
            {
                int i = file2compile.fileConfig.Count;
                // Add new configurations, use same precompiled header setting as project uses for given configuration.
                FileConfigurationInfo nfci = new FileConfigurationInfo() { PrecompiledHeader = projectConfig[i].PrecompiledHeader };
                nfci.Optimization = EOptimization.ProjectDefault;
                file2compile.fileConfig.Add(nfci);

                if (subField != null)   
                    //file2compile.fileConfig[i].customBuildRule = new CustomBuildRule;
                    typeof(FileConfigurationInfo).GetField(subField).SetValue(nfci, Activator.CreateInstance(type));
            } //while

            foreach (int iCfg in confIndexes2Process)
            {
                FileConfigurationInfo fci = file2compile.fileConfig[iCfg];
                Object o2set;

                if (subField == null)
                    o2set = fci;
                else
                    o2set = typeof(FileConfigurationInfo).GetField(subField).GetValue(fci);     //fci.customBuildRule

                object oValue;

                if (fi.FieldType.IsEnum)
                    oValue = Enum.Parse(fi.FieldType, fileProps.Value);
                else
                    oValue = Convert.ChangeType(fileProps.Value, fi.FieldType);

                if (xmlNodeName == "AdditionalInputs")
                    oValue = oValue.ToString().Replace(";%(AdditionalInputs)", "");     //No need for this extra string.

                fi.SetValue(o2set, oValue);
            } //foreach
        } //foreach
    } //ExtractCompileOptions

    /// <summary>
    /// Gets xml node by name.
    /// </summary>
    /// <param name="node">Xml node from where to get</param>
    /// <param name="field">Xml tag to query</param>
    /// <returns>object if xml node value if any, null if not defined</returns>
    static object ElementValue(XElement node, String field)
    { 
        object oValue = node.Element(node.Document.Root.Name.Namespace + field)?.Value;
        return oValue;
    }


    /// <summary>
    /// Copies field by "field" - name, from node.
    /// </summary>
    /// <returns>false if fails (value does not exists(</returns>
    static bool CopyField(object o2set, String field, XElement node)
    {
        FieldInfo fi = o2set.GetType().GetField(field);
        Object oValue = null;

        while (true)
        {
            oValue = ElementValue(node, field);
            if (oValue != null)
                break;

            if (field == "ProjectGuid")     // Utility projects can have such odd nodes naming.
            {
                field = "ProjectGUID";
                continue;
            }
            break;
        }


        if (fi.FieldType == typeof(EKeyword))
        {
            if (oValue == null)
                return false;
            oValue = Enum.Parse(typeof(EKeyword), (String)oValue);
        }

        fi.SetValue(o2set, oValue);
        return true;
    }

    static bool IsSimpleDataType(Type type)
    {
        if (type == typeof(bool) ||
            type == typeof(double) ||
            type == typeof(int) ||
            type.IsEnum ||
            type == typeof(string) ||
            type == typeof(float) ||
            type == typeof(System.Int64) ||
            type == typeof(System.Int16)
            )
            return true;

        return false;
    }

    /// <summary>
    /// Copies data from node to object o, using field info fi.
    /// </summary>
    void copyNodeToObject(FieldInfo fi, XElement node, object o)
    {
        object childo = fi.GetValue(o);
        Type type = fi.FieldType;
        XElement[] nodes = node.Elements().ToArray();

        //  Explode sub nodes if we have anything extra to scan. (Comes from ItemDefinitionGroup)
        for (int i = 0; i < nodes.Length; i++)
        {
            String nodeName = nodes[i].Name.LocalName;
            FieldInfo childFi = type.GetField(nodeName);
            if (childFi == null)
                continue;

            if (!IsSimpleDataType(childFi.FieldType))
                continue;

            childFi.SetValue(childo, Convert.ChangeType(nodes[i].Value, childFi.FieldType));
        }
    }




    void extractGeneralCompileOptions(XElement node)
    {
        int[] cfgIndexes = null;
        
        String config = getConfiguration(node);
        if( config == null )
        {
            // Visual studio does not have projects like this, but GYP generator does
            cfgIndexes = Enumerable.Range( 0, configurations.Count ).ToArray();
        }
        else
        {
            int iCfg = configurations.IndexOf( config );

            if( iCfg == -1 )
                return;

            cfgIndexes = new int[] { iCfg };
        }

        foreach ( int iCfg in cfgIndexes )
        {
            Configuration cfg = projectConfig[iCfg];

            List<XElement> nodes = node.Elements().ToList();

            //  Explode sub nodes if we have anything extra to scan. (Comes from ItemDefinitionGroup)
            for (int i = 0; i < nodes.Count; i++)
            {
                String nodeName = nodes[i].Name.LocalName;
                // Nodes can be exploded into same scan loop as long as key do not overlap (e.g. compile options versus link options).
                if (nodeName == "ClCompile" || nodeName == "Link" || nodeName == "AntPackage" || nodeName == "Lib")    // These nodes are located in ItemDefinitionGroup, we simply expand sub children.
                {
                    foreach (XElement compLinkNode in nodes[i].Elements())
                    {
                        // ClCompile & Link has same named options, we try to differentiate them here.
                        if (compLinkNode.Name.LocalName == "AdditionalOptions")
                            compLinkNode.Name = nodeName + "_AdditionalOptions";
                        nodes.Add(compLinkNode);
                    }

                    nodes.RemoveAt(i);
                    i--;
                }
            } //for

            foreach (XElement cfgNode in nodes)
            {
                String fieldName = cfgNode.Name.LocalName;

                if (fieldName == "LibraryDependencies")
                    fieldName = "AdditionalDependencies";

                //
                // Visual studio project keeps Lib options in separate xml tag <Lib> <AdditionalOptions>... - but for us it's the same as LinkOptions.
                //
                if (fieldName == "Lib_AdditionalOptions") fieldName = "Link_AdditionalOptions";

                FieldInfo fi = typeof(Configuration).GetField(fieldName);
                if (fi == null)
                    continue;

                if (!IsSimpleDataType(fi.FieldType))
                {
                    copyNodeToObject(fi, cfgNode, cfg);
                    continue;
                }

                if (fi.FieldType.IsEnum)
                {
                    if (fi.FieldType.GetCustomAttribute<DescriptionAttribute>() == null )
                    {
                        if (fi.FieldType == typeof(EUseOfMfc) && cfgNode.Value == "false")
                        {
                            fi.SetValue(cfg, EUseOfMfc._false);
                        }
                        else
                        {
                            String v = cfgNode.Value;
                            if (fi.Name == "PrecompiledHeader" && v == "")
                                v = "ProjectDefault";

                            // cmake produces file like this.
                            if (fi.Name == "DebugInformationFormat" && v == "")
                                v = "Invalid";

                            fi.SetValue(cfg, Enum.Parse(fi.FieldType, v));
                        }
                    }
                    else
                    {
                        // Extract from Description attributes their values and map corresponding enumeration.
                        int value = fi.FieldType.GetEnumNames().Select(x => fi.FieldType.GetMember(x)[0].GetCustomAttribute<DescriptionAttribute>().Description).ToList().IndexOf(cfgNode.Value);
                        if (value == -1)
                            new Exception2("Invalid / not supported value '" + cfgNode.Value + "'");
                        fi.SetValue(cfg, Enum.Parse(fi.FieldType, fi.FieldType.GetEnumNames()[value]));
                    }

                    if (fieldName == "ConfigurationType")
                        ((Configuration)cfg).ConfigurationTypeUpdated();
                }
                else
                {
                    fi.SetValue(cfg, Convert.ChangeType(cfgNode.Value, fi.FieldType));
                }
            } //foreach
        } //foreach
    } //extractGeneralCompileOptions


    /// <summary>
    /// Gets projects guid from file.
    /// </summary>
    /// <param name="path">path from where to load project</param>
    /// <returns>Project guid</returns>
    static public String getProjectGuid(String path)
    {
        Project p = LoadProject(null, path, null, 1);
        return p.ProjectGuid;
    }

    /// <summary>
    /// Loads project. If project exists in solution, it's loaded in same instance.
    /// </summary>
    /// <param name="solution">Solution if any exists, null if not available.</param>
    /// <param name="path">path from where to load project</param>
    /// <param name="project">instance into which to load, null if create new</param>
    /// <param name="loadLevel">1 if interested only in guid</param>
    static public Project LoadProject(Solution solution, String path, Project project = null, int loadLevel = 0 )
    {
        if (path == null)
            path = Path.Combine(Path.GetDirectoryName(solution.path) , project.RelativePath);

        if (project == null)
            project = new Project() { solution = solution };

        if (!File.Exists(path))
            return null;

        XDocument p = XDocument.Load(path);

        var toolsVerNode = p.Root.Attribute("ToolsVersion");
        if (toolsVerNode == null)       // For example vs2008 projects.
            throw new Exception("Project file format is not supported: '" + path + "'");

        String toolsVer = toolsVerNode.Value;
        if(toolsVer != "")
            project.setToolsVersion(toolsVer);

        foreach (XElement node in p.Root.Elements())
        {
            String lname = node.Name.LocalName;

            switch (lname)
            {
                case "ItemGroup":
                    if (loadLevel == 1) continue;
                    // ProjectConfiguration has Configuration & Platform sub nodes, but they cannot be reconfigured to anything else then this attribute.
                    if (node.Attribute("Label")?.Value == "ProjectConfigurations")
                    {
                        project.configurations = node.Elements().Select(x => x.Attribute("Include").Value).ToList();
                        project.configurations.ForEach(x => project.projectConfig.Add(new Configuration()));
                    }
                    else
                    {
                        //
                        // .h / .cpp / custom build files are picked up here.
                        //
                        foreach (XElement igNode in node.Elements())
                        {
                            FileInfo f = new FileInfo();
                            f.includeType = (IncludeType)Enum.Parse(typeof(IncludeType), igNode.Name.LocalName);
                            f.relativePath = igNode.Attribute("Include").Value;

                            // Simplify path apperance. (Potentially cmake / premake5 were used)
                            if (f.relativePath.StartsWith(".\\"))
                                f.relativePath = f.relativePath.Substring(2);

                            if (f.includeType == IncludeType.Reference)
                                f.HintPath = igNode.Elements().Where(x => x.Name.LocalName == "HintPath").FirstOrDefault()?.Value;

                            if (f.includeType == IncludeType.ClCompile || f.includeType == IncludeType.CustomBuild)
                                project.ExtractCompileOptions(igNode, f, (f.includeType == IncludeType.CustomBuild) ? "customBuildRule" : null );

                            if (f.includeType == IncludeType.ProjectReference)
                                f.Project = igNode.Elements().Where(x => x.Name.LocalName == "Project").FirstOrDefault()?.Value;

                            project.files.Add(f);
                        } //for
                    }
                    break;

                case "PropertyGroup":
                    {
                        String label = node.Attribute("Label")?.Value;

                        switch (label)
                        {
                            case "Globals":
                                foreach (String field in new String[] { "ProjectGuid", "Keyword", "WindowsTargetPlatformVersion" /*, "RootNamespace"*/ })
                                {
                                    if (!CopyField(project, field, node) && field == "Keyword")
                                    {
                                        if (Path.GetExtension(path).ToLower() == ".androidproj")
                                        {
                                            // Android packaging projects does not have Keyword
                                            String buildType = ElementValue(node, "AndroidBuildType") as String;
                                            if(buildType != null && buildType == "Gradle")
                                                project.Keyword = EKeyword.GradlePackage;
                                            else
                                                project.Keyword = EKeyword.AntPackage;
                                        }
                                    } //if

                                    if (project.ProjectGuid != null && loadLevel == 1)
                                        return project;
                                }
                                break;

                            case null:                  // Non tagged node contains rest of configurations like 'LinkIncremental', 'OutDir', 'IntDir', 'TargetName', 'TargetExt'

                                if (node.Attribute("Condition") == null)
                                {
                                    // Android packaging project can contain such empty nodes. "<PropertyGroup />"
                                    
                                    // C# project
                                    foreach (XElement subNode in node.Elements())
                                    {
                                        if (subNode.Name.LocalName == "ProjectGuid")
                                        {
                                            project.ProjectGuid = subNode.Value;

                                            if (project.ProjectGuid != null && loadLevel == 1)
                                                return project;
                                        }
                                    }
                                    continue;
                                }

                                project.extractGeneralCompileOptions(node);
                                break;

                            case "Configuration":
                                if (loadLevel == 1) continue;
                                project.extractGeneralCompileOptions(node);
                                break;

                            case "UserMacros":
                                // What is it - does needs to be supported ?
                                break;

                            case "Locals":
                                // GYP generator specific. What was it?
                                break;

                            default:
                                if (Debugger.IsAttached) Debugger.Break();
                                break;
                        } //switch
                    }
                    break;

                case "Import": break;           // Skip for now.
                case "ImportGroup": break;      // Skip for now.
                case "ItemDefinitionGroup":
                    if (loadLevel == 1) continue;
                    project.extractGeneralCompileOptions(node);
                    break;

                default:
                    if (Debugger.IsAttached) Debugger.Break();
                    break;
            } //switch
        } //foreach

        return project;
    } //LoadProject

    static String condition( String confName )
    {
        return "Condition=\"'$(Configuration)|$(Platform)'=='" + confName + "'\"";
    }

    StringBuilder o;    // Stream where we serialize project.

    /// <summary>
    /// Dumps file or project specific configuration.
    /// </summary>
    /// <param name="conf">Configuration to dump</param>
    /// <param name="confName">Configuration name, null if project wise</param>
    /// <param name="projectConf">If null, then conf is project wide configuration, if non-null - project configuration of file specific configuration</param>
    void DumpConfiguration(FileConfigurationInfo conf, String confName = null, Configuration projectConf = null )
    {
        String sCond = "";
        if (confName != null)
            sCond = " " + condition(confName);

        //-----------------------------------------------------------------------------------------
        // When UseDebugLibraries is set to true, and optimizations are enabled
        // we must disable run-time checks, otherwise will get compilation error.
        //  cl : Command line error D8016: '/O2' and '/RTC1' command-line options are incompatible
        //-----------------------------------------------------------------------------------------
        bool bUseDebugLibraries = false;
        EOptimization opt = conf.Optimization;
        EBasicRuntimeChecks rtc = conf.BasicRuntimeChecks;

        if (projectConf != null)    // If we have project wide configuration, copy values from there just to know if we need
        {                           // to configure on individual file level.
            bUseDebugLibraries = projectConf.UseDebugLibraries;

            if (opt == EOptimization.ProjectDefault)
                opt = projectConf.Optimization;

            if (rtc == EBasicRuntimeChecks.ProjectDefault)
                rtc = projectConf.BasicRuntimeChecks;
        }
        else
            bUseDebugLibraries = (conf as Configuration).UseDebugLibraries;

        if (bUseDebugLibraries &&  opt != EOptimization.Disabled && 
            // Not yet specified by end-user
            rtc == EBasicRuntimeChecks.ProjectDefault )
            conf.BasicRuntimeChecks = EBasicRuntimeChecks.Default;



        if ( conf.ObjectFileName != null )
            o.AppendLine("      <ObjectFileName" + sCond + ">" + conf.ObjectFileName + "</ObjectFileName>");

        if (conf.PreprocessorDefinitions.Length != 0)
        {
            String defines = conf.PreprocessorDefinitions;
            if (defines.Length != 0) defines += ";";
            defines += "%(PreprocessorDefinitions)";

            o.AppendLine("      <PreprocessorDefinitions" + sCond + ">" + defines + "</PreprocessorDefinitions>");
        }

        if (conf.DebugInformationFormat == EDebugInformationFormat.Invalid)
            conf.DebugInformationFormat = conf.getDebugInformationFormatDefault(confName);

        if (conf.DebugInformationFormat != conf.getDebugInformationFormatDefault(confName))
        {
            //
            // <DebugInformationFormat>None appered only from vs2013 version. Previous versions of vs did not mark "None" anyhow - whole
            // xml tag was omitted, resulting in default value
            //
            if( fileFormatVersion >= 2013 || conf.DebugInformationFormat != EDebugInformationFormat.None )
                o.AppendLine( "      <DebugInformationFormat" + sCond + ">" + conf.DebugInformationFormat.ToString() + "</DebugInformationFormat>" );

            //
            //  http://stackoverflow.com/questions/2762930/vs2010-always-thinks-project-is-out-of-date-but-nothing-has-changed
            //  See "Forcing recompile of all source files due to missing PDB".
            //
            //  ProgramDataBaseFileName must be set to empty if DebugInformationFormat is None.
            //
            if ( conf.DebugInformationFormat == EDebugInformationFormat.None )
                o.AppendLine("      <ProgramDataBaseFileName />");
        }

        if (conf.AdditionalIncludeDirectories.Length != 0 )
            o.AppendLine("      <AdditionalIncludeDirectories" + sCond + ">" + conf.AdditionalIncludeDirectories + ";%(AdditionalIncludeDirectories)</AdditionalIncludeDirectories>");

        if( conf.RuntimeLibrary != ERuntimeLibrary.NotSet)
            o.AppendLine( "      <RuntimeLibrary" + sCond + ">" + conf.RuntimeLibrary.ToString() + "</RuntimeLibrary>" );

        if( conf.DisableSpecificWarnings.Length != 0)
            o.AppendLine("      <DisableSpecificWarnings" + sCond + ">" + conf.DisableSpecificWarnings + ";%(DisableSpecificWarnings)</DisableSpecificWarnings>");

        if (conf.ExceptionHandling != EExceptionHandling.ProjectDefault)
        {
            String v = typeof(EExceptionHandling).GetField(conf.ExceptionHandling.ToString()).GetCustomAttribute<DescriptionAttribute>().Description;
            o.AppendLine("      <ExceptionHandling" + sCond + ">" + v + "</ExceptionHandling>");
        }

        if ( conf.BasicRuntimeChecks != EBasicRuntimeChecks.ProjectDefault)
            o.AppendLine("      <BasicRuntimeChecks" + sCond + ">" + conf.BasicRuntimeChecks + "</BasicRuntimeChecks>");

        if ( conf.ClCompile_AdditionalOptions.Length != 0)
            o.AppendLine("      <AdditionalOptions" + sCond + ">" + conf.ClCompile_AdditionalOptions + " %(AdditionalOptions)</AdditionalOptions>");

        if (projectConf != null)
        {
            // File specific flags only
            if (conf.PrecompiledHeader != EPrecompiledHeaderUse.ProjectDefault && projectConf.PrecompiledHeader != conf.PrecompiledHeader)
                o.AppendLine("      <PrecompiledHeader" + sCond + ">" + conf.PrecompiledHeader + "</PrecompiledHeader>");

            if (Keyword == EKeyword.Android && conf.CompileAs != ECompileAs.Default)
                o.AppendLine("      <CompileAs" + sCond + ">" + conf.CompileAs.ToString() + "</CompileAs>");

            if (conf.Optimization != EOptimization.ProjectDefault && conf.Optimization != projectConf.Optimization)
                o.AppendLine("      <Optimization" + sCond + ">" + conf.getOptimization(this) + "</Optimization>");
        }

        // Not applicable for Android platform
        if ( Keyword != EKeyword.Android && projectConf != null)
        {
            if (conf.IntrinsicFunctions && conf.IntrinsicFunctions != projectConf.IntrinsicFunctions)
                o.AppendLine("      <IntrinsicFunctions" + sCond + ">true</IntrinsicFunctions>");

            if (conf.FunctionLevelLinking && conf.FunctionLevelLinking != projectConf.FunctionLevelLinking)
                o.AppendLine("      <FunctionLevelLinking" + sCond + ">true</FunctionLevelLinking>");
        }

        if ( conf.ShowIncludes)
            o.AppendLine("      <ShowIncludes" + sCond + ">true</ShowIncludes>");

        if(conf.ExcludedFromBuild )
            o.AppendLine("      <ExcludedFromBuild" + sCond + ">true</ExcludedFromBuild>");
    } //DumpConfiguration


    /// <summary>
    /// Resorts configuration list in some particular order.
    /// </summary>
    /// <param name="configurations">Configuration to sort</param>
    /// <param name="bX3264hasPriority">x86 / x64 platforms have priority.</param>
    /// <param name="b64HasPriority">64 bit configurations have priority. null if not use this sort criteria.</param>
    /// <param name="bCompareConfigNameFirst">true if compare config name first</param>
    /// <returns></returns>
    static public List<String> getSortedConfigurations(List<String> configurations, bool bX3264hasPriority, bool? b64HasPriority = true, bool bCompareConfigNameFirst = false)
    {
        List<String> configurationsSorted = new List<string>();
        configurationsSorted.AddRange(configurations);

        int xPriority = bX3264hasPriority ? 1 : -1;
        int xa64Priority = 1;
        
        if(b64HasPriority.HasValue) 
            xa64Priority = b64HasPriority.Value ? 1 : -1;
        
            configurationsSorted.Sort(delegate (String c1, String c2)
        {
            String[] cp1 = c1.Split('|');
            String[] cp2 = c2.Split('|');
            String p1 = cp1[1].ToLower();
            String p2 = cp2[1].ToLower();

            if (bCompareConfigNameFirst)
            {
                int cr2 = cp1[0].CompareTo(cp2[0]);
                if (cr2 != 0)
                    return cr2;
            }

            if (p1.StartsWith("x") != p2.StartsWith("x"))       // Give x86 & x64 priority over ARM based names.
                return p1.StartsWith("x") ? -xPriority : xPriority;

            if (b64HasPriority.HasValue)
            {
                if (p1.Contains("64") != p2.Contains("64"))         // 64-bit named configurations have priority.
                    return p1.Contains("64") ? -xa64Priority : xa64Priority;
            }

            int cr = p1.CompareTo(p2);
            if (cr != 0)
                return cr;

            return cp1[0].CompareTo(cp2[0]);
        }
        );

        return configurationsSorted;
    }
    
    List<String> getSortedConfigurations(bool bX3264hasPriority)
    {
        return Project.getSortedConfigurations(configurations, bX3264hasPriority);
    }


    IncludeType simplifyForGroupReopen(IncludeType inctype)
    {
        switch (inctype)
        {
            default:
                return inctype;

            case IncludeType.ProjectReference:
                return inctype;     // Must be separated from rest of xml stuff

            case IncludeType.AndroidManifest:
            case IncludeType.Content:
            case IncludeType.AntBuildXml:
            case IncludeType.AntProjectPropertiesFile:
                // Android tags does not require ItemGroup reopen, just return one of android types.
                return IncludeType.Content;
        }
    }

    /// <summary>
    /// Escapes xml special characters. http://weblogs.sqlteam.com/mladenp/archive/2008/10/21/Different-ways-how-to-escape-an-XML-string-in-C.aspx
    /// </summary>
    static String XmlEscape(String s)
    {
        return SecurityElement.Escape(s);
    }


    /// <summary>
    /// Saves project if necessary.
    /// </summary>
    public void SaveProject(UpdateInfo uinfo)
    {
        //
        // We serialize here using string append, so we can easily compare with Visual studio projects with your favorite comparison tool.
        //

        //
        // Make our project selected just to fix it if necessary
        //
        SolutionProjectBuilder.externalproject(null);
        SolutionProjectBuilder.m_project = this;

        if (String.IsNullOrEmpty(ProjectGuid))
            SolutionProjectBuilder.uuid(ProjectName);

        if (configurations.Count == 0)
            SolutionProjectBuilder.platforms("x86");

        if (projectConfig.Count != configurations.Count)        // Make sure we have project configurations created.
            SolutionProjectBuilder.filter();

        SolutionProjectBuilder.m_project = null;

        //
        //  Reassign unique object filenames so linker would not conflict
        //
        Dictionary<String, int> objFileNames = new Dictionary<string, int>();
        foreach (FileInfo fi in files)
        {
            if (fi.includeType != IncludeType.ClCompile)
                continue;

            String fileBase = Path.GetFileNameWithoutExtension(fi.relativePath.Replace("/", "\\")).ToLower();

            if (objFileNames.ContainsKey(fileBase))
            {
                String objFilename = "$(IntDir)\\" + fileBase + objFileNames[fileBase] + ".obj";
                // Add file specific configurations if list is empty.
                while (configurations.Count > fi.fileConfig.Count) fi.fileConfig.Add(new FileConfigurationInfo() { Optimization = EOptimization.ProjectDefault } );
                foreach ( var c in fi.fileConfig )
                    if( String.IsNullOrEmpty(c.ObjectFileName) )    // User can override, then can result in compilation errors.
                        c.ObjectFileName = objFilename;
                
                 objFileNames[fileBase]++;
            } else
            {
                objFileNames[fileBase] = 1;
            }
        } //foreach

        String projectPath;

        if (solution == null)
            projectPath = SolutionProjectBuilder.m_workPath;
        else
            projectPath = Path.GetDirectoryName(solution.path);
        projectPath = Path.Combine(projectPath, getRelativePath());

        // Initialize ToolsVersion to default.
        if( ToolsVersion == null )
            SetFileFormatVersion( fileFormatVersion );

        o = new StringBuilder();
        o.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        o.AppendLine("<Project DefaultTargets=\"Build\" ToolsVersion=\"" + ToolsVersion + "\" xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">");
        o.AppendLine("  <ItemGroup Label=\"ProjectConfigurations\">");

        //
        // Dump configuration list
        //
        foreach (String config in configurations)
        {
            String[] confPlatfom = config.Split('|');
            o.AppendLine("    <ProjectConfiguration Include=\"" + config + "\">");
            o.AppendLine("      <Configuration>" + confPlatfom[0] + "</Configuration>");
            o.AppendLine("      <Platform>" + confPlatfom[1] + "</Platform>");
            o.AppendLine("    </ProjectConfiguration>");
        }
        o.AppendLine("  </ItemGroup>");

        o.AppendLine("  <PropertyGroup Label=\"Globals\">");

        bool bIsAntPackagingProject = Keyword == EKeyword.AntPackage;
        bool bIsGradlePackagingProject = Keyword == EKeyword.GradlePackage;

        if(bIsGradlePackagingProject)
            o.AppendLine("    <AndroidBuildType>Gradle</AndroidBuildType>");

        if (!bIsAntPackagingProject && !bIsGradlePackagingProject)
            o.AppendLine("    <ProjectGuid>" + ProjectGuid + "</ProjectGuid>");

        //
        // Copied from premake5: VS 2013 adds the <IgnoreWarnCompileDuplicatedFilename> to get rid
        // of spurious warnings when the same filename is present in different
        // configurations.
        //
        if (Keyword == EKeyword.None || Keyword == EKeyword.MFCProj || Keyword == EKeyword.Win32Proj || Keyword == EKeyword.Android )
            o.AppendLine("    <IgnoreWarnCompileDuplicatedFilename>true</IgnoreWarnCompileDuplicatedFilename>");

        if (Keyword == EKeyword.Win32Proj || Keyword == EKeyword.Android)
            o.AppendLine("    <Keyword>" + Keyword + "</Keyword>");

        String rootNamespace = ProjectName;
        if (rootNamespace.IndexOf('.') != -1)
            rootNamespace = rootNamespace.Substring(0, rootNamespace.IndexOf('.'));

        o.AppendLine("    <RootNamespace>" + rootNamespace + "</RootNamespace>");

        if (!String.IsNullOrEmpty(WindowsTargetPlatformVersion))
            o.AppendLine("    <WindowsTargetPlatformVersion>" + WindowsTargetPlatformVersion + "</WindowsTargetPlatformVersion>");

        if (Keyword == EKeyword.MFCProj )
            o.AppendLine("    <Keyword>" + Keyword + "</Keyword>");


        bool bIsAndroidProject = Keyword == EKeyword.Android;
        if (bIsAndroidProject)
            o.AppendLine("    <DefaultLanguage>en-US</DefaultLanguage>");

        if (bIsAndroidProject || bIsAntPackagingProject || bIsGradlePackagingProject )
        {
            String mvsv;
            switch (fileFormatVersion)
            {
                case 2015: mvsv = "14.0"; break;
                case 2017: mvsv = "15.0"; break;
                // Try to predict the future somehow
                default:   mvsv = (((fileFormatVersion - 2015) / 2) + 14).ToString() + ".0"; break;
            }
            o.AppendLine("    <MinimumVisualStudioVersion>" + mvsv + "</MinimumVisualStudioVersion>");
        }

        if (bIsAntPackagingProject || bIsGradlePackagingProject)
            o.AppendLine("    <ProjectVersion>1.0</ProjectVersion>");

        if( bIsAntPackagingProject || bIsGradlePackagingProject )
        {
            o.AppendLine( "    <ProjectGuid>" + ProjectGuid + "</ProjectGuid>" );
            // This is needed to disable ABI compatibility check when deploying to device.
            // Otherwise results in error: Error installing the package. The package ABI '' is incompatible with the ABI 'arm64v8a' of device
            o.AppendLine( "    <_PackagingProjectWithoutNativeComponent>true</_PackagingProjectWithoutNativeComponent>" );
        }

        if (bIsAndroidProject)
            o.AppendLine("    <ApplicationType>Android</ApplicationType>");

        if (bIsAndroidProject)
        {
            String atv;
            switch (fileFormatVersion)
            {
                case 2015: atv = "2.0"; break;
                case 2017: atv = "3.0"; break;
                // Try to predict the future somehow
                default: atv = (((fileFormatVersion - 2015) / 2) + 2).ToString() + ".0"; break;
            }

            o.AppendLine("    <ApplicationTypeRevision>" + atv + "</ApplicationTypeRevision>");
        }

        o.AppendLine("  </PropertyGroup>");

        // Some mysterious xml tag.
        String propsPath = "$(VCTargetsPath)\\Microsoft.Cpp";

        if (bIsAntPackagingProject || bIsGradlePackagingProject)
            propsPath = "$(AndroidTargetsPath)\\Android";

        o.AppendLine("  <Import Project=\"" + propsPath + ".Default.props\" />");

        //
        // Dump general information.
        //
        foreach (String confName in getSortedConfigurations(Keyword == EKeyword.Android))
        {
            int iConf = configurations.IndexOf(confName);
            Configuration conf = projectConfig[iConf];

            o.AppendLine("  <PropertyGroup " + condition(confName) + " Label=\"Configuration\">");

            switch (Keyword)
            {
                case EKeyword.AntPackage:
                    o.AppendLine("    <UseDebugLibraries>" + conf.UseDebugLibraries.ToString().ToLower() + "</UseDebugLibraries>");
                    o.AppendLine("    <ConfigurationType>Application</ConfigurationType>"); // Why this line is needed anyway?
                    if (conf.AndroidAPILevel != null && conf.AndroidAPILevel != Configuration.getAndroidAPILevelDefault(confName))
                        o.AppendLine("    <AndroidAPILevel>" + conf.AndroidAPILevel + "</AndroidAPILevel>");
                    break;

                case EKeyword.GradlePackage:
                    o.AppendLine("    <ConfigurationType>Application</ConfigurationType>");
                    break;

                default:
                    o.AppendLine("    <ConfigurationType>" + conf.ConfigurationType.ToString() + "</ConfigurationType>");
                    o.AppendLine("    <UseDebugLibraries>" + conf.UseDebugLibraries.ToString().ToLower() + "</UseDebugLibraries>");
                    break;
            }

            if (!bIsAntPackagingProject && !bIsGradlePackagingProject)
            {
                String pts = conf.PlatformToolset;
                if (pts == null) pts = conf.getPlatformToolsetDefault(this);
                o.AppendLine("    <PlatformToolset>" + pts + "</PlatformToolset>");

                if (Keyword == EKeyword.Android)
                {
                    if (conf.AndroidAPILevel != null && conf.AndroidAPILevel != Configuration.getAndroidAPILevelDefault(confName))
                        o.AppendLine("    <AndroidAPILevel>" + conf.AndroidAPILevel + "</AndroidAPILevel>");

                    if (conf.UseOfStl != EUseOfStl.gnustl_static)
                    {
                        String v = typeof(EUseOfStl).GetField(conf.UseOfStl.ToString()).GetCustomAttribute<DescriptionAttribute>().Description;
                        o.AppendLine("    <UseOfStl>" + v + "</UseOfStl>");
                    }
                    
                    if( conf.ThumbMode != EThumbMode.NotSpecified && conf.ThumbMode != Configuration.getThumbModeDefault(confName))
                        o.AppendLine( "    <ThumbMode>" + conf.ThumbMode + "</ThumbMode>" );
                }
            }

            if (conf.WholeProgramOptimization != EWholeProgramOptimization.NoWholeProgramOptimization)
            {
                String value = typeof(EWholeProgramOptimization).GetMember(conf.WholeProgramOptimization.ToString())[0].GetCustomAttribute<DescriptionAttribute>().Description;
                o.AppendLine("    <WholeProgramOptimization>" + value + "</WholeProgramOptimization>");
            }
            if (Keyword == EKeyword.Win32Proj || Keyword == EKeyword.MFCProj)
                o.AppendLine("    <CharacterSet>" + conf.CharacterSet.ToString() + "</CharacterSet>");

            if (Keyword == EKeyword.MFCProj && conf.UseOfMfc != EUseOfMfc.None)
                o.AppendLine("    <UseOfMfc>" + conf.UseOfMfc + "</UseOfMfc>");

            o.AppendLine("  </PropertyGroup>");
        } //for

        o.AppendLine("  <Import Project=\"" + propsPath + ".props\" />");

        if (bIsGradlePackagingProject)
        {
            o.AppendLine( "  <ItemDefinitionGroup>" );
            o.AppendLine( "    <GradlePackage>" );
            
            foreach( String confName in getSortedConfigurations( false ) )
            {
                int iConf = configurations.IndexOf( confName );
                Configuration conf = projectConfig[iConf];

                if( conf.ApkFileName != null )
                    o.AppendLine( "  <ApkFileName " + condition( confName ) + ">" + conf.ApkFileName + "</ApkFileName>" );
            } //foreach

            if( GradlePackage.GradleVersion != null)
                o.AppendLine( "    <GradleVersion>" + GradlePackage.GradleVersion + "</GradleVersion>" );

            if( !GradlePackage.IsProjectDirectoryDefault() )
                o.AppendLine( "    <ProjectDirectory>" + GradlePackage.ProjectDirectory + "</ProjectDirectory>" );

            o.AppendLine( "    </GradlePackage>" );
            o.AppendLine( "  </ItemDefinitionGroup>" );
        }

        switch (Keyword)
        {
            case EKeyword.GradlePackage:
            case EKeyword.AntPackage:
                o.AppendLine("  <ImportGroup Label=\"ExtensionSettings\" />");
                break;
            default:
                o.AppendLine("  <ImportGroup Label=\"ExtensionSettings\">");
                o.AppendLine("  </ImportGroup>");
                break;
        }

        if (Keyword == EKeyword.Android)
        {
            o.AppendLine("  <ImportGroup Label=\"Shared\">");
            o.AppendLine("  </ImportGroup>");
        }
        else if (Keyword == EKeyword.AntPackage)
        {
            o.AppendLine("  <ImportGroup Label=\"Shared\" />");
        }

        if (!bIsAntPackagingProject && !bIsGradlePackagingProject)
            foreach (String confName in getSortedConfigurations(true))
            {
                o.AppendLine("  <ImportGroup Label=\"PropertySheets\" Condition=\"'$(Configuration)|$(Platform)'=='" + confName + "'\">");
                o.AppendLine("    <Import Project=\"$(UserRootDir)\\Microsoft.Cpp.$(Platform).user.props\" Condition=\"exists('$(UserRootDir)\\Microsoft.Cpp.$(Platform).user.props')\" Label=\"LocalAppDataPlatform\" />");
                o.AppendLine("  </ImportGroup>");
            } //foreach

        //
        // Dump compiler and linker options.
        //
        o.AppendLine("  <PropertyGroup Label=\"UserMacros\" />");

        if (bIsAntPackagingProject)
            o.AppendLine("  <PropertyGroup />");

        foreach (String confName in getSortedConfigurations(false))
        {
            int iConf = configurations.IndexOf(confName);
            Configuration conf = projectConfig[iConf];

            bool bAppendLinkIncremental = Keyword != EKeyword.Android;
            bool bAppendOutDir = conf.OutDir != null && conf.OutDir != conf.getOutDirDefault(this);
            bool bAppendIntDir = conf.IntDir != null && conf.IntDir != conf.getOutDirDefault(this);
            bool bAppendTargetName = conf.TargetName != null && conf.TargetName != conf.getTargetNameDefault(this);
            bool bAppendTargetExt = conf.TargetExt != null;
            bool bAppendAny = conf.IncludePath != "";
            if (bAppendAny)
                bAppendAny = conf.LibraryPath != "";

            // Empty node.
            if (!(bAppendLinkIncremental || bAppendOutDir || bAppendIntDir || bAppendTargetName || bAppendTargetExt || bAppendAny))
            {
                o.AppendLine("  <PropertyGroup " + condition(confName) + " />");
                continue;
            }

            o.AppendLine("  <PropertyGroup " + condition(confName) + ">");

            if (bAppendLinkIncremental)
                o.AppendLine("    <LinkIncremental>" + conf.LinkIncremental.ToString().ToLower() + "</LinkIncremental>");

            if( bAppendOutDir )
            {
                String outDir = conf.OutDir;

                if( Keyword == EKeyword.Android || Keyword == EKeyword.GradlePackage || Keyword == EKeyword.AntPackage )
                {
                    // Visual studio bug: Debugging fails to attach if directory is not starting with $(ProjectDir) or $(SolutionDir)
                    // or Access to the path 'bin\Debug_ARM\.gdb' is denied.
                    // For Windows projects specifying using relative path implies $(ProjectDir) prefix before that.
                    if( !IsPathProjectOrSolutionRooted(outDir) )
                        outDir = "$(ProjectDir)" + outDir;
                }
                
                o.AppendLine( "    <OutDir>" + outDir + "</OutDir>" );
            }

            if (bAppendIntDir)
                o.AppendLine("    <IntDir>" + conf.IntDir + "</IntDir>");

            if(conf.IncludePath != "")
                o.AppendLine("    <IncludePath>" + conf.IncludePath + ";$(IncludePath)</IncludePath>");

            if (bAppendTargetName)
                o.AppendLine("    <TargetName>" + conf.TargetName + "</TargetName>");

            if (bAppendTargetExt)
                o.AppendLine("    <TargetExt>" + conf.TargetExt + "</TargetExt>");

            if (conf.LibraryPath != "")
                o.AppendLine("    <LibraryPath>" + conf.LibraryPath + ";$(LibraryPath)</LibraryPath>");

            o.AppendLine("  </PropertyGroup>");
        } //for

        if( !bIsGradlePackagingProject )
        for (int iConf = 0; iConf < configurations.Count; iConf++)
        {
            String confName = configurations[iConf];
            Configuration conf = projectConfig[iConf];
            o.AppendLine("  <ItemDefinitionGroup " + condition(confName) + ">");

            if (bIsAntPackagingProject)
            {
                o.AppendLine("    <AntPackage>");
                o.AppendLine("      <AndroidAppLibName>$(RootNamespace)</AndroidAppLibName>");
                o.AppendLine("    </AntPackage>");
                o.AppendLine("  </ItemDefinitionGroup>");
                continue;
            }

            o.AppendLine("    <ClCompile>");

            if( conf.PrecompiledHeader != EPrecompiledHeaderUse.ProjectDefault )
                o.AppendLine("      <PrecompiledHeader>" + conf.PrecompiledHeader.ToString() + "</PrecompiledHeader>");

            // No need to specify if it's default header file.
            if (conf.PrecompiledHeader == EPrecompiledHeaderUse.Use && conf.PrecompiledHeaderFile != "stdafx.h")
                o.AppendLine("      <PrecompiledHeaderFile>" + conf.PrecompiledHeaderFile + "</PrecompiledHeaderFile>");

            if (Keyword == EKeyword.Android)
                o.AppendLine("      <CompileAs>" + conf.CompileAs.ToString() + "</CompileAs>");

            // No need to specify as it's Visual studio default.
            if (conf.WarningLevel != EWarningLevel.Level1)
                o.AppendLine("      <WarningLevel>" + conf.WarningLevel + "</WarningLevel>");

            o.AppendLine("      <Optimization>" + conf.Optimization + "</Optimization>");

            if (conf.FunctionLevelLinking)   //premake5 is not generating those, I guess disabled if it's value is false.
                o.AppendLine("      <FunctionLevelLinking>true</FunctionLevelLinking>");
            if (conf.IntrinsicFunctions && Keyword != EKeyword.Android)
                o.AppendLine("      <IntrinsicFunctions>true</IntrinsicFunctions>");

            DumpConfiguration(conf);

            o.AppendLine("    </ClCompile>");

            o.AppendLine("    <Link>");

            if (Keyword != EKeyword.Android)
                o.AppendLine("      <SubSystem>" + conf.SubSystem + "</SubSystem>");

            if (Keyword != EKeyword.Android)
            {
                String v = "";
                switch (conf.GenerateDebugInformation)
                {
                    default:
                    case EGenerateDebugInformation.No: v = "false"; break;
                    case EGenerateDebugInformation.OptimizeForDebugging: v = "true"; break;
                    case EGenerateDebugInformation.OptimizeForFasterLinking: v = "DebugFastLink"; break;
                }
                o.AppendLine("      <GenerateDebugInformation>" + v + "</GenerateDebugInformation>");

                if( conf.ModuleDefinitionFile != "" )
                    o.AppendLine("      <ModuleDefinitionFile>" + conf.ModuleDefinitionFile + "</ModuleDefinitionFile>");
            } //if

            // Link libraries.
            String links = conf.AdditionalDependencies;

            if (conf.LibraryDependencies.Length != 0)
            {
                if (links.Length != 0) links += ";";
                links += conf.LibraryDependencies;
            }

            if (links.Length != 0)
            {
                if (Keyword == EKeyword.Win32Proj)
                {
                    o.AppendLine("      <AdditionalDependencies>" + links + ";%(AdditionalDependencies)</AdditionalDependencies>");
                }
                else
                {
                    o.AppendLine("      <LibraryDependencies>%(LibraryDependencies);" + links + ";</LibraryDependencies>");
                    //o.AppendLine("      <AdditionalDependencies>");
                    //o.AppendLine("      </AdditionalDependencies>");
                }
            }

            if (conf.EnableCOMDATFolding)
                o.AppendLine("      <EnableCOMDATFolding>true</EnableCOMDATFolding>");
            if (conf.OptimizeReferences)
                o.AppendLine("      <OptimizeReferences>true</OptimizeReferences>");
            if (conf.Profile)
                o.AppendLine("      <Profile>true</Profile>");

            if (conf.AdditionalLibraryDirectories.Length != 0)
                o.AppendLine("      <AdditionalLibraryDirectories>" + conf.AdditionalLibraryDirectories + "</AdditionalLibraryDirectories>");

            if (conf.ConfigurationType != EConfigurationType.StaticLibrary && conf.Link_AdditionalOptions.Length != 0)
                o.AppendLine("      <AdditionalOptions>" + conf.Link_AdditionalOptions + " %(AdditionalOptions)</AdditionalOptions>");

            // OutputFile ?
            o.AppendLine("    </Link>");

            // PreBuildEvent, PreLinkEvent, PostBuildEvent
            foreach (String step in new String[] { "PreBuild", "PreLink", "PostBuild" })
            {
                BuildEvent bevent = (BuildEvent)conf.GetType().GetField(step + "Event").GetValue(conf);

                if (bevent.Command != "")
                { 
                    o.AppendLine("    <" + step + "Event>");
                    o.AppendLine("      <Command>" + XmlEscape(bevent.Command) + "</Command>");
                    o.AppendLine("    </" + step + "Event>");
                }
            } //foreach

            if (conf.ConfigurationType == EConfigurationType.StaticLibrary && conf.Link_AdditionalOptions.Length != 0)
            { 
                o.AppendLine("    <Lib>");

                if (conf.Link_AdditionalOptions.Length != 0)
                    o.AppendLine("      <AdditionalOptions>" + conf.Link_AdditionalOptions + "</AdditionalOptions>");

                o.AppendLine("    </Lib>");
            }

            o.AppendLine("  </ItemDefinitionGroup>");
        } //for


        IncludeType inctype = IncludeType.Invalid;
        bool bItemGroupOpened = false;
        //
        // Dump files array
        //
        foreach (FileInfo fi in files)
        {
            bool bHasCustomBuildStep = fi.fileConfig.Where(x => x.customBuildRule != null).FirstOrDefault() != null;

            if (bHasCustomBuildStep)
                fi.includeType = IncludeType.CustomBuild;

            if (simplifyForGroupReopen(inctype) != simplifyForGroupReopen(fi.includeType))
            {
                if (bItemGroupOpened)
                    o.AppendLine("  </ItemGroup>");

                o.AppendLine("  <ItemGroup>");
                bItemGroupOpened = true;
                inctype = fi.includeType;
            } //if

            o.Append("    <" + fi.includeType + " Include=\"" + fi.relativePath.Replace("/", "\\") + "\"");

            if (fi.includeType == IncludeType.ProjectReference)
            {
                o.AppendLine(">");
                o.AppendLine("      <Project>" + fi.Project + "</Project>");
                o.AppendLine("    </ProjectReference>");
                continue;
            }

            if (fi.includeType == IncludeType.CustomBuild)
            {
                o.AppendLine(">");

                foreach (String confName in getSortedConfigurations(false))
                {
                    int iConf = configurations.IndexOf(confName);
                    CustomBuildRule cbr = fi.fileConfig[iConf].customBuildRule;

                    if (cbr == null)
                    {
                        // If we don't have custom build rule defined, then disable custom build step.
                        o.AppendLine("      <ExcludedFromBuild " + condition(confName) + ">true</ExcludedFromBuild>");
                        continue;
                    }
                    o.AppendLine("      <Command " + condition(confName) + ">" + XmlEscape(cbr.Command) + "</Command>");

                    if (cbr.AdditionalInputs != "")
                        o.AppendLine("      <AdditionalInputs " + condition(confName) + ">" + XmlEscape(cbr.AdditionalInputs) + ";%(AdditionalInputs)</AdditionalInputs>");

                    o.AppendLine("      <Outputs " + condition(confName) + ">" + cbr.Outputs + "</Outputs>");
                    if (cbr.Message != "Performing Custom Build Tools")
                        o.AppendLine("      <Message " + condition(confName) + ">" + cbr.Message + "</Message>");
                } //foreach

                foreach (String confName in getSortedConfigurations(false))
                {
                    int iConf = configurations.IndexOf(confName);
                    CustomBuildRule cbr = fi.fileConfig[iConf].customBuildRule;
                    
                    if (cbr != null && !cbr.LinkObjects)
                        o.AppendLine("      <LinkObjects " + condition(confName) + ">false</LinkObjects>");
                } //foreach

                o.AppendLine("    </CustomBuild>");
                continue;
            }

            //------------------------------------------------------------
            // If compileAs is not specified for Android, we set it 
            // according to file extension.
            //------------------------------------------------------------
            if (Keyword == EKeyword.Android)
            {
                String ext = Path.GetExtension(fi.relativePath).ToLower();
                ECompileAs? compAs = null;

                for (int iConf = 0; iConf < configurations.Count; iConf++)
                {
                    Configuration conf = projectConfig[iConf];
                    if (conf.CompileAs != ECompileAs.CompileAsC && ext == ".c")
                        compAs = ECompileAs.CompileAsC;

                    if (conf.CompileAs == ECompileAs.CompileAsC && ext == ".cpp")
                        compAs = ECompileAs.CompileAsCpp;
                }

                if (compAs.HasValue)
                { 
                    updateFileConfigurations(fi);
                    foreach (var fconf in fi.fileConfig)
                        if( fconf.CompileAs == ECompileAs.Default)      // Can be overridden by end-user
                            fconf.CompileAs = compAs.Value;
                }
            }


            if (fi.fileConfig.Count == 0)
            {
                o.AppendLine(" />");
            }
            else
            {
                o.AppendLine(">");

                // We have file specific configuration options
                for (int iConf = 0; iConf < configurations.Count; iConf++)
                {
                    String confName = configurations[iConf];
                    FileConfigurationInfo conf = fi.fileConfig[iConf];
                    DumpConfiguration(conf, confName, projectConfig[iConf]);
                }
                o.AppendLine("    </" + fi.includeType + ">");
            } //if-else
        } //for

        if (bItemGroupOpened)
            o.AppendLine("  </ItemGroup>");


        o.AppendLine("  <Import Project=\"" + propsPath + ".targets\" />");

        if (bIsPackagingProject)
        {
            o.AppendLine("  <ImportGroup Label=\"ExtensionTargets\" />");
        }
        else
        {
            o.AppendLine("  <ImportGroup Label=\"ExtensionTargets\">");
            o.AppendLine("  </ImportGroup>");
        }
        o.AppendLine("</Project>");

        String projectsFile = o.ToString();


        //-----------------------------------------------------------------------------------------------------------
        // For android projects we don't need to generate .filters file at all.
        //-----------------------------------------------------------------------------------------------------------
        if (Keyword == EKeyword.GradlePackage)
        { 
            UpdateFile(projectPath, projectsFile, uinfo, false);
            return;
        }

        //-----------------------------------------------------------------------------------------------------------
        // .filters file generation.
        //-----------------------------------------------------------------------------------------------------------
        String filtersPath = projectPath + ".filters";
        o = new StringBuilder();
        o.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");

        o.AppendLine("<Project ToolsVersion=\"4.0\" xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">");

        List<String> folderList = files.Where( x => x.includeType != IncludeType.ProjectReference).
            Select(x => Path.GetDirectoryName(x.relativePath.Replace("/", "\\"))).Where(x => x != "").Distinct().ToList();
        int nCharsToSkip = 0;

        //
        //  We try to simply path'es so not so many upper folder references would exists.
        //
        while (true)
        {
            bool bHasUpReference = folderList.Where(x => x.StartsWith("..\\")).FirstOrDefault() != null;
            bool bAllUpReferences = false;
            if (bHasUpReference) bAllUpReferences = folderList.Where(x => !x.StartsWith("..\\")).FirstOrDefault() == null;

            if (bHasUpReference && bAllUpReferences)
            {
                folderList = folderList.Select(x => x.Substring(3)).ToList();
                nCharsToSkip += 3;
            }
            else
            {
                break;
            }
        }

        //
        //  Folder list to create for .filter file.
        //
        List<String> folderListToCreate = new List<string>();

        foreach (String dir in folderList)
        {
            if (folderListToCreate.Contains(dir))
                continue;

            String dirSoFar = "";
            foreach (String part in dir.Split(new char[] { '\\' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (dirSoFar.Length != 0) dirSoFar += "\\";
                dirSoFar += part;

                if (!folderListToCreate.Contains(dirSoFar))
                    folderListToCreate.Add(dirSoFar);
            }
        }

        folderListToCreate.Sort();

        if (folderListToCreate.Count != 0)
        {
            o.AppendLine("  <ItemGroup>");
            foreach (String dir in folderListToCreate)
            {
                o.AppendLine("    <Filter Include=\"" + XmlEscape(dir) + "\">");
                o.AppendLine("      <UniqueIdentifier>" + SolutionProjectBuilder.GenerateGuid("folder:" + dir) + "</UniqueIdentifier>");
                o.AppendLine("    </Filter>");
            }
            o.AppendLine("  </ItemGroup>");
        } //if

        //
        //  Dump file + folder path list.
        //
        foreach (String enumName in typeof(IncludeType).GetEnumNames())
        {
            IncludeType elemType = (IncludeType)Enum.Parse(typeof(IncludeType), enumName);

            // Should not reflect to filter file.
            if (elemType == IncludeType.ProjectReference)
                continue;

            List<String> fileList = files.Where(x => x.includeType == elemType).Select(x => x.relativePath.Replace("/", "\\")).ToList();

            if (fileList.Count == 0)
                continue;

            fileList.Sort();

            o.AppendLine("  <ItemGroup>");
            foreach (String file in fileList)
            {
                String dir = Path.GetDirectoryName(file);
                if( dir.Length > nCharsToSkip)
                    dir = dir.Substring(nCharsToSkip);
                o.Append("    <" + enumName + " Include=\"" + XmlEscape(file) + "\"");

                if (dir == "")
                {
                    o.AppendLine(" />");
                    continue;
                }
                else
                {
                    o.AppendLine(">");
                }
                
                o.AppendLine("      <Filter>" + XmlEscape(dir) + "</Filter>");
                o.AppendLine("    </" + enumName + ">");
            }
            o.AppendLine("  </ItemGroup>");
        }
        o.AppendLine("</Project>");

        String filtersFile = o.ToString();
        //
        // We first save .filters file, because Visual Studio typically does not reload project if only .filters file was changed.
        // Then if .filters file changes, we force project save as well.
        //
        bool bSaved = UpdateFile(filtersPath, filtersFile, uinfo);
        UpdateFile(projectPath, projectsFile, uinfo, bSaved);
    } //SaveProject

    /// <summary>
    /// Save file contents if file were updated.
    /// </summary>
    /// <param name="path">Path to save</param>
    /// <param name="newFileContents">new file contents to save</param>
    /// <param name="force">true if force to save file</param>
    /// <returns>true if file was updated.</returns>
    private bool UpdateFile(string path, String newFileContents, UpdateInfo uinfo, bool force = false)
    {
        //
        // Write project itself.
        //
        String currentFileContents = "";
        if (File.Exists(path)) currentFileContents = File.ReadAllText(path);

        // Projects & filters files uses windows linefeed (0x0D / 0x0A), but some git repositories might store projects with
        // incorrect linefeeds, Visual studio will put correct linefeeds when saving.
        
        //
        // Save only if needed.
        //
        if (currentFileContents == newFileContents && !force)
        {
            uinfo.MarkFileUpdated(path, false);
            return false;
        }
        else
        {
            if (SolutionProjectBuilder.isDeveloper() && File.Exists(path)) File.Copy(path, path + ".bkp", true);
            File.WriteAllText(path, newFileContents, Encoding.UTF8);
            uinfo.MarkFileUpdated(path, true);
            return true;
        } //if-else
    } //UpdateFile

    /// <summary>
    /// Checks if path is rooted against SolutionDir or ProjectDir
    /// </summary>
    /// <param name="path">path. By default same as project directory.</param>
    /// <returns>true if rooted.</returns>
    public static bool IsPathProjectOrSolutionRooted( String path)
    {
        if( path.StartsWith( "$(ProjectDir)", StringComparison.CurrentCultureIgnoreCase ) ||
            path.StartsWith( "$(SolutionDir)", StringComparison.CurrentCultureIgnoreCase ) )
            return true;

        return false;
    }



} //Project

