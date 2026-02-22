using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Linq;

public class AddVolumetricFogFeature : MonoBehaviour
{
    [MenuItem("Tools/Add Volumetric Fog Render Feature")]
    static void AddFeature()
    {
        // Find the PC_Renderer asset
        string[] guids = AssetDatabase.FindAssets("t:UniversalRendererData PC_Renderer");

        if (guids.Length == 0)
        {
            Debug.LogError("[AddVolumetricFogFeature] Could not find PC_Renderer asset!");
            return;
        }

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        UniversalRendererData rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);

        if (rendererData == null)
        {
            Debug.LogError($"[AddVolumetricFogFeature] Failed to load renderer at path: {path}");
            return;
        }

        // Check if VolumetricFogRenderFeature already exists
        var features = rendererData.rendererFeatures;
        bool alreadyHasFog = features.Any(f => f != null && f.GetType().Name == "VolumetricFogRenderFeature");

        if (alreadyHasFog)
        {
            Debug.Log("[AddVolumetricFogFeature] VolumetricFogRenderFeature already exists in renderer!");
            return;
        }

        // Find the VolumetricFogRenderFeature type by searching all assemblies
        System.Type fogFeatureType = null;
        foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            fogFeatureType = assembly.GetType("VolumetricFogAndMist2.VolumetricFogRenderFeature");
            if (fogFeatureType != null) break;
        }

        if (fogFeatureType == null)
        {
            Debug.LogError("[AddVolumetricFogFeature] Could not find VolumetricFogRenderFeature type! Make sure the package is fully imported and Unity has recompiled.");
            return;
        }

        // Create instance of the render feature
        var fogFeature = ScriptableObject.CreateInstance(fogFeatureType) as ScriptableRendererFeature;

        if (fogFeature == null)
        {
            Debug.LogError("[AddVolumetricFogFeature] Failed to create VolumetricFogRenderFeature instance!");
            return;
        }

        fogFeature.name = "VolumetricFogRenderFeature";

        // Add the feature to the renderer
        AssetDatabase.AddObjectToAsset(fogFeature, rendererData);

        // Use reflection to add to the m_RendererFeatures list
        var serializedObject = new SerializedObject(rendererData);
        var featuresProperty = serializedObject.FindProperty("m_RendererFeatures");
        featuresProperty.arraySize++;
        var newFeatureProperty = featuresProperty.GetArrayElementAtIndex(featuresProperty.arraySize - 1);
        newFeatureProperty.objectReferenceValue = fogFeature;

        serializedObject.ApplyModifiedProperties();

        // Mark assets as dirty and save
        EditorUtility.SetDirty(rendererData);
        EditorUtility.SetDirty(fogFeature);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[AddVolumetricFogFeature] Successfully added VolumetricFogRenderFeature to {path}");
    }
}
