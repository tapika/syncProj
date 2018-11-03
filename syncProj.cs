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
    public string ParentProjectGuid;

    public String Keyword;
    
    [XmlIgnore]
    public List<Project> nodes = new List<Project>();   // Child nodes (Empty folder also does not have any children)
    [XmlIgnore]
    public Project parent;                              // Points to folder which contains given project

    public string ProjectName;
    public string RelativePath;

    public bool IsSubFolder()
    {
        // Currently made it like this - is there any other way to detect this ?
        bool b = ProjectName == RelativePath;
        return b;
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
    public List<bool> build = new List<bool>();

    /// <summary>
    /// true to deploy project, false - not, null - invalid. List is null if not used at all.
    /// </summary>
    public List<bool?> deploy = null;

    /// <summary>
    /// Project guid, for example "{65787061-7400-0000-0000-000000000000}"
    /// </summary>
    public string ProjectGuid;

    /// <summary>
    /// Project dependent guids.
    /// </summary>
    public List<String> ProjectDependencies { get; set; }

    /// <summary>
    /// This array includes all items from ItemGroup, independently whether it's include file or file to compile, because
    /// visual studio is ordering them alphabetically - we keep same array to be able to sort files.
    /// </summary>
    public List<FileInfo> files = new List<FileInfo>();


    public string AsSlnString()
    {
        return "Project(\"" + ParentProjectGuid + "\") = \"" + ProjectName + "\", \"" + RelativePath + "\", \"" + ProjectGuid + "\"";
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
    public List<String> configurations = new List<string>();

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
            "Project\\(\"(?<ParentProjectGuid>{[A-F0-9-]+})\"\\) = \"(?<ProjectName>.*?)\", \"(?<RelativePath>.*?)\", \"(?<ProjectGuid>{[A-F0-9-]+})\"[\r\n]*(?<dependencies>.*?)EndProject[\r\n]+", 
            RegexOptions.Singleline);

        
        reProjects.Replace(slnTxt, new MatchEvaluator(m =>
            {
                Project p = new Project();

                foreach (String g in reProjects.GetGroupNames())
                {
                    if (g == "0")   //"0" - RegEx special kind of group
                        continue;

                    String v = m.Groups[g].ToString();
                    if (g != "dependencies")
                    { 
                        p.GetType().GetField(g).SetValue(p, v);
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
                    p.build.Add(false);
                }

                if (action == "ActiveCfg")
                {
                    p.slnConfigurations[iConfigIndex] = projectConfig;
                }
                else
                {
                    if (action.StartsWith("Build"))
                    {
                        p.build[iConfigIndex] = true;
                    }
                    else
                    {
                        if (action.StartsWith("Deploy"))
                        {
                            if (p.deploy == null) p.deploy = new List<bool?>();

                            while (p.deploy.Count < s.configurations.Count)
                                p.deploy.Add(null);

                            p.deploy[iConfigIndex] = true;
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
    /// Builds solution or project .lua script.
    /// </summary>
    /// <param name="format">lua or cs</param>
    public void UpdateProjectScript(String format)
    {
        String fileName = Path.GetFileNameWithoutExtension(path);
        String outPath = Path.Combine(Path.GetDirectoryName(path), fileName + "." + format);
        StringBuilder o = new StringBuilder();
        o.AppendLine("");
        o.AppendLine("solution \"" + fileName + "\"");

        Solution sln = solutionOrProject as Solution;
        o.AppendLine("    configurations { " + String.Join(",", sln.configurations.Select(x => "\"" + x.Split('|')[0] + "\"").Distinct()) + " }");
        o.AppendLine("    platforms { " + String.Join(",", sln.configurations.Select(x => "\"" + x.Split('|')[1] + "\"").Distinct()) + " }");

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
                o.AppendLine("    group \"" + newGroup + "\"");
            }
            wasInSubGroup = newGroup;

            // Define project
            String name = Path.GetFileNameWithoutExtension(p.RelativePath);
            String dir = Path.GetDirectoryName(p.RelativePath);
            o.AppendLine();
            o.AppendLine("    externalproject \"" + name + "\"");
            o.AppendLine("        location \"" + dir.Replace("\\", "/") + "\"");
            o.AppendLine("        uuid \"" + p.ProjectGuid.Substring(1, p.ProjectGuid.Length-2) + "\"");
            o.AppendLine("        language \"C++\"");
            o.AppendLine("        kind \"SharedLib\"");

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

                    o.AppendLine("        dependson \"" + depp.ProjectName + "\"");
                } //foreach
            } //if
        } //foreach
        o.AppendLine();

        File.WriteAllText(outPath, o.ToString());
    } //UpdateProjectScript
}

/// <summary>
/// Helper class for generating solution or projects.
/// </summary>
public class SolutionProjectBuilder
{
    static Solution m_solution = null;
    static Project  m_project = null;
    static String   m_solutionDir;         // Path where we are building solution / project at. By default same as script is started from.
    static String[] m_platforms;
    static String[] m_configurations;
    private static readonly Destructor Finalise = new Destructor();

    static SolutionProjectBuilder()
    {
        m_solutionDir = Path.GetDirectoryName(GetScriptPath(3));
        //Console.WriteLine(buildDir);
    }

    private sealed class Destructor
    {
        ~Destructor()
        {
            //Console.WriteLine("shutdown");
            externalproject(null);
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

    /// <summary>
    /// Specify platform list to be used for your solution or project.
    ///     For example: platforms("x32", "x64");
    /// </summary>
    /// <param name="platformList">List of platforms to support</param>
    static public void platforms( params String[] platformList )
    {
        m_platforms = platformList;
    }

    /// <summary>
    /// Specify which configurations to support. Typically "Debug" and "Release".
    /// </summary>
    /// <param name="configurationList">Configuration list to support</param>
    static public void configurations(params String[] configurationList )
    {
        m_configurations = configurationList;
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

    static public void uuid(String uuid)
    {
    }



    static String GetScriptPath(int iFrame = 0)
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

};



class Script
{
    static int Main(String[] args)
    {
        try
        {
            String slnFile = null;
            String format = null;

            foreach (String arg in args)
            {
                if (!(arg.StartsWith("-") || arg.StartsWith("/")))
                {
                    slnFile = arg;
                    continue;
                }

                switch (arg.Substring(1).ToLower())
                {
                    case "lua": format = "lua"; break;
                    case "cs": format = "cs"; break;
                }
            } //foreach

            if (slnFile == null || format == null)
            {
                Console.WriteLine("Usage: syncProj <.sln or .vcxproj file> (-lua|-cs)");
                Console.WriteLine("");
                Console.WriteLine(" -cs     - C# script output");
                Console.WriteLine(" -lua    - premake5's lua script output");
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
            proj.UpdateProjectScript(format);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
            return -2;
        }

        return 0;
    } //Main
}

