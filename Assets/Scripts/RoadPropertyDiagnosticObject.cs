using System.ComponentModel;
using Mapbox.BaseModule.Unity;
using Mapbox.VectorModule.MeshGeneration.GameObjectModifiers;
using Mapbox.VectorModule.MeshGeneration.Unity;
using UnityEngine;

namespace KiloWorld
{
    [DisplayName("Road Property Diagnostic")]
    [CreateAssetMenu(menuName = "Mapbox/Modifiers/Road Property Diagnostic")]
    public class RoadPropertyDiagnosticObject : ScriptableGameObjectModifierObject
    {
        public bool logAllRoads = false;
        public bool logOnlyFirstRoad = true;

        private RoadPropertyDiagnostic _roadDiagnostic;
        protected override GameObjectModifier _gameObjectModifierImplementation => _roadDiagnostic;

        public override void ConstructModifier(UnityContext unityContext)
        {
            _roadDiagnostic = new RoadPropertyDiagnostic
            {
                logAllRoads = this.logAllRoads,
                logOnlyFirstRoad = this.logOnlyFirstRoad
            };
            Debug.Log("[RoadPropertyDiagnosticObject] Modifier constructed and ready");
        }
    }
}
