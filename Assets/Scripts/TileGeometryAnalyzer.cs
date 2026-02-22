using UnityEngine;

/// <summary>
/// Debug component to analyze how Mapbox organizes tile geometry.
/// Attach to any GameObject and press Space in Play mode.
/// </summary>
public class TileGeometryAnalyzer : MonoBehaviour
{
    [Header("Analysis")]
    public bool runOnStart = false;
    public bool logDetails = true;

    private void Start()
    {
        if (runOnStart)
        {
            AnalyzeGeometry();
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            AnalyzeGeometry();
        }
    }

    private void AnalyzeGeometry()
    {
        Debug.Log("=== TILE GEOMETRY ANALYSIS ===");

        // Find all tile GameObjects
        var allObjects = FindObjectsOfType<GameObject>();
        int tileCount = 0;
        int totalRenderers = 0;
        int totalMeshes = 0;
        int totalVertices = 0;
        int totalTriangles = 0;

        foreach (var go in allObjects)
        {
            // Look for Overture/tile objects
            if (go.name.Contains("Overture") || go.name.Contains("tile") || go.name.Contains("building") || go.name.Contains("road"))
            {
                tileCount++;

                var renderer = go.GetComponent<Renderer>();
                if (renderer != null)
                {
                    totalRenderers++;

                    if (logDetails)
                    {
                        var bounds = renderer.bounds;
                        var size = bounds.size;
                        Debug.Log($"[Renderer] {go.name} - Bounds: {size.x:F1}m x {size.z:F1}m, Center: {bounds.center}");
                    }
                }

                var meshFilter = go.GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    totalMeshes++;
                    var mesh = meshFilter.sharedMesh;
                    totalVertices += mesh.vertexCount;
                    totalTriangles += mesh.triangles.Length / 3;

                    if (logDetails)
                    {
                        Debug.Log($"[Mesh] {go.name} - Verts: {mesh.vertexCount}, Tris: {mesh.triangles.Length / 3}");
                    }
                }
            }
        }

        Debug.Log($"=== SUMMARY ===");
        Debug.Log($"Tile GameObjects: {tileCount}");
        Debug.Log($"Total Renderers: {totalRenderers}");
        Debug.Log($"Total Meshes: {totalMeshes}");
        Debug.Log($"Total Vertices: {totalVertices:N0}");
        Debug.Log($"Total Triangles: {totalTriangles:N0}");
        Debug.Log($"Avg Verts/Renderer: {(totalRenderers > 0 ? totalVertices / totalRenderers : 0):N0}");
        Debug.Log($"Avg Tris/Renderer: {(totalRenderers > 0 ? totalTriangles / totalRenderers : 0):N0}");

        // Check if frustum culling is working
        AnalyzeFrustumCulling();
    }

    private void AnalyzeFrustumCulling()
    {
        Debug.Log("=== FRUSTUM CULLING CHECK ===");

        var camera = Camera.main;
        if (camera == null)
        {
            Debug.LogWarning("No main camera found");
            return;
        }

        var planes = GeometryUtility.CalculateFrustumPlanes(camera);

        int inFrustum = 0;
        int outOfFrustum = 0;
        int renderersEnabled = 0;
        int renderersDisabled = 0;

        var allRenderers = FindObjectsOfType<Renderer>();
        foreach (var renderer in allRenderers)
        {
            bool isVisible = GeometryUtility.TestPlanesAABB(planes, renderer.bounds);

            if (isVisible)
                inFrustum++;
            else
                outOfFrustum++;

            if (renderer.enabled)
                renderersEnabled++;
            else
                renderersDisabled++;

            // Check if renderer is visible but should be culled
            if (!isVisible && renderer.enabled && renderer.isVisible)
            {
                Debug.LogWarning($"[Culling Issue] {renderer.gameObject.name} is out of frustum but still rendering!");
            }
        }

        Debug.Log($"Renderers in frustum: {inFrustum}");
        Debug.Log($"Renderers out of frustum: {outOfFrustum}");
        Debug.Log($"Renderers enabled: {renderersEnabled}");
        Debug.Log($"Renderers disabled: {renderersDisabled}");
        Debug.Log($"Frustum culling efficiency: {(outOfFrustum > 0 ? (float)outOfFrustum / (inFrustum + outOfFrustum) * 100f : 0f):F1}% culled");
    }
}
