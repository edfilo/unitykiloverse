using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Centralized API manager for all backend calls.
/// Handles environment selection, automatic fallback, and request routing.
/// </summary>
public class APIManager : MonoBehaviour
{
    public enum APIEnvironment
    {
        Auto,
        Localhost,
        Tethered,
        Tunnel,
        Production
    }

    private static APIManager _instance;
    public static APIManager Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("APIManager");
                _instance = go.AddComponent<APIManager>();
                DontDestroyOnLoad(go);
                Debug.Log("[APIManager] Singleton instance created");
            }
            return _instance;
        }
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

    // Environment URLs
    private const string LOCALHOST_URL = "http://localhost:3000";
    // Dev LAN / tether endpoints. Auto-connect will try both unless overridden by profile.
    // Prefer mDNS on LAN so the phone doesn't depend on a specific IP.
    private const string LAN_MDNS_URL = "http://fred.local:3000";
    private const string LAN_URL = "http://192.168.40.34:3000";
    private const string HOTSPOT_URL = "http://172.20.10.5:3000";
    private const string TUNNEL_URL = "https://api-tunnel.kilo.gallery";
    private const string PRODUCTION_URL = "https://api.kilomeme.com";

    // Current environment (loaded from profile or defaults to Auto)
    private APIEnvironment m_currentEnvironment = APIEnvironment.Auto;
    private string m_activeURL = null;

    // PlayerPrefs key for production API override
    private const string PREF_PRODUCTION_API = "K1L0_ProductionAPI";

    /// <summary>Check if production API override is enabled.</summary>
    public static bool IsProductionOverride()
    {
        return PlayerPrefs.GetInt(PREF_PRODUCTION_API, 0) == 1;
    }

    /// <summary>Set/clear production API override.</summary>
    public static void SetProductionOverride(bool on)
    {
        PlayerPrefs.SetInt(PREF_PRODUCTION_API, on ? 1 : 0);
        PlayerPrefs.Save();
        if (Instance != null)
            Instance.SetEnvironment(on ? APIEnvironment.Production : APIEnvironment.Auto);
    }

    // Cache RenderManager to avoid repeated FindObjectOfType calls
    private KiloWorld.Rendering.Systems.RenderManager m_cachedRenderManager = null;

    /// <summary>
    /// Set the API environment (called from settings panel or profile)
    /// </summary>
    public void SetEnvironment(APIEnvironment env)
    {
        m_currentEnvironment = env;
        m_activeURL = null; // Reset cached URL
        Debug.Log($"[APIManager] Environment set to: {env}");
    }

    /// <summary>
    /// Get current API environment setting
    /// </summary>
    public APIEnvironment GetCurrentEnvironment()
    {
        return m_currentEnvironment;
    }

    /// <summary>
    /// Get the active base URL (will determine on first access)
    /// </summary>
    public string GetBaseURL()
    {
        if (m_activeURL != null)
            return m_activeURL;

        // Try to get overrides from Profile
        string overrideLocal = null;
        string overrideTether = null;


        // Cache RenderManager to avoid repeated FindObjectOfType calls (can be slow)
        if (m_cachedRenderManager == null)
        {
            m_cachedRenderManager = FindFirstObjectByType<KiloWorld.Rendering.Systems.RenderManager>();
        }
        var renderManager = m_cachedRenderManager;
        if (renderManager != null && renderManager.profile != null)
        {
            var api = renderManager.profile.api;
            if (!string.IsNullOrEmpty(api.customLocalhostURL)) overrideLocal = NormalizeURL(api.customLocalhostURL);
            if (!string.IsNullOrEmpty(api.customTetheredURL)) overrideTether = NormalizeURL(api.customTetheredURL);
        }

        switch (m_currentEnvironment)
        {
            case APIEnvironment.Localhost:
#if !UNITY_EDITOR
                // On device, localhost means the phone — redirect to tethered
                Debug.LogWarning("[APIManager] Localhost selected but running on device — redirecting to Tethered (localhost = phone, not Mac)");
                m_activeURL = overrideTether ?? LAN_MDNS_URL;
#else
                m_activeURL = overrideLocal ?? LOCALHOST_URL;
#endif
                break;
            case APIEnvironment.Tethered:
                m_activeURL = overrideTether ?? LAN_MDNS_URL;
                break;
            case APIEnvironment.Tunnel:
                m_activeURL = TUNNEL_URL;
                break;
            case APIEnvironment.Production:
                m_activeURL = PRODUCTION_URL;
                break;
            case APIEnvironment.Auto:
                // Auto mode is determined lazily by TryAutoConnect on first request.
                // Do NOT prefill m_activeURL here; otherwise Auto will never fall back
                // (e.g. if LAN/mDNS fails, we'll never try tunnel/production).
                m_activeURL = null;
                break;
        }

        Debug.Log($"[APIManager] Base URL: {m_activeURL ?? "None (Auto)"}");
        return m_activeURL;
    }

    private string NormalizeURL(string url)
    {
        if (string.IsNullOrEmpty(url)) return url;
        
        // If it's just an IP or hostname, add protocol and port
        if (!url.StartsWith("http"))
        {
            url = "http://" + url;
        }
        
        // If no port specified and it's not a standard web URL, add :3000
        // Check for colon after the protocol (http:// is 7 chars)
        if (url.IndexOf(":", 6) == -1 && !url.Contains(".com") && !url.Contains(".app"))
        {
            url = url.TrimEnd('/') + ":3000";
        }
        
        return url.TrimEnd('/');
    }

    private bool m_isConnecting = false;

    /// <summary>
    /// Auto mode: Try environments in order until one succeeds
    /// Localhost → Tethered → Tunnel → Production
    /// </summary>
    public IEnumerator TryAutoConnect(Action<bool, string> onComplete)
    {
        if (m_isConnecting)
        {
            while (m_isConnecting) yield return null;
            onComplete?.Invoke(m_activeURL != null, m_activeURL);
            yield break;
        }

        m_isConnecting = true;
        // Debug.Log("[APIManager] Auto mode: Testing connections in order...");

        // ... (Override logic) ...
        // Get overrides from Profile
        string overrideLocal = null;
        string overrideTether = null;


        // Use cached RenderManager if available
        if (m_cachedRenderManager == null)
        {
            m_cachedRenderManager = FindFirstObjectByType<KiloWorld.Rendering.Systems.RenderManager>();
        }
        var renderManager = m_cachedRenderManager;
        if (renderManager != null && renderManager.profile != null)
        {
            var api = renderManager.profile.api;
            if (!string.IsNullOrEmpty(api.customLocalhostURL)) overrideLocal = NormalizeURL(api.customLocalhostURL);
            if (!string.IsNullOrEmpty(api.customTetheredURL)) overrideTether = NormalizeURL(api.customTetheredURL);
        }

        // Test order — skip localhost on iOS/Android devices (localhost = the device, not the Mac)
        var urls = new List<string>();
        var names = new List<string>();

#if UNITY_EDITOR || UNITY_STANDALONE_OSX
        // Editor + Mac standalone both run on the same machine as the API server,
        // so localhost is reachable. Only device builds (iOS/Android) need to skip it.
        urls.Add(overrideLocal ?? LOCALHOST_URL);
        names.Add("Localhost");
#else
        // On device: localhost can never reach the Mac, skip it entirely
        Debug.Log("[APIManager] Skipping localhost on device build (localhost = phone, not Mac)");
#endif
        // First: profile override (if any), else LAN.
        // NOTE: On iOS, mDNS/DNS resolution can stall UnityWebRequest before timeouts apply.
        // Prefer the direct LAN IP first, then try mDNS.
        if (overrideTether != null)
        {
            urls.Add(overrideTether);
            names.Add("Tether Override");
        }
        else
        {
            urls.Add(LAN_URL);
            names.Add("LAN IP");
            urls.Add(LAN_MDNS_URL);
            names.Add("LAN mDNS");
        }
        if (overrideTether == null)
        {
            urls.Add(HOTSPOT_URL);
            names.Add("Hotspot");
        }
        urls.Add(TUNNEL_URL);
        names.Add("Tunnel");
        urls.Add(PRODUCTION_URL);
        names.Add("Production");

        float autoConnectStart = Time.realtimeSinceStartup;
        for (int i = 0; i < urls.Count; i++)
        {
            float attemptStart = Time.realtimeSinceStartup;
            Debug.Log($"[APIManager] Testing {names[i]}: {urls[i]}/ping");

            bool success = false;
            // Keep auto-connect snappy on device: LAN/mDNS failures must fall back quickly.
            int timeoutSec;
            if (Application.isEditor) timeoutSec = 5;
            else if (names[i].Contains("LAN") || names[i].Contains("Hotspot")) timeoutSec = 3;
            else timeoutSec = 8;

            // Send minimal POST request (ping endpoint requires POST, not GET)
            string testPayload = "{\"userId\":\"test\",\"lastActive\":0}";
            using (UnityWebRequest www = new UnityWebRequest($"{urls[i]}/ping", "POST"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(testPayload);
                www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                www.timeout = timeoutSec;

                yield return www.SendWebRequest();

                float attemptSec = Time.realtimeSinceStartup - attemptStart;
                if (attemptSec > 3f)
                    Debug.LogWarning($"[APIManager] TryAutoConnect {names[i]} took {attemptSec:F1}s (timeout={timeoutSec}s)");
                if (www.result == UnityWebRequest.Result.Success)
                {
                    success = true;
                    m_activeURL = urls[i];
                    float totalSec = Time.realtimeSinceStartup - autoConnectStart;
                    Debug.Log($"[APIManager] ✓ Connected to {names[i]}: {urls[i]} (TryAutoConnect total: {totalSec:F1}s)");
                    m_isConnecting = false;
                    onComplete?.Invoke(true, urls[i]);
                    yield break;
                }
            }
        }

        float totalAutoSec = Time.realtimeSinceStartup - autoConnectStart;
        Debug.LogWarning($"[APIManager] Auto mode: All connections failed after {totalAutoSec:F1}s. Falling back to Tunnel.");
        m_activeURL = TUNNEL_URL;
        m_isConnecting = false;
        onComplete?.Invoke(false, m_activeURL);
    }

    /// <summary>
    /// Generic POST request to backend
    /// </summary>
    public IEnumerator Post(string endpoint, string jsonPayload, Action<bool, string> onComplete)
    {
        float callStart = Time.realtimeSinceStartup;
        Debug.Log($"[APIManager] ➔ POST {endpoint} (Env: {m_currentEnvironment}, Active URL: {m_activeURL ?? "None (Auto-connecting...)"})");

        if (!string.IsNullOrEmpty(jsonPayload) && jsonPayload.Contains("\"transmissionType\":\"transmitter\""))
        {
            Debug.Log($"[APIManager] (tx) payload includes transmitter directive (bytes={System.Text.Encoding.UTF8.GetByteCount(jsonPayload)})");
        }

        // Auto mode: determine connection if not already set
        if (m_currentEnvironment == APIEnvironment.Auto && m_activeURL == null)
        {
            bool autoConnected = false;
            yield return TryAutoConnect((success, url) => {
                autoConnected = success;
            });
        }

        string url = $"{GetBaseURL()}{endpoint}";
        Debug.Log($"[APIManager] Sending POST to {url}");
        // LLM-backed endpoints can take 15-25s; editor used 10s which was too tight.
        int timeoutSec = 30;

        using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonPayload);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            
            if (FirebaseAuthManager.Instance != null && !string.IsNullOrEmpty(FirebaseAuthManager.Instance.idToken))
            {
                www.SetRequestHeader("Authorization", $"Bearer {FirebaseAuthManager.Instance.idToken}");
            }
            
            www.timeout = timeoutSec;

            yield return www.SendWebRequest();

            float elapsed = Time.realtimeSinceStartup - callStart;
            if (elapsed > 5f)
                Debug.LogWarning($"[APIManager] POST {endpoint} took {elapsed:F1}s (possible API lockup)");

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[APIManager] ✗ POST {endpoint} failed after {elapsed:F1}s: {www.error}");
                onComplete?.Invoke(false, www.error);
            }
            else
            {
                string response = www.downloadHandler.text;
                Debug.Log($"[APIManager] ✓ POST {endpoint} success in {elapsed:F1}s: {response}");
                onComplete?.Invoke(true, response);
            }
        }
    }

    /// <summary>
    /// Generic GET request to backend
    /// </summary>
    public IEnumerator Get(string endpoint, Action<bool, string> onComplete)
    {
        float getCallStart = Time.realtimeSinceStartup;
        Debug.Log($"[APIManager] ➔ GET {endpoint} (Env: {m_currentEnvironment}, Active URL: {m_activeURL ?? "None (Auto-connecting...)"})");

        // Auto mode: determine connection if not already set
        if (m_currentEnvironment == APIEnvironment.Auto && m_activeURL == null)
        {
            bool autoConnected = false;
            yield return TryAutoConnect((success, url) => {
                autoConnected = success;
            });
        }

        string url = $"{GetBaseURL()}{endpoint}";
        Debug.Log($"[APIManager] Sending GET to resolved URL: {url}");

        using (UnityWebRequest www = UnityWebRequest.Get(url))
        {
            if (FirebaseAuthManager.Instance != null && !string.IsNullOrEmpty(FirebaseAuthManager.Instance.idToken))
            {
                www.SetRequestHeader("Authorization", $"Bearer {FirebaseAuthManager.Instance.idToken}");
            }
            
            www.timeout = 10;
            yield return www.SendWebRequest();

            float elapsed = Time.realtimeSinceStartup - getCallStart;
            if (elapsed > 5f)
                Debug.LogWarning($"[APIManager] GET {endpoint} took {elapsed:F1}s (possible API lockup)");
            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[APIManager] ✗ GET {endpoint} failed after {elapsed:F1}s: {www.error}");
                onComplete?.Invoke(false, www.error);
            }
            else
            {
                string response = www.downloadHandler.text;
                Debug.Log($"[APIManager] ✓ GET {endpoint} success in {elapsed:F1}s: {response}");
                onComplete?.Invoke(true, response);
            }
        }
    }
}
