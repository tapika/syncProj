//css_ref ..\..\..\syncproj.exe
using System;

partial class Builder : SolutionProjectBuilder
{
    static String[] sArray(params String[] args) { return args; }

    static void Main(String[] args)
    {
        var type = typeof(EExceptionHandling);
        
        foreach (String p in sArray("windows", "android"))
        {
            String[] enumNames = Enum.GetNames(type);

            project("out_ehtest_" + p);
            platforms("Win32");

            foreach (String name in enumNames)
            {
                EExceptionHandling eh = (EExceptionHandling)Enum.Parse(type, name);
                if (eh == EExceptionHandling.ProjectDefault)
                    continue;
                kind("DynamicLibrary", p);
                String file = name.ToLower() + ".cpp";
                files("?" + file);
                filter("files:" + file);
                CCpp_CodeGeneration_EnableCppExceptions(eh);
            }
        }
    } //Main
}; //class Builder

