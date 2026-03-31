using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.Linq;

public class HeadlessBuilder
{
    public static void Build()
    {
        string[] scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            Debug.LogError("No scenes enabled in EditorBuildSettings.");
            EditorApplication.Exit(1);
            return;
        }

        string buildPath = "Builds/Headless/kiloverse_server";
        BuildTarget target = BuildTarget.StandaloneLinux64; // Default to Linux 64-bit

        // Parse command line arguments for target and path
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-customBuildPath" && i + 1 < args.Length)
            {
                buildPath = args[i + 1];
            }
            if (args[i] == "-customBuildTarget" && i + 1 < args.Length)
            {
                if (args[i + 1] == "OSX") target = BuildTarget.StandaloneOSX;
                if (args[i + 1] == "Linux") target = BuildTarget.StandaloneLinux64;
                if (args[i + 1] == "Win64") target = BuildTarget.StandaloneWindows64;
            }
        }

        Debug.Log($"Starting headless build for {target} to {buildPath}...");

        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
        buildPlayerOptions.scenes = scenes;
        buildPlayerOptions.locationPathName = buildPath;
        buildPlayerOptions.target = target;
        
        // Use Dedicated Server subtarget for Unity 6 if possible
        // In Unity 6, this is done via standaloneBuildSubtarget
        buildPlayerOptions.subtarget = (int)StandaloneBuildSubtarget.Server;
        
        buildPlayerOptions.options = BuildOptions.EnableHeadlessMode | BuildOptions.Development;

        BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log("Build succeeded: " + summary.totalSize + " bytes");
            EditorApplication.Exit(0);
        }

        if (summary.result == BuildResult.Failed)
        {
            Debug.Log("Build failed");
            EditorApplication.Exit(1);
        }
    }

    public static void BuildIOS()
    {
        string[] scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            Debug.LogError("No scenes enabled in EditorBuildSettings.");
            EditorApplication.Exit(1);
            return;
        }

        string buildPath = "Builds/iOS";
        
        // Parse command line arguments for path
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-customBuildPath" && i + 1 < args.Length)
            {
                buildPath = args[i + 1];
            }
        }

        Debug.Log($"Starting iOS build to {buildPath}...");

        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
        buildPlayerOptions.scenes = scenes;
        buildPlayerOptions.locationPathName = buildPath;
        buildPlayerOptions.target = BuildTarget.iOS;
        buildPlayerOptions.options = BuildOptions.None; // Standard iOS build (Xcode project)

        BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log("iOS Build Succeeded (Xcode Project generated at: " + buildPath + ")");
            EditorApplication.Exit(0);
        }

        if (summary.result == BuildResult.Failed)
        {
            Debug.LogError("iOS Build Failed");
            EditorApplication.Exit(1);
        }
    }
}
