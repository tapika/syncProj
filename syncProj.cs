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
using System.ComponentModel;

class Dictionary2<TKey, TValue> : Dictionary<TKey, TValue>
{
    public TValue this[TKey name]
    {
        get
        {
            if (!ContainsKey(name))
            {
                TValue v = default(TValue);
                Type t = typeof(TValue);
                if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>))
                    v = (TValue)Activator.CreateInstance(t);
                Add(name, v);
            }
            return base[name];
        }

        set {
            base[name] = value;
        }
    }
}



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
    /// Walks through list of items, locates value using fieldName, identifies how many configuration lines we will need
    /// and inserts lines into lines2dump.
    /// </summary>
    /// <param name="proj">Project which hosts configuration list.</param>
    /// <param name="list">List to get values from</param>
    /// <param name="fieldName">Field name to scan </param>
    /// <param name="lines2dump">Lines which shall be created / updated.</param>
    /// <param name="valueToLine">value to config line translator function</param>
    static void ConfigationSpecificValue(Project proj, 
        IList list, 
        String fieldName, 
        Dictionary2<String, List<String>> lines2dump,
        Func<String, String> valueToLine
    )
    {
        if (list.Count == 0)
            return;

        Type itemType = list[0].GetType();
        FieldInfo fi = itemType.GetField(fieldName);
        // Value to it's repeatability.
        Dictionary2<String, int> weigths = new Dictionary2<string, int>();
        bool bCheckPlatformsVariation = proj.getPlatforms().Count > 1;
        bool bCheckConfigurationNameVariation = proj.getConfigurationNames().Count > 1;

        //---------------------------------------------------------------------
        // Select generic value, which is repeated in most of configurations.
        //---------------------------------------------------------------------
        for (int i = 0; i < list.Count; i++)
        {
            String value = fi.GetValue(list[i]).ToString();
            weigths[value]++;
        }
        int maxWeight = weigths.Values.Max();
        List<String> maxValues = weigths.Where(x => x.Value == maxWeight).Select(x => x.Key).ToList();
        String defaultValue = null;

        if (maxValues.Count == 1)
            defaultValue = maxValues[0];

        weigths.Clear();
        //---------------------------------------------------------------------
        // Select by config or by platform specific values repeated in most of
        // configuration, and which are not satisfied by defaultValue if any.
        //---------------------------------------------------------------------
        for (int i = 0; i < list.Count; i++)
        {
            String value = fi.GetValue(list[i]).ToString();

            if (defaultValue != null && value == defaultValue)
                continue;

            String[] configNamePlatform = proj.configurations[i].Split('|');
            weigths["c" + configNamePlatform[0]]++;
            weigths["p" + configNamePlatform[1]]++;
        }
        
        // if we have some configuration defined, try to add to it.
        foreach (var kv in lines2dump)
            weigths[kv.Key]++;

        // Remove all single repeated entries ( Can list them separately )
        foreach (var k in weigths.Where(x => x.Value <= 1).Select(x => x.Key).ToList())
        weigths.Remove(k);

        // Configuration name "" (global), "c|p<Name>" (specific to configuration name or to platform)
        // "m<ConfigName>|<PlatformName>" - configuration & platform specific item.
        Dictionary<String, String> configToValue = new Dictionary<string, string>();

        //---------------------------------------------------------------------
        // Finally collect all single entries values, to see if defaultValue
        // and by config or by platform matches are needed anymore.
        //---------------------------------------------------------------------
        for (int i = 0; i < list.Count; i++)
        {
            String value = fi.GetValue(list[i]).ToString();
            String[] configNamePlatform = proj.configurations[i].Split('|');
            String configName = configNamePlatform[0];
            String platform = configNamePlatform[1];

            if (configToValue.ContainsKey("") && configToValue[""] == value)
                continue;

            bool matched = false;

            foreach (var cvpair in configToValue)
            {
                matched = cvpair.Key.StartsWith("c") && cvpair.Key.Substring(1) == configName && cvpair.Value == value;
                if (matched) break;

                matched = cvpair.Key.StartsWith("p") && cvpair.Key.Substring(1) == platform && cvpair.Value == value;
                if (matched) break;
            }

            if (matched)
                continue;

            if (defaultValue != null && defaultValue == value)      // We have default value, and our default value matches our value.
            {
                configToValue[""] = value;      // Rule by default value.
                continue;
            }

            bool bCheckConfigurationName = bCheckConfigurationNameVariation;
            if (configToValue.ContainsKey("c" + configName))
            {
                if (configToValue["c" + configName] == value)
                    continue;

                bCheckConfigurationName = false;
            }

            if (bCheckConfigurationName && weigths.ContainsKey("c" + configName) )
            {
                configToValue["c" + configName] = value;
                continue;
            }

            bool bCheckPlatform = bCheckPlatformsVariation;
            if (configToValue.ContainsKey("p" + platform))
            {
                if (configToValue["p" + platform] == value)
                    continue;

                bCheckPlatform = false;
            }

            if (bCheckPlatform && weigths.ContainsKey("p" + platform))
            {
                configToValue["p" + platform] = value;
                continue;
            }

            configToValue["m" + proj.configurations[i]] = value;
        } //for

        foreach (var cvpair in configToValue)
            lines2dump[cvpair.Key].Add(valueToLine(cvpair.Value));
    } //ConfigationSpecificValue



    /// <summary>
    /// Builds solution or project .lua/.cs scripts
    /// </summary>
    /// <param name="path">Full path from where project was loaded.</param>
    /// <param name="solutionOrProject">Solution or project</param>
    /// <param name="bProcessProjects">true to process sub-project, false not</param>
    /// <param name="format">lua or cs</param>
    /// <param name="outFile">Output filename without extension</param>
    static public void UpdateProjectScript( String path, object solutionOrProject, String outFile, String format, bool bProcessProjects, String outPrefix)
    {
        bool bCsScript = (format == "cs");
        String brO = " ", brC = "";
        String arO = " { ", arC = " }";
        String head = "";
        String comment = "-- ";
        if (bCsScript)
        {
            brO = "("; brC = ");";
            arO = "( "; arC = " );";
            head = "    ";
            comment = "// ";
        }

        String fileName = outFile;
        if( fileName == null ) fileName = Path.GetFileNameWithoutExtension(path);
        if (outPrefix != "") fileName = outPrefix + fileName;

        String outDir = Path.GetDirectoryName(path);
        String outPath = Path.Combine(outDir, fileName + "." + format);

        Console.WriteLine("- Updating '" + fileName + "." + format + "...");
        StringBuilder o = new StringBuilder();
        Solution sln = solutionOrProject as Solution;
        Project proj = solutionOrProject as Project;

        //
        // C# script header
        //
        if (bCsScript)
        {
            o.AppendLine("//css_ref " +
                Path2.makeRelative(Assembly.GetExecutingAssembly().Location, Path.GetDirectoryName(path)));
            o.AppendLine("using System;         //Exception");
            o.AppendLine();
            o.AppendLine("partial class Builder: SolutionProjectBuilder");
            o.AppendLine("{");
            o.AppendLine();
            if( sln != null )
                o.AppendLine("  static void Main()");
            else
                o.AppendLine("  static void project" + proj.ProjectName + "()");
            
            o.AppendLine("  {");
            o.AppendLine();
            o.AppendLine("    try {");
        }

        o.AppendLine("");

        if (sln != null)
        {
            // ---------------------------------------------------------------------------------
            //  Building solution
            // ---------------------------------------------------------------------------------
            o.AppendLine(head + "solution" + brO + "\"" + fileName + "\"" + brC);

            o.AppendLine(head + "    configurations" + arO + " " + String.Join(",", sln.configurations.Select(x => "\"" + x.Split('|')[0] + "\"").Distinct()) + arC);
            o.AppendLine(head + "    platforms" + arO + String.Join(",", sln.configurations.Select(x => "\"" + x.Split('|')[1] + "\"").Distinct()) + arC);

            String wasInSubGroup = "";
            List<String> groupParts = new List<string>();

            foreach (Project p in sln.projects)
            {
                if (p.IsSubFolder())
                    continue;

                String projectPath = Path.Combine(outDir, p.RelativePath);
                if(bProcessProjects)
                    UpdateProjectScript(projectPath, p, null, format, false, outPrefix);
                    
                // Defines group / in which sub-folder we are.
                groupParts.Clear();
                Project pScan = p.parent;
                for (; pScan != null; pScan = pScan.parent)
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

                if (bProcessProjects)
                {
                    String fileInclude = name;
                    if (outPrefix != "") fileInclude = outPrefix + name;

                    o.AppendLine(head + "    " + comment + "Project '" + fileInclude + "'");

                    if (format == "lua")
                    {
                        fileInclude = Path.Combine(dir, fileInclude + "." + format);
                        fileInclude = fileInclude.Replace("\\", "/");

                        o.AppendLine(head + "    include \"" + fileInclude + "\"");
                    }
                }
                else
                {
                    o.AppendLine(head + "    externalproject" + brO + "\"" + name + "\"" + brC);
                    o.AppendLine(head + "        location" + brO + "\"" + dir.Replace("\\", "/") + "\"" + brC);
                    o.AppendLine(head + "        uuid" + brO + "\"" + p.ProjectGuid.Substring(1, p.ProjectGuid.Length - 2) + "\"" + brC);
                    o.AppendLine(head + "        language" + brO + "\"C++\"" + brC);
                    o.AppendLine(head + "        kind" + brO + "\"SharedLib\"" + brC);
                } //if-else

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

                        o.AppendLine(head + "        dependson" + brO + "\"" + outPrefix + depp.ProjectName + "\"" + brC);
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
                o.AppendLine("}; //class Builder");
                o.AppendLine();
            }
        }
        else {
            // ---------------------------------------------------------------------------------
            //  Building project
            // ---------------------------------------------------------------------------------
            //o.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            //o.AppendLine("<Project DefaultTargets=\"Build\" ToolsVersion=\"12.0\" xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">");
            //o.AppendLine("  <ItemGroup Label=\"ProjectConfigurations\">");

            o.AppendLine(head + "project" + brO + "\"" + fileName + "\"" + brC);

            if (format == "lua")
                o.AppendLine(head + "    location \".\"");

            o.AppendLine(head + "    configurations" + arO + " " + String.Join(",", proj.configurations.Select(x => "\"" + x.Split('|')[0] + "\"").Distinct()) + arC);
            o.AppendLine(head + "    platforms" + arO + String.Join(",", proj.configurations.Select(x => "\"" + x.Split('|')[1] + "\"").Distinct()) + arC);
            o.AppendLine(head + "    uuid" + brO + "\"" + proj.ProjectGuid.Substring(1, proj.ProjectGuid.Length - 2) + "\"" + brC);

            Dictionary2<String, List<String>> lines2dump = new Dictionary2<string, List<string>>();

            ConfigationSpecificValue(proj, proj.projectConfig, "ConfigurationType", lines2dump, (s) => {
                    String r = typeof(EConfigurationType).GetMember(s)[0].GetCustomAttribute<PremakeTagAttribute>().tag;
                    return "kind" + brO + "\"" + r + "\"" + brC;
            } );

            ConfigationSpecificValue(proj, proj.projectConfig, "UseDebugLibraries", lines2dump, (s) => {
                return "symbols" + brO + "\"" + ((s == "True") ? "on" : "off") + "\"" + brC;
            });

            ConfigationSpecificValue(proj, proj.projectConfig, "PlatformToolset", lines2dump, (s) => {
                return "toolset" + brO + "\"" + s + "\"" + brC;
            });

            ConfigationSpecificValue(proj, proj.projectConfig, "CharacterSet", lines2dump, (s) => {
                String r = typeof(ECharacterSet).GetMember(s)[0].GetCustomAttribute<PremakeTagAttribute>().tag;
                return "characterset" + brO + "\"" + r + "\"" + brC;
            });

            ConfigationSpecificValue(proj, proj.projectConfig, "OutDir", lines2dump, (s) => { return "targetdir" + brO + "\"" + s.Replace("\\", "\\\\") + "\"" + brC; });
            ConfigationSpecificValue(proj, proj.projectConfig, "IntDir", lines2dump, (s) => { return "objdir" + brO + "\"" + s.Replace("\\", "\\\\") + "\"" + brC; });
            ConfigationSpecificValue(proj, proj.projectConfig, "TargetName", lines2dump, (s) => { return "targetname" + brO + "\"" + s + "\"" + brC; });
            ConfigationSpecificValue(proj, proj.projectConfig, "TargetExt", lines2dump, (s) => { return "targetextension" + brO + "\"" + s + "\"" + brC; });
            
            // Can be used only to enabled, for .lua - ConfigationSpecificValue should be changed to operate on some default (false) value.
            ConfigationSpecificValue(proj, proj.projectConfig, "WholeProgramOptimization", lines2dump, (s) => {
                if(s == "UseLinkTimeCodeGeneration" )
                    return "flags" + arO + "\"LinkTimeOptimization\"" + arC;
                return "";
            });

            foreach (var cfg in proj.projectConfig)
            {
                if (cfg.PrecompiledHeader == EPrecompiledHeaderUse.NotUsing)
                    cfg.PrecompiledHeaderFile = "";
            }

            ConfigationSpecificValue(proj, proj.projectConfig, "PrecompiledHeaderFile", lines2dump, (s) => {
                if (s != "")
                    return "pchheader" + brO + "\"" + s + "\"" + brC;
                return "flags" + arO + "\"NoPch\"" + arC;
            });

            //---------------------------------------------------------------------------------
            // Semicolon (';') separated lists.
            //  Like defines, additional include directories.
            //---------------------------------------------------------------------------------
            String[] fieldNames = new String[] { "PreprocessorDefinitions", "AdditionalIncludeDirectories" };
            String[] funcNames = new String[] { "defines", "includedirs" };

            for( int iListIndex = 0; iListIndex < fieldNames.Length; iListIndex++ )
            {
                String commaField = fieldNames[iListIndex];

                FieldInfo fi = typeof(Configuration).GetField(commaField);
                List<List<String>> items = new List<List<string>>();

                // Collect all values from semicolon separated string.
                foreach (var cfg in proj.projectConfig)
                {
                    String value = (String)fi.GetValue(cfg);
                    // Sometimes defines can contain linefeeds but from configuration perspective they are useless.
                    value = value.Replace("\n", "");
                    items.Add(value.Split( new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList());
                }

                while (true)
                {
                    // Until we still have values.
                    String oneValue = null;
                    for (int i = 0; i < items.Count; i++)
                        if (items[i].Count != 0)
                        {
                            oneValue = items[i][0];
                            break;
                        }

                    if (oneValue == null)
                        break;

                    for (int i = 0; i < items.Count; i++)
                        if (items[i].Remove(oneValue))
                            fi.SetValue(proj.projectConfig[i], oneValue);
                        else
                            fi.SetValue(proj.projectConfig[i], "");

                    ConfigationSpecificValue(proj, proj.projectConfig, commaField, lines2dump, (s) => { return funcNames[iListIndex] + s; });
                } //for
            } //for

            foreach (String funcName in funcNames)
            {
                foreach (var kv in lines2dump)
                {
                    String line = funcName + arO;
                    bool bAddLine = false;
                    bool first = true;
                    for (int i = 0; i < kv.Value.Count; i++)
                    {
                        String s = kv.Value[i];
                        if (s.StartsWith(funcName))
                        {
                            kv.Value.RemoveAt(i);
                            i--;

                            String oneEntryValue = s.Substring(funcName.Length);

                            if (oneEntryValue == "" || 
                                // Special kind of define which simply tells to inherit from project settings.
                                (funcName == "defines" && oneEntryValue == "%(PreprocessorDefinitions)") )
                                continue;

                            oneEntryValue = oneEntryValue.Replace("\"", "\\\"");    // Escape " mark.
                            if (!first) line += ", ";
                            first = false;
                            line += "\"" + oneEntryValue + "\"";
                            bAddLine = true;
                        }
                    }
                    line += arC;
                    if(bAddLine)
                        kv.Value.Add(line);
                } //foreach
            } //foreach

            //ConfigationSpecificValue(proj, proj.projectConfig, "GenerateDebugInformation", lines2dump, (s) => {
            //    String r = typeof(EGenerateDebugInformation).GetMember(s)[0].GetCustomAttribute<PremakeTagAttribute>().tag;
            //    return "symbols" + brO + "\"" + r + "\"" + brC;
            //});


            if (lines2dump.ContainsKey(""))
            {
                foreach ( String line in lines2dump[""])
                    o.AppendLine(head + "    " + line);
                lines2dump.Remove("");
            }

            foreach (var kv in lines2dump)
            {
                char c = kv.Key.Substring(0, 1)[0];
                String s = kv.Key.Substring(1);
                switch (c)
                {
                    case 'c': o.AppendLine( head + "    filter " + arO + "\"" + s + "\"" + arC); break;
                    case 'p': o.AppendLine( head + "    filter " + arO + "\"platforms:" + s + "\"" + arC); break;
                    case 'm': o.AppendLine( head + "    filter " + arO + "\"" + s.Replace("|", "\", \"platforms:") + "\"" + arC); break;
                }

                foreach (String line in kv.Value)
                {
                    if (line.Length == 0) continue;
                    o.AppendLine(head + "        " + line);
                }

                o.AppendLine("");
            } //foreach

            if (lines2dump.Count != 0)      // Close any filter if we have ones.
            {
                o.AppendLine(head + "    filter " + arO + arC);
                o.AppendLine("");
            }

            if (proj.files.Count != 0)
            {
                o.AppendLine(head + "    files" + arO );
                foreach (FileInfo fi in proj.files)
                {
                    o.AppendLine(head + "        \"" + fi.relativePath.Replace("\\", "/") + "\",");
                }
                o.AppendLine(head + "    " + arC );
            } //if

        } //if-else

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


partial class Script
{

    static int Main(String[] args)
    {
        try
        {
            String slnFile = null;
            List<String> formats = new List<string>();
            String outFile = null;
            String outPrefix = "";
            bool bProcessProjects = true;

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
                    case "p": i++; outPrefix = args[i]; break;
                    case "sln": bProcessProjects = false; break;
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
                Console.WriteLine(" -p      - sets prefix for all output files");
                Console.WriteLine(" -sln    - does not processed projects");
                Console.WriteLine("");
                return -2;
            }

            SolutionOrProject proj = new SolutionOrProject(slnFile);
            String projCacheFile = slnFile + ".cache";
            SolutionOrProject projCache;

            if (File.Exists(projCacheFile))
                projCache = SolutionOrProject.LoadCache(projCacheFile);

            Solution s = proj.solutionOrProject as Solution;
            if (s != null && bProcessProjects)
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
                SolutionOrProject.UpdateProjectScript(proj.path, proj.solutionOrProject, outFile, format, bProcessProjects, outPrefix);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
            return -2;
        }

        return 0;
    } //Main
}

