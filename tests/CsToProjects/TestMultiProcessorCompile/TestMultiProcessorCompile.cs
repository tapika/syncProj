//css_ref ..\..\..\syncproj.exe
using System;

partial class Builder : SolutionProjectBuilder
{
    static void Main(String[] args)
    {
        project("out_TestMultiProcessorCompile");
            platforms("Win32", "x64");

        kind("ConsoleApp");

        filter("Debug");
            symbols("on");

        filter();
        EnableMultiProcessBuild();
        files("?test.cpp");
    } //Main
}; //class Builder

