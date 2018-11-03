using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Collections;
using System.ComponentModel;

/// <summary>
/// Helper class for generating solution or projects.
/// </summary>
public class SolutionProjectBuilder
{
    /// <summary>
    /// Currently selected active solution on which all function below operates upon. null if not selected yet.
    /// </summary>
    public static Solution m_solution = null;

    /// <summary>
    /// Currently selected active project on which all function below operates upon. null if not selected yet.
    /// </summary>
    public static Project m_project = null;

    /// <summary>
    /// project which perform update of solution, all newly added projects must be dependent on it.
    /// </summary>
    static String solutionUpdateProject = "";

    /// <summary>
    /// Path where we are building solution / project at. By default same as script is started from.
    /// </summary>
    public static String m_workPath;
    
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
        String path = Path2.GetScriptPath(3);

        if (path != null)
            m_workPath = Path.GetDirectoryName(path);
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
                
                if (m_solution != null)
                {
                    // Flush project into solution.
                    externalproject(null);
                    m_solution.SaveSolution();

                    foreach (Project p in m_solution.projects)
                        if( !p.bDefinedAsExternal )
                            p.SaveProject();
                }
                else
                    if (m_project != null)
                    m_project.SaveProject();
                else
                    Console.WriteLine("No solution or project defined. Use project() or solution() functions in script");
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

    static void requireSolutionSelected()
    {
        if (m_solution == null)
            throw new Exception2("Solution not specified (Use solution(\"name\" to specify new solution)");
    }

    static void requireProjectSelected()
    {
        if (m_project == null)
            throw new Exception2("Project not specified (Use project(\"name\" to specify new project)");
    }

    static void generateConfigurations()
    {
        // Generating configurations for solution
        if (m_project == null)
        {
            requireSolutionSelected();

            m_solution.configurations.Clear();

            foreach (String platform in m_platforms)
                foreach (String configuration in m_configurations)
                    m_solution.configurations.Add(configuration + "|" + platform);
        }
        else {
            requireProjectSelected();

            m_project.configurations.Clear();

            foreach (String platform in m_platforms)
                foreach (String configuration in m_configurations)
                    m_project.configurations.Add(configuration + "|" + platform);
        }
    }

    /// <summary>
    /// Specify platform list to be used for your solution or project.
    ///     For example: platforms("x32", "x64");
    /// </summary>
    /// <param name="platformList">List of platforms to support</param>
    static public void platforms(params String[] platformList)
    {
        m_platforms = platformList.Distinct().ToList();
        generateConfigurations();
    }

    /// <summary>
    /// Specify which configurations to support. Typically "Debug" and "Release".
    /// </summary>
    /// <param name="configurationList">Configuration list to support</param>
    static public void configurations(params String[] configurationList)
    {
        m_configurations = configurationList.Distinct().ToList();
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
        {
            if (m_solution != null)
            {
                if(!m_solution.projects.Contains(m_project) )
                    m_solution.projects.Add(m_project);
            }
            else
                // We are collecting projects only (no solution) and this is not last project
                if (name != null)
                m_project.SaveProject();
        }

        // Release active project
        m_project = null;
        resetConfigurations();

        if (name == null)       // Will be used to "flush" last filled project.
        {
            return;
        }

        if (m_solution != null)
        {
            m_project = m_solution.projects.Where(x => x.ProjectName == name).FirstOrDefault();

            // Selecting already specified project.
            if (m_project != null)
                return;
        }

        m_project = new Project() { solution = m_solution };

        if (solutionUpdateProject != "")
            dependson(solutionUpdateProject);

        m_project.ProjectName = name;
        m_project.language = "C++";
        String path = Path.Combine(m_scriptRelativeDir, name);
        path = new Regex(@"[^\\/]+[\\/]..[\\/]").Replace(path, "");     // Remove extra upper folder references if any
        m_project.RelativePath = path;

        Project parent = m_solutionRoot;
        String pathSoFar = "";

        foreach (String pathPart in m_groupPath.Split(new char[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries))
        {
            Project p = parent.nodes.Where(x => x.ProjectName == pathPart && x.RelativePath == pathPart).FirstOrDefault();
            pathSoFar = pathSoFar + ((pathSoFar.Length != 0) ? "/" : "") + pathPart;
            if (p == null)
            {
                p = new Project() { solution = m_solution, ProjectName = pathPart, RelativePath = pathPart, ProjectGuid = GenerateGuid(pathSoFar), bIsFolder = true };
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
    /// Add to solution reference to external project. Call with null parameter to flush currently active project (Either to solution
    /// or to disk).
    /// </summary>
    /// <param name="name">Project name</param>
    static public void externalproject(String name)
    {
        specifyproject(name);
        if( name != null )
            m_project.bDefinedAsExternal = true;
    }

    /// <summary>
    /// Adds new project to solution
    /// </summary>
    /// <param name="name">Project name</param>
    static public void project(String name)
    {
        specifyproject(name);
        
        if( name != null )
            m_project.bDefinedAsExternal = false;
    }

    /// <summary>
    /// Sets Visual Studio file format version to be used.
    /// </summary>
    /// <param name="vsVersion">2010, 2012, 2013, ...</param>
    static public void vsver(int vsVersion)
    {
        if (m_solution == null && m_project == null)
            requireSolutionSelected();

        if (m_project != null)
        {
            m_project.SetFileFormatVersion(vsVersion);

            using (new UsingSyncProj(1))
            {
                switch (vsVersion)
                {
                    case 2010: toolset("v100");  break;
                    case 2012: toolset("v110");  break;
                    case 2013: toolset("v120");  break;
                    case 2015: toolset("v140");  break;
                    case 2017: toolset("v141");  break;
                    default:
                        // Try to guess the future. 2019 => "v160" ?
                        toolset("v" + (((vsVersion - 2019) + 16) * 10).ToString());
                        break;
                } //switch
            } //using
        }
        else 
        {
            m_solution.fileFormatVersion = vsVersion;
        }
    } //vsver

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

    /// <summary>
    /// Specifies project uuid - could be written in form of normal guid ("{5E40B384-095E-452A-839D-E0B62833256F}")
    /// - use this kind of syntax if you need to produce your project in backwards compatible manner - so existing
    /// solution can load your project.
    /// Could be also written in any string form, but you need to take care that string is unique enough accross where your
    /// project is used. For example project("test1"); uuid("test"); project("test1"); uuid("test"); will result in
    /// two identical project uuid's and Visual studio will try to resave your project with changed uuid.
    /// </summary>
    /// <param name="nameOrUuid">Project uuid or some unique name</param>
    static public void uuid(String nameOrUuid)
    {
        requireProjectSelected();

        bool forceGuid = nameOrUuid.Contains('{') || nameOrUuid.Contains('}');
        String uuid = "";

        var guidMatch = SolutionProjectBuilder.guidMatcher.Match(nameOrUuid);
        if (guidMatch.Success)
        {
            uuid = "{" + guidMatch.Groups[1].Value + "}";
        }
        else
        {
            if (forceGuid)
                throw new Exception2("Invalid uuid value '" + nameOrUuid + "'");

            uuid = GenerateGuid(nameOrUuid);
        }

        if (m_solution != null)
        {
            Project p = m_solution.projects.Where(x => x.ProjectGuid == uuid).FirstOrDefault();
            if (p != null)
                throw new Exception2("uuid '" + uuid + "' is already used by project '" + 
                    p.ProjectName + "' - please consider using different uuid. " +
                    "For more information refer to uuid() function description.");
        }

        m_project.ProjectGuid = uuid;

    } //uuid

    /// <summary>
    /// Sets project programming language (reflects to used project extension)
    /// </summary>
    /// <param name="lang">C++ or C#</param>
    static public void language(String lang)
    {
        requireProjectSelected();
        ECompileAs compileAs = ECompileAs.Default;

        switch (lang)
        {
            case "C":  
                m_project.language = lang; 
                compileAs = ECompileAs.CompileAsC;
                break;
            case "C++": 
                m_project.language = lang;
                compileAs = ECompileAs.CompileAsCpp;
                break;
            case "C#": 
                m_project.language = lang; 
                break;
            default:
                throw new Exception2("Language '" + lang + "' is not supported");
        } //switch

        // Set up default compilation language
        foreach (var conf in getSelectedProjectConfigurations())
            conf.CompileAs = compileAs;
    }

    static Regex guidMatcher = new Regex("^[{(]?([0-9A-Fa-f]{8}[-][0-9A-Fa-f]{4}[-][0-9A-Fa-f]{4}[-][0-9A-Fa-f]{4}[-][0-9A-Fa-f]{12})[)}]?$");

    /// <summary>
    /// Specify one or more non-linking project build order dependencies.
    /// </summary>
    /// <param name="dependencies">List of dependent project. Can include only project name. Can include first guid and then project path. (For android packaging project)</param>
    static public void dependson(params String[] dependencies)
    {
        requireProjectSelected();

        for (int i = 0; i < dependencies.Length; i++)
        {
            String nameOrGuid = dependencies[i];
            String name = "";

            Match mGuid = guidMatcher.Match(nameOrGuid);
            if (mGuid.Success)
            {
                if (i + 1 != dependencies.Length) name = dependencies[i + 1];

                files(name);
                FileInfo fi = m_project.files.Last();
                fi.includeType = IncludeType.ProjectReference;
                fi.Project = "{" + mGuid.Groups[1].Value + "}";
                i++;
            }
            else
            {
                name = nameOrGuid;
            }

            if (m_project.ProjectDependencies == null)
                m_project.ProjectDependencies = new List<string>();

            m_project.ProjectDependencies.Add(nameOrGuid);
        } //for
    } //dependson

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
        String fullPath = Path.Combine(SolutionProjectBuilder.m_workPath, path);

        CsScript.RunScript(fullPath, true, true, out errors, "no_exception_handling");
    }


    /// <summary>
    /// Selected configurations (Either project global or file specific) selected by filter.
    /// selectedFileConfigurations is file specific, selectedConfigurations is project wide.
    /// </summary>
    static List<FileConfigurationInfo> selectedFileConfigurations = new List<FileConfigurationInfo>();
    static List<FileConfigurationInfo> selectedConfigurations = new List<FileConfigurationInfo>();
    static String[] selectedFilters = null;     // null if not set
    static bool bLastSetFilterWasFileSpecific = false;

    static void resetConfigurations()
    {
        selectedFileConfigurations = new List<FileConfigurationInfo>();
        selectedConfigurations = new List<FileConfigurationInfo>();
        selectedFilters = null;     // null if not set
        bLastSetFilterWasFileSpecific = false;
    }

    /// <summary>
    /// Gets currently selected configurations by filter.
    /// </summary>
    /// <param name="bForceNonFileSpecific">true to force project specific configuration set.</param>
    /// <param name="callerFrame">Tells how many call call frame behind is end-user code. (Non syncproj code). (Reflects to error reporting)</param>
    static List<FileConfigurationInfo> getSelectedConfigurations( bool bForceNonFileSpecific, int callerFrame = 0)
    {
        List<FileConfigurationInfo> list;

        if (bForceNonFileSpecific)
            list = selectedConfigurations;
        else
            list = (bLastSetFilterWasFileSpecific) ? selectedFileConfigurations: selectedConfigurations;

        if (list.Count == 0)
            filter();

        if (list.Count == 0)
            throw new Exception2("Please specify configurations() and platforms() before using this function. Amount of configurations and platforms must be non-zero.", callerFrame + 1);
            
        return list;
    }

    /// <summary>
    /// Gets project configuration list, throw exception if cannot be found
    /// </summary>
    static List<Configuration> getSelectedProjectConfigurations()
    {
        List<Configuration> projConfs = getSelectedConfigurations(true).Cast<Configuration>().Where(x => x != null).ToList();
        return projConfs;
    }


    /// <summary>
    /// Selects to which configurations to apply subsequent function calls (like "kind", "symbols", "files"
    /// and so on...)
    /// </summary>
    /// <param name="filters">
    ///     Either configuration name, for example "Debug" / "Release" or
    ///     by platform name, for example: "platforms:Win32", "platforms:x64"
    ///     or by file name, for example: "files:my.cpp"
    /// </param>
    static public void filter(params String[] filters)
    {
        requireProjectSelected();

        Dictionary2<String, String> dFilt = new Dictionary2<string, string>();

        foreach (String filter in filters)
        {
            String[] v = filter.Split(new char[] { ':' }, 2);

            if (v.Length == 1)
            {
                dFilt["configurations"] = v[0];
            }
            else
            {
                String key = v[0].ToLower();
                if (key != "configurations" && key != "platforms" && key != "files")
                    throw new Exception2("filter tag '" + key + "' is not supported");

                dFilt[key] = v[1];
            } //if-else
        } //for

        IList configItems = m_project.projectConfig;
        Type type = typeof(Configuration);

        if (dFilt.ContainsKey("files"))
        {
            String fName = dFilt["files"];
            FileInfo fileInfo = m_project.files.Where(x => x.relativePath == fName).FirstOrDefault();

            if (fileInfo == null)
                throw new Exception2("File not found: '" + fName + "' - please specify correct filename. Should be registered via 'files' function.");
            configItems = fileInfo.fileConfig;
            type = typeof(FileConfigurationInfo);
            bLastSetFilterWasFileSpecific = true;
        }
        else {
            bLastSetFilterWasFileSpecific = false;
        }

        String confMatchPatten;
        if (dFilt.ContainsKey("configurations"))
            confMatchPatten = dFilt["configurations"];
        else
            confMatchPatten = ".*?";

        confMatchPatten += "\\|";

        if (dFilt.ContainsKey("platforms"))
            confMatchPatten += dFilt["platforms"];
        else
            confMatchPatten += ".*";

        Regex reConfMatch = new Regex(confMatchPatten);
        
        if(bLastSetFilterWasFileSpecific)
            selectedFileConfigurations.Clear();
        else
            selectedConfigurations.Clear();

        for ( int i = 0; i < m_project.configurations.Count; i++ )
        {
            if (reConfMatch.Match(m_project.configurations[i]).Success)
            {
                while (i >= configItems.Count)
                {
                    FileConfigurationInfo fci = (FileConfigurationInfo)Activator.CreateInstance(type);
                    fci.confName = m_project.configurations[i];
                    configItems.Add(fci);
                }
            
                if(bLastSetFilterWasFileSpecific)
                    selectedFileConfigurations.Add((FileConfigurationInfo)configItems[i]);
                else
                    selectedConfigurations.Add((FileConfigurationInfo)configItems[i]);
            }
        } //for

        bool bFilterResultsAreEmpty = false;
        if (bLastSetFilterWasFileSpecific)
            bFilterResultsAreEmpty = selectedFileConfigurations.Count == 0;
        else
            bFilterResultsAreEmpty = selectedConfigurations.Count == 0;

        if (bFilterResultsAreEmpty)
            throw new Exception2("Specified filter did not select any of configurations, please check the filter");

        if (!bLastSetFilterWasFileSpecific)
            selectedFilters = filters;
    } //filter

    /// <summary>
    /// Specifies application type, one of following: 
    /// </summary>
    /// <param name="_kind">
    /// WindowedApp, Application    - Window application<para />
    /// DynamicLibrary, SharedLib   - .dll<para />
    /// </param>
    /// <param name="os">"windows" (default), "android" or "package"</param>
    static public void kind(String _kind, String os = null)
    {
        requireProjectSelected();

        if (os == null) os = "windows";
        switch (os.ToLower())
        {
            case "windows": 
                if(m_project.Keyword == EKeyword.None)      // flags ("MFC") can also set this
                    m_project.Keyword = EKeyword.Win32Proj; 
                break;
            case "android": m_project.Keyword = EKeyword.Android; break;
            case "package": m_project.Keyword = EKeyword.Package; break;
            default:
                throw new Exception2("os value is not supported '" + os + "' - supported values are: windows, android, package");
        }
        

        EConfigurationType type;
        var enums = Enum.GetValues(typeof(EConfigurationType)).Cast<EConfigurationType>();
        ESubSystem subsystem = ESubSystem.Windows;

        switch (_kind)
        {
            case "ConsoleApp":          type = EConfigurationType.Application; subsystem = ESubSystem.Console; break;
            case "WindowedApp":         type = EConfigurationType.Application; break;
            case "Application":         type = EConfigurationType.Application; break;
            case "SharedLib":           type = EConfigurationType.DynamicLibrary; break;
            case "DynamicLibrary":      type = EConfigurationType.DynamicLibrary; break;
            case "StaticLib":           type = EConfigurationType.StaticLibrary; break;
            case "Utility":             type = EConfigurationType.Utility; m_project.Keyword = EKeyword.None; break;
            default:
                throw new Exception2("kind value is not supported '" + _kind + "' - supported values are: " + String.Join(",", enums.Select(x => x.ToString()) ));
        }

        if (type == EConfigurationType.Utility )
        {
            foreach (String _platform in m_project.getPlatforms())
            {
                String platform = _platform.ToLower();
                    
                if( platform != "win32" && platform != "x64" )
                    throw new Exception("Utility projects can be defined only on platform 'Win32' or 'x64' - not others.");
            }
        }

        foreach (var conf in getSelectedProjectConfigurations())
        {
            conf.ConfigurationType = type;
            conf.SubSystem = subsystem;
        }
    } //kind

    /// <summary>
    /// Selects the compiler, linker, etc. which are used to build a project or configuration.
    /// </summary>
    /// <param name="toolset">
    /// For example:<para />
    ///     'v140' - for Visual Studio 2015.<para />
    ///     'v120' - for Visual Studio 2013.<para />
    /// </param>
    static public void toolset(String toolset)
    {
        foreach (var conf in getSelectedProjectConfigurations())
            conf.PlatformToolset = toolset;
    }

    
    /// <summary>
    /// Sets current android api level. Default is "android-19".
    /// </summary>
    /// <param name="apilevel">Android api level</param>
    static public void androidapilevel(String apilevel)
    {
        foreach (var conf in getSelectedProjectConfigurations())
            conf.AndroidAPILevel = apilevel;
    }

    /// <summary>
    /// Sets specific STL library for Android platform.
    /// </summary>
    /// <param name="useofstl"></param>
    static public void useofstl(String useofstl)
    {
        var values = Configuration.UseOfStl_getSupportedValues();
        int index = values.IndexOf(useofstl);
        if (index == -1)
            throw new Exception2("Use of STL value '" + useofstl + "' is not supported / invalid.\r\nValid values are: " + String.Join(", ", values));

        EUseOfStl v = (EUseOfStl)Enum.Parse(typeof(EUseOfStl), typeof(EUseOfStl).GetEnumNames()[index]);

        foreach (var conf in getSelectedProjectConfigurations())
            conf.UseOfStl = v;
    }


    /// <summary>
    /// Selects character set.
    /// </summary>
    /// <param name="charset">One of following: "Unicode", "Multibyte", "MBCS"</param>
    static public void characterset(String charset)
    {
        ECharacterSet cs;
        switch (charset.ToLower())
        {
            case "unicode":     cs = ECharacterSet.Unicode;   break;
            case "mbcs":        cs = ECharacterSet.MultiByte; break;
            case "multibyte":   cs = ECharacterSet.MultiByte; break;
            default:
                throw new Exception2("characterset value is not supported '" + charset + "' - supported values are: " + 
                    String.Join(",", Enum.GetValues(typeof(ECharacterSet)).Cast<ECharacterSet>().Select(x => x.ToString())));
        } //switch

        foreach (var conf in getSelectedProjectConfigurations())
            conf.CharacterSet = cs;
    }

    /// <summary>
    /// Specifies output directory.
    /// </summary>
    static public void targetdir(String directory)
    {
        directory = directory.Replace("/", "\\");
        if (!directory.EndsWith("\\"))
            directory += "\\";

        foreach (var conf in getSelectedProjectConfigurations())
            conf.OutDir = directory;
    }

    /// <summary>
    /// Specifies intermediate Directory.
    /// </summary>
    /// <param name="directory">For example "$(Configuration)\" or "obj\$(Platform)\$(Configuration)\"</param>
    static public void objdir(String directory)
    {
        directory = directory.Replace("/", "\\");
        if (!directory.EndsWith("\\"))
            directory += "\\";

        foreach (var conf in getSelectedProjectConfigurations())
            conf.IntDir = directory;
    }

    /// <summary>
    /// Specifies target name. (Filename without extension)
    /// </summary>
    static public void targetname(String name)
    {
        foreach (var conf in getSelectedProjectConfigurations())
            conf.TargetName = name;
    }

    /// <summary>
    /// Specifies target file extension, including comma separator.
    /// </summary>
    /// <param name="extension">For example ".dll", ".exe"</param>
    static public void targetextension(String extension)
    {
        foreach (var conf in getSelectedProjectConfigurations())
            conf.TargetExt = extension;
    }

    /// <summary>
    /// Specifies the #include form of the precompiled header file name.
    /// </summary>
    /// <param name="file">header file</param>
    static public void pchheader(String file)
    {
        foreach (var conf in getSelectedProjectConfigurations())
        {
            conf.PrecompiledHeader = EPrecompiledHeaderUse.Use;
            conf.PrecompiledHeaderFile = file;
        }
    }

    /// <summary>
    /// Specifies the C/C++ source code file which controls the compilation of the header.
    /// </summary>
    /// <param name="file">precompiled source code which needs to be compiled</param>
    static public void pchsource(String file)
    {
        var bkp1 = selectedFileConfigurations;
        var bkp2 = bLastSetFilterWasFileSpecific;

        List<String> fileFilter = new List<string>();
        fileFilter.Add("files:" + file);

        if (selectedFilters != null)
            fileFilter.AddRange(selectedFilters);

        filter(fileFilter.ToArray());

        foreach (var conf in getSelectedConfigurations(false))
            conf.PrecompiledHeader = EPrecompiledHeaderUse.Create;

        selectedFileConfigurations = bkp1;
        bLastSetFilterWasFileSpecific = bkp2;
    } //pchsource

    /// <summary>
    /// Specified whether debug symbols are enabled or not.
    /// </summary>
    /// <param name="value">
    /// "on" - debug symbols are enabled<para />
    /// "off" - debug symbols are disabled<para />
    /// "fastlink" - debug symbols are enabled + faster linking enabled.<para />
    /// </param>
    static public void symbols(String value)
    {
        //
        //  symbols preconfigures multiple flags. premake5 also preconfigures multiple. Maybe later if needs to be separated - create separate functions calls for
        //  each configurable parameter.
        //
        EGenerateDebugInformation d;
        bool bUseDebugLibraries = false;
        switch (value.ToLower())
        {
            case "on":          d = EGenerateDebugInformation.OptimizeForDebugging; bUseDebugLibraries = true;  break;
            case "off":         d = EGenerateDebugInformation.No; bUseDebugLibraries = false; break;
            case "fastlink":    d = EGenerateDebugInformation.OptimizeForFasterLinking; bUseDebugLibraries = true; break;
            default:
                throw new Exception2("Allowed symbols() values are: on, off, fastlink");
        }

        foreach (var conf in getSelectedProjectConfigurations())
        {
            conf.GenerateDebugInformation = d;
            conf.UseDebugLibraries = bUseDebugLibraries;
            optimize_symbols_recheck(conf);
        } //foreach
    }

    /// <summary>
    /// Specifies additional include directories.
    /// </summary>
    /// <param name="dirs">List of additional include directories.</param>
    static public void includedirs(params String[] dirs)
    {
        foreach (var conf in getSelectedConfigurations(false))
        {
            if (conf.AdditionalIncludeDirectories.Length != 0)
                conf.AdditionalIncludeDirectories += ";";

            conf.AdditionalIncludeDirectories += String.Join(";", dirs);
        }
    }

    /// <summary>
    /// Specifies system include directories.
    /// </summary>
    /// <param name="dirs">List of include directories.</param>
    static public void sysincludedirs(params String[] dirs)
    {
        foreach (var conf in getSelectedProjectConfigurations())
        {
            if (conf.IncludePath.Length != 0)
                conf.IncludePath += ";";

            conf.IncludePath += String.Join(";", dirs);
        }
    }

    /// <summary>
    /// Specifies system library directories.
    /// </summary>
    /// <param name="dirs">List of include directories.</param>
    static public void syslibdirs(params String[] dirs)
    {
        foreach (var conf in getSelectedProjectConfigurations())
        {
            if (conf.LibraryPath.Length != 0)
                conf.LibraryPath += ";";

            conf.LibraryPath += String.Join(";", dirs);
        }
    }

    /// <summary>
    /// Specifies additional defines.
    /// </summary>
    /// <param name="defines">defines, like for example "DEBUG", etc...</param>
    static public void defines(params String[] defines)
    {
        foreach (var conf in getSelectedConfigurations(false))
        {
            if (conf.PreprocessorDefinitions.Length != 0)
                conf.PreprocessorDefinitions += ";";

            conf.PreprocessorDefinitions += String.Join(";", defines);
        }
    }

    /// <summary>
    /// Matches files from folder _dir using glob file pattern.
    /// In glob file pattern matching * reflects to any file or folder name, ** refers to any path (including sub-folders).
    /// ? refers to any character.
    /// 
    /// There exists also 3-rd party library for performing similar matching - 'Microsoft.Extensions.FileSystemGlobbing'
    /// but it was dragging a lot of dependencies, I've decided to survive without it.
    /// </summary>
    /// <returns>List of files matches your selection</returns>
    static public String[] matchFiles( String _dir, String filePattern )
    {
        if (filePattern.IndexOfAny(new char[] { '*', '?' }) == -1)      // Speed up matching, if no asterisk / widlcard, then it can be simply file path.
        {
            String path = Path.Combine(_dir, filePattern);
            if (File.Exists(path))
                return new String[] { filePattern };
            return new String[] { };
        }

        String dir = Path.GetFullPath(_dir);        // Make it absolute, just so we can extract relative path'es later on.
        String[] pattParts = filePattern.Replace("/", "\\").Split('\\');
        List<String> scanDirs = new List<string>();
        scanDirs.Add(dir);

        //
        //  By default glob pattern matching specifies "*" to any file / folder name, 
        //  which corresponds to any character except folder separator - in regex that's "[^\\]*"
        //  glob matching also allow double astrisk "**" which also recurses into subfolders. 
        //  We split here each part of match pattern and match it separately.
        //
        for (int iPatt = 0; iPatt < pattParts.Length; iPatt++)
        {
            bool bIsLast = iPatt == (pattParts.Length - 1);
            bool bRecurse = false;

            String regex1 = Regex.Escape(pattParts[iPatt]);         // Escape special regex control characters ("*" => "\*", "." => "\.")
            String pattern = Regex.Replace(regex1, @"\\\*(\\\*)?", delegate (Match m)
                {
                    if (m.ToString().Length == 4)   // "**" => "\*\*" (escaped) - we need to recurse into sub-folders.
                    {
                        bRecurse = true;
                        return ".*";
                    }
                    else
                        return @"[^\\]*";
                }).Replace(@"\?", ".");

            if (pattParts[iPatt] == "..")                           // Special kind of control, just to scan upper folder.
            {
                for (int i = 0; i < scanDirs.Count; i++)
                    scanDirs[i] = scanDirs[i] + "\\..";
                
                continue;
            }
                
            Regex re = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            int nScanItems = scanDirs.Count;
            for (int i = 0; i < nScanItems; i++)
            {
                String[] items;
                if (!bIsLast)
                    items = Directory.GetDirectories(scanDirs[i], "*", (bRecurse) ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
                else
                    items = Directory.GetFiles(scanDirs[i], "*", (bRecurse) ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);

                foreach (String path in items)
                {
                    String matchSubPath = path.Substring(scanDirs[i].Length + 1);
                    if (re.Match(matchSubPath).Success)
                        scanDirs.Add(path);
                }
            }
            scanDirs.RemoveRange(0, nScanItems);    // Remove items what we have just scanned.
        } //for

        //  Make relative and return.
        return scanDirs.Select( x => x.Substring(dir.Length + 1) ).ToArray();
    } //matchFiles


    /// <summary>
    /// Adds one or more file into project.
    /// </summary>
    /// <param name="filePatterns">File patterns to be added</param>
    static public void files(params String[] filePatterns)
    {
        requireProjectSelected();

        foreach (String _filePattern in filePatterns)
        {
            bool bMandatory = true;
            
            String filePattern;
            if (_filePattern.StartsWith("??"))              // Escape questionmark for pattern matching.
            {
                filePattern = _filePattern.Substring(1);
            }
            else
            {
                if (_filePattern.StartsWith("?"))
                {
                    filePattern = _filePattern.Substring(1);
                    bMandatory = false;
                }
                else
                {
                    filePattern = _filePattern;
                }
            }

            String[] fileList = matchFiles(m_project.getProjectFolder(), filePattern);

            if (bMandatory && fileList.Length == 0)
            {
                throw new Exception2("No file found which is specified by pattern '" + filePattern + "'.\r\n" +
                    "If file is generated during project build, please mark it as optional with '?' character in front of filename - for example files(\"?temp.txt\") "
                );
            }

            foreach (String file in fileList)
            {
                // If file already exists in project, we just ignore and continue.
                bool bExist = m_project.files.Where(x => x.relativePath == file).FirstOrDefault() != null;
                if (bExist)
                    continue;

                String fullFilePath = Path.Combine(m_project.getProjectFolder(), filePattern);
                FileInfo fi = new FileInfo() { relativePath = file };

                switch (Path.GetExtension(filePattern).ToLower())
                {
                    case ".properties":
                        fi.includeType = IncludeType.AntProjectPropertiesFile; break;
                    case ".xml":
                        {
                            String filename = Path.GetFileNameWithoutExtension(filePattern).ToLower();
                            if (filename == "build")
                                fi.includeType = IncludeType.AntBuildXml;
                            else if (filename.Contains("manifest"))
                                fi.includeType = IncludeType.AndroidManifest;
                            else
                                fi.includeType = IncludeType.Content;
                        }
                        break;
                    case ".c":
                    case ".cxx":
                    case ".cpp":
                        fi.includeType = IncludeType.ClCompile; break;

                    case ".h":
                        fi.includeType = IncludeType.ClInclude; break;

                    case ".rc":
                        fi.includeType = IncludeType.ResourceCompile; break;

                    case ".ico":
                        fi.includeType = IncludeType.Image; break;

                    case ".txt":
                        fi.includeType = IncludeType.Text; break;

                    default:
                        fi.includeType = IncludeType.None; break;
                }

                m_project.files.Add(fi);
            }
        } //foreach
    } //files

    /// <summary>
    /// Specifies custom build rule for specific file.
    /// </summary>
    /// <param name="cbt">Custom build rule.</param>
    static public void buildrule(CustomBuildRule cbt)
    { 
        requireProjectSelected();
        foreach (var conf in getSelectedConfigurations(false))
            conf.customBuildRule = cbt;
    } //buildrule


    /// <summary>
    /// Sets up custom build rule for project or solution configuration script.
    /// </summary>
    /// <param name="script2include">Script to include into project</param>
    /// <param name="script2compile">Script which shall be compiled once script2include is changed</param>
    /// <param name="pathToSyncProjExe">Path where syncProj.exe will reside</param>
    /// <param name="inDir">Where solution or project is located.</param>
    static void selfCompileScript(String script2include, String script2compile, String pathToSyncProjExe, String inDir)
    {
        if (script2compile == null)
            script2compile = script2include;

        pathToSyncProjExe = SolutionOrProject.getSyncProjExeLocation(inDir, pathToSyncProjExe);

        using (new UsingSyncProj(2 /*called from projectScript & solutionScript - 2 frames in call stack */))
        {
            files(script2include);
            filter("files:" + script2include);

            String scriptDir = Path.Combine(m_workPath, m_scriptRelativeDir);
            String tempLogFile = "$(IntermediateOutputPath)" + Path.GetFileName(script2compile).Replace('.', '_') + "_log.txt";

            buildrule(
                new CustomBuildRule()
                {
                    Command = "\"" + pathToSyncProjExe + "\" $(ProjectDir)" + script2compile + "\r\n" + "echo 1>" + tempLogFile,
                    Outputs = tempLogFile,
                    Message = ""
                }
            );
            filter();
        }
    }

    /// <summary>
    /// Configures project rebuild step.
    /// </summary>
    /// <param name="script2include">Script to include into project</param>
    /// <param name="script2compile">Script which shall be compiled once script2include is changed</param>
    /// <param name="pathToSyncProjExe">Path where syncProj.exe will reside</param>
    static public void projectScript(String script2include, String script2compile = null, String pathToSyncProjExe = null )
    {
        requireProjectSelected();
        selfCompileScript(script2include, script2compile, pathToSyncProjExe, m_project.getProjectFolder());
    }

    /// <summary>
    /// Configures solution rebuild step.
    /// </summary>
    /// <param name="script2include">Script to include into project</param>
    /// <param name="script2compile">Script which shall be compiled once script2include is changed</param>
    /// <param name="pathToSyncProjExe">Path where syncProj.exe will reside</param>
    static public void solutionScript(String script2include, String script2compile = null, String pathToSyncProjExe = null )
    {
        if (script2compile == null)
            script2compile = script2include;

        requireSolutionSelected();
        String projectName = Path.GetFileNameWithoutExtension(script2compile);

        using (new UsingSyncProj(1))
        {
            group("0 Solution Update");
            project(projectName);
                platforms("Win32");
                configurations(m_configurations.ToArray());
                kind("Utility");
                // Redirect so would not conflict with existing projects.
                objdir("obj/" + projectName + "_temp");

            foreach (String plat in m_solution.getPlatforms())
                configmap(plat, "Win32");
        }

        selfCompileScript(script2include, script2compile, pathToSyncProjExe, m_solution.getSolutionFolder());
        group("");
        solutionUpdateProject = projectName;
    }

    /// <summary>
    /// Defines configuration mapping from solution (first argument) to project (second argument)
    /// For example configmap( "Development", "Debug" )
    /// </summary>
    /// <param name="confList"></param>
    static public void configmap(params String[] confList)
    {
        requireSolutionSelected();
        requireProjectSelected();

        if (confList.Length % 2 == 1)
            throw new Exception2("Input argument count must be dividable by 2 (solution, project configuration pairs)");

        if (m_project.slnConfigurations == null || m_solution.configurations.Count != m_project.slnConfigurations.Count)
        {
            m_project.slnConfigurations.Clear();
            // Make 1 to 1 mapping.
            m_project.slnConfigurations.AddRange(m_solution.configurations);
        }

        for (int i = 0; i < confList.Length; i += 2)
        {
            String from = confList[i].ToLower();
            String to = confList[i + 1];
            int matchMethod = 0;

            for( int iConf = 0; iConf < m_solution.configurations.Count; iConf++)
            {
                String conf = m_solution.configurations[iConf];
                bool match = false;
                String targetConf = "";

                // In full style, for example "Debug|Win32"
                if (from.Contains('|'))
                {
                    match = from == conf.ToLower();
                    targetConf = to;
                }
                else
                {
                    String[] confPlat = conf.Split('|');

                    // In partial style, for example "Debug" or "Win32"
                    if (matchMethod == 0)
                    {
                        // Once we have detected whether we are matching configuration name or platform, 
                        // we keep matching same thing.
                        if (from == confPlat[0].ToLower())
                        {
                            matchMethod = 1;
                        }
                        else
                        {
                            if (from == confPlat[1].ToLower())
                                matchMethod = 2;
                        }
                    } //if

                    switch (matchMethod)
                    {
                        case 1:
                            match = (from == confPlat[0].ToLower());
                            targetConf = to + "|" + confPlat[1];
                            break;
                        case 2:
                            match = (from == confPlat[1].ToLower());
                            targetConf = confPlat[0] + "|" + to;
                            break;
                    }
                }

                if (match)
                    m_project.slnConfigurations[iConf] = targetConf;
            } //foreach
        } //for
    } //configmap

    /// <summary>
    /// Enables certain flags for specific configurations.
    /// </summary>
    /// <param name="flags">
    /// "LinkTimeOptimization" - Enable link-time (i.e. whole program) optimizations.<para />
    /// </param>
    static public void flags(params String[] flags)
    {
        requireProjectSelected();

        foreach (String flag in flags)
        {
            switch (flag.ToLower())
            {
                case "linktimeoptimization":
                    {
                        foreach (var conf in getSelectedProjectConfigurations())
                            conf.WholeProgramOptimization = EWholeProgramOptimization.UseLinkTimeCodeGeneration;
                    }
                    break;

                case "mfc":
                    m_project.Keyword = EKeyword.MFCProj;

                    foreach (var conf in getSelectedProjectConfigurations())
                        if (conf.UseOfMfc == EUseOfMfc.None )
                            conf.UseOfMfc = EUseOfMfc.Dynamic;
                    break;

                case "staticruntime":
                    foreach (var conf in getSelectedProjectConfigurations())
                        conf.UseOfMfc = EUseOfMfc.Static;
                    break;

                case "nopch":
                    foreach (var conf in getSelectedConfigurations(false))
                        conf.PrecompiledHeader = EPrecompiledHeaderUse.NotUsing;
                    break;

                default:
                    throw new Exception2("Flag '" + flag + "' is not supported");
            } //switch 
        } //foreach
    } //flags

    /// <summary>
    /// Sets platform version.
    /// </summary>
    /// <param name="ver">Target Platform Version, e.g. "8.1" or "10.0.14393.0"</param>
    static public void systemversion(String ver)
    { 
        requireProjectSelected();
        m_project.WindowsTargetPlatformVersion = ver;
    }

    /// <summary>
    /// Adds one or more obj or lib into project to link against.
    /// </summary>
    /// <param name="files"></param>
    static public void links(params String[] files)
    { 
        foreach (var conf in getSelectedProjectConfigurations())
        {
            if (conf.AdditionalDependencies.Length != 0)
                conf.AdditionalDependencies += ";";

            conf.AdditionalDependencies += String.Join(";", files);
        }
    } //links


    /// <summary>
    /// Adds one or more library directory from where to search .obj / .lib files.
    /// </summary>
    /// <param name="folders"></param>
    static public void libdirs(params String[] folders)
    {
        foreach (var conf in getSelectedProjectConfigurations())
        {
            if (conf.AdditionalLibraryDirectories.Length != 0)
                conf.AdditionalLibraryDirectories += ";";

            conf.AdditionalLibraryDirectories += String.Join(";", folders);
        }
    } //links


    /// <summary>
    /// optimize and symbols reflect to debug format chosen.
    /// </summary>
    /// <param name="fconf"></param>
    static void optimize_symbols_recheck(FileConfigurationInfo fconf)
    {
        Configuration conf = fconf as Configuration;
        if (conf == null) 
            return;     // Project wide configuration only.

        EDebugInformationFormat debugFormat = EDebugInformationFormat.None;
        if (conf.UseDebugLibraries)
        {
            if (m_project.Keyword == EKeyword.Android)
            {
                // Android
                debugFormat = EDebugInformationFormat.FullDebug;
            }
            else
            {
                // Windows
                if (conf.Optimization == EOptimization.Full)
                {
                    debugFormat = EDebugInformationFormat.ProgramDatabase;
                }
                else
                {
                    debugFormat = EDebugInformationFormat.EditAndContinue;
                }
            }
        }
        else 
        { 
            debugFormat = EDebugInformationFormat.None;
        }

        conf.DebugInformationFormat = debugFormat;
    } //optimize_symbols_recheck


    /// <summary>
    /// Specifies optimization level to be used.
    /// </summary>
    /// <param name="optLevel">Optimization level to enable - one of following: off, size, speed, on(or full)</param>
    static public void optimize(String optLevel)
    {
        EOptimization opt;
        bool bFunctionLevelLinking = true;
        bool bIntrinsicFunctions = true;
        bool bEnableCOMDATFolding = false;
        bool bOptimizeReferences = false;

        switch (optLevel.ToLower())
        {
            case "custom": 
                opt = EOptimization.Custom; 
                break;
            case "off": 
                opt = EOptimization.Disabled; 
                bFunctionLevelLinking = false; 
                bIntrinsicFunctions = false; 
                break;
            case "full":
            case "on": 
                opt = EOptimization.Full; 
                bEnableCOMDATFolding = true; 
                bOptimizeReferences = true;
                break;
            case "size": 
                opt = EOptimization.MinSpace; 
                break;
            case "speed": 
                opt = EOptimization.MaxSpeed;
                break;
            default:
                throw new Exception2("Allowed optimization() values are: off, size, speed, on(or full)");
        }

        foreach (var conf in getSelectedConfigurations(false))
        {
            conf.Optimization = opt;
            conf.FunctionLevelLinking = bFunctionLevelLinking;
            conf.IntrinsicFunctions = bIntrinsicFunctions;
            conf.EnableCOMDATFolding = bEnableCOMDATFolding;
            conf.OptimizeReferences = bOptimizeReferences;
            optimize_symbols_recheck(conf);
        }
    } //optimize

    /// <summary>
    /// Passes arguments directly to the compiler command line without translation.
    /// </summary>
    static public void buildoptions(params String[] options)
    {
        foreach (var conf in getSelectedConfigurations(false))
        {
            if (conf.ClCompile_AdditionalOptions.Length != 0)
                conf.ClCompile_AdditionalOptions += " ";

            conf.ClCompile_AdditionalOptions += String.Join(" ", options);
        }
    }

    /// <summary>
    /// Passes arguments directly to the linker command line without translation.
    /// </summary>
    static public void linkoptions(params String[] options)
    {
        foreach (var conf in getSelectedConfigurations(false))
        {
            if (conf.Link_AdditionalOptions.Length != 0)
                conf.Link_AdditionalOptions += " ";

            conf.Link_AdditionalOptions += String.Join(" ", options);
        }
    }

    /// <summary>
    /// Sets current object filename (output of compilation result).
    /// Windows: Can be file or directory name.
    /// Android: Typically: "$(IntDir)%(filename).o"
    /// </summary>
    /// <param name="objFilename"></param>
    static public void objectfilename(String objFilename)
    {
        foreach (var conf in getSelectedConfigurations(false))
            conf.ObjectFileName = objFilename;   
    }

    /// <summary>
    /// Advanced feature - forces particular file to preprocess only file (to be compiled).
    /// This can be used for checking define expansion.
    /// </summary>
    /// <param name="file">File to preprocess</param>
    /// <param name="outExtension">Output file extension</param>
    /// <param name="bDoPreprocess">true if you want to preprocess file, false if compile normally, but include only preprocessed output</param>
    static public void preprocessFile( String file, bool bDoPreprocess = true, String outExtension = ".preprocessed")
    {
        requireProjectSelected();

        if (!File.Exists(file))
            throw new Exception2("File '" + file + "' does not exist." );

        files(file);

        if (bDoPreprocess)
        {
            filter("files:" + file);

            if (m_project.Keyword == EKeyword.Win32Proj)
            {
                // MS C++ compiler
                buildoptions("/P /Fi\"%(FullPath)" + outExtension + "\"");
            }
            else
            {
                // clang or gcc
                buildoptions("-E");
                objectfilename("%(FullPath)" + outExtension);
            }
            filter();
        }

        files("?" + file + outExtension);
    } //preprocessFiles


    /// <summary>
    /// Enables show includes only for specific file.
    /// </summary>
    /// <param name="fileList">files for which to enable showIncludes.</param>
    static public void showIncludes(params String[] fileList)
    {
        requireProjectSelected();

        using (new UsingSyncProj(1))
        {
            foreach (String file in fileList)
            {
                // If file already exists in project, we just ignore and continue.
                FileInfo fi = m_project.files.Where(x => x.relativePath == file).FirstOrDefault();
                if (fi == null)
                {
                    files(file);
                    fi = m_project.files.Where(x => x.relativePath == file).FirstOrDefault();
                }

                filter("files:" + file);

                foreach (var conf in getSelectedConfigurations(false))
                {
                    if (m_project.Keyword == EKeyword.Win32Proj)
                    {
                        conf.ShowIncludes = true;
                    }
                    else
                    {
                        buildoptions("-M");
                        objectfilename("");
                    }
                }
            } //foreach
            
            filter();
        } //using

    } //showIncludes

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
            fromWhere = ex2.getThrowLocation();

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

