//css_ref ..\..\..\syncproj.exe
using System;

class Builder: SolutionProjectBuilder
{
    static void Main(String[] args)
    {
        try {

            project("out_MFCApplication1");
                configurations(  "Debug","Release" );
                platforms( "Win32","x64" );
                uuid("22DF2B57-18B4-4C55-A294-03C2FB53D478");
                vsver(2015);
                projectScript("MFCApplication1.cs");
                flags("MFC");
                systemversion("8.1");
                kind("WindowedApp","windows");
                toolset("v140");
                characterset("Unicode");
                pchheader("stdafx.h");
                defines( "_WINDOWS" );
                filter ( "Debug" );
                    symbols("on");
                    optimize("off");
                    postbuildcommands( "echo Post build step from Debug configuration" );
                    defines( "_DEBUG" );

                filter ( "Release" );
                    symbols("off");
                    optimize("speed");
                    flags( "LinkTimeOptimization" );
                    prebuildcommands( "echo Prebuild step from Release configuration" );
                    prelinkcommands( "echo prelink event" );
                    defines( "NDEBUG" );

                filter ( "platforms:Win32" );
                    defines( "WIN32" );

                filter (  );

                files( 
                    "ReadMe.txt",
                    "MFCApplication1.h",
                    "MFCApplication1Dlg.h",
                    "Resource.h",
                    "stdafx.h",
                    "targetver.h",
                    "MFCApplication1.cpp",
                    "MFCApplication1Dlg.cpp",
                    "stdafx.cpp",
                    "MFCApplication1.rc",
                    "res/MFCApplication1.rc2",
                    "res/MFCApplication1.ico"
                 );
                filter (  );
                pchsource("stdafx.cpp");

        } catch( Exception ex )
        {
            ConsolePrintException(ex, args);
        }
    } //Main
}; //class Builder

