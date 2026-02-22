using UnityEngine;
using Mapbox.BaseModule.Data;
using System.Collections.Generic;

namespace KiloWorld
{
    /// <summary>
    /// Validates mesh data before it's sent to Unity to prevent corrupt geometry (NaN, Infinity, extreme values)
    /// </summary>
    public static class MeshDataValidator
    {
        private const float MAX_VALID_COORDINATE = 100000f; // 100km from origin

        public static bool IsValid(MeshData meshData, out string reason)
        {
            reason = null;

            // Check for null or empty data
            if (meshData == null)
            {
                reason = "MeshData is null";
                return false;
            }

            if (meshData.Vertices.Count < 3)
            {
                reason = $"Not enough vertices ({meshData.Vertices.Count})";
                return false;
            }

            // Validate vertices
            for (int i = 0; i < meshData.Vertices.Count; i++)
            {
                var vertex = meshData.Vertices[i];

                // Check for NaN
                if (float.IsNaN(vertex.x) || float.IsNaN(vertex.y) || float.IsNaN(vertex.z))
                {
                    reason = $"Vertex {i} contains NaN: {vertex}";
                    return false;
                }

                // Check for Infinity
                if (float.IsInfinity(vertex.x) || float.IsInfinity(vertex.y) || float.IsInfinity(vertex.z))
                {
                    reason = $"Vertex {i} contains Infinity: {vertex}";
                    return false;
                }

                // Check for extreme values (likely corrupt data)
                float maxCoord = Mathf.Max(Mathf.Abs(vertex.x), Mathf.Abs(vertex.y), Mathf.Abs(vertex.z));
                if (maxCoord > MAX_VALID_COORDINATE)
                {
                    reason = $"Vertex {i} too far from origin ({maxCoord}m): {vertex}";
                    return false;
                }
            }

            // Validate triangles
            if (meshData.Triangles.Count == 0)
            {
                reason = "No triangles defined";
                return false;
            }

            foreach (var triangleList in meshData.Triangles)
            {
                if (triangleList == null) continue;

                // Check triangle indices are valid
                for (int i = 0; i < triangleList.Count; i++)
                {
                    int index = triangleList[i];
                    if (index < 0 || index >= meshData.Vertices.Count)
                    {
                        reason = $"Triangle index {index} out of bounds (vertex count: {meshData.Vertices.Count})";
                        return false;
                    }
                }
            }

            return true;
        }

        public static void LogInvalidMesh(string featureName, string reason)
        {
            Debug.LogWarning($"[MeshValidator] Rejecting corrupt mesh for '{featureName}': {reason}");
        }
    }
}
