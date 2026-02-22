using UnityEngine;

/// <summary>
/// Ensures UILayoutManager exists at runtime
/// </summary>
public class UIBootstrapper : MonoBehaviour
{
    void Awake()
    {
        // Check if UILayoutManager exists
        UILayoutManager layoutManager = FindFirstObjectByType<UILayoutManager>();
        if (layoutManager == null)
        {
            Debug.Log("[UIBootstrapper] Creating UILayoutManager");
            GameObject layoutManagerGO = new GameObject("UILayoutManager");
            layoutManager = layoutManagerGO.AddComponent<UILayoutManager>();
        }
        else
        {
            Debug.Log("[UIBootstrapper] UILayoutManager already exists");
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
