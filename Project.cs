using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.Serialization;

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
        list = new List<T>(configurations.Count);

        while (list.Count < configurations.Count)
            list.Add(defT);
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

            if (bSet)
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
} //Project

