//css_ref ..\..\syncproj.exe
/*
    We here test how syncProj will compile / not compile C# scripts. 
*/
using System;
using System.IO;

partial class Builder : SolutionProjectBuilder
{
    static public void inv( int i, bool bExpectedToBeCompled )
    {
        Console.Write( i.ToString() + ") invokeScript: ");
        if (bExpectedToBeCompled)
            Console.WriteLine("(compiled):");
        else
            Console.WriteLine("(up-to-date):");

        invokeScript("_test1.cs", true);
        Console.WriteLine("");
        i++;
    }

    static void save(String file, String contents )
    {
        String path = Path.Combine(SolutionProjectBuilder.m_workPath, file);
        File.WriteAllText(path, contents);
    }

    static void setdate(String file, DateTime dt)
    {
        String path = Path.Combine(SolutionProjectBuilder.m_workPath, file);
        File.SetLastWriteTime(path, dt);
    }

    static String fullpath( String file )
    {
        String path = Path.Combine(SolutionProjectBuilder.m_workPath, file);
        return path;    
    }
    
    static void Main(String[] args)
    {
        try
        {
            CsScriptInfo.g_bCsDebug = true;
            save("_test1.cs", "using System; class MyTest { static void Main(String[] args) { Console.WriteLine(\"test1\"); } };");
            inv(0, true);
            save("_test1.cs", 
                "//css_include _test2.cs\r\n" +
                "using System; partial class MyTest { static void Main(String[] args) { Console.WriteLine(\"test1\"); HelloWorld(); } };");

            save("_test2.cs", "using System; partial class MyTest { static void HelloWorld() { Console.WriteLine(\"test2\"); } };");
            inv(1,true);
            inv(2,false);
            setdate("_test2.cs", File.GetLastWriteTime(fullpath("_test2.cs")).AddDays(-30));
            inv(3,true);
            setdate("_test2.cs", File.GetLastWriteTime(fullpath("_test2.cs")).AddSeconds(1));
            inv(4,true);
            inv(5,false);

            save("_test1.cs",
                "//css_include _test3.cs\r\n" +
                "//css_include _test2.cs\r\n" +
                "using System; partial class MyTest { static void Main(String[] args) { Console.WriteLine(\"test1\"); HelloWorld();HelloWorld2(); } };");

            save("_test2.cs", "using System; partial class MyTest { static void HelloWorld() { Console.WriteLine(\"test2\"); } };");
            save("_test3.cs", "using System; partial class MyTest { static void HelloWorld2() { Console.WriteLine(\"test2\"); } };");
            inv(6,true);
            inv(7,false);
            setdate("_test2.cs", File.GetLastWriteTime(fullpath("_test2.cs")).AddSeconds(-1));
            inv(8,true);
            inv(9,false);
            setdate("_test2.cs", File.GetLastWriteTime(fullpath("_test3.cs")).AddMonths(-1));
            inv(10, true);
            inv(11, false);


        }
        catch (Exception ex)
        {
            ConsolePrintException(ex, args);
        }
        finally
        {
            CsScriptInfo.g_bCsDebug = false;
        }

    } //Main
}; //class Builder

