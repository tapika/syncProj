//css_ref ..\..\..\syncproj.exe
using System;

partial class Builder : SolutionProjectBuilder
{
    static void Main(String[] args)
    {
        foreach (String p in sArray("windows", "android"))
        {
            project("out_enablemultiprocessor_" + p);
            platforms("Win32");
            kind("DynamicLibrary", p);
            files("?test.cpp");

            // Test that 'MinimalRebuild' gets produced.
            filter("Debug");
                symbols("on");
            filter();

            EnableMultiProcessorBuild();
        }
    }

    static String[] sArray(params String[] args) { return args; }
};


