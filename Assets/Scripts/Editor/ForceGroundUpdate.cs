using UnityEngine;
using UnityEditor;
using KiloWorld.Rendering.Systems;

public class ForceGroundUpdate : Editor
{
    [MenuItem("KiloWorld/Force Ground Plane Update")]
    public static void ForceUpdate()
    {
        RenderManager manager = FindObjectOfType<RenderManager>();
        if (manager != null)
        {
            Debug.Log("[ForceGroundUpdate] Found RenderManager, calling Apply()...");
            manager.Apply();
            Debug.Log("[ForceGroundUpdate] Apply() complete. Check console for ground update logs.");
        }
        else
        {
            Debug.LogError("[ForceGroundUpdate] RenderManager not found in scene!");
        }
        
        SimpleGroundPlane plane = FindObjectOfType<SimpleGroundPlane>();
        if (plane != null)
        {
            Debug.Log($"[ForceGroundUpdate] Found SimpleGroundPlane at position: {plane.transform.position}");
            Material mat = plane.GetGroundMaterial();
            if (mat != null)
            {
                Debug.Log($"[ForceGroundUpdate] Ground material: {mat.name}");
                Debug.Log($"[ForceGroundUpdate] Material color: {mat.GetColor("_BaseColor")}");
                Debug.Log($"[ForceGroundUpdate] Material smoothness: {mat.GetFloat("_Smoothness")}");
                Texture tex = mat.GetTexture("_BaseMap");
                Debug.Log($"[ForceGroundUpdate] Material texture: {(tex != null ? tex.name : "None")}");
            }
        }
        else
        {
            Debug.LogError("[ForceGroundUpdate] SimpleGroundPlane not found!");
        }
    }
}
