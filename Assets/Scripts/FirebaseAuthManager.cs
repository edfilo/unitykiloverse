using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Manages Firebase Authentication using REST API
/// Supports Apple Sign-In with automatic token exchange
/// Lightweight alternative to full Firebase Unity SDK
/// </summary>
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

    [Header("Firebase Configuration")]
    public string firebaseApiKey = "AIzaSyCJYzfGpp9lYBkIHlAyflGJ-vaT1WfpzjU";
    public string firebaseProjectId = "kiloworld-aa8d6";

    [Header("Auth State")]
    public bool isAuthenticated = false;
    public string userId = null;
    public string idToken = null;
    public string refreshToken = null;
    public string email = null;
    public string displayName = null;

    // Events
    public event Action<bool> OnAuthStateChanged;
    public event Action<string> OnAuthError;

    private const string PREF_ID_TOKEN = "FirebaseIdToken";
    private const string PREF_REFRESH_TOKEN = "FirebaseRefreshToken";
    private const string PREF_USER_ID = "FirebaseUserId";
    private const string PREF_EMAIL = "FirebaseEmail";
    private const string PREF_DISPLAY_NAME = "FirebaseDisplayName";

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        // Try to restore session
        LoadStoredAuth();
    }

    private void Start()
    {
        // If we have a refresh token, try to refresh the ID token
        if (!string.IsNullOrEmpty(refreshToken))
        {
            RefreshIdTokenAsync();
        }
    }

    /// <summary>
    /// Load stored authentication from PlayerPrefs
    /// </summary>
    private void LoadStoredAuth()
    {
        if (PlayerPrefs.HasKey(PREF_ID_TOKEN))
        {
            idToken = PlayerPrefs.GetString(PREF_ID_TOKEN);
            refreshToken = PlayerPrefs.GetString(PREF_REFRESH_TOKEN, "");
            userId = PlayerPrefs.GetString(PREF_USER_ID, "");
            email = PlayerPrefs.GetString(PREF_EMAIL, "");
            displayName = PlayerPrefs.GetString(PREF_DISPLAY_NAME, "");
            isAuthenticated = true;

            Debug.Log($"[FirebaseAuth] Restored session for user: {userId}");
            OnAuthStateChanged?.Invoke(true);
        }
    }

    /// <summary>
    /// Save authentication to PlayerPrefs
    /// </summary>
    public void SaveAuthPublic() => SaveAuth();

    private void SaveAuth()
    {
        PlayerPrefs.SetString(PREF_ID_TOKEN, idToken ?? "");
        PlayerPrefs.SetString(PREF_REFRESH_TOKEN, refreshToken ?? "");
        PlayerPrefs.SetString(PREF_USER_ID, userId ?? "");
        PlayerPrefs.SetString(PREF_EMAIL, email ?? "");
        PlayerPrefs.SetString(PREF_DISPLAY_NAME, displayName ?? "");
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Sign in with Apple ID Token
    /// Exchanges Apple identity token for Firebase credentials directly via Firebase REST API
    /// </summary>
    private string _pendingAppleFullName;

    public void SignInWithApple(string appleIdToken, string appleNonce, string appleFullName = null)
    {
        Debug.Log("[FirebaseAuth] Signing in with Apple via Firebase REST API...");
        Debug.Log($"[FirebaseAuth] idToken length={appleIdToken?.Length}, nonce length={appleNonce?.Length}");

        _pendingAppleFullName = appleFullName;

        string url = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithIdp?key={firebaseApiKey}";

        // Log nonce details for debugging
        Debug.Log($"[FirebaseAuth] Raw nonce (first 8): {(appleNonce?.Length > 8 ? appleNonce.Substring(0, 8) : appleNonce)}...");
        Debug.Log($"[FirebaseAuth] ID token (first 40): {(appleIdToken?.Length > 40 ? appleIdToken.Substring(0, 40) : appleIdToken)}...");

        // Apple token contains SHA256(rawNonce). Firebase compares literally, so we must hash it.
        string hashedNonce = Sha256(appleNonce);
        Debug.Log($"[FirebaseAuth] rawNonce={appleNonce}");
        Debug.Log($"[FirebaseAuth] hashedNonce={hashedNonce}");
        string postBody = $"id_token={appleIdToken}&nonce={hashedNonce}&providerId=apple.com";

        string json = JsonUtility.ToJson(new FirebaseIdpRequest {
            postBody = postBody,
            requestUri = $"https://{firebaseProjectId}.firebaseapp.com/__/auth/handler",
            returnSecureToken = true,
            returnIdpCredential = true
        });

        var www = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        www.uploadHandler = new UploadHandlerRaw(bodyRaw);
        www.downloadHandler = new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");
        www.timeout = 15;

        var op = www.SendWebRequest();
        op.completed += _ => OnSignInComplete(www);
    }

    private void OnSignInComplete(UnityWebRequest www)
    {
        try
        {
            if (www.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[FirebaseAuth] Raw response: {www.downloadHandler.text}");
                var authResponse = JsonUtility.FromJson<FirebaseAuthResponse>(www.downloadHandler.text);

                userId = authResponse.localId;
                idToken = authResponse.idToken;
                refreshToken = authResponse.refreshToken;
                email = authResponse.email ?? "";
                displayName = authResponse.displayName ?? _pendingAppleFullName ?? "";

                isAuthenticated = true;
                SaveAuth();

                Debug.Log($"[FirebaseAuth] ✓ Signed in successfully: {userId}");
                OnAuthStateChanged?.Invoke(true);

                if (DeviceIDManager.Instance != null)
                {
                    PlayerPrefs.SetString("FirebaseUID_" + DeviceIDManager.Instance.DeviceID, userId);
                    PlayerPrefs.Save();
                }
            }
            else
            {
                string responseBody = www.downloadHandler?.text ?? "(no body)";
                Debug.LogError($"[FirebaseAuth] Sign in failed: HTTP {www.responseCode} {www.error}");
                Debug.LogError($"[FirebaseAuth] Response: {responseBody}");

                // Show the actual Firebase error to the user
                string userMsg = $"HTTP {www.responseCode}: ";
                if (responseBody.Contains("INVALID_IDP_RESPONSE"))
                {
                    // Extract the detail after the colon
                    int idx = responseBody.IndexOf("INVALID_IDP_RESPONSE");
                    string detail = responseBody.Substring(idx, Math.Min(80, responseBody.Length - idx));
                    userMsg += detail;
                }
                else if (responseBody.Contains("MISSING_OR_INVALID_NONCE"))
                {
                    int idx = responseBody.IndexOf("MISSING_OR_INVALID_NONCE");
                    string detail = responseBody.Substring(idx, Math.Min(300, responseBody.Length - idx));
                    userMsg = detail;
                }
                else if (responseBody.Contains("ADMIN_ONLY_OPERATION"))
                    userMsg += "APPLE NOT ENABLED IN FIREBASE";
                else
                    userMsg += responseBody.Length > 120 ? responseBody.Substring(0, 120) : responseBody;

                OnAuthError?.Invoke(userMsg);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[FirebaseAuth] Exception in OnSignInComplete: {e}");
            OnAuthError?.Invoke($"Auth exception: {e.Message}");
        }
        finally
        {
            www.Dispose();
        }
    }

    /// <summary>
    /// Refresh the Firebase ID token using the refresh token (no coroutine)
    /// </summary>
    public void RefreshIdTokenAsync()
    {
        if (string.IsNullOrEmpty(refreshToken))
        {
            Debug.LogWarning("[FirebaseAuth] No refresh token available");
            return;
        }

        string url = $"https://securetoken.googleapis.com/v1/token?key={firebaseApiKey}";
        string body = $"grant_type=refresh_token&refresh_token={UnityWebRequest.EscapeURL(refreshToken)}";

        var www = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(body);
        www.uploadHandler = new UploadHandlerRaw(bodyRaw);
        www.downloadHandler = new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");
        www.timeout = 10;

        var op = www.SendWebRequest();
        op.completed += _ => OnRefreshComplete(www);
    }

    private void OnRefreshComplete(UnityWebRequest www)
    {
        try
        {
            if (www.result == UnityWebRequest.Result.Success)
            {
                var response = JsonUtility.FromJson<RefreshTokenResponse>(www.downloadHandler.text);
                idToken = response.id_token;
                refreshToken = response.refresh_token;
                userId = response.user_id;

                SaveAuth();
                Debug.Log($"[FirebaseAuth] ✓ Token refreshed for user: {userId}");
            }
            else
            {
                Debug.LogWarning($"[FirebaseAuth] Token refresh failed: HTTP {www.responseCode} {www.error}");
                Debug.LogWarning($"[FirebaseAuth] Refresh response: {www.downloadHandler?.text}");
                SignOut();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[FirebaseAuth] Refresh exception: {e.Message}");
            SignOut();
        }
        finally
        {
            www.Dispose();
        }
    }

    /// <summary>
    /// Sign out and clear all stored authentication
    /// </summary>
    public void SignOut()
    {
        userId = null;
        idToken = null;
        refreshToken = null;
        email = null;
        displayName = null;
        isAuthenticated = false;

        PlayerPrefs.DeleteKey(PREF_ID_TOKEN);
        PlayerPrefs.DeleteKey(PREF_REFRESH_TOKEN);
        PlayerPrefs.DeleteKey(PREF_USER_ID);
        PlayerPrefs.DeleteKey(PREF_EMAIL);
        PlayerPrefs.DeleteKey(PREF_DISPLAY_NAME);
        PlayerPrefs.Save();

        Debug.Log("[FirebaseAuth] Signed out");
        OnAuthStateChanged?.Invoke(false);
    }

    /// <summary>
    /// Get current user ID (Firebase UID or device ID fallback)
    /// </summary>
    public string GetUserId()
    {
        if (isAuthenticated && !string.IsNullOrEmpty(userId))
        {
            return userId;
        }

        // Fallback to device ID
        if (DeviceIDManager.Instance != null)
        {
            return DeviceIDManager.Instance.DeviceID;
        }

        return SystemInfo.deviceUniqueIdentifier;
    }

    [Serializable]
    private class FirebaseAuthResponse
    {
        public string kind;
        public string idToken;
        public string email;
        public string refreshToken;
        public string expiresIn;
        public string localId;
        public bool registered;
        public string displayName;
        public string photoUrl;
    }

    [Serializable]
    private class RefreshTokenResponse
    {
        public string id_token;
        public string refresh_token;
        public string user_id;
        public string expires_in;
        public string token_type;
        public string project_id;
    }

    [Serializable]
    private class FirebaseIdpRequest
    {
        public string postBody;
        public string requestUri;
        public bool returnSecureToken;
        public bool returnIdpCredential;
    }

    private static string Sha256(string input)
    {
        using (var sha = SHA256.Create())
        {
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            var sb = new System.Text.StringBuilder(64);
            foreach (byte b in bytes)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
