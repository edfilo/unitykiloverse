using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kiloverse.FPSPort
{
    [Serializable]
    public class FpsAtlasEntity
    {
        public Rect TextureRect;
        public int MidFloorCount = 1;
        public float ColumnCount = 3;
        public float TopSectionRatio = 0.2f;
        public float BottomSectionRatio = 0.2f;
        public int PreferredEdgeSectionLength = 40;

        [HideInInspector] public float FloorHeight;
        [HideInInspector] public float FirstFloorHeight;
        [HideInInspector] public float TopFloorHeight;
        [HideInInspector] public float bottomOfTopUv;
        [HideInInspector] public float topOfMidUv;
        [HideInInspector] public float topOfBottomUv;
        [HideInInspector] public float midUvHeight;
        [HideInInspector] public float WallToFloorRatio;

        public void CalculateParameters()
        {
            // Derive floor heights from atlas aspect so side walls keep their proportions.
            FirstFloorHeight = PreferredEdgeSectionLength * ((TextureRect.height * BottomSectionRatio) / TextureRect.width);
            TopFloorHeight = PreferredEdgeSectionLength * ((TextureRect.height * TopSectionRatio) / TextureRect.width);
            FloorHeight = PreferredEdgeSectionLength * ((1 - TopSectionRatio - BottomSectionRatio) * (TextureRect.height / TextureRect.width));

            bottomOfTopUv = TextureRect.yMax - (TextureRect.size.y * TopSectionRatio);
            topOfMidUv = TextureRect.yMax - (TextureRect.height * TopSectionRatio);
            topOfBottomUv = TextureRect.yMin + (TextureRect.size.y * BottomSectionRatio);
            midUvHeight = TextureRect.height * (1 - TopSectionRatio - BottomSectionRatio);
            WallToFloorRatio = (1 - TopSectionRatio - BottomSectionRatio) * (TextureRect.height / TextureRect.width);
        }
    }

    [CreateAssetMenu(menuName = "Kiloverse/FPS Port/Atlas Info")]
    public class FpsAtlasInfo : ScriptableObject
    {
        public List<FpsAtlasEntity> Textures = new List<FpsAtlasEntity>();

        private void OnValidate()
        {
            foreach (var tex in Textures)
            {
                tex?.CalculateParameters();
            }
        }
    }
}
