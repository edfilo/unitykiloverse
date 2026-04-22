using UnityEngine;
using System.Runtime.InteropServices;
using System;
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
using AOT;
#endif

public class AppleSignInHandler : MonoBehaviour
{
    #if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void _SignInWithApple(string callbackObject);

    [DllImport("__Internal")]
    private static extern bool _IsAppleSignInAvailable();
    #elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
    private delegate void AppleSignInCallback(IntPtr resultPtr);

    [DllImport("AppleSignIn")]
    private static extern void _SignInWithApple(AppleSignInCallback onSuccess, AppleSignInCallback onError);

    [DllImport("AppleSignIn")]
    private static extern bool _IsAppleSignInAvailable();

    // Keep delegate refs alive — IL2CPP won't pin lambdas.
    private static readonly AppleSignInCallback _successDelegate = OnNativeSuccess;
    private static readonly AppleSignInCallback _errorDelegate = OnNativeError;

    [MonoPInvokeCallback(typeof(AppleSignInCallback))]
    private static void OnNativeSuccess(IntPtr resultPtr)
    {
        string result = resultPtr != IntPtr.Zero ? Marshal.PtrToStringAnsi(resultPtr) : "";
        if (_instance != null) _instance.OnAppleSignInCallback(result);
    }

    [MonoPInvokeCallback(typeof(AppleSignInCallback))]
    private static void OnNativeError(IntPtr errorPtr)
    {
        string error = errorPtr != IntPtr.Zero ? Marshal.PtrToStringAnsi(errorPtr) : "Unknown error";
        if (_instance != null) _instance.OnAppleSignInError(error);
    }
    #endif

    private static AppleSignInHandler _instance;
    public static AppleSignInHandler Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("AppleSignInHandler");
                _instance = go.AddComponent<AppleSignInHandler>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    public event Action<string, string, string, string> OnAppleSignInSuccess;
    public event Action<string> OnAppleSignInFailed;

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

    public bool IsAvailable()
    {
        #if (UNITY_IOS || UNITY_STANDALONE_OSX) && !UNITY_EDITOR
        return _IsAppleSignInAvailable();
        #else
        return false;
        #endif
    }

    public void SignIn()
    {
        #if UNITY_IOS && !UNITY_EDITOR
        _SignInWithApple(gameObject.name);
        Debug.Log("[AppleSignIn] Triggered native Sign in with Apple");
        #elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
        _SignInWithApple(_successDelegate, _errorDelegate);
        Debug.Log("[AppleSignIn] Triggered native Sign in with Apple (Mac)");
        #else
        Debug.LogWarning("[AppleSignIn] Not available in Editor - simulating success");
        SimulateEditorSignIn();
        #endif
    }

    public void OnAppleSignInCallback(string result)
    {
        Debug.Log($"[AppleSignIn] Received callback: {result}");

        if (string.IsNullOrEmpty(result))
        {
            OnAppleSignInFailed?.Invoke("Empty response from Apple");
            return;
        }

        try
        {
            string[] parts = result.Split('|');
            if (parts.Length >= 2)
            {
                string idToken = parts[0];
                string nonce = parts[1];
                string fullName = parts.Length > 2 ? parts[2] : "";
                string appleUserId = parts.Length > 3 ? parts[3] : "";

                Debug.Log($"[AppleSignIn] ✓ Sign-in successful for: {fullName}, appleUserId: {appleUserId}");
                OnAppleSignInSuccess?.Invoke(idToken, nonce, fullName, appleUserId);
            }
            else
            {
                OnAppleSignInFailed?.Invoke("Invalid response format");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[AppleSignIn] Failed to parse response: {e.Message}");
            OnAppleSignInFailed?.Invoke(e.Message);
        }
    }

    public void OnAppleSignInError(string error)
    {
        Debug.LogError($"[AppleSignIn] Error: {error}");
        OnAppleSignInFailed?.Invoke(error);
    }

    private void SimulateEditorSignIn()
    {
        #if UNITY_EDITOR
        string fakeIdToken = "EDITOR_TEST_TOKEN_" + Guid.NewGuid().ToString();
        string fakeNonce = Guid.NewGuid().ToString();
        string fakeName = "Test User";

        Debug.Log($"[AppleSignIn] [EDITOR] Simulating sign-in with fake credentials");
        OnAppleSignInSuccess?.Invoke(fakeIdToken, fakeNonce, fakeName, "EDITOR_APPLE_USER");
        #endif
    }
}
