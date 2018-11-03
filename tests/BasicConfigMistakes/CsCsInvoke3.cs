//css_ref ..\..\syncproj.exe
//css_include _helloWorld2.cs

using System;

partial class Builder : SolutionProjectBuilder
{
    static void Main(String[] args)
    {
        try
        {
            // First instantiate SolutionProjectBuilder, then initialize -x command line arguments to not to print full path's.
            String testx = m_workPath;
            initFromArgs(args);
            Console.WriteLine( "Started from path '" + Exception2.getPath(m_workPath) + "'");
        }
        catch (Exception ex)
        {
            ConsolePrintException(ex, args);
        }
    } //Main
}; //class Builder

