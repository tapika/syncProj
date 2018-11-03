using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Serialization;

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
        s.slnVer = Double.Parse(Regex.Match(slnTxt, "[\r\n]+Microsoft Visual Studio Solution File, Format Version ([0-9.]+)", RegexOptions.Multiline).Groups[1].Value, CultureInfo.InvariantCulture);

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
} //class Solution

