using UnityEngine;

/// <summary>
/// Bootstraps K1L0 HUD and legacy UILayoutManager
/// </summary>
public class UIBootstrapper : MonoBehaviour
{
    void Awake()
    {
        // Create K1L0 HUD
        K1L0HUD hud = FindFirstObjectByType<K1L0HUD>();
        if (hud == null)
        {
            Debug.Log("[UIBootstrapper] Creating K1L0HUD");
            GameObject hudGO = new GameObject("K1L0HUD");
            DontDestroyOnLoad(hudGO);
            hudGO.AddComponent<K1L0HUD>();
        }

        // Keep UILayoutManager for WeatherView/LocationLabelUI data flow
        UILayoutManager layoutManager = FindFirstObjectByType<UILayoutManager>();
        if (layoutManager == null)
        {
            Debug.Log("[UIBootstrapper] Creating UILayoutManager");
            GameObject layoutManagerGO = new GameObject("UILayoutManager");
            layoutManager = layoutManagerGO.AddComponent<UILayoutManager>();
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
