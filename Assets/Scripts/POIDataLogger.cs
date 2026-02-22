using UnityEngine;
using Mapbox.VectorModule.Unity;
using Mapbox.VectorModule;
using System.Collections;

/// <summary>
/// Logs POI data from the poi_label layer to understand what data is available
/// Attach to the same GameObject as VectorLayerModuleScript (KiloMap)
/// </summary>
public class POIDataLogger : MonoBehaviour
{
    private VectorLayerModuleScript _vectorModule;
    private VectorLayerModule _moduleImplementation;
    private bool _hasLogged = false;

    private void Start()
    {
        _vectorModule = GetComponent<VectorLayerModuleScript>();
        if (_vectorModule == null)
        {
            Debug.LogError("[POIDataLogger] VectorLayerModuleScript not found!");
            return;
        }

        Debug.Log("[POIDataLogger] Starting - will attempt to access POI data");
        StartCoroutine(WaitAndAccessModule());
    }

    private IEnumerator WaitAndAccessModule()
    {
        // Wait for map initialization
        yield return new WaitForSeconds(3f);

        try
        {
            _moduleImplementation = _vectorModule.ModuleImplementation as VectorLayerModule;

            if (_moduleImplementation != null)
            {
                Debug.Log("[POIDataLogger] VectorLayerModule accessed successfully");
                StartCoroutine(MonitorPOIData());
            }
            else
            {
                Debug.LogError("[POIDataLogger] Could not cast ModuleImplementation to VectorLayerModule");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[POIDataLogger] Error accessing module: {e.Message}");
        }
    }

    private IEnumerator MonitorPOIData()
    {
        while (!_hasLogged)
        {
            yield return new WaitForSeconds(1f);

            // Try to access the visualizers to see POI data
            Debug.Log("[POIDataLogger] Checking for POI layer visualizer...");

            // Log that we're monitoring
            if (Time.frameCount % 300 == 0) // Every ~5 seconds
            {
                Debug.Log("[POIDataLogger] Still monitoring for POI data... (buildings and roads should be visible)");
            }
        }
    }
}
