using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;


[ExcludeFromCodeCoverage]
public class syncProjUnitSuiteInfo : UnitSuiteInfo
{
    List<UnitTestInfo> unittests = new List<UnitTestInfo>();

    override public IEnumerable<UnitSuiteInfo> GetSuites()
    {
        String testsDir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Tests");

        List<String> allTests = new List<string>();

        //  Rename folder to start from '_' to exclude it from test scope.
        String[] dirs = Directory.GetDirectories(testsDir).Where(x => !Path.GetFileName(x).StartsWith("_")).ToArray();

        foreach (String dir in dirs)
        {
            //
            // All test cases are either .cs or .sln projects.
            //
            String[] tests = Directory.GetFiles(dir, "*", SearchOption.AllDirectories).
                Where(x => (x.EndsWith(".cs") || x.EndsWith(".sln") || x.EndsWith(".bat")) && !Path.GetFileName(x).StartsWith("_")).ToArray();

            allTests.AddRange(tests.Select(x => x.Substring(testsDir.Length + 1)));
        }

        String testsuite = "";
        List<syncProjUnitSuiteInfo> suites = new List<syncProjUnitSuiteInfo>();

        foreach (String test in allTests)
        {
            String[] parts = test.Split('\\').ToArray();
            String newTestsuite = parts.First();

            syncProjUnitSuiteInfo suite = suites.Where(x => x.SuiteName == newTestsuite).FirstOrDefault();
            if (suite == null)
            {
                suite = new syncProjUnitSuiteInfo() { SuiteName = newTestsuite };
                suites.Add(suite);
            }

            syncProjUnitTestInfo unittest = new syncProjUnitTestInfo();
            unittest.sourceCodePath = Path.Combine(testsDir, test);
            String src = parts.Last();
            unittest.UnitTestName = Path.GetFileName(src);

            suite.unittests.Add(unittest);
        }

        return suites;
    }

    override public IEnumerable<UnitTestInfo> GetUnitTests()
    {
        return unittests;
    }
}



[ExcludeFromCodeCoverage]
public class syncProjUnitTestInfo : UnitTestInfo
{
    static String diffExe = null;

    override public void InvokeTest(bool isLastMethod, TestResults localTestResults)
    {
        String testsDir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Tests");

        if (diffExe == null)
        {
            //
            // We use TortoiseMerge.exe if we have one (can copy paste changes in merge tool itself)
            //
            diffExe = @"C:\Program Files\TortoiseSVN\bin\TortoiseMerge.exe";
            bool bTortoiseDiff = true;

            if (!File.Exists(diffExe))
            {
                diffExe = Path.Combine(testsDir, "ExamDiff.exe");
                bTortoiseDiff = false;
            }

            if (!File.Exists(diffExe))
                throw new Exception("Diff tool does not exists '" + diffExe + "'");

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
        }

        int error = 0;
        String test = sourceCodePath;
        String dir = Path.GetDirectoryName(test);
        String logActual = "";

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
            if (Array.IndexOf(excludeBats, Path.GetFileNameWithoutExtension(test).ToLower()) != -1)
                return;

            error = ExecCmd(test + " -x", ref logActual);
            toolName = "Batch";
        }
        else
        {
            ConsoleWriter cw = new ConsoleWriter();
            SolutionProjectBuilder.resetStatics();
            var co = Console.Out;
            Console.SetOut(cw);
            error = syncProj.Main(new String[] { "-x", "-p", "out_", test });

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
        for (int iFile = 0; iFile < verifyFiles.Count; iFile++)
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

                if (iFile != 0)
                    logActual = File.ReadAllText(logActualFile);

                if (logActual == logAccepted)
                {
                    localTestResults.files++;
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
                    throw new OperationCanceledException("Testing aborted");

                // If first file (console output), save log file before performing move
                if (iFile == 0)
                    File.WriteAllText(logActualFile, logActual);

                if (dr == DialogResult.Yes)
                {
                    File.Copy(logActualFile, logAcceptedFile, true);
                }
                else
                {
                    if (!bAcceptedFileExists)   // Just a dummy file so comparison tool would not mind.
                        File.WriteAllText(logAcceptedFile, "");

                    Process.Start(diffExe, "\"" + logAcceptedFile + "\" \"" + logActualFile + "\"").WaitForExit();
                    logActual = File.ReadAllText(logActualFile);
                } //if-else
            } //while (file is not the same)

            if (File.Exists(logActualFile))
                File.Delete(logActualFile);
        } //for file
    }

    public override string ToString()
    {
        return "UnitTestName:" + UnitTestName;
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
}


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

