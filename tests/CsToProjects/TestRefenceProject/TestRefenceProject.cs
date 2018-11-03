//css_ref ..\..\..\syncproj.exe
using System;

partial class Builder : SolutionProjectBuilder
{
    static void Main(String[] args)
    {
        try
        {
            solution("out_test");
                platforms("Win32");
            
            project("out_lib");
                platforms("Win32");
                kind("StaticLib");

            project("out_test");
                platforms("Win32");
                kind("ConsoleApp");

                references("out_lib.vcxproj", "");

        }
        catch (Exception ex)
        {
            ConsolePrintException(ex, args);
        }
    } //Main
}; //class Builder

