using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ForceBuildingDepth : MonoBehaviour
{
    public Material targetMaterial; // Optional: assign if you know it
    public float checkInterval = 1.0f;

    private void Start()
    {
        StartCoroutine(CheckRoutine());
    }

    private IEnumerator CheckRoutine()
    {
        while (true)
        {
            ForceDepthOnAllRenderers();
            yield return new WaitForSeconds(checkInterval);
        }
    }

    [ContextMenu("Force Now")]
[ContextMenu("Force Now")]
[ContextMenu("Force Now")]
public void ForceDepthOnAllRenderers()
    {
        Debug.Log("[ForceBuildingDepth] ===== STARTING DEPTH CHECK =====");
        
        // Find ALL MeshRenderers
        MeshRenderer[] meshRenderers = FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
        Debug.Log($"[ForceBuildingDepth] Found {meshRenderers.Length} MeshRenderers");
        
        // Log each one with detail
        foreach (var mr in meshRenderers)
        {
            GameObject go = mr.gameObject;
            Debug.Log($"[ForceBuildingDepth] MeshRenderer on '{go.name}' | Layer: {LayerMask.LayerToName(go.layer)} | Active: {go.activeInHierarchy} | Materials: {mr.sharedMaterials.Length}");
            
            for (int i = 0; i < mr.sharedMaterials.Length; i++)
            {
                Material mat = mr.sharedMaterials[i];
                if (mat == null)
                {
                    Debug.LogWarning($"  Material [{i}] is NULL");
                    continue;
                }
                
                Debug.Log($"  Material [{i}]: {mat.name} | Shader: {mat.shader.name} | Queue: {mat.renderQueue} | ZWrite: {(mat.HasProperty("_ZWrite") ? mat.GetFloat("_ZWrite").ToString() : "N/A")}");
                
                // Force depth writing
                if (mat.HasProperty("_ZWrite"))
                {
                    mat.SetFloat("_ZWrite", 1.0f);
                }
                if (mat.HasProperty("_Surface"))
                {
                    mat.SetFloat("_Surface", 0.0f);
                }
                if (mat.renderQueue >= 2500)
                {
                    mat.renderQueue = 2000;
                }
                mat.SetOverrideTag("RenderType", "Opaque");
                
                Debug.Log($"  -> FIXED: Queue={mat.renderQueue}, ZWrite={mat.GetFloat("_ZWrite")}");
            }
        }
        
        Debug.Log("[ForceBuildingDepth] ===== DEPTH CHECK COMPLETE =====");
    }

private void ApplyDepthFix(Renderer r)
    {
        Debug.Log($"[ForceBuildingDepth] Checking: {r.name} (parent: {(r.transform.parent != null ? r.transform.parent.name : "none")})");
        
        foreach (var mat in r.materials)
        {
            if (mat == null) continue;

            Debug.Log($"  Material: {mat.name} | Shader: {mat.shader.name} | Queue: {mat.renderQueue} | ZWrite: {(mat.HasProperty("_ZWrite") ? mat.GetFloat("_ZWrite").ToString() : "N/A")}");

            bool changed = false;

            // 1. URP Properties
            if (mat.HasProperty("_Surface") && mat.GetFloat("_Surface") != 0.0f) { mat.SetFloat("_Surface", 0.0f); changed = true; }
            if (mat.HasProperty("_ZWrite") && mat.GetFloat("_ZWrite") != 1.0f) { mat.SetFloat("_ZWrite", 1.0f); changed = true; }
            
            // 2. Standard Shader Properties (Legacy/Built-in)
            if (mat.HasProperty("_Mode") && mat.GetFloat("_Mode") != 0.0f) 
            { 
                mat.SetFloat("_Mode", 0.0f); // Opaque
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                mat.SetInt("_ZWrite", 1);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.DisableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = -1;
                changed = true; 
            }

            // 3. Force Render Queue
            if (mat.renderQueue >= 2500) // If it's in Transparent queue
            {
                mat.renderQueue = 2000; // Force to Geometry
                changed = true;
            }
            
            mat.SetOverrideTag("RenderType", "Opaque");

            if (changed)
            {
                Debug.Log($"[ForceBuildingDepth] *** FIXED: {r.name} | Mat: {mat.name} | Shader: {mat.shader.name}");
            }
        }
    }
}
