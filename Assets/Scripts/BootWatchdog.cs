using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class BootWatchdog : MonoBehaviour
{
    [SerializeField] private float timeoutSeconds = 60f;

    private static float _lastBootTime;
    private static string _lastBootLabel;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
#if UNITY_EDITOR
        var go = new GameObject("BootWatchdog");
        DontDestroyOnLoad(go);
        go.AddComponent<BootWatchdog>();
#endif
    }

    private void Awake()
    {
        _lastBootTime = Time.realtimeSinceStartup;
        _lastBootLabel = "BootWatchdog.Awake";
    }

    private static bool _loggedPostBoot;
    private void Update()
    {
#if UNITY_EDITOR
        if (!EditorApplication.isPlaying) return;
        if (BootState.AllowPlayer)
        {
            if (!_loggedPostBoot)
            {
                _loggedPostBoot = true;
                BootDiagnostics.Mark("BootWatchdog first Update after AllowPlayer");
            }
            return; // boot complete, stop watchdog
        }
        float idle = Time.realtimeSinceStartup - _lastBootTime;
        if (idle > timeoutSeconds)
        {
            Debug.LogError($"[BootWatchdog] No boot progress for {idle:F1}s. Last boot: {_lastBootLabel}. Stopping play mode.");
            EditorApplication.isPlaying = false;
        }
#endif
    }

    public static void Mark(string label)
    {
        _lastBootTime = Time.realtimeSinceStartup;
        _lastBootLabel = label;
    }
}
