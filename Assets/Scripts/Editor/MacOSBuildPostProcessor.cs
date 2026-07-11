#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using System;
using System.Diagnostics;
using System.IO;
using Debug = UnityEngine.Debug;

public class MacOSBuildPostProcessor
{
    [PostProcessBuild]
    public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
    {
        if (target != BuildTarget.StandaloneOSX) return;

        Debug.Log($"[MacOSBuildPostProcessor] Processing build at: {pathToBuiltProject}");

        string plistPath = pathToBuiltProject + "/Contents/Info.plist";
        if (File.Exists(plistPath))
        {
            string plistContent = File.ReadAllText(plistPath);
            int insertionPoint = plistContent.LastIndexOf("</dict>");
            if (insertionPoint > 0)
            {
                string inject = "";
                if (!plistContent.Contains("NSLocationUsageDescription"))
                {
                    inject += "\n\t<key>NSLocationUsageDescription</key>\n\t<string>Kiloverse needs your location to show nearby transmitters and track steps.</string>\n";
                }
                if (!plistContent.Contains("NSLocationWhenInUseUsageDescription"))
                {
                    inject += "\n\t<key>NSLocationWhenInUseUsageDescription</key>\n\t<string>Kiloverse needs your location to show nearby transmitters and track steps.</string>\n";
                }
                if (!plistContent.Contains("NSLocationAlwaysAndWhenInUseUsageDescription"))
                {
                    inject += "\n\t<key>NSLocationAlwaysAndWhenInUseUsageDescription</key>\n\t<string>Kiloverse needs your location to show nearby transmitters and track steps.</string>\n";
                }
                if (!plistContent.Contains("NSAppTransportSecurity"))
                {
                    inject += "\n\t<key>NSAppTransportSecurity</key>\n\t<dict>\n\t\t<key>NSAllowsArbitraryLoads</key>\n\t\t<true/>\n\t</dict>\n";
                }
                if (!plistContent.Contains("LSUIElement"))
                {
                    inject += "\n\t<key>LSUIElement</key>\n\t<false/>\n";
                }
                if (!string.IsNullOrEmpty(inject))
                {
                    File.WriteAllText(plistPath, plistContent.Insert(insertionPoint, inject));
                    Debug.Log("[MacOSBuildPostProcessor] Injected custom keys (NSLocationUsageDescription, NSLocationWhenInUseUsageDescription, NSLocationAlwaysAndWhenInUseUsageDescription, NSAppTransportSecurity, LSUIElement) into Info.plist");
                }
            }
        }
        else
        {
            Debug.LogWarning($"[MacOSBuildPostProcessor] Info.plist not found at {plistPath}");
        }

        BuildAndEmbedOverlay(pathToBuiltProject);

        ReSignWithEntitlements(pathToBuiltProject);
    }

    // Compile the shared Swift overlay (Assets/Plugins/iOS/K1L0WeatherOverlay.swift)
    // into K1L0Overlay.bundle and drop it into <app>/Contents/PlugIns so the macOS
    // player resolves DllImport("K1L0Overlay"). Runs before ReSignWithEntitlements so
    // the --deep codesign re-signs the embedded bundle with the dev identity.
    static void BuildAndEmbedOverlay(string appPath)
    {
        string script = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "native-mac", "build_overlay_bundle.sh"));
        if (!File.Exists(script))
        {
            Debug.LogWarning($"[MacOSBuildPostProcessor] overlay build script not found at {script}; skipping native overlay.");
            return;
        }

        string pluginsDir = Path.Combine(appPath, "Contents", "PlugIns");
        Directory.CreateDirectory(pluginsDir);

        var psi = new ProcessStartInfo("/bin/bash", $"\"{script}\" \"{pluginsDir}\"")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using (var proc = Process.Start(psi))
        {
            string stdout = proc.StandardOutput.ReadToEnd();
            string stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit();
            if (proc.ExitCode != 0)
                Debug.LogError($"[MacOSBuildPostProcessor] overlay bundle build failed ({proc.ExitCode}): {stderr}");
            else
                Debug.Log($"[MacOSBuildPostProcessor] overlay bundle embedded at {pluginsDir}/K1L0Overlay.bundle\n{stdout}");
        }
    }

    // Re-sign the .app with Sign in with Apple entitlements. Without this the
    // Apple Sign In sheet fails on Mac (iOS gets entitlements via Xcode; Unity's
    // standalone Mac build ships with an ad-hoc signature that has none).
    static void ReSignWithEntitlements(string appPath)
    {
        string entitlementsPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "BuildResources", "K1L0.entitlements"));
        if (!File.Exists(entitlementsPath))
        {
            Debug.LogWarning($"[MacOSBuildPostProcessor] Entitlements file not found at {entitlementsPath}; skipping codesign.");
            return;
        }

        // Restricted entitlements (applesignin) require an embedded provisioning
        // profile — AMFI rejects launch otherwise. The profile was generated
        // once via a stub Xcode project and checked into BuildResources.
        string profileSrc = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "BuildResources", "K1L0.provisionprofile"));
        if (File.Exists(profileSrc))
        {
            string profileDst = Path.Combine(appPath, "Contents", "embedded.provisionprofile");
            File.Copy(profileSrc, profileDst, overwrite: true);
            Debug.Log($"[MacOSBuildPostProcessor] Embedded provisioning profile at {profileDst}");
        }
        else
        {
            Debug.LogWarning($"[MacOSBuildPostProcessor] No provisioning profile at {profileSrc}; Sign in with Apple may fail.");
        }

        string identity = Environment.GetEnvironmentVariable("MAC_SIGN_IDENTITY");
        if (string.IsNullOrEmpty(identity))
        {
            identity = FindFirstAppleDevelopmentIdentity();
        }
        if (string.IsNullOrEmpty(identity))
        {
            Debug.LogWarning("[MacOSBuildPostProcessor] No Apple Development identity found; skipping codesign. Set MAC_SIGN_IDENTITY env var to override.");
            return;
        }

        // --deep re-signs nested dylibs (UnityPlayer.dylib etc.) with the same
        // team ID as the outer bundle; without it dyld refuses to load them.
        // No --options runtime: hardened runtime breaks Mono BoehmGC thread
        // suspension signals, causing an infinite _sigtramp loop on startup.
        // Only re-enable for notarization builds, with the JIT/unsigned-mem
        // entitlements Mono requires.
        string args = $"--force --deep --entitlements \"{entitlementsPath}\" --sign \"{identity}\" \"{appPath}\"";
        Debug.Log($"[MacOSBuildPostProcessor] codesign {args}");

        var psi = new ProcessStartInfo("codesign", args)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using (var proc = Process.Start(psi))
        {
            string stdout = proc.StandardOutput.ReadToEnd();
            string stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit();
            if (proc.ExitCode != 0)
                Debug.LogError($"[MacOSBuildPostProcessor] codesign failed ({proc.ExitCode}): {stderr}");
            else
                Debug.Log($"[MacOSBuildPostProcessor] codesign OK. {stdout}{stderr}");
        }
    }

    static string FindFirstAppleDevelopmentIdentity()
    {
        var psi = new ProcessStartInfo("security", "find-identity -v -p codesigning")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using (var proc = Process.Start(psi))
        {
            string stdout = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();
            foreach (var line in stdout.Split('\n'))
            {
                int quote = line.IndexOf('"');
                int endQuote = line.LastIndexOf('"');
                if (quote >= 0 && endQuote > quote)
                {
                    string name = line.Substring(quote + 1, endQuote - quote - 1);
                    if (name.StartsWith("Apple Development") || name.StartsWith("Developer ID Application"))
                        return name;
                }
            }
        }
        return null;
    }
}
#endif
