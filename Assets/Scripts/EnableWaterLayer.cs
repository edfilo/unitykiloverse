using UnityEngine;
using UnityEditor;
using System.Linq;

/// <summary>
/// Editor script to enable the water layer visualizer on KiloMap
/// </summary>
public class EnableWaterLayer : MonoBehaviour
{
    [ContextMenu("Enable Water Layer")]
    void EnableWater()
    {
        GameObject kiloMap = GameObject.Find("KiloMap");
        if (kiloMap == null)
        {
            Debug.LogError("[EnableWaterLayer] KiloMap not found!");
            return;
        }

        // Get the VectorLayerModuleScript component
        var vectorModule = kiloMap.GetComponent(System.Type.GetType("Mapbox.VectorModule.Unity.VectorLayerModuleScript, MapboxVectorModule"));
        if (vectorModule == null)
        {
            Debug.LogError("[EnableWaterLayer] VectorLayerModuleScript not found on KiloMap!");
            return;
        }

        #if UNITY_EDITOR
        var vizType = System.Type.GetType("Mapbox.VectorModule.Unity.VectorLayerVisualizerObject, MapboxVectorModule");

        // Load visualizer assets
        var buildingViz = AssetDatabase.LoadAssetAtPath("Assets/KiloverseMapbox/LayerVisualizers/BuildingLayerVisualizer.asset", vizType);
        var roadViz = AssetDatabase.LoadAssetAtPath("Assets/KiloverseMapbox/LayerVisualizers/RoadLayerVisualizer.asset", vizType);
        var waterViz = AssetDatabase.LoadAssetAtPath("Assets/KiloverseMapbox/LayerVisualizers/WaterLayerVisualizer.asset", vizType);

        if (buildingViz == null) Debug.LogError("[EnableWaterLayer] BuildingLayerVisualizer.asset not found!");
        if (roadViz == null) Debug.LogError("[EnableWaterLayer] RoadLayerVisualizer.asset not found!");
        if (waterViz == null) Debug.LogError("[EnableWaterLayer] WaterLayerVisualizer.asset not found! (Did CreateWaterAssets run?)");

        if (buildingViz != null && roadViz != null && waterViz != null)
        {
            // Use SerializedObject to modify the private _layerVisualizers field
            SerializedObject so = new SerializedObject(vectorModule);
            SerializedProperty visualizersField = so.FindProperty("_layerVisualizers");
            
            if (visualizersField != null && visualizersField.isArray)
            {
                // Set array size to 3 (Buildings + Roads + Water)
                visualizersField.arraySize = 3;
                visualizersField.GetArrayElementAtIndex(0).objectReferenceValue = buildingViz;
                visualizersField.GetArrayElementAtIndex(1).objectReferenceValue = roadViz;
                visualizersField.GetArrayElementAtIndex(2).objectReferenceValue = waterViz;
                
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(vectorModule as UnityEngine.Object);
                
                Debug.Log("[EnableWaterLayer] ✓✓ Water layer visualizer ENABLED! Visualizers: Buildings + Roads + Water");
            }
            else
            {
                Debug.LogError("[EnableWaterLayer] Could not find _layerVisualizers field!");
            }
        }
        #else
        Debug.LogWarning("[EnableWaterLayer] This script only works in the Unity Editor!");
        #endif
    }

    void Start()
    {
        // Auto-run on start
        EnableWater();
        
        // Self-destruct after running
        Destroy(this);
    }
}
