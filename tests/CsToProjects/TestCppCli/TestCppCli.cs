//css_ref ..\..\..\syncproj.exe
using System;

partial class Builder : SolutionProjectBuilder
{
    static void projVariant(ECLRSupport clr, String ver, String custom = null)
    {
        String name = "out_clr_" + clr.ToString().ToLower() + "_NET_" + ver;
        if (custom != null) name += "_" + custom;
        project(name);
        vsver(2017);
        platforms("Win32");
        configurations("Release", "Debug");
        kind("consoleapplication");
        commonLanguageRuntime(clr);
        TargetFrameworkVersion(ver);
    }


    static void Main(String[] args)
    {
        solution("out_CppCliProjects");
        vsver(2017);
        platforms("Win32");
        configurations("Release", "Debug");
        projVariant(ECLRSupport.None, "v4.5");
        projVariant(ECLRSupport.Pure, "v4.7.2");
        projVariant(ECLRSupport.Safe, "v4.7.1");
        projVariant(ECLRSupport.True, "v4.7.2", "debug_custom");
        references("System");
        references( @"..\..\..\syncproj.exe", false, false, false );
        filter("Debug");
        commonLanguageRuntime(ECLRSupport.Safe);
    }
};



