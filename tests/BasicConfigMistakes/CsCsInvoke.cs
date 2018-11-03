//css_ref ..\..\syncproj.exe
//css_import _helloWorld2.cs

using System;

partial class Builder : SolutionProjectBuilder
{
    static void Main(String[] args)
    {
        try
        {
            initFromArgs(args);
            Console.WriteLine( "Started from path '" + Exception2.getPath(m_workPath) + "'");
        }
        catch (Exception ex)
        {
            ConsolePrintException(ex, args);
        }
    } //Main
}; //class Builder

