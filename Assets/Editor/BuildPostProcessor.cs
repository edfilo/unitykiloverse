#if UNITY_IOS || UNITY_STANDALONE_OSX
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

public class BuildPostProcessor
{
    [PostProcessBuild]
    public static void OnPostProcessBuild(BuildTarget target, string path)
    {
        if (target == BuildTarget.iOS)
        {
#if UNITY_IOS
            string plistPath = path + "/Info.plist";
            PlistDocument plist = new PlistDocument();
            plist.ReadFromFile(plistPath);

            PlistElementDict rootDict = plist.root;
            
            // Add Microphone Permission
            string micUsage = "We need access to the microphone to record your audio drops.";
            rootDict.SetString("NSMicrophoneUsageDescription", micUsage);
            
            // Add Location Usage if missing (since you use GPS)
            string locUsage = "We use your location to place your audio drops on the map.";
            rootDict.SetString("NSLocationWhenInUseUsageDescription", locUsage);

            File.WriteAllText(plistPath, plist.WriteToString());
            UnityEngine.Debug.Log("[BuildPostProcessor] Added NSMicrophoneUsageDescription to Info.plist");
#endif
        }
        else if (target == BuildTarget.StandaloneOSX)
        {
            // For macOS builds, the Info.plist is inside the .app bundle
            string plistPath = path + "/Contents/Info.plist";
            if (File.Exists(plistPath))
            {
                // We can't use PlistDocument for macOS easily without #if UNITY_IOS usually, 
                // but let's try manual text insertion or standard XML parsing if needed.
                // However, Unity's PlistDocument is technically in the iOS namespace, 
                // but often available if the package is installed. 
                // Simplest way for macOS is to rely on Player Settings, but we can try to inject if needed.
                
                // Note: Modifying macOS .app signature after build invalidates it, so this is risky 
                // if not re-signed. Usually best done in Player Settings.
                UnityEngine.Debug.LogWarning("[BuildPostProcessor] Remember to set Microphone Usage Description in Player Settings for macOS!");
            }
        }
    }
}
#endif
