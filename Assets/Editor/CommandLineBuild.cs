using UnityEditor;
using UnityEditor.Build.Reporting;
using System.Linq;

public class CommandLineBuild
{
    public static void BuildiOS()
    {
        var scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = "/Users/kiloverse/unitykiloverse/Builds/iOS",
            target = BuildTarget.iOS,
            options = BuildOptions.None
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            EditorApplication.Exit(1);
        }
    }

    public static void BuildMac()
    {
        // Force asset pipeline + script compile refresh — batchmode with -quit sometimes
        // skips detecting edited .cs files and ships a stale assembly.
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();

        var scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = "/Users/kiloverse/unitykiloverse/Builds/Mac/K1L0.app",
            target = BuildTarget.StandaloneOSX,
            options = BuildOptions.None
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            EditorApplication.Exit(1);
        }
    }
}
