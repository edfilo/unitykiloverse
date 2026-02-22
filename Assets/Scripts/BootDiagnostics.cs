using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class BootDiagnostics : MonoBehaviour
{
    private static BootDiagnostics _instance;
    private static readonly List<string> _events = new List<string>(64);
    private static float _startTime;
    private static string _lastLine = "";
    private static string _logPath;
    private static StreamWriter _logWriter;
    private static bool _minimalBootLog;

    /// <summary>When true (e.g. editor bypass), only key lines are written to BootLog.txt to avoid clutter.</summary>
    public static void SetMinimalBootLog(bool minimal) { _minimalBootLog = minimal; }

    [SerializeField] private bool showOverlay = true;
    private GUIStyle _style;
    private static bool _gracePeriodLogged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        if (_instance != null) return;
        StartBootLog();
        var go = new GameObject("BootDiagnostics");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<BootDiagnostics>();
        _startTime = Time.realtimeSinceStartup;
        Mark("AfterSceneLoad");
#endif
    }

    private static bool IsMinimalStep(string step)
    {
        if (string.IsNullOrEmpty(step)) return false;
        string s = step.ToLowerInvariant();
        // Only these lines go to file in minimal mode (avoid "start"/"init" - they match too much)
        return s.Contains("bypass") || s.Contains("complete") || s.Contains("editor skip")
            || s.Contains("allowplayer") || s.Contains("bootsequence start") || s.Contains("overturemapmanager")
            || s.Contains("grace period") || s.Contains("playerbootgate") || s.Contains("buildingcollider")
            || s.Contains("userpresence") || s.Contains("heartbeat") || s.Contains("first update after") || s.Contains("bootwatchdog")
            || s.Contains("locationlabel") || s.Contains("weatherview") || s.Contains("gpslocation");
    }

    private static void StartBootLog()
    {
        try
        {
#if UNITY_EDITOR
            var projectDir = Path.GetDirectoryName(Application.dataPath);
            _logPath = Path.Combine(projectDir ?? "", "BootLog.txt");
#else
            _logPath = Path.Combine(Application.persistentDataPath, "BootLog.txt");
#endif
            _logWriter = new StreamWriter(_logPath, append: false) { AutoFlush = true };
            _logWriter.WriteLine($"[{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}] Boot log started (editor={Application.isEditor})");
            _logWriter.WriteLine($"Log file: {_logPath}");
            _logWriter.WriteLine("If the editor locks up, the LAST line above is where it froze.");
            _logWriter.WriteLine("---");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning($"[BootDiagnostics] Could not create BootLog: {e.Message}");
        }
    }

    public static void Mark(string step)
    {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        if (string.IsNullOrEmpty(step)) return;
        if (_startTime <= 0f) _startTime = Time.realtimeSinceStartup;
        float t = Time.realtimeSinceStartup - _startTime;
        string line = $"{t:0.000}s f{Time.frameCount} {step}";
        _lastLine = line;
        _events.Add(line);
        if (_events.Count > 20) _events.RemoveAt(0);
        // In editor minimal mode skip Debug.Log entirely - Console update can lock the editor when many messages arrive
#if !UNITY_EDITOR
        Debug.Log("[Boot] " + line);
#else
        if (!_minimalBootLog)
            Debug.Log("[Boot] " + line);
#endif
        try
        {
            if (_logWriter != null && (!_minimalBootLog || IsMinimalStep(step)))
                _logWriter.WriteLine($"{t:0.000}s f{Time.frameCount} {step}");
        }
        catch { /* ignore */ }
#if UNITY_EDITOR
        BootWatchdog.Mark(step);
#endif
#endif
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private static bool _loggedFirstUpdateAfterBoot;
    private void Update()
    {
#if UNITY_EDITOR
        if (BootState.AllowPlayer && !_loggedFirstUpdateAfterBoot)
        {
            _loggedFirstUpdateAfterBoot = true;
            Mark("BootDiagnostics first Update after AllowPlayer");
        }
        if (_gracePeriodLogged || !BootState.AllowPlayer) return;
        if (BootState.PostBootGracePeriodElapsed)
        {
            _gracePeriodLogged = true;
            Mark("PostBoot grace period ended - map/building load can start");
        }
#endif
    }

    private void OnDestroy()
    {
        try
        {
            if (_logWriter != null)
            {
                _logWriter.WriteLine($"[{System.DateTime.Now:HH:mm:ss}] Boot diagnostics destroyed. Log file: {_logPath}");
                _logWriter.Close();
                _logWriter = null;
            }
        }
        catch { /* ignore */ }
    }

    private void OnGUI()
    {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        if (!showOverlay) return;
        // In editor, hide overlay once boot is complete to reduce GUI load (can cause lockup)
        if (BootState.AllowPlayer) return;
        // During "waiting for tiles" hide overlay to avoid GUI load (can cause lockup)
        if (!BootState.FirstTilesLoaded) return;
        if (_style == null)
        {
            _style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                normal = { textColor = Color.white }
            };
        }

        float x = 16f;
        float y = 16f;
        float lineHeight = 24f;
        GUI.Box(new Rect(8, 8, 900, 24 + _events.Count * lineHeight), GUIContent.none);
        GUI.Label(new Rect(x, y, 880, 24), "Boot Diagnostics (last 20)", _style);
        y += lineHeight;
        if (!string.IsNullOrEmpty(_lastLine))
        {
            GUI.Label(new Rect(x, y, 880, 24), "Last: " + _lastLine, _style);
            y += lineHeight;
        }
        foreach (var e in _events)
        {
            GUI.Label(new Rect(x, y, 880, 24), e, _style);
            y += lineHeight;
        }
#endif
    }
}
