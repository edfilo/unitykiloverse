using UnityEngine;
using UnityEditor;
using Mapbox.VectorModule.MeshGeneration.MeshModifiers.ScriptableObjects;
using Mapbox.VectorModule.MeshGeneration.GameObjectModifiers;
using Mapbox.VectorModule.MeshGeneration.Unity; // For ModifierStackObject
using Mapbox.VectorModule.Unity; // For VectorLayerVisualizerObject
using System.Collections.Generic;

[InitializeOnLoad]
public class CreateWaterAssets
{
    static CreateWaterAssets()
    {
        EditorApplication.delayCall += CheckAndCreate;
    }

    private static void CheckAndCreate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        string checkPath = "Assets/KiloverseMapbox/LayerVisualizers/WaterLayerVisualizer.asset";
        if (AssetDatabase.LoadAssetAtPath<Object>(checkPath) == null)
        {
            Debug.Log("[CreateWaterAssets] Water assets missing. Creating now...");
            CreateAssets();
        }
    }

    [MenuItem("Kiloverse/Create Water Assets")]
    public static void CreateAssets()
    {
        // 1. Create Material
        Material waterMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if (waterMat == null) waterMat = new Material(Shader.Find("Standard"));
        
        waterMat.color = new Color(0f, 0.4f, 0.8f, 0.8f); // Blue semi-transparent
        waterMat.SetFloat("_Smoothness", 0.9f);
        waterMat.SetFloat("_Metallic", 0.0f);
        
        string matPath = "Assets/KiloverseMapbox/Materials/Water.mat";
        AssetDatabase.CreateAsset(waterMat, matPath);

        // 2. Create Modifiers
        
        // Polygon Mesh Modifier (no height offset - use visualizer offset instead)
        var polyMod = ScriptableObject.CreateInstance<PolygonMeshModifierObject>();
        string polyPath = "Assets/KiloverseMapbox/Modifiers/WaterPolygonMesh.asset";
        AssetDatabase.CreateAsset(polyMod, polyPath);

        // Snap Terrain Modifier
        var snapMod = ScriptableObject.CreateInstance<SnapTerrainModifierObject>();
        string snapPath = "Assets/KiloverseMapbox/Modifiers/WaterSnapTerrain.asset";
        AssetDatabase.CreateAsset(snapMod, snapPath);

        // Material Modifier
        var matMod = ScriptableObject.CreateInstance<MaterialModifierObject>();
        matMod.Materials = new Material[] { waterMat };
        string matModPath = "Assets/KiloverseMapbox/Modifiers/WaterMaterial.asset";
        AssetDatabase.CreateAsset(matMod, matModPath);

        // 3. Create Modifier Stack (ModifierStackObject)
        var stack = ScriptableObject.CreateInstance<ModifierStackObject>();
        string stackPath = "Assets/KiloverseMapbox/ModifierStacks/WaterStack.asset";
        AssetDatabase.CreateAsset(stack, stackPath);

        SerializedObject soStack = new SerializedObject(stack);
        
        // Set Layer Type in Settings
        soStack.FindProperty("Settings").FindPropertyRelative("LayerType").enumValueIndex = 0; // Polygon
        
        // Add Mesh Modifiers
        var meshModsProp = soStack.FindProperty("MeshModifiers");
        meshModsProp.arraySize = 2;
        meshModsProp.GetArrayElementAtIndex(0).objectReferenceValue = polyMod;
        meshModsProp.GetArrayElementAtIndex(1).objectReferenceValue = snapMod;

        // Add GameObject Modifiers
        var goModsProp = soStack.FindProperty("GoModifiers");
        goModsProp.arraySize = 1;
        goModsProp.GetArrayElementAtIndex(0).objectReferenceValue = matMod;

        soStack.ApplyModifiedProperties();

        // 4. Create Visualizer (VectorLayerVisualizerObject)
        var visualizer = ScriptableObject.CreateInstance<VectorLayerVisualizerObject>();
        string vizPath = "Assets/KiloverseMapbox/LayerVisualizers/WaterLayerVisualizer.asset";
        AssetDatabase.CreateAsset(visualizer, vizPath);

        SerializedObject soViz = new SerializedObject(visualizer);

        // Set Layer Name (Key)
        soViz.FindProperty("_vectorLayerName").stringValue = "water";

        // Set Y Offset - water at 0.015, roads at 0.02 (so bridges appear above water)
        var settingsProp = soViz.FindProperty("_settings");
        var offsetProp = settingsProp.FindPropertyRelative("Offset");
        offsetProp.vector3Value = new Vector3(0, 0.015f, 0);

        // Add Stack to List
        var stacksProp = soViz.FindProperty("_modifierStackObjects");
        stacksProp.arraySize = 1;
        stacksProp.GetArrayElementAtIndex(0).objectReferenceValue = stack;

        soViz.ApplyModifiedProperties();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[CreateWaterAssets] ✓✓ Created Water Assets successfully!");
    }
}
