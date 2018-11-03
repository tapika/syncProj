//css_ref ..\..\..\syncproj.exe
using System;

partial class Builder : SolutionProjectBuilder
{
    static void Main(String[] args)
    {
        project("out_TestRemoveFiles");
            platforms("Win32", "x64");

        files( "?indir1/alpha.c" );
        files( "?indir1/beta.c" );
        files( "?indir1/subdir/gamma.c" );
        files( "?indir2/delta.c" );

        // Select multiple files, and disable them from building on all from Win32 configuration, but keep building in x64 configuration
        filter( "files:indir1/*.c", "platforms:Win32" );
            removefiles( "**" );
        filter();

        // Just remove whole file from project.
        removefiles( "**delta.c" );
    } //Main
}; //class Builder

