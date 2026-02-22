using System.ComponentModel;
using Mapbox.BaseModule.Unity;
using Mapbox.VectorModule.MeshGeneration.GameObjectModifiers;
using Mapbox.VectorModule.MeshGeneration.Unity;
using UnityEngine;

[DisplayName("Building Property Test")]
[CreateAssetMenu(menuName = "Mapbox/Modifiers/Building Property Test")]
public class BuildingPropertyTestObject : ScriptableGameObjectModifierObject
{
    private BuildingPropertyTest _test;
    protected override GameObjectModifier _gameObjectModifierImplementation => _test;

    public override void ConstructModifier(UnityContext unityContext)
    {
        _test = new BuildingPropertyTest();
    }
}
