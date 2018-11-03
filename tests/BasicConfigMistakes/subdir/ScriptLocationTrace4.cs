//css_ref ..\..\..\syncproj.exe
using System;
using System.IO;

partial class Builder : SolutionProjectBuilder
{
    static void Main(String[] args)
    {
        project("out_ScriptLocationTrace4"); platforms("Win32"); projectScript("ScriptLocationTrace4.cs");

        Console.WriteLine("4. I'm in folder '" + m_scriptRelativeDir + "'");
        invokeScript("subdir2/_ScriptLocationTrace3.cs");
        Console.WriteLine("4. I'm in folder '" + m_scriptRelativeDir + "'");
    } //Main
}; //class Builder

