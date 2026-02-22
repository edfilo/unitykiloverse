using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class DebugSSAO : MonoBehaviour
{
    [ContextMenu("Force Enable SSAO Now")]
    void ForceEnableSSAO()
    {
        var urpAsset = UniversalRenderPipeline.asset;
        if (urpAsset == null) { Debug.LogError("No URP Asset!"); return; }

        var renderer = urpAsset.scriptableRenderer;
        if (renderer == null) { Debug.LogError("No Renderer!"); return; }

        var type = typeof(ScriptableRenderer);
        var fieldInfo = type.GetField("m_RendererFeatures", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var features = fieldInfo?.GetValue(renderer) as System.Collections.Generic.List<ScriptableRendererFeature>;

        foreach (var feature in features)
        {
            if (feature == null) continue;

            if (feature.GetType().Name.Contains("ScreenSpaceAmbientOcclusion"))
            {
                Debug.Log("Found SSAO, forcing to max settings...");
                feature.SetActive(true);

                // Get settings struct
                var settingsField = feature.GetType().GetField("m_Settings", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (settingsField != null)
                {
                    var settings = settingsField.GetValue(feature);
                    var settingsType = settings.GetType();

                    // Set extreme values to make it obvious
                    SetField(settings, "Intensity", 5.0f);
                    SetField(settings, "Radius", 2.0f);
                    SetField(settings, "DirectLightingStrength", 0.0f); // Remove from lit areas
                    SetField(settings, "Samples", 2); // High quality

                    // Write back the struct
                    settingsField.SetValue(feature, settings);

                    Debug.Log("SSAO forced to: Intensity=5.0, Radius=2.0, DirectLighting=0.0");
                }

                // Mark dirty and recreate
                #if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(feature);
                #endif

                feature.Create(); // Force recreation
            }
        }
    }

    void SetField(object obj, string fieldName, object value)
    {
        var field = obj.GetType().GetField(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
        if (field != null)
        {
            if (field.FieldType.IsEnum && value is int intVal)
                field.SetValue(obj, System.Enum.ToObject(field.FieldType, intVal));
            else
                field.SetValue(obj, value);
            Debug.Log($"Set {fieldName} = {value}");
        }
        else
        {
            Debug.LogWarning($"Field {fieldName} not found!");
        }
    }

    [ContextMenu("Debug SSAO Feature")]
    void DebugSSAOFeature()
    {
        var urpAsset = UniversalRenderPipeline.asset;
        if (urpAsset == null)
        {
            Debug.LogError("URP Asset is null!");
            return;
        }

        Debug.Log($"URP Asset: {urpAsset.name}");

        // Access the active renderer
        var renderer = urpAsset.scriptableRenderer;
        if (renderer == null)
        {
            Debug.LogError("Renderer is null!");
            return;
        }

        Debug.Log($"Active Renderer: {renderer.GetType().Name}");

        // Use Reflection to get 'm_RendererFeatures'
        var type = typeof(ScriptableRenderer);
        var fieldInfo = type.GetField("m_RendererFeatures", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

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

        Debug.Log($"Found {features.Count} renderer features:");

        foreach (var feature in features)
        {
            if (feature == null)
            {
                Debug.Log("  - NULL feature");
                continue;
            }

            string featureName = feature.name;
            string typeName = feature.GetType().Name;
            bool isActive = feature.isActive;

            Debug.Log($"  - Feature: {featureName}");
            Debug.Log($"    Type: {typeName}");
            Debug.Log($"    Active: {isActive}");

            // Check if it's SSAO
            if (typeName.Contains("ScreenSpaceAmbientOcclusion") || featureName.Contains("SSAO"))
            {
                Debug.Log($"    >>> FOUND SSAO FEATURE! <<<");

                // Try to get settings
                var settingsField = feature.GetType().GetField("m_Settings", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (settingsField != null)
                {
                    var settings = settingsField.GetValue(feature);
                    if (settings != null)
                    {
                        Debug.Log($"    Settings Type: {settings.GetType().Name}");

                        // List all fields in settings
                        var settingsFields = settings.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        Debug.Log($"    Settings fields:");
                        foreach (var field in settingsFields)
                        {
                            var value = field.GetValue(settings);
                            Debug.Log($"      {field.Name} = {value}");
                        }
                    }
                }
            }
        }
    }
}
