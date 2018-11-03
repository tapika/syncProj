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
    /// <param name="bCompileNextToCs">
    ///     When this parameter is set to true, produced assembly will be kept next to C# script path.
    ///     This also means that StackTrace will be able to display correct source code path and code line, unlike when loaded into ram
    ///     false - if you don't care about source code position</param>
    /// <param name="bAllowThrow">true if allow to throw exceptions</param>
    /// <param name="errors">Errors if any</param>
    /// <param name="args">Main argument parameters</param>
    /// <returns>true if execution was successful.</returns>
    static public bool RunScript( String _path, bool bCompileNextToCs, bool bAllowThrow, out String errors, params String[] args )
    {
        errors = "";

        // ----------------------------------------------------------------
        //  Load script
        // ----------------------------------------------------------------
        String path = Path.GetFullPath( _path );
        if( !File.Exists( path ) )
        {
            errors = "Error: Could not load file '" + path + "': File does not exists.";
            if (bAllowThrow)
                throw new Exception2(errors);
            return false;
        }

        String tempDll;

        if (bCompileNextToCs)
        {
            tempDll = Path.Combine(Path.GetDirectoryName(_path), Path.GetFileNameWithoutExtension(_path));
        }
        else {
            tempDll = GetUniqueTempFilename(path);
        }
        
        String pdb = tempDll + ".pdb";
        tempDll += ".dll";

        bool bCompileDll = true;

        // Compile .dll only if .cs is newer than .dll.
        if (bCompileDll && File.Exists(tempDll))
            bCompileDll = File.GetLastWriteTime(path) > File.GetLastWriteTime(tempDll);

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
            //  Scan through C# header at begging of file, and include more
            //  sources if needed.
            //
            //  Using C# kind of syntax - like this:
            //      //css_import <file.cs>;
            // ----------------------------------------------------------------
            Regex reIsCommentUsingEmptyLine = new Regex("^ *(//|using|$)", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            Regex reCssImport = new Regex("^ *//css_import +(.*?);?$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            List<String> filesToCompile = new List<string>();

            filesToCompile.Add(path);
            int iLine = 1;

            using (StreamReader reader = new StreamReader(path))
            {
                for ( ; ; iLine++ )
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

                        if (!Path.IsPathRooted(file))
                            file = Path.Combine(Path.GetDirectoryName(path), file);

                        if (!File.Exists(file))
                            throw new FileSpecificException("Include file '" + file + "' was not found", path, iLine);
                            
                        filesToCompile.Add(file);
                    } //if
                } //for
            } //using

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
                        path, error.Line, error.Column, error.ErrorNumber, error.ErrorText
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
        Assembly asm;

        if (!bCompileNextToCs)
        {
            byte[] asmRaw = File.ReadAllBytes(tempDll);
            byte[] pdbRaw = null;

            if (File.Exists(pdb))
            {
                try
                {
                    pdbRaw = File.ReadAllBytes(pdb);
                }
                catch (Exception)
                {
                }
            }

            asm = Assembly.Load(asmRaw, pdbRaw);
            try
            {
                File.Delete(tempDll);
                File.Delete(pdb);
            }
            catch (Exception)
            {
                // Could not delete intermediate files.. Oh well...
            }
        }
        else
        {
            asm = Assembly.LoadFrom(tempDll);
        }
            
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
            errors = String.Format( "{0}(1,1): error: Code does not have 'Main' function\r\n", path );
            if (bAllowThrow)
                throw new Exception2(errors);
            return false;
        }

        if ( entry.GetParameters().Length != 1 )
        {
            errors = String.Format("{0}(1,1): error: Function '{1}' is not expected to have {2} parameter(s)\r\n", path, funcName,entry.GetParameters().Length);
            if (bAllowThrow)
                throw new Exception2(errors);
            return false;
            
        }

        FieldInfo fi = builderClass.GetField("m_scriptRelativeDir", BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy);
        if (fi != null)
        {
            String scriptSubPath = Path2.makeRelative(Path.GetDirectoryName(_path), SolutionProjectBuilder.m_workPath);
            fi.SetValue(null, scriptSubPath);
        }

        // ----------------------------------------------------------------
        //  Run script
        // ----------------------------------------------------------------
        try
        {
            entry.Invoke(null, new object[] { args });
        } catch ( Exception ex )
        {
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
        string tmpFolder = System.IO.Path.GetTempPath();
        string outFile = tmpFolder + baseName + "_" + ProcID;
        return outFile;
    }

}
