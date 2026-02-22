using UnityEngine;
using UnityEditor;
using System.IO;

[InitializeOnLoad]
public class UpgradeBikerHelmetMaterials
{
    static UpgradeBikerHelmetMaterials()
    {
        EditorApplication.delayCall += CheckAndUpgrade;
    }

    private static void CheckAndUpgrade()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        // Check if materials need upgrading by looking for Standard shader
        string[] materialPaths = AssetDatabase.FindAssets("Mat_Biker_Helmet", new[] { "Assets/Biker_Helmet_3" });

        bool needsUpgrade = false;
        foreach (string guid in materialPaths)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.EndsWith(".mat"))
            {
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat != null && mat.shader.name == "Standard")
                {
                    needsUpgrade = true;
                    break;
                }
            }
        }

        if (needsUpgrade)
        {
            Debug.Log("[UpgradeBikerHelmetMaterials] Biker Helmet materials need URP upgrade. Upgrading now...");
            UpgradeMaterials();
        }
    }

    [MenuItem("Kiloverse/Upgrade Biker Helmet Materials to URP")]
    public static void UpgradeMaterials()
    {
        // Find URP/Lit shader
        Shader urpLitShader = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLitShader == null)
        {
            Debug.LogError("[UpgradeBikerHelmetMaterials] URP/Lit shader not found!");
            return;
        }

        // Find all Biker Helmet materials
        string[] materialGuids = AssetDatabase.FindAssets("Mat_Biker_Helmet", new[] { "Assets/Biker_Helmet_3" });

        int upgraded = 0;
        foreach (string guid in materialGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".mat")) continue;

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;

            // Skip if already URP
            if (mat.shader.name.Contains("Universal Render Pipeline")) continue;

            // Store old textures
            Texture mainTex = mat.GetTexture("_MainTex");
            Texture normalMap = mat.GetTexture("_BumpMap");
            Texture metallicMap = mat.GetTexture("_MetallicGlossMap");
            Texture occlusionMap = mat.GetTexture("_OcclusionMap");
            Texture emissionMap = mat.GetTexture("_EmissionMap");
            Texture parallaxMap = mat.GetTexture("_ParallaxMap");

            float bumpScale = mat.GetFloat("_BumpScale");
            float glossMapScale = mat.GetFloat("_GlossMapScale");
            float occlusionStrength = mat.GetFloat("_OcclusionStrength");
            float parallax = mat.GetFloat("_Parallax");
            Color color = mat.GetColor("_Color");
            Color emissionColor = mat.GetColor("_EmissionColor");

            // Upgrade shader
            mat.shader = urpLitShader;

            // Remap textures to URP properties
            if (mainTex != null) mat.SetTexture("_BaseMap", mainTex);
            if (normalMap != null) mat.SetTexture("_BumpMap", normalMap);
            if (metallicMap != null) mat.SetTexture("_MetallicGlossMap", metallicMap);
            if (occlusionMap != null) mat.SetTexture("_OcclusionMap", occlusionMap);
            if (emissionMap != null) mat.SetTexture("_EmissionMap", emissionMap);
            if (parallaxMap != null) mat.SetTexture("_ParallaxMap", parallaxMap);

            // Remap properties
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_BumpScale", bumpScale);
            mat.SetFloat("_Smoothness", glossMapScale);
            mat.SetFloat("_OcclusionStrength", occlusionStrength);
            mat.SetFloat("_Parallax", parallax);

            // Enable emission if there's an emission map
            if (emissionMap != null)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emissionColor);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            // Enable normal map
            if (normalMap != null)
            {
                mat.EnableKeyword("_NORMALMAP");
            }

            EditorUtility.SetDirty(mat);
            upgraded++;
            Debug.Log($"[UpgradeBikerHelmetMaterials] Upgraded: {Path.GetFileName(path)}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[UpgradeBikerHelmetMaterials] ✓ Upgraded {upgraded} Biker Helmet materials to URP/Lit");
    }
}
