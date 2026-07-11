#if UNITY_IOS
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using System.IO;

public class IOSPostBuild
{
    [PostProcessBuild]
    public static void OnPostProcessBuild(BuildTarget buildTarget, string path)
    {
        if (buildTarget != BuildTarget.iOS) return;

        string plistPath = path + "/Info.plist";
        PlistDocument plist = new PlistDocument();
        plist.ReadFromFile(plistPath);

        PlistElementDict rootDict = plist.root;

        // Pedometer Permission
        SetPlistKey(rootDict, "NSMotionUsageDescription", "We use motion data to count your steps and fuel your beams.");

        // Location Permission
        SetPlistKey(rootDict, "NSLocationWhenInUseUsageDescription", "We need your location to show you on the map and find nearby beams.");
        SetPlistKey(rootDict, "NSLocationAlwaysAndWhenInUseUsageDescription", "We need your location to show you on the map and find nearby beams.");
        SetPlistKey(rootDict, "NSPhotoLibraryUsageDescription", "K1L0 lets you choose a photo to use in a transmission.");
        SetPlistKey(rootDict, "NSPhotoLibraryAddUsageDescription", "K1L0 lets you save transmissions to your camera roll.");
        SetPlistKey(rootDict, "NSCameraUsageDescription", "K1L0 lets you take a photo to use in a transmission.");

        // Write to file
        File.WriteAllText(plistPath, plist.WriteToString());

        // Add Capabilities
        string projPath = PBXProject.GetPBXProjectPath(path);
        PBXProject proj = new PBXProject();
        proj.ReadFromFile(projPath);

        string targetGuid = proj.GetUnityMainTargetGuid(); // Main app target

        // Unity has no PluginImporter for .metal, so the tuning shader never
        // reaches the generated project on its own. Copy it in and compile it
        // with UnityFramework — the same target that compiles the plugin Swift,
        // so ShaderLibrary.bundle(Bundle(for:)) finds it in that metallib.
        string metalSrc = Path.Combine(UnityEngine.Application.dataPath, "Plugins/iOS/K1L0TuningShader.metal");
        const string metalRel = "Libraries/Plugins/iOS/K1L0TuningShader.metal";
        if (File.Exists(metalSrc))
        {
            string metalDst = Path.Combine(path, metalRel);
            Directory.CreateDirectory(Path.GetDirectoryName(metalDst));
            File.Copy(metalSrc, metalDst, true);
            string metalGuid = proj.ContainsFileByProjectPath(metalRel)
                ? proj.FindFileGuidByProjectPath(metalRel)
                : proj.AddFile(metalRel, metalRel, PBXSourceTree.Source);
            proj.AddFileToBuild(proj.GetUnityFrameworkTargetGuid(), metalGuid);
            proj.WriteToFile(projPath);
        }

        string entitlementsPath = "Unity-iPhone.entitlements";
        
        var capManager = new ProjectCapabilityManager(projPath, entitlementsPath, "Unity-iPhone");
        
        // Add Sign In With Apple
        capManager.AddSignInWithApple();
        
        // Add Push Notifications (Optional, common with Firebase)
        // capManager.AddPushNotifications(true);
        
        capManager.WriteToFile();
    }

    private static void SetPlistKey(PlistElementDict root, string key, string value)
    {
        if (root[key] == null)
        {
            root.SetString(key, value);
        }
    }
}
#endif
