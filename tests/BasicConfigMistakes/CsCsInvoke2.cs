//css_ref ..\..\syncproj.exe
//css_include ..\_cscsInvokeHelper.cs

using System;

partial class Builder : SolutionProjectBuilder
{
    static void Main(String[] args)
    {
        setWorkPath();
        SomeMethod(args);
    } //Main
}; //class Builder

