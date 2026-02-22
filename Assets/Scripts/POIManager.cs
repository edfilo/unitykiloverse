using UnityEngine;
using TMPro;
using Mapbox.VectorModule.MeshGeneration.GameObjectModifiers;

/// <summary>
/// Manages POI (Point of Interest) labels spawned from Mapbox vector tiles.
/// This script configures POI labels via the PrefabModifier callback.
/// </summary>
public class POIManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The PrefabModifierObject ScriptableObject that spawns POI prefabs")]
    public PrefabModifierObject prefabModifier;

    [Header("Label Settings")]
    [Tooltip("Font size for POI labels")]
    public float fontSize = 3f;

    [Tooltip("Label color")]
    public Color labelColor = Color.white;

    [Tooltip("Height offset above the POI point")]
    public float labelHeightOffset = 2f;

    [Tooltip("Enable billboard behavior (labels always face camera)")]
    public bool enableBillboard = true;

private void Start()
    {
        if (prefabModifier == null)
        {
            Debug.LogError("[POIManager] PrefabModifierObject is not assigned!");
            return;
        }
        prefabModifier.PrefabCreated += OnPOISpawned;
        Debug.Log("[POIManager] Subscribed to POI prefab creation - will show ALL POIs");
    }

private void OnDestroy()
    {
        if (prefabModifier != null)
        {
            prefabModifier.PrefabCreated -= OnPOISpawned;
        }
    }

    /// <summary>
    /// Called when a POI prefab is spawned by the PrefabModifier.
    /// Configures the label appearance.
    /// </summary>
    private void OnPOISpawned(GameObject poiGameObject)
    {
        if (poiGameObject == null) return;

        // Use GameObject name as label text (contains feature ID from Mapbox)
        string labelText = poiGameObject.name;

        // Find TextMeshPro component in children
        var tmp = poiGameObject.GetComponentInChildren<TextMeshPro>();

        if (tmp == null)
        {
            Debug.LogWarning($"[POIManager] No TextMeshPro found on POI prefab: {poiGameObject.name}");
            return;
        }

        // Set label properties
        tmp.text = labelText;
        tmp.fontSize = fontSize;
        tmp.color = labelColor;
        tmp.alignment = TextAlignmentOptions.Center;

        // Adjust position with height offset
        tmp.transform.localPosition = new Vector3(0, labelHeightOffset, 0);

        // Add billboard behavior if enabled
        if (enableBillboard)
        {
            var billboard = poiGameObject.GetComponent<Billboard>();
            if (billboard == null)
            {
                billboard = poiGameObject.AddComponent<Billboard>();
            }
        }

        Debug.Log($"[POIManager] Configured POI label: {labelText}");
    }
}
