using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

// Compatibility shim retained while the old Unity HUD/scripts are phased out.
// Firebase auth now lives in the native Swift layer. Unity reads the native
// session bridge and never initializes the Firebase Unity SDK.
public class FirebaseAuthManager : MonoBehaviour
{
    private static FirebaseAuthManager _instance;
    public static FirebaseAuthManager Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("FirebaseAuthManager");
                _instance = go.AddComponent<FirebaseAuthManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    public bool isAuthenticated;
    public string userId;
    public string idToken;
    public string refreshToken;
    public string email;
    public string displayName;

    public event Action<bool> OnAuthStateChanged;
    public event Action<string> OnAuthError;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        RefreshFromNativeSession(notify: false);
    }

    private void RefreshFromNativeSession(bool notify)
    {
        userId = K1L0NativeSessionBridge.ResolveUserId("");
        email = K1L0NativeSessionBridge.Email;
        displayName = K1L0NativeSessionBridge.DisplayName;
        isAuthenticated = K1L0NativeSessionBridge.IsAuthenticated || !string.IsNullOrWhiteSpace(PlayerPrefs.GetString("FirebaseUserId", ""));
        idToken = "";
        refreshToken = "";
        if (notify) OnAuthStateChanged?.Invoke(isAuthenticated);
    }

    public void SaveAuthPublic()
    {
        if (!string.IsNullOrWhiteSpace(userId))
        {
            PlayerPrefs.SetString("FirebaseUserId", userId);
            PlayerPrefs.SetString("K1L0UserId", userId);
        }
        if (!string.IsNullOrWhiteSpace(email)) PlayerPrefs.SetString("FirebaseEmail", email);
        if (!string.IsNullOrWhiteSpace(displayName)) PlayerPrefs.SetString("FirebaseDisplayName", displayName);
        PlayerPrefs.Save();
    }

    public void SignInWithApple(string appleIdToken, string appleNonce, string appleFullName = null)
    {
        SignInWithNativeApple("", appleFullName, "", appleIdToken);
    }

    public void SignInWithNativeApple(string appleUserId, string appleFullName = null, string appleEmail = null, string fallbackToken = null)
    {
        string stableSource = !string.IsNullOrWhiteSpace(appleUserId)
            ? appleUserId
            : (!string.IsNullOrWhiteSpace(fallbackToken) ? fallbackToken : SystemInfo.deviceUniqueIdentifier);

        userId = MakeStableAppleUserId(stableSource);
        email = (appleEmail ?? "").Trim();
        displayName = !string.IsNullOrWhiteSpace(appleFullName)
            ? appleFullName.Trim()
            : PlayerPrefs.GetString("FirebaseDisplayName", "").Trim();
        idToken = "";
        refreshToken = "";
        isAuthenticated = !string.IsNullOrWhiteSpace(userId);

        K1L0NativeSessionBridge.ApplyLocalSession(userId, email, displayName, isAuthenticated);
        SaveAuthPublic();
        OnAuthStateChanged?.Invoke(isAuthenticated);
    }

    public void SignInAnonymously()
    {
        if (string.IsNullOrWhiteSpace(userId))
            userId = K1L0NativeSessionBridge.ResolveUserId(SystemInfo.deviceUniqueIdentifier);
        isAuthenticated = !string.IsNullOrWhiteSpace(userId);
        SaveAuthPublic();
        OnAuthStateChanged?.Invoke(isAuthenticated);
    }

    public void RefreshIdTokenAsync()
    {
        RefreshFromNativeSession(notify: false);
    }

    public void SignOut()
    {
        userId = "";
        idToken = "";
        refreshToken = "";
        email = "";
        displayName = "";
        isAuthenticated = false;

        PlayerPrefs.DeleteKey("FirebaseUserId");
        PlayerPrefs.DeleteKey("K1L0UserId");
        PlayerPrefs.DeleteKey("FirebaseEmail");
        PlayerPrefs.DeleteKey("FirebaseDisplayName");
        PlayerPrefs.Save();
        K1L0NativeSessionBridge.ClearLocalSession();
        OnAuthStateChanged?.Invoke(false);
    }

    public string GetUserId()
    {
        RefreshFromNativeSession(notify: false);
        if (!string.IsNullOrWhiteSpace(userId)) return userId;
        if (DeviceIDManager.Instance != null) return DeviceIDManager.Instance.DeviceID;
        return SystemInfo.deviceUniqueIdentifier;
    }

    private static string MakeStableAppleUserId(string source)
    {
        source = (source ?? "").Trim();
        if (string.IsNullOrWhiteSpace(source))
            source = SystemInfo.deviceUniqueIdentifier;

        using var sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(source));
        var sb = new StringBuilder("apple_", 30);
        for (int i = 0; i < 12 && i < hash.Length; i++)
            sb.Append(hash[i].ToString("x2"));
        return sb.ToString();
    }
}
