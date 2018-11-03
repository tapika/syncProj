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

[DebuggerDisplay("{ProjectName}, {RelativePath}, {ProjectGuid}")]
public class Project
{
    public string ParentProjectGuid;
    
    [XmlIgnore]
    public List<Project> nodes = new List<Project>();   // Child nodes (Empty folder also does not have any children)
    [XmlIgnore]
    public Project parent;                              // Points to folder which contains given project

    public string ProjectName;
    public string RelativePath;

    /// <summary>
    /// Same amount of configurations as in solution, this however lists project configurations, which correspond to solution configuration
    /// using same index.
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

    public string AsSlnString()
    {
        return "Project(\"" + ParentProjectGuid + "\") = \"" + ProjectName + "\", \"" + RelativePath + "\", \"" + ProjectGuid + "\"";
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

        XDocument p = XDocument.Load(path);
        return project;
    }
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

                while (p.configurations.Count < s.configurations.Count)
                {
                    p.configurations.Add(null);
                    p.build.Add(false);
                }

                if (action == "ActiveCfg")
                {
                    p.configurations[iConfigIndex] = projectConfig;
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

    public SolutionOrProject(String path)
    {
        String header = "";
        using (StreamReader sr = new StreamReader(path, true))
        {
            header += sr.ReadLine();
            header += sr.ReadLine();
            sr.Close();
        }

        if (header.Contains("Microsoft Visual Studio Solution File"))
            solutionOrProject = Solution.LoadSolution(path);
        else if (header.Contains("<SolutionOrProject"))
            LoadCache(path);
        else
            solutionOrProject = Project.LoadProject(null, path);
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

}


class Script
{
    static int Main(String[] args)
    {
        //String slnFile = @"E:\Prototyping\vlc-2.2.1.32-2013\modules\audio_output\opensles_plugin\opensles_plugin.sln";
        String slnFile = @"C:\!deleteme!\Android1.sln";

        try
        {
            SolutionOrProject proj = new SolutionOrProject(slnFile);
            String projCacheFile = slnFile + ".cache";
            SolutionOrProject projCache;

            if (File.Exists(projCacheFile))
                projCache = SolutionOrProject.LoadCache(projCacheFile);

            proj.SaveCache(projCacheFile);

            Solution s = proj.solutionOrProject as Solution;
            if (s != null)
            {
                foreach (Project p in s.projects)
                {
                    if (p.RelativePath == p.ProjectName)
                        continue;
                    
                    Project.LoadProject(s, null, p);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
            return -2;
        }

        return 0;
    } //Main
}