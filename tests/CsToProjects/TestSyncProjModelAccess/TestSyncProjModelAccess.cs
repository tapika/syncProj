//css_ref ..\..\..\syncproj.exe
using System;

partial class Builder : SolutionProjectBuilder
{
    static void ListFiles( String pattern, bool bAllowNoFiles = false )
    {
        try
        {
            //
            // Walking through files already added to project.
            //
            Console.WriteLine( "--------- List of files by pattern '" + pattern + "' ---------" );
            foreach( FileInfo fi in getCurrentProjectFiles( pattern, bAllowNoFiles ) )
            {
                Console.WriteLine( fi.relativePath );
            }
        }
        catch( Exception ex )
        {
            Console.WriteLine( "Exception: '" + ex.Message + "'" );
        
        }
    }

    static void Main(String[] args)
    {
        solution( "out_TestSyncProjModelAccess" );

        project( "out_TestSyncProjModelAccess" );
        vsver(2015);
        platforms("Win32");
        kind("ConsoleApp");

        files( "?some.cpp" );
        files( "?x86/1.asm", "?x86/2.asm", "?x86/3.asm", "?x86/subdir/4.asm" );

        ListFiles( "x86/**.asm" );
        ListFiles( "x86/*.asm" );
        ListFiles( "**" );
        ListFiles( "does_not_exists" );
        ListFiles( "does_not_exists", true );
        ListFiles( "does_not_exists_with_pattern/**", true );

        foreach( FileInfo fi in getCurrentProjectFiles( "*.cpp" ) )
        {
            selectConfigurations( fi );
            defines( "CPP_DEFINE" );
        }

        foreach( FileInfo fi in getCurrentProjectFiles( "**.asm" ) )
        {
            selectConfigurations( fi );

            buildrule( new CustomBuildRule()
            {
                Message = "Assembling '" + fi.relativePath + "'...",
                Command = "superasm.exe " + fi.relativePath,
                Outputs = fi.relativePath + ".obj"
            } );
        }

    } //Main
}; //class Builder

