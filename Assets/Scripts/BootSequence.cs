using System.Collections;
using UnityEngine;
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

#if UNITY_EDITOR
    [Tooltip("If true, skip waiting for first tiles so editor can boot to skybox + player (test for lockup).")]
    [SerializeField] private bool skipTilesWaitInEditor = true;
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        // Mac: start at a Maps-like window size
        Screen.SetResolution(1280, 900, false);
#endif
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        BootDiagnostics.Mark("BootSequence Init");
#endif
        var go = new GameObject("BootSequence");
        DontDestroyOnLoad(go);
        var component = go.AddComponent<BootSequence>();
        // Ensure component stays enabled
        component.enabled = true;
        go.SetActive(true);
    }

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
}
