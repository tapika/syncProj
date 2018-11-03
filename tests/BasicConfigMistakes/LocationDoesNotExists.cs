//css_ref ..\..\..\..\syncproj.exe
using System;

partial class Builder : SolutionProjectBuilder
{
    static void Main(String[] args)
    {
        try
        {
            project("out_test_windows");
                location("subdir_WhichDoesNotExists");
                platforms("Win32");
        }
        catch (Exception ex)
        {
            ConsolePrintException(ex, args);
        }
    } //Main
}; //class Builder

