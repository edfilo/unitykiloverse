using System;
using Mapbox.BaseModule.Map;
using Mapbox.BaseModule.Utilities;
using Mapbox.VectorModule.MeshGeneration.GameObjectModifiers;
using UnityEngine;

[Serializable]
public class POIVisualizerModifier : GameObjectModifier
{
    private static bool _hasLoggedOnce = false;

    public override void Run(VectorEntity ve, IMapInformation mapInformation)
    {
        if (ve.GameObject == null) return;

        ve.GameObject.SetActive(true);

        // COMPREHENSIVE DIAGNOSTIC - Log FIRST POI only
        if (!_hasLoggedOnce)
        {
            _hasLoggedOnce = true;
            Debug.Log("====== POI PROPERTY DIAGNOSTIC ======");
            Debug.Log($"GameObject: {ve.GameObject.name}");
            Debug.Log($"Feature != null: {ve.Feature != null}");

            if (ve.Feature != null)
            {
                Debug.Log($"Feature.Data != null: {ve.Feature.Data != null}");
                if (ve.Feature.Data != null)
                {
                    Debug.Log($"Feature.Data.Id: {ve.Feature.Data.Id}");
                    Debug.Log($"Feature.Data.Layer: {ve.Feature.Data.Layer}");

                    var dataProps = ve.Feature.Data.GetProperties();
                    Debug.Log($"Data.GetProperties() returned: {(dataProps == null ? "NULL" : dataProps.Count + " props")}");
                    if (dataProps != null && dataProps.Count > 0)
                    {
                        Debug.Log("Data.GetProperties() contents:");
                        foreach (var kvp in dataProps)
                        {
                            Debug.Log($"  [{kvp.Key}] = {kvp.Value}");
                        }
                    }
                }

                Debug.Log($"Feature.Properties != null: {ve.Feature.Properties != null}");
                if (ve.Feature.Properties != null)
                {
                    Debug.Log($"Feature.Properties.Count: {ve.Feature.Properties.Count}");
                    if (ve.Feature.Properties.Count > 0)
                    {
                        Debug.Log("Feature.Properties contents:");
                        foreach (var kvp in ve.Feature.Properties)
                        {
                            Debug.Log($"  [{kvp.Key}] = {kvp.Value}");
                        }
                    }
                    else
                    {
                        Debug.LogWarning("Feature.Properties is EMPTY!");
                        Debug.LogWarning("Possible reasons:");
                        Debug.LogWarning("1. Using style-optimized tiles that strip unused properties");
                        Debug.LogWarning("2. Mapbox style doesn't use POI labels, so 'name' property excluded");
                        Debug.LogWarning("3. Properties only available at specific zoom levels");
                    }
                }
            }

            Debug.Log("=====================================");
        }

        // Create quad marker
        var marker = GameObject.CreatePrimitive(PrimitiveType.Quad);
        marker.transform.SetParent(ve.GameObject.transform, false);
        marker.transform.localPosition = new Vector3(0f, 30f, 0f);
        marker.transform.localScale = new Vector3(10f, 10f, 1f);

        var renderer = marker.GetComponent<Renderer>();
        if (renderer != null)
        {
            var mat = new Material(Shader.Find("Standard"));
            mat.color = Color.orange;
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Glossiness", 0f);
            renderer.material = mat;
        }

        marker.AddComponent<Billboard>();
    }
}
