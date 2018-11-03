using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using System.Xml.Linq;
using System.Collections;
using System.Reflection;
using System.Security.Cryptography;

public enum PrecompiledHeaderUse
{
    Create,
    Use,
    NotUsing = 0 //enum default is 0.
}

public enum IncludeType
{
    /// <summary>
    /// Header file (.h)
    /// </summary>
    ClInclude,

    /// <summary>
    /// Any custom file with custom build step
    /// </summary>
    CustomBuild,

    /// <summary>
    /// Source codes (.cpp) files
    /// </summary>
    ClCompile,

    /// <summary>
    /// .def / .bat
    /// </summary>
    None,

    /// <summary>
    /// .txt files.
    /// </summary>
    Text,

    /// <summary>
    /// .rc / resource files.
    /// </summary>
    ResourceCompile,

    /// <summary>
    /// .ico files.
    /// </summary>
    Image,

    // Following enumerations are used in android packaging project (.androidproj)
    Content,
    AntBuildXml,
    AndroidManifest,
    AntProjectPropertiesFile,
    ProjectReference
}


[DebuggerDisplay("{relativePath} ({includeType})")]
public class FileInfo
{
    public IncludeType includeType;

    public String relativePath;

    /// <summary>
    /// Pre-configuration list.
    /// </summary>
    public List<PrecompiledHeaderUse> phUse = new List<PrecompiledHeaderUse>();

    /// <summary>
    /// Pre-configuration list of defines. ';' separated strings. null if not used.
    /// </summary>
    public List<String> PreprocessorDefinitions;

    /// <summary>
    /// Pre-configuration list of include directories;
    /// </summary>
    public List<String> AdditionalIncludeDirectories;

    /// <summary>
    /// Pre-configuration list of output filename
    /// </summary>
    public List<String> ObjectFileName;
    public List<String> XMLDocumentationFileName;

    /// <summary>
    /// null if not in use, non-null if custom build tool is in use.
    /// </summary>
    public List<CustomBuildToolProperties> customBuildTool;
}


[DebuggerDisplay("Custom Build Tool '{Message}'")]
public class CustomBuildToolProperties
{
    /// <summary>
    /// Visual studio: Command line
    /// </summary>
    public String Command = "";
    /// <summary>
    /// Visual studio: description
    /// </summary>
    public String Message = "";
    /// <summary>
    /// Visual studio: outputs
    /// </summary>
    public String Outputs = "";
    /// <summary>
    /// Visual studio: additional dependencies
    /// </summary>
    public String AdditionalInputs = "";
}


[DebuggerDisplay("{ProjectName}, {RelativePath}, {ProjectGuid}")]
public class Project
{
    /// <summary>
    /// true if it's folder (in solution), false if it's project. (default)
    /// </summary>
    public bool bIsFolder = false;


    /// <summary>
    /// Made as a property so can be set over reflection.
    /// </summary>
    public String ProjectHostGuid
    { 
        get
        {
            if (bIsFolder)
                return "{2150E333-8FDC-42A3-9474-1A3956D46DE8}";
            else
                return "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}";
        }
        set
        {
            switch (value)
            {
                case "{2150E333-8FDC-42A3-9474-1A3956D46DE8}": bIsFolder = true; break;
                case "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}": bIsFolder = false; break;
                default:
                    throw new Exception2("Invalid project host guid '" + value + "'");
            }
        }
    }

    public String Keyword;
    
    [XmlIgnore]
    public List<Project> nodes = new List<Project>();   // Child nodes (Empty folder also does not have any children)
    [XmlIgnore]
    public Project parent;                              // Points to folder which contains given project

    public string ProjectName;
    public string RelativePath;
    public string language;                             // if null - RelativePath includes file extension, if non-null - "C++" or "C#" - defines project file extension.

    /// <summary>
    /// gets relative path based on programming language
    /// </summary>
    /// <returns></returns>
    public String getRelativePath()
    {
        String path = RelativePath.Replace("/", "\\");

        if (language != null)
        {
            switch (language)
            {
                case "C++": return path + ".vcxproj";
                case "C#": return path + ".csproj";
            }
        }
    
        return path;    
    }


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
        return "Project(\"" + ((bIsFolder) ? "Folder":"Project") + "\") = \"" + ProjectName + "\", \"" + RelativePath + "\", \"" + ProjectGuid + "\"";
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
    static String getConfiguration(XElement node)
    {
        String config = RegexExract("^'\\$\\(Configuration\\)\\|\\$\\(Platform\\)'=='(.*)'", node.Attribute("Condition").Value)[0];
        return config;
    }


    void confListInit<T>(ref List<T> list, T defT = default(T) )
    {
        list = new List<T>(configurations.Count);

        while (list.Count < configurations.Count)
            list.Add(defT);
    }


    /// <summary>
    /// Extracts compilation options for single cpp/cs file.
    /// </summary>
    /// <param name="clCompile">xml node from where to get</param>
    /// <param name="file2compile">compiler options to fill out</param>
    void ExtractCompileOptions( XElement clCompile, FileInfo file2compile )
    {
        foreach (XElement fileProps in clCompile.Elements())
        {
            String config = getConfiguration(fileProps);

            int iCfg = configurations.IndexOf(config);
            if (iCfg == -1)
                continue;           // Invalid configuration string specified.


            String localName = fileProps.Name.LocalName;

            if (localName == "PrecompiledHeader")
            {
                confListInit(ref file2compile.phUse);
                file2compile.phUse[iCfg] = (PrecompiledHeaderUse)Enum.Parse(typeof(PrecompiledHeaderUse), fileProps.Value);
                continue;
            }

            //
            // PreprocessorDefinitions, AdditionalIncludeDirectories, ObjectFileName, XMLDocumentationFileName
            //
            FieldInfo fi = typeof(FileInfo).GetField(fileProps.Name.LocalName);
            if (fi == null)
            {
                if (Debugger.IsAttached) Debugger.Break();
                continue;
            }

            List<String> list = (List<String>)fi.GetValue(file2compile);
            bool bSet = list == null;
            confListInit(ref list, "");
            list[iCfg] = fileProps.Value;
            
            if( bSet )
                fi.SetValue(file2compile, list);
        } //foreach
    } //ExtractCompileOptions

    static void CopyField(object o2set, String field, XElement node)
    { 
        o2set.GetType().GetField(field).SetValue(o2set, node.Element(node.Document.Root.Name.Namespace + field)?.Value);
    }


    /// <summary>
    /// Loads project. If project exists in solution, it's loaded in same instance.
    /// </summary>
    /// <param name="solution">Solution if any exists, null if not available.</param>
    static public Project LoadProject(Solution solution, String path, Project project = null)
    {
        if (path == null)
            path = Path.GetDirectoryName(solution.path) + "\\" + project.RelativePath;

        if (project == null)
            project = new Project();

        if (!File.Exists(path))
            return null;

        XDocument p = XDocument.Load(path);

        foreach (XElement node in p.Root.Elements())
        {
            String lname = node.Name.LocalName;

            switch (lname)
            {
                case "ItemGroup":
                    // ProjectConfiguration has Configuration & Platform sub nodes, but they cannot be reconfigured to anything else then this attribute.
                    if (node.Attribute("Label")?.Value == "ProjectConfigurations")
                    {
                        project.configurations = node.Elements().Select(x => x.Attribute("Include").Value).ToList();
                    }
                    else {
                        //
                        // .h / .cpp / custom build files are picked up here.
                        //
                        foreach (XElement igNode in node.Elements())
                        {
                            FileInfo f = new FileInfo();
                            f.includeType = (IncludeType)Enum.Parse(typeof(IncludeType), igNode.Name.LocalName);
                            f.relativePath = igNode.Attribute("Include").Value;
                            
                            if(f.includeType == IncludeType.ClCompile )
                                project.ExtractCompileOptions(igNode, f);

                            //
                            // Custom build tool
                            //
                            if (f.includeType == IncludeType.CustomBuild)
                            {
                                f.customBuildTool = new List<CustomBuildToolProperties>(project.configurations.Count);
                                
                                while (f.customBuildTool.Count < project.configurations.Count)
                                    f.customBuildTool.Add(new CustomBuildToolProperties());

                                foreach(XElement custbNode in igNode.Elements())
                                {
                                    FieldInfo fi = typeof(CustomBuildToolProperties).GetField(custbNode.Name.LocalName);
                                    if (fi == null) continue;

                                    String config = getConfiguration(custbNode);
                                    int iCfg = project.configurations.IndexOf(config);

                                    if (iCfg == -1)
                                        continue;

                                    fi.SetValue(f.customBuildTool[iCfg], custbNode.Value);
                                } //for
                            } //if

                            project.files.Add(f);
                        } //for
                    }
                    break;
                
                case "PropertyGroup":
                    {
                        if (node.Attribute("Label")?.Value == "Globals")
                        {
                            foreach (String field in new String[] { "ProjectGuid", "Keyword" /*, "RootNamespace"*/ })
                                CopyField(project, field, node);
                        }
                    }
                    break;
            } //switch
        } //foreach

        return project;
    } //LoadProject
}

/// <summary>
/// .sln loaded into class.
/// </summary>
public class Solution
{
    [XmlIgnore]
    public String path;                                 // File path from where solution was loaded.
    double slnVer;                                      // 11.00 - vs2010, 12.00 - vs2015
    
    public String VisualStudioVersion;                  // null for old visual studio's
    public String MinimumVisualStudioVersion;           // null for old visual studio's
    
    public List<Project> projects = new List<Project>();
    
    /// <summary>
    /// List of configuration list, in form "{Configuration}|{Platform}", for example "Release|Win32".
    /// To extract individual platforms / configuration list, use following functions.
    /// </summary>
    public List<String> configurations = new List<string>();

    public IEnumerable<String> getPlatforms()
    { 
        return configurations.Select(x => "\"" + x.Split('|')[0]).Distinct();
    }

    public IEnumerable<String> getConfigurations()
    {
        return configurations.Select(x => "\"" + x.Split('|')[1]).Distinct();
    }



    public Solution() { }

    /// <summary>
    /// Loads visual studio .sln solution
    /// </summary>
    /// <exception cref="System.IO.FileNotFoundException">The file specified in path was not found.</exception>
    static public Solution LoadSolution(string path)
    {
        Solution s = new Solution();
        s.path = path;

        String slnTxt = File.ReadAllText(path);
        s.slnVer = Double.Parse( Regex.Match(slnTxt, "[\r\n]+Microsoft Visual Studio Solution File, Format Version ([0-9.]+)", RegexOptions.Multiline).Groups[1].Value, CultureInfo.InvariantCulture);

        foreach (String line in new String[] { "VisualStudioVersion", "MinimumVisualStudioVersion" })
        {
            var m = Regex.Match(slnTxt, "^" + line + " = ([0-9.]+)", RegexOptions.Multiline);
            String v = null;
            if (m.Success)
                v = m.Groups[1].Value;

            s.GetType().GetField(line).SetValue(s, v);
        }

        Regex reProjects = new Regex(
            "Project\\(\"(?<ProjectHostGuid>{[A-F0-9-]+})\"\\) = \"(?<ProjectName>.*?)\", \"(?<RelativePath>.*?)\", \"(?<ProjectGuid>{[A-F0-9-]+})\"[\r\n]*(?<dependencies>.*?)EndProject[\r\n]+", 
            RegexOptions.Singleline);

        
        reProjects.Replace(slnTxt, new MatchEvaluator(m =>
            {
                Project p = new Project();

                foreach (String g in reProjects.GetGroupNames())
                {
                    if (g == "0")   //"0" - RegEx special kind of group
                        continue;

                    //
                    // ProjectHostGuid, ProjectName, RelativePath, ProjectGuid fields/properties are set here.
                    //
                    String v = m.Groups[g].ToString();
                    if (g != "dependencies")
                    {
                        FieldInfo fi = p.GetType().GetField(g);
                        if (fi != null)
                        {
                            fi.SetValue(p, v);
                        }
                        else
                        {
                            p.GetType().GetProperty(g).SetValue(p, v);
                        }
                        continue;
                    }

                    if (v == "")    // No dependencies set
                        continue;

                    String depsv = new Regex( "ProjectSection\\(ProjectDependencies\\)[^\r\n]*?[\r\n]+" + "(.*?)" + "EndProjectSection", RegexOptions.Singleline).Match(v).Groups[1].Value;

                    //
                    // key is always equal to it's value.
                    // http://stackoverflow.com/questions/5629981/question-about-visual-studio-sln-file-format
                    //
                    p.ProjectDependencies = new Regex("\\s*?({[A-F0-9-]+}) = ({[A-F0-9-]+})[\r\n]+", RegexOptions.Multiline).Matches(depsv).Cast<Match>().Select(x => x.Groups[1].Value).ToList();
                } //foreach

                s.projects.Add(p);
                return "";
           }
        )
        );

        new Regex("GlobalSection\\(SolutionConfigurationPlatforms\\).*?[\r\n]+(.*?)EndGlobalSection[\r\n]+", RegexOptions.Singleline).Replace(slnTxt, new MatchEvaluator(m2 =>
            {
                s.configurations = new Regex("\\s*(.*)\\s+=").Matches(m2.Groups[1].ToString()).Cast<Match>().Select( x => x.Groups[1].Value ).ToList();
                return "";
            }
        ) );

        new Regex("GlobalSection\\(ProjectConfigurationPlatforms\\).*?[\r\n]+(.*?)EndGlobalSection[\r\n]+", RegexOptions.Singleline).Replace(slnTxt, new MatchEvaluator(m2 =>
        {
            foreach (Match m3 in new Regex("\\s*({[A-F0-9-]+})\\.(.*?)\\.(.*?)\\s+=\\s+(.*?)[\r\n]+").Matches(m2.Groups[1].ToString()))
            {
                String guid = m3.Groups[1].Value;
                String solutionConfig = m3.Groups[2].Value;
                String action = m3.Groups[3].Value;
                String projectConfig = m3.Groups[4].Value;

                Project p = s.projects.Where(x => x.ProjectGuid == guid).FirstOrDefault();
                if (p == null)
                    continue;
                
               int iConfigIndex = s.configurations.IndexOf(solutionConfig);
               if(iConfigIndex == -1)
                    continue;

                while (p.slnConfigurations.Count < s.configurations.Count)
                {
                    p.slnConfigurations.Add(null);
                    p.slnBuildProject.Add(false);
                }

                if (action == "ActiveCfg")
                {
                    p.slnConfigurations[iConfigIndex] = projectConfig;
                }
                else
                {
                    if (action.StartsWith("Build"))
                    {
                        p.slnBuildProject[iConfigIndex] = true;
                    }
                    else
                    {
                        if (action.StartsWith("Deploy"))
                        {
                            if (p.slnDeployProject == null) p.slnDeployProject = new List<bool?>();

                            while (p.slnDeployProject.Count < s.configurations.Count)
                                p.slnDeployProject.Add(null);

                            p.slnDeployProject[iConfigIndex] = true;
                        }
                    }
                } //if-esle
            }
            return "";
        }
        ));

        //
        // Initializes parent-child relationship.
        //
        new Regex("GlobalSection\\(NestedProjects\\).*?[\r\n]+(.*?)EndGlobalSection[\r\n]+", RegexOptions.Singleline).Replace(slnTxt, new MatchEvaluator(m4 =>
            {
                String v = m4.Groups[1].Value;
                new Regex("\\s*?({[A-F0-9-]+}) = ({[A-F0-9-]+})[\r\n]+", RegexOptions.Multiline).Replace(v, new MatchEvaluator(m5 =>
                {
                    String[] args = m5.Groups.Cast<Group>().Skip(1).Select(x => x.Value).ToArray();
                    Project child = s.projects.Where(x => args[0] == x.ProjectGuid).FirstOrDefault();
                    Project parent = s.projects.Where(x => args[1] == x.ProjectGuid).FirstOrDefault();
                    parent.nodes.Add(child);
                    child.parent = parent;
                    return "";
                }));
                return "";
            }
        ));

        return s;
    } //LoadSolution
} //class Solution

/// <summary>
/// Let's make system flexible enough that we can load projects and solutions independently of file format.
/// </summary>
[XmlInclude(typeof(Solution)), XmlInclude(typeof(Project))]
public class SolutionOrProject
{
    public object solutionOrProject;
    public String path;

    public SolutionOrProject(String _path)
    {
        String header = "";
        using (StreamReader sr = new StreamReader(_path, true))
        {
            header += sr.ReadLine();
            header += sr.ReadLine();
            sr.Close();
        }

        if (header.Contains("Microsoft Visual Studio Solution File"))
            solutionOrProject = Solution.LoadSolution(_path);
        else if (header.Contains("<SolutionOrProject"))
            LoadCache(_path);
        else
            solutionOrProject = Project.LoadProject(null, _path);
        
        path = _path;
    }

    public SolutionOrProject()
    {
    }


    public void SaveCache(String path)
    {
        XmlSerializer ser = new XmlSerializer(typeof(SolutionOrProject));

        using (var ms = new MemoryStream())
        {
            ser.Serialize(ms, this);
            String outS = Encoding.UTF8.GetString(ms.ToArray());
            File.WriteAllText(path, outS);
        }
    } //Save


    static public SolutionOrProject LoadCache(String path)
    {
        XmlSerializer ser = new XmlSerializer(typeof(SolutionOrProject));

        using (var s = File.OpenRead(path))
        {
            SolutionOrProject prj = (SolutionOrProject)ser.Deserialize(s);
            return prj;
        }
    }

    /// <summary>
    /// Builds solution or project .lua/.cs scripts
    /// </summary>
    /// <param name="format">lua or cs</param>
    /// <param name="outFile">Output filename without extension</param>
    public void UpdateProjectScript(String outFile, String format)
    {
        bool bCsScript = (format == "cs");
        String brO = " ", brC = "";
        String arO = " { ", arC = " }";
        String head = "";
        if (bCsScript)
        {
            brO = "("; brC = ");";
            arO = "( "; arC = " );";
            head = "    ";
        }

        String fileName = outFile;
        if( fileName == null ) fileName = Path.GetFileNameWithoutExtension(path);
        
        String outPath = Path.Combine(Path.GetDirectoryName(path), fileName + "." + format);

        Console.WriteLine("- Updating '" + fileName + "." + format + "...");
        StringBuilder o = new StringBuilder();

        //
        // C# script header
        //
        if (bCsScript)
        {
            o.AppendLine("//css_ref " +
                Path2.makeRelative(Assembly.GetExecutingAssembly().Location, Path.GetDirectoryName(path)));
            o.AppendLine("using System;         //Exception");
            o.AppendLine();
            o.AppendLine("class Script: SolutionProjectBuilder");
            o.AppendLine("{");
            o.AppendLine();
            o.AppendLine("  static void Main()");
            o.AppendLine("  {");
            o.AppendLine();
            o.AppendLine("    try {");
        }

        o.AppendLine("");
        o.AppendLine(head + "solution" + brO + "\"" + fileName + "\"" + brC);

        Solution sln = solutionOrProject as Solution;
        o.AppendLine(head + "    configurations" + arO + " " + String.Join(",", sln.configurations.Select(x => "\"" + x.Split('|')[0] + "\"").Distinct()) + arC);
        o.AppendLine(head + "    platforms" + arO + String.Join(",", sln.configurations.Select(x => "\"" + x.Split('|')[1] + "\"").Distinct()) + arC);

        String wasInSubGroup = "";
        List<String> groupParts = new List<string>();

        foreach (Project p in sln.projects)
        {
            if (p.IsSubFolder())
                continue;

            // Defines group / in which sub-folder we are.
            groupParts.Clear();
            Project pScan = p.parent;
            for( ; pScan != null; pScan = pScan.parent )
                groupParts.Insert(0, pScan.ProjectName);

            String newGroup = String.Join("/", groupParts);
            if (wasInSubGroup != newGroup)
            {
                o.AppendLine();
                o.AppendLine(head + "    group" + brO + "\"" + newGroup + "\"" + brC);
            }
            wasInSubGroup = newGroup;

            // Define project
            String name = Path.GetFileNameWithoutExtension(p.RelativePath);
            String dir = Path.GetDirectoryName(p.RelativePath);
            o.AppendLine();
            o.AppendLine(head + "    externalproject" + brO + "\"" + name + "\"" + brC);
            o.AppendLine(head + "        location" + brO + "\"" + dir.Replace("\\", "/") + "\"" + brC);
            o.AppendLine(head + "        uuid" + brO + "\"" + p.ProjectGuid.Substring(1, p.ProjectGuid.Length-2) + "\"" + brC);
            o.AppendLine(head + "        language" + brO + "\"C++\"" + brC);
            o.AppendLine(head + "        kind" + brO + "\"SharedLib\"" + brC);

            //
            // Define dependencies of project.
            //
            if (p.ProjectDependencies != null)
            {
                o.AppendLine();

                foreach (String projDepGuid in p.ProjectDependencies)
                {
                    Project depp = sln.projects.Where(x => x.ProjectGuid == projDepGuid).FirstOrDefault();
                    if (depp == null)
                        continue;

                    o.AppendLine(head + "        dependson" + brO + "\"" + depp.ProjectName + "\"" + brC);
                } //foreach
            } //if
        } //foreach
        
        o.AppendLine();
        //
        // C# script trailer
        //
        if (bCsScript)
        { 
            o.AppendLine("    } catch( Exception ex )");
            o.AppendLine("    {");
            o.AppendLine("        ConsolePrintException(ex);");
            o.AppendLine("    }");
            o.AppendLine("  } //Main");
            o.AppendLine("}; //class Script");
            o.AppendLine();
        }

        File.WriteAllText(outPath, o.ToString());
    } //UpdateProjectScript
}

/// <summary>
/// Same as Exception, only we save call stack in here (to be able to report error line later on).
/// </summary>
public class Exception2 : Exception
{
    public StackTrace strace;
    String msg;

    public Exception2( String _msg )
    {
        msg = _msg;
        strace = new StackTrace(true);
    }

    public override string Message
    {
        get
        {
            return msg;
        }
    }
};


/// <summary>
/// Helper class for generating solution or projects.
/// </summary>
public class SolutionProjectBuilder
{
    static Solution m_solution = null;
    static Project  m_project = null;
    static String   m_solutionDir;         // Path where we are building solution / project at. By default same as script is started from.
    static List<String> m_platforms = new List<String>();
    static List<String> m_configurations = new List<String>();
    static Project m_solutionRoot = new Project();
    static String m_groupPath = "";
    private static readonly Destructor Finalise = new Destructor();

    static SolutionProjectBuilder()
    {
        m_solutionDir = Path.GetDirectoryName(Path2.GetScriptPath(3));
        //Console.WriteLine(buildDir);
    }

    /// <summary>
    /// Execute once for each script using SolutionProjectBuilder class.
    /// </summary>
    private sealed class Destructor
    {
        ~Destructor()
        {
            try
            {
                externalproject(null);

                String slnPath = m_solution.path;
                Console.Write("Writing solution '" + slnPath + "' ... ");
                StringBuilder o = new StringBuilder();

                o.AppendLine();
                // For now hardcoded for vs2013, get rid of this hardcoding later on.
                o.AppendLine("Microsoft Visual Studio Solution File, Format Version 12.00");
                o.AppendLine("# Visual Studio 2013");
                //o.AppendLine("VisualStudioVersion = 12.0.30501.0");
                //o.AppendLine("MinimumVisualStudioVersion = 10.0.40219.1");

                //
                // Dump projects.
                //
                foreach (Project p in m_solution.projects)
                {
                    o.AppendLine("Project(\"" + p.ProjectHostGuid + "\") = \"" + p.ProjectName + "\", \"" + p.getRelativePath() + "\", \"" + p.ProjectGuid + "\"");

                    //
                    // Dump project dependencies.
                    //
                    if (p.ProjectDependencies != null)
                    {
                        o.AppendLine("	ProjectSection(ProjectDependencies) = postProject");
                        foreach (String depProjName in p.ProjectDependencies)
                        {
                            Project dproj = m_solution.projects.Where(x => x.ProjectName == depProjName).FirstOrDefault();
                            if (dproj != null)
                                o.AppendLine("		" + dproj.ProjectGuid + " = " + dproj.ProjectGuid);
                        }
                        o.AppendLine("	EndProjectSection");
                    } //if

                    o.AppendLine("EndProject");
                }

                //
                // Dump configurations.
                //
                o.AppendLine("Global");
                o.AppendLine("	GlobalSection(SolutionConfigurationPlatforms) = preSolution");
                foreach (String cfg in m_solution.configurations)
                {
                    o.AppendLine("		" + cfg + " = " + cfg);
                }
                o.AppendLine("	EndGlobalSection");


                //
                // Dump solution to project configuration mapping and whether or not to build specific project.
                //
                o.AppendLine("	GlobalSection(ProjectConfigurationPlatforms) = postSolution");
                foreach (Project p in m_solution.projects)
                {
                    for( int iConf = 0; iConf < m_solution.configurations.Count; iConf++)
                    {
                        String conf = m_solution.configurations[iConf];
                        String mappedConf = conf;

                        if (p.slnConfigurations != null && iConf < p.slnConfigurations.Count)
                            mappedConf = p.slnConfigurations[iConf];

                        bool bPeformBuild = true;

                        if (p.slnBuildProject != null && iConf < p.slnBuildProject.Count)
                            bPeformBuild = p.slnBuildProject[iConf];


                        o.AppendLine("		" + p.ProjectGuid + "." + conf + ".ActiveCfg = " + mappedConf);
                        if(bPeformBuild)
                            o.AppendLine("		" + p.ProjectGuid + "." + conf + ".Build.0 = " + mappedConf);
                        
                    } //for
                } //foreach
                o.AppendLine("	EndGlobalSection");
                o.AppendLine("	GlobalSection(SolutionProperties) = preSolution");
                o.AppendLine("		HideSolutionNode = FALSE");
                o.AppendLine("	EndGlobalSection");

                //
                // Dump project dependency hierarchy.
                //
                Project root = m_solution.projects.FirstOrDefault();

                if (root != null)
                {
                    while (root.parent != null) root = root.parent;
                    o.AppendLine("	GlobalSection(NestedProjects) = preSolution");

                    //
                    // Flatten tree without recursion.
                    //
                    int treeIndex = 0;
                    List<Project> projects2 = new List<Project>();
                    projects2.AddRange(root.nodes);

                    for (; treeIndex < projects2.Count; treeIndex++)
                    {
                        if (projects2[treeIndex].nodes.Count == 0)
                            continue;
                        projects2.AddRange(projects2[treeIndex].nodes);
                    }

                    foreach (Project p in projects2)
                    {
                        if (p.parent.parent == null)
                            continue;
                        o.AppendLine("		" + p.ProjectGuid + " = " + p.parent.ProjectGuid);
                    }
                    
                    o.AppendLine("	EndGlobalSection");
                } //if

                o.AppendLine("EndGlobal");

                String currentSln = "";
                if( File.Exists(slnPath) ) currentSln = File.ReadAllText(slnPath);
                
                String newSln = o.ToString().Replace("\r\n", "\n");
                //
                // Save only if needed.
                //
                if (currentSln == newSln)
                {
                    Console.WriteLine("up-to-date.");
                }
                else
                {
                    File.WriteAllText(slnPath, newSln, Encoding.UTF8);
                    Console.WriteLine("ok.");
                } //if-else
            }
            catch (Exception ex)
            {
                ConsolePrintException(ex);
            }
        }
    }

    /// <summary>
    /// Creates new solution.
    /// </summary>
    /// <param name="name">Solution name</param>
    static public void solution(String name)
    {
        m_solution = new Solution();
        m_solution.path = Path.Combine(m_solutionDir, name);
        if (!m_solution.path.EndsWith(".sln") )
            m_solution.path += ".sln";
    }


    static void generateConfigurations()
    {
        foreach (String platform in m_platforms)
            foreach (String configuration in m_configurations)
                m_solution.configurations.Add(configuration + "|" + platform);
    }

    /// <summary>
    /// Specify platform list to be used for your solution or project.
    ///     For example: platforms("x32", "x64");
    /// </summary>
    /// <param name="platformList">List of platforms to support</param>
    static public void platforms( params String[] platformList )
    {
        m_platforms = m_platforms.Concat(platformList).Distinct().ToList();
        generateConfigurations();
    }

    /// <summary>
    /// Specify which configurations to support. Typically "Debug" and "Release".
    /// </summary>
    /// <param name="configurationList">Configuration list to support</param>
    static public void configurations(params String[] configurationList )
    {
        m_configurations = m_configurations.Concat(configurationList).Distinct().ToList();
        generateConfigurations();
    }


    /// <summary>
    /// Generates Guid based on String. Key assumption for this algorithm is that name is unique (across where it it's being used)
    /// and if name byte length is less than 16 - it will be fetched directly into guid, if over 16 bytes - then we compute sha-1
    /// hash from string and then pass it to guid.
    /// </summary>
    /// <param name="name">Unique name which is unique across where this guid will be used.</param>
    /// <returns>For example "{706C7567-696E-7300-0000-000000000000}" for "plugins"</returns>
    static public String GenerateGuid(String name)
    {
        byte[] buf = Encoding.UTF8.GetBytes(name);
        byte[] guid = new byte[16];
        if (buf.Length < 16)
        {
            Array.Copy(buf, guid, buf.Length);
        }
        else
        {
            using (SHA1 sha1 = SHA1.Create())
            {
                byte[] hash = sha1.ComputeHash(buf);
                // Hash is 20 bytes, but we need 16. We loose some of "uniqueness", but I doubt it will be fatal
                Array.Copy(hash, guid, 16);
            }
        }

        // Don't use Guid constructor, it tends to swap bytes. We want to preserve original string as hex dump.
        String guidS = "{" + String.Format("{0:X2}{1:X2}{2:X2}{3:X2}-{4:X2}{5:X2}-{6:X2}{7:X2}-{8:X2}{9:X2}-{10:X2}{11:X2}{12:X2}{13:X2}{14:X2}{15:X2}", 
            guid[0], guid[1], guid[2], guid[3], guid[4], guid[5], guid[6], guid[7], guid[8], guid[9], guid[10], guid[11], guid[12], guid[13], guid[14], guid[15]) + "}";

        return guidS;
    }


    /// <summary>
    /// Add to solution reference to external project
    /// </summary>
    /// <param name="name">Project name</param>
    static public void externalproject(String name)
    {
        if (m_project != null)
            m_solution.projects.Add(m_project);

        if (name == null)       // Will be used to "flush" last filled project.
            return;

        m_project = new Project();
        m_project.ProjectName = name;

        Project parent = m_solutionRoot;
        String pathSoFar = "";

        foreach (String pathPart in m_groupPath.Split(new char[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries))
        {
            Project p = parent.nodes.Where(x => x.ProjectName == pathPart && x.RelativePath == pathPart).FirstOrDefault();
            pathSoFar = pathSoFar + ((pathSoFar.Length != 0) ? "/" : "") + pathPart;
            if (p == null)
            {
                p = new Project() { ProjectName = pathPart, RelativePath = pathPart, ProjectGuid = GenerateGuid(pathSoFar), bIsFolder = true };
                m_solution.projects.Add(p);
                parent.nodes.Add(p);
                p.parent = parent;
            }
            
            parent = p;
        }

        parent.nodes.Add(m_project);
        m_project.parent = parent;
    }

    /// <summary>
    /// The location function sets the destination directory for a generated solution or project file.
    /// </summary>
    /// <param name="path"></param>
    static public void location(String path)
    {
        if (m_project == null)
        {
            m_solutionDir = path;
        }
        else {
            m_project.RelativePath = Path.Combine(path, m_project.ProjectName);
        }
    }

    static public void kind(String _kind)
    { 
    }

    /// <summary>
    /// Specifies project uuid.
    /// </summary>
    /// <param name="uuid"></param>
    static public void uuid(String uuid)
    {
        if (m_project == null)
            throw new Exception2("Cannot specify uuid - no project selected");

        Guid guid;
        if( !Guid.TryParse(uuid, out guid) )
            throw new Exception2("Invalid uuid value '" + uuid + "'");

        m_project.ProjectGuid = "{" + uuid + "}";
    }

    /// <summary>
    /// Sets project programming language (reflects to used project extension)
    /// </summary>
    /// <param name="lang"></param>
    static public void language(String lang)
    {
        if (m_project == null)
            throw new Exception2("Project not selected");

        switch (lang)
        {
            case "C++": m_project.language = lang; break;
            case "C#": m_project.language = lang; break;
            default:
                throw new Exception2("Language '" + lang + "' is not supported");
        } //switch
    }

    static public void dependson(String projectName)
    {
        if (m_project.ProjectDependencies == null)
            m_project.ProjectDependencies = new List<string>();

        m_project.ProjectDependencies.Add(projectName);
    }

    /// <summary>
    /// Sets current "directory" where project should be placed.
    /// </summary>
    /// <param name="groupPath"></param>
    static public void group(String groupPath)
    {
        m_groupPath = groupPath;
    }

    /// <summary>
    /// Prints more details about given exception. In visual studio format for errors.
    /// </summary>
    /// <param name="ex">Exception occurred.</param>
    static public void ConsolePrintException(Exception ex)
    {
        Exception2 ex2 = ex as Exception2;
        String fromWhere = "";
        if (ex2 != null)
        {
            StackFrame f = ex2.strace.GetFrame(ex2.strace.FrameCount - 1);
            fromWhere = f.GetFileName() + "(" + f.GetFileLineNumber() + "," + f.GetFileColumnNumber() + "): ";
        }
        
        Console.WriteLine(fromWhere + "error: " + ex.Message);
        Console.WriteLine();
        Console.WriteLine("----------------------- Full call stack trace follows -----------------------");
        Console.WriteLine();
        Console.WriteLine(ex.StackTrace);
        Console.WriteLine();
    }
};

class Path2
{
    public static String GetScriptPath(int iFrame = 0)
    {
        string fileName = new System.Diagnostics.StackTrace(true).GetFrame(iFrame).GetFileName();
        //
        // http://www.csscript.net/help/Environment.html
        //
        if (fileName == null)
            fileName = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyDescriptionAttribute), true)
                     .Cast<AssemblyDescriptionAttribute>()
                     .First()
                     .Description;

        return fileName;
    }

    /// <summary>
    /// Rebases file with path fromPath to folder with baseDir.
    /// </summary>
    /// <param name="fromPath">Full file path (absolute)</param>
    /// <param name="baseDir">Full base directory path (absolute)</param>
    /// <returns>Relative path to file in respect of baseDir</returns>
    static public String makeRelative(String fromPath, String baseDir)
    {
        String pathSep = "\\";
        String[] p1 = Regex.Split(fromPath, "[\\\\/]").Where(x => x.Length != 0).ToArray();
        String[] p2 = Regex.Split(baseDir, "[\\\\/]").Where(x => x.Length != 0).ToArray();
        int i = 0;

        for (; i < p1.Length && i < p2.Length; i++)
            if (String.Compare(p1[i], p2[i], true) != 0)    // Case insensitive match
                break;

        String r = String.Join(pathSep, Enumerable.Repeat("..", p2.Length - i).Concat(p1.Skip(i).Take(p1.Length - i)));
        return r;
    }
};


class Script
{

    static int Main(String[] args)
    {
        try
        {
            String slnFile = null;
            List<String> formats = new List<string>();
            String outFile = null;

            for( int i = 0; i < args.Length; i++ )
            {
                String arg = args[i];

                if (!(arg.StartsWith("-") || arg.StartsWith("/")))
                {
                    slnFile = arg;
                    continue;
                }

                switch (arg.Substring(1).ToLower())
                {
                    case "lua": formats.Add("lua"); break;
                    case "cs": formats.Add("cs"); break;
                    case "o": i++;  outFile = args[i]; break;
                }
            } //foreach

            if (slnFile == null || formats.Count == 0)
            {
                Console.WriteLine("Usage: syncProj <.sln or .vcxproj file> (-lua|-cs) [-o file]");
                Console.WriteLine("");
                Console.WriteLine(" -cs     - C# script output");
                Console.WriteLine(" -lua    - premake5's lua script output");
                Console.WriteLine("");
                Console.WriteLine(" -o      - sets output file (without extension)");
                Console.WriteLine("");
                return -2;
            }

            SolutionOrProject proj = new SolutionOrProject(slnFile);
            String projCacheFile = slnFile + ".cache";
            SolutionOrProject projCache;

            if (File.Exists(projCacheFile))
                projCache = SolutionOrProject.LoadCache(projCacheFile);

            Solution s = proj.solutionOrProject as Solution;
            if (s != null)
            {
                foreach (Project p in s.projects)
                {
                    if (p.IsSubFolder())
                        continue;
                    
                    Project.LoadProject(s, null, p);
                }
            }

            proj.SaveCache(projCacheFile);
            foreach ( String format in formats )
                proj.UpdateProjectScript(outFile, format);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
            return -2;
        }

        return 0;
    } //Main
}

