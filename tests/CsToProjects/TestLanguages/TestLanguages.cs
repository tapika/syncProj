//css_ref ..\..\..\syncproj.exe
using System;

partial class Builder : SolutionProjectBuilder
{
    static void Main(String[] args)
    {
        project("out_languages");
        platforms( "ARM" );
        kind("SharedLib", "android");

        RunTimeTypeInformation(true);

        var type = typeof(ECLanguageStandard);
        foreach( String name in Enum.GetNames(type) )
        {
            String fName = name.ToLower() + ".c";
            files("?" + fName);
            filter("files:" + fName);
            CLanguageStandard( (ECLanguageStandard) Enum.Parse( type, name ) );
        }

        type = typeof(ECppLanguageStandard);
        foreach (String name in Enum.GetNames(type))
        {
            String fName = name.ToLower() + ".cpp";
            files("?" + fName);
            filter("files:" + fName);
            CppLanguageStandard((ECppLanguageStandard)Enum.Parse(type, name));
        }
    } //Main
}; //class Builder

