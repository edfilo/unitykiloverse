using UnityEngine;

[ExecuteInEditMode]
public class MeshVertexDebugger : MonoBehaviour
{
    public bool showVertices = true;
    public bool showTriangles = false;
    public float vertexSize = 0.5f;
    public Color vertexColor = Color.yellow;
    public Color triangleColor = Color.cyan;

    private void OnDrawGizmos()
    {
        if (!showVertices && !showTriangles) return;

        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null) return;

        Mesh mesh = meshFilter.sharedMesh;
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;

        // Draw vertices
        if (showVertices)
        {
            Gizmos.color = vertexColor;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 worldPos = transform.TransformPoint(vertices[i]);
                Gizmos.DrawSphere(worldPos, vertexSize);
            }
        }

        // Draw triangles
        if (showTriangles)
        {
            Gizmos.color = triangleColor;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                if (i + 2 < triangles.Length)
                {
                    Vector3 v1 = transform.TransformPoint(vertices[triangles[i]]);
                    Vector3 v2 = transform.TransformPoint(vertices[triangles[i + 1]]);
                    Vector3 v3 = transform.TransformPoint(vertices[triangles[i + 2]]);

                    Gizmos.DrawLine(v1, v2);
                    Gizmos.DrawLine(v2, v3);
                    Gizmos.DrawLine(v3, v1);
                }
            }
        }
    }

    [ContextMenu("Log Mesh Stats")]
    public void LogMeshStats()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            Debug.LogWarning("No mesh found!");
            return;
        }

        Mesh mesh = meshFilter.sharedMesh;
        Debug.Log($"[{gameObject.name}] Vertices: {mesh.vertexCount}, Triangles: {mesh.triangles.Length / 3}, Bounds: {mesh.bounds}");
    }
}
