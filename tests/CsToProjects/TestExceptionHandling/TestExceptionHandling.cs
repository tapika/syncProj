//css_ref ..\..\..\syncproj.exe
using System;

partial class Builder : SolutionProjectBuilder
{
    static void Main(String[] args)
    {
        var type = typeof(EExceptionHandling);
        foreach( String name in Enum.GetNames(type) )
        {
            EExceptionHandling eh = (EExceptionHandling)Enum.Parse(type, name);
            if (eh == EExceptionHandling.ProjectDefault)
                continue; 
            project( "out_eh" + name.ToLower() );
            platforms( "Win32" );
            CCpp_CodeGeneration_EnableCppExceptions( eh );
        }
    } //Main
}; //class Builder

