using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using System.IO;

/// <summary>
/// Post-process build script to add AuthenticationServices framework for Apple Sign-In
/// </summary>
public class AppleSignInPostProcess
{
    [PostProcessBuild(1)]
    public static void OnPostProcessBuild(BuildTarget buildTarget, string path)
    {
        if (buildTarget == BuildTarget.iOS)
        {
            Debug.Log("[AppleSignIn] Adding AuthenticationServices framework to Xcode project...");

            string projectPath = path + "/Unity-iPhone.xcodeproj/project.pbxproj";
            PBXProject pbxProject = new PBXProject();
            pbxProject.ReadFromFile(projectPath);

            // Get the target GUID
            string frameworkTargetGuid = pbxProject.GetUnityFrameworkTargetGuid();
            string mainTargetGuid = pbxProject.GetUnityMainTargetGuid();
            if (string.IsNullOrEmpty(mainTargetGuid))
            {
                mainTargetGuid = pbxProject.TargetGuidByName("Unity-iPhone");
            }

            // Add AuthenticationServices framework
            pbxProject.AddFrameworkToProject(frameworkTargetGuid, "AuthenticationServices.framework", true);

            // Ensure Sign in with Apple entitlement exists for the main target
            string entitlementsFileName = "Unity-iPhone.entitlements";
            string entitlementsPath = Path.Combine(path, entitlementsFileName);
            PlistDocument entitlements = new PlistDocument();
            if (File.Exists(entitlementsPath))
            {
                entitlements.ReadFromFile(entitlementsPath);
            }
            else
            {
                entitlements.Create();
            }

            PlistElementArray signInArray = null;
            if (entitlements.root.values.ContainsKey("com.apple.developer.applesignin"))
            {
                signInArray = entitlements.root["com.apple.developer.applesignin"].AsArray();
            }
            else
            {
                signInArray = entitlements.root.CreateArray("com.apple.developer.applesignin");
            }

            bool hasDefault = false;
            foreach (var element in signInArray.values)
            {
                if (element.AsString() == "Default")
                {
                    hasDefault = true;
                    break;
                }
            }
            if (!hasDefault)
            {
                signInArray.AddString("Default");
            }

            entitlements.WriteToFile(entitlementsPath);
            pbxProject.SetBuildProperty(mainTargetGuid, "CODE_SIGN_ENTITLEMENTS", entitlementsFileName);

            // Save the modified project
            pbxProject.WriteToFile(projectPath);

            Debug.Log("[AppleSignIn] ✓ AuthenticationServices framework and entitlements added");
        }
    }
}
