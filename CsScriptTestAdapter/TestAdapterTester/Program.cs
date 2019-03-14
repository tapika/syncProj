using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

class Program
{
    static String getDir([System.Runtime.CompilerServices.CallerFilePath] string fileName = "")
    {
        return Path.GetDirectoryName(fileName);
    }

    static void Main()
    {
        String dir = getDir();
        Assembly asm = Assembly.LoadFile(@"C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise\Common7\IDE\Extensions\TestPlatform\vstest.console.exe");
        AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
        {
            if (args.RequestingAssembly == null)
                return null;

            String dll = Path.Combine(Path.GetDirectoryName(args.RequestingAssembly.Location), args.Name.Split(',')[0] + ".dll");

            if (File.Exists(dll))
                return Assembly.LoadFrom(dll);

            return null;
        };
        var main = asm.GetTypes().Select(x => x.GetMethod("Main", BindingFlags.Static | BindingFlags.Public)).Where(x => x != null).First();

        main.Invoke(null, 
            new object[] {
                new string[] { "-lt" , Path.Combine(dir, @"..\CsScriptTestAdapter.dll") }
            }
        );
    }
}


