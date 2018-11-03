//css_ref ..\..\..\syncproj.exe
using System;
using System.IO;

partial class Builder : SolutionProjectBuilder
{
    static void Main(String[] args)
    {
        Console.WriteLine("2. I'm in folder '" + m_scriptRelativeDir + "'");
        invokeScript("subdir2/_ScriptLocationTrace3.cs");
        Console.WriteLine("2. I'm in folder '" + m_scriptRelativeDir + "'");
    } //Main
}; //class Builder

