using UnityEngine;
using Mapbox.VectorModule.MeshGeneration.GameObjectModifiers;

/// <summary>
/// Monitors PrefabModifier events to debug POI spawning
/// Attach this to the same GameObject as VectorLayerModuleScript
/// </summary>
public class POIPrefabMonitor : MonoBehaviour
{
    [Header("References")]
    public PrefabModifierObject prefabModifier;

    private int spawnCount = 0;

    private void Start()
    {
        if (prefabModifier == null)
        {
            Debug.LogError("[POIPrefabMonitor] PrefabModifierObject not assigned!");
            return;
        }

        Debug.Log($"[POIPrefabMonitor] Subscribing to PrefabCreated event on {prefabModifier.name}");
        prefabModifier.PrefabCreated += OnPOISpawned;
    }

    private void OnDestroy()
    {
        if (prefabModifier != null)
        {
            prefabModifier.PrefabCreated -= OnPOISpawned;
        }
    }

    private void OnPOISpawned(GameObject poiGameObject)
    {
        spawnCount++;
        Debug.Log($"[POIPrefabMonitor] POI #{spawnCount} spawned!");
        Debug.Log($"  - Name: {poiGameObject.name}");
        Debug.Log($"  - Position: {poiGameObject.transform.position}");
        Debug.Log($"  - LocalPosition: {poiGameObject.transform.localPosition}");
        Debug.Log($"  - Parent: {(poiGameObject.transform.parent ? poiGameObject.transform.parent.name : "null")}");

        if (poiGameObject.transform.parent != null)
        {
            Debug.Log($"  - Parent Position: {poiGameObject.transform.parent.position}");
        }
    }
}
