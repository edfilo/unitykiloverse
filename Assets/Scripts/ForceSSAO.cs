using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Reflection;
using System.Linq;

[ExecuteAlways]
public class ForceSSAO : MonoBehaviour
{
    [Header("Force SSAO To Max")]
    public bool forceOnStart = true;
    [Range(0, 10)] public float intensity = 5.0f;
    [Range(0.01f, 5f)] public float radius = 2.0f;
    [Range(0, 1)] public float directLightingStrength = 0.0f;

    void Start()
    {
        if (forceOnStart && Application.isPlaying)
        {
            ForceMaxSSAO();
        }
    }

    [ContextMenu("Force Max SSAO Now")]
    public void ForceMaxSSAO()
    {
        var urpAsset = UniversalRenderPipeline.asset;
        if (urpAsset == null) { Debug.LogError("No URP Asset!"); return; }

        var renderer = urpAsset.scriptableRenderer;
        if (renderer == null) { Debug.LogError("No Renderer!"); return; }

        var type = typeof(ScriptableRenderer);
        var fieldInfo = type.GetField("m_RendererFeatures", BindingFlags.NonPublic | BindingFlags.Instance);
        var features = fieldInfo?.GetValue(renderer) as System.Collections.Generic.List<ScriptableRendererFeature>;

        foreach (var feature in features)
        {
            if (feature == null) continue;

            if (feature.GetType().Name.Contains("ScreenSpaceAmbientOcclusion"))
            {
                Debug.Log($"<color=yellow>[ForceSSAO] Forcing SSAO to max: Intensity={intensity}, Radius={radius}, DirectLight={directLightingStrength}</color>");
                feature.SetActive(true);

                var settingsField = feature.GetType().GetField("m_Settings", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (settingsField != null)
                {
                    var settings = settingsField.GetValue(feature);

                    SetField(settings, "Intensity", intensity);
                    SetField(settings, "Radius", radius);
                    SetField(settings, "DirectLightingStrength", directLightingStrength);
                    SetField(settings, "Samples", 2); // High
                    SetField(settings, "AfterOpaque", false);
                    SetField(settings, "Source", 1); // DepthNormals

                    settingsField.SetValue(feature, settings);

                    #if UNITY_EDITOR
                    UnityEditor.EditorUtility.SetDirty(feature);
                    #endif
                    feature.Create();

                    Debug.Log("<color=green>[ForceSSAO] SSAO recreated with max settings!</color>");
                }
            }
        }
    }

    void SetField(object obj, string name, object value)
    {
        var field = obj.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (field != null)
        {
            if (field.FieldType.IsEnum && value is int i) field.SetValue(obj, System.Enum.ToObject(field.FieldType, i));
            else field.SetValue(obj, value);
        }
    }

    [ContextMenu("Analyze Active Renderer")]
    public void Analyze()
    {
        var urpAsset = UniversalRenderPipeline.asset;
        if (urpAsset == null)
        {
            Debug.LogError("URP Asset is null!");
            return;
        }

        Debug.Log($"Active URP Asset: {urpAsset.name} (Instance ID: {urpAsset.GetInstanceID()})");

        // Access the renderer data list using reflection
        var field = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);
        if (field == null)
        {
            Debug.LogError("Could not find m_RendererDataList!");
            return;
        }

        var rendererDataList = field.GetValue(urpAsset) as ScriptableRendererData[];
        if (rendererDataList == null || rendererDataList.Length == 0)
        {
            Debug.LogError("Renderer Data List is empty or null!");
            return;
        }

        Debug.Log($"Renderer Data List Count: {rendererDataList.Length}");
        
        // Assume default renderer is index 0
        var activeRendererData = rendererDataList[0]; // Or fetch active index from urpAsset
        Debug.Log($"Default Renderer Data: {activeRendererData.name}");

        Debug.Log($"Features in {activeRendererData.name}:");
        foreach (var feature in activeRendererData.rendererFeatures)
        {
            if (feature == null)
            {
                Debug.Log("- [NULL FEATURE]");
                continue;
            }
            Debug.Log($"- {feature.name} (Type: {feature.GetType().FullName}) - Active: {feature.isActive}");
        }
    }
}
