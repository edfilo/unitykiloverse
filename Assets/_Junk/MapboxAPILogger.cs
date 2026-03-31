using UnityEngine;
using System.Linq;

using Mapbox.BaseModule.Data.DataFetchers;
using Kiloverse.Mapbox;
using Mapbox.BaseModule.Data.Tiles;
using Kiloverse.Mapbox;
using Mapbox.UnityMapService;
using Kiloverse.Mapbox;
using Mapbox.MapDebug.Scripts.Logging;
using Kiloverse.Mapbox;
using Mapbox.VectorTile; // For VectorTile parsing
using Kiloverse.Mapbox;

public class MapboxAPILogger : MonoBehaviour
{
    private KiloverseMapInfo _map;

    private void Start()
    {
        _map = FindFirstObjectByType<KiloverseMapInfo>();
        if (_map == null)
        {
            Debug.LogError("[MapboxAPILogger] No KiloverseMapInfo found in scene!");
            return;
        }
        
        DontDestroyOnLoad(gameObject);

        Debug.Log($"[MapboxAPILogger] Found map: {_map.name}. Starting API logging...");
        StartCoroutine(WaitForMapAndSubscribe());
    }

    private System.Collections.IEnumerator WaitForMapAndSubscribe()
    {
        while (_map.MapboxMap == null || _map.MapboxMap.MapService == null)
        {
            yield return null;
        }

        Debug.Log("[MapboxAPILogger] Skipping API logging (using Kiloverse tiles, not Mapbox API)");
        yield break;

        // Note: The code below is disabled since we use Kiloverse tiles (not Mapbox API)
        // If you want to re-enable Mapbox API logging, change MapService to return object instead of KiloverseMapInfo
        /*
        var mapService = _map.MapboxMap.MapService as MapUnityService;
        if (mapService != null)
        {
            var dataFetcher = mapService.GetFetchingManager();
            if (dataFetcher != null)
            {
                dataFetcher.FetchInitialized += OnFetchInitialized;
                dataFetcher.FetchFinished += OnFetchFinished;
                Debug.Log("[MapboxAPILogger] ✓ Subscribed to fetch events");
            }
        }
        else
        {
            Debug.LogWarning("[MapboxAPILogger] Could not get MapUnityService!");
        }
        */
    }

    private void OnFetchInitialized(FetchInfo fetchInfo)
    {
        var tile = fetchInfo.Tile;
        // Construct a pseudo-url or at least the canonical ID
        string tileId = tile.Id.ToString(); 
        Debug.Log($"[api] REQUESTING TILE: {tileId} (Type: {tile.GetType().Name})");
    }

    private void OnFetchFinished(FetchInfo fetchInfo)
    {
        var tile = fetchInfo.Tile;

        if (tile.CurrentTileState == TileState.Loaded)
        {
            // Resolve ambiguity: We want the Unity Wrapper VectorTile
            var vectorTile = tile as Mapbox.BaseModule.Data.Tiles.VectorTile;
            
            if (vectorTile != null && vectorTile.Data != null)
            {
                // vectorTile.Data is the parsed Mapbox.VectorTile object
                int buildingCount = 0;
                int roadCount = 0;
                int poiCount = 0;
                
                // Iterate layers
                foreach (var layerName in vectorTile.Data.LayerNames())
                {
                    var layer = vectorTile.Data.GetLayer(layerName);
                    int count = layer.FeatureCount();

                    if (layerName.Contains("building")) buildingCount += count;
                    else if (layerName.Contains("road")) roadCount += count;
                    else if (layerName.Contains("poi") || layerName.Contains("label")) poiCount += count;
                }

                Debug.Log($"[api] RESPONSE TILE: {tile.Id}\n" +
                          $"      > Buildings: {buildingCount}\n" +
                          $"      > Roads:     {roadCount}\n" +
                          $"      > POIs:      {poiCount}\n" +
                          $"      > Total Layers: {vectorTile.Data.LayerNames().Count}");
            }
            else
            {
                // Fallback for Raster or other tiles
                // Log the actual type to debug why cast failed
                Debug.Log($"[api] RESPONSE TILE (Non-Vector?): {tile.Id} | Type: {tile.GetType().FullName}");
            }
        }
        else if (tile.CurrentTileState != TileState.Loading)
        {
             Debug.LogWarning($"[api] TILE STATE: {tile.Id} is {tile.CurrentTileState}");
        }
    }

    private void OnDestroy()
    {
        // Note: Cleanup disabled since we use Kiloverse tiles (not Mapbox API)
        /*
        if (_map != null && _map.MapboxMap != null && _map.MapboxMap.MapService != null)
        {
            var mapService = _map.MapboxMap.MapService as MapUnityService;
            if (mapService != null)
            {
                var dataFetcher = mapService.GetFetchingManager();
                if (dataFetcher != null)
                {
                    dataFetcher.FetchInitialized -= OnFetchInitialized;
                    dataFetcher.FetchFinished -= OnFetchFinished;
                }
            }
        }
        */
    }
}
