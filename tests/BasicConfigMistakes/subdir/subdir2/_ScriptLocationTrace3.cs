//css_ref ..\..\..\..\syncproj.exe
using System;
using System.IO;

partial class Builder : SolutionProjectBuilder
{
    static void Main(String[] args)
    {
        Console.WriteLine("3. I'm in folder '" + m_scriptRelativeDir + "'");
    } //Main
}; //class Builder

