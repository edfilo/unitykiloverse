using UnityEngine;
using UnityEngine.UI;
using System.Linq;

namespace Kiloverse.Mapbox
{
    /// <summary>
    /// Displays ALL layer memory pressure and culling effectiveness stats.
    /// Tracks: Buildings, Roads, Water, Places, Labels
    /// Shows: Total in memory, Active (not tile-culled), Visible (renderer enabled)
    /// </summary>
    public class BuildingMemoryStats : MonoBehaviour
    {
        [Header("UI Display")]
        [SerializeField] private Text statsText;
        [SerializeField] private bool showOnScreenGUI = true;

        [Header("Update Settings")]
        [SerializeField] private float updateInterval = 1f; // Update stats every second

        private System.Collections.Generic.Dictionary<string, LayerStats> _layerStats =
            new System.Collections.Generic.Dictionary<string, LayerStats>();
        private int _totalTiles;
        private int _activeTiles;

        private float _lastUpdateTime;

        private class LayerStats
        {
            public int TotalInMemory;
            public int Active;
            public int Visible;
        }

        void Update()
        {
            if (Time.time - _lastUpdateTime < updateInterval) return;
            _lastUpdateTime = Time.time;

            UpdateStats();
        }

        void UpdateStats()
        {
            _layerStats.Clear();

            // Get all GameObjects from OvertureMapManager's tileObjects
            var mapManager = FindObjectOfType<OvertureMapManager>();
            if (mapManager == null) return;

            // Access tile objects via reflection
            var rendererField = typeof(OvertureMapManager).GetField("m_Renderer",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (rendererField == null) return;

            var renderer = rendererField.GetValue(mapManager);
            if (renderer == null) return;

            var tileObjectsField = renderer.GetType().GetField("_tileObjects",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (tileObjectsField == null) return;

            var tileObjects = tileObjectsField.GetValue(renderer) as System.Collections.IDictionary;
            if (tileObjects == null) return;

            // Count objects per layer
            foreach (System.Collections.DictionaryEntry entry in tileObjects)
            {
                var objects = entry.Value as System.Collections.Generic.List<GameObject>;
                if (objects == null) continue;

                foreach (var go in objects)
                {
                    if (go == null) continue;

                    // Determine layer from object name
                    string layerName = GetLayerName(go.name);

                    if (!_layerStats.ContainsKey(layerName))
                    {
                        _layerStats[layerName] = new LayerStats();
                    }

                    var stats = _layerStats[layerName];
                    stats.TotalInMemory++;

                    if (go.activeInHierarchy)
                    {
                        stats.Active++;

                        var meshRenderer = go.GetComponent<MeshRenderer>();
                        if (meshRenderer != null && meshRenderer.enabled)
                        {
                            stats.Visible++;
                        }
                    }
                }
            }

            // Count tile visibility
            var tileVisibilityField = renderer.GetType().GetField("_tileVisibility",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (tileVisibilityField != null)
            {
                var tileVisibility = tileVisibilityField.GetValue(renderer) as System.Collections.IDictionary;
                if (tileVisibility != null)
                {
                    _totalTiles = tileVisibility.Count;
                    _activeTiles = 0;
                    foreach (System.Collections.DictionaryEntry entry in tileVisibility)
                    {
                        if ((bool)entry.Value) _activeTiles++;
                    }
                }
            }

            // Update UI if available
            if (statsText != null)
            {
                UpdateUIText();
            }
        }

        string GetLayerName(string objectName)
        {
            if (objectName.Contains("building")) return "Buildings";
            if (objectName.Contains("road") || objectName.Contains("segment")) return "Roads";
            if (objectName.Contains("place") || objectName.Contains("poi")) return "Places";
            if (objectName.Contains("water")) return "Water";
            if (objectName.Contains("LabelAnchor")) return "Labels";
            return "Other";
        }

        void UpdateUIText()
        {
            string text = "<b>LAYER MEMORY PRESSURE</b>\n";

            foreach (var kvp in _layerStats.OrderByDescending(x => x.Value.TotalInMemory))
            {
                var stats = kvp.Value;
                int tileCulled = stats.TotalInMemory - stats.Active;
                int rendererCulled = stats.Active - stats.Visible;
                float efficiency = stats.TotalInMemory > 0 ? ((1f - stats.Visible / (float)stats.TotalInMemory) * 100f) : 0f;

                text += $"\n<b>{kvp.Key}</b>\n";
                text += $"  Memory: <color=yellow>{stats.TotalInMemory}</color>\n";
                text += $"  Active: <color=cyan>{stats.Active}</color> (tile-culled: {tileCulled})\n";
                text += $"  Visible: <color=lime>{stats.Visible}</color> (renderer-culled: {rendererCulled})\n";
                text += $"  Efficiency: <color=lime>{efficiency:F1}%</color>\n";
            }

            text += $"\n<b>TILES</b>\n";
            text += $"Total: {_totalTiles} | Active: {_activeTiles}";

            statsText.text = text;
        }

        void OnGUI()
        {
            if (!showOnScreenGUI) return;

            GUIStyle style = new GUIStyle(GUI.skin.box);
            style.alignment = TextAnchor.UpperLeft;
            style.fontSize = 12;
            style.normal.textColor = Color.white;

            string stats = "LAYER MEMORY PRESSURE\n━━━━━━━━━━━━━━━━━━━━━━━━━━━\n";

            foreach (var kvp in _layerStats.OrderByDescending(x => x.Value.TotalInMemory))
            {
                var layerStats = kvp.Value;
                int tileCulled = layerStats.TotalInMemory - layerStats.Active;
                int rendererCulled = layerStats.Active - layerStats.Visible;
                float efficiency = layerStats.TotalInMemory > 0 ? ((1f - layerStats.Visible / (float)layerStats.TotalInMemory) * 100f) : 0f;

                stats += $"\n{kvp.Key}: {layerStats.TotalInMemory} in memory\n";
                stats += $"  └─ Active: {layerStats.Active} (tile-culled: {tileCulled})\n";
                stats += $"  └─ Visible: {layerStats.Visible} (renderer-culled: {rendererCulled})\n";
                stats += $"  └─ Efficiency: {efficiency:F1}%\n";
            }

            stats += $"\nTiles: {_activeTiles}/{_totalTiles} active";

            // Dynamic height based on number of layers
            int height = 120 + (_layerStats.Count * 65);
            GUI.Box(new Rect(10, 10, 400, height), stats, style);
        }
    }
}
