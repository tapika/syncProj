//css_ref ..\..\..\syncproj.exe
//css_ref System.Web.Extensions.dll
using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;

// Just a test application that we can use any 3rd party .dll as css_ref

public class Test
{
    public string S { get; set; }
    public int I { get; set; }
    public List<int> L;
}

partial class Builder : SolutionProjectBuilder
{
    static void Main(String[] args)
    {
        var json = @"{
""S"": ""Hello, world."",
""I"": 4711,
""L"": [1, 2, 3]
}";

        Test t = (Test)new JavaScriptSerializer().Deserialize(json, typeof(Test));

        Console.WriteLine("S: " + t.S);
        Console.WriteLine("I: " + t.I);
        Console.WriteLine("L: " + String.Join(", ", t.L));

    } //Main
}; //class Builder

