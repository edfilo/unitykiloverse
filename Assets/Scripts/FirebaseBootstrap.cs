using System;
using UnityEngine;

// Compatibility shim. The Firebase Unity SDK is no longer initialized from C#;
// native Swift/session/API layers own auth and data access.
public class FirebaseBootstrap : MonoBehaviour
{
    public static bool IsReady { get; private set; } = true;
    public static event Action OnReady;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoBoot()
    {
        IsReady = true;
        OnReady?.Invoke();
    }

    public static void WhenReady(Action action)
    {
        action?.Invoke();
    }
}
