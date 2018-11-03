//css_ref ..\..\..\syncproj.exe
using System;

partial class Builder : SolutionProjectBuilder
{
    static void Main(String[] args)
    {
        try
        {
            project("out_test_windows");
                platforms("Win32", "x64");

            files("?test.c");

            filter("files:test.c", "platforms:Win32");
                buildrule( new CustomBuildRule() { Command = "echo 1", Outputs = "test.log", Message = "" } );
            filter();

        }
        catch (Exception ex)
        {
            ConsolePrintException(ex, args);
        }
    } //Main
}; //class Builder

