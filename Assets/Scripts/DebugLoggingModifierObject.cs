using System.ComponentModel;
using Mapbox.BaseModule.Unity;
using Mapbox.VectorModule.MeshGeneration.GameObjectModifiers;
using Mapbox.VectorModule.MeshGeneration.Unity;
using UnityEngine;

namespace KiloWorld
{
    [DisplayName("Debug Logging Modifier")]
    [CreateAssetMenu(menuName = "Mapbox/Modifiers/Debug Logging Modifier")]
    public class DebugLoggingModifierObject : ScriptableGameObjectModifierObject
    {
        private DebugLoggingModifier _debugModifier;
        protected override GameObjectModifier _gameObjectModifierImplementation => _debugModifier;

        public override void ConstructModifier(UnityContext unityContext)
        {
            _debugModifier = new DebugLoggingModifier();
            Debug.Log("[DebugLoggingModifierObject] Modifier constructed and ready");
        }
    }
}
