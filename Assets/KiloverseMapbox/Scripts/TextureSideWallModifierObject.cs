using Kiloverse.Mapbox;
using System.ComponentModel;
using global::Mapbox.BaseModule.Unity;
using global::Mapbox.VectorModule.MeshGeneration.Unity;
using global::Mapbox.VectorModule.MeshGeneration.MeshModifiers;
using UnityEngine;

namespace Kiloverse.Mapbox
{
    [DisplayName("Texture Side Wall Modifier")]
    [CreateAssetMenu(menuName = "Kiloverse/Modifiers/Texture Side Wall Modifier")]
    public class TextureSideWallModifierObject : ScriptableMeshModifierObject, IPolygonMeshModifier
    {
        public AtlasInfo AtlasInfo;

        [Header("Debug Options")]
        [Tooltip("Override building heights with random values (10-50m) for testing floor subdivision")]
        public bool UseRandomHeights = false;

        [Tooltip("Color each floor section differently for testing (bottom=red, mid=green, top=blue)")]
        public bool UseRandomColors = false;

        [System.Obsolete("Height is now extracted from building feature data")]
        [UnityEngine.HideInInspector]
        public float Height = 10f; // Keep for backwards compatibility with serialized assets

        private TextureSideWallModifier _textureSideWallModifierImplementation;
        protected override MeshModifier _meshModifierImplementation => _textureSideWallModifierImplementation;

public override void ConstructModifier(UnityContext unityContext)
        {
            UnityEngine.Debug.Log($"[TextureSideWallModifierObject] ConstructModifier called! AtlasInfo={(AtlasInfo != null ? "valid" : "NULL")}, UseRandomHeights={UseRandomHeights}, UseRandomColors={UseRandomColors}");
            
            if (AtlasInfo == null)
            {
                UnityEngine.Debug.LogError("[TextureSideWallModifierObject] AtlasInfo is NULL! Modifier will not work.");
                return;
            }
            
            _textureSideWallModifierImplementation = new TextureSideWallModifier(AtlasInfo, UseRandomHeights, UseRandomColors);
            UnityEngine.Debug.Log("[TextureSideWallModifierObject] TextureSideWallModifier instance created successfully");
        }
    }
}
