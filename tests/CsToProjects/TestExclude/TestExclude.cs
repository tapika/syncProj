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

            filter("Debug", "files:test.c");
                flags("excludedfrombuild");
                // Allow different naming just from flexibility perspective
                flags("excludefrombuild");
            filter();

        }
        catch (Exception ex)
        {
            ConsolePrintException(ex, args);
        }
    } //Main
}; //class Builder

