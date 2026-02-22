using System;
using System.Collections.Generic;
using UnityEngine;
using Mapbox.BaseModule.Data;
using Mapbox.BaseModule.Map;
using Mapbox.BaseModule.Unity;
using Mapbox.BaseModule.Utilities;
using Mapbox.VectorModule.MeshGeneration.GameObjectModifiers;
using Kiloverse.Mapbox;

/// <summary>
/// Stores road class metadata for each GameObject created by the road visualizer.
/// This allows VirtualGridSpawner to filter which roads are eligible for beam spawning.
/// </summary>
[Serializable]
public class RoadClassTagger : GameObjectModifier
{
    // Static registry: Maps GameObject instance ID -> road class
    private static Dictionary<int, string> s_RoadClasses = new Dictionary<int, string>();

    public static string GetRoadClass(GameObject go)
    {
        if (go == null) return null;
        s_RoadClasses.TryGetValue(go.GetInstanceID(), out string roadClass);
        return roadClass;
    }

public static new void Clear()
    {
        s_RoadClasses.Clear();
    }

    public override void Run(VectorEntity ve, IMapInformation mapInformation)
    {
        // Extract road class from feature properties
        string roadClass = ve.Feature.Properties.ContainsKey("class")
            ? ve.Feature.Properties["class"].ToString()
            : "unknown";

        // Store in metadata component (ve.GameObject is created by the time modifiers run)
        if (ve.GameObject != null)
        {
            var metadata = ve.GameObject.GetComponent<RoadSegmentMetadata>();
            if (metadata == null)
            {
                metadata = ve.GameObject.AddComponent<RoadSegmentMetadata>();
            }
            metadata.roadClass = roadClass;

            // Also store in static registry for quick lookup
            s_RoadClasses[ve.GameObject.GetInstanceID()] = roadClass;
        }
    }

    public override void OnPoolItem(VectorEntity ve)
    {
        // Clean up when GameObject is pooled/destroyed
        if (ve.GameObject != null)
        {
            s_RoadClasses.Remove(ve.GameObject.GetInstanceID());
        }
    }
}
