using System.ComponentModel;
using global::Mapbox.BaseModule.Unity;
using global::Mapbox.VectorModule.MeshGeneration.Unity;
using UnityEngine;

namespace Kiloverse.FPSPort
{
    [DisplayName("FPS Port/Texture Side Wall Modifier")]
    [CreateAssetMenu(menuName = "Kiloverse/FPS Port/Modifiers/Texture Side Wall Modifier")]
    public class FpsTextureSideWallModifierObject : ScriptableMeshModifierObject
    {
        public FpsAtlasInfo AtlasInfo;
        public bool CenterSegments = true;
        public bool SeparateSubmesh = true;
        [Header("Debug")]
        public bool DebugLogVertices = false;
        public int DebugLogLimit = 10;

        private FpsTextureSideWallModifier _impl;
        protected override global::Mapbox.VectorModule.MeshGeneration.MeshModifiers.MeshModifier _meshModifierImplementation => _impl;

        public override void ConstructModifier(UnityContext unityContext)
        {
            if (AtlasInfo == null)
            {
                _impl = null;
                return;
            }

            _impl = new FpsTextureSideWallModifier(
                AtlasInfo,
                CenterSegments,
                SeparateSubmesh,
                DebugLogVertices,
                DebugLogLimit,
                AtlasInfo != null ? AtlasInfo.name : "(null atlas)");
        }
    }
}
