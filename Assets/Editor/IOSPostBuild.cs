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
        string frameworkGuid = proj.GetUnityFrameworkTargetGuid();

        // Native Swift owns the live RTDB chain observer. Keep Firebase as an
        // explicit Swift Package dependency so every Unity export preserves it.
        string firebasePackage = proj.AddRemotePackageReferenceAtVersionUpToNextMajor(
            "https://github.com/firebase/firebase-ios-sdk.git", "12.0.0");
        proj.AddRemotePackageFrameworkToProject(frameworkGuid, "FirebaseCore", firebasePackage, false);
        proj.AddRemotePackageFrameworkToProject(frameworkGuid, "FirebaseDatabase", firebasePackage, false);

        string firebasePlistSource = "Assets/Plugins/iOS/GoogleService-Info.plist";
        if (File.Exists(firebasePlistSource))
        {
            string firebasePlistName = "GoogleService-Info.plist";
            File.Copy(firebasePlistSource, Path.Combine(path, firebasePlistName), true);
            string firebasePlistGuid = proj.AddFile(firebasePlistName, firebasePlistName, PBXSourceTree.Source);
            proj.AddFileToBuild(targetGuid, firebasePlistGuid);
        }

        // MetalPluginPostBuild owns native Metal target membership. Keep this
        // postprocessor focused on plist/capability configuration.
        proj.WriteToFile(projPath);

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
