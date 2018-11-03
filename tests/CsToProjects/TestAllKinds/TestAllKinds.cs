//css_ref ..\..\..\syncproj.exe
using System;

partial class Builder : SolutionProjectBuilder
{
    static void Main(String[] args)
    {
        try
        {
            solution("out_TestAllKinds");

                foreach (String _kind in new String[] { "ConsoleApp", "WindowedApp", "Application", "SharedLib", "DynamicLibrary", "StaticLibrary", "StaticLib", "Utility", "ConsoleApplication" })
                {
                    String script = "out_Project" + _kind;
                    project(script);
                    vsver(2013);
                    platforms("Win32", "x64");
                    kind(_kind, "windows");
                    //projectScript("TestAllKinds.cs");
                    files("?test.cpp");
                }
        }
        catch (Exception ex)
        {
            ConsolePrintException(ex, args);
        }
    } //Main
}; //class Builder

