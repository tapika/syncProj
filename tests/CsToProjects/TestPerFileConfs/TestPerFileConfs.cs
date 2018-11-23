//css_ref ..\..\..\syncproj.exe
using System;

partial class Builder : SolutionProjectBuilder
{
    static void fileOptimize( String file, String optLevel )
    {
        files( "?" + file );
        filter( "files:" + file );
        optimize( optLevel );
    }

    static void testProject( int vsVer, String platform, String sSymbols )
    {
        project( "out_Test" + platform + "_symbols_" + sSymbols + "_vs" + vsVer.ToString() );
        vsver( vsVer );
        platforms( "Win32" );
        kind( "ConsoleApp", platform );
        symbols( sSymbols );

        fileOptimize( "test.cpp", "off" );
        fileOptimize( "test2.cpp", "speed" );
        fileOptimize( "test3.cpp", "full" );
        fileOptimize( "test4.cpp", "size" );
        fileOptimize( "test5.cpp", "custom" );

        // Will end up with same .obj filename, will create separate configuration for that file.
        // Must use same optimization level as project
        files( "?dir/test5.cpp" );
    }


    static void Main(String[] args)
    {
        try
        {
            solution("out_TestPerFileConfs");

            testProject( 2013, "Windows", "on" );
            testProject( 2013, "Windows", "off" );
            testProject( 2010, "Windows", "on" );
            testProject( 2010, "Windows", "off" );
            testProject( 2015, "Android", "off" );
            testProject( 2017, "Windows", "on" );
            testProject( 2017, "Windows", "fastlink");
            testProject( 2017, "Windows", "fulldebug");

        }
        catch (Exception ex)
        {
            ConsolePrintException(ex, args);
        }
    } //Main
}; //class Builder

