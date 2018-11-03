//css_ref ..\..\..\syncproj.exe
using System;

partial class Builder : SolutionProjectBuilder
{
    static void Main(String[] args)
    {
        solution("out_test");
            platforms("ARM");
            
        project("out_lan1");
            platforms("ARM");
            kind("StaticLib", "android");
            language();
            files("?test.c");

        project("out_lan2");
            platforms("ARM");
            kind("StaticLib", "android");
            language("C");
            files("?test.c");

        project("out_lan3");
            platforms("ARM");
            kind("StaticLib", "android");
            language("C++");
            files("?test.c");

    } //Main
}; //class Builder

