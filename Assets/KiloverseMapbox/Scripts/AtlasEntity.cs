using System;
using UnityEngine;

namespace Kiloverse.Mapbox
{
    [Serializable]
    public class AtlasEntity
    {
        [Header("Floor Heights (Meters)")]
        [Tooltip("Height of ground floor in meters (typically 4-5m for commercial entrances)")]
        public float GroundFloorHeightMeters = 4.5f;

        [Tooltip("Height of each middle floor in meters (typically 3-3.5m for residential/office)")]
        public float MidFloorHeightMeters = 3.5f;

        [Tooltip("Height of top floor in meters (typically 4-5m for penthouses, 0 to disable)")]
        public float TopFloorHeightMeters = 4.0f;

        [Header("Texture Atlas UV Mapping")]
        [Tooltip("UV rectangle in atlas texture for this facade")]
        public Rect TextureRect;

        [Tooltip("Ratio of texture height allocated to bottom section (0-1)")]
        public float BottomSectionRatio = 0.2f;

        [Tooltip("Ratio of texture height allocated to top section (0-1)")]
        public float TopSectionRatio = 0.2f;

        [Tooltip("Number of repeating floor textures in middle section")]
        public int MidFloorCount = 4;

        [Header("Horizontal Subdivision")]
        [Tooltip("Number of horizontal window columns in texture")]
        public float ColumnCount = 3;

        [Tooltip("Preferred wall segment length for tiling")]
        public int PreferredEdgeSectionLength = 40;

        [HideInInspector] public float bottomOfTopUv;
        [HideInInspector] public float topOfMidUv;
        [HideInInspector] public float topOfBottomUv;
        [HideInInspector] public float midUvHeight;

        public void CalculateParameters()
        {
            midUvHeight = (1 - TopSectionRatio - BottomSectionRatio) / MidFloorCount;
            bottomOfTopUv = 1 - TopSectionRatio;
            topOfMidUv = BottomSectionRatio + (MidFloorCount * midUvHeight);
            topOfBottomUv = BottomSectionRatio;
        }
    }
}
