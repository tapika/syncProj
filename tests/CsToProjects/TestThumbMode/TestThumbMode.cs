//css_ref ..\..\..\syncproj.exe
using System;
using System.IO;

partial class Builder : SolutionProjectBuilder
{
    static void Main(String[] args)
    {
        String csName = getExecutingScript( false, true );
        project( "out_" + csName );
        vsver(2015);
        platforms("ARM", "ARM64");
        kind("DynamicLibrary", "android");

        filter( "platforms:ARM" );
            thumbmode( EThumbMode.ARM );

    } //Main
}; //class Builder

