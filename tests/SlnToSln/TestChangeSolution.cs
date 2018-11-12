//css_ref ..\..\syncproj.exe
using System;
using System.Linq;

class Builder: SolutionProjectBuilder
{
    static void Main(String[] args)
    {
        Solution s = Solution.LoadSolution("_consApp1.sln");

        s.RemoveEmptyFolders();
        s.ChangeProjectDependenciesFromGuidsToNames();

        Solution stests = s.Clone();

        s.EnableProjectBuild((Project x) => { return !x.ProjectName.ToLower().Contains("classlib1"); });
        UpdateInfo info = new UpdateInfo();
        s.SaveSolution(info, "out_consApp_classlib1_off_rest_on.sln");

        foreach (var p in s.projects)
        {
            if (p.ProjectDependencies == null) continue;
            Console.WriteLine(p.ProjectName + " depends on:");
            foreach (var n in p.ProjectDependencies)
                Console.WriteLine("  -" + n);
        }
        Console.WriteLine();

        try
        {
            s.ChangeProjectDependenciesFromGuidsToNames();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Exception: " + ex.Message);
        }

        stests.EnableProjectBuild((Project x) => { return x.ProjectName.ToLower().Contains("classlib1"); });
        stests.SaveSolution(info, "out_consApp_classlib1_on_rest_off.sln");
        info.DisplaySummary();

        bSaveGeneratedProjects = false;
    }
};

