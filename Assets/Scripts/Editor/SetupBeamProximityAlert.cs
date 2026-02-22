using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public class SetupBeamProximityAlert
{
    static SetupBeamProximityAlert()
    {
        EditorApplication.delayCall += CheckAndSetup;
    }

    private static void CheckAndSetup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        // Check if BeamProximityAlert exists in active scene
        var scene = SceneManager.GetActiveScene();
        if (scene.name != "KiloScene") return;

        var existingAlert = Object.FindFirstObjectByType<BeamProximityAlert>();
        if (existingAlert == null)
        {
            Debug.Log("[SetupBeamProximityAlert] BeamProximityAlert not found in scene. Adding now...");
            CreateBeamProximityAlert();
        }
    }

    [MenuItem("Kiloverse/Setup Beam Proximity Alert")]
    public static void CreateBeamProximityAlert()
    {
        // Find or create GameObject
        GameObject alertObj = GameObject.Find("BeamProximityAlert");
        if (alertObj == null)
        {
            alertObj = new GameObject("BeamProximityAlert");
        }

        // Add component if not present
        BeamProximityAlert alert = alertObj.GetComponent<BeamProximityAlert>();
        if (alert == null)
        {
            alert = alertObj.AddComponent<BeamProximityAlert>();
        }

        // Mark scene dirty
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        Debug.Log("[SetupBeamProximityAlert] ✓ BeamProximityAlert component added to scene");
    }
}
