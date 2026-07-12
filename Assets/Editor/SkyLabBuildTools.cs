using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SkyLabBuildTools
{
    public const string ScenePath = "Assets/Scenes/K1L0SkyLab.unity";

    [MenuItem("K1L0/Sky Lab/Create Lightweight Scene")]
    public static void CreateScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var cameraObject = new GameObject("Sky Lab Camera");
        cameraObject.tag = "MainCamera";
        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.fieldOfView = 60f;
        camera.nearClipPlane = .1f;
        camera.farClipPlane = 1000f;
        cameraObject.AddComponent<AudioListener>();
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log($"[SkyLab] Lightweight scene ready: {ScenePath}");
    }

    public static void BuildiOS()
    {
        if (!System.IO.File.Exists(ScenePath)) CreateScene();
        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = "/Users/kiloverse/unitykiloverse/Builds/SkyLab-iOS",
            target = BuildTarget.iOS,
            options = BuildOptions.Development
        };
        var report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            EditorApplication.Exit(1);
    }

    [MenuItem("K1L0/Build/Production iOS Export")]
    public static void BuildProductioniOS()
    {
        CommandLineBuild.BuildiOS();
    }
}
