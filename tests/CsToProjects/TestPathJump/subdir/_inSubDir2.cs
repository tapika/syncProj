//css_ref ..\..\..\..\..\syncproj.exe
using System;

partial class Builder : SolutionProjectBuilder
{
    static void Main(String[] args)
    {
        Console.WriteLine("This script ran from: " + Path2.makeRelative(getCsDir(), m_workPath));
        Console.WriteLine("Script name: " + getCsFileName() );
    } //Main
}; //class Builder


