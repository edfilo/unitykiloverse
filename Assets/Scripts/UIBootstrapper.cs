using UnityEngine;

/// <summary>
/// Bootstraps K1L0 HUD and legacy UILayoutManager
/// </summary>
public class UIBootstrapper : MonoBehaviour
{
    void Awake()
    {
        // Ensure boot state is set (BootSequence/MinimalBoot not in scene after recovery)
        if (!BootState.AllowMap)
        {
            Debug.Log("[UIBootstrapper] Triggering MinimalBoot (BootSequence not in scene)");
            BootState.SetRenderAllowed();
            BootState.SetGPSAllowed();
            BootState.SetTeleportAllowed();
            BootState.SetMapAllowed();
            BootState.SetPlayerAllowed();
        }

        // Create K1L0 HUD
        K1L0HUD hud = FindFirstObjectByType<K1L0HUD>();
        if (hud == null)
        {
            Debug.Log("[UIBootstrapper] Creating K1L0HUD");
            GameObject hudGO = new GameObject("K1L0HUD");
            DontDestroyOnLoad(hudGO);
            hudGO.AddComponent<K1L0HUD>();
        }

        K1L0NativeOverlay.EnsureInstalled();

        // Disable old UILayoutManager if it exists (K1L0HUD replaces it)
        UILayoutManager layoutManager = FindFirstObjectByType<UILayoutManager>();
        if (layoutManager != null)
        {
            Debug.Log("[UIBootstrapper] Disabling old UILayoutManager (K1L0HUD replaces it)");
            layoutManager.enabled = false;
        }

        // Disable old PedometerCanvas if it exists
        GameObject oldCanvas = GameObject.Find("PedometerCanvas");
        if (oldCanvas != null)
        {
            oldCanvas.SetActive(false);
            Debug.Log("[UIBootstrapper] Disabled old PedometerCanvas");
        }
    }
}
