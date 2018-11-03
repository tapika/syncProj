//css_ref ..\..\..\syncproj.exe
using System;

partial class Builder : SolutionProjectBuilder
{
    static void Main(String[] args)
    {
        try
        {
            solution("Test");
            project("out_Test");
            vsver(2010);
            platforms("Win32");
            kind("ConsoleApp");
            files("?test.def");
        }
        catch (Exception ex)
        {
            ConsolePrintException(ex, args);
        }
    } //Main
}; //class Builder

