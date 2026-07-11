using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class RuntimeLabelSetup : MonoBehaviour
{
    void Start()
    {
        Setup();
    }

    public void Setup()
    {
        Debug.Log("[RuntimeLabelSetup] Starting setup...");

        // 1. Create LabelManager if missing
        LabelManager manager = FindObjectOfType<LabelManager>();
        if (manager == null)
        {
            GameObject managerGO = new GameObject("LabelManager");
            manager = managerGO.AddComponent<LabelManager>();
            Debug.Log("[RuntimeLabelSetup] Created LabelManager in scene.");
        }

        // 1a. LocationsPanel removed - now created by UILayoutManager
        // (UILayoutManager handles the new modular layout system)

        // 1b. Create OnboardingUI (Always create fresh to ensure it covers everything)
        if (FindObjectOfType<OnboardingUI>() == null)
        {
            GameObject onboardingGO = new GameObject("OnboardingUI");
            onboardingGO.AddComponent<OnboardingUI>();
            Debug.Log("[RuntimeLabelSetup] Created OnboardingUI.");
        }

        // 1c. Network/presence data is owned by the native iOS layer and backend.
        // Unity only creates render/bootstrap helpers here.

        // DISABLED: UserPresenceManager creation moved to GPSLocationController.EnsureServices()
        // (only after GPS is ready, to prevent sending bogus (0,0) coordinates)
        /*
        if (FindObjectOfType<UserPresenceManager>() == null)
        {
            GameObject presenceGO = new GameObject("UserPresenceManager");
            presenceGO.AddComponent<UserPresenceManager>();
            Debug.Log("[RuntimeLabelSetup] Created UserPresenceManager.");
        }
        */
        Debug.Log("[RuntimeLabelSetup] UserPresenceManager will be created by GPSLocationController after GPS is ready.");

        // 2. Check/Create Prefab
        // We can't easily create a prefab asset at runtime in a build, but in Editor we can.
#if UNITY_EDITOR
        string prefabPath = "Assets/Prefabs/EntityLabelPrefab.prefab";
        string prefabDir = "Assets/Prefabs";
        if (!System.IO.Directory.Exists(prefabDir)) System.IO.Directory.CreateDirectory(prefabDir);

        GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        
        if (existingPrefab == null)
        {
            if (Application.isPlaying)
            {
                Debug.LogError("[RuntimeLabelSetup] EntityLabelPrefab missing, but skipping prefab creation during Play Mode to avoid editor stalls.");
                return;
            }
            // Create compact label prefab (80x20 pixels)
            GameObject prefabGO = new GameObject("EntityLabelPrefab");
            RectTransform rootRT = prefabGO.AddComponent<RectTransform>();
            rootRT.sizeDelta = new Vector2(80, 20); // Compact size
            rootRT.pivot = new Vector2(0.5f, 0.5f);
            prefabGO.AddComponent<CanvasRenderer>();
            
            EntityLabel entityLabel = prefabGO.AddComponent<EntityLabel>();

            // Background (Black)
            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(prefabGO.transform);
            Image bgImg = bgObj.AddComponent<Image>();
            bgImg.color = Color.black;
            RectTransform bgRT = bgObj.GetComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.sizeDelta = Vector2.zero;
            bgRT.anchoredPosition = Vector2.zero;

            // Name Text (White, properly sized)
            GameObject nameObj = new GameObject("NameText");
            nameObj.transform.SetParent(prefabGO.transform);
            TextMeshProUGUI tmp = nameObj.AddComponent<TextMeshProUGUI>();
            tmp.text = "0.0m";
            tmp.fontSize = 12;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.enableAutoSizing = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            
            RectTransform nameRT = nameObj.GetComponent<RectTransform>();
            nameRT.anchorMin = Vector2.zero;
            nameRT.anchorMax = Vector2.one;
            nameRT.sizeDelta = Vector2.zero;
            nameRT.anchoredPosition = Vector2.zero;
            entityLabel.nameText = tmp;

            // Health bar (hidden, just for API compatibility)
            GameObject barFillObj = new GameObject("HealthBarFill");
            barFillObj.transform.SetParent(prefabGO.transform);
            Image fillImg = barFillObj.AddComponent<Image>();
            fillImg.enabled = false; // Completely disable
            RectTransform fillRT = barFillObj.GetComponent<RectTransform>();
            fillRT.sizeDelta = Vector2.zero;
            entityLabel.healthBar = fillImg;

            // Save as Prefab
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(prefabGO, prefabPath);
            Destroy(prefabGO); // Destroy the scene instance
            
            manager.labelPrefab = savedPrefab;
            Debug.Log($"[RuntimeLabelSetup] Created and assigned prefab at {prefabPath}");
        }
        else
        {
            if (manager.labelPrefab == null)
            {
                manager.labelPrefab = existingPrefab;
                Debug.Log("[RuntimeLabelSetup] Assigned existing prefab to LabelManager.");
            }
        }
#endif
        Debug.Log("[RuntimeLabelSetup] Setup Complete! You can now stop play mode.");
    }
}
