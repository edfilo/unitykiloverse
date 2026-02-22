using UnityEngine;
using UnityEditor;
using System.Linq;

public class CountGeometry
{
    [MenuItem("Tools/Count Scene Geometry")]
    public static void CountSceneGeometry()
    {
        var allRenderers = Object.FindObjectsOfType<MeshRenderer>();
        var allFilters = Object.FindObjectsOfType<MeshFilter>();

        Debug.Log($"<color=yellow>========== GEOMETRY STATISTICS ==========</color>");
        Debug.Log($"Total MeshRenderers: {allRenderers.Length}");
        Debug.Log($"Total MeshFilters: {allFilters.Length}");

        // Count by name patterns
        var buildingObjects = allRenderers.Where(r => r.name.ToLower().Contains("building")).ToArray();
        var roadObjects = allRenderers.Where(r => r.name.ToLower().Contains("road")).ToArray();
        var poiObjects = allRenderers.Where(r => r.name.ToLower().Contains("poi")).ToArray();

        Debug.Log($"\n<color=cyan>By Type:</color>");
        Debug.Log($"  Buildings: {buildingObjects.Length}");
        Debug.Log($"  Roads: {roadObjects.Length}");
        Debug.Log($"  POIs: {poiObjects.Length}");

        // Count vertices and triangles
        long totalVertices = 0;
        long totalTriangles = 0;

        foreach (var filter in allFilters)
        {
            if (filter.sharedMesh != null)
            {
                totalVertices += filter.sharedMesh.vertexCount;
                totalTriangles += filter.sharedMesh.triangles.Length / 3;
            }
        }

        Debug.Log($"\n<color=cyan>Mesh Data:</color>");
        Debug.Log($"  Total Vertices: {totalVertices:N0}");
        Debug.Log($"  Total Triangles: {totalTriangles:N0}");

        // Find top parent containers
        var parentGroups = allRenderers
            .Where(r => r.transform.parent != null)
            .GroupBy(r => r.transform.parent.name)
            .OrderByDescending(g => g.Count())
            .Take(15);

        Debug.Log($"\n<color=cyan>Top 15 Parent Containers:</color>");
        foreach (var group in parentGroups)
        {
            Debug.Log($"  {group.Key}: {group.Count()} renderers");
        }

        // Find root tile objects
        var rootObjects = allRenderers
            .Where(r => r.transform.parent == null || r.transform.parent.parent == null)
            .GroupBy(r => r.transform.root.name)
            .OrderByDescending(g => g.Count())
            .Take(10);

        Debug.Log($"\n<color=cyan>Top 10 Root Objects:</color>");
        foreach (var group in rootObjects)
        {
            Debug.Log($"  {group.Key}: {group.Count()} renderers");
        }

        Debug.Log($"<color=yellow>=========================================</color>");
    }
}
