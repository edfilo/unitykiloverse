#if UNITY_IOS
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using System.IO;

/// <summary>
/// Unity's PluginImporter doesn't treat .metal files under Assets/Plugins/iOS
/// as iOS plugins (they get a DefaultImporter and are skipped at export).
/// Copy them into the exported project and add them to both native targets.
/// The Swift overlay lives in UnityFramework while some native views are hosted by
/// the main app, so either bundle may be asked to resolve the hologram metallib.
/// </summary>
public class MetalPluginPostBuild
{
    [PostProcessBuild(2)]
    public static void OnPostProcessBuild(BuildTarget buildTarget, string path)
    {
        if (buildTarget != BuildTarget.iOS) return;

        string[] metalFiles = Directory.GetFiles("Assets/Plugins/iOS", "*.metal", SearchOption.TopDirectoryOnly);
        if (metalFiles.Length == 0) return;

        string projPath = PBXProject.GetPBXProjectPath(path);
        PBXProject proj = new PBXProject();
        proj.ReadFromFile(projPath);

        string appGuid = proj.GetUnityMainTargetGuid();
        string appSourcesGuid = proj.GetSourcesBuildPhaseByTarget(appGuid);
        string frameworkGuid = proj.GetUnityFrameworkTargetGuid();
        string frameworkSourcesGuid = proj.GetSourcesBuildPhaseByTarget(frameworkGuid);

        foreach (string src in metalFiles)
        {
            string fileName = Path.GetFileName(src);
            string destRel = "Libraries/Plugins/iOS/" + fileName;
            string destAbs = Path.Combine(path, destRel);
            Directory.CreateDirectory(Path.GetDirectoryName(destAbs));
            File.Copy(src, destAbs, true);

            string fileGuid = proj.AddFile(destRel, destRel, PBXSourceTree.Source);
            proj.AddFileToBuildSection(appGuid, appSourcesGuid, fileGuid);
            proj.AddFileToBuildSection(frameworkGuid, frameworkSourcesGuid, fileGuid);
            UnityEngine.Debug.Log($"[MetalPluginPostBuild] Added {fileName} to app and UnityFramework Metal sources.");
        }

        proj.WriteToFile(projPath);
    }
}
#endif
