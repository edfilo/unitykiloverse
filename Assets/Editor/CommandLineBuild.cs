using UnityEditor;
using UnityEditor.Build.Reporting;
using System.Linq;

public class CommandLineBuild
{
    // Built-in skybox shaders are created at runtime via Shader.Find for the dynamic sky.
    // Unity strips built-in shaders that aren't referenced by an asset, so Shader.Find
    // returns null in player builds — force them into the build here.
    static void EnsureAlwaysIncludedShaders(params string[] names)
    {
        var objs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
        if (objs == null || objs.Length == 0) { UnityEngine.Debug.LogWarning("[Build] GraphicsSettings.asset not found"); return; }
        var so = new SerializedObject(objs[0]);
        var arr = so.FindProperty("m_AlwaysIncludedShaders");
        if (arr == null) { UnityEngine.Debug.LogWarning("[Build] m_AlwaysIncludedShaders not found"); return; }
        foreach (var n in names)
        {
            var sh = UnityEngine.Shader.Find(n);
            if (sh == null) { UnityEngine.Debug.LogWarning($"[Build] Shader not found: {n}"); continue; }
            bool present = false;
            for (int i = 0; i < arr.arraySize; i++)
                if (arr.GetArrayElementAtIndex(i).objectReferenceValue == sh) { present = true; break; }
            if (present) continue;
            int idx = arr.arraySize;
            arr.InsertArrayElementAtIndex(idx);
            arr.GetArrayElementAtIndex(idx).objectReferenceValue = sh;
            UnityEngine.Debug.Log($"[Build] ✓ Always-include shader: {n}");
        }
        so.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
    }

    public static void BuildiOS()
    {
        // K1L0 uses a local HTTP endpoint on LAN (and falls back to HTTPS tunnel/production).
        // iOS builds must allow insecure HTTP for UnityWebRequest, otherwise boot will throw:
        // "Non-secure network connections disabled in Player Settings" / "Insecure connection not allowed".
        PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
        EnsureAlwaysIncludedShaders("Skybox/Procedural", "Skybox/Cubemap", "Universal Render Pipeline/Unlit", "Universal Render Pipeline/Particles/Unlit");

        // Manual code signing for device builds. Scoping the team/profile to the Unity
        // targets here (rather than passing them globally to xcodebuild) keeps the signing
        // settings off the SwiftPM/Firebase package targets, which reject provisioning
        // profiles. Team 7R2746UPX7 with the local "com.filowatt.k1lo" development profile.
        PlayerSettings.iOS.appleEnableAutomaticSigning = false;
        PlayerSettings.iOS.appleDeveloperTeamID = "7R2746UPX7";
        PlayerSettings.iOS.iOSManualProvisioningProfileType = ProvisioningProfileType.Development;
        PlayerSettings.iOS.iOSManualProvisioningProfileID = "67f9ab53-0e20-4359-86f4-41dc044a69e0";

        var scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        const string iosOut = "/Users/kiloverse/unitykiloverse/Builds/iOS";
        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = iosOut,
            target = BuildTarget.iOS,
            options = BuildOptions.None
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            EditorApplication.Exit(1);
        }

        // Bake the >K1L0 icon into the exported AppIcon.appiconset. Unity's
        // Standalone+iOS targets don't honor our PlayerSettings icons reliably,
        // so we resize Assets/Icons/KiloverseAppIcon_K.png into every Icon-*.png
        // slot via sips. Source of truth: the same PNG used by GenerateMacAppIcon.
        try { GenerateIosAppIcons(iosOut); }
        catch (System.Exception e) { UnityEngine.Debug.LogWarning($"[BuildiOS] Icon bake failed: {e.Message}"); }
    }

    static void GenerateIosAppIcons(string iosBuildPath)
    {
        const string sourcePng = "Assets/Icons/KiloverseAppIcon_K.png";
        string sourceAbs = System.IO.Path.GetFullPath(sourcePng);
        if (!System.IO.File.Exists(sourceAbs))
        {
            UnityEngine.Debug.LogWarning($"[BuildiOS] Icon source missing: {sourceAbs}");
            return;
        }
        string iconset = System.IO.Path.Combine(iosBuildPath, "Unity-iPhone/Images.xcassets/AppIcon.appiconset");
        if (!System.IO.Directory.Exists(iconset))
        {
            UnityEngine.Debug.LogWarning($"[BuildiOS] AppIcon.appiconset not found at {iconset}");
            return;
        }

        // (size, filename) — every Icon-*.png slot Unity emits for iOS.
        var slots = new (int, string)[] {
            (1024, "Icon-Store-1024.png"),
            (152,  "Icon-iPad-152.png"),
            (167,  "Icon-iPad-167.png"),
            (76,   "Icon-iPad-76.png"),
            (20,   "Icon-iPad-Notification-20.png"),
            (40,   "Icon-iPad-Notification-40.png"),
            (29,   "Icon-iPad-Settings-29.png"),
            (58,   "Icon-iPad-Settings-58.png"),
            (40,   "Icon-iPad-Spotlight-40.png"),
            (80,   "Icon-iPad-Spotlight-80.png"),
            (120,  "Icon-iPhone-120.png"),
            (180,  "Icon-iPhone-180.png"),
            (40,   "Icon-iPhone-Notification-40.png"),
            (60,   "Icon-iPhone-Notification-60.png"),
            (29,   "Icon-iPhone-Settings-29.png"),
            (58,   "Icon-iPhone-Settings-58.png"),
            (87,   "Icon-iPhone-Settings-87.png"),
            (120,  "Icon-iPhone-Spotlight-120.png"),
            (80,   "Icon-iPhone-Spotlight-80.png"),
        };
        foreach (var (sz, name) in slots)
        {
            string dest = System.IO.Path.Combine(iconset, name);
            RunCmd("/usr/bin/sips", $"-z {sz} {sz} \"{sourceAbs}\" --out \"{dest}\"");
        }
        UnityEngine.Debug.Log($"[BuildiOS] ✓ Baked {slots.Length} icon slots into {iconset}");
    }

    public static void BuildMac()
    {
        EnsureAlwaysIncludedShaders("Skybox/Procedural", "Skybox/Cubemap", "Universal Render Pipeline/Unlit", "Universal Render Pipeline/Particles/Unlit");
        // Force asset pipeline + script compile refresh — batchmode with -quit sometimes
        // skips detecting edited .cs files and ships a stale assembly.
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        // NOTE: RequestScriptCompilation() can deadlock in batchmode in some Unity versions.
        // BuildPipeline.BuildPlayer will trigger compilation as needed.

        var scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        const string appPath = "/Users/kiloverse/unitykiloverse/Builds/Mac/K1L0.app";
        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = appPath,
            target = BuildTarget.StandaloneOSX,
            options = BuildOptions.None
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            EditorApplication.Exit(1);
        }

        // Bake the >K1L0 icon into the bundle. Unity's Standalone target doesn't
        // honor the iPhone-tagged BuildTargetPlatformIcons, so the .app ships
        // without a PlayerIcon.icns by default. We generate one from the source
        // PNG via iconutil and drop it into Contents/Resources/.
        try { GenerateMacAppIcon(appPath); }
        catch (System.Exception e) { UnityEngine.Debug.LogWarning($"[BuildMac] Icon copy failed: {e.Message}"); }

        // Speech-to-text on macOS requires this usage description for the permission prompt.
        try { EnsureMacSpeechUsageDescription(appPath); }
        catch (System.Exception e) { UnityEngine.Debug.LogWarning($"[BuildMac] Plist permission update failed: {e.Message}"); }

        // Updating Info.plist invalidates the bundle signature; re-sign so macOS can launch it.
        try { ResignMacApp(appPath); }
        catch (System.Exception e) { UnityEngine.Debug.LogWarning($"[BuildMac] Re-sign failed: {e.Message}"); }

        // Close + relaunch after every successful build so the running app never
        // lags behind the latest build output.
        try { RelaunchMacApp(appPath); }
        catch (System.Exception e) { UnityEngine.Debug.LogWarning($"[BuildMac] Relaunch failed: {e.Message}"); }
    }

    static void EnsureMacSpeechUsageDescription(string appPath)
    {
        string plist = System.IO.Path.Combine(appPath, "Contents/Info.plist");
        if (!System.IO.File.Exists(plist)) return;

        const string key = "NSSpeechRecognitionUsageDescription";
        const string value = "K1L0 uses speech recognition to transcribe your voice at music transmitters.";

        // Set if exists, else add.
        RunCmdAllowFail("/usr/libexec/PlistBuddy", $"-c \"Set :{key} \\\"{value}\\\"\" \"{plist}\"");
        RunCmdAllowFail("/usr/libexec/PlistBuddy", $"-c \"Add :{key} string \\\"{value}\\\"\" \"{plist}\"");
    }

    static void ResignMacApp(string appPath)
    {
        // Prefer the installed Apple Development identity so Gatekeeper accepts the build.
        // Fallback to ad-hoc if no identity is available.
        string identity = "640064C92384F0D2AF983CECF45421DFCE72882B";
        try
        {
            RunCmd("/usr/bin/codesign", $"--force --deep --sign {identity} \"{appPath}\"");
        }
        catch
        {
            RunCmd("/usr/bin/codesign", $"--force --deep --sign - \"{appPath}\"");
        }
    }

    static void RelaunchMacApp(string appPath)
    {
        // Best-effort: pkill returns non-zero if the app isn't running.
        RunCmdAllowFail("/usr/bin/pkill", "-x K1L0");
        RunCmd("/usr/bin/open", $"\"{appPath}\"");
        UnityEngine.Debug.Log($"[BuildMac] ✓ Relaunched {appPath}");
    }

    static void GenerateMacAppIcon(string appPath)
    {
        const string sourcePng = "Assets/Icons/KiloverseAppIcon_K.png";
        string sourceAbs = System.IO.Path.GetFullPath(sourcePng);
        if (!System.IO.File.Exists(sourceAbs))
        {
            UnityEngine.Debug.LogWarning($"[BuildMac] Icon source missing: {sourceAbs}");
            return;
        }

        string iconsetDir = "/tmp/k1l0_iconset.iconset";
        if (System.IO.Directory.Exists(iconsetDir)) System.IO.Directory.Delete(iconsetDir, true);
        System.IO.Directory.CreateDirectory(iconsetDir);

        // (size, filename) — iconutil expects this exact set.
        var variants = new (int, string)[] {
            (16,  "icon_16x16.png"),
            (32,  "icon_16x16@2x.png"),
            (32,  "icon_32x32.png"),
            (64,  "icon_32x32@2x.png"),
            (128, "icon_128x128.png"),
            (256, "icon_128x128@2x.png"),
            (256, "icon_256x256.png"),
            (512, "icon_256x256@2x.png"),
            (512, "icon_512x512.png"),
            (1024,"icon_512x512@2x.png"),
        };
        foreach (var (sz, name) in variants)
        {
            RunCmd("/usr/bin/sips", $"-z {sz} {sz} \"{sourceAbs}\" --out \"{iconsetDir}/{name}\"");
        }

        string icnsOut = "/tmp/PlayerIcon.icns";
        if (System.IO.File.Exists(icnsOut)) System.IO.File.Delete(icnsOut);
        RunCmd("/usr/bin/iconutil", $"-c icns \"{iconsetDir}\" -o \"{icnsOut}\"");

        string dest = System.IO.Path.Combine(appPath, "Contents/Resources/PlayerIcon.icns");
        System.IO.File.Copy(icnsOut, dest, overwrite: true);
        UnityEngine.Debug.Log($"[BuildMac] ✓ Wrote {dest}");
    }

    static void RunCmd(string bin, string args)
    {
        var psi = new System.Diagnostics.ProcessStartInfo(bin, args)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        using (var p = System.Diagnostics.Process.Start(psi))
        {
            p.WaitForExit();
            if (p.ExitCode != 0)
                throw new System.Exception($"{bin} {args} exited {p.ExitCode}: {p.StandardError.ReadToEnd()}");
        }
    }

    static void RunCmdAllowFail(string bin, string args)
    {
        var psi = new System.Diagnostics.ProcessStartInfo(bin, args)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        using (var p = System.Diagnostics.Process.Start(psi))
        {
            p.WaitForExit();
        }
    }
}
