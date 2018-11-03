using System;

partial class Builder: SolutionProjectBuilder
{
    static void DoTest1()
    {
        Console.WriteLine("This script ran from: " + Path2.makeRelative(getCsDir(), m_workPath));
        Console.WriteLine("Script name: " + getCsFileName() );
    } //Main
}; //class Builder

