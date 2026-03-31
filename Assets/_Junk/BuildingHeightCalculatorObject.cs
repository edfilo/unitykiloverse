using System.ComponentModel;
using UnityEngine;
using Mapbox.BaseModule.Unity;
using Mapbox.VectorModule.MeshGeneration.GameObjectModifiers;
using Mapbox.VectorModule.MeshGeneration.Unity;

/// <summary>
/// ScriptableObject wrapper for BuildingHeightCalculator.
/// This allows the modifier to be added via the Unity Inspector.
/// Add this to your Building layer's GameObject Modifiers list.
/// </summary>
[DisplayName("Building Height Calculator")]
[CreateAssetMenu(menuName = "Mapbox/Modifiers/Building Height Calculator")]
public class BuildingHeightCalculatorObject : ScriptableGameObjectModifierObject
{
    [SerializeField]
    [Tooltip("Height per floor when calculating from num_floors (meters)")]
    public float metersPerFloor = 4f;

    [SerializeField]
    [Tooltip("Minimum height for buildings without height or floor data (meters)")]
    public float minimumHeight = 8f;

    private BuildingHeightCalculator _calculatorImplementation;

    protected override GameObjectModifier _gameObjectModifierImplementation => _calculatorImplementation;

    public override void ConstructModifier(UnityContext unityContext)
    {
        Debug.Log($"[BuildingHeightCalculatorObject] ConstructModifier called! metersPerFloor={metersPerFloor}, minimumHeight={minimumHeight}");
        _calculatorImplementation = new BuildingHeightCalculator
        {
            metersPerFloor = metersPerFloor,
            minimumHeight = minimumHeight
        };
        Debug.Log($"[BuildingHeightCalculatorObject] Created BuildingHeightCalculator instance");
    }
}
