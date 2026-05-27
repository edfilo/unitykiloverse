using System.Collections;
using UnityEngine;
#if !UNITY_WEBGL
using System.IO;
using System;
#endif
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Boot is coroutine-based: it only enqueues fast tasks (set flags) and yields between them.
/// It does NOT block. Lockups come from other systems (OvertureMapManager, UserPresenceManager,
/// API TryAutoConnect, etc.) that run during/after boot—those must yield or defer heavy work.
/// </summary>
public class BootSequence : MonoBehaviour
{
    [SerializeField] private float gpsReadyTimeoutSeconds = 45f;

#if !UNITY_WEBGL
    private static readonly object _deviceLogLock = new object();
    private static bool _deviceLogInstalled;
    private static string _deviceLogPath;
#endif

#if UNITY_EDITOR
    [Tooltip("If true, skip waiting for first tiles so editor can boot to skybox + player (test for lockup).")]
    [SerializeField] private bool skipTilesWaitInEditor = true;
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        // Mac Retina reports Unity player pixels at 2x the window points.
        // This opens as an iPhone-shaped 390×844 point viewport.
        Screen.SetResolution(780, 1688, false);
#endif
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        BootDiagnostics.Mark("BootSequence Init");
#endif
#if !UNITY_WEBGL
        InstallDeviceFileLogger();
#endif
        var go = new GameObject("BootSequence");
        DontDestroyOnLoad(go);
        var component = go.AddComponent<BootSequence>();
        // Ensure presence/heartbeat runs even if GPSLocationController is missing/disabled.
        // This prevents the HUD getting stuck "scanning locations" with no weather/stories.
        if (go.GetComponent<UserPresenceManager>() == null)
            go.AddComponent<UserPresenceManager>();
        // Ensure component stays enabled
        component.enabled = true;
        go.SetActive(true);
    }

#if !UNITY_WEBGL
    private static void InstallDeviceFileLogger()
    {
        if (_deviceLogInstalled) return;
        _deviceLogInstalled = true;

        try
        {
            _deviceLogPath = Path.Combine(Application.persistentDataPath, "k1l0_device.log");
            lock (_deviceLogLock)
            {
                File.WriteAllText(
                    _deviceLogPath,
                    $"===== K1L0 BOOT {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} =====\n" +
                    $"persistentDataPath={Application.persistentDataPath}\n" +
                    $"platform={Application.platform}\n" +
                    $"version={Application.version}\n\n"
                );
            }

            Application.logMessageReceivedThreaded += (condition, stackTrace, type) =>
            {
                try
                {
                    lock (_deviceLogLock)
                    {
                        File.AppendAllText(_deviceLogPath, $"{DateTime.Now:HH:mm:ss.fff} [{type}] {condition}\n");
                        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                            File.AppendAllText(_deviceLogPath, $"{stackTrace}\n");
                    }
                }
                catch { /* never throw from logger */ }
            };

            Debug.Log($"[BootSequence] Device file logger installed: {_deviceLogPath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[BootSequence] Failed to install device file logger: {e.Message}");
        }
    }
#endif

    private void Awake()
    {
        // Ensure we stay alive
        enabled = true;
        gameObject.SetActive(true);
    }

    private void Start()
    {
#if UNITY_EDITOR
        // Editor: run boot synchronously via EditorApplication.update since coroutines freeze after frame 2
        EditorApplication.update += EditorBootOnce;
#else
        StartCoroutine(Run());
#endif
    }

#if UNITY_EDITOR
    private void EditorBootOnce()
    {
        EditorApplication.update -= EditorBootOnce;
        if (!Application.isPlaying) return;

        BootDiagnostics.SetMinimalBootLog(true);
        BootDiagnostics.Mark("BootSequence start");
        BootDiagnostics.Mark("BootSequence editor sync boot");

        BootState.SetRenderAllowed();
        BootDiagnostics.Mark("BootTask completed wait for AllowRender");

        BootState.SetGPSAllowed();
        BootDiagnostics.Mark("BootTask completed wait for AllowGPS");

        BootState.SetTeleportAllowed();
        BootDiagnostics.Mark("BootTask completed wait for AllowTeleport");

        BootState.SetMapAllowed();
        BootDiagnostics.Mark("BootTask completed wait for AllowMap");

        BootDiagnostics.Mark("BootSequence editor bypass - skip tiles wait");

        BootState.SetPlayerAllowed();
        BootDiagnostics.Mark("BootTask completed wait for AllowPlayer");

        BootDiagnostics.Mark("BootSequence complete (editor)");
        Debug.Log("[BootSequence] Editor sync boot complete — all states allowed.");
    }
#endif

    private IEnumerator Run()
    {
        BootDiagnostics.Mark("BootSequence start");

        // Ensure one frame for scene objects to Awake
        yield return null;

        // Force an early API connect probe so the app can't get stuck
        // "scanning locations" with zero backend calls.
        StartCoroutine(HealthProbe());

        BootDiagnostics.Mark("BootSequence before AllowRender");
        yield return BootTaskQueue.Enqueue("AllowRender", () => BootState.SetRenderAllowed());
        BootDiagnostics.Mark("BootSequence after AllowRender");

        BootDiagnostics.Mark("BootSequence before AllowGPS");
        yield return BootTaskQueue.Enqueue("AllowGPS", () => BootState.SetGPSAllowed());
        BootDiagnostics.Mark("BootSequence after AllowGPS");

        // Wait for GPS (or timeout) before teleport + map
        float start = Time.realtimeSinceStartup;
        while (!GPSLocationController.GPSReady)
        {
            if (Time.realtimeSinceStartup - start > gpsReadyTimeoutSeconds)
            {
                BootDiagnostics.Mark("BootSequence GPS timeout");
                break;
            }
            yield return new WaitForSeconds(0.5f);
        }

        yield return BootTaskQueue.Enqueue("AllowTeleport", () => BootState.SetTeleportAllowed());
        BootDiagnostics.Mark("BootSequence after AllowTeleport");

        yield return BootTaskQueue.Enqueue("AllowMap", () => BootState.SetMapAllowed());
        yield return BootTaskQueue.Enqueue("AllowPlayer", () => BootState.SetPlayerAllowed());
        BootDiagnostics.Mark("BootSequence complete");
    }

    private IEnumerator HealthProbe()
    {
        // Give one frame so APIManager singleton creation doesn't contend with boot.
        yield return null;
        yield return APIManager.Instance.Get("/health", (success, response) =>
        {
            if (success) Debug.Log($"[BootSequence] HealthProbe OK: {response}");
            else Debug.LogError($"[BootSequence] HealthProbe FAILED: {response}");
        });
    }
}
