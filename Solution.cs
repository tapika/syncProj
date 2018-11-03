using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Serialization;

/// <summary>
/// .sln loaded into class.
/// </summary>
public class Solution
{
    /// <summary>
    /// Solution name
    /// </summary>
    public String name;

    /// <summary>
    /// File path from where solution was loaded.
    /// </summary>
    [XmlIgnore]
    public String path;

    /// <summary>
    /// Just an internal project for tracking project hierarchy
    /// </summary>
    public Project solutionRoot = new Project();

    /// <summary>
    /// Solution name for debugger.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public override string ToString()
    {
        return "Solution, name = " + name;
    }

    /// <summary>
    /// Gets solution path
    /// </summary>
    /// <returns></returns>
    public String getSolutionFolder()
    {
        return Path.GetDirectoryName(path);
    }

    double slnVer;                                      // 11.00 - vs2010, 12.00 - vs2015

    /// <summary>
    /// Visual studio version information used for generation, for example 2010, 2012, 2015 and so on...
    /// </summary>
    public int fileFormatVersion;

    /// <summary>
    /// null for old visual studio's
    /// </summary>
    public String VisualStudioVersion;
    
    /// <summary>
    /// null for old visual studio's
    /// </summary>
    public String MinimumVisualStudioVersion;

    /// <summary>
    /// List of project included into solution.
    /// </summary>
    public List<Project> projects = new List<Project>();

    /// <summary>
    /// List of configuration list, in form "{Configuration}|{Platform}", for example "Release|Win32".
    /// To extract individual platforms / configuration list, use following functions.
    /// </summary>
    public List<String> configurations = new List<string>();

    /// <summary>
    /// Extracts platfroms supported by solution
    /// </summary>
    public IEnumerable<String> getPlatforms()
    {
        return configurations.Select(x => x.Split('|')[1]).Distinct();
    }

    /// <summary>
    /// Extracts configuration names supported by solution
    /// </summary>
    public IEnumerable<String> getConfigurations()
    {
        return configurations.Select(x => x.Split('|')[0]).Distinct();
    }


    /// <summary>
    /// Creates new solution.
    /// </summary>
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
        //
        //  Extra line feed is used by Visual studio, cmake does not generate extra line feed.
        //
        s.slnVer = Double.Parse(Regex.Match(slnTxt, "[\r\n]?Microsoft Visual Studio Solution File, Format Version ([0-9.]+)", RegexOptions.Multiline).Groups[1].Value, CultureInfo.InvariantCulture);

        int vsNumber = Int32.Parse(Regex.Match(slnTxt, "^\\# Visual Studio (Express )?([0-9]+)", RegexOptions.Multiline).Groups[2].Value);
        if (vsNumber > 2000)
            s.fileFormatVersion = vsNumber;
        else
            s.fileFormatVersion = vsNumber - 14 + 2015;     // Visual Studio 14 => vs2015, formula might not be applicable for future vs versions.

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
            Project p = new Project() { solution = s };

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

                String depsv = new Regex("ProjectSection\\(ProjectDependencies\\)[^\r\n]*?[\r\n]+" + "(.*?)" + "EndProjectSection", RegexOptions.Singleline).Match(v).Groups[1].Value;

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
            s.configurations = new Regex("\\s*(.*)\\s+=").Matches(m2.Groups[1].ToString()).Cast<Match>().Select(x => x.Groups[1].Value).ToList();
            return "";
        }
        ));

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
                if (iConfigIndex == -1)
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


    /// <summary>
    /// Saves solution into .sln file. Where to save is defined by path.
    /// </summary>
    public void SaveSolution(UpdateInfo uinfo)
    {
        String slnPath = path;

        //
        //  For all projects which does not have uuid, we generated uuid based on project name.
        //
        SolutionProjectBuilder.externalproject(null);   // Release any active project if we have one.
        foreach (Project p in projects)
        {
            if (String.IsNullOrEmpty(p.ProjectGuid))
            {
                SolutionProjectBuilder.m_project = p;
                SolutionProjectBuilder.uuid(p.ProjectName);
                SolutionProjectBuilder.m_project = null;
            }
        } //foreach

        StringBuilder o = new StringBuilder();

        o.AppendLine();

        int verTag = fileFormatVersion;

        if (verTag == 0)
            verTag = 2015;

        String formatVersion = "12.00";

        if (verTag <= 2010)
            formatVersion = "11.00";

        o.AppendLine("Microsoft Visual Studio Solution File, Format Version " + formatVersion);

        if (verTag >= 2015)
            verTag -= 2015 - 14;

        o.AppendLine("# Visual Studio " + verTag.ToString());

        // For some reason must be specified, otherwise Visual studio will try to save project after load.
        if (fileFormatVersion >= 2015)
        {
            String ver = MinimumVisualStudioVersion;
            if( ver == null ) ver = "10.0.40219.1";
            o.AppendLine("MinimumVisualStudioVersion = " + ver);
        }

        // Visual studio 2015 itself dumps also VisualStudioVersion & MinimumVisualStudioVersion - but we cannot support it, as it's targetted per visual studio toolset version.

        //
        // Dump projects.
        //
        foreach (Project p in projects)
        {
            o.AppendLine("Project(\"" + p.ProjectHostGuid + "\") = \"" + p.ProjectName + "\", \"" + p.getRelativePath() + "\", \"" + p.ProjectGuid.ToUpper() + "\"");

            //
            // Dump project dependencies.
            //
            if (p.ProjectDependencies != null)
            {
                o.AppendLine("	ProjectSection(ProjectDependencies) = postProject");
                foreach (String depProjName in p.ProjectDependencies)
                {
                    Project dproj = projects.Where(x => x.ProjectName == depProjName).FirstOrDefault();
                    if (dproj != null)
                        o.AppendLine("		" + dproj.ProjectGuid.ToUpper() + " = " + dproj.ProjectGuid.ToUpper());
                }
                o.AppendLine("	EndProjectSection");
            } //if

            o.AppendLine("EndProject");
        }


        List<String> sortedConfs = Project.getSortedConfigurations(configurations, false, null, true);

        //
        // Dump configurations.
        //
        o.AppendLine("Global");
        o.AppendLine("	GlobalSection(SolutionConfigurationPlatforms) = preSolution");
        foreach (String cfg in sortedConfs)
        {
            o.AppendLine("		" + cfg + " = " + cfg);
        }
        o.AppendLine("	EndGlobalSection");


        //
        // Dump solution to project configuration mapping and whether or not to build specific project.
        //
        o.AppendLine("	GlobalSection(ProjectConfigurationPlatforms) = postSolution");
        foreach (Project p in projects)
        {
            if (p.IsSubFolder() )       // If sub-folder no need to list it here.
                continue;

            List<String> projConfs = p.getConfigurationNames();
            List<String> projPlatforms = p.getPlatforms();

            foreach( String conf in sortedConfs )
            {
                int iConf = configurations.IndexOf(conf);
                String mappedConf = conf;

                bool bPeformBuild = true;
                bool? bPerformDeploy = null;

                if (p.Keyword == EKeyword.Package)
                    bPerformDeploy = true;


                if (p.slnConfigurations != null && iConf < p.slnConfigurations.Count)
                {
                    // Mapped configuration item is specified.
                    mappedConf = p.slnConfigurations[iConf];
                }
                else {
                    //
                    // Try to map configuration by ourselfs. Map x86 to Win32 automatically.
                    //
                    if (!p.configurations.Contains(conf))
                    {
                        String[] confPlat = conf.Split('|');

                        if (projConfs.Contains(confPlat[0]) && confPlat[1] == "x86" && projPlatforms.Contains("Win32"))
                        {
                            mappedConf = confPlat[0] + '|' + "Win32";
                        }
                        else
                        {
                            // Configuration cannot be mapped (E.g. Solution has "Debug|Arm", project supports only "Debug|Win32".
                            // We disable project build, but try to map configuration anyway - otherwise Visual Studio will 
                            // try to save solution by itself.
                            bPeformBuild = false;
                            bPerformDeploy = null;

                            mappedConf = p.configurations.Where(x => x.StartsWith(confPlat[0])).FirstOrDefault();
                            if (mappedConf == null)
                                mappedConf = p.configurations[0];
                        } //if-else
                    } //if
                }

                if (p.slnBuildProject != null && iConf < p.slnBuildProject.Count)
                    bPeformBuild = p.slnBuildProject[iConf];

                if (p.slnDeployProject != null && iConf < p.slnConfigurations.Count)
                    bPerformDeploy = p.slnDeployProject[iConf];

                o.AppendLine("		" + p.ProjectGuid.ToUpper() + "." + conf + ".ActiveCfg = " + mappedConf);
                if (bPeformBuild)
                    o.AppendLine("		" + p.ProjectGuid.ToUpper() + "." + conf + ".Build.0 = " + mappedConf);
                
                if(bPerformDeploy.HasValue && bPerformDeploy.Value )
                    o.AppendLine("		" + p.ProjectGuid.ToUpper() + "." + conf + ".Deploy.0 = " + mappedConf);

            } //for
        } //foreach
        o.AppendLine("	EndGlobalSection");
        o.AppendLine("	GlobalSection(SolutionProperties) = preSolution");
        o.AppendLine("		HideSolutionNode = FALSE");
        o.AppendLine("	EndGlobalSection");

        //
        // Dump project dependency hierarchy.
        //
        Project root = projects.FirstOrDefault();

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
                o.AppendLine("		" + p.ProjectGuid.ToUpper() + " = " + p.parent.ProjectGuid.ToUpper());
            }

            o.AppendLine("	EndGlobalSection");
        } //if

        o.AppendLine("EndGlobal");

        String currentSln = "";
        if (File.Exists(slnPath)) currentSln = File.ReadAllText(slnPath);

        String newSln = o.ToString();
        //
        // Save only if needed.
        //
        if (currentSln == newSln)
        {
            uinfo.MarkFileUpdated(slnPath, false);
        }
        else
        {
            if(SolutionProjectBuilder.isDeveloper() && File.Exists(slnPath)) File.Copy(slnPath, slnPath + ".bkp", true);
            File.WriteAllText(slnPath, newSln, Encoding.UTF8);
            uinfo.MarkFileUpdated(slnPath, true);
        } //if-else
    } //SaveSolution


} //class Solution

