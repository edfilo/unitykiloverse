using UnityEngine;
using UnityEditor;

[InitializeOnLoad]
public class ForceMaterialUpdate
{
    static ForceMaterialUpdate()
    {
        EditorApplication.delayCall += UpdateMaterial;
    }

    static void UpdateMaterial()
    {
        string path = "Assets/FPSPort/Materials/FpsSideMaterialURP.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        
        if (mat != null)
        {
            // Check if already updated
            if (mat.shader.name == "Kiloverse/RealisticBuildingEmissive")
            {
                // Already updated, but ensure emission color is correct
                if (mat.GetColor("_EmissionColor").r > 2.0f) // If still super bright yellow
                {
                    Debug.Log("[ForceUpdate] Correcting emission color...");
                    mat.SetColor("_EmissionColor", new Color(1f, 0.3f, 0.1f));
                    mat.SetFloat("_EmissionIntensity", 5f);
                    EditorUtility.SetDirty(mat);
                    AssetDatabase.SaveAssets();
                }
                return;
            }

            Debug.Log("[ForceUpdate] Updating material shader...");
            
            // Force Shader Update
            Shader shader = Shader.Find("Kiloverse/RealisticBuildingEmissive");
            if (shader != null) 
            {
                mat.shader = shader;
                
                // Re-assign textures to ensure they map to new shader properties
                Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/FPSPort/Textures/realistic/RealisticSideAlbedo.png");
                Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/FPSPort/Textures/realistic/RealisticSideNormal.png");
                Texture2D metallic = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/FPSPort/Textures/realistic/RealisticSideMetallic.png");
                
                if (albedo != null) 
                {
                    mat.SetTexture("_MainTex", albedo);
                    mat.SetTexture("_BaseMap", albedo);
                }
                
                // Force Base Color to White so texture shows correctly
                mat.SetColor("_BaseColor", Color.white);
                mat.SetColor("_Color", Color.white);
                
                if (normal != null) mat.SetTexture("_BumpMap", normal);
                
                // Use Metallic as Emission Mask (User confirmed it's black/white mask)
                if (metallic != null) mat.SetTexture("_EmissionMap", metallic);
                
                // Clear Metallic/AO maps from PBR slots if we aren't using them there (or keep them if valid)
                // Assuming we only use Albedo/Normal for PBR, and Metallic for Emission Mask
                mat.SetTexture("_MetallicGlossMap", null);
                mat.SetTexture("_OcclusionMap", null);
                
                // Ensure non‑metallic (dielectric) so albedo color shows
                mat.SetFloat("_Metallic", 0f);
                // A moderate smoothness keeps the surface a bit glossy but not mirror‑like
                mat.SetFloat("_Smoothness", 0.4f);
                
                // Enable Emission
                mat.SetColor("_EmissionColor", new Color(1f, 0.4f, 0.1f));
                mat.SetFloat("_EmissionIntensity", 2.0f);
                mat.EnableKeyword("_EMISSION");
                
                EditorUtility.SetDirty(mat);
                AssetDatabase.SaveAssets();
                Debug.Log("[ForceUpdate] ✅ Material updated: Metallic as Emission Mask");
            }
            else
            {
                Debug.LogError("[ForceUpdate] Shader 'Kiloverse/RealisticBuildingEmissive' not found!");
            }
        }
        else
        {
            Debug.LogError($"[ForceUpdate] Material not found at {path}");
        }

        // Also check Top material
        string topPath = "Assets/FPSPort/Materials/FpsTopMaterialURP.mat";
        Material topMat = AssetDatabase.LoadAssetAtPath<Material>(topPath);
        if (topMat != null)
        {
             // Ensure it's not glowing yellow
             if (topMat.HasProperty("_EmissionColor") && topMat.GetColor("_EmissionColor").maxColorComponent > 1.0f)
             {
                 topMat.SetColor("_EmissionColor", Color.black);
                 topMat.DisableKeyword("_EMISSION");
                 EditorUtility.SetDirty(topMat);
                 Debug.Log("[ForceUpdate] Fixed Top material emission");
             }
        }
    }
}
