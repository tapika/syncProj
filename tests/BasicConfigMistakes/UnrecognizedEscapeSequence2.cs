//css_ref ..\..\syncproj.exe
using System;

partial class Builder: SolutionProjectBuilder
{

    static void Main(String[] args)
    {
        try {
            project("0_test");
                platforms("Win32");
                files ("test\ainvalidPath");
        }
        catch ( Exception ex )
        {
            ConsolePrintException(ex, args);
        }
    } //Main
}; //class Builder
