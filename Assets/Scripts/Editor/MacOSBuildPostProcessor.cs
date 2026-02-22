#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using System.IO;

public class MacOSBuildPostProcessor
{
    [PostProcessBuild]
    public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
    {
        if (target == BuildTarget.StandaloneOSX)
        {
            Debug.Log($"[MacOSBuildPostProcessor] Processing build at: {pathToBuiltProject}");

            string plistPath = pathToBuiltProject + "/Contents/Info.plist";
            if (File.Exists(plistPath))
            {
                Debug.Log($"[MacOSBuildPostProcessor] Found Info.plist at: {plistPath}");
                
                // Read current plist
                string plistContent = File.ReadAllText(plistPath);

                // Check if key already exists
                if (!plistContent.Contains("NSLocationUsageDescription"))
                {
                    // Basic XML injection - simpler than parsing full Plist for this one key
                    // Find the last </dict> and insert before it
                    int insertionPoint = plistContent.LastIndexOf("</dict>");
                    if (insertionPoint > 0)
                    {
                        string newKey = "\n\t<key>NSLocationUsageDescription</key>\n\t<string>Kiloverse needs your location to show nearby transmitters and track steps.</string>\n";
                        
                        // Add ATS for HTTP
                        newKey += "\n\t<key>NSAppTransportSecurity</key>\n\t<dict>\n\t\t<key>NSAllowsArbitraryLoads</key>\n\t\t<true/>\n\t</dict>\n";

                        string newContent = plistContent.Insert(insertionPoint, newKey);
                        File.WriteAllText(plistPath, newContent);
                        Debug.Log("[MacOSBuildPostProcessor] Injected NSLocationUsageDescription and NSAppTransportSecurity into Info.plist");
                    }
                    else
                    {
                        Debug.LogError("[MacOSBuildPostProcessor] Failed to find insertion point in Info.plist");
                    }
                }
                else
                {
                    Debug.Log("[MacOSBuildPostProcessor] NSLocationUsageDescription already exists.");
                }
            }
            else
            {
                // Sometimes path ends in .app, sometimes it's the folder containing it depending on Unity version?
                // pathToBuiltProject usually is "Path/To/App.app"
                Debug.LogWarning($"[MacOSBuildPostProcessor] Info.plist not found at {plistPath}. If this is not a .app bundle, this is expected.");
            }
        }
    }
}
#endif
