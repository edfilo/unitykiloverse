using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public class SetupHelmetManager
{
    static SetupHelmetManager()
    {
        EditorApplication.delayCall += CheckAndSetup;
    }

    private static void CheckAndSetup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        var scene = SceneManager.GetActiveScene();
        if (scene.name != "KiloScene") return;

        // Check if HelmetManager exists
        var existingManager = Object.FindFirstObjectByType<HelmetManager>();
        if (existingManager == null)
        {
            Debug.Log("[SetupHelmetManager] HelmetManager not found. Adding to Player...");
            CreateHelmetManager();
        }
    }

    [MenuItem("Kiloverse/Setup Helmet Manager")]
    public static void CreateHelmetManager()
    {
        // Find Player GameObject
        GameObject player = GameObject.Find("Player");
        if (player == null)
        {
            Debug.LogError("[SetupHelmetManager] Player GameObject not found!");
            return;
        }

        // Add component if not present
        HelmetManager manager = player.GetComponent<HelmetManager>();
        if (manager == null)
        {
            manager = player.AddComponent<HelmetManager>();
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[SetupHelmetManager] ✓ HelmetManager component added to Player");
    }
}
