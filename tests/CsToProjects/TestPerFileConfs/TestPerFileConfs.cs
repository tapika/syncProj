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

    static void Main(String[] args)
    {
        try
        {
            solution("out_TestPerFileConfs");

            project("out_TestWin");
            vsver(2012);
            platforms("Win32");
            kind("ConsoleApp");

            fileOptimize( "test.cpp", "off" );
            fileOptimize( "test2.cpp", "speed" );
            fileOptimize( "test3.cpp", "full" );
            fileOptimize( "test4.cpp", "size" );
            fileOptimize( "test5.cpp", "custom" );
            
            // Will end up with same .obj filename, will create separate configuration for that file.
            // Must use same optimization level as project
            files( "?dir/test5.cpp");

            project( "out_TestAndroid" );
            vsver( 2012 );
            platforms( "ARM" );
            kind( "ConsoleApp", "android" );

            fileOptimize( "test.cpp", "off" );
            fileOptimize( "test2.cpp", "speed" );
            fileOptimize( "test3.cpp", "full" );
            fileOptimize( "test4.cpp", "size" );
            fileOptimize( "test5.cpp", "custom" );

            // Will end up with same .obj filename, will create separate configuration for that file.
            // Must use same optimization level as project
            files( "?dir/test5.cpp" );
        }
        catch (Exception ex)
        {
            ConsolePrintException(ex, args);
        }
    } //Main
}; //class Builder

