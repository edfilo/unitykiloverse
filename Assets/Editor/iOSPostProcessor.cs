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
#endif
        }
    }
}
