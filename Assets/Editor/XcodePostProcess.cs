using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using System.IO;

/// <summary>
/// Automatically creates proper entitlements file after every Unity build.
/// This prevents both "modified during build" error AND missing application-identifier error.
/// </summary>
public class XcodePostProcess
{
    [PostProcessBuild(1)]
    public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
    {
        if (target != BuildTarget.iOS)
            return;

        Debug.Log("[XcodePostProcess] Applying post-build fixes...");

        // Path to entitlements file
        string entitlementsPath = pathToBuiltProject + "/Unity-iPhone.entitlements";

        PlistDocument entitlements = new PlistDocument();
        if (File.Exists(entitlementsPath))
        {
            entitlements.ReadFromFile(entitlementsPath);
        }
        else
        {
            entitlements.Create();
        }

        PlistElementDict rootDict = entitlements.root;
        rootDict.values.Remove("application-identifier");
        rootDict.values.Remove("com.apple.developer.location.push");
        var appleSignIn = rootDict.values.ContainsKey("com.apple.developer.applesignin")
            ? rootDict["com.apple.developer.applesignin"].AsArray()
            : rootDict.CreateArray("com.apple.developer.applesignin");
        bool hasDefault = false;
        foreach (var value in appleSignIn.values)
        {
            if (value.AsString() == "Default")
            {
                hasDefault = true;
                break;
            }
        }
        if (!hasDefault) appleSignIn.AddString("Default");

        // Save entitlements file
        entitlements.WriteToFile(entitlementsPath);
        Debug.Log($"[XcodePostProcess] ✓ Preserved Sign in with Apple entitlement: {entitlementsPath}");

        // Verify Info.plist has location permissions
        string plistPath = pathToBuiltProject + "/Info.plist";
        PlistDocument plist = new PlistDocument();
        plist.ReadFromFile(plistPath);

        var plistRoot = plist.root;

        if (!plistRoot.values.ContainsKey("NSLocationWhenInUseUsageDescription"))
        {
            plistRoot.SetString("NSLocationWhenInUseUsageDescription", "We use your location to place your audio drops on the map.");
            Debug.Log("[XcodePostProcess] ⚠ Added missing NSLocationWhenInUseUsageDescription");
        }

        if (!plistRoot.values.ContainsKey("NSLocationAlwaysAndWhenInUseUsageDescription"))
        {
            plistRoot.SetString("NSLocationAlwaysAndWhenInUseUsageDescription", "We need your location to show you on the map and find nearby beams.");
            Debug.Log("[XcodePostProcess] ⚠ Added missing NSLocationAlwaysAndWhenInUseUsageDescription");
        }

        plist.WriteToFile(plistPath);

        Debug.Log("[XcodePostProcess] ✓ Post-build fixes applied successfully!");
    }
}
