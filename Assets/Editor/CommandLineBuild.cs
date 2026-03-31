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
            locationPathName = "/Users/kiloverse/unitykiloverse/ios-build",
            target = BuildTarget.iOS,
            options = BuildOptions.None
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            EditorApplication.Exit(1);
        }
    }
}
