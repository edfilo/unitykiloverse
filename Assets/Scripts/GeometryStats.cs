using UnityEngine;
using System.Linq;

[ExecuteAlways]
public class GeometryStats : MonoBehaviour
{
    [ContextMenu("Count All Geometry")]
    public void CountGeometry()
    {
        var allRenderers = FindObjectsOfType<MeshRenderer>();
        var allFilters = FindObjectsOfType<MeshFilter>();

        Debug.Log($"<color=yellow>========== GEOMETRY STATISTICS ==========</color>");
        Debug.Log($"Total MeshRenderers: {allRenderers.Length}");
        Debug.Log($"Total MeshFilters: {allFilters.Length}");

        // Count by parent name patterns
        var buildingObjects = allRenderers.Where(r => r.name.ToLower().Contains("building")).ToArray();
        var roadObjects = allRenderers.Where(r => r.name.ToLower().Contains("road")).ToArray();
        var tileObjects = allRenderers.Where(r => r.name.Contains("tile") || r.name.Contains("Tile")).ToArray();

        Debug.Log($"\n<color=cyan>By Type:</color>");
        Debug.Log($"  Buildings: {buildingObjects.Length}");
        Debug.Log($"  Roads: {roadObjects.Length}");
        Debug.Log($"  Tiles: {tileObjects.Length}");

        // Count vertices
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

        // Group by parent to find tile containers
        var parentGroups = allRenderers
            .Where(r => r.transform.parent != null)
            .GroupBy(r => r.transform.parent.name)
            .OrderByDescending(g => g.Count())
            .Take(10);

        Debug.Log($"\n<color=cyan>Top 10 Parent Containers:</color>");
        foreach (var group in parentGroups)
        {
            Debug.Log($"  {group.Key}: {group.Count()} renderers");
        }

        Debug.Log($"<color=yellow>=========================================</color>");
    }
}
