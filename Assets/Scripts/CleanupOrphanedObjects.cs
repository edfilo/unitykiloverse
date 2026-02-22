using UnityEngine;

/// <summary>
/// Removes orphaned GameObjects that have missing scripts and can't be deleted normally.
/// Add this to any GameObject and run "Cleanup Now" from context menu.
/// </summary>
public class CleanupOrphanedObjects : MonoBehaviour
{
    [ContextMenu("Cleanup _RoadSetup and RoadStreetLightSystem")]
    void CleanupOrphanedRoadObjects()
    {
        int cleaned = 0;

        // Find and destroy _RoadSetup
        GameObject roadSetup = GameObject.Find("_RoadSetup");
        if (roadSetup != null)
        {
            LogMissingScriptsOnObject(roadSetup);
            Debug.Log($"<color=yellow>[Cleanup] Destroying {roadSetup.name}</color>");
            DestroyImmediate(roadSetup);
            cleaned++;
        }

        // Find and destroy RoadStreetLightSystem
        GameObject streetLights = GameObject.Find("RoadStreetLightSystem");
        if (streetLights != null)
        {
            LogMissingScriptsOnObject(streetLights);
            Debug.Log($"<color=yellow>[Cleanup] Destroying {streetLights.name}</color>");
            DestroyImmediate(streetLights);
            cleaned++;
        }

        Debug.Log($"<color=green>[Cleanup] Removed {cleaned} orphaned object(s)!</color>");
    }

    void LogMissingScriptsOnObject(GameObject obj)
    {
        Component[] components = obj.GetComponents<Component>();
        int missingCount = 0;

        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] == null)
            {
                Debug.LogWarning($"  → Missing script (component index {i}) on: {obj.name}", obj);
                missingCount++;
            }
        }

        if (missingCount > 0)
        {
            Debug.LogWarning($"  → Total missing scripts on {obj.name}: {missingCount}");
        }
        else
        {
            Debug.Log($"  → No missing scripts on {obj.name}");
        }
    }

    [ContextMenu("Find All Objects With Missing Scripts")]
    void FindObjectsWithMissingScripts()
    {
        GameObject[] allObjects = FindObjectsOfType<GameObject>();
        int count = 0;

        foreach (GameObject obj in allObjects)
        {
            Component[] components = obj.GetComponents<Component>();
            foreach (Component comp in components)
            {
                if (comp == null) // Missing script
                {
                    Debug.LogWarning($"Missing script on: {GetGameObjectPath(obj)}", obj);
                    count++;
                }
            }
        }

        Debug.Log($"<color=cyan>[Cleanup] Found {count} missing script(s)</color>");
    }

    string GetGameObjectPath(GameObject obj)
    {
        string path = obj.name;
        Transform parent = obj.transform.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        return path;
    }
}
