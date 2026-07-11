using System;
using UnityEngine;

public sealed class K1L0NativeSessionBridge : MonoBehaviour
{
    public static K1L0NativeSessionBridge Instance { get; private set; }

    public static string UserId { get; private set; } = "";
    public static string DeviceId { get; private set; } = "";
    public static string Email { get; private set; } = "";
    public static string DisplayName { get; private set; } = "";
    public static bool IsAuthenticated { get; private set; }
    public static bool HasNativeUserId => !string.IsNullOrWhiteSpace(UserId);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("K1L0NativeSessionBridge");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<K1L0NativeSessionBridge>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadCachedSession();
    }

    public void ApplyNativeSessionState(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload)) return;

        NativeSessionPayload parsed;
        try
        {
            parsed = JsonUtility.FromJson<NativeSessionPayload>(payload);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[K1L0NativeSessionBridge] native session parse failed: {ex.Message}");
            return;
        }

        if (parsed == null) return;
        Apply(parsed);
    }

    public static string ResolveUserId(string fallback = "")
    {
        if (!string.IsNullOrWhiteSpace(UserId)) return UserId;

        var cachedFirebase = PlayerPrefs.GetString("FirebaseUserId", "").Trim();
        if (!string.IsNullOrWhiteSpace(cachedFirebase)) return cachedFirebase;

        var cachedK1L0 = PlayerPrefs.GetString("K1L0UserId", "").Trim();
        if (!string.IsNullOrWhiteSpace(cachedK1L0)) return cachedK1L0;

        var cachedDevice = PlayerPrefs.GetString("DeviceID", "").Trim();
        if (!string.IsNullOrWhiteSpace(cachedDevice)) return cachedDevice;

        return fallback ?? "";
    }

    public static void ApplyLocalSession(string userId, string email, string displayName, bool isAuthenticated)
    {
        Apply(new NativeSessionPayload
        {
            userId = userId ?? "",
            deviceId = PlayerPrefs.GetString("DeviceID", PlayerPrefs.GetString("deviceID", "")).Trim(),
            email = email ?? "",
            displayName = displayName ?? "",
            isAuthenticated = isAuthenticated
        });
    }

    public static void ClearLocalSession()
    {
        UserId = "";
        Email = "";
        DisplayName = "";
        IsAuthenticated = false;
    }

    private static void LoadCachedSession()
    {
        UserId = ResolveUserId("");
        DeviceId = PlayerPrefs.GetString("DeviceID", PlayerPrefs.GetString("deviceID", "")).Trim();
        Email = PlayerPrefs.GetString("FirebaseEmail", "").Trim();
        DisplayName = PlayerPrefs.GetString("FirebaseDisplayName", "").Trim();
        IsAuthenticated = !string.IsNullOrWhiteSpace(PlayerPrefs.GetString("FirebaseUserId", ""));
    }

    private static void Apply(NativeSessionPayload payload)
    {
        string nextUserId = (payload.userId ?? "").Trim();
        string nextDeviceId = (payload.deviceId ?? "").Trim();
        string nextEmail = (payload.email ?? "").Trim();
        string nextDisplayName = (payload.displayName ?? "").Trim();
        bool nextIsAuthenticated = payload.isAuthenticated;

        if (string.IsNullOrWhiteSpace(nextUserId) && !nextIsAuthenticated)
        {
            var cachedUserId = ResolveUserId("");
            if (!string.IsNullOrWhiteSpace(cachedUserId))
            {
                nextUserId = cachedUserId;
                nextIsAuthenticated = true;
            }
        }

        bool changed = nextUserId != UserId
            || nextDeviceId != DeviceId
            || nextEmail != Email
            || nextDisplayName != DisplayName
            || nextIsAuthenticated != IsAuthenticated;
        if (!changed) return;

        UserId = nextUserId;
        DeviceId = nextDeviceId;
        Email = nextEmail;
        DisplayName = nextDisplayName;
        IsAuthenticated = nextIsAuthenticated;

        if (!string.IsNullOrWhiteSpace(UserId))
        {
            PlayerPrefs.SetString("FirebaseUserId", UserId);
            PlayerPrefs.SetString("K1L0UserId", UserId);
        }
        if (!string.IsNullOrWhiteSpace(DeviceId))
            PlayerPrefs.SetString("DeviceID", DeviceId);
        if (!string.IsNullOrWhiteSpace(Email))
            PlayerPrefs.SetString("FirebaseEmail", Email);
        if (!string.IsNullOrWhiteSpace(DisplayName))
            PlayerPrefs.SetString("FirebaseDisplayName", DisplayName);

        PlayerPrefs.Save();
        Debug.Log($"[K1L0NativeSessionBridge] session user={UserId} auth={IsAuthenticated}");
    }

    [Serializable]
    private sealed class NativeSessionPayload
    {
        public string userId;
        public string deviceId;
        public string email;
        public string displayName;
        public bool isAuthenticated;
    }
}
