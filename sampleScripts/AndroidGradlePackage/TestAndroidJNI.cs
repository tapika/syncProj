//css_ref ..\..\..\syncproj\syncproj.exe
using System;

class Builder: SolutionProjectBuilder
{
    static void CommonSettings()
    {
        vsver(2015);
        configurations("Debug", "Release");
        platforms("ARM", "ARM64", "x64", "x86");
    }

    static void Main(String[] args)
    {
        //----------------------------------------------------
        solution( "TestAndroidJNI" );
        CommonSettings();

        //--[ C++ ]-------------------------------------------
        project( "native_lib");
        CommonSettings();

        targetdir("bin/$(Configuration)/$(TargetArchAbi)");
        targetname( "libnative_lib" );
        objdir("obj/$(ProjectName)_$(Configuration)_$(Platform)");
        kind("DynamicLibrary", "android");
        files("src/main/cpp/native-lib.cpp");
        optimize( "off" );
        symbols( "on" );
        projectScript("TestAndroidJNI.cs");

        //--[ Java ]------------------------------------------
        project("JavaApp");
        CommonSettings();

        targetdir( "bin/$(Configuration)_$(Platform)" );
        objdir("obj/$(ProjectName)_$(Configuration)_$(Platform)");
        GradleProjectDirectory();
        references( "native_lib.vcxproj", "" );
        kind("Application","gradlepackage");
        GradleVersion("4.1");
        files(
            "**/MainActivity.java",
            "build.gradle", 
            "gradlew.bat",
            "getSignInfo.bat"
        );
        GradleApkFileName( "$(Platform)\\$(Configuration)\\app-$(Platform)-$(Configuration).apk" );
    } //Main
}; //class Builder

