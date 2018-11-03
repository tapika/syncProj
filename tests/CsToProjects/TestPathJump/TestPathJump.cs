//css_ref ..\..\..\syncproj.exe
//css_include subdir/_inSubDir.cs
using System;

partial class Builder : SolutionProjectBuilder
{
    static void Main(String[] args)
    {
        getCsDir();     // Instantiate SolutionProjectBuilder
        DoTest1();
        invokeScript( "subdir/_inSubDir2.cs" );
    } //Main
}; //class Builder

