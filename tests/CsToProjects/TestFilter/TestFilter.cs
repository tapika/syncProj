//css_ref ..\..\..\syncproj.exe
using System;

partial class Builder : SolutionProjectBuilder
{
    static void Main(String[] args)
    {
        try
        {
            project("out_test_windows");
                platforms("Win32", "x64");

            files("?test.c");

            filter("files:test.c", "platforms:Win32");
                buildrule( new CustomBuildRule() { Command = "echo 1", Outputs = "test.log", Message = "", AdditionalInputs = "in.txt", LinkObjects = false } );
            filter();

            files( "?indir1/alpha.c" );
            files( "?indir1/beta.c" );
            files( "?indir1/subdir/gamma.c" );
            files( "?indir2/delta.c" );

            filter( "files:indir1/*.c", "platforms:Win32" );
                defines( "ALPHA_OR_BETA" );
            filter();

            filter( "files:indir1/**.c", "platforms:Win32" );
                defines( "ALPHA_OR_BETA_OR_GAMMA" );
            filter();
            
            filter( "files:**delta.c", "platforms:Win32" );
                defines( "DELTA" );
            filter();


            project("out_test_filter");
            platforms("Win32");
            configurations("Debug", "Release", "ReleaseDebug");

                filter("Debug");
                defines("_DEBUG");

                filter("Release");
                defines("NDEBUG");

                filter("ReleaseDebug");
                defines("NDEBUG2");

        }
        catch (Exception ex)
        {
            ConsolePrintException(ex, args);
        }
    } //Main
}; //class Builder

