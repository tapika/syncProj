//css_ref ..\..\..\syncproj.exe
using System;

class Builder: SolutionProjectBuilder
{
    static void CommonSettings(int vsVer)
    {
        vsver(vsVer);
        configurations("Debug", "Release");
        platforms("ARM", "ARM64", "x64", "x86");
    }

    static void makeSampleProjects(int vsVer)
    {
        String vsSuffix = "_vs" + vsVer.ToString();
        //----------------------------------------------------
        solution("out_TestAndroidJNI" + vsSuffix);
        CommonSettings(vsVer);

        //--[ C++ ]-------------------------------------------
        project("out_native_lib" + vsSuffix);
        CommonSettings(vsVer);

        targetdir("bin/$(Configuration)/$(TargetArchAbi)");
        targetname("libnative_lib");
        objdir("obj/$(ProjectName)_$(Configuration)_$(Platform)");
        kind("DynamicLibrary", "android");
        files("src/main/cpp/native-lib.cpp");
        optimize("off");
        symbols("on");
        projectScript("TestAndroidPackage.cs");

        //--[ Java ]------------------------------------------
        project("out_JavaApp" + vsSuffix);
        CommonSettings(vsVer);

        targetdir("bin/$(Configuration)_$(Platform)");
        objdir("obj/$(ProjectName)_$(Configuration)_$(Platform)");
        GradleProjectDirectory();
        referencesProject("out_native_lib" + vsSuffix +  ".vcxproj", "");
        kind("Application", "gradlepackage");
        GradleVersion("4.1");
        files(
            "**/MainActivity.java",
            "build.gradle",
            "gradlew.bat",
            "getSignInfo.bat"
        );
        GradleApkFileName("$(Platform)\\$(Configuration)\\app-$(Platform)-$(Configuration).apk");
        GradleToolName("gradlew.bat");
    }



    static void Main(String[] args)
    {
        makeSampleProjects(2015);
        makeSampleProjects(2017);
    }
}; //class Builder

