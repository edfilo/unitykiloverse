using Mapbox.Example.Scripts.TileProviderBehaviours;
using Mapbox.UnityMapService.TileProviders;
using UnityEngine;

namespace KiloWorld.Map
{
    public class FixedAreaTileProviderBehaviour : TileProviderBehaviour
    {
        [Header("Tile Coverage (per direction)")]
        [SerializeField] private int tilesToWest = 7;
        [SerializeField] private int tilesToEast = 7;
        [SerializeField] private int tilesToNorth = 7;
        [SerializeField] private int tilesToSouth = 7;

        private UnityFixedAreaTileProvider _provider;

        public override TileProvider Core
        {
            get
            {
                if (_provider == null)
                {
                    _provider = new UnityFixedAreaTileProvider
                    {
                        TilesToWest = tilesToWest,
                        TilesToEast = tilesToEast,
                        TilesToNorth = tilesToNorth,
                        TilesToSouth = tilesToSouth
                    };
                }
                return _provider;
            }
        }
    }
}
