using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using TMPro;
using System.IO;

// Creates Assets/TextMesh Pro/Resources/Fonts & Materials/MartianMono SDF.asset
// from the bundled Martian Mono TTF, sets the material to white + drop shadow,
// and saves it as a Resources-loadable asset. The runtime ForceMartianMonoFont
// component swaps every TMP_Text.font to this asset at scene load.
//
// CLI: Unity -batchmode -quit -executeMethod MartianMonoFontGenerator.Generate
public static class MartianMonoFontGenerator
{
    const string SourceTtfPath  = "Assets/TextMesh Pro/Fonts/MartianMono-Regular.ttf";
    const string TargetAssetPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/MartianMono SDF.asset";

    public static void Generate()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        var ttf = AssetDatabase.LoadAssetAtPath<Font>(SourceTtfPath);
        if (ttf == null)
        {
            Debug.LogError($"[MartianMono] Source TTF not found at {SourceTtfPath}");
            EditorApplication.Exit(1);
            return;
        }

        var fa = TMP_FontAsset.CreateFontAsset(
            ttf,
            samplingPointSize: 90,
            atlasPadding: 9,
            renderMode: GlyphRenderMode.SDFAA,
            atlasWidth: 4096,
            atlasHeight: 4096,
            atlasPopulationMode: AtlasPopulationMode.Dynamic,
            enableMultiAtlasSupport: true);

        if (fa == null)
        {
            Debug.LogError("[MartianMono] CreateFontAsset returned null");
            EditorApplication.Exit(1);
            return;
        }

        // Save the font asset BEFORE configuring the material so the material
        // gets associated with the persisted asset (TMP creates a child material).
        if (File.Exists(TargetAssetPath)) AssetDatabase.DeleteAsset(TargetAssetPath);
        AssetDatabase.CreateAsset(fa, TargetAssetPath);

        // Add the font's atlas texture and material as sub-assets so they
        // survive build (CreateFontAsset returns a fully-formed asset; we just
        // need to make sure children are persisted under it).
        if (fa.atlasTexture != null && AssetDatabase.GetAssetPath(fa.atlasTexture) == "")
            AssetDatabase.AddObjectToAsset(fa.atlasTexture, fa);
        if (fa.material != null && AssetDatabase.GetAssetPath(fa.material) == "")
            AssetDatabase.AddObjectToAsset(fa.material, fa);

        ConfigureMaterial(fa.material);
        EditorUtility.SetDirty(fa);
        if (fa.material != null) EditorUtility.SetDirty(fa.material);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[MartianMono] ✓ Created {TargetAssetPath} (atlas={fa.atlasTexture?.width}x{fa.atlasTexture?.height}, material={(fa.material!=null?"ok":"NULL")})");
    }

    static void ConfigureMaterial(Material mat)
    {
        if (mat == null) return;

        mat.SetColor(ShaderUtilities.ID_FaceColor, Color.white);

        // Strong drop shadow — black underlay offset down-right with soft edge,
        // tuned heavy enough to read on busy backgrounds without a UI panel behind.
        mat.EnableKeyword("UNDERLAY_ON");
        mat.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 1f));
        mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 1.2f);
        mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -1.2f);
        mat.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.3f);
        mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.55f);
    }
}
