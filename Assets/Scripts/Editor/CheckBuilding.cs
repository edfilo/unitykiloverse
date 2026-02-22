using UnityEngine;
using UnityEditor;

public class CheckBuilding
{
    [MenuItem("Debug/Check Building T7_962")]
    public static void Check()
    {
        CheckBuilding962();
    }

    [MenuItem("Debug/Check Building T7_0")]
    public static void CheckFirst()
    {
        CheckBuildingByName("Overture_building_T7_0");
    }

    private static void CheckBuilding962()
    {
        CheckBuildingByName("Overture_building_T7_962");
    }

    private static void CheckBuildingByName(string buildingName)
    {
        var go = GameObject.Find(buildingName);
        if (go == null)
        {
            Debug.LogError($"Building {buildingName} not found!");
            return;
        }

        var mf = go.GetComponent<MeshFilter>();
        var mr = go.GetComponent<MeshRenderer>();

        Debug.Log($"=== {buildingName} ===");
        Debug.Log($"Active: {go.activeInHierarchy}");
        Debug.Log($"Position: {go.transform.position}");
        Debug.Log($"Scale: {go.transform.localScale}");

        if (mf != null && mf.sharedMesh != null)
        {
            Debug.Log($"Mesh vertices: {mf.sharedMesh.vertexCount}");
            Debug.Log($"Mesh bounds: {mf.sharedMesh.bounds}");
            Debug.Log($"Mesh bounds size: {mf.sharedMesh.bounds.size}");
        }
        else
        {
            Debug.LogError("No mesh!");
        }

        if (mr != null)
        {
            Debug.Log($"Renderer enabled: {mr.enabled}");
            Debug.Log($"Renderer bounds: {mr.bounds}");
            Debug.Log($"Materials count: {mr.sharedMaterials.Length}");
            for (int i = 0; i < mr.sharedMaterials.Length; i++)
            {
                Debug.Log($"  Material {i}: {(mr.sharedMaterials[i] != null ? mr.sharedMaterials[i].name : "NULL")}");
            }
        }
        else
        {
            Debug.LogError("No renderer!");
        }
    }
}
