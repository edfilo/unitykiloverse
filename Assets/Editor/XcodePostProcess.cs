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

        // Get bundle identifier from PlayerSettings
        string bundleIdentifier = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.iOS);
        Debug.Log($"[XcodePostProcess] Bundle ID: {bundleIdentifier}");

        // Path to entitlements file
        string entitlementsPath = pathToBuiltProject + "/Unity-iPhone.entitlements";

        // Create proper entitlements file with required fields
        PlistDocument entitlements = new PlistDocument();
        PlistElementDict rootDict = entitlements.root;

        // CRITICAL: Add application-identifier (required by iOS)
        // Format: TEAM_ID.BUNDLE_ID (e.g., "ABC123DEF4.com.kilomeme.kiloverse")
        // Unity should fill this automatically, but we ensure it exists
        string teamID = PlayerSettings.iOS.appleDeveloperTeamID;
        if (!string.IsNullOrEmpty(teamID))
        {
            rootDict.SetString("application-identifier", $"{teamID}.{bundleIdentifier}");
            Debug.Log($"[XcodePostProcess] ✓ Set application-identifier: {teamID}.{bundleIdentifier}");
        }
        else
        {
            Debug.LogWarning("[XcodePostProcess] ⚠ Team ID not set in Player Settings! Set it in Build Settings → iOS → Other Settings → Identification → Apple Developer Team ID");
        }

        // Add location permissions (if using GPS)
        rootDict.SetBoolean("com.apple.developer.location.push", true);
        Debug.Log("[XcodePostProcess] ✓ Added location push capability");

        // Save entitlements file
        entitlements.WriteToFile(entitlementsPath);
        Debug.Log($"[XcodePostProcess] ✓ Created entitlements file: {entitlementsPath}");

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
