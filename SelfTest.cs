using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Just a helper class so Visual studio would be able to see our test system and
/// launch code coverage.
/// </summary>
[TestClass]
public class TestStarter
{
    /// <summary>
    /// Runs all tests
    /// </summary>
    [TestMethod]
    public void RunAllTests()
    {
        syncProj.Main("-t", "-testexplorer");
    }

    /// <summary>
    /// Modify however you want - to add "shortcut" to any test which is failing, by default RunAllTests
    /// will find all tests automatically at run-time.
    /// </summary>
    [TestMethod]
    public void RunTest1()
    {
        syncProj.Main("-t", "-testexplorer", "TestAllKinds");
    }

}

partial class syncProj
{
    static bool bAllowExceptionThrow = false;
    /// <summary>
    /// Starts self diagnostic tests.
    /// 
    /// Basic concept of syncProj self testing is that syncProj finds all *.sln and *.cs and tries to "execute" them and record console output.
    /// 
    /// Recorded output then is compared against *.accepted_log.txt (console output) or *.accepted (actual xml output)-files - whether they are the same.
    /// If not same - developer can accept changes made or continue fixing the code.
    /// 
    /// Developer can use:
    ///     syncProj.exe -t                 - to start testing
    ///     syncProj.exe -t someTest        - to start individual test (without .cs extension)
    ///     syncProj.exe -t someTest -keep  - to keep generated files
    /// 
    /// </summary>
    /// <param name="args">command line arguments to syncProj tool</param>
    /// <param name="i">args should be parsed from i'th position</param>
    [ExcludeFromCodeCoverage]
    public static void StartSelfTests( String[] args, int i )
    {
        String testToStart = null;
        bool bKeepResults = false;

        for (int j = i; j < args.Length; j++)
        {
            String arg = args[j];

            if (!(arg.StartsWith("-") || arg.StartsWith("/")))
            {
                testToStart = arg;
                continue;
            }
            
            switch (arg.Substring(1).ToLower())
            {
                case "keep": bKeepResults = true; break;
                case "testexplorer":
                    bAllowExceptionThrow = true;
                    break;
            }
        }

        String exePath = Assembly.GetExecutingAssembly().Location;
        String testsDir = Path.Combine(Path.GetDirectoryName(exePath), "Tests");

        if (!Directory.Exists(testsDir))
            throw new Exception("Tests directory does not exists: '" + testsDir + "'");

        //
        // We use TortoiseMerge.exe if we have one (can copy paste changes in merge tool itself)
        //
        String diffExe = @"C:\Program Files\TortoiseSVN\bin\TortoiseMerge.exe";
        bool bTortoiseDiff = true;

        if (!File.Exists(diffExe))
        {
            diffExe = Path.Combine(testsDir, "ExamDiff.exe");
            bTortoiseDiff = false;
        }

        if( !File.Exists(diffExe) )
            throw new Exception("Diff tool does not exists '" + diffExe + "'");

        //
        //  Clean up test results.
        //
        String[] resultFiles = Directory.GetFiles(testsDir, "*", SearchOption.AllDirectories).Where(x => Path.GetFileName(x).StartsWith("out_") && !x.EndsWith(".accepted")).ToArray();
        foreach (String file in resultFiles)
            File.Delete(file);

        //  Rename folder to start from '_' to exclude it from test scope.
        String[] dirs = Directory.GetDirectories(testsDir).Where( x => !Path.GetFileName(x).StartsWith("_")).ToArray();

        Console.Write("Self testing ");
        int nTestsPassed = 0;

        String testCategoryToStart = null;

        String[] localDirs = dirs.Select(x => Path2.makeRelative(x, testsDir)).ToArray();

        int index = Array.IndexOf(localDirs, testToStart);
        if (index != -1)
            testCategoryToStart = dirs[index];

        if (testCategoryToStart == null )
            testCategoryToStart = dirs.Where(x => Path.GetFileName(x) == testToStart).FirstOrDefault();

        if (testCategoryToStart != null )
        {
            dirs = new String[] { testCategoryToStart };
            testToStart = null;
        }

        //
        // Directory name will just highlight testing scope.
        //
        foreach (String dir in dirs)
        {
            //
            // All test cases are either .cs or .sln projects.
            //
            String[] tests = Directory.GetFiles(dir, "*", SearchOption.AllDirectories).
                Where( x => (x.EndsWith(".cs") || x.EndsWith(".sln") || x.EndsWith(".bat")) && !Path.GetFileName(x).StartsWith("_")).ToArray();
            String logActual = "";

            foreach (String test in tests)
            {
                int error = 0;

                // Start only particular test
                String testFilename = Path.GetFileNameWithoutExtension(test);
                if (testToStart != null && !testFilename.CompareTo(testToStart, true) )
                    continue;

                UpdateInfo.lastUpdateInfo = null;
                UpdateInfo.bTesting = true;

                //
                // We launch test via direct Invoke currently - it's just simpler to debug if there is a problem,
                // but this requires initializing static global variables back to default state.
                //
                // I've left here ExecCmd - but main problem is that even MessageBox shown from console application
                // is closed without any clear reason.
                //
                bool bIsBat = Path.GetExtension(test).ToLower() == ".bat";
                String toolName = "syncProj";
                if (bIsBat)
                {
                    // Requires Android stuff preinstalled, we want to focus on syncProj testing.
                    String[] excludeBats = new string[] { "gradlew", "getsigninfo" };
                    if (Array.IndexOf(excludeBats,Path.GetFileNameWithoutExtension(test).ToLower()) != -1)
                        continue;

                    error = ExecCmd(test + " -x", ref logActual);
                    toolName = "Batch";
                }
                else 
                {
                    ConsoleWriter cw = new ConsoleWriter();
                    SolutionProjectBuilder.resetStatics();
                    var co = Console.Out;
                    Console.SetOut(cw);
                    error = syncProj.Main(new String[] { "-x", "-p","out_",test });
                    
                    bool bIsScript = Path.GetExtension(test).ToLower() == ".cs";
                    if (bIsScript)  // By default it's executed by default destructor, here we need to launch it manually.
                        SolutionProjectBuilder.SaveGenerated();
                    
                    cw.Flush();
                    Console.SetOut(co);
                    logActual = cw.sb.ToString();
                }

                String errMsg;

                // Add command line tool error code into log itself.
                if (error == 0)
                    errMsg = toolName + " exited with no error";
                else
                    errMsg = toolName + " exited with ERROR: " + error;

                logActual = errMsg + "\r\n" + logActual;

                Regex reOutFile = new Regex("\\\\" + Regex.Escape("out_" + Path.GetFileNameWithoutExtension(test)) + "(\\.|_cs)");
                List<String> verifyFiles = Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Where(x => reOutFile.Match(x).Success).ToList();

                verifyFiles.Insert(0, test + ".accepted_log.txt");
                Dictionary<String, bool> testedFiles = new Dictionary<string, bool>();

                //
                //  Collect all information about which files syncProj tried to save or check if they are up-to-date.
                //
                if (UpdateInfo.lastUpdateInfo != null)
                {
                    verifyFiles.AddRange(UpdateInfo.lastUpdateInfo.filesUpdated);
                    verifyFiles.AddRange(UpdateInfo.lastUpdateInfo.filesUpToDate);
                }

                //
                // iFile == 0 only for output of command line execution - rest of files are actually compared files.
                //
                for ( int iFile = 0; iFile < verifyFiles.Count; iFile++ )
                {
                    String file = verifyFiles[iFile];

                    // Pick up only two last file extensions. test.sln.accepted_log.txt => "test.sln" + ".accepted_log.txt"
                    var re = Regex.Match(file, @"(.*\\[^\\]+?)(\.[^\\\.]*\.[^\\\.]*)$");
                    String fPathBase;
                    String dblExt;
                    String ext = Path.GetExtension(file).ToLower();

                    if (re.Success)
                    {
                        fPathBase = re.Groups[1].ToString();
                        dblExt = re.Groups[2].ToString().ToLower();
                    }
                    else 
                    {
                        fPathBase = Path.GetDirectoryName(file) + "\\" + Path.GetFileNameWithoutExtension(file);
                        dblExt = Path.GetExtension(file).ToLower();
                    }

                    String logAcceptedFile = "";
                    String logActualFile = "";

                    if (ext == ".accepted")
                    {
                        // file which is expected to be generated
                        logAcceptedFile = file;
                        logActualFile = file.Substring(0, file.Length - ext.Length);
                    }
                    else
                    {
                        if (dblExt == ".accepted_log.txt")
                        {
                            // tool output
                            logAcceptedFile = file;
                            logActualFile = fPathBase + ".actual_log.txt";
                        }
                        else 
                        {
                            // actual file generated
                            logAcceptedFile = file + ".accepted";
                            logActualFile = file;
                        }
                    } //if-else

                    if (testedFiles.ContainsKey(logAcceptedFile))
                        continue;

                    testedFiles[logAcceptedFile] = true;

                    String testNameShort = test.Substring(testsDir.Length + 1);

                    if (iFile != 0)
                        testNameShort += " for file '" + Path.GetFileName(file) + "'";

                    //
                    //  Until we have decision made by developer.
                    //
                    while (true)
                    {
                        String logAccepted = "";
                        String errorMessage;
                        bool bAcceptedFileExists = File.Exists(logAcceptedFile);

                        if (!bAcceptedFileExists)
                        {
                            // First time acceptance
                            errorMessage = "Test results for '" + testNameShort + "' does not yet exists.";
                        }
                        else
                        {
                            // Second time acceptance
                            errorMessage = "Test on '" + testNameShort + "' failed, results differ.";
                            logAccepted = File.ReadAllText(logAcceptedFile);
                        }

                        if( iFile != 0 )
                            logActual = File.ReadAllText(logActualFile);

                        if (logActual == logAccepted)
                        {
                            nTestsPassed++;
                            break;
                        }

                        DialogResult dr = MessageBox.Show(
                            errorMessage + "\r\n" +
                            "\r\n" +
                            "Press:\r\n" +
                            "- 'Yes' to accept current changes\r\n" +
                            "- 'No' to view full file comparison\r\n" +
                            "- 'Cancel' to abort and fix code\r\n"
                            , "Test failed - " + testNameShort, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button3);

                        if (dr == DialogResult.Cancel)
                        {
                            if (bAllowExceptionThrow)
                                throw new Exception("Testing aborted");

                            Console.WriteLine();
                            Console.WriteLine("Testing aborted.");
                            return;
                        }

                        // If first file (console output), save log file before performing move
                        if (iFile == 0)
                            File.WriteAllText(logActualFile, logActual);

                        if (dr == DialogResult.Yes)
                        {
                            File.Copy(logActualFile, logAcceptedFile, true);
                        }
                        else
                        {
                            //
                            // Launch diff viewer.
                            //
                            if (!bTortoiseDiff)
                            {
                                // Change background color of comparison.
                                RegistryKey key = RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.CurrentUser, RegistryView.Registry64).CreateSubKey(@"Software\PrestoSoft\ExamDiff\Settings");
                                if (key != null && key.GetValue("TestInitialized") as String != "1")
                                {
                                    key.SetValue("TestInitialized", "1");
                                    key.SetValue("Back Changed Color", 0xb7b7ff);
                                }
                            }

                            if (!bAcceptedFileExists)   // Just a dummy file so comparison tool would not mind.
                                File.WriteAllText(logAcceptedFile, "");

                            Process.Start(diffExe, "\"" + logActualFile + "\" \"" + logAcceptedFile + "\"").WaitForExit();
                            logActual = File.ReadAllText(logActualFile);
                        } //if-else
                    } //while (file is not the same)

                    if (File.Exists(logActualFile) && !bKeepResults)
                        File.Delete(logActualFile);

                } //for file

                Console.Write(".");
            } //for test
        } //for dir

        Console.WriteLine(" ok.");
        Console.WriteLine(nTestsPassed + " tests passed.");
    } //StartSelfDiagnostic


    public class ConsoleWriter : TextWriter
    {
        public StringBuilder sb = new StringBuilder();

        public override Encoding Encoding { get { return Encoding.UTF8; } }

        public override void Write(string value)
        {
            sb.Append(value);
        }

        public override void WriteLine(string value)
        {
            sb.AppendLine(value);
        }
    }

    /// <summary>
    /// Executes command and returns standard output & standard error to error string.
    /// </summary>
    /// <returns>Application exit code</returns>
    public static int ExecCmd(String cmd, ref String error)
    {
        Process p = new Process();
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.RedirectStandardOutput = true;
        p.StartInfo.FileName = "cmd.exe";
        // Whole command should be quoted.
        // cmd.exe /C ""mytool.exe" "c:\mypath\myfile.txt""
        //            ^                                   ^                                   ^
        // https://social.msdn.microsoft.com/forums/vstudio/en-US/03ea84cf-19a6-450d-a3d6-8a139857e0cd/help-with-paths-containing-spaces
        //
        p.StartInfo.Arguments = "/C \"" + cmd + "\" 2>&1";
        // Console.WriteLine("Executing 'cmd.exe " + p.StartInfo.Arguments + "'");
        p.Start();
        error = p.StandardOutput.ReadToEnd();
        p.WaitForExit();

        return p.ExitCode;
    } //ExecCmd

} //partial class Script

