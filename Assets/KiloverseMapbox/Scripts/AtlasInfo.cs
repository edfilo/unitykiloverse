using System.Collections.Generic;
using UnityEngine;

namespace Kiloverse.Mapbox
{
    [CreateAssetMenu(menuName = "Kiloverse/Atlas Info")]
    public class AtlasInfo : ScriptableObject
    {
        public List<AtlasEntity> Textures = new List<AtlasEntity>();
        
        private void OnValidate()
        {
            foreach (var texture in Textures)
            {
                texture?.CalculateParameters();
            }
        }
    }
}
