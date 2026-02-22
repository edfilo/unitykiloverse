using UnityEngine;
using Kiloverse.Mapbox;

public class CheckUSXMetadata : MonoBehaviour
{
    void Start()
    {
        Invoke("Check", 10f);
    }

    void Check()
    {
        var usx = GameObject.Find("Overture_building_T7_872_(U.S. Steel Tower)");
        if (usx == null)
        {
            Debug.LogError("USX Tower not found!");
            return;
        }

        var metadata = usx.GetComponent<BuildingMetadata>();
        var renderer = usx.GetComponent<MeshRenderer>();
        var meshFilter = usx.GetComponent<MeshFilter>();

        Debug.Log("=== U.S. STEEL TOWER DEBUG ===");
        Debug.Log($"GameObject: {usx.name}");
        Debug.Log($"Transform position: {usx.transform.position}");
        Debug.Log($"Transform localPosition: {usx.transform.localPosition}");
        Debug.Log($"Transform scale: {usx.transform.localScale}");
        Debug.Log($"Parent: {(usx.transform.parent != null ? usx.transform.parent.name : "null")}");

        if (metadata != null)
        {
            Debug.Log($"BuildingMetadata.buildingName: {metadata.buildingName}");
            Debug.Log($"BuildingMetadata.heightMeters: {metadata.heightMeters}");
            Debug.Log($"BuildingMetadata.numFloors: {metadata.numFloors}");
            Debug.Log($"BuildingMetadata.worldHeightMeters: {metadata.worldHeightMeters}");
            Debug.Log($"BuildingMetadata.featureId: {metadata.featureId}");
        }

        if (renderer != null)
        {
            Debug.Log($"MeshRenderer bounds.size.y: {renderer.bounds.size.y:F1}m");
            Debug.Log($"MeshRenderer bounds.center: {renderer.bounds.center}");
        }

        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            Debug.Log($"Mesh bounds.size.y: {meshFilter.sharedMesh.bounds.size.y:F6}");
            Debug.Log($"Mesh vertex count: {meshFilter.sharedMesh.vertexCount}");
        }
    }
}
