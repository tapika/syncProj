//css_ref ..\..\..\syncproj.exe
using System;
using System.IO;

partial class Builder : SolutionProjectBuilder
{
    static void Main(String[] args)
    {
        String csName = getExecutingScript( false, true );
        solution( "out_" + csName );

        project( "out_" + csName );
        vsver(2015);
        platforms("Win32");
        kind("ConsoleApp");
        files( "?some.cpp" );

        solution( "out_" + csName + "2");

        // Reselect solution, reselect project, add one more file.
        solution( "out_" + csName );
        project( "out_" + csName );
        files( "?some2.cpp" );

        Console.WriteLine(Path.GetFileName(getExecutingScript()));
        Console.WriteLine(getExecutingScript(false, true));
        Console.WriteLine(getExecutingScript(false, true, true) );
        Console.WriteLine( Exception2.getPath( getExecutingScript(true, true)) );


    } //Main
}; //class Builder

