//css_include _helloWorld3.cs
using System;

partial class Builder : SolutionProjectBuilder
{
    public static void SomeMethod()
    {
         Console.WriteLine("Hello world");
         SomeMethod2();
    } //Main
}; //class Builder

