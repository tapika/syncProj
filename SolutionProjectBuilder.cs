using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// Helper class for generating solution or projects.
/// </summary>
public class SolutionProjectBuilder
{
    static Solution m_solution = null;
    static Project m_project = null;
    public static String m_workPath;  // Path where we are building solution / project at. By default same as script is started from.
    
    /// <summary>
    /// Relative directory from solution. Set by RunScript.
    /// </summary>
    public static String m_scriptRelativeDir = "";
    static List<String> m_platforms = new List<String>();
    static List<String> m_configurations = new List<String>();
    static Project m_solutionRoot = new Project();
    static String m_groupPath = "";
    private static readonly Destructor Finalise = new Destructor();

    static SolutionProjectBuilder()
    {
        m_workPath = Path.GetDirectoryName(Path2.GetScriptPath(3));
        //Console.WriteLine(m_workPath);
    }

    /// <summary>
    /// Just an indicator that we did not have any exception.
    /// </summary>
    static bool bEverythingIsOk = true;

    /// <summary>
    /// Execute once for each invocation of script. Not executed if multiple scripts are included.
    /// </summary>
    private sealed class Destructor
    {
        ~Destructor()
        {
            try
            {
                if (!bEverythingIsOk)
                    return;
                
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
                    for (int iConf = 0; iConf < m_solution.configurations.Count; iConf++)
                    {
                        String conf = m_solution.configurations[iConf];
                        String mappedConf = conf;

                        if (p.slnConfigurations != null && iConf < p.slnConfigurations.Count)
                            mappedConf = p.slnConfigurations[iConf];

                        bool bPeformBuild = true;

                        if (p.slnBuildProject != null && iConf < p.slnBuildProject.Count)
                            bPeformBuild = p.slnBuildProject[iConf];


                        o.AppendLine("		" + p.ProjectGuid + "." + conf + ".ActiveCfg = " + mappedConf);
                        if (bPeformBuild)
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
                if (File.Exists(slnPath)) currentSln = File.ReadAllText(slnPath);

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
        m_solution.path = Path.Combine(m_workPath, name);
        if (!m_solution.path.EndsWith(".sln"))
            m_solution.path += ".sln";
    }


    static void generateConfigurations()
    {
        m_solution.configurations.Clear();

        foreach (String platform in m_platforms)
            foreach (String configuration in m_configurations)
                m_solution.configurations.Add(configuration + "|" + platform);
    }

    /// <summary>
    /// Specify platform list to be used for your solution or project.
    ///     For example: platforms("x32", "x64");
    /// </summary>
    /// <param name="platformList">List of platforms to support</param>
    static public void platforms(params String[] platformList)
    {
        m_platforms = m_platforms.Concat(platformList).Distinct().ToList();
        generateConfigurations();
    }

    /// <summary>
    /// Specify which configurations to support. Typically "Debug" and "Release".
    /// </summary>
    /// <param name="configurationList">Configuration list to support</param>
    static public void configurations(params String[] configurationList)
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


    static void specifyproject(String name)
    {
        if (m_project != null)
            m_solution.projects.Add(m_project);

        if (name == null)       // Will be used to "flush" last filled project.
            return;

        m_project = new Project();
        m_project.ProjectName = name;
        m_project.language = "C++";
        m_project.RelativePath = Path.Combine(m_scriptRelativeDir, name);

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
    /// Add to solution reference to external project
    /// </summary>
    /// <param name="name">Project name</param>
    static public void externalproject(String name)
    {
        specifyproject(name);
    }

    /// <summary>
    /// Adds new project to solution
    /// </summary>
    /// <param name="name">Project name</param>
    static public void project(String name)
    {
        specifyproject(name);
    }


    /// <summary>
    /// The location function sets the destination directory for a generated solution or project file.
    /// </summary>
    /// <param name="path"></param>
    static public void location(String path)
    {
        if (m_project == null)
        {
            m_workPath = path;
        }
        else
        {
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
        if (!Guid.TryParse(uuid, out guid))
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
    /// Invokes C# Script by source code path. If any error, exception will be thrown.
    /// </summary>
    /// <param name="path">c# script path</param>
    static public void invokeScript(String path)
    {
        String errors = "";
        if (!CsScript.RunScript(path, true, out errors, "no_exception_handling"))
            throw new Exception(errors);
    }

    static public void toolset(String name)
    {
    }

    static public void characterset(String name)
    {
    }

    static public void targetdir(String name)
    {
    }

    static public void objdir(String name)
    {
    }

    static public void targetname(String name)
    {
    }

    static public void targetextension(String name)
    {
    }

    static public void pchheader(String name)
    {
    }

    static public void pchsource(String name)
    {
    }

    static public void filter(params String[] filter)
    { 
    }

    static public void symbols(String name)
    {
    }

    static public void includedirs(params String[] dirs)
    {
    }

    static public void defines(params String[] defines)
    {
    }

    static public void files(params String[] files)
    {
    }

    static public void flags(params String[] flags)
    {
    }


    /// <summary>
    /// Prints more details about given exception. In visual studio format for errors.
    /// </summary>
    /// <param name="ex">Exception occurred.</param>
    static public void ConsolePrintException(Exception ex, String[] args = null)
    {
        if (args != null && args.Contains("no_exception_handling"))
            throw ex;

        Exception2 ex2 = ex as Exception2;
        String fromWhere = "";
        if (ex2 != null)
        {
            StackFrame f = ex2.strace.GetFrame(ex2.strace.FrameCount - 1);
            // Not always can be determined for some reason
            if(f.GetFileName() != null )
                fromWhere = f.GetFileName() + "(" + f.GetFileLineNumber() + "," + f.GetFileColumnNumber() + "): ";
        }

        if(!ex.Message.Contains("error") )
            Console.WriteLine(fromWhere + "error: " + ex.Message);
        else
            Console.WriteLine(ex.Message);

        Console.WriteLine();
        Console.WriteLine("----------------------- Full call stack trace follows -----------------------");
        Console.WriteLine();
        Console.WriteLine(ex.StackTrace);
        Console.WriteLine();
        bEverythingIsOk = false;
    }
};

