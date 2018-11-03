//css_ref ..\..\..\syncproj.exe
using System;

partial class Builder : SolutionProjectBuilder
{
    static void Main(String[] args)
    {
        try
        {
            project("out_test_windows");
            platforms("Win32");

            prebuildcommands("echo 1");
            prebuildcommands("echo 2");

            prelinkcommands("echo 3");
            prelinkcommands("echo 4");
            
            postbuildcommands("echo 5");
            postbuildcommands("echo 6");

            exportFiles("..", "test.h", "test2.h");
        }
        catch (Exception ex)
        {
            ConsolePrintException(ex, args);
        }
    } //Main
}; //class Builder

