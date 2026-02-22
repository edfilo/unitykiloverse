using UnityEngine;
using UnityEditor;

public class FindUSXTower
{
    [MenuItem("Debug/Find & Frame USX Tower")]
    public static void FindTower()
    {
        // Building #962 in tile T7 (14/4551/6176) is USX Tower
        var go = GameObject.Find("Overture_building_T7_962");
        if (go == null)
        {
            Debug.LogError("USX Tower (Overture_building_T7_962) not found! Make sure tile 14/4551/6176 is loaded.");
            return;
        }

        Debug.Log($"Found USX Tower at position: {go.transform.position}");
        Debug.Log($"World position with scale: {go.transform.position + go.transform.localPosition}");

        // Select it in hierarchy
        Selection.activeGameObject = go;

        // Frame it in scene view
        SceneView.lastActiveSceneView.Frame(new Bounds(go.transform.position, Vector3.one * 100f), false);

        // Move scene camera to look at it
        var sceneView = SceneView.lastActiveSceneView;
        if (sceneView != null)
        {
            // Position camera 500m away looking at the building
            Vector3 buildingPos = go.transform.position;
            sceneView.pivot = buildingPos;
            sceneView.rotation = Quaternion.Euler(20f, -45f, 0f);
            sceneView.size = 500f;
            sceneView.Repaint();
        }

        Debug.Log("USX Tower selected and framed in Scene view!");
    }

    [MenuItem("Debug/Move Player to USX Tower")]
    public static void TeleportToTower()
    {
        var tower = GameObject.Find("Overture_building_T7_962");
        if (tower == null)
        {
            Debug.LogError("USX Tower not found!");
            return;
        }

        var player = Camera.main;
        if (player == null)
        {
            Debug.LogError("Main camera not found!");
            return;
        }

        // Move camera 200m away from tower
        Vector3 towerPos = tower.transform.position;
        Vector3 offset = new Vector3(-200f, 50f, -200f);
        player.transform.position = towerPos + offset;
        player.transform.LookAt(towerPos);

        Debug.Log($"Teleported player to {player.transform.position}, looking at USX Tower at {towerPos}");
    }
}
