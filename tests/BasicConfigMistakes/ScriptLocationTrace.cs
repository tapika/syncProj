//css_ref ..\..\syncproj.exe
using System;
using System.IO;

partial class Builder: SolutionProjectBuilder
{

    static void Main(String[] args)
    {
        try {
            setLocationFromScriptPath();
            Console.WriteLine("1. I'm in folder '" + m_scriptRelativeDir + "'");
            invokeScript("subdir/_ScriptLocationTrace2.cs");
            Console.WriteLine("1. I'm in folder '" + m_scriptRelativeDir + "'");
            invokeScript("subdir/ScriptLocationTrace4.cs");
            Console.WriteLine("1. I'm in folder '" + m_scriptRelativeDir + "'");

        }
        catch ( Exception ex )
        {
            ConsolePrintException(ex, args);
        }
    } //Main
}; //class Builder

