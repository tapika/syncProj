//css_ref ..\..\..\syncproj.exe
using System;

partial class Builder : SolutionProjectBuilder
{
    static void Main(String[] args)
    {
        try
        {
            solution("out_TestPerFileConfs");

            project("out_TestWin");
            vsver(2012);
            platforms("Win32");
            kind("ConsoleApp");

            files("?test.cpp");
            filter("files:test.cpp");
            optimize("off");

            files("?test2.cpp");
            filter("files:test2.cpp");
            optimize("speed");

            files( "?test3.cpp" );
            filter( "files:test3.cpp" );
            optimize( "full" );

            files( "?test4.cpp" );
            filter( "files:test4.cpp" );
            optimize( "size" );

            files( "?test5.cpp" );
            filter( "files:test5.cpp" );
            optimize( "custom" );

            project( "out_TestAndroid" );
            vsver( 2012 );
            platforms( "ARM" );
            kind( "ConsoleApp", "android" );

            files( "?test.cpp" );
            filter( "files:test.cpp" );
            optimize( "off" );

            files( "?test2.cpp" );
            filter( "files:test2.cpp" );
            optimize( "speed" );

            files( "?test3.cpp" );
            filter( "files:test3.cpp" );
            optimize( "full" );

            files( "?test4.cpp" );
            filter( "files:test4.cpp" );
            optimize( "size" );

            files( "?test5.cpp" );
            filter( "files:test5.cpp" );
            optimize( "custom" );

        }
        catch (Exception ex)
        {
            ConsolePrintException(ex, args);
        }
    } //Main
}; //class Builder

