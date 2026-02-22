using UnityEngine;
using System.Linq;

public class TileDebugCounter : MonoBehaviour
{
    [Header("Runtime Analysis")]
    public Transform runtimeObjectsRoot;

    [ContextMenu("Count Tiles and Objects")]
    public void CountTilesAndObjects()
    {
        if (runtimeObjectsRoot == null)
        {
            runtimeObjectsRoot = GameObject.Find("RuntimeObjectsRoot")?.transform;
        }

        if (runtimeObjectsRoot == null)
        {
            Debug.LogError("RuntimeObjectsRoot not found!");
            return;
        }

        // Count tiles by zoom level
        var tilesByZoom = new System.Collections.Generic.Dictionary<int, int>();
        var totalActive = 0;
        var totalInactive = 0;
        var totalBuildings = 0;
        var totalRoads = 0;
        var totalPOIs = 0;

        foreach (Transform tileTransform in runtimeObjectsRoot)
        {
            // Parse tile name like "15/12345/67890"
            var parts = tileTransform.name.Split('/');
            if (parts.Length == 3 && int.TryParse(parts[0], out int zoom))
            {
                if (!tilesByZoom.ContainsKey(zoom))
                    tilesByZoom[zoom] = 0;
                tilesByZoom[zoom]++;

                // Count objects under this tile
                var children = tileTransform.GetComponentsInChildren<Transform>(true);
                foreach (var child in children)
                {
                    if (child == tileTransform) continue;

                    if (child.gameObject.activeInHierarchy)
                        totalActive++;
                    else
                        totalInactive++;

                    if (child.name.Contains("building", System.StringComparison.OrdinalIgnoreCase))
                        totalBuildings++;
                    if (child.name.Contains("road", System.StringComparison.OrdinalIgnoreCase))
                        totalRoads++;
                    if (child.name.Contains("poi", System.StringComparison.OrdinalIgnoreCase))
                        totalPOIs++;
                }
            }
        }

        Debug.Log("=== TILE COUNT BY ZOOM LEVEL ===");
        foreach (var kvp in tilesByZoom.OrderBy(x => x.Key))
        {
            Debug.Log($"Zoom {kvp.Key}: {kvp.Value} tiles");
        }

        Debug.Log($"\n=== OBJECT COUNTS ===");
        Debug.Log($"Total Active Objects: {totalActive}");
        Debug.Log($"Total Inactive (Hidden) Objects: {totalInactive}");
        Debug.Log($"Buildings: {totalBuildings}");
        Debug.Log($"Roads: {totalRoads}");
        Debug.Log($"POI Labels: {totalPOIs}");
        Debug.Log($"TOTAL OBJECTS: {totalActive + totalInactive}");

        // Get mesh stats
        var meshFilters = runtimeObjectsRoot.GetComponentsInChildren<MeshFilter>(true);
        int totalVerts = 0;
        int totalTris = 0;
        foreach (var mf in meshFilters)
        {
            if (mf.sharedMesh != null)
            {
                totalVerts += mf.sharedMesh.vertexCount;
                totalTris += mf.sharedMesh.triangles.Length / 3;
            }
        }
        Debug.Log($"\n=== MESH STATS ===");
        Debug.Log($"Total Vertices: {totalVerts:N0}");
        Debug.Log($"Total Triangles: {totalTris:N0}");
        Debug.Log($"MeshFilters: {meshFilters.Length}");
    }
}
