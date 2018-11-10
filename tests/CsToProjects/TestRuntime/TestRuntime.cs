//css_ref ..\..\..\syncproj.exe
using System;

partial class Builder : SolutionProjectBuilder
{
    static void Main(String[] args)
    {
        var type = typeof(ERuntimeLibrary);
        foreach( String name in Enum.GetNames(type) )
        {
            project( "out_rt" + name.ToLower() );
            platforms( "Win32" );
            CCpp_CodeGeneration_RuntimeLibrary( (ERuntimeLibrary) Enum.Parse( type, name ) );
        }

        type = typeof(ECLRSupport);
        foreach (String name in Enum.GetNames(type))
        {
            project("out_clr_" + name.ToLower());
            platforms("Win32");
            commonLanguageRuntime((ECLRSupport)Enum.Parse(type, name));
        }
    } //Main
}; //class Builder

