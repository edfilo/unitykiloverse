using System;
using UnityEngine;
using Mapbox.BaseModule.Data;
using Mapbox.BaseModule.Map;
using Mapbox.BaseModule.Unity;
using Mapbox.BaseModule.Utilities;
using Mapbox.VectorModule.MeshGeneration.GameObjectModifiers;

/// <summary>
/// Calculates building height from num_floors if height is missing.
/// Uses 4 meters per floor as default.
/// Runs as a GameObject modifier to inject height before mesh generation.
/// </summary>
[Serializable]
public class BuildingHeightCalculator : GameObjectModifier
{
    public float metersPerFloor = 4f;
    public float minimumHeight = 8f;

    public override void Run(VectorEntity ve, IMapInformation mapInformation)
    {
        if (ve == null || ve.Feature == null || ve.Feature.Properties == null) return;

        // Check what properties exist
        bool hasHeight = ve.Feature.Properties.ContainsKey("height");
        bool hasRenderHeight = ve.Feature.Properties.ContainsKey("render_height");
        bool hasNumFloors = ve.Feature.Properties.ContainsKey("num_floors");

        float finalHeight = minimumHeight;
        string source = "minimum";

        // Priority: height > render_height > num_floors > minimum
        if (hasHeight)
        {
            try
            {
                float heightValue = Convert.ToSingle(ve.Feature.Properties["height"]);
                if (heightValue > 0)
                {
                    finalHeight = heightValue;
                    source = "height";
                }
                else if (hasRenderHeight)
                {
                    float renderHeightValue = Convert.ToSingle(ve.Feature.Properties["render_height"]);
                    if (renderHeightValue > 0)
                    {
                        finalHeight = renderHeightValue;
                        source = "render_height";
                    }
                    else if (hasNumFloors)
                    {
                        float numFloors = Convert.ToSingle(ve.Feature.Properties["num_floors"]);
                        if (numFloors > 0)
                        {
                            finalHeight = numFloors * metersPerFloor;
                            source = $"num_floors({numFloors})";
                        }
                    }
                }
                else if (hasNumFloors)
                {
                    float numFloors = Convert.ToSingle(ve.Feature.Properties["num_floors"]);
                    if (numFloors > 0)
                    {
                        finalHeight = numFloors * metersPerFloor;
                        source = $"num_floors({numFloors})";
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BuildingHeightCalculator] Failed to parse height: {ex.Message}");
            }
        }
        else if (hasRenderHeight)
        {
            try
            {
                float renderHeightValue = Convert.ToSingle(ve.Feature.Properties["render_height"]);
                if (renderHeightValue > 0)
                {
                    finalHeight = renderHeightValue;
                    source = "render_height";
                }
                else if (hasNumFloors)
                {
                    float numFloors = Convert.ToSingle(ve.Feature.Properties["num_floors"]);
                    if (numFloors > 0)
                    {
                        finalHeight = numFloors * metersPerFloor;
                        source = $"num_floors({numFloors})";
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BuildingHeightCalculator] Failed to parse render_height: {ex.Message}");
            }
        }
        else if (hasNumFloors)
        {
            try
            {
                float numFloors = Convert.ToSingle(ve.Feature.Properties["num_floors"]);
                if (numFloors > 0)
                {
                    finalHeight = numFloors * metersPerFloor;
                    source = $"num_floors({numFloors})";
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BuildingHeightCalculator] Failed to parse num_floors: {ex.Message}");
            }
        }

        // Set the final height
        ve.Feature.Properties["height"] = finalHeight;

        // Log tall buildings (potential USX/BNY matches)
        if (finalHeight >= 200f || UnityEngine.Random.value < 0.001f)
        {
            Debug.Log($"[BuildingHeightCalculator] {finalHeight:F1}m from '{source}' | has: h={hasHeight} rh={hasRenderHeight} nf={hasNumFloors}");
        }
    }
}
