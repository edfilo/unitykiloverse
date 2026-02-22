using System.ComponentModel;
using Mapbox.BaseModule.Unity;
using Mapbox.VectorModule.MeshGeneration.GameObjectModifiers;
using Mapbox.VectorModule.MeshGeneration.Unity;
using UnityEngine;

[DisplayName("POI Label")]
[CreateAssetMenu(menuName = "Mapbox/Modifiers/POI Label")]
public class POILabelModifierObject : ScriptableGameObjectModifierObject
{
    // No settings needed - visibility controlled by POICanvasManager
    // POICanvasManager has inspector controls for:
    // - maxDistance (1600m default)
    // - neverCullDistance (30m default)
    // - useElevatorLogic (checkbox)
    // - useOverlapCulling (checkbox)

    private POILabelModifier _labelModifier;
    protected override GameObjectModifier _gameObjectModifierImplementation => _labelModifier;

    public override void ConstructModifier(UnityContext unityContext)
    {
        Debug.Log($"[POILabelModifierObject] ConstructModifier called - Creating POILabelModifier");
        _labelModifier = new POILabelModifier();
        Debug.Log($"[POILabelModifierObject] ConstructModifier complete - Modifier created: {(_labelModifier != null ? "OK" : "NULL")}");
    }
}
