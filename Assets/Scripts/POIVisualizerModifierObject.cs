using System.ComponentModel;
using Mapbox.BaseModule.Unity;
using Mapbox.VectorModule.MeshGeneration.GameObjectModifiers;
using Mapbox.VectorModule.MeshGeneration.Unity;
using UnityEngine;

[DisplayName("POI Visualizer")]
[CreateAssetMenu(menuName = "Mapbox/Modifiers/POI Visualizer")]
public class POIVisualizerModifierObject : ScriptableGameObjectModifierObject
{
    private POIVisualizerModifier _visualizerModifier;
    protected override GameObjectModifier _gameObjectModifierImplementation => _visualizerModifier;

    public override void ConstructModifier(UnityContext unityContext)
    {
        _visualizerModifier = new POIVisualizerModifier();
    }
}
