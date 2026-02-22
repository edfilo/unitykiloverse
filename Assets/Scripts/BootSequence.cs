using System.Collections;
using UnityEngine;

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
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
#if UNITY_EDITOR
        if (skipTilesWaitInEditor)
            BootDiagnostics.SetMinimalBootLog(true);
#endif
        BootDiagnostics.Mark("BootSequence start");

        // Ensure one frame for scene objects to Awake
        yield return null;

        BootDiagnostics.Mark("BootSequence before AllowRender");
        yield return BootTaskQueue.Enqueue("AllowRender", () => BootState.SetRenderAllowed());
        BootDiagnostics.Mark("BootSequence after AllowRender");

        BootDiagnostics.Mark("BootSequence before AllowGPS");
        yield return BootTaskQueue.Enqueue("AllowGPS", () => BootState.SetGPSAllowed());
        BootDiagnostics.Mark("BootSequence after AllowGPS");

        BootDiagnostics.Mark($"BootSequence checking platform: isMobilePlatform={Application.isMobilePlatform}");
        if (!Application.isMobilePlatform)
        {
            BootDiagnostics.Mark("BootSequence editor fast path");
            BootDiagnostics.Mark("BootSequence before AllowTeleport");
            yield return BootTaskQueue.Enqueue("AllowTeleport", () => BootState.SetTeleportAllowed());
            BootDiagnostics.Mark("BootSequence after AllowTeleport");
            
            BootDiagnostics.Mark("BootSequence before AllowMap");
            yield return BootTaskQueue.Enqueue("AllowMap", () => BootState.SetMapAllowed());
            BootDiagnostics.Mark("BootSequence after AllowMap - waiting for first tiles");

#if UNITY_EDITOR
            // EDITOR BYPASS: skip waiting for tiles so we can test skybox + player without map lockup
            if (skipTilesWaitInEditor)
            {
                BootDiagnostics.Mark("BootSequence editor bypass - skip tiles wait");
            }
            else
#endif
            {
                // Keep boot "going" until first tiles are on screen (so loading runs during boot, not after)
                const float tilesWaitTimeout = 90f;
                float tilesWaitStart = Time.realtimeSinceStartup;
                while (!BootState.FirstTilesLoaded)
                {
                    float elapsed = Time.realtimeSinceStartup - tilesWaitStart;
                    if (elapsed > tilesWaitTimeout)
                    {
                        BootDiagnostics.Mark("BootSequence tiles timeout - completing anyway");
                        Debug.LogWarning($"[BootSequence] No tiles loaded after {tilesWaitTimeout}s - completing boot anyway.");
                        break;
                    }
                    BootDiagnostics.Mark($"Waiting for tiles... {(int)elapsed}s");
                    yield return new WaitForSeconds(0.5f);
                    BootDiagnostics.Mark("Waiting for tiles loop resume");
                }
                if (BootState.FirstTilesLoaded)
                    BootDiagnostics.Mark("BootSequence first tiles loaded");
            }

            BootDiagnostics.Mark("BootSequence before AllowPlayer");
            yield return BootTaskQueue.Enqueue("AllowPlayer", () => BootState.SetPlayerAllowed());
            BootDiagnostics.Mark("BootSequence after AllowPlayer");
            // Yield one frame so "complete" and all other Updates don't pile in the same frame (lockup started after we broke up boot)
            yield return null;
            BootDiagnostics.Mark("BootSequence complete (editor)");
            yield break;
        }

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
