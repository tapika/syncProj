using System;

partial class Builder : SolutionProjectBuilder
{
    public static void SomeMethod( String[] args )
    {
        SomeMethod2(args);
    } //Main

    public static void SomeMethod2( String[] args )
    {
        initFromArgs(args);
        Console.WriteLine( "Started from path '" + Exception2.getPath(m_workPath) + "'");
    } //Main

}; //class Builder

