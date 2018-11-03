//css_ref ..\..\..\syncproj.exe
using System;

partial class Builder : SolutionProjectBuilder
{
    static void Main(String[] args)
    {
        project("out_DoubleFile");
            files("?Memory.cpp");
            files("?memOry.cpp");           // Same file, only using different case

            files("?Stack.cpp");
            files("?Stack.c");              // Different file, but duplicate .obj => stack1.obj
            files("?SubFolder/meMory.cpp");
            files("?SubFolder/Stack.c");    // Same name => stack2.obj
    } //Main
}; //class Builder

