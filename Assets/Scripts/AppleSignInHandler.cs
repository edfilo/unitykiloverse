using UnityEngine;
using System.Runtime.InteropServices;
using System;

/// <summary>
/// Handles Apple Sign-In for iOS
/// Uses native iOS code to trigger Sign in with Apple
/// </summary>
public class AppleSignInHandler : MonoBehaviour
{
    #if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void _SignInWithApple(string callbackObject);

    [DllImport("__Internal")]
    private static extern bool _IsAppleSignInAvailable();
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

    public event Action<string, string, string, string> OnAppleSignInSuccess; // idToken, nonce, fullName, appleUserId
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

    /// <summary>
    /// Check if Sign in with Apple is available on this device
    /// </summary>
    public bool IsAvailable()
    {
        #if UNITY_IOS && !UNITY_EDITOR
        return _IsAppleSignInAvailable();
        #else
        return false;
        #endif
    }

    /// <summary>
    /// Trigger Sign in with Apple flow
    /// </summary>
    public void SignIn()
    {
        #if UNITY_IOS && !UNITY_EDITOR
        _SignInWithApple(gameObject.name);
        Debug.Log("[AppleSignIn] Triggered native Sign in with Apple");
        #else
        Debug.LogWarning("[AppleSignIn] Not available in Editor - simulating success");
        // Simulate success in Editor for testing
        SimulateEditorSignIn();
        #endif
    }

    /// <summary>
    /// Called by native iOS code when sign-in succeeds
    /// Message format: "idToken|nonce|fullName"
    /// </summary>
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

    /// <summary>
    /// Called by native iOS code when sign-in fails
    /// </summary>
    public void OnAppleSignInError(string error)
    {
        Debug.LogError($"[AppleSignIn] Error: {error}");
        OnAppleSignInFailed?.Invoke(error);
    }

    /// <summary>
    /// Simulate Apple Sign-In in Unity Editor for testing
    /// </summary>
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
