//css_ref ..\..\..\syncproj.exe
using System;
using System.IO;

class Builder: SolutionProjectBuilder
{
    static void Main(String[] args)
    {
        try {

            solution("out_MFCApplication1_sln");
                vsver(2015);
                configurations(  "Debug","Release" );
                platforms( "x64","x86" );
                solutionScript("MFCApplication1_sln.cs");
                
                invokeScript("MFCApplication1.cs");

        } catch( Exception ex )
        {
            ConsolePrintException(ex, args);
        }
    } //Main
}; //class Builder

