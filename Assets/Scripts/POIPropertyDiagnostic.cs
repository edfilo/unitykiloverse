using System;
using Mapbox.BaseModule.Map;
using Mapbox.BaseModule.Utilities;
using Mapbox.VectorModule.MeshGeneration.GameObjectModifiers;
using UnityEngine;

[Serializable]
public class POIPropertyDiagnostic : GameObjectModifier
{
    private static bool _hasLoggedOnce = false;

    public override void Run(VectorEntity ve, IMapInformation mapInformation)
    {
        if (ve.GameObject == null) return;
        
        // Only log once to avoid spam
        if (_hasLoggedOnce) return;
        _hasLoggedOnce = true;

        Debug.Log("====== POI PROPERTY DIAGNOSTIC ======");
        
        // Check if we have feature data
        if (ve.Feature == null)
        {
            Debug.LogError("Feature is NULL!");
            return;
        }

        // Check Data object
        if (ve.Feature.Data == null)
        {
            Debug.LogError("Feature.Data is NULL!");
            return;
        }

        Debug.Log($"TileId: {ve.Feature.TileId}");
        Debug.Log($"Feature.Data.Id: {ve.Feature.Data.Id}");
        Debug.Log($"Feature.Data.Layer: {ve.Feature.Data.Layer}");
        
        // Check Properties dictionary (set by VectorLayerVisualizer)
        Debug.Log($"Feature.Properties null? {ve.Feature.Properties == null}");
        if (ve.Feature.Properties != null)
        {
            Debug.Log($"Feature.Properties.Count: {ve.Feature.Properties.Count}");
            if (ve.Feature.Properties.Count > 0)
            {
                Debug.Log("Properties found:");
                foreach (var kvp in ve.Feature.Properties)
                {
                    Debug.Log($"  [{kvp.Key}] = {kvp.Value}");
                }
            }
            else
            {
                Debug.LogWarning("Properties dictionary exists but is EMPTY!");
                Debug.LogWarning("This usually means:");
                Debug.LogWarning("1. Using style-optimized tiles that exclude unused properties");
                Debug.LogWarning("2. The Mapbox style doesn't use POI labels, so 'name' was stripped");
                Debug.LogWarning("3. Need to switch to non-optimized tiles or modify the style");
            }
        }

        // Try calling GetProperties() directly on Data
        Debug.Log("Calling Feature.Data.GetProperties()...");
        var dataProps = ve.Feature.Data.GetProperties();
        Debug.Log($"Data.GetProperties() returned: {(dataProps == null ? "NULL" : dataProps.Count + " properties")}");
        if (dataProps != null && dataProps.Count > 0)
        {
            Debug.Log("Data properties found:");
            foreach (var kvp in dataProps)
            {
                Debug.Log($"  [{kvp.Key}] = {kvp.Value}");
            }
        }

        Debug.Log("=====================================");
    }
}
