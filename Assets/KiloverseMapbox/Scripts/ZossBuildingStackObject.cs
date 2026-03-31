using Kiloverse.Mapbox;
using System.ComponentModel;
using global::Mapbox.BaseModule.Unity;
using global::Mapbox.VectorModule.MeshGeneration.Unity;
using global::Mapbox.VectorModule.MeshGeneration.MeshModifiers;
using UnityEngine;

namespace Kiloverse.Mapbox
{
    [DisplayName("Zoss Building Stack")]
    [CreateAssetMenu(menuName = "KiloWorld/Modifiers/Zoss Building Stack")]
    public class ZossBuildingStackObject : ScriptableMeshModifierObject
    {
        [Header("Floor Settings")]
        public float firstFloorHeight = 4.5f;
        public float upperFloorHeight = 3.5f;
        
        [Header("Door Settings")]
        public float minDoorWidth = 3.0f;
        public float maxDoorWidth = 5.0f;
        [Range(0.5f, 0.8f)] public float minDoorHeightRatio = 0.5f;
        [Range(0.5f, 0.8f)] public float maxDoorHeightRatio = 0.8f;

        [Header("Window Settings")]
        public float minWindowWidth = 1.5f;
        public float maxWindowWidth = 4.0f;
        public float minWindowSpacing = 1.0f;
        public float maxWindowSpacing = 5.0f;
        [Range(0.4f, 0.8f)] public float windowHeightRatio = 0.6f;

        [Header("Colors")]
        public Color[] wallColors = new Color[] {
            new Color(0.6f, 0.4f, 0.4f),  // Lighter red-brown (was 0.3, 0.1, 0.1)
            new Color(0.5f, 0.5f, 0.5f),  // Lighter gray (was 0.2, 0.2, 0.2)
            new Color(0.5f, 0.5f, 0.6f)   // Lighter blue-gray (was 0.3, 0.3, 0.35)
        };

        [Header("Window Realism (Photographic)")]
        [Tooltip("Percentage of windows to randomly turn off (dead rooms)")]
        [Range(0, 1)] public float windowOffPercentage = 0.30f;
        [Tooltip("Random brightness variance per window (±%)")]
        [Range(0, 1)] public float windowBrightnessVariance = 0.25f;
        [Tooltip("Darken bottom floor windows (light spill physics)")]
        [Range(0, 1)] public float bottomFloorDarkening = 0.35f;
        [Tooltip("Random seed offset for window variance")]
        public int windowVarianceSeed = 0;

        private ZossBuildingStack _implementation;
        protected override MeshModifier _meshModifierImplementation => _implementation;

public override void ConstructModifier(UnityContext unityContext)
        {
            _implementation = new ZossBuildingStack(
                firstFloorHeight,
                upperFloorHeight,
                minDoorWidth,
                maxDoorWidth,
                minDoorHeightRatio,
                maxDoorHeightRatio,
                minWindowWidth,
                maxWindowWidth,
                minWindowSpacing,
                maxWindowSpacing,
                windowHeightRatio,
                wallColors,
                windowOffPercentage,
                windowBrightnessVariance,
                bottomFloorDarkening,
                windowVarianceSeed
            );
        }
    }
}
