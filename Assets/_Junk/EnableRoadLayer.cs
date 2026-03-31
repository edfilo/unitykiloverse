using UnityEngine;
using UnityEditor;
using System.Linq;

/// <summary>
/// Editor script to enable the road layer visualizer on KiloMap
/// </summary>
public class EnableRoadLayer : MonoBehaviour
{
    [ContextMenu("Enable Road Layer")]
    void EnableRoads()
    {
        GameObject kiloMap = GameObject.Find("KiloMap");
        if (kiloMap == null)
        {
            Debug.LogError("[EnableRoadLayer] KiloMap not found!");
            return;
        }

        // Get the VectorLayerModuleScript component
        var vectorModule = kiloMap.GetComponent(System.Type.GetType("Mapbox.VectorModule.Unity.VectorLayerModuleScript, MapboxVectorModule"));
        if (vectorModule == null)
        {
            Debug.LogError("[EnableRoadLayer] VectorLayerModuleScript not found on KiloMap!");
            return;
        }

        #if UNITY_EDITOR
        // Load the road visualizer asset
        var roadViz = AssetDatabase.LoadAssetAtPath("Assets/KiloverseMapbox/LayerVisualizers/RoadLayerVisualizer.asset",
            System.Type.GetType("Mapbox.VectorModule.Unity.VectorLayerVisualizerObject, MapboxVectorModule"));
        
        var buildingViz = AssetDatabase.LoadAssetAtPath("Assets/KiloverseMapbox/LayerVisualizers/BuildingLayerVisualizer.asset",
            System.Type.GetType("Mapbox.VectorModule.Unity.VectorLayerVisualizerObject, MapboxVectorModule"));

        if (roadViz == null)
        {
            Debug.LogError("[EnableRoadLayer] RoadLayerVisualizer.asset not found!");
            return;
        }

        if (buildingViz == null)
        {
            Debug.LogError("[EnableRoadLayer] BuildingLayerVisualizer.asset not found!");
            return;
        }

        // Use SerializedObject to modify the private _layerVisualizers field
        SerializedObject so = new SerializedObject(vectorModule);
        SerializedProperty visualizersField = so.FindProperty("_layerVisualizers");
        
        if (visualizersField != null && visualizersField.isArray)
        {
            // Set array size to 2 (buildings + roads)
            visualizersField.arraySize = 2;
            visualizersField.GetArrayElementAtIndex(0).objectReferenceValue = buildingViz;
            visualizersField.GetArrayElementAtIndex(1).objectReferenceValue = roadViz;
            
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(vectorModule as UnityEngine.Object);
            
            Debug.Log("[EnableRoadLayer] ✓✓ Road layer visualizer ENABLED! Road layer should now appear.");
            Debug.Log("[EnableRoadLayer] Visualizers: Buildings + Roads");
        }
        else
        {
            Debug.LogError("[EnableRoadLayer] Could not find _layerVisualizers field!");
        }
        #else
        Debug.LogWarning("[EnableRoadLayer] This script only works in the Unity Editor!");
        #endif
    }

    void Start()
    {
        // Auto-run on start
        EnableRoads();
        
        // Self-destruct after running
        Destroy(this);
    }
}
