using UnityEngine;
using UnityEditor;

public class CheckTextureImportSettings : EditorWindow
{
    [MenuItem("Tools/Check Brick Texture Settings")]
    static void CheckSettings()
    {
        // Check normal map
        string normalPath = "Assets/BuildingTextures/Bricks097_1K-JPG_NormalGL.jpg";
        TextureImporter normalImporter = AssetImporter.GetAtPath(normalPath) as TextureImporter;

        if (normalImporter != null)
        {
            bool needsReimport = false;

            // Check if it's set as a normal map
            if (normalImporter.textureType != TextureImporterType.NormalMap)
            {
                Debug.LogWarning($"[TextureCheck] Normal map is NOT imported as Normal Map type! Fixing...");
                normalImporter.textureType = TextureImporterType.NormalMap;
                needsReimport = true;
            }

            // Check wrap mode (should be Repeat for tiling)
            if (normalImporter.wrapMode != TextureWrapMode.Repeat)
            {
                Debug.LogWarning($"[TextureCheck] Normal map wrap mode is {normalImporter.wrapMode}, setting to Repeat...");
                normalImporter.wrapMode = TextureWrapMode.Repeat;
                needsReimport = true;
            }

            if (needsReimport)
            {
                normalImporter.SaveAndReimport();
                Debug.Log("[TextureCheck] Normal map reimported with correct settings!");
            }
            else
            {
                Debug.Log($"[TextureCheck] Normal map settings OK: Type={normalImporter.textureType}, Wrap={normalImporter.wrapMode}");
            }
        }

        // Check albedo map
        string albedoPath = "Assets/BuildingTextures/Bricks097_1K-JPG_Color.jpg";
        TextureImporter albedoImporter = AssetImporter.GetAtPath(albedoPath) as TextureImporter;

        if (albedoImporter != null)
        {
            bool needsReimport = false;

            if (albedoImporter.wrapMode != TextureWrapMode.Repeat)
            {
                Debug.LogWarning($"[TextureCheck] Albedo wrap mode is {albedoImporter.wrapMode}, setting to Repeat...");
                albedoImporter.wrapMode = TextureWrapMode.Repeat;
                needsReimport = true;
            }

            if (needsReimport)
            {
                albedoImporter.SaveAndReimport();
                Debug.Log("[TextureCheck] Albedo reimported with correct settings!");
            }
            else
            {
                Debug.Log($"[TextureCheck] Albedo settings OK: Wrap={albedoImporter.wrapMode}");
            }
        }

        // Check occlusion map
        string aoPath = "Assets/BuildingTextures/Bricks097_1K-JPG_AmbientOcclusion.jpg";
        TextureImporter aoImporter = AssetImporter.GetAtPath(aoPath) as TextureImporter;

        if (aoImporter != null)
        {
            bool needsReimport = false;

            if (aoImporter.wrapMode != TextureWrapMode.Repeat)
            {
                Debug.LogWarning($"[TextureCheck] AO wrap mode is {aoImporter.wrapMode}, setting to Repeat...");
                aoImporter.wrapMode = TextureWrapMode.Repeat;
                needsReimport = true;
            }

            if (needsReimport)
            {
                aoImporter.SaveAndReimport();
                Debug.Log("[TextureCheck] AO map reimported with correct settings!");
            }
            else
            {
                Debug.Log($"[TextureCheck] AO settings OK: Wrap={aoImporter.wrapMode}");
            }
        }

        EditorUtility.DisplayDialog("Texture Import Check",
            "Check complete! See console for details.\n\nIf textures were fixed, they'll be reimported automatically.",
            "OK");
    }
}
