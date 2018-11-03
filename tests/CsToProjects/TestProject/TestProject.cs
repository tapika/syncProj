//css_ref ..\..\..\syncproj.exe
using System;

partial class Builder : SolutionProjectBuilder
{
    static void Main(String[] args)
    {
        try
        {
            solution("out_test");
                platforms("Win32", "x64", "ARM", "ARM64");
            
            project("out_test_windows");
                platforms("Win32", "x64");

            files("test.c");

            filter("Debug", "files:test.c");
                defines("HAVE_DYNAMIC_PLUGINS");

        }
        catch (Exception ex)
        {
            ConsolePrintException(ex, args);
        }
    } //Main
}; //class Builder

