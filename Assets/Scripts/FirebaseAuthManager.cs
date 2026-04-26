using UnityEngine;
using System;
using System.Threading.Tasks;
using Firebase.Auth;
using Firebase.Extensions;

// Firebase Authentication via the official Firebase Unity SDK.
// SignInWithApple exchanges an Apple ID token for a real Firebase credential,
// so idToken below is a Firebase-minted token accepted by RTDB security rules.
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

    [Header("Auth State")]
    public bool isAuthenticated = false;
    public string userId = null;
    public string idToken = null;
    public string refreshToken = null; // kept for API compatibility; Firebase SDK manages refresh internally
    public string email = null;
    public string displayName = null;

    public event Action<bool> OnAuthStateChanged;
    public event Action<string> OnAuthError;

    private const string PREF_USER_ID = "FirebaseUserId";
    private const string PREF_EMAIL = "FirebaseEmail";
    private const string PREF_DISPLAY_NAME = "FirebaseDisplayName";

    private FirebaseAuth _auth;
    private string _pendingAppleFullName;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        // Restore cached profile fields immediately so UI can render while SDK boots.
        email = PlayerPrefs.GetString(PREF_EMAIL, "");
        displayName = PlayerPrefs.GetString(PREF_DISPLAY_NAME, "");
        userId = PlayerPrefs.GetString(PREF_USER_ID, "");

        FirebaseBootstrap.WhenReady(OnFirebaseReady);
    }

    private void OnFirebaseReady()
    {
        _auth = FirebaseAuth.DefaultInstance;
        _auth.StateChanged += OnAuthStateChangedInternal;
        // Seed state from whatever the SDK already has (persists across launches).
        OnAuthStateChangedInternal(_auth, EventArgs.Empty);
    }

    private void OnDestroy()
    {
        if (_auth != null)
        {
            _auth.StateChanged -= OnAuthStateChangedInternal;
        }
    }

    private void OnAuthStateChangedInternal(object sender, EventArgs e)
    {
        var user = _auth?.CurrentUser;
        if (user != null)
        {
            userId = user.UserId;
            email = user.Email ?? "";
            if (!string.IsNullOrEmpty(user.DisplayName)) displayName = user.DisplayName;
            isAuthenticated = true;
            SaveAuth();
            Debug.Log($"[FirebaseAuth] StateChanged → signed in: {userId}");
            // Fetch a fresh ID token so other systems (RTDB REST, backend) can use it.
            user.TokenAsync(false).ContinueWithOnMainThread((Task<string> t) =>
            {
                if (t.IsFaulted || t.IsCanceled)
                {
                    Debug.LogWarning($"[FirebaseAuth] TokenAsync failed: {t.Exception?.Message}");
                }
                else
                {
                    idToken = t.Result;
                }
                OnAuthStateChanged?.Invoke(true);
            });
        }
        else
        {
            isAuthenticated = false;
            idToken = null;
            Debug.Log("[FirebaseAuth] StateChanged → signed out");
            OnAuthStateChanged?.Invoke(false);
        }
    }

    public void SaveAuthPublic() => SaveAuth();

    private void SaveAuth()
    {
        PlayerPrefs.SetString(PREF_USER_ID, userId ?? "");
        PlayerPrefs.SetString(PREF_EMAIL, email ?? "");
        PlayerPrefs.SetString(PREF_DISPLAY_NAME, displayName ?? "");
        PlayerPrefs.Save();
    }

    public void SignInWithApple(string appleIdToken, string appleNonce, string appleFullName = null)
    {
        _pendingAppleFullName = appleFullName;
        FirebaseBootstrap.WhenReady(() => SignInWithAppleInternal(appleIdToken, appleNonce));
    }

    private void SignInWithAppleInternal(string appleIdToken, string appleRawNonce)
    {
        Debug.Log($"[FirebaseAuth] SignInWithApple via SDK (idToken len={appleIdToken?.Length}, nonce len={appleRawNonce?.Length})");
        LogJwtClaims(appleIdToken, appleRawNonce);
        Credential credential;
        try
        {
            // Firebase Unity SDK expects the RAW nonce — it hashes server-side to compare
            // against the hashed nonce embedded in Apple's id_token.
            credential = OAuthProvider.GetCredential("apple.com", appleIdToken, appleRawNonce, null);
        }
        catch (Exception e)
        {
            Debug.LogError($"[FirebaseAuth] GetCredential failed: {e}");
            OnAuthError?.Invoke($"credential error: {e.Message}");
            return;
        }

        _auth.SignInWithCredentialAsync(credential).ContinueWithOnMainThread((Task<FirebaseUser> task) =>
        {
            if (task.IsCanceled)
            {
                OnAuthError?.Invoke("sign-in canceled");
                return;
            }
            if (task.IsFaulted)
            {
                string msg = task.Exception?.GetBaseException()?.Message ?? "sign-in failed";
                Debug.LogError($"[FirebaseAuth] SignInWithCredential failed: {task.Exception}");
                OnAuthError?.Invoke(msg);
                return;
            }

            var user = task.Result;
            userId = user.UserId;
            email = user.Email ?? "";
            displayName = !string.IsNullOrEmpty(user.DisplayName) ? user.DisplayName : (_pendingAppleFullName ?? "");
            isAuthenticated = true;
            SaveAuth();

            if (DeviceIDManager.Instance != null)
            {
                PlayerPrefs.SetString("FirebaseUID_" + DeviceIDManager.Instance.DeviceID, userId);
                PlayerPrefs.Save();
            }

            Debug.Log($"[FirebaseAuth] ✓ Signed in: {userId}");

            user.TokenAsync(false).ContinueWithOnMainThread((Task<string> t) =>
            {
                if (!t.IsFaulted && !t.IsCanceled) idToken = t.Result;
                OnAuthStateChanged?.Invoke(true);
            });
        });
    }

    // Dev-mode sign-in used on Mac/Editor where Apple Sign-In isn't wired up.
    // Produces a real Firebase user with a valid ID token so RTDB rules pass.
    public void SignInAnonymously()
    {
        FirebaseBootstrap.WhenReady(() =>
        {
            if (_auth.CurrentUser != null)
            {
                Debug.Log($"[FirebaseAuth] SignInAnonymously: already signed in as {_auth.CurrentUser.UserId}");
                return;
            }
            _auth.SignInAnonymouslyAsync().ContinueWithOnMainThread((Task<AuthResult> task) =>
            {
                if (task.IsCanceled || task.IsFaulted)
                {
                    string msg = task.Exception?.GetBaseException()?.Message ?? "anonymous sign-in failed";
                    Debug.LogError($"[FirebaseAuth] SignInAnonymously failed: {task.Exception}");
                    OnAuthError?.Invoke(msg);
                    return;
                }
                Debug.Log($"[FirebaseAuth] ✓ Anonymous sign-in: {task.Result.User.UserId}");
            });
        });
    }

    // Forces a fresh Firebase ID token. Other systems call this before making
    // authenticated requests that may have been rejected with 401.
    public void RefreshIdTokenAsync()
    {
        if (_auth?.CurrentUser == null)
        {
            Debug.LogWarning("[FirebaseAuth] RefreshIdTokenAsync: no current user");
            return;
        }
        _auth.CurrentUser.TokenAsync(true).ContinueWithOnMainThread((Task<string> t) =>
        {
            if (t.IsFaulted || t.IsCanceled)
            {
                Debug.LogWarning($"[FirebaseAuth] Token refresh failed: {t.Exception?.GetBaseException()?.Message}");
                return;
            }
            idToken = t.Result;
            Debug.Log("[FirebaseAuth] ✓ Token refreshed");
        });
    }

    public void SignOut()
    {
        if (_auth != null) _auth.SignOut();

        userId = null;
        idToken = null;
        refreshToken = null;
        email = null;
        displayName = null;
        isAuthenticated = false;

        PlayerPrefs.DeleteKey(PREF_USER_ID);
        PlayerPrefs.DeleteKey(PREF_EMAIL);
        PlayerPrefs.DeleteKey(PREF_DISPLAY_NAME);
        PlayerPrefs.Save();

        Debug.Log("[FirebaseAuth] Signed out");
        OnAuthStateChanged?.Invoke(false);
    }

    private static void LogJwtClaims(string jwt, string rawNonce)
    {
        try
        {
            var parts = jwt?.Split('.');
            if (parts == null || parts.Length != 3) { Debug.LogWarning("[FirebaseAuth] id_token is not a 3-part JWT"); return; }
            string p = parts[1].Replace('-', '+').Replace('_', '/');
            int pad = p.Length % 4; if (pad != 0) p += new string('=', 4 - pad);
            string json = System.Text.Encoding.UTF8.GetString(System.Convert.FromBase64String(p));
            Debug.Log($"[FirebaseAuth] id_token claims: {json}");
            Debug.Log($"[FirebaseAuth] raw nonce passed to Firebase: '{rawNonce}'");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[FirebaseAuth] JWT decode failed: {e.Message}");
        }
    }

    public string GetUserId()
    {
        if (isAuthenticated && !string.IsNullOrEmpty(userId)) return userId;
        if (DeviceIDManager.Instance != null) return DeviceIDManager.Instance.DeviceID;
        return SystemInfo.deviceUniqueIdentifier;
    }
}
