using UnityEngine;

/// <summary>
/// Manages player helmet appearance based on KiloSettings
/// </summary>
public class HelmetManager : MonoBehaviour
{
    private GameObject helmetObject;
    private KiloWorld.Rendering.KiloWorldMasterProfile profile;
    private Vector3 lastScale;
    private Vector3 lastPosition;
    private Vector3 lastRotation;
    private bool lastVisibility;

    void Start()
    {
        // Delay to let ForceHelmet create the helmet first
        Invoke(nameof(Initialize), 0.1f);
    }

    void Initialize()
    {
        // Find helmet object (ForceHelmet should have created it by now)
        FindHelmet();

        // Load profile
        profile = KiloWorld.Rendering.Systems.RenderManager.Instance?.profile;
        if (profile == null)
        {
            Debug.LogError("[HelmetManager] Profile not found! RenderManager.Instance = " + (KiloWorld.Rendering.Systems.RenderManager.Instance != null ? "exists" : "null"));
            return;
        }

        Debug.Log($"[HelmetManager] Profile loaded. Helmet settings - Scale: {profile.helmet.scale}, Rotation: {profile.helmet.rotation}");

        // Apply initial settings
        ApplyHelmetSettings();

        // Store last values
        StoreLastSettings();
    }

    void Update()
    {
        if (profile == null || helmetObject == null) return;

        // Apply settings every frame in editor for live tweaking (without logging)
        #if UNITY_EDITOR
        ApplyHelmetSettingsQuiet();
        #else
        // In build, only apply if settings changed
        if (HasSettingsChanged())
        {
            ApplyHelmetSettingsQuiet();
            StoreLastSettings();
        }
        #endif
    }

void ApplyHelmetSettingsQuiet()
    {
        if (helmetObject == null || profile == null) return;

        var settings = profile.helmet;

        // Apply scale
        helmetObject.transform.localScale = Vector3.one * settings.scale;

        // Apply visibility
        helmetObject.SetActive(settings.showHelmet);

        // Update HelmetFollower component if present
        // Apply local transforms - helmet is parented to head bone
        helmetObject.transform.localPosition = settings.positionOffset;
        helmetObject.transform.localRotation = Quaternion.Euler(settings.rotation);

        // Remove old HelmetFollower if present (legacy)
        HelmetFollower follower = helmetObject.GetComponent<HelmetFollower>();
        if (follower != null)
        {
            Destroy(follower);
        }
    }

    void FindHelmet()
    {
        // Try multiple possible locations
        string[] possibleNames = { "PlayerHelmet", "BikerHelmet", "Biker_Halmet3", "Biker_Helmet3", "helmet", "Helmet" };

        // First try direct GameObject.Find
        foreach (string name in possibleNames)
        {
            helmetObject = GameObject.Find(name);
            if (helmetObject != null)
            {
                Debug.Log($"[HelmetManager] Found helmet by name: {helmetObject.name} at {helmetObject.transform.position}");
                return;
            }
        }

        // Try finding as child of player
        GameObject player = GameObject.Find("Player");
        if (player != null)
        {
            Debug.Log($"[HelmetManager] Searching in Player's children...");
            foreach (string name in possibleNames)
            {
                Transform helmetTransform = player.transform.Find(name);
                if (helmetTransform != null)
                {
                    helmetObject = helmetTransform.gameObject;
                    Debug.Log($"[HelmetManager] Found helmet as child of Player: {helmetObject.name}");
                    return;
                }
            }

            // Search recursively in all children
            foreach (Transform child in player.GetComponentsInChildren<Transform>(true))
            {
                if (child.name.ToLower().Contains("helmet") || child.name.ToLower().Contains("halmet"))
                {
                    helmetObject = child.gameObject;
                    Debug.Log($"[HelmetManager] Found helmet recursively: {helmetObject.name} at path: {GetFullPath(child)}");
                    return;
                }
            }

            // Check ForceHelmet script
            ForceHelmet forceHelmet = player.GetComponent<ForceHelmet>();
            if (forceHelmet != null)
            {
                helmetObject = forceHelmet.GetCreatedHelmet();
                if (helmetObject != null)
                {
                    Debug.Log($"[HelmetManager] Found helmet from ForceHelmet component: {helmetObject.name}");
                    return;
                }
            }
        }

        Debug.LogWarning("[HelmetManager] Helmet object not found! Searched for: " + string.Join(", ", possibleNames));
    }

    string GetFullPath(Transform transform)
    {
        string path = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }
        return path;
    }

void ApplyHelmetSettings()
    {
        if (helmetObject == null || profile == null) return;

        var settings = profile.helmet;

        // Debug current state
        Debug.Log($"[HelmetManager] Applying settings:\n" +
                 $"  Scale: {settings.scale}\n" +
                 $"  Position Offset: {settings.positionOffset}\n" +
                 $"  Rotation: {settings.rotation}\n" +
                 $"  Show Helmet: {settings.showHelmet}\n" +
                 $"  Current localScale: {helmetObject.transform.localScale}\n" +
                 $"  Current position: {helmetObject.transform.position}\n" +
                 $"  Active: {helmetObject.activeSelf}");

        // Apply scale
        helmetObject.transform.localScale = Vector3.one * settings.scale;

        // Apply visibility
        helmetObject.SetActive(settings.showHelmet);

        // Update HelmetFollower component if present
        // Apply local transforms - helmet is parented to head bone
        helmetObject.transform.localPosition = settings.positionOffset;
        helmetObject.transform.localRotation = Quaternion.Euler(settings.rotation);

        // Remove old HelmetFollower if present (legacy)
        HelmetFollower follower = helmetObject.GetComponent<HelmetFollower>();
        if (follower != null)
        {
            Debug.Log($"[HelmetManager] Removing legacy HelmetFollower component");
            Destroy(follower);
        }

        Debug.Log($"[HelmetManager] After applying:\n" +
                 $"  localScale: {helmetObject.transform.localScale}\n" +
                 $"  position: {helmetObject.transform.position}\n" +
                 $"  rotation: {helmetObject.transform.rotation.eulerAngles}");
    }

    bool HasSettingsChanged()
    {
        var settings = profile.helmet;
        return settings.scale != lastScale.x ||
               settings.positionOffset != lastPosition ||
               settings.rotation != lastRotation ||
               settings.showHelmet != lastVisibility;
    }

    void StoreLastSettings()
    {
        var settings = profile.helmet;
        lastScale = Vector3.one * settings.scale;
        lastPosition = settings.positionOffset;
        lastRotation = settings.rotation;
        lastVisibility = settings.showHelmet;
    }

    // Public method to manually refresh settings
    [ContextMenu("Refresh Helmet Settings")]
    public void RefreshSettings()
    {
        FindHelmet();
        if (profile == null)
        {
            profile = KiloWorld.Rendering.Systems.RenderManager.Instance?.profile;
        }

        if (helmetObject != null && profile != null)
        {
            Debug.Log($"[HelmetManager] Manual refresh - Found helmet: {helmetObject.name}");
            ApplyHelmetSettings();
            StoreLastSettings();
        }
        else
        {
            Debug.LogWarning($"[HelmetManager] Manual refresh failed - Helmet: {(helmetObject != null ? helmetObject.name : "null")}, Profile: {(profile != null ? "found" : "null")}");
        }
    }

    [ContextMenu("Reset Helmet to Default")]
    public void ResetToDefault()
    {
        if (helmetObject == null)
        {
            Debug.LogWarning("[HelmetManager] No helmet to reset!");
            return;
        }

        helmetObject.transform.localScale = Vector3.one;
        HelmetFollower follower = helmetObject.GetComponent<HelmetFollower>();
        if (follower != null)
        {
            follower.offset = Vector3.zero;
            follower.rotationOffset = Quaternion.identity;
        }
        helmetObject.SetActive(true);
        Debug.Log("[HelmetManager] Reset helmet to default (scale 1, no offset, no rotation)");
    }

    [ContextMenu("Log Helmet Info")]
    public void LogHelmetInfo()
    {
        if (helmetObject != null)
        {
            Debug.Log($"[HelmetManager] Helmet Info:\n" +
                     $"Name: {helmetObject.name}\n" +
                     $"Path: {GetFullPath(helmetObject.transform)}\n" +
                     $"Local Scale: {helmetObject.transform.localScale}\n" +
                     $"Local Position: {helmetObject.transform.localPosition}\n" +
                     $"Local Rotation: {helmetObject.transform.localRotation.eulerAngles}\n" +
                     $"Active: {helmetObject.activeSelf}");
        }
        else
        {
            Debug.LogWarning("[HelmetManager] No helmet object found!");
        }

        if (profile != null)
        {
            Debug.Log($"[HelmetManager] Profile Settings:\n" +
                     $"Scale: {profile.helmet.scale}\n" +
                     $"Position: {profile.helmet.positionOffset}\n" +
                     $"Rotation: {profile.helmet.rotation}\n" +
                     $"Visible: {profile.helmet.showHelmet}");
        }
    }
}
