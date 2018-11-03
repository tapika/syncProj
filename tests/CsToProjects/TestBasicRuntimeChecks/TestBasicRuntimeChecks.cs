//css_ref ..\..\..\syncproj.exe
using System;

partial class Builder : SolutionProjectBuilder
{
    static void Main(String[] args)
    {
        var type = typeof(EBasicRuntimeChecks);
        foreach( String name in Enum.GetNames(type) )
        {
            project( "out_rtc" + name.ToLower() );
            platforms( "Win32" );
            CCpp_CodeGeneration_BasicRuntimeChecks( (EBasicRuntimeChecks) Enum.Parse( type, name ) );
        }

        project("out_ReleaseWithSymbols");
            configurations("Release");
            optimize("speed");
            // Should turn off BasicRuntimeChecks to avoid compilation error.
            symbols("on");
            files("?test.cpp");

    } //Main
}; //class Builder

