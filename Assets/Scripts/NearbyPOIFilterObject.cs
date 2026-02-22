using System.ComponentModel;
using Mapbox.BaseModule.Unity;
using Mapbox.VectorModule.MeshGeneration.GameObjectModifiers;
using Mapbox.VectorModule.MeshGeneration.Unity;
using UnityEngine;

[DisplayName("Nearby POI Filter")]
[CreateAssetMenu(menuName = "Mapbox/Modifiers/Nearby POI Filter")]
public class NearbyPOIFilterObject : ScriptableGameObjectModifierObject
{
    [SerializeField] private float maxDistance = 1000f;

    private NearbyPOIFilter _filter;
    protected override GameObjectModifier _gameObjectModifierImplementation => _filter;

    public override void ConstructModifier(UnityContext unityContext)
    {
        _filter = new NearbyPOIFilter();
    }
}
