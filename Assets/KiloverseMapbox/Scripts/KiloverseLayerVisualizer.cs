using Kiloverse.Mapbox;
using System.Collections.Generic;
using UnityEngine;
using global::Mapbox.VectorTile;
using global::Mapbox.BaseModule.Map;
using global::Mapbox.BaseModule.Data;

namespace Kiloverse.Mapbox
{
    /// <summary>
    /// Base Kiloverse layer visualizer - bridges our custom interface to existing Mapbox rendering
    /// Eventually will be fully independent, but for now uses Mapbox mesh generation
    /// </summary>
    public class KiloverseLayerVisualizer : IKiloverseLayerVisualizer
    {
        public string LayerName { get; private set; }
        public bool IsActive { get; set; }

        private readonly IMapInformation _mapInfo;
        private readonly Transform _root;
        private readonly Dictionary<string, GameObject> _tileObjects = new Dictionary<string, GameObject>();

        public KiloverseLayerVisualizer(string layerName, IMapInformation mapInfo, Transform root)
        {
            LayerName = layerName;
            _mapInfo = mapInfo;
            _root = root;
            IsActive = true;

            Debug.Log($"[KiloverseLayerVisualizer] Created visualizer for layer: {layerName}");
        }

        public void ProcessLayer(string tileId, object layerData, Transform parentTransform)
        {
            if (!IsActive)
            {
                Debug.Log($"[KiloverseLayerVisualizer] Skipping layer {LayerName} - visualizer inactive");
                return;
            }

            Debug.Log($"[KiloverseLayerVisualizer] Processing tile {tileId} layer {LayerName}...");

            // For now, just log - we'll wire up actual rendering next
            if (layerData is VectorTileLayer vtLayer)
            {
                Debug.Log($"[KiloverseLayerVisualizer] Layer {LayerName} has {vtLayer.FeatureCount()} features");
            }
        }

        public void CleanupTile(string tileId)
        {
            if (_tileObjects.TryGetValue(tileId, out GameObject tileObj))
            {
                Object.Destroy(tileObj);
                _tileObjects.Remove(tileId);
                Debug.Log($"[KiloverseLayerVisualizer] Cleaned up tile {tileId} for layer {LayerName}");
            }
        }
    }
}
