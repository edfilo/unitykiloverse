using System.ComponentModel;
using Mapbox.BaseModule.Unity;
using Mapbox.VectorModule.MeshGeneration.GameObjectModifiers;
using Mapbox.VectorModule.MeshGeneration.Unity;
using UnityEngine;

[DisplayName("POI Data Logger")]
[CreateAssetMenu(menuName = "Mapbox/Modifiers/POI Data Logger")]
public class POIDataLoggerModifierObject : ScriptableGameObjectModifierObject
{
    private POIDataLoggerModifier _loggerModifier;
    protected override GameObjectModifier _gameObjectModifierImplementation => _loggerModifier;

public override void ConstructModifier(UnityContext unityContext)
    {
        _loggerModifier = new POIDataLoggerModifier();
        Debug.Log("[POIDataLoggerModifierObject] Logger constructed");
    }
}
