//css_ref ..\..\syncproj.exe
using System;

partial class Builder: SolutionProjectBuilder
{

    static void Main(String[] args)
    {
        try {
            group("WrongEscapeSequence\finpath");
        }
        catch ( Exception ex )
        {
            ConsolePrintException(ex, args);
        }
    } //Main
}; //class Builder
