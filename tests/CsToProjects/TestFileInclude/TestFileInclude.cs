//css_ref ..\..\..\syncproj.exe
using System;

partial class Builder : SolutionProjectBuilder
{
    static void Main(String[] args)
    {
        try
        {
            solution("TestAllKinds");
            project("out_Test");
            vsver(2012);
            platforms("Win32");
            kind("ConsoleApp");
            files("*.h");
        }
        catch (Exception ex)
        {
            ConsolePrintException(ex, args);
        }
    } //Main
}; //class Builder

