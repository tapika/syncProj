//#define NODEBUGTRACE
using System;
using System.Linq;
using System.Text;
using Microsoft.CSharp;
using System.CodeDom.Compiler;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Collections.Generic;


/// <summary>
/// class for executing c# script.
/// </summary>
public class CsScript
{
    static CSharpCodeProvider provider;
    static ICodeCompiler compiler;
    static String[] refAssemblies;

    /// <summary>
    /// Compiles .cs script into dll/pdb, loads as assembly, and executes Main function.
    /// Temporary dll/pdb gets deleted. If .cs throws exception - it will be converted to
    /// error information, including .cs filename and source code line information.
    /// </summary>
    /// <param name="_path">Path to script which to execute</param>
    /// <param name="bAllowThrow">true if allow to throw exceptions</param>
    /// <param name="errors">Errors if any</param>
    /// <param name="args">Main argument parameters</param>
    /// <returns>true if execution was successful.</returns>
    static public bool RunScript( String _path, bool bAllowThrow, out String errors, params String[] args )
    {
        errors = "";

        // ----------------------------------------------------------------
        //  Load script
        // ----------------------------------------------------------------
        String path = Path.GetFullPath( _path );
        if( !File.Exists( path ) )
        {
            errors = "Error: Could not load file '" + Exception2.getPath(path) + "': File does not exists.";
            if (bAllowThrow)
                throw new Exception2(errors, 1);
            return false;
        }

        // Create syncProj cache folder next to syncProj.exe executable.
        String cacheDir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "syncProjCache");
        if (!Directory.Exists(cacheDir))
        {
            DirectoryInfo di = Directory.CreateDirectory(cacheDir);
            di.Attributes = FileAttributes.Directory | FileAttributes.Hidden;
        }

        String dllBaseName = Path.GetFileNameWithoutExtension(_path);
        String tempDll;

        for (int i = 1; ; i++)
        {
            tempDll = Path.Combine(cacheDir, dllBaseName);
            if (i != 1)
                tempDll += i;

            String dllInfoFile = tempDll + "_script.txt";       // We keep here C# script full path just not to get collisions.
            if (!File.Exists(dllInfoFile))
            {
                File.WriteAllText(dllInfoFile, path);
                break;
            }

            if ( File.ReadAllText(dllInfoFile) == path)
                break;
        }

        String pdb = tempDll + ".pdb";
        tempDll += ".dll";

        List<String> filesToCompile = new List<string>();
        filesToCompile.Add(path);

        CsScriptInfo csInfo = getCsFileInfo(path, true);
        filesToCompile.AddRange(csInfo.csFiles);


        bool bCompileDll = false;

        //---------------------------------------------------------------------------------------------------
        // Compile .dll only if script.cs and it's dependent .cs are newer than compiled .dll.
        //---------------------------------------------------------------------------------------------------
        if (!File.Exists(tempDll))
            bCompileDll = true;

        if (!bCompileDll)
        {
            DateTime tempDllDate = File.GetLastWriteTime(tempDll);

            foreach (String file in filesToCompile)
            {
                if (File.GetLastWriteTime(file) > tempDllDate)
                {
                    bCompileDll = true;

                    if (csInfo.bCsDebug)
                        Console.WriteLine("Compiling '" + tempDll + " because following file changed: " + file);

                    break;
                }
            }
        }
        if (csInfo.bCsDebug && !bCompileDll)
            Console.WriteLine(tempDll + " is up-to-date.");


        if (bCompileDll)
        {
            // ----------------------------------------------------------------
            //  Compile it into ram
            // ----------------------------------------------------------------
            if (provider == null)
                provider = new CSharpCodeProvider();
#pragma warning disable 618
            if (compiler == null)
                compiler = provider.CreateCompiler();
#pragma warning restore 618
            CompilerParameters compilerparams = new CompilerParameters();
            compilerparams.GenerateExecutable = false;
#if NODEBUGTRACE
            // Currently it's not possible to generate in ram pdb debugging information.
            // Compiler option /debug:full should in theory allow that, but it does not work.
            compilerparams.GenerateInMemory = true;
#else
            compilerparams.GenerateInMemory = false;
            compilerparams.IncludeDebugInformation = true;          // Needed to get line / column numbers
            compilerparams.OutputAssembly = tempDll;
            compilerparams.CompilerOptions = "/d:DEBUG /d:TRACE";   // /debug+ /debug:full /optimize-
#endif

            // Add assemblies from my domain - all which are not dynamic.
            if (refAssemblies == null)
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic).Select(a => a.Location).ToList();

                for (int i = 0; i < assemblies.Count; i++)
                {
                    if (assemblies[i].EndsWith(".exe") && !assemblies[i].EndsWith("\\syncproj.exe"))
                    {
                        assemblies.RemoveAt(i);
                        i--;
                    }
                }

                refAssemblies = assemblies.ToArray();
            }
            compilerparams.ReferencedAssemblies.AddRange(refAssemblies);

            // ----------------------------------------------------------------
            //  If compile errors - report and exit.
            // ----------------------------------------------------------------
            CompilerResults results = compiler.CompileAssemblyFromFileBatch(compilerparams, filesToCompile.ToArray());
            if (results.Errors.HasErrors)
            {
                // Mimic visual studio error handling.
                StringBuilder sb = new StringBuilder();
                foreach (CompilerError error in results.Errors)
                {
                    sb.AppendFormat("{0}({1},{2}): error {3}: {4}\r\n",
                        Exception2.getPath(error.FileName), error.Line, error.Column, error.ErrorNumber, error.ErrorText
                    );
                }
                errors = sb.ToString();
                if (bAllowThrow)
                    throw new Exception2(errors);

                return false;
            }
        } //if

        //------------------------------------------------------------------------------------------------------
        //
        // Let's check that script contains correct css_ref (Might be copied from another project).
        // We allow here also multiple copies of syncProj, as long as path to syncProj.exe is valid in .cs header
        // (Can be edited by C# script)
        //
        //------------------------------------------------------------------------------------------------------
        Regex reCssRef = new Regex("^ *//css_ref  *(.*);?([\r\n]+|$)", RegexOptions.Multiline);
        bool bUpdateScriptPath = false;
        String targetCsPath = "";

        using (StreamReader reader = new StreamReader(path))
        {
            for (int i = 0; i < 10; i++)
            { 
                String line = reader.ReadLine() ?? "";
                var re = reCssRef.Match(line);
                if (re.Success)
                {
                    // Current path, referred from C# script
                    String currentCsPath = re.Groups[1].Value;
                    String dir = Path.GetDirectoryName(path);
                    String exePath = SolutionOrProject.getSyncProjExeLocation(dir, currentCsPath);
                    targetCsPath = Path2.makeRelative(exePath, dir);
                    String referredExe = currentCsPath;
                    
                    if( !Path.IsPathRooted(referredExe) )       // Uses relative path, let's make it absolute.
                        referredExe = Path.Combine(dir, currentCsPath);

                    if (currentCsPath != targetCsPath && !File.Exists(referredExe))
                        bUpdateScriptPath = true;               // Path is not the same as ours, and .exe referred by C# script does not exists.
                } //if
            } //for
        } //using

        if (bUpdateScriptPath)
        {
            String file = File.ReadAllText(path);
            String newFile = reCssRef.Replace(file, new MatchEvaluator( m => { return "//css_ref " + targetCsPath + "\r\n"; } ) );
            File.WriteAllText(path, newFile);
        }

        // ----------------------------------------------------------------
        //  Preload compiled .dll and it's debug information into ram.
        // ----------------------------------------------------------------
        MethodInfo entry = null;
        String funcName = "";
        Assembly asm = Assembly.LoadFrom(tempDll);
            
        //Assembly asm = results.CompiledAssembly;
        // ----------------------------------------------------------------
        //  Locate entry point
        // ----------------------------------------------------------------
        BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.IgnoreCase;
        Type builderClass = null;

        foreach (Type type in asm.GetTypes())
        {
            funcName = "Main";
            entry = type.GetMethod(funcName, flags);

            if (entry == null)
            {
                funcName = "ScriptMain";
                entry = type.GetMethod(funcName, flags);
            }

            if (entry != null)
            {
                builderClass = type;
                break;
            }
        }

        if( entry == null )
        {
            errors = String.Format( "{0}(1,1): error: Code does not have 'Main' function\r\n", Exception2.getPath(path) );
            if (bAllowThrow)
                throw new Exception2(errors);
            return false;
        }

        if ( entry.GetParameters().Length != 1 )
        {
            errors = String.Format("{0}(1,1): error: Function '{1}' is not expected to have {2} parameter(s)\r\n", Exception2.getPath(path), funcName,entry.GetParameters().Length);
            if (bAllowThrow)
                throw new Exception2(errors);
            return false;
            
        }

        String oldScriptRelativeDir = SolutionProjectBuilder.m_scriptRelativeDir;
        String scriptSubPath = Path2.makeRelative(Path.GetDirectoryName(_path), SolutionProjectBuilder.m_workPath);
        SolutionProjectBuilder.m_scriptRelativeDir = scriptSubPath;
        String oldScriptPath = SolutionProjectBuilder.m_currentlyExecutingScriptPath;
        SolutionProjectBuilder.m_currentlyExecutingScriptPath = _path;

        // ----------------------------------------------------------------
        //  Run script
        // ----------------------------------------------------------------
        try
        {
            entry.Invoke(null, new object[] { args });
            SolutionProjectBuilder.m_scriptRelativeDir = oldScriptRelativeDir;
            SolutionProjectBuilder.m_currentlyExecutingScriptPath = oldScriptPath;
        }
        catch ( Exception ex )
        {
            SolutionProjectBuilder.m_scriptRelativeDir = oldScriptRelativeDir;
            SolutionProjectBuilder.m_currentlyExecutingScriptPath = oldScriptPath;
            Exception2 ex2 = ex.InnerException as Exception2;
            if (ex2 != null && bAllowThrow)
                throw ex2;

            try
                {
                StackFrame[] stack = new StackTrace(ex.InnerException, true).GetFrames();
                StackFrame lastCall = stack[0];

                errors = String.Format("{0}({1},{2}): error: {3}\r\n", path,
                    lastCall.GetFileLineNumber(), lastCall.GetFileColumnNumber(), ex.InnerException.Message);
                
            } catch (Exception ex3 )
            {
                errors = String.Format("{0}(1,1): error: Internal error - exception '{3}'\r\n", path, ex3.Message);
            }
            if (bAllowThrow)
                throw new Exception2(errors);
            return false;
        }

        return true;
    } //RunScript

    static String GetUniqueTempFilename( String path )
    {
        String baseName = Path.GetFileNameWithoutExtension(path);
        string ProcID = Process.GetCurrentProcess().Id.ToString();
        string tmpFolder = Path.GetTempPath();
        string outFile = tmpFolder + baseName + "_" + ProcID;
        return outFile;
    }

    /// <summary>
    /// Scans through C# script and gets additional information about C# script itself, 
    /// like dependent .cs files, and so on.
    /// </summary>
    /// <param name="csPath">C# script to load and scan</param>
    /// <param name="bUseAbsolutePaths">true if to use absolute paths, false if not</param>
    /// <returns>C# script info</returns>
    static public CsScriptInfo getCsFileInfo( String csPath, bool bUseAbsolutePaths )
    {
        CsScriptInfo csInfo = new CsScriptInfo();

        // ----------------------------------------------------------------
        //  Using C# kind of syntax - like this:
        //      //css_include <file.cs>;
        // ----------------------------------------------------------------
        Regex reIsCommentUsingEmptyLine = new Regex("^ *(//|using|$)", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        Regex reCssImport = new Regex("^ *//css_include +(.*?);?$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        Regex reDebug = new Regex("^ *//css_debug", RegexOptions.Multiline | RegexOptions.IgnoreCase);

        int iLine = 1;

        csInfo.bCsDebug = false;

        using (StreamReader reader = new StreamReader(csPath))
        {
            for (; ; iLine++)
            {
                String line = reader.ReadLine();
                if (line == null)
                    break;

                // If we have any comments, or using namespace or empty line, we continue scanning, otherwise aborting (class, etc...)
                if (!reIsCommentUsingEmptyLine.Match(line).Success)
                    break;

                var rem = reCssImport.Match(line);
                if (rem.Success)
                {
                    String file = rem.Groups[1].Value;
                    String fileFullPath = file;

                    if (!Path.IsPathRooted(file))
                        fileFullPath = Path.Combine(Path.GetDirectoryName(csPath), file);

                    if (!File.Exists(fileFullPath))
                        throw new FileSpecificException("Include file specified in '" + fileFullPath + "' was not found (Included from '" + csPath + "')", csPath, iLine);

                    if( bUseAbsolutePaths )
                        csInfo.csFiles.Add(fileFullPath);
                    else
                        csInfo.csFiles.Add(file);
                } //if

                if (reDebug.Match(line).Success)
                    csInfo.bCsDebug = true;
            } //for
        } //using

        return csInfo;
    } //getCsFileInfo
} //class CsScript


/// <summary>
/// Additional info about c# script.
/// </summary>
public class CsScriptInfo
{
    /// <summary>
    /// Referred .cs files to include into compilation
    /// </summary>
    public List<String> csFiles = new List<string>();

    /// <summary>
    /// Just additional //css_debug for compile troubleshooting in this code
    /// </summary>
    public bool bCsDebug;
}


