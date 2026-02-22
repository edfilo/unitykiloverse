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
        Ngrok,
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
    private const string TETHERED_URL = "http://172.20.10.5:3000";
    private const string NGROK_URL = "https://e735cc352f38.ngrok-free.app";
    private const string PRODUCTION_URL = "https://api.kilomeme.com";

    // Cached ngrok URL from tethered API
    private string m_dynamicNgrokURL = null;

    // Current environment (loaded from profile or defaults to Auto)
    private APIEnvironment m_currentEnvironment = APIEnvironment.Auto;
    private string m_activeURL = null;
    
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
        string overrideNgrok = null;

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
            if (!string.IsNullOrEmpty(api.customNgrokURL)) overrideNgrok = NormalizeURL(api.customNgrokURL);
        }

        switch (m_currentEnvironment)
        {
            case APIEnvironment.Localhost:
#if !UNITY_EDITOR
                // On device, localhost means the phone — redirect to tethered
                Debug.LogWarning("[APIManager] Localhost selected but running on device — redirecting to Tethered (localhost = phone, not Mac)");
                m_activeURL = overrideTether ?? TETHERED_URL;
#else
                m_activeURL = overrideLocal ?? LOCALHOST_URL;
#endif
                break;
            case APIEnvironment.Tethered:
                m_activeURL = overrideTether ?? TETHERED_URL;
                break;
            case APIEnvironment.Ngrok:
                m_activeURL = overrideNgrok ?? (m_dynamicNgrokURL ?? NGROK_URL);
                break;
            case APIEnvironment.Production:
                m_activeURL = PRODUCTION_URL;
                break;
            case APIEnvironment.Auto:
                // Auto mode will be determined by TryAutoConnect
#if UNITY_EDITOR
                m_activeURL = overrideLocal ?? LOCALHOST_URL; // Editor: default to localhost
#else
                m_activeURL = overrideTether ?? TETHERED_URL; // Device: default to tethered
#endif
                break;
        }

        Debug.Log($"[APIManager] Base URL: {m_activeURL}");
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
    /// Localhost → Tethered → Ngrok → Production
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
        string overrideNgrok = null;

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
            if (!string.IsNullOrEmpty(api.customNgrokURL)) overrideNgrok = NormalizeURL(api.customNgrokURL);
        }

        // Test order — skip localhost on iOS/Android devices (localhost = the device, not the Mac)
        var urls = new List<string>();
        var names = new List<string>();

#if UNITY_EDITOR
        urls.Add(overrideLocal ?? LOCALHOST_URL);
        names.Add("Localhost");
#else
        // On device: localhost can never reach the Mac, skip it entirely
        Debug.Log("[APIManager] Skipping localhost on device build (localhost = phone, not Mac)");
#endif
        urls.Add(overrideTether ?? TETHERED_URL);
        names.Add("Tethered");
        urls.Add(overrideNgrok ?? (m_dynamicNgrokURL ?? NGROK_URL));
        names.Add("Ngrok");
        urls.Add(PRODUCTION_URL);
        names.Add("Production");

        float autoConnectStart = Time.realtimeSinceStartup;
        for (int i = 0; i < urls.Count; i++)
        {
            float attemptStart = Time.realtimeSinceStartup;
            Debug.Log($"[APIManager] Testing {names[i]}: {urls[i]}/ping");

            bool success = false;
            // In editor use shorter timeout so dead localhost doesn't feel like a lockup
            int timeoutSec = Application.isEditor ? 5 : 30;

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
        Debug.LogWarning($"[APIManager] Auto mode: All connections failed after {totalAutoSec:F1}s. Falling back to Ngrok.");
        m_activeURL = overrideNgrok ?? NGROK_URL;
        m_isConnecting = false;
        onComplete?.Invoke(false, m_activeURL);
    }

    /// <summary>
    /// Refresh dynamic ngrok URL from tethered API
    /// </summary>
    public IEnumerator RefreshNgrokURL(Action<bool, string> onComplete = null)
    {
        string url = $"{TETHERED_URL}/ngrok-status?restart=true";
        Debug.Log($"[APIManager] Fetching dynamic ngrok URL from: {url}");

        using (UnityWebRequest www = UnityWebRequest.Get(url))
        {
            www.timeout = 5;
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[APIManager] Failed to fetch ngrok URL: {www.error}");
                onComplete?.Invoke(false, null);
                yield break;
            }

            try
            {
                var json = JsonUtility.FromJson<NgrokResponse>(www.downloadHandler.text);
                if (json != null && !string.IsNullOrEmpty(json.url))
                {
                    m_dynamicNgrokURL = json.url;
                    Debug.Log($"[APIManager] ✓ Got dynamic ngrok URL: {m_dynamicNgrokURL}");
                    onComplete?.Invoke(true, m_dynamicNgrokURL);
                }
                else
                {
                    Debug.LogWarning("[APIManager] Invalid ngrok response format");
                    onComplete?.Invoke(false, null);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[APIManager] Failed to parse ngrok response: {e.Message}");
                onComplete?.Invoke(false, null);
            }
        }
    }

    [Serializable]
    private class NgrokResponse
    {
        public string status;
        public string url;
    }

    /// <summary>
    /// Generic POST request to backend
    /// </summary>
    public IEnumerator Post(string endpoint, string jsonPayload, Action<bool, string> onComplete)
    {
        float callStart = Time.realtimeSinceStartup;
        Debug.Log($"[APIManager] ➔ POST {endpoint} (Env: {m_currentEnvironment}, Active URL: {m_activeURL ?? "None (Auto-connecting...)"})");

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
        int timeoutSec = Application.isEditor ? 10 : 30;

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
