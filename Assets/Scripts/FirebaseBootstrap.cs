using System;
using System.Threading.Tasks;
using Firebase;
using Firebase.Extensions;
using UnityEngine;

// Initializes Firebase Unity SDK on app launch. Auth and Database become
// usable only after CheckAndFixDependenciesAsync resolves Available.
public class FirebaseBootstrap : MonoBehaviour
{
    public static bool IsReady { get; private set; }
    public static event Action OnReady;
    public static FirebaseApp App { get; private set; }

    private static FirebaseBootstrap _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoBoot()
    {
        if (_instance != null) return;
        var go = new GameObject("FirebaseBootstrap");
        _instance = go.AddComponent<FirebaseBootstrap>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        Debug.Log("[FirebaseBootstrap] Checking dependencies...");
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            var status = task.Result;
            if (status == DependencyStatus.Available)
            {
                App = FirebaseApp.DefaultInstance;
                IsReady = true;
                Debug.Log($"[FirebaseBootstrap] ✓ Firebase ready (app={App.Name}, project={App.Options.ProjectId})");
                OnReady?.Invoke();
            }
            else
            {
                Debug.LogError($"[FirebaseBootstrap] Dependencies not available: {status}");
            }
        });
    }

    // Run an action as soon as Firebase is ready (or immediately if already ready).
    public static void WhenReady(Action action)
    {
        if (action == null) return;
        if (IsReady) { action(); return; }
        OnReady += action;
    }
}
