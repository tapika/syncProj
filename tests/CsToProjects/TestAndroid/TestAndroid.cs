//css_ref ..\..\..\syncproj.exe
using System;

partial class Builder : SolutionProjectBuilder
{
    static void Main(String[] args)
    {
        project( "out_TestAndroid" );
        configurations("DebugRelease");
        platforms( "ARM64" );
        kind("StaticLibrary", "android");

        files("?test.c");

        files("?test2.cpp");

        files("?test3.cpp");
        filter("files:test3.cpp");
        language("C");
        filter();

        files("?test4.c");
        filter("files:test4.c");
        language("C++");
        filter();
    }
};

