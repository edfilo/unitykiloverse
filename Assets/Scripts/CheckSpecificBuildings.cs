using UnityEngine;
using Kiloverse.Mapbox;

public class CheckSpecificBuildings : MonoBehaviour
{
    void Start()
    {
        // Check USX Tower
        var usx = GameObject.Find("Overture_building_T7_962");
        if (usx != null)
        {
            var bm = usx.GetComponent<BuildingMetadata>();
            if (bm != null)
            {
                Debug.Log($"=== USX TOWER (T7_962) ===");
                Debug.Log($"Name: {bm.buildingName}");
                Debug.Log($"Height: {bm.heightMeters}m (metadata)");
                Debug.Log($"World Height: {bm.worldHeightMeters}m (renderer)");
                Debug.Log($"Num Floors: {bm.numFloors}");
                Debug.Log($"Feature ID: {bm.featureId}");
            }
        }

        // Check BNY Mellon
        var bny = GameObject.Find("Overture_building_T7_847_(BNY Mellon Center)");
        if (bny != null)
        {
            var bm = bny.GetComponent<BuildingMetadata>();
            if (bm != null)
            {
                Debug.Log($"=== BNY MELLON CENTER (T7_847) ===");
                Debug.Log($"Name: {bm.buildingName}");
                Debug.Log($"Height: {bm.heightMeters}m (metadata)");
                Debug.Log($"World Height: {bm.worldHeightMeters}m (renderer)");
                Debug.Log($"Num Floors: {bm.numFloors}");
                Debug.Log($"Feature ID: {bm.featureId}");
            }
        }

        // Find all buildings and sort by height
        var allBuildings = FindObjectsOfType<BuildingMetadata>();
        System.Collections.Generic.List<BuildingMetadata> sorted = new System.Collections.Generic.List<BuildingMetadata>(allBuildings);
        sorted.Sort((a, b) => b.worldHeightMeters.CompareTo(a.worldHeightMeters));

        Debug.Log($"=== TOP 5 TALLEST BUILDINGS (of {sorted.Count} total) ===");
        for (int i = 0; i < Mathf.Min(5, sorted.Count); i++)
        {
            var bm = sorted[i];
            Debug.Log($"{i+1}. {bm.buildingName} - {bm.worldHeightMeters:F1}m (floors: {bm.numFloors})");
        }
    }
}
