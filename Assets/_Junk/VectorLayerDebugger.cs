using UnityEngine;
using Mapbox.VectorModule.Unity;

/// <summary>
/// Attach to GameObject with VectorLayerModuleScript to debug vector tile loading
/// </summary>
[RequireComponent(typeof(VectorLayerModuleScript))]
public class VectorLayerDebugger : MonoBehaviour
{
    private VectorLayerModuleScript _vectorModule;

    private void Awake()
    {
        _vectorModule = GetComponent<VectorLayerModuleScript>();
        if (_vectorModule == null)
        {
            Debug.LogError("[VectorLayerDebugger] VectorLayerModuleScript not found!");
            return;
        }

        Debug.Log("[VectorLayerDebugger] Found VectorLayerModuleScript - will monitor map loading");
    }

    private void Start()
    {
        Debug.Log("[VectorLayerDebugger] VectorLayerModuleScript started - map should begin loading tiles");
    }

    private void Update()
    {
        // Log once when tiles are loading (first 10 frames)
        if (Time.frameCount <= 10)
        {
            Debug.Log($"[VectorLayerDebugger] Frame {Time.frameCount} - Map is processing...");
        }
    }
}
