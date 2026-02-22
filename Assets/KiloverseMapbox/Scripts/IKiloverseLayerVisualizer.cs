using UnityEngine;

namespace Kiloverse.Mapbox
{
    /// <summary>
    /// Kiloverse custom layer visualizer interface - NO MAPBOX DEPENDENCY
    /// </summary>
    public interface IKiloverseLayerVisualizer
    {
        /// <summary>
        /// The name of the layer this visualizer handles (e.g., "building", "road", "poi_label", "water")
        /// </summary>
        string LayerName { get; }
        
        /// <summary>
        /// Whether this visualizer is currently active
        /// </summary>
        bool IsActive { get; set; }
        
        /// <summary>
        /// Process a tile's layer data and generate GameObjects
        /// </summary>
        /// <param name="tileId">The tile ID (z/x/y)</param>
        /// <param name="layerData">Raw layer data from the tile</param>
        /// <param name="parentTransform">Parent transform to attach generated objects to</param>
        void ProcessLayer(string tileId, object layerData, Transform parentTransform);
        
        /// <summary>
        /// Clean up resources for a specific tile
        /// </summary>
        void CleanupTile(string tileId);
    }
}