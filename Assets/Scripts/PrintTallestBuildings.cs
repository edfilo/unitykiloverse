using UnityEngine;
using Kiloverse.Mapbox;

public class PrintTallestBuildings : MonoBehaviour
{
    [SerializeField] private float checkInterval = 15f;

    void Start()
    {
        InvokeRepeating("CheckBuildings", 5f, checkInterval);
    }

    void CheckBuildings()
    {
        var allBuildings = FindObjectsOfType<BuildingMetadata>();

        if (allBuildings.Length == 0)
        {
            Debug.LogWarning($"[{Time.time:F1}s] No buildings found with BuildingMetadata component!");
            return;
        }

        Debug.Log($"[{Time.time:F1}s] Found {allBuildings.Length} total buildings");

        System.Collections.Generic.List<BuildingMetadata> sorted = new System.Collections.Generic.List<BuildingMetadata>(allBuildings);
        sorted.Sort((a, b) => b.worldHeightMeters.CompareTo(a.worldHeightMeters));

        Debug.Log("=== TOP 10 TALLEST BUILDINGS BY RENDERED HEIGHT ===");
        for (int i = 0; i < Mathf.Min(10, sorted.Count); i++)
        {
            var bm = sorted[i];
            var renderer = bm.GetComponent<MeshRenderer>();
            Vector3 worldPos = renderer != null ? renderer.bounds.center : bm.transform.position;

            Debug.Log($"{i+1}. '{bm.buildingName}' | GameObject: {bm.gameObject.name}");
            Debug.Log($"    Rendered Height: {bm.worldHeightMeters:F1}m | Metadata Height: {bm.heightMeters:F1}m | Floors: {bm.numFloors}");
            Debug.Log($"    World Position: ({worldPos.x:F2}, {worldPos.y:F2}, {worldPos.z:F2})");
        }
    }
}
