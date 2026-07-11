using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif
using System.IO;

public class iOSPostProcessor
{
    [PostProcessBuild]
    public static void OnPostProcessBuild(BuildTarget buildTarget, string path)
    {
        if (buildTarget == BuildTarget.iOS)
        {
#if UNITY_IOS
            string plistPath = path + "/Info.plist";
            PlistDocument plist = new PlistDocument();
            plist.ReadFromFile(plistPath);

            PlistElementDict rootDict = plist.root;

            // Add Motion Usage Description (Pedometer)
            string motionKey = "NSMotionUsageDescription";
            if (rootDict[motionKey] == null)
            {
                rootDict.SetString(motionKey, "This app uses the pedometer to track your steps in the Kiloverse.");
            }

            // Add Location Usage Description (GPS) - Good practice to ensure it's here
            string locationKey = "NSLocationWhenInUseUsageDescription";
            if (rootDict[locationKey] == null)
            {
                rootDict.SetString(locationKey, "This app uses your location to place you on the map.");
            }

            string photoKey = "NSPhotoLibraryUsageDescription";
            if (rootDict[photoKey] == null)
            {
                rootDict.SetString(photoKey, "K1L0 lets you choose a photo to use in a transmission.");
            }

            string photoAddKey = "NSPhotoLibraryAddUsageDescription";
            if (rootDict[photoAddKey] == null)
            {
                rootDict.SetString(photoAddKey, "K1L0 lets you save transmissions to your camera roll.");
            }

            string cameraKey = "NSCameraUsageDescription";
            if (rootDict[cameraKey] == null)
            {
                rootDict.SetString(cameraKey, "K1L0 lets you take a photo to use in a transmission.");
            }

            // Add App Transport Security Exception (Allow HTTP)
            // Required for connecting to local dev servers (localhost/tethered)
            PlistElementDict atsDict = rootDict.CreateDict("NSAppTransportSecurity");
            atsDict.SetBoolean("NSAllowsArbitraryLoads", true);
            Debug.Log("[iOSPostProcessor] Added NSAppTransportSecurity -> NSAllowsArbitraryLoads = YES");

            // Add Local Network Usage Description (iOS 14+)
            // Required for connecting to local IPs like 172.20.10.5
            string localNetKey = "NSLocalNetworkUsageDescription";
            if (rootDict[localNetKey] == null)
            {
                rootDict.SetString(localNetKey, "Kiloverse needs to connect to a local server for development and testing.");
                Debug.Log("[iOSPostProcessor] Added NSLocalNetworkUsageDescription");
            }

            rootDict.SetBoolean("UIStatusBarHidden", false);
            rootDict.SetBoolean("UIViewControllerBasedStatusBarAppearance", false);
            rootDict.SetString("UIStatusBarStyle", "UIStatusBarStyleLightContent");
            Debug.Log("[iOSPostProcessor] Enabled visible iOS status bar");

            // Write to file
            File.WriteAllText(plistPath, plist.WriteToString());
            Debug.Log("[iOSPostProcessor] Added NSMotionUsageDescription and NSLocationWhenInUseUsageDescription to Info.plist");

            string projPath = PBXProject.GetPBXProjectPath(path);
            PBXProject proj = new PBXProject();
            proj.ReadFromFile(projPath);

            string mainTarget = proj.GetUnityMainTargetGuid();
            string frameworkTarget = proj.GetUnityFrameworkTargetGuid();

            proj.AddFrameworkToProject(frameworkTarget, "SwiftUI.framework", false);
            proj.AddFrameworkToProject(frameworkTarget, "UIKit.framework", false);
            proj.AddFrameworkToProject(frameworkTarget, "CoreMotion.framework", false);
            proj.AddFrameworkToProject(frameworkTarget, "CoreLocation.framework", false);
            proj.SetBuildProperty(frameworkTarget, "SWIFT_VERSION", "5.0");
            proj.SetBuildProperty(mainTarget, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "YES");
            proj.SetBuildProperty(frameworkTarget, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "NO");
            proj.WriteToFile(projPath);
            Debug.Log("[iOSPostProcessor] Added K1L0 SwiftUI weather overlay to UnityFramework");
#endif
        }
    }
}
