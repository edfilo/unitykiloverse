using UnityEngine;
using System.Collections.Generic;

namespace Kiloverse.Mapbox
{
    /// <summary>
    /// Stores metadata for Overture buildings and displays it in the Inspector
    /// </summary>
    public class BuildingMetadata : MonoBehaviour
{
    [Header("Building Info")]
    public string buildingName = "";
    public string featureId = "";
    public float heightMeters = 0f;
    public int numFloors = 0;

    [Header("Mesh Info")]
    public int meshVertexCount = 0;
    public Vector3 meshBoundsSize = Vector3.zero;
    public Vector3 meshBoundsCenter = Vector3.zero;

    [Header("World Info")]
    public Vector3 worldPosition = Vector3.zero;
    public Vector3 worldBoundsMin = Vector3.zero;
    public Vector3 worldBoundsMax = Vector3.zero;
    public float worldHeightMeters = 0f;

    [Header("Tile Info")]
    public string tileKey = "";
    public int tileIndex = -1;
    public int buildingIndex = -1;

    [Header("Properties")]
    [TextArea(3, 10)]
    public string rawProperties = "";

    public void Initialize(Dictionary<string, object> properties, string tile, int tileIdx, int buildingIdx)
    {
        tileKey = tile;
        tileIndex = tileIdx;
        buildingIndex = buildingIdx;

        // Extract building name
        if (properties != null && properties.ContainsKey("names"))
        {
            try
            {
                var namesJson = properties["names"].ToString();
                if (namesJson.Contains("primary"))
                {
                    int start = namesJson.IndexOf("\"primary\":\"") + 11;
                    int end = namesJson.IndexOf("\"", start);
                    if (end > start)
                    {
                        buildingName = namesJson.Substring(start, end - start);
                    }
                }
            }
            catch { }
        }

        // Extract height
        if (properties != null)
        {
            if (properties.ContainsKey("height"))
            {
                try { heightMeters = System.Convert.ToSingle(properties["height"]); } catch { }
            }

            if (properties.ContainsKey("num_floors"))
            {
                try { numFloors = System.Convert.ToInt32(properties["num_floors"]); } catch { }
            }

            if (properties.ContainsKey("id"))
            {
                featureId = properties["id"].ToString();
            }

            // Store all properties as JSON-like string
            var propsList = new List<string>();
            foreach (var kvp in properties)
            {
                propsList.Add($"{kvp.Key}: {kvp.Value}");
            }
            rawProperties = string.Join("\n", propsList);
        }

        // Update mesh info
        UpdateMeshInfo();
    }

    public void UpdateMeshInfo()
    {
        var mf = GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            meshVertexCount = mf.sharedMesh.vertexCount;
            meshBoundsSize = mf.sharedMesh.bounds.size;
            meshBoundsCenter = mf.sharedMesh.bounds.center;
        }

        var mr = GetComponent<MeshRenderer>();
        if (mr != null)
        {
            worldPosition = transform.position;
            worldBoundsMin = mr.bounds.min;
            worldBoundsMax = mr.bounds.max;
            worldHeightMeters = mr.bounds.size.y;
        }
    }

    void OnValidate()
    {
        // Update mesh info when inspector refreshes
        if (Application.isPlaying)
        {
            UpdateMeshInfo();
        }
    }
}
}
