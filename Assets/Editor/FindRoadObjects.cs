using UnityEngine;
using UnityEditor;

public class FindRoadObjects
{
    [MenuItem("Debug/Find Road Children")]
    public static void FindRoads()
    {
        var roadParent = GameObject.Find("road layer objects");
        if (roadParent != null)
        {
            Debug.Log($"Found road parent with {roadParent.transform.childCount} children");
            for (int i = 0; i < Mathf.Min(5, roadParent.transform.childCount); i++)
            {
                var child = roadParent.transform.GetChild(i);
                var mf = child.GetComponent<MeshFilter>();
                var mr = child.GetComponent<MeshRenderer>();
                Debug.Log($"Child {i}: {child.name}, Mesh: {(mf?.sharedMesh != null ? $"{mf.sharedMesh.vertexCount} verts, bounds={mf.sharedMesh.bounds}" : "null")}, Material: {mr?.sharedMaterial?.name}");
            }
        }

        var waterParent = GameObject.Find("water layer objects");
        if (waterParent != null)
        {
            Debug.Log($"Found water parent with {waterParent.transform.childCount} children");
            for (int i = 0; i < Mathf.Min(5, waterParent.transform.childCount); i++)
            {
                var child = waterParent.transform.GetChild(i);
                var mf = child.GetComponent<MeshFilter>();
                var mr = child.GetComponent<MeshRenderer>();
                Debug.Log($"Child {i}: {child.name}, Mesh: {(mf?.sharedMesh != null ? $"{mf.sharedMesh.vertexCount} verts, bounds={mf.sharedMesh.bounds}" : "null")}, Material: {mr?.sharedMaterial?.name}");
            }
        }
    }
}
