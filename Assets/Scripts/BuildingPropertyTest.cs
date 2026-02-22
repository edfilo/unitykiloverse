using System;
using Mapbox.BaseModule.Map;
using Mapbox.BaseModule.Utilities;
using Mapbox.VectorModule.MeshGeneration.GameObjectModifiers;
using UnityEngine;

[Serializable]
public class BuildingPropertyTest : GameObjectModifier
{
    private static int _logCount = 0;
    private static int _maxLogs = 2;

    public override void Run(VectorEntity ve, IMapInformation mapInformation)
    {
        if (_logCount >= _maxLogs) return;
        _logCount++;

        Debug.Log($"====== BUILDING TEST #{_logCount} ======");
        Debug.Log($"GameObject: {ve.GameObject?.name}");
        Debug.Log($"Feature null? {ve.Feature == null}");

        if (ve.Feature != null)
        {
            Debug.Log($"Feature.Data null? {ve.Feature.Data == null}");
            if (ve.Feature.Data != null)
            {
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

            Debug.Log($"Feature.Properties null? {ve.Feature.Properties == null}");
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
            }
        }
        Debug.Log("===================================");
    }
}
