using System.ComponentModel;
using Mapbox.BaseModule.Unity;
using Mapbox.VectorModule.MeshGeneration.GameObjectModifiers;
using Mapbox.VectorModule.MeshGeneration.Unity;
using UnityEngine;

[DisplayName("POI Property Diagnostic")]
[CreateAssetMenu(menuName = "Mapbox/Modifiers/POI Property Diagnostic")]
public class POIPropertyDiagnosticObject : ScriptableGameObjectModifierObject
{
    private POIPropertyDiagnostic _diagnostic;
    protected override GameObjectModifier _gameObjectModifierImplementation => _diagnostic;

    public override void ConstructModifier(UnityContext unityContext)
    {
        _diagnostic = new POIPropertyDiagnostic();
    }
}
