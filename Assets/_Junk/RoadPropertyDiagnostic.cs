using System;
using Mapbox.BaseModule.Map;
using Mapbox.BaseModule.Utilities;
using Mapbox.VectorModule.MeshGeneration.GameObjectModifiers;
using UnityEngine;

/// <summary>
/// Diagnostic modifier to log all road properties from Mapbox tiles.
/// Add this to your Road Layer Visualizer to see what metadata is available.
/// </summary>
[Serializable]
public class RoadPropertyDiagnostic : GameObjectModifier
{
    private int roadCount = 0;
    public bool logAllRoads = false; // Set true to log every road (verbose!)
    public bool logOnlyFirstRoad = true; // Only log the first road

    public override void Run(VectorEntity ve, IMapInformation mapInformation)
    {
        roadCount++;

        // Only log first road by default to avoid spam
        if (logOnlyFirstRoad && roadCount > 1 && !logAllRoads)
            return;

        Debug.Log($"<color=cyan>========== ROAD #{roadCount} ==========</color>");
        Debug.Log($"Road ID: {ve.Feature.Data.Id}");
        Debug.Log($"Layer: {ve.Feature.Data.Layer}");
        Debug.Log($"Geometry Type: {ve.Feature.Data.GeometryType}");
        Debug.Log($"Points: {ve.Feature.Points.Count} line(s)");

        // Log all available properties
        if (ve.Feature.Properties != null && ve.Feature.Properties.Count > 0)
        {
            Debug.Log($"<color=yellow>Properties ({ve.Feature.Properties.Count}):</color>");
            foreach (var prop in ve.Feature.Properties)
            {
                Debug.Log($"  • {prop.Key} = {prop.Value}");

                // Highlight width/lane data
                if (prop.Key.ToLower().Contains("width") || prop.Key.ToLower().Contains("lane"))
                {
                    Debug.Log($"    <color=green>^^^ WIDTH/LANE DATA FOUND! ^^^</color>");
                }
            }

            // Check for common road properties
            CheckProperty(ve, "class");
            CheckProperty(ve, "type");
            CheckProperty(ve, "width");
            CheckProperty(ve, "lanes");
            CheckProperty(ve, "structure");
            CheckProperty(ve, "oneway");
            CheckProperty(ve, "name");
        }
        else
        {
            Debug.LogWarning("  <color=red>NO PROPERTIES FOUND!</color>");
            Debug.LogWarning("  This usually means:");
            Debug.LogWarning("  1. Using style-optimized tiles that strip unused properties");
            Debug.LogWarning("  2. Need to enable property access in Mapbox style");
        }

        Debug.Log($"<color=cyan>================================</color>\n");
    }

    void CheckProperty(VectorEntity ve, string key)
    {
        if (ve.Feature.Properties.ContainsKey(key))
        {
            Debug.Log($"  <color=lime>✓ {key}: {ve.Feature.Properties[key]}</color>");
        }
        else
        {
            Debug.Log($"  <color=gray>✗ {key}: not available</color>");
        }
    }
}
