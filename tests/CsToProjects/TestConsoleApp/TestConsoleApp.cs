//css_ref ..\..\..\syncproj.exe
using System;

partial class Builder : SolutionProjectBuilder
{
    static void Main(String[] args)
    {
        try
        {
            project("out_static_lib");
                platforms("Win32", "x64");

            kind("ConsoleApp");

            buildoptions("/compiler_flag");
            linkoptions("/linker_flag1");
            linkoptions("/linker_flag2");
            files("?test.cpp");
            Linker_Optimizations_References();
            CCpp_CodeGeneration_EnableFunctionLevelLinking();
            Ccpp_Optimization_WholeProgramGeneration(EWholeProgramOptimization.UseLinkTimeCodeGeneration);
            Ccpp_Optimization_Optimization(EOptimization.MinSpace);
        }
        catch (Exception ex)
        {
            ConsolePrintException(ex, args);
        }
    } //Main
}; //class Builder

