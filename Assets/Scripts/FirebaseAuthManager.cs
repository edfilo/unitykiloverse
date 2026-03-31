using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System;

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
            StartCoroutine(RefreshIdToken());
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
    public void SignInWithApple(string appleIdToken, string appleNonce, string appleFullName = null)
    {
        StartCoroutine(SignInWithAppleCoroutine(appleIdToken, appleNonce, appleFullName));
    }

    private IEnumerator SignInWithAppleCoroutine(string appleIdToken, string appleNonce, string appleFullName)
    {
        Debug.Log("[FirebaseAuth] Signing in with Apple via Firebase REST API...");

        // Call Firebase Auth REST API directly to sign in with Apple OAuth credential
        string url = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithIdp?key={firebaseApiKey}";

        // Build request body for Firebase OAuth sign-in
        // postBody is a form-encoded string; JWTs are base64url-safe (no special JSON chars)
        string postBody = $"id_token={appleIdToken}&nonce={appleNonce}&providerId=apple.com";
        string requestUri = "https://appleid.apple.com";

        Debug.Log($"[FirebaseAuth] idToken length={appleIdToken?.Length}, nonce length={appleNonce?.Length}");
        Debug.Log($"[FirebaseAuth] postBody length={postBody.Length}");

        string json = JsonUtility.ToJson(new FirebaseIdpRequest {
            postBody = postBody,
            requestUri = requestUri,
            returnSecureToken = true,
            returnIdpCredential = true
        });

        using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            www.timeout = 10;

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    Debug.Log($"[FirebaseAuth] Raw response: {www.downloadHandler.text}");
                    var authResponse = JsonUtility.FromJson<FirebaseAuthResponse>(www.downloadHandler.text);

                    userId = authResponse.localId;
                    idToken = authResponse.idToken;
                    refreshToken = authResponse.refreshToken;
                    email = authResponse.email ?? "";
                    displayName = authResponse.displayName ?? appleFullName ?? "";

                    isAuthenticated = true;
                    SaveAuth();

                    Debug.Log($"[FirebaseAuth] ✓ Signed in successfully: {userId}");
                    OnAuthStateChanged?.Invoke(true);

                    // Update DeviceIDManager to use Firebase UID
                    if (DeviceIDManager.Instance != null)
                    {
                        // Store mapping between device ID and Firebase UID
                        PlayerPrefs.SetString("FirebaseUID_" + DeviceIDManager.Instance.DeviceID, userId);
                        PlayerPrefs.Save();
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[FirebaseAuth] Failed to parse auth response: {e.Message}");
                    Debug.LogError($"[FirebaseAuth] Response was: {www.downloadHandler.text}");
                    OnAuthError?.Invoke("Authentication failed");
                }
            }
            else
            {
                Debug.LogError($"[FirebaseAuth] Sign in failed: {www.error}");
                Debug.LogError($"[FirebaseAuth] Response: {www.downloadHandler.text}");
                OnAuthError?.Invoke(www.error);
            }
        }
    }

    /// <summary>
    /// Refresh the Firebase ID token using the refresh token
    /// </summary>
    public IEnumerator RefreshIdToken()
    {
        if (string.IsNullOrEmpty(refreshToken))
        {
            Debug.LogWarning("[FirebaseAuth] No refresh token available");
            yield break;
        }

        string url = $"https://securetoken.googleapis.com/v1/token?key={firebaseApiKey}";
        string json = $@"{{
            ""grant_type"": ""refresh_token"",
            ""refresh_token"": ""{refreshToken}""
        }}";

        using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            www.timeout = 10;

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var response = JsonUtility.FromJson<RefreshTokenResponse>(www.downloadHandler.text);
                    idToken = response.id_token;
                    refreshToken = response.refresh_token;
                    userId = response.user_id;

                    SaveAuth();
                    Debug.Log($"[FirebaseAuth] ✓ Token refreshed for user: {userId}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[FirebaseAuth] Failed to parse refresh response: {e.Message}");
                }
            }
            else
            {
                Debug.LogWarning($"[FirebaseAuth] Token refresh failed: {www.error}");
                // Clear stored auth if refresh fails
                SignOut();
            }
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
}
