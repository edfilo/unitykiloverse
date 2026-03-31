// One-shot editor script to generate TMP SDF font assets for Inter Light and Regular.
// Run via menu: Tools > Generate Inter Font Assets
// Safe to delete after running.

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using TMPro;
using TMPro.EditorUtilities;

public static class GenerateInterFontAssets
{
    [MenuItem("Tools/Generate Inter Font Assets")]
    public static void Generate()
    {
        GenerateAsset("Assets/Fonts/Inter/Inter-Light.ttf", "Inter-Light SDF");
        GenerateAsset("Assets/Fonts/Inter/Inter-Regular.ttf", "Inter-Regular SDF");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[GenerateInterFontAssets] Done. Font assets saved to Assets/Resources/Fonts/");
    }

    static void GenerateAsset(string ttfPath, string assetName)
    {
        var font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
        if (font == null)
        {
            Debug.LogError($"[GenerateInterFontAssets] Font not found at {ttfPath}");
            return;
        }

        // Use TMP's font asset creation API
        var fontAsset = TMP_FontAsset.CreateFontAsset(font, 42, 5,
            UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
            512, 512,
            AtlasPopulationMode.Dynamic);

        if (fontAsset == null)
        {
            Debug.LogError($"[GenerateInterFontAssets] Failed to create font asset for {ttfPath}");
            return;
        }

        fontAsset.name = assetName;

        // Save to Resources/Fonts/ so they're loadable via Resources.Load
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Fonts"))
        {
            AssetDatabase.CreateFolder("Assets/Resources", "Fonts");
        }
        string outputPath = $"Assets/Resources/Fonts/{assetName}.asset";
        AssetDatabase.CreateAsset(fontAsset, outputPath);

        // Also save the atlas texture
        if (fontAsset.atlasTexture != null)
        {
            fontAsset.atlasTexture.name = assetName + " Atlas";
            AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);
        }
        if (fontAsset.material != null)
        {
            fontAsset.material.name = assetName + " Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        }

        Debug.Log($"[GenerateInterFontAssets] Created {outputPath}");
    }
}
#endif
