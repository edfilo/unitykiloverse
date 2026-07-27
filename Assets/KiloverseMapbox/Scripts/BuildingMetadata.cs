using UnityEngine;
using System.Collections.Generic;

namespace Kiloverse.Mapbox
{
    /// <summary>
    /// Stores metadata for Overture buildings and displays it in the Inspector
    /// </summary>
public class BuildingMetadata : MonoBehaviour
{
    private static readonly HashSet<BuildingMetadata> ActiveRegistry = new HashSet<BuildingMetadata>();

    // BuildingFlattener consumes this registry incrementally. Tile generation
    // announces only the building that changed; there is no scene-wide polling.
    public static event System.Action<BuildingMetadata, bool> ActiveStateChanged;
    public static event System.Action<BuildingMetadata> GeometryChanged;

    public static IEnumerable<BuildingMetadata> ActiveBuildings => ActiveRegistry;

    // Monotonic diagnostic revision. Consumers should prefer the events above.
    public static int SceneRevision { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeRegistry()
    {
        ActiveRegistry.Clear();
        SceneRevision = 0;
        ActiveStateChanged = null;
        GeometryChanged = null;
    }

    private static void NoteSceneGeometryChanged()
    {
        unchecked { SceneRevision++; }
    }

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

    // Runtime-only ownership flag. Tile/frustum visibility code must not
    // re-enable a renderer while BuildingFlattener is suppressing its shell.
    [System.NonSerialized] public bool runtimeFlattened;

    [Header("Runtime LOD")]
    [Tooltip("LOD selected when this building or merged building batch was generated.")]
    public string generatedLod = "Unknown";
    public float generatedDistanceMeters = -1f;
    public bool mergedLodBatch;
    public bool facadeGeometryVisible = true;

    [System.NonSerialized] private int[][] cachedFacadeTriangles;
    [System.NonSerialized] private int originalSubMeshCount;

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
            if (properties.TryGetValue("_k1lo_lod", out var lodValue))
                generatedLod = lodValue?.ToString() ?? "Unknown";
            if (properties.TryGetValue("_k1lo_lod_distance_m", out var distanceValue))
            {
                try { generatedDistanceMeters = System.Convert.ToSingle(distanceValue); } catch { }
            }
            if (properties.TryGetValue("_k1lo_merged_lod", out var mergedValue))
            {
                try { mergedLodBatch = System.Convert.ToBoolean(mergedValue); } catch { }
            }
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
        RegisterActive();
        NoteSceneGeometryChanged();
        GeometryChanged?.Invoke(this);
    }

    private void OnEnable()
    {
        RegisterActive();
    }

    private void OnDisable()
    {
        if (!ActiveRegistry.Remove(this)) return;
        NoteSceneGeometryChanged();
        ActiveStateChanged?.Invoke(this, false);
    }

    private void RegisterActive()
    {
        if (!isActiveAndEnabled || !ActiveRegistry.Add(this)) return;
        NoteSceneGeometryChanged();
        ActiveStateChanged?.Invoke(this, true);
    }

    /// <summary>
    /// Removes/restores emissive facade submeshes without touching the shell or
    /// rebuilding the containing tile. Triangle indices are cached only for a
    /// building that actually changes state, keeping the memory cost bounded.
    /// </summary>
    public bool SetFacadeGeometryVisible(bool visible)
    {
        var mf = GetComponent<MeshFilter>();
        var mesh = mf != null ? mf.sharedMesh : null;
        if (mesh == null) return false;

        if (!visible)
        {
            if (!facadeGeometryVisible || mesh.subMeshCount <= 1) return false;
            originalSubMeshCount = mesh.subMeshCount;
            cachedFacadeTriangles = new int[originalSubMeshCount - 1][];
            for (int i = 1; i < originalSubMeshCount; i++)
                cachedFacadeTriangles[i - 1] = mesh.GetTriangles(i);
            mesh.subMeshCount = 1;
            facadeGeometryVisible = false;
            return true;
        }

        if (facadeGeometryVisible || cachedFacadeTriangles == null || originalSubMeshCount <= 1)
            return false;
        mesh.subMeshCount = originalSubMeshCount;
        for (int i = 1; i < originalSubMeshCount; i++)
            mesh.SetTriangles(cachedFacadeTriangles[i - 1], i, false);
        mesh.RecalculateBounds();
        cachedFacadeTriangles = null;
        facadeGeometryVisible = true;
        return true;
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
