using System;
using Mapbox.BaseModule.Map;
using Mapbox.BaseModule.Utilities;
using Mapbox.VectorModule.MeshGeneration.GameObjectModifiers;
using UnityEngine;

[Serializable]
public class DebugLoggingModifier : GameObjectModifier
{
    private int featureCount = 0;

    public override void Run(VectorEntity ve, IMapInformation mapInformation)
    {
        featureCount++;

        Debug.Log($"[DebugLoggingModifier] Feature #{featureCount}");
        Debug.Log($"  - Feature ID: {ve.Feature.Data.Id}");
        Debug.Log($"  - Layer: {ve.Feature.Data.Layer}");
        Debug.Log($"  - Geometry Type: {ve.Feature.Data.GeometryType}");
        Debug.Log($"  - Points count: {ve.Feature.Points.Count}");

        if (ve.Feature.Points.Count > 0 && ve.Feature.Points[0].Count > 0)
        {
            Debug.Log($"  - First point: {ve.Feature.Points[0][0]}");
        }

        // Log all properties
        if (ve.Feature.Properties != null && ve.Feature.Properties.Count > 0)
        {
            Debug.Log($"  - Properties ({ve.Feature.Properties.Count}):");
            foreach (var prop in ve.Feature.Properties)
            {
                Debug.Log($"    - {prop.Key}: {prop.Value}");
            }
        }
        else
        {
            Debug.Log($"  - No properties");
        }

        Debug.Log($"  - Parent Transform: {ve.Transform.name} at {ve.Transform.position}");
        Debug.Log($"  - Map Scale: {mapInformation.Scale}");
    }
}
