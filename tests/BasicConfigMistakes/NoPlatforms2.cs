//css_ref ..\..\syncproj.exe
using System;

partial class Builder: SolutionProjectBuilder
{

    static void Main(String[] args)
    {
        project("out_NoPlatforms2");

        platforms("Win32");
        files("?dbd.cpp");
        defines( "_DEBUG" );

        platforms("Win32", "x64");
    } //Main
}; //class Builder
