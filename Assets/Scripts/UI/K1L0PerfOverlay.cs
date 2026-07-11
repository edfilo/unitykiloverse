using UnityEngine;
using UnityEngine.Profiling;
using TMPro;
using System.Globalization;
using Kiloverse.Mapbox;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

/// <summary>
/// Real-time performance overlay for dev builds.
/// Shows FPS, battery drain rate, thermal state, memory, and render stats.
/// Lives on K1L0CanvasRoot.HUD, top-right corner.
/// </summary>
public class K1L0PerfOverlay : MonoBehaviour
{
    private TextMeshProUGUI _text;
    private float _updateInterval = 1f;
    private float _nextUpdate;

    // FPS tracking
    private int _frameCount;
    private float _fpsAccum;
    private float _currentFps;
    private float _worstFrameTime;

    // Battery tracking
    private float _batteryAtStart;
    private float _batteryStartTime;
    private float _lastBatteryLevel;
    private float _lastBatteryTime;
    private float _drainPerHour;
    private int _drainSampleCount;

    // Thermal
    private string _thermalState = "?";
    private Color _thermalColor = Color.white;

    // Pedometer (cached lookup)
    private PedometerService _pedometer;

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern int _GetThermalState();

    [DllImport("__Internal")]
    private static extern void K1L0DeliverPerfStats(string json);
#endif

    private static K1L0PerfOverlay _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoSpawn()
    {
        if (_instance != null) return;
        var go = new GameObject("K1L0PerfOverlay");
        DontDestroyOnLoad(go);
        go.AddComponent<K1L0PerfOverlay>();
    }

    void Start()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        CreateUI();
        _text.enabled = ProfileEditorModal.ShowPerfOverlay;
        ProfileEditorModal.OnDebugTogglesChanged += () => {
            if (_text != null) _text.enabled = ProfileEditorModal.ShowPerfOverlay;
        };

        // Battery init
        SystemInfo.batteryLevel.ToString(); // force battery monitoring
        _batteryAtStart = SystemInfo.batteryLevel;
        _batteryStartTime = Time.realtimeSinceStartup;
        _lastBatteryLevel = _batteryAtStart;
        _lastBatteryTime = _batteryStartTime;

        _nextUpdate = Time.realtimeSinceStartup + _updateInterval;
    }

    void CreateUI()
    {
        var go = new GameObject("PerfLabel");
        go.transform.SetParent(K1L0CanvasRoot.HUD, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(1, 1);
        rt.anchoredPosition = new Vector2(-4, -4);
        rt.sizeDelta = new Vector2(0, 50);

        _text = go.AddComponent<TextMeshProUGUI>();
        var font = Resources.Load<TMP_FontAsset>("Fonts/IBMPlexMono-Regular SDF");
        if (font == null) font = TMP_Settings.defaultFontAsset;
        _text.font = font;
        _text.fontSize = 10;
        _text.color = new Color(0.6f, 1f, 0.6f, 0.8f);
        _text.alignment = TextAlignmentOptions.TopRight;
        _text.raycastTarget = false;
        _text.enableWordWrapping = false;
    }

    void Update()
    {
        _frameCount++;
        _fpsAccum += Time.unscaledDeltaTime;
        if (Time.unscaledDeltaTime > _worstFrameTime)
            _worstFrameTime = Time.unscaledDeltaTime;

        if (Time.realtimeSinceStartup < _nextUpdate) return;
        _nextUpdate = Time.realtimeSinceStartup + _updateInterval;

        // FPS
        _currentFps = _frameCount / _fpsAccum;
        float worstMs = _worstFrameTime * 1000f;
        _frameCount = 0;
        _fpsAccum = 0;
        _worstFrameTime = 0;

        // Battery drain
        float bat = SystemInfo.batteryLevel;
        float now = Time.realtimeSinceStartup;
        // Use overall drain since start — most reliable measurement
        float totalElapsed = now - _batteryStartTime;
        float totalDrop = bat >= 0 ? (_batteryAtStart - bat) * 100f : 0;
        _drainPerHour = totalElapsed > 120f ? (totalDrop / totalElapsed) * 3600f : 0;

        // Thermal state
        UpdateThermalState();

        // Memory
        long reservedMB = Profiler.GetTotalReservedMemoryLong() / (1024 * 1024);
        long allocMB = Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024);

        // Battery status
        string batStr = bat >= 0 ? $"{bat * 100f:F0}%" : "?";
        string drainStr = _drainPerHour > 0 ? $"{_drainPerHour:F1}" : "...";

        // Build display
        Color fpsColor = _currentFps >= 28 ? new Color(0.6f, 1f, 0.6f) :
                         _currentFps >= 20 ? Color.yellow : Color.red;

        string fpsHex = ColorUtility.ToHtmlStringRGB(fpsColor);
        string thermHex = ColorUtility.ToHtmlStringRGB(_thermalColor);

        // Steps from PedometerService (-1 means not yet sampled)
        if (_pedometer == null) _pedometer = Object.FindFirstObjectByType<PedometerService>();
        string stepsStr = "";
        if (_pedometer != null)
        {
            string s24 = _pedometer.stepsLast24Hours >= 0 ? _pedometer.stepsLast24Hours.ToString("N0") : "...";
            string s7d = _pedometer.stepsLast7Days   >= 0 ? _pedometer.stepsLast7Days.ToString("N0")   : "...";
            stepsStr = $"24h:{s24}  7d:{s7d}";
        }

        _text.text =
            $"<#{fpsHex}>{_currentFps:F0} FPS</color> <color=#888>{worstMs:F0}ms</color>\n" +
            $"<#{thermHex}>{_thermalState}</color> {allocMB}MB  {stepsStr}\n" +
            $"{batStr} {drainStr}%/hr ({totalDrop:F1}% in {totalElapsed / 60f:F0}m)";

        PushNativePerfStats(worstMs, allocMB, reservedMB, bat, _drainPerHour);
    }

    void PushNativePerfStats(float worstMs, long allocMB, long reservedMB, float batteryLevel, float drainPerHour)
    {
#if UNITY_IOS && !UNITY_EDITOR
        string json = string.Format(
            CultureInfo.InvariantCulture,
            "{{\"fps\":{0:F1},\"frameMs\":{1:F1},\"allocMB\":{2},\"reservedMB\":{3},\"thermal\":\"{4}\",\"batteryPct\":{5:F1},\"batteryDrainPctPerHour\":{6:F2},{7}}}",
            _currentFps,
            worstMs,
            allocMB,
            reservedMB,
            EscapeJson(_thermalState),
            batteryLevel >= 0 ? batteryLevel * 100f : -1f,
            drainPerHour,
            OvertureMapManager.RenderDebugJson()
        );
        K1L0DeliverPerfStats(json);
#endif
    }

    static string EscapeJson(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    void UpdateThermalState()
    {
#if UNITY_IOS && !UNITY_EDITOR
        int state = _GetThermalState();
        switch (state)
        {
            case 0: _thermalState = "NOMINAL"; _thermalColor = new Color(0.6f, 1f, 0.6f); break;
            case 1: _thermalState = "FAIR"; _thermalColor = Color.yellow; break;
            case 2: _thermalState = "SERIOUS"; _thermalColor = new Color(1f, 0.5f, 0f); break;
            case 3: _thermalState = "CRITICAL"; _thermalColor = Color.red; break;
            default: _thermalState = $"?({state})"; _thermalColor = Color.white; break;
        }
#else
        _thermalState = "EDITOR";
        _thermalColor = Color.white;
#endif
    }
}
