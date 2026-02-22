using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Reflection;

[ExecuteAlways]
public class RenderManagerDebug : MonoBehaviour
{
    public bool logOnUpdate = false;

    [ContextMenu("Debug SSAO Settings")]
    public void DebugSSAO()
    {
        var urpAsset = UniversalRenderPipeline.asset;
        if (urpAsset == null)
        {
            Debug.LogError("No URP Asset found!");
            return;
        }

        Debug.Log($"URP Asset: {urpAsset.name}");

        var renderer = urpAsset.scriptableRenderer;
        if (renderer == null)
        {
            Debug.LogError("No ScriptableRenderer found!");
            return;
        }

        var type = typeof(ScriptableRenderer);
        var fieldInfo = type.GetField("m_RendererFeatures", BindingFlags.NonPublic | BindingFlags.Instance);
        
        if (fieldInfo == null)
        {
            Debug.LogError("Could not find m_RendererFeatures field!");
            return;
        }

        var features = fieldInfo.GetValue(renderer) as System.Collections.Generic.List<ScriptableRendererFeature>;
        if (features == null)
        {
            Debug.LogError("Features list is null!");
            return;
        }

        Debug.Log($"Found {features.Count} renderer features.");

        foreach (var feature in features)
        {
            if (feature == null) continue;
            
            Debug.Log($"Feature: {feature.name} (Type: {feature.GetType().Name}) - Active: {feature.isActive}");

            if (feature.name.Contains("SSAO") || feature.GetType().Name.Contains("ScreenSpaceAmbientOcclusion"))
            {
                Debug.Log("--- SSAO SETTINGS DUMP ---");
                
                // Inspect fields directly
                LogFields(feature);

                // Inspect "settings" or "m_Settings"
                var settingsField = feature.GetType().GetField("settings", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (settingsField == null) settingsField = feature.GetType().GetField("m_Settings", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase);

                if (settingsField != null)
                {
                    object settingsObj = settingsField.GetValue(feature);
                    Debug.Log($"Found Settings Object: {settingsField.Name}");
                    LogFields(settingsObj, "  ");
                }
                else
                {
                    Debug.LogError("Could not find 'settings' or 'm_Settings' field on SSAO feature!");
                }
            }
        }
    }

    private void LogFields(object obj, string prefix = "")
    {
        if (obj == null) return;
        var fields = obj.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (var f in fields)
        {
            // Skip some noisy unity fields
            if (f.Name == "m_ObjectHideFlags" || f.Name.Contains("BackingField")) continue;
            
            try
            {
                Debug.Log($"{prefix}{f.Name}: {f.GetValue(obj)}");
            }
            catch {}
        }
    }

    private void Update()
    {
        if (logOnUpdate) DebugSSAO();
    }
}
