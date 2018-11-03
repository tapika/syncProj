using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.Serialization;

/// <summary>
/// Custom class for mapping enumeration values to premake configuration tag.
/// </summary>
public class PremakeTagAttribute : Attribute
{
    public String tag;

    public PremakeTagAttribute(String s)
    {
        tag = s;
    }
}


public enum EPrecompiledHeaderUse
{
    Create = 2,
    Use = 1,
    NotUsing = 0 //enum default is 0.
}

public enum EWarningLevel
{
    TurnOffAllWarnings,
    Level1,
    Level2,
    Level3,
    Level4,
    EnableAllWarnings
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


/// <summary>
/// Configuration class which configures project as well as individual file entries.
/// </summary>
public class FileConfigurationInfo
{
    //--------------------------------------------------------------------------------------------
    // Following fields are located under following XML nodes
    // ItemDefinitionGroup\
    //                     ClCompile
    //                     Link
    // ItemGroup\
    //                     ClCompile
    //--------------------------------------------------------------------------------------------
    public EPrecompiledHeaderUse PrecompiledHeader = EPrecompiledHeaderUse.NotUsing;

    /// <summary>
    /// Defines, ';' separated list.
    /// </summary>
    public String PreprocessorDefinitions = "";

    /// <summary>
    /// Additional Include Directories, ';' separated list.
    /// </summary>
    public String AdditionalIncludeDirectories = "";
    public String ShowIncludes;

    /// <summary>
    /// obj / lib files, ';' separated list.
    /// </summary>
    public String AdditionalDependencies = "";

    /// <summary>
    /// Output filename
    /// </summary>
    public String ObjectFileName;
    public String XMLDocumentationFileName;

    public String PrecompiledHeaderFile = "stdafx.h";
}




[DebuggerDisplay("{relativePath} ({includeType})")]
public class FileInfo
{
    public IncludeType includeType;

    public String relativePath;

    public List<FileConfigurationInfo> fileConfig = new List<FileConfigurationInfo>();

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

public enum EConfigurationType
{
    /// <summary>
    /// .exe
    /// </summary>
    [PremakeTag("WindowedApp")]
    Application = 0,
    
    /// <summary>
    /// .dll
    /// </summary>
    [PremakeTag("SharedLib")]
    DynamicLibrary
};

public enum ECharacterSet
{ 
    /// <summary>
    /// Unicode
    /// </summary>
    [PremakeTag("Unicode")]
    Unicode = 0,
    
    /// <summary>
    /// Ansi
    /// </summary>
    [PremakeTag("MBCS")]
    MultiByte
}


[Description("")]   // Marker to switch Enum value / Description when parsing
public enum EWholeProgramOptimization
{
    /// <summary>
    /// Visual studio default.
    /// </summary>
    [Description("false")]
    NoWholeProgramOptimization = 0,

    [Description("true")]
    UseLinkTimeCodeGeneration,

    [Description("PGInstrument")]
    ProfileGuidedOptimization_Instrument,

    [Description("PGOptimize")]
    ProfileGuidedOptimization_Optimize,

    [Description("PGUpdate")]
    ProfileGuidedOptimization_Update
}


public enum ESubSystem
{
    NotSet,
    Windows,
    Console,
    Native,
    EFI_Application,
    EFI_Boot_Service_Driver,
    EFI_ROM,
    EFI_Runtime,
    POSIX
}

public enum EOptimization
{
    Custom,
    Disabled,
    
    /// <summary>
    /// Minimize Size
    /// </summary>
    MinSpace,

    /// <summary>
    /// Maximize Speed
    /// </summary>
    MaxSpeed,

    /// <summary>
    /// Full Optimization
    /// </summary>
    Full
}

[Description("")]   // Marker to switch Enum value / Description when parsing
public enum EGenerateDebugInformation
{
    [Description("false"), PremakeTag("off")]
    No = 0,

    [Description("true"), PremakeTag("on")]
    OptimizeForDebugging,

    [Description("DebugFastLink"), PremakeTag("fastlink")]
    OptimizeForFasterLinking
}



public class Configuration: FileConfigurationInfo
{
    public EConfigurationType ConfigurationType = EConfigurationType.Application;

    public void ConfigurationTypeUpdated()
    {
        switch (ConfigurationType)
        {
            case EConfigurationType.Application: TargetExt = ".exe"; break;
            case EConfigurationType.DynamicLibrary: TargetExt = ".dll"; break;
        }
    } //ConfigurationTypeUpdated

    public bool UseDebugLibraries = false;

    /// <summary>
    /// For example:
    ///     'v140' - for Visual Studio 2015.
    ///     'v120' - for Visual Studio 2013.
    /// </summary>
    public String PlatformToolset = "v140";
    public ECharacterSet CharacterSet = ECharacterSet.Unicode;

    public bool LinkIncremental = true;
    public EWholeProgramOptimization WholeProgramOptimization;

    /// <summary>
    /// Output Directory. 
    ///     Visual studio default:  $(SolutionDir)$(Configuration)\
    ///     premake default:        bin\$(Platform)\$(Configuration)\
    /// </summary>
    public String OutDir = "$(SolutionDir)$(Configuration)\\";

    /// <summary>
    /// Intermediate Directory.
    ///     Visual studio default:  $(Configuration)\
    ///     premake default:        obj\$(Platform)\$(Configuration)\
    /// </summary>
    public String IntDir = "$(Configuration)\\";

    /// <summary>
    /// Target Name.
    /// Visual studio default: $(ProjectName)
    /// </summary>
    public String TargetName = "$(ProjectName)";

    /// <summary>
    /// Target Extension (.exe, .dll, ...)
    /// </summary>
    public String TargetExt;

    public EWarningLevel WarningLevel = EWarningLevel.Level1;

    /// <summary>
    /// Typically Windows or Console.
    /// </summary>
    public ESubSystem SubSystem;
    public EOptimization Optimization;

    /// <summary>
    /// Visual studio defaults: OptimizeForDebugging for release, OptimizeForFasterLinking for debug.
    /// </summary>
    public EGenerateDebugInformation GenerateDebugInformation;
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
    /// Gets list of supported configurations like 'Debug' / 'Release'
    /// </summary>
    public List<String> getConfigurationNames()
    {
        return configurations.Select(x => "\"" + x.Split('|')[0] + "\"").Distinct().ToList();
    }

    /// <summary>
    /// Gets list of supported platforms like 'Win32' / 'x64'
    /// </summary>
    public List<String> getPlatforms()
    {
        return configurations.Select(x => "\"" + x.Split('|')[1] + "\"").Distinct().ToList();
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
    static String getConfiguration(XElement node)
    {
        String config = RegexExract("^'\\$\\(Configuration\\)\\|\\$\\(Platform\\)'=='(.*)'", node.Attribute("Condition").Value)[0];
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
    void ExtractCompileOptions(XElement clCompile, FileInfo file2compile)
    {
        foreach (XElement fileProps in clCompile.Elements())
        {
            String config = getConfiguration(fileProps);

            int iCfg = configurations.IndexOf(config);
            if (iCfg == -1)
                continue;           // Invalid configuration string specified.


            String localName = fileProps.Name.LocalName;
            FieldInfo fi = typeof(FileConfigurationInfo).GetField(fileProps.Name.LocalName);
            if (fi == null)
            {
                if (Debugger.IsAttached) Debugger.Break();
                continue;
            }

            //
            // PrecompiledHeader, PreprocessorDefinitions, AdditionalIncludeDirectories, ObjectFileName, XMLDocumentationFileName
            //
            while (file2compile.fileConfig.Count < configurations.Count)
            {
                int i = file2compile.fileConfig.Count;
                // Add new configurations, use same precompiled header setting as project uses for given configuration.
                file2compile.fileConfig.Add(new FileConfigurationInfo() { PrecompiledHeader = projectConfig[i].PrecompiledHeader });
            }

            FileConfigurationInfo fci = file2compile.fileConfig[iCfg];
            object oValue;

            if (fi.FieldType.IsEnum)
                oValue = Enum.Parse(fi.FieldType, fileProps.Value);
            else
                oValue = Convert.ChangeType(fileProps.Value, fi.FieldType);

            fi.SetValue(fci, oValue);
        } //foreach
    } //ExtractCompileOptions

    static void CopyField(object o2set, String field, XElement node)
    {
        o2set.GetType().GetField(field).SetValue(o2set, node.Element(node.Document.Root.Name.Namespace + field)?.Value);
    }

    void extractGeneralCompileOptions(XElement node)
    {
        String config = getConfiguration(node);

        int iCfg = configurations.IndexOf(config);

        if (iCfg == -1)
            return;

        Configuration cfg = projectConfig[iCfg];

        List<XElement> nodes = node.Elements().ToList();

        //  Explode sub nodes if we have anything extra to scan. (Comes from ItemDefinitionGroup)
        for (int i = 0; i < nodes.Count; i++)
        {
            String nodeName = nodes[i].Name.LocalName;
            if (nodeName == "ClCompile" || nodeName == "Link")      // These nodes are located in ItemDefinitionGroup, we simply expand sub children.
            {
                nodes.AddRange(nodes[i].Elements());
                nodes.RemoveAt(i);
                i--;
            }
        } //for

        foreach (XElement cfgNode in nodes)
        {
            String fieldName = cfgNode.Name.LocalName;
            FieldInfo fi = typeof(Configuration).GetField(fieldName);
            if (fi == null)
                continue;

            if (fi.FieldType.IsEnum)
            {
                if (fi.FieldType.GetCustomAttribute<DescriptionAttribute>() == null )
                {
                    fi.SetValue(cfg, Enum.Parse(fi.FieldType, cfgNode.Value));
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
    } //extractGeneralCompileOptions



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

                            if (f.includeType == IncludeType.ClCompile)
                                project.ExtractCompileOptions(igNode, f);

                            //
                            // Custom build tool
                            //
                            if (f.includeType == IncludeType.CustomBuild)
                            {
                                f.customBuildTool = new List<CustomBuildToolProperties>(project.configurations.Count);

                                while (f.customBuildTool.Count < project.configurations.Count)
                                    f.customBuildTool.Add(new CustomBuildToolProperties());

                                foreach (XElement custbNode in igNode.Elements())
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
                        String label = node.Attribute("Label")?.Value;

                        switch (label)
                        {
                            case "Globals":
                                foreach (String field in new String[] { "ProjectGuid", "Keyword" /*, "RootNamespace"*/ })
                                    CopyField(project, field, node);
                                break;
                            case null:                  // Non tagged node contains rest of configurations like 'LinkIncremental', 'OutDir', 'IntDir', 'TargetName', 'TargetExt'
                            case "Configuration":
                                project.extractGeneralCompileOptions(node);
                                break;
                            case "UserMacros":
                                // What is it - does needs to be supported ?
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
                    project.extractGeneralCompileOptions(node);
                    break;

                default:
                    if (Debugger.IsAttached) Debugger.Break();
                    break;
            } //switch
        } //foreach

        return project;
    } //LoadProject
} //Project

