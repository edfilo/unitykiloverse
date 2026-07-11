using UnityEngine;
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
using System;
using System.Collections;
using System.Runtime.InteropServices;
using AOT;
#endif

// Installs the native Swift weather/HUD overlay on the macOS standalone player.
//
// This is the Mac counterpart to Assets/Plugins/iOS/K1L0WeatherOverlayBootstrap.mm,
// which auto-installs the overlay on iOS via a UIApplicationDidBecomeActive observer.
// The macOS player has no such hook, so we drive K1L0InstallWeatherOverlay() from
// managed code once the scene is up.
//
// Bridge note: iOS routes Swift -> Unity through the player's exported UnitySendMessage
// symbol. The macOS standalone player does NOT export that symbol, so instead we hand
// the overlay a callback function pointer (K1L0SetUnityCallback); the Swift shim forwards
// every UnitySendMessage(...) call to it, and we re-dispatch it as GameObject.SendMessage
// — functionally identical to UnitySendMessage. The Swift install() retries until it finds
// the player window, so timing is forgiving; we re-poke it to match the iOS cadence.
//
// The overlay binary ships as K1L0Overlay.bundle in <K1L0.app>/Contents/Plugins,
// built from the SAME Assets/Plugins/iOS/K1L0WeatherOverlay.swift used by the iOS app.
public static class K1L0NativeOverlay
{
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
    private delegate void UnityMessageDelegate(IntPtr objectName, IntPtr method, IntPtr message);

    [DllImport("K1L0Overlay")]
    private static extern void K1L0InstallWeatherOverlay();

    [DllImport("K1L0Overlay")]
    private static extern void K1L0SetUnityCallback(UnityMessageDelegate callback);

    // Held in a static field so the GC never collects the delegate the native side calls.
    private static readonly UnityMessageDelegate s_callback = OnUnityMessage;
    private static bool s_callbackRegistered;
    private static bool s_bootstrapped;

    [MonoPInvokeCallback(typeof(UnityMessageDelegate))]
    private static void OnUnityMessage(IntPtr objectNamePtr, IntPtr methodPtr, IntPtr messagePtr)
    {
        try
        {
            string objectName = Marshal.PtrToStringUTF8(objectNamePtr);
            string method = Marshal.PtrToStringUTF8(methodPtr);
            string message = Marshal.PtrToStringUTF8(messagePtr) ?? "";
            if (string.IsNullOrEmpty(objectName) || string.IsNullOrEmpty(method)) return;

            // The overlay marshals on its main thread, which is the Unity main thread on
            // macOS standalone, so calling SendMessage here is safe.
            Debug.Log($"[K1L0NativeOverlay] msg {objectName}.{method}(\"{message}\")");
            var target = GameObject.Find(objectName);
            if (target != null)
                target.SendMessage(method, message, SendMessageOptions.DontRequireReceiver);
            else
                Debug.LogWarning($"[K1L0NativeOverlay] no GameObject named '{objectName}'");
        }
        catch (Exception e)
        {
            Debug.LogWarning("[K1L0NativeOverlay] callback failed: " + e.Message);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (s_bootstrapped) return;
        s_bootstrapped = true;
        Debug.Log("[K1L0NativeOverlay] bootstrap");
        var go = new GameObject("K1L0NativeOverlayBootstrap");
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideAndDontSave;
        go.AddComponent<K1L0NativeOverlayBootstrap>();
        Install();
    }
#endif

    public static void EnsureInstalled()
    {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        Bootstrap();
#endif
    }

#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
    internal static void Install()
    {
        try
        {
            if (!s_callbackRegistered)
            {
                K1L0SetUnityCallback(s_callback);
                s_callbackRegistered = true;
            }
            K1L0InstallWeatherOverlay();
            Debug.Log("[K1L0NativeOverlay] install requested");
        }
        catch (Exception e)
        {
            Debug.LogWarning("[K1L0NativeOverlay] install failed: " + e.Message);
        }
    }
#endif
}

#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
internal sealed class K1L0NativeOverlayBootstrap : MonoBehaviour
{
    private IEnumerator Start()
    {
        // Mirror the iOS 0.8s post-activate delay, then re-poke to keep the overlay
        // pinned above Unity's content view as the window settles.
        foreach (var delay in new[] { 0.8f, 1.6f, 3.0f })
        {
            yield return new WaitForSeconds(delay);
            K1L0NativeOverlay.Install();
        }
    }
}
#endif
