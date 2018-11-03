//css_ref ..\..\..\syncproj.exe
using System;

partial class Builder : SolutionProjectBuilder
{
    static void Main(String[] args)
    {
        solution("out_TestCharsets");

        foreach (String chars in new String[] { "Invalid", "Unicode", "Multibyte", "mbcs" })
        {
            try
            {
                String script = "out_Project" + chars;
                project(script);
                vsver(2015);
                platforms("Win32", "x64");
                kind("ConsoleApp", "windows");
                characterset(chars);
                files("?test.cpp");
            }
            catch (Exception ex)
            {
                ConsolePrintException(ex, new String[] { } );
                m_project = null;       // Don't save corrupted project
            }
        } //foreach

        // Force to save dispite of errors.
        SaveGenerated(true);
    } //Main
}; //class Builder

