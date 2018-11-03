//css_ref ..\..\..\syncproj.exe
using System;

partial class Builder : SolutionProjectBuilder
{
    static void Main(String[] args)
    {
        try
        {
            solution("out_test");
                platforms("Win32");
            
            project("out_test_windows");
                platforms("Win32");

            files("?test.c");

            filter("Debug", "files:test.c");
                disablewarnings( "4244", "4127" );
            filter();
            files("?test2.c");

            disablewarnings( "4018", "4018" );
        }
        catch (Exception ex)
        {
            ConsolePrintException(ex, args);
        }
    } //Main
}; //class Builder

