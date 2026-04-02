using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
// Old Mapbox SDK imports removed — using Kiloverse.Mapbox types
using MapboxVectorTile = global::Mapbox.BaseModule.Data.Tiles.VectorTile;
using UnityEngine;
using UnityEngine.Networking;
using ICSharpCode.SharpZipLib.GZip;
using System.IO;

namespace Kiloverse.Mapbox
{
    /// <summary>
    /// Orchestrates the Overture maps using the server-side /xyz/ tile proxy.
    /// Replaces the complex client-side PMTiles reader with simple HTTP requests.
    /// </summary>
    public class OvertureMapManager : MonoBehaviour
    {
        private void Awake()
        {
            Debug.Log("[OvertureMapManager] ===== AWAKE CALLED =====");
            Debug.Log($"[OvertureMapManager] Component enabled: {enabled}");
            Debug.Log($"[OvertureMapManager] GameObject active: {gameObject.activeInHierarchy}");
        }

        private void OnDisable()
        {
            Debug.LogError("[OvertureMapManager] ❌ COMPONENT WAS DISABLED!");
        }

        private void OnDestroy()
        {
            Debug.LogError("[OvertureMapManager] ❌ COMPONENT WAS DESTROYED!");
        }

        [Header("Map")]
        [SerializeField] private KiloverseMapInfo _map;
        public KiloverseMapInfo map => _map;
        [SerializeField] private Camera playerCamera; // For frustum culling
        [SerializeField] private int zoomLevel = 16;
        [SerializeField] private float tilePollInterval = 1f;
        [SerializeField] private bool logOvertureLayers = true;

        [Header("Visualizers (ScriptableObjects)")]
        [SerializeField] private IVectorLayerVisualizer buildingVisualizer;
        [SerializeField] private IVectorLayerVisualizer roadVisualizer;
        [SerializeField] private IVectorLayerVisualizer poiVisualizer;
        [SerializeField] private IVectorLayerVisualizer waterVisualizer;

        private XYZTileFetcher m_PlacesFetcher;
        private XYZTileFetcher m_BuildingsFetcher;
        private XYZTileFetcher m_TransportationFetcher;
        private XYZTileFetcher m_BaseFetcher;

        private readonly List<XYZLayer> m_Layers = new List<XYZLayer>();
        private OvertureVectorRenderer m_Renderer;
        private float m_LastPoll;
        [Header("Editor Safety")]
        [SerializeField] private int editorWarmupFrames = 120;
        [SerializeField] private int editorUpdateStride = 2;
        private bool _loggedFirstUpdate = false;
        
        // Cache player controller to avoid expensive FindFirstObjectByType calls
        private KiloFirstPersonController _cachedPlayerController;
        private int _playerSearchFrame = 0;
        private const int PLAYER_SEARCH_INTERVAL = 60; // Search every 60 frames (~1 second)

private void Start()
    {
        BootDiagnostics.Mark("OvertureMapManager.Start");
#if UNITY_EDITOR
        // Editor: skip map init entirely so boot doesn't lock (Find/Construct block main thread).
        // Set to false in Inspector to enable map for testing.
        if (editorSkipMapInit)
        {
            BootDiagnostics.Mark("OvertureMapManager editor skip init (no map)");
            return;
        }
        StartCoroutine(DelayedInitInEditor());
#else
        StartCoroutine(InitializeAfterGPS());
#endif
    }

    [Header("Editor: set true to skip map/tiles (faster boot, no map)")]
    [SerializeField] private bool editorSkipMapInit = false;

    private IEnumerator DelayedInitInEditor()
    {
        yield return new WaitForSeconds(2f);
        BootDiagnostics.Mark("OvertureMapManager editor delay done");
        yield return InitializeAfterGPS();
    }

    private IEnumerator InitializeAfterGPS()
    {
        BootDiagnostics.Mark("Overture wait GPS");
        Debug.Log("[OvertureMapManager] Waiting for GPS...");

        while (!BootState.AllowMap)
        {
            yield return null;
        }
        BootDiagnostics.Mark("Overture map allowed");

        // Wait for GPS to be ready
        while (!GPSLocationController.GPSReady)
        {
            yield return null;
        }

        BootDiagnostics.Mark("Overture GPS ready");
#if UNITY_EDITOR
        // Let BootSequence log "Waiting for tiles... 0s" before we do any heavy Find/Construct
        yield return new WaitForSeconds(0.5f);
        BootDiagnostics.Mark("Overture after 0.5s yield");
#endif
        Debug.Log("[OvertureMapManager] ✓ GPS ready, initializing...");

        if (_map == null)
        {
            _map = FindObjectOfType<KiloverseMapInfo>();
        }

        if (_map == null)
        {
            Debug.LogError("[OvertureMapManager] ❌ KiloverseMapInfo not found!");
            enabled = false;
            yield break;
        }

        // Endpoints point to the new Cloudflare Worker /xyz/ route
        m_PlacesFetcher = new XYZTileFetcher("https://api.kilomeme.com/xyz/places/{z}/{x}/{y}.mvt");
        m_BuildingsFetcher = new XYZTileFetcher("https://api.kilomeme.com/xyz/buildings/{z}/{x}/{y}.mvt");
        m_TransportationFetcher = new XYZTileFetcher("https://api.kilomeme.com/xyz/transportation/{z}/{x}/{y}.mvt");
        m_BaseFetcher = new XYZTileFetcher("https://api.kilomeme.com/xyz/base/{z}/{x}/{y}.mvt");

        Debug.Log("[OvertureMapManager] Tile fetchers created, yielding...");

        // Yield to let other systems continue
        yield return null;

        BootDiagnostics.Mark("Overture coroutine resumed after yield");
        Debug.Log("[OvertureMapManager] ✓ Coroutine resumed after yield");

        if (!enabled || !gameObject.activeInHierarchy)
        {
            Debug.LogError($"[OvertureMapManager] ❌ Disabled after yield!");
            yield break;
        }

        Debug.Log("[OvertureMapManager] Looking for visualizers...");
        yield return null; // Prevent editor freeze before heavy Find
        if (Application.isEditor)
            BootDiagnostics.Mark("Overture before Find VectorLayerModule");
        // Find visualizers from VectorLayerModuleScript component (Unity can't serialize interface fields in Inspector)
        var vectorLayerModule = FindFirstObjectByType<VectorLayerModuleScript>();
        if (Application.isEditor)
            BootDiagnostics.Mark("Overture after Find VectorLayerModule");
        yield return null;

        if (vectorLayerModule != null)
        {
            Debug.Log($"[OvertureMapManager] Found VectorLayerModuleScript on GameObject: {vectorLayerModule.gameObject.name}");

            // Get the _layerVisualizers field (it's a List<VectorLayerVisualizerObject>)
            var layerVisListField = typeof(VectorLayerModuleScript).GetField("_layerVisualizers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (layerVisListField != null)
            {
                var visualizerList = layerVisListField.GetValue(vectorLayerModule) as System.Collections.IList;
                if (visualizerList != null)
                {
                    Debug.Log($"[OvertureMapManager] Found {visualizerList.Count} visualizers in list");
                    int itemIndex = 0;
                    foreach (var item in visualizerList)
                    {
                        // Yield every visualizer in editor to prevent main-thread freeze
                        if (Application.isEditor)
                        {
                            yield return null;
                        }
                        itemIndex++;
                        try
                        {
                            Debug.Log($"[OvertureMapManager] List item: {item}, Type: {item?.GetType().Name}");

                            // VectorLayerVisualizerObject is a wrapper - cast it first, then get the visualizer
                            Debug.Log("[OvertureMapManager] About to cast to VectorLayerVisualizerObject...");
                            var visualizerObject = item as VectorLayerVisualizerObject;
                            Debug.Log($"[OvertureMapManager] Cast result: {visualizerObject != null}");

                            if (visualizerObject != null)
                            {
                                Debug.Log($"[OvertureMapManager] Visualizer object name: {visualizerObject.name}");

                                // CRITICAL FIX: Call ConstructLayerVisualizer() to properly initialize the visualizer WITH modifier stacks
                                // The _layerVisualizer field is null until this method is called
                                // This method creates the visualizer and adds all modifier stacks from _modifierStackObjects
                                IVectorLayerVisualizer visualizer = null;
                                try
                                {
                                    // Create UnityContext (required by ConstructLayerVisualizer)
                                    var unityContext = new UnityContext();

                                    // Call ConstructLayerVisualizer - this creates the visualizer AND adds modifier stacks
                                    if (Application.isEditor)
                                        BootDiagnostics.Mark($"Overture before Construct {visualizerObject.name}");
                                    visualizer = visualizerObject.ConstructLayerVisualizer(_map.MapInformation, unityContext);
                                    if (Application.isEditor)
                                        BootDiagnostics.Mark($"Overture after Construct {visualizerObject.name}");
                                    Debug.Log($"[OvertureMapManager] ✓ Constructed visualizer '{visualizerObject.name}': {visualizer != null}");

                                    // Verify modifier stacks were added
                                    if (visualizer != null)
                                    {
                                        var visualizerImpl = visualizer as VectorLayerVisualizer;
                                        if (visualizerImpl != null)
                                        {
                                            var stacks = visualizerImpl.GetModStacks;
                                            Debug.Log($"[OvertureMapManager] ✓ Visualizer '{visualizerObject.name}' has {stacks?.Count ?? 0} modifier stacks");
                                        }
                                    }
                                }
                                catch (System.Exception ex)
                                {
                                    Debug.LogError($"[OvertureMapManager] Failed to construct visualizer '{visualizerObject.name}': {ex.Message}");
                                }

                                if (visualizer != null)
                                {
                                    string visualizerName = visualizerObject.name.ToLower();
                                    Debug.Log($"[OvertureMapManager] ✓ Registered visualizer: {visualizerObject.name}");

                                    if (visualizerName.Contains("building")) buildingVisualizer = visualizer;
                                    else if (visualizerName.Contains("road")) roadVisualizer = visualizer;
                                    else if (visualizerName.Contains("poi") || visualizerName.Contains("place")) poiVisualizer = visualizer;
                                    else if (visualizerName.Contains("water")) waterVisualizer = visualizer;
                                }
                                else
                                {
                                    Debug.LogWarning($"[OvertureMapManager] Failed to construct visualizer: {visualizerObject.name}");
                                }
                            }
                            else
                            {
                                Debug.LogWarning($"[OvertureMapManager] Cast failed - item is not VectorLayerVisualizerObject: {item}");
                            }
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogError($"[OvertureMapManager] Exception processing visualizer: {ex.Message}\n{ex.StackTrace}");
                        }
                        // Yield after each visualizer in editor (outside try-catch; CS1626)
                        if (Application.isEditor)
                        {
                            yield return null;
                        }
                    }
                }
                else
                {
                    Debug.LogError("[OvertureMapManager] _layerVisualizers field is null!");
                }
            }
            else
            {
                Debug.LogError("[OvertureMapManager] Could not find _layerVisualizers field!");
            }
        }
        else
        {
            Debug.LogError("[OvertureMapManager] VectorLayerModuleScript not found! Cannot load visualizers.");
        }

        Debug.Log($"[OvertureMapManager] Loaded visualizers: Building={buildingVisualizer != null}, Road={roadVisualizer != null}, POI={poiVisualizer != null}, Water={waterVisualizer != null}");
        yield return null;

        // Create visualizer dictionary
        var visualizers = new Dictionary<string, IVectorLayerVisualizer>();
        if (buildingVisualizer != null) visualizers["building"] = buildingVisualizer;
        if (roadVisualizer != null) visualizers["road"] = roadVisualizer;
        if (poiVisualizer != null) visualizers["poi_label"] = poiVisualizer;
        if (waterVisualizer != null) visualizers["water"] = waterVisualizer;

        if (Application.isEditor)
            BootDiagnostics.Mark("Overture before new OvertureVectorRenderer");
        m_Renderer = new OvertureVectorRenderer(_map, visualizers, logOvertureLayers);
        if (Application.isEditor)
            BootDiagnostics.Mark("Overture after new OvertureVectorRenderer");

        // Start initialization check
        BootDiagnostics.Mark("Overture InitializeWhenReady start");
        StartCoroutine(InitializeWhenReady());
    }

        private IEnumerator InitializeWhenReady()
        {
            BootDiagnostics.Mark("Overture InitializeWhenReady enter");
            // KILOVERSE CUSTOM SDK: We bypass Mapbox initialization since we're using our own tile system
            // All tiles come from api.kilomeme.com, not Mapbox servers
            Debug.Log("[OvertureMapManager] Initializing KiloverseMapbox custom SDK (bypassing Mapbox token requirement)...");

            // Wait one frame to ensure MapboxMapBehaviour has set up its coordinate systems
            yield return null;

            Debug.Log("[OvertureMapManager] Linking visualizers...");
            m_Renderer.InitializeVisualizers();
            RegisterLayers();
            Debug.Log($"[OvertureMapManager] Initialization complete. Layers: {m_Layers.Count}");
            BootDiagnostics.Mark("Overture InitializeWhenReady done");

            // Start building height logger to debug tall buildings
            StartCoroutine(InitBuildingHeightLoggerDelayed());
        }

        private IEnumerator InitBuildingHeightLoggerDelayed()
        {
            BootDiagnostics.Mark("BuildingHeightLogger init scheduled");
            // Give the first few frames to settle to avoid editor stalls.
            yield return null;
            yield return null;
            yield return new WaitForSeconds(5f);
            BootDiagnostics.Mark("BuildingHeightLogger init start");
            var _ = BuildingHeightLogger.Instance; // Trigger singleton creation
            BootDiagnostics.Mark("BuildingHeightLogger init done");
        }



private static bool _loggedFirstUpdateAfterAllowPlayer;
        private static bool _loggedEditorSkipMapInitHint;
private void Update()
        {
            if (!_loggedFirstUpdate)
            {
                BootDiagnostics.Mark("Overture.Update first");
                _loggedFirstUpdate = true;
            }
#if UNITY_EDITOR
            if (BootState.AllowPlayer && !_loggedFirstUpdateAfterAllowPlayer)
            {
                _loggedFirstUpdateAfterAllowPlayer = true;
                BootDiagnostics.Mark("OvertureMapManager first Update after AllowPlayer");
            }
            // One-time hint when tiles won't load because map init was skipped
            if (!_loggedEditorSkipMapInitHint && editorSkipMapInit && Time.frameCount > 60)
            {
                _loggedEditorSkipMapInitHint = true;
                Debug.LogWarning("[OvertureMapManager] Tiles not loading. Set 'Editor Skip Map Init' = FALSE on OvertureMapManager (Inspector) to enable tile loading in editor.");
            }
#endif
            // Editor: do NOT run any map/tile work until post-boot grace period has elapsed (prevents lockup)
            if (Application.isEditor && !BootState.PostBootGracePeriodElapsed)
            {
                return;
            }

            if (Application.isEditor)
            {
                if (Time.frameCount < editorWarmupFrames)
                {
                    return;
                }
                if (editorUpdateStride > 1 && (Time.frameCount % editorUpdateStride) != 0)
                {
                    return;
                }
            }

            // CRITICAL: Wait for GPS before loading/repositioning tiles (prevents main thread blocking during GPS init)
            if (!GPSLocationController.GPSReady)
            {
                if (Time.frameCount % 300 == 0) // Log every 5 seconds
                {
                    Debug.Log("[OvertureMapManager] Waiting for GPS to be ready before loading tiles...");
                }
                return;
            }

            // While boot is still "waiting for tiles", do minimal work so we don't lock up the frame
            bool waitingForFirstTiles = !BootState.FirstTilesLoaded;
            if (waitingForFirstTiles)
            {
                // Only poll for tiles every 2s during wait (not every 1s)
                if (Time.time - m_LastPoll < 2f) return;
                m_LastPoll = Time.time;
                // Skip tile position updates and frustum culling until we have tiles
                // Fall through to RequestCurrentTile only
            }
            else
            {
                // FLOATING ORIGIN: Reposition tiles EVERY FRAME for smooth conveyor-belt motion
                if (_map != null && m_Renderer != null)
                {
                    m_Renderer.UpdateAllTilePositions();
                }
                if (Time.time - m_LastPoll < tilePollInterval) return;
                m_LastPoll = Time.time;
            }

            // Skip heavy debug block while waiting for first tiles
            if (waitingForFirstTiles && Time.frameCount % 300 == 0)
            {
                BootDiagnostics.Mark("OvertureMapManager polling for first tile");
            }
            if (!waitingForFirstTiles && Time.frameCount % 300 == 0)
            {
                Debug.Log($"[OvertureMapManager] Update: map={(_map != null ? "OK" : "NULL")}, m_Renderer={(m_Renderer != null ? "OK" : "NULL")}, m_Layers.Count={m_Layers.Count}");

                // Count building GameObjects (total and active)
                if (m_Renderer != null)
                {
                    int totalBuildings = 0;
                    int activeBuildings = 0;

                    // Find the building layer root
                    var buildingLayerRoot = GameObject.Find("building layer objects");
                    if (buildingLayerRoot != null)
                    {
                        // Count unique tile indexes
                        HashSet<string> tileIndexes = new HashSet<string>();
                        Dictionary<string, int> tileCount = new Dictionary<string, int>();
                        Dictionary<string, int> activeTileCount = new Dictionary<string, int>();

                        totalBuildings = buildingLayerRoot.transform.childCount;

                        for (int i = 0; i < buildingLayerRoot.transform.childCount; i++)
                        {
                            Transform child = buildingLayerRoot.transform.GetChild(i);
                            if (child.gameObject.activeInHierarchy)
                                activeBuildings++;

                            // Extract tile index from name like "Overture_building_T5_123"
                            string name = child.name;
                            int tIndex = name.IndexOf("_T");
                            if (tIndex >= 0)
                            {
                                int underscoreAfter = name.IndexOf("_", tIndex + 2);
                                if (underscoreAfter > 0)
                                {
                                    string tileNum = name.Substring(tIndex + 2, underscoreAfter - tIndex - 2);
                                    tileIndexes.Add(tileNum);

                                    if (!tileCount.ContainsKey(tileNum))
                                    {
                                        tileCount[tileNum] = 0;
                                        activeTileCount[tileNum] = 0;
                                    }
                                    tileCount[tileNum]++;
                                    if (child.gameObject.activeInHierarchy)
                                        activeTileCount[tileNum]++;
                                }
                            }
                        }

                        var sortedTiles = new System.Collections.Generic.List<string>(tileIndexes);
                        sortedTiles.Sort((a, b) => int.Parse(a).CompareTo(int.Parse(b)));

                        Debug.Log($"[OvertureMapManager] Buildings: {activeBuildings}/{totalBuildings} active | Unique tiles: {tileIndexes.Count} (T{string.Join(", T", sortedTiles.ToArray())})");
                    }
                }
            }

            // Wait for initialization to complete
            if (_map == null || m_Renderer == null || m_Layers.Count == 0)
            {
                return;
            }

            // CRITICAL: Don't call RequestCurrentTile until well after boot completes
            // Even with grace period check, we need extra safety to prevent freeze
            if (Application.isEditor && BootState.AllowPlayer)
            {
                float timeSinceAllowPlayer = Time.realtimeSinceStartup - BootState.AllowPlayerTime;
                if (timeSinceAllowPlayer < 3f) // Wait 3 seconds after AllowPlayer before requesting tiles
                {
                    return;
                }
            }

            RequestCurrentTile();

            // Frustum culling - skip while waiting for first tiles (nothing to cull yet)
            if (waitingForFirstTiles) { /* skip */ }
            else if (m_Renderer == null)
            {
                if (Time.frameCount % 300 == 0)
                    Debug.LogError("[OvertureMapManager] m_Renderer is NULL! Cannot run frustum culling!");
            }
            else
            {
                m_Renderer.UpdateFrustumCulling();
            }
        }

        private void RegisterLayers()
        {
            // Clear all existing tiles from renderer (handles zoom level changes)
            m_Renderer?.ClearAllTiles();

            m_Layers.Clear();
            // Pass Name first, then Source Layers
            m_Layers.Add(new XYZLayer(m_BaseFetcher, m_Renderer, "LandLayer", "land", "land_use", "land_cover", "infrastructure"));
            m_Layers.Add(new XYZLayer(m_BaseFetcher, m_Renderer, "WaterLayer", "water"));
            m_Layers.Add(new XYZLayer(m_TransportationFetcher, m_Renderer, "RoadsLayer", "segment")); // Overture uses 'segment' in older schemas, but let's check 'transportation'
            m_Layers.Add(new XYZLayer(m_BuildingsFetcher, m_Renderer, "BuildingsLayer", "building"));
            m_Layers.Add(new XYZLayer(m_PlacesFetcher, m_Renderer, "PlacesLayer", "place"));
            Debug.Log($"[OvertureMapManager] Registered {m_Layers.Count} XYZ layers.");
        }

        // Calculate which tiles are visible in camera frustum
private HashSet<TileId> GetVisibleTilesInFrustum(LatitudeLongitude playerLatLon, int zoom)
    {
        var visibleTiles = new HashSet<TileId>();

        if (playerCamera == null)
        {
            playerCamera = Camera.main;
            if (playerCamera == null)
            {
                Debug.LogWarning("[TileLoad] No camera found for frustum culling - frustum disabled");
                return visibleTiles;
            }
        }

        // Calculate frustum planes (same approach as UpdateFrustumCulling())
        Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(playerCamera);

        // Get player's center tile
        var centerTile = Conversions.LatitudeLongitudeToTileId(playerLatLon, zoom);

        // Map center in Web Mercator (for calculating Unity positions)
        var mapboxLatLng = _map.MapInformation.LatitudeLongitude;
        var kiloverseLatLng = new LatitudeLongitude(mapboxLatLng.Latitude, mapboxLatLng.Longitude);
        Vector2d mapCenterMercator = Conversions.LatitudeLongitudeToWebMercator(kiloverseLatLng);

        int checkedCount = 0;
        int rejectedCount = 0;

        // Check tiles in 5x5 grid (larger than 3x3 to catch edge tiles)
        for (int dx = -2; dx <= 2; dx++)
        {
            for (int dy = -2; dy <= 2; dy++)
            {
                checkedCount++;
                var tileX = centerTile.X + dx;
                var tileY = centerTile.Y + dy;
                var tileId = new TileId(zoom, tileX, tileY);

                // Get tile bounds in Web Mercator
                var tileBounds = Conversions.TileBoundsInWebMercator(new TileId(zoom, tileX, tileY));
                
                // Calculate tile center in Web Mercator (NOT lat/lon!)
                Vector2d tileCenterMercator = new Vector2d(
                    (tileBounds.minX + tileBounds.maxX) / 2.0,
                    (tileBounds.minY + tileBounds.maxY) / 2.0
                );

                // Calculate Unity world position (offset from map center)
                Vector2d offsetMercator = tileCenterMercator - mapCenterMercator;
                Vector3 tileCenterUnity = new Vector3((float)offsetMercator.x, 0, (float)offsetMercator.y);

                // Calculate tile size in Unity units
                float tileWidthUnity = (float)(tileBounds.maxX - tileBounds.minX);
                float tileHeightUnity = (float)(tileBounds.maxY - tileBounds.minY);
                
                // Create bounds for this tile (center point + extents)
                // Height of 500 to ensure buildings are included
                Bounds tileBoundsUnity = new Bounds(
                    tileCenterUnity + new Vector3(0, 250, 0),
                    new Vector3(tileWidthUnity, 500, tileHeightUnity)
                );

                // Test if tile bounds intersect frustum (same as UpdateFrustumCulling())
                bool inFrustum = GeometryUtility.TestPlanesAABB(frustumPlanes, tileBoundsUnity);

                // Debug center tile
                if (dx == 0 && dy == 0)
                {
                    Debug.Log($"[FRUSTUM-CENTER] z{zoom}/{tileX}/{tileY} Unity=({tileCenterUnity.x:F1},{tileCenterUnity.y:F1},{tileCenterUnity.z:F1}) Size=({tileWidthUnity:F1}x{tileHeightUnity:F1}) InFrustum={inFrustum}");
                }

                if (inFrustum)
                {
                    visibleTiles.Add(tileId);
                }
                else
                {
                    rejectedCount++;
                }
            }
        }

        if (visibleTiles.Count == 0)
        {
            Debug.LogWarning($"[FRUSTUM] z{zoom}: All tiles rejected! Checked={checkedCount}, Rejected={rejectedCount}, CameraPos={playerCamera.transform.position}");
        }
        else
        {
            Debug.Log($"[FRUSTUM] z{zoom}: {visibleTiles.Count} tiles visible (checked {checkedCount})");
        }

        return visibleTiles;
    }

private void RequestCurrentTile()
        {
            if (!BootState.FirstTilesLoaded)
                BootDiagnostics.Mark("RequestCurrentTile start");
            // Overture tile zoom limits (tested):
            // - Base/Transportation: max z12 (~10km tiles, updates every ~3-10km movement)
            // - Buildings: max z14 (~2.5km tiles, updates every ~800m-2.5km movement)
            // - Places: max z15 (~1.2km tiles, updates every ~400m-1.2km movement)
            // Each layer independently tracks its own tiles and only loads/unloads when needed

            // Get position: player GPS if available, else map center (so tiles load during boot before player enabled)
            LatitudeLongitude playerLatLon;
            
            // Cache player controller to avoid expensive FindFirstObjectByType calls every frame
            // Only search periodically or if cached reference is null
            bool needsSearch = _cachedPlayerController == null;
            
            if (needsSearch || _playerSearchFrame >= PLAYER_SEARCH_INTERVAL)
            {
                _playerSearchFrame = 0;
                // In editor, throttle Find calls even more aggressively right after boot
                if (Application.isEditor && BootState.AllowPlayer)
                {
                    float timeSinceAllowPlayer = Time.realtimeSinceStartup - BootState.AllowPlayerTime;
                    if (timeSinceAllowPlayer < 5f && Time.frameCount % 120 != 0) // Only search every 2 seconds for first 5 seconds
                    {
                        // Use cached or fallback to map center - don't search yet
                    }
                    else
                    {
                        _cachedPlayerController = FindFirstObjectByType<KiloFirstPersonController>();
                    }
                }
                else
                {
                    _cachedPlayerController = FindFirstObjectByType<KiloFirstPersonController>();
                }
            }
            else
            {
                _playerSearchFrame++;
            }
            
            var playerController = _cachedPlayerController;
            
            if (!BootState.FirstTilesLoaded)
                BootDiagnostics.Mark("RequestCurrentTile after FindPlayer");
            if (playerController != null)
            {
                var mapboxPlayerGPS = playerController.playerGPS;
                playerLatLon = new LatitudeLongitude(mapboxPlayerGPS.Latitude, mapboxPlayerGPS.Longitude);
            }
            else
            {
                if (_map == null || _map.MapInformation == null) return;
                var mapCenter = _map.MapInformation.LatitudeLongitude;
                playerLatLon = new LatitudeLongitude(mapCenter.Latitude, mapCenter.Longitude);
                if (Time.frameCount % 60 == 0)
                    Debug.Log($"[OvertureMapManager] No player yet - using map center for tiles: ({playerLatLon.Latitude:F4}, {playerLatLon.Longitude:F4})");
            }

            // Debug current position every ~5 seconds
            if (Time.frameCount % 300 == 0 && playerController != null)
            {
                Vector3 playerWorldPos = playerController.transform.position;
                Debug.Log($"[TileLoad] Player GPS: ({playerLatLon.Latitude:F6}, {playerLatLon.Longitude:F6}) | Unity position: ({playerWorldPos.x:F1}, {playerWorldPos.y:F1}, {playerWorldPos.z:F1})");
            }

            // Update each layer independently
            // TEMP: Frustum culling disabled - using 3x3 grid for all layers until we fix Web Mercator scaling
            // TODO: Fix frustum culling to account for Web Mercator coordinate scale
            if (Time.frameCount % 300 == 0)
            {
                Debug.Log($"[TileLoad] Updating {m_Layers.Count} layers (3x3 grid mode): {string.Join(", ", m_Layers.Select(l => l.Name))}");
            }

            foreach (var layer in m_Layers)
            {
                // POI: Always 3x3 grid (for distance sorting in locations panel)
                // Visual layers: Frustum-based (buildings, roads, land/water)
                // TEMP: Force 3x3 grid for buildings to debug USX Tower loading
                bool useFrustumLoading = !layer.Name.Contains("Places") && !layer.Name.Contains("Buildings");
                HashSet<TileId> visibleTiles = null;

                if (useFrustumLoading)
                {
                    visibleTiles = GetVisibleTilesInFrustum(playerLatLon, layer.GetMaxZoom());
                    
                    // Log specific tile IDs for BuildingsLayer to debug USX Tower loading
                    if (layer.Name.Contains("Buildings"))
                    {
                        string tileList = string.Join(", ", visibleTiles.Select(t => $"{t.Z}/{t.X}/{t.Y}"));
                        Debug.Log($"[FRUSTUM] {layer.Name} z{layer.GetMaxZoom()}: {visibleTiles.Count} tiles visible: {tileList}");
                    }
                    else
                    {
                        Debug.Log($"[FRUSTUM] {layer.Name} z{layer.GetMaxZoom()}: {visibleTiles.Count} tiles visible");
                    }
                }

                layer.UpdateTilesForPosition(playerLatLon, this, useFrustumLoading, visibleTiles);
            }
        }
    }

    public class XYZTileFetcher
    {
        private readonly string _urlTemplate;

        public XYZTileFetcher(string urlTemplate)
        {
            _urlTemplate = urlTemplate;
        }

        public IEnumerator FetchTile(int zoom, int x, int y, Action<byte[]> onComplete)
        {
            var url = _urlTemplate
                .Replace("{z}", zoom.ToString())
                .Replace("{x}", x.ToString())
                .Replace("{y}", y.ToString());

            // Log to BootLog so we can see if a request is in flight when editor freezes
            string layerName = "tile";
            if (_urlTemplate.Contains("places")) layerName = "places";
            else if (_urlTemplate.Contains("buildings")) layerName = "buildings";
            else if (_urlTemplate.Contains("transportation")) layerName = "roads";
            else if (_urlTemplate.Contains("base")) layerName = "base";
            BootDiagnostics.Mark($"Tile API START {layerName} {zoom}/{x}/{y}");
            Debug.Log($"[XYZTileFetcher] Requesting {url}");

            float startTime = Time.realtimeSinceStartup;
            using (var request = UnityWebRequest.Get(url))
            {
#if UNITY_EDITOR
                request.timeout = 15; // Editor: avoid hanging forever if API is down
#endif
                yield return request.SendWebRequest();

                float elapsed = (Time.realtimeSinceStartup - startTime) * 1000f;
                long responseCode = request.responseCode;
                if (request.result == UnityWebRequest.Result.Success)
                {
                    var data = request.downloadHandler.data;
                    int sizeBytes = data != null ? data.Length : 0;
                    BootDiagnostics.Mark($"Tile API DONE {layerName} {zoom}/{x}/{y} ok {(int)elapsed}ms");
                    Debug.Log($"[XYZTileFetcher] ✓ {layerName} {zoom}/{x}/{y}: {responseCode} | {sizeBytes} bytes | {elapsed:F0}ms");
                    if (data != null && data.Length > 0)
                    {
                        onComplete?.Invoke(data);
                    }
                    else
                    {
                        Debug.LogWarning($"[XYZTileFetcher] ⚠️ Empty tile {zoom}/{x}/{y} (0 bytes)");
                        onComplete?.Invoke(null);
                    }
                }
                else
                {
                    BootDiagnostics.Mark($"Tile API DONE {layerName} {zoom}/{x}/{y} fail {responseCode}");
                    string responseBody = request.downloadHandler?.text ?? "";
                    Debug.LogError($"[XYZTileFetcher] ❌ {layerName} {zoom}/{x}/{y}: {responseCode} | {request.error}\nResponse: {(responseBody.Length > 200 ? responseBody.Substring(0, 200) + "..." : responseBody)}");
                    onComplete?.Invoke(null);
                }
            }
        }
    }

    public class XYZLayer
    {
        protected readonly XYZTileFetcher Fetcher;
        protected readonly string[] SourceLayers;
        public readonly string Name;
        protected readonly OvertureVectorRenderer Renderer;
        protected readonly int MaxZoom;

        private readonly HashSet<TileId> _loadedTiles = new HashSet<TileId>();
        private readonly HashSet<TileId> _requestingTiles = new HashSet<TileId>(); // Tiles currently being requested/loaded
        private readonly HashSet<TileId> _renderingTiles = new HashSet<TileId>();
        private TileId? _lastCenterTile;
        private int _globalTileIndex = 0; // Never reset, keeps counting across all tile loads

        // Tiered tile cache - tuned to actual tile sizes and walking speed (~5km/h)
        //   z12 (~10km tiles): Player crosses one every ~2 hours. Keep 10 min, max 12 tiles.
        //   z14 (~2.5km tiles): Player crosses one every ~30 min. Keep 5 min, max 16 tiles.
        //   z15 (~1.2km tiles): Player crosses one every ~15 min. Keep 2 min, max 20 tiles.
        private readonly Dictionary<TileId, float> _tileCacheTime = new Dictionary<TileId, float>();
        private float TileCacheTimeout => MaxZoom <= 12 ? 600f : MaxZoom <= 14 ? 300f : 120f;
        private int MaxLoadedTiles => MaxZoom <= 12 ? 12 : MaxZoom <= 14 ? 16 : 20;

        // Track tile request times for API performance logging
        public static readonly Dictionary<string, float> _tileRequestTimes = new Dictionary<string, float>();

        public XYZLayer(XYZTileFetcher fetcher, OvertureVectorRenderer renderer, string name, params string[] sourceLayers)
        {
            Fetcher = fetcher;
            Renderer = renderer;
            SourceLayers = sourceLayers;
            Name = name;

            // Set max zoom based on layer name (tested limits)
            if (name.Contains("Places"))
                MaxZoom = 15;  // Places: z15 (~1.2km tiles, 3x3 = 3.6km coverage)
            else if (name.Contains("Buildings"))
                MaxZoom = 14;  // Buildings: z14 CONFIRMED (z15 has no data, must use z14)
            else if (name.Contains("Roads"))
                MaxZoom = 14;  // Roads: z14 CONFIRMED (z15 has no data, must use z14)
            else if (name.Contains("Land") || name.Contains("Water"))
                MaxZoom = 12;  // Land/Water: z12 (10km tiles, can stay large)
            else
                MaxZoom = 12;  // Default safe zoom
        }

        public int GetMaxZoom()
        {
            return MaxZoom;
        }

        public void UpdateTilesForPosition(LatitudeLongitude latLon, MonoBehaviour coroutineHost, bool useFrustum = false, HashSet<TileId> visibleTiles = null)
        {
            // Calculate current center tile at this layer's zoom
            var currentCenter = Conversions.LatitudeLongitudeToTileId(latLon, MaxZoom);

            // Determine which tiles should be loaded
            HashSet<TileId> newTiles;

            if (useFrustum && visibleTiles != null)
            {
                // Frustum-based loading (buildings, roads, land/water)
                newTiles = visibleTiles;
            }
            else
            {
                // Grid-based loading (POI - always 3x3)
                newTiles = new HashSet<TileId>();
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        var tileX = currentCenter.X + dx;
                        var tileY = currentCenter.Y + dy;
                        newTiles.Add(new TileId(currentCenter.Z, tileX, tileY));
                    }
                }
            }

            // Update cache times for visible tiles (reset timeout)
            float currentTime = Time.time;
            foreach (var tile in newTiles)
            {
                _tileCacheTime[tile] = currentTime;
            }

            // Find tiles to add (in new set but not in loaded or currently requesting)
            var tilesToAdd = new HashSet<TileId>(newTiles);
            tilesToAdd.ExceptWith(_loadedTiles);
            tilesToAdd.ExceptWith(_requestingTiles);

            // Find tiles to remove (loaded tiles that are no longer visible AND cache expired)
            var tilesToRemove = new HashSet<TileId>();
            foreach (var tile in _loadedTiles)
            {
                if (!newTiles.Contains(tile))
                {
                    // Tile not visible - check if cache expired (tiered by zoom level)
                    if (_tileCacheTime.TryGetValue(tile, out float lastSeenTime))
                    {
                        if (currentTime - lastSeenTime > TileCacheTimeout)
                        {
                            tilesToRemove.Add(tile);
                            _tileCacheTime.Remove(tile);
                        }
                    }
                    else
                    {
                        // No cache entry - remove immediately
                        tilesToRemove.Add(tile);
                    }
                }
            }

            // MEMORY CAP: If we exceed max loaded tiles, evict oldest non-visible tiles first
            int projectedCount = _loadedTiles.Count + tilesToAdd.Count - tilesToRemove.Count;
            if (projectedCount > MaxLoadedTiles)
            {
                int excess = projectedCount - MaxLoadedTiles;
                // Sort non-visible tiles by oldest cache time → evict oldest first
                var evictionCandidates = _loadedTiles
                    .Where(t => !newTiles.Contains(t) && !tilesToRemove.Contains(t) && !_renderingTiles.Contains(t))
                    .OrderBy(t => _tileCacheTime.TryGetValue(t, out float time) ? time : 0f)
                    .Take(excess)
                    .ToList();

                foreach (var tile in evictionCandidates)
                {
                    tilesToRemove.Add(tile);
                    _tileCacheTime.Remove(tile);
                }

                if (evictionCandidates.Count > 0)
                {
                    Debug.Log($"[TileLoad] {Name}: MEMORY CAP evicted {evictionCandidates.Count} oldest tiles (limit={MaxLoadedTiles}, loaded={_loadedTiles.Count})");
                }
            }

            // Log ONCE when tiles change (reduced spam)
            if (tilesToAdd.Count > 0 || tilesToRemove.Count > 0)
            {
                string mode = useFrustum ? "FRUSTUM" : "GRID";
                Debug.Log($"[TileLoad] {Name} ({mode}): +{tilesToAdd.Count} tiles, -{tilesToRemove.Count} tiles | Loaded: {_loadedTiles.Count}/{MaxLoadedTiles}, Cached: {_tileCacheTime.Count}, CacheTimeout: {TileCacheTimeout}s");
            }

            // Remove old tiles (but skip tiles that are currently rendering)
            foreach (var tile in tilesToRemove)
            {
                if (_renderingTiles.Contains(tile))
                {
                    continue; // Don't remove tiles that are mid-render
                }

                if (Renderer != null)
                {
                    Renderer.RemoveTile(tile);
                }
                _loadedTiles.Remove(tile);
            }

            // Load new tiles (editor: throttle to prevent lockup when many responses arrive at once)
            int maxNewPerPoll = Application.isEditor ? 2 : 20;
            int maxInFlight = Application.isEditor ? 4 : 25;
            int added = 0;
            foreach (var tile in tilesToAdd)
            {
                if (added >= maxNewPerPoll || _requestingTiles.Count >= maxInFlight) break;

                string tileKey = $"{tile.Z}/{tile.X}/{tile.Y}";
                _tileRequestTimes[tileKey] = Time.realtimeSinceStartup;

                _requestingTiles.Add(tile); // Mark as requesting (will move to _loadedTiles after successful load)
                coroutineHost.StartCoroutine(RequestTile(tile, _globalTileIndex));
                _globalTileIndex++;
                added++;
            }

            _lastCenterTile = new TileId(currentCenter.Z, currentCenter.X, currentCenter.Y);
        }

public IEnumerator RequestTile(TileId tile, int tileIndex)
        {
            yield return Fetcher.FetchTile(tile.Z, tile.X, tile.Y, data =>
            {
                if (data != null && data.Length > 0)
                {
                    _renderingTiles.Add(tile); // Mark as rendering

                    try
                    {
                        Renderer?.RenderTile(tile, data, SourceLayers, Name, tileIndex);

                        // Successfully loaded - move from requesting to loaded
                        _requestingTiles.Remove(tile);
                        _loadedTiles.Add(tile);
                        if (_loadedTiles.Count == 1)
                            BootState.SetFirstTilesLoaded();
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[TileLoad] {Name}: RenderTile FAILED for {tile.Z}/{tile.X}/{tile.Y}: {ex.Message}");
                        _requestingTiles.Remove(tile); // Failed - remove from requesting so we can retry later
                    }
                    finally
                    {
                        _renderingTiles.Remove(tile); // Always unmark when done
                    }
                }
                else
                {
                    _requestingTiles.Remove(tile); // Failed - remove from requesting
                }
            });
        }
    }

    public class OvertureVectorRenderer
    {
        private readonly KiloverseMapInfo _map;
        private readonly Dictionary<string, IVectorLayerVisualizer> _visualizers =
            new Dictionary<string, IVectorLayerVisualizer>();
        private readonly Dictionary<string, List<GameObject>> _tileObjects =
            new Dictionary<string, List<GameObject>>();
        private readonly HashSet<string> _loggedTiles = new HashSet<string>();
        private readonly bool _logLayers;
        private static bool _waterTileStructureLogged;
        private bool _initialized;
        private Vector2d _lastMapCenterMercator;

        // Layer root Y offsets — applied every frame to prevent z-fighting
        // Key: visualizer key ("road", "water", "building", "poi_label")
        // Value: Y offset in meters
        private readonly Dictionary<string, float> _layerYOffsets = new Dictionary<string, float>
        {
            { "water",     0.15f },
            { "road",      0.3f  },
            { "building",  0f    },
            { "poi_label", 0f    }
        };
        // Cache: maps each tile object's Transform → its layer Y offset (set once at creation)
        private readonly Dictionary<Transform, float> _objectYOffset = new Dictionary<Transform, float>();

        private static readonly Dictionary<string, string> LayerMapping = new Dictionary<string, string>
        {
            { "segment", "road" },
            { "place", "poi_label" },
            { "building", "building" },
            { "water", "water" }
        };

        // === MEMORY BUDGETS per source layer per tile ===
        // Currently UNCAPPED to measure real-world memory usage on downtown tiles.
        // Each vertex ≈ 80 bytes (pos 12 + normal 12 + tangent 16 + uv0 8 + uv1 8 + indices ~24)
        // The [MEMORY] logs report per-tile and global totals so you can decide where to cap later.
        private const int VERTEX_BUDGET_BUILDING = int.MaxValue;  // UNCAPPED — monitoring only
        private const int VERTEX_BUDGET_ROAD     = int.MaxValue;
        private const int VERTEX_BUDGET_WATER    = int.MaxValue;
        private const int VERTEX_BUDGET_DEFAULT  = int.MaxValue;
        private const int FEATURE_CAP_BUILDING   = int.MaxValue;  // UNCAPPED — load all skyscrapers
        private const int FEATURE_CAP_ROAD       = int.MaxValue;
        private const int FEATURE_CAP_DEFAULT    = int.MaxValue;
        private const int FEATURES_PER_YIELD     = 50;       // Yield to main thread every N features (editor uses 10)

        public OvertureVectorRenderer(KiloverseMapInfo map, Dictionary<string, IVectorLayerVisualizer> visualizers, bool logLayers)
        {
            _map = map;
            _logLayers = logLayers;

            // Use provided visualizers directly (no Mapbox SDK dependency!)
            foreach (var kvp in visualizers)
            {
                _visualizers[kvp.Key] = kvp.Value;
                Debug.Log($"[OvertureVectorRenderer] Linked visualizer: {kvp.Key}");
            }

            InitializeVisualizers();
        }

        public void InitializeVisualizers()
        {
            if (_initialized || _map == null) return;

            // Ensure parent layer objects are created and active
            foreach (var kvp in _visualizers)
            {
                var visualizer = kvp.Value as VectorLayerVisualizer;
                if (visualizer != null)
                {
                    var rootField = typeof(VectorLayerVisualizer).GetField("_layerRootObject", BindingFlags.NonPublic | BindingFlags.Instance);
                    var rootTransform = rootField?.GetValue(visualizer) as Transform;

                    if (rootTransform == null || rootTransform.gameObject == null)
                    {
                        // Create layer root object if it doesn't exist
                        var layerRoot = new GameObject($"{kvp.Key} layer objects");
                        rootTransform = layerRoot.transform;
                        rootField?.SetValue(visualizer, rootTransform);
                        Debug.Log($"[OvertureVectorRenderer] Created layer root: {layerRoot.name}");
                    }
                    else
                    {
                        rootTransform.gameObject.SetActive(true);
                        Debug.Log($"[OvertureVectorRenderer] Activated existing layer root: {rootTransform.name}");
                    }

                    // Set initial layer Y positions for proper rendering order:
                    // Ground plane (0.05) < Water (0.15) < Roads (0.3) < Buildings (0.0 - they have height)
                    // RenderManager.ApplyMapSettings() will override with profile values at runtime
                    float layerY = 0f;
                    if (kvp.Key.Contains("water")) layerY = 0.15f;
                    else if (kvp.Key.Contains("road")) layerY = 0.3f;
                    if (layerY > 0f)
                    {
                        rootTransform.position = new Vector3(0, layerY, 0);
                        Debug.Log($"[OvertureVectorRenderer] Set '{kvp.Key}' layer root Y={layerY}");
                    }

                    // Initialize the visualizer (sets up modifier stacks)
                    // FIXED: Don't force synchronous execution - this blocks Unity's main thread in editor!
                    // Instead, start the initialization coroutine asynchronously to prevent freezing.
                    try
                    {
                        // Start the initialization coroutine asynchronously
                        // In editor, we need to yield properly to avoid freezing
                        if (_map != null && _map.gameObject != null)
                        {
                            _map.StartCoroutine(InitializeVisualizerCoroutine(visualizer, kvp.Key));
                        }
                        else
                        {
                            // Fallback: if we can't start a coroutine, try minimal sync init
                            // This should only happen in edge cases
                            Debug.LogWarning($"[OvertureVectorRenderer] Cannot start coroutine for '{kvp.Key}', map object not available");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[OvertureVectorRenderer] Failed to initialize '{kvp.Key}': {ex.Message}");
                    }
                }
            }

            // Mark as initialized immediately so rendering can proceed
            // The actual visualizer initialization will complete asynchronously
            _initialized = _visualizers.Count > 0;
            Debug.Log($"[OvertureVectorRenderer] Initializing {_visualizers.Count} visualizers asynchronously (no blocking) - KILOVERSE SDK");
        }

        private IEnumerator InitializeVisualizerCoroutine(VectorLayerVisualizer visualizer, string key)
        {
            var initEnumerator = visualizer.Initialize();
            while (initEnumerator.MoveNext())
            {
                // Yield each frame to prevent blocking Unity's main thread
                yield return null;
            }
            var stacks = visualizer.GetModStacks;
            Debug.Log($"[OvertureVectorRenderer] ✓ Initialized '{key}': {stacks?.Count ?? 0} modifier stacks (pool created)");
        }

public void RenderTile(TileId tile, byte[] payload, string[] sourceLayers, string layerName = "", int tileIndex = -1)
        {
            // Start coroutine to render tile over multiple frames
            _map.StartCoroutine(RenderTileCoroutine(tile, payload, sourceLayers, layerName, tileIndex));
        }

private IEnumerator RenderTileCoroutine(TileId tile, byte[] payload, string[] sourceLayers, string layerName = "", int tileIndex = -1)
        {
            // Editor: yield immediately to spread burst when many tiles complete at once (prevents lockup)
            if (Application.isEditor) yield return null;

            string tileIndexStr = tileIndex >= 0 ? $" tile #{tileIndex}" : "";
            if (!_initialized) InitializeVisualizers();
            if (!_initialized)
            {
                 Debug.LogError("[OvertureVectorRenderer] Failed to initialize visualizers. Check VectorLayerModuleScript.");
                 yield break;
            }

            var tileKey = $"{tile.Z}/{tile.X}/{tile.Y}";

            // Calculate API timing
            float apiTimeMs = 0f;
            if (XYZLayer._tileRequestTimes.TryGetValue(tileKey, out float startTime))
            {
                apiTimeMs = (Time.realtimeSinceStartup - startTime) * 1000f; // Convert to milliseconds
                XYZLayer._tileRequestTimes.Remove(tileKey); // Clean up
            }

            // Use the simplified decoder
            if (!PMTilesTileDecoder.TryParseVectorTile(payload, out var vectorTile))
            {
                Debug.LogWarning($"[OvertureVectorRenderer]{tileIndexStr} Failed to parse tile {tileKey}");
                yield break; // Failed to parse
            }

            if (_logLayers && !_loggedTiles.Contains(tileKey))
            {
                _loggedTiles.Add(tileKey);
            }

            var createdObjects = new List<GameObject>();
            foreach (var sourceLayer in sourceLayers)
            {
                var visualizerKey = ResolveVisualizerKey(sourceLayer);
                if (visualizerKey == null) continue;
                
                // Debug.Log($"[Overture] Processing '{sourceLayer}' -> '{visualizerKey}'");

                if (!_visualizers.TryGetValue(visualizerKey, out var visualizer))
                {
                     Debug.LogWarning($"[Overture]{tileIndexStr} No visualizer found for key '{visualizerKey}'");
                     continue;
                }

                if (!vectorTile.LayerNames().Contains(sourceLayer))
                {
                    continue;
                }

                // Check if visualizer has a valid layer root (prevents NullReferenceException in CreateGo)
                var visualizerImpl = visualizer as VectorLayerVisualizer;
                if (visualizerImpl != null)
                {
                    var rootField = typeof(VectorLayerVisualizer).GetField("_layerRootObject", BindingFlags.NonPublic | BindingFlags.Instance);
                    var rootTransform = rootField?.GetValue(visualizerImpl) as Transform;
                    if (rootTransform == null || rootTransform.gameObject == null)
                    {
                        Debug.LogError($"[Overture]{tileIndexStr} Visualizer '{visualizerKey}' has no layer root object! Skipping tile {tileKey}.");
                        continue;
                    }
                    Debug.Log($"[TileLoad] {layerName}{tileIndexStr} {tileKey}: Layer root exists: {rootTransform.gameObject.name} (active={rootTransform.gameObject.activeInHierarchy})");
                }

                var layerData = vectorTile.GetLayer(sourceLayer);
                var meshDataDict = new Dictionary<int, HashSet<MeshData>>();

                // WATER DIAGNOSTIC: Log tile structure once — what makes it "water" is the layer name (backend puts features there)
                if (sourceLayer == "water" && !_waterTileStructureLogged)
                {
                    _waterTileStructureLogged = true;
                    var layerNames = vectorTile.LayerNames();
                    var layerList = layerNames != null ? string.Join(", ", layerNames) : "(null)";
                    Debug.Log($"[Water] {tileKey} TILE LAYERS: [{layerList}] — we render layer 'water' (backend decides content)");
                }

                if (visualizerImpl != null)
                {
                    var stacks = visualizerImpl.GetModStacks;
                    if (stacks == null || stacks.Count == 0)
                    {
                        Debug.LogWarning($"[Overture]{tileIndexStr} Visualizer '{visualizerKey}' has no modifier stacks! Tile {tileKey} cannot be rendered.");
                        continue;
                    }

                    foreach (var stackPair in stacks)
                    {
                        var stack = stackPair.Value;
                        int featureCount = layerData.FeatureCount();
                        int waterSkipped = 0, waterRendered = 0; // Diagnostic for water layer

                        // Memory budgets: prevent single tile from consuming unbounded RAM
                        int vertexBudget = sourceLayer switch {
                            "building" => VERTEX_BUDGET_BUILDING,
                            "segment"  => VERTEX_BUDGET_ROAD,
                            "water"    => VERTEX_BUDGET_WATER,
                            _          => VERTEX_BUDGET_DEFAULT
                        };
                        int featureCap = sourceLayer switch {
                            "building" => FEATURE_CAP_BUILDING,
                            "segment"  => FEATURE_CAP_ROAD,
                            _          => FEATURE_CAP_DEFAULT
                        };
                        int cumulativeVerts = 0;
                        int processedFeatures = 0;
                        bool budgetExceeded = false;

                        int effectiveFeatureCount = Mathf.Min(featureCount, featureCap);
                        if (featureCount > featureCap)
                        {
                            Debug.Log($"[TileLoad] {layerName}{tileIndexStr} {tileKey} '{sourceLayer}': Feature cap hit — {featureCount} features, capped to {featureCap}");
                        }

                        int featuresPerYield = Application.isEditor ? 5 : FEATURES_PER_YIELD;
                        for (int i = 0; i < effectiveFeatureCount; i++)
                        {
                            // Yield periodically to prevent frame stalls on dense tiles (editor: more often to avoid lockup)
                            if (i > 0 && i % featuresPerYield == 0)
                                yield return null;

                            var feature = layerData.GetFeature(i);

                            // Convert to Unity Feature
                            var featureUnity = new VectorFeatureUnity();
                            featureUnity.Properties = feature.GetProperties();

                            // BUG FIX: Mapbox.VectorTile.VectorTileReader.dll doubles num_floors values
                            // Evidence: MVT tile contains num_floors=64, GetProperties() returns 128
                            // Confirmed by parsing same tile with Python mapbox_vector_tile library
                            // Affects ALL buildings: building #1 MVT=13 SDK=26, building #962 MVT=64 SDK=128
                            // Root cause: Unknown bug in Mapbox.VectorTile.dll protobuf parser
                            // TODO: Replace DLL with fixed version from https://github.com/mapbox/vector-tile-cs
                            if (sourceLayer == "building" && featureUnity.Properties.ContainsKey("num_floors"))
                            {
                                try
                                {
                                    long numFloors = Convert.ToInt64(featureUnity.Properties["num_floors"]);
                                    featureUnity.Properties["num_floors"] = numFloors / 2;
                                }
                                catch (System.Exception ex)
                                {
                                    Debug.LogWarning($"[MVT Bug Fix] Failed to correct num_floors for building #{i}: {ex.Message}");
                                }
                            }

                            featureUnity.Data = feature;
                            featureUnity.TileId = tile;
                            
                            // Geometry conversion (Simplified from VectorLayerVisualizer)
                            var geom = feature.Geometry<float>(0);
                            var points = new List<List<Vector3>>();
                            foreach (var g in geom)
                            {
                                var part = new List<Vector3>();
                                foreach (var p in g)
                                {
                                    part.Add(new Vector3(p.X / 4096f, 0, -1 * (p.Y / 4096f))); // 4096 = Layer Extent
                                }
                                points.Add(part);
                            }
                            featureUnity.Points = points;

                            // WATER FILTERS: Skip problematic water (El Paso fix - giant polygons, ocean, intermittent, canals)
                            if (sourceLayer == "water")
                            {
                                if (IsIntermittentWater(featureUnity.Properties))
                                {
                                    waterSkipped++; continue; // Skip dry washes, arroyos
                                }
                                if (IsDesertWaterClass(featureUnity.Properties))
                                {
                                    waterSkipped++; continue; // Skip canal, ditch, stream, drain - often dry in desert
                                }
                                if (IsOceanOrSea(featureUnity.Properties))
                                {
                                    waterSkipped++; continue; // Skip ocean/sea - giant polygons, not relevant inland
                                }
                                // Skip GIANT polygons (ocean, bathymetry, land_cover misclassification)
                                // At z12 tile ~10km; 300+ verts = likely ocean/land_cover, not pond/river
                                int totalVerts = 0;
                                if (featureUnity.Points != null)
                                    foreach (var part in featureUnity.Points) totalVerts += part?.Count ?? 0;
                                if (totalVerts > 300)
                                {
                                    if (i < 3) // Log first few skips
                                        Debug.Log($"[Water] {tileKey} SKIP giant polygon #{i}: {totalVerts} verts, class={GetWaterClass(featureUnity.Properties)}");
                                    waterSkipped++; continue;
                                }
                                var cls = GetWaterClass(featureUnity.Properties);
                                var sub = GetWaterSubtype(featureUnity.Properties);
                                // LINESTRING water = river centerlines. PolygonMeshModifier would close the path and fill whole tile.
                                // Render as extruded line (like roads) instead.
                                if ((int)feature.GeometryType == (int)GeomType.LINESTRING)
                                {
                                    if (string.IsNullOrEmpty(cls) && string.IsNullOrEmpty(sub))
                                    {
                                        waterSkipped++; continue; // Skip drainage lines, etc.
                                    }
                                    // Generate line mesh for river centerline
                                    var riverMd = new MeshData();
                                    riverMd.Feature = featureUnity;
                                    GenerateRiverLineMesh(featureUnity, riverMd, _map.MapInformation);
                                    if (riverMd.Vertices != null && riverMd.Vertices.Count > 0)
                                    {
                                        cumulativeVerts += riverMd.Vertices.Count;
                                        processedFeatures++;
                                        if (waterRendered < 2)
                                            Debug.Log($"[Water] {tileKey} RENDER LINESTRING #{waterRendered} class={cls} subtype={sub} verts={totalVerts} (as line)");
                                        waterRendered++;
                                        if (!meshDataDict.ContainsKey(stackPair.Key)) meshDataDict.Add(stackPair.Key, new HashSet<MeshData>());
                                        meshDataDict[stackPair.Key].Add(riverMd);
                                        if (cumulativeVerts > vertexBudget && !budgetExceeded)
                                        {
                                            budgetExceeded = true;
                                            Debug.LogWarning($"[MEMORY] {layerName}{tileIndexStr} {tileKey} 'water': VERTEX BUDGET exceeded. Skipping remaining features.");
                                        }
                                        if (budgetExceeded) break;
                                    }
                                    continue;
                                }
                                // Skip water with NO class/subtype - likely land_cover or merged source, causes "water everywhere" in desert
                                if (string.IsNullOrEmpty(cls) && string.IsNullOrEmpty(sub))
                                {
                                    if (i < 2) // Log first 2 skips + full diagnostic (geom type, all props)
                                    {
                                        var geomType = feature.GeometryType.ToString();
                                        var sb = new System.Text.StringBuilder();
                                        if (featureUnity.Properties != null)
                                            foreach (var kvp in featureUnity.Properties)
                                                sb.Append($"{kvp.Key}={kvp.Value} ");
                                        Debug.Log($"[Water] {tileKey} SKIP no class/subtype #{i} geom={geomType} verts={totalVerts} | PROPS: {(sb.Length > 0 ? sb.ToString() : "(none)")}");
                                    }
                                    waterSkipped++; continue;
                                }
                                // DIAGNOSTIC: Log RENDERED water (these pass the filter — if blue everywhere, these have class/subtype)
                                if (waterRendered < 3)
                                {
                                    var sb = new System.Text.StringBuilder();
                                    if (featureUnity.Properties != null)
                                        foreach (var kvp in featureUnity.Properties)
                                            sb.Append($"{kvp.Key}={kvp.Value} ");
                                    Debug.Log($"[Water] {tileKey} RENDER #{waterRendered} class={cls} subtype={sub} verts={totalVerts} | PROPS: {(sb.Length > 0 ? sb.ToString() : "(none)")}");
                                }
                                waterRendered++;
                            }

                            // BYPASS FILTER CHECK
                            // if (stack.Filters != null && !stack.Filters.Try(featureUnity)) continue;

                            var md = new MeshData();
                            md.Feature = featureUnity;

                            // Check if we have valid geometry for mesh generation
                            bool hasGeometry = featureUnity.Points != null &&
                                              featureUnity.Points.Count > 0 &&
                                              featureUnity.Points[0].Count > 0;

                            // ONLY run GameObject modifiers early for POI labels (place layer)
                            // POI labels create their own anchors and don't need mesh GameObjects
                            // Other layers (roads, buildings) need GameObject modifiers to run AFTER CreateGo
                            if (sourceLayer == "place" || sourceLayer == "poi_label" || sourceLayer == "poi")
                            {
                                // Register POI with TransmitterScanner directly (POILabelModifier removed)
                                try
                                {
                                    RegisterPOIWithScanner(featureUnity, tile);
                                }
                                catch (System.Exception modEx)
                                {
                                    Debug.LogError($"[TileLoad] POI registration failed: {modEx.Message}");
                                }
                            }

                            // Skip mesh generation if no geometry (POI modifiers already ran above)
                            if (!hasGeometry)
                            {
                                // Features with no geometry (e.g., POIs) don't generate meshes
                                continue;
                            }

                            
                            // DEBUG: Log buildings with high floor counts in T7
                            if (sourceLayer == "building" && tileKey.Contains("14/4551/6176"))
                            {
                                if (featureUnity.Properties.ContainsKey("num_floors"))
                                {
                                    try
                                    {
                                        int floors = Convert.ToInt32(featureUnity.Properties["num_floors"]);
                                        if (floors >= 50)
                                        {
                                            string hasHeight = featureUnity.Properties.ContainsKey("height") ? featureUnity.Properties["height"].ToString() : "NO";
                                            Debug.Log($"[TileLoad] T7 TALL BUILDING #{i}: num_floors={floors}, height={hasHeight}");
                                        }
                                    }
                                    catch { }
                                }
                            }
// Wrap in try-catch to handle invalid geometry
                            try
                            {
                                md = stack.RunMeshModifiers(featureUnity, md, _map.MapInformation);
                            }
                            catch (System.Exception ex)
                            {
                                // Only log first error per tile to avoid spam
                                if (i == 0)
                                {
                                    Debug.LogWarning($"[Overture]{tileIndexStr} Tile {tile.Z}/{tile.X}/{tile.Y} layer '{sourceLayer}': Mesh generation failed on feature #{i}\nException: {ex.GetType().Name}\nMessage: {ex.Message}\nStack: {ex.StackTrace}");
                                }
                                continue; // Skip this feature
                            }
                            
                            // Only add MeshData with valid vertices to avoid CreateGo crashes
                            if (md.Vertices != null && md.Vertices.Count > 0)
                            {
                                cumulativeVerts += md.Vertices.Count;
                                processedFeatures++;

                                // VERTEX BUDGET CHECK: Stop processing if we'd exceed memory budget
                                if (cumulativeVerts > vertexBudget)
                                {
                                    if (!budgetExceeded)
                                    {
                                        budgetExceeded = true;
                                        float estimatedMB = (cumulativeVerts * 80f) / (1024f * 1024f);
                                        Debug.LogWarning($"[MEMORY] {layerName}{tileIndexStr} {tileKey} '{sourceLayer}': VERTEX BUDGET exceeded at feature {i}/{effectiveFeatureCount} — {cumulativeVerts:N0} verts (~{estimatedMB:F1}MB), budget={vertexBudget:N0}. Skipping remaining features.");
                                    }
                                    break; // Stop processing more features for this layer
                                }

                                if (!meshDataDict.ContainsKey(stackPair.Key)) meshDataDict.Add(stackPair.Key, new HashSet<MeshData>());
                                meshDataDict[stackPair.Key].Add(md);
                            }
                        }

                        // WATER SUMMARY: If blue everywhere but we skip no-class, something else is rendering
                        if (sourceLayer == "water" && featureCount > 0)
                        {
                            Debug.Log($"[Water] {tileKey} SUMMARY: {featureCount} total → {waterSkipped} skipped, {waterRendered} passed filter (rendered)");
                        }

                        // Log memory stats for this layer
                        if (processedFeatures > 0)
                        {
                            float estimatedMB = (cumulativeVerts * 80f) / (1024f * 1024f);
                            string budgetStatus = budgetExceeded ? " CAPPED" : "";
                            Debug.Log($"[MEMORY] {layerName}{tileIndexStr} {tileKey} '{sourceLayer}': {processedFeatures}/{featureCount} features, {cumulativeVerts:N0} verts (~{estimatedMB:F1}MB){budgetStatus}");
                        }
                    }
                }

                // Check if mesh data was actually generated
                int meshCount = 0;
                foreach(var pair in meshDataDict)
                {
                    foreach(var m in pair.Value)
                    {
                        if (m.Vertices.Count > 0) meshCount++;
                    }
                }

                // Only create GameObjects if we have actual mesh geometry
                // POI labels have 0 mesh modifiers, so skip CreateGo for them (labels created by POILabelModifier)
                if (meshCount > 0)
                {
                    // Verify all MeshData in dict has valid data before calling CreateGo
                    int validMeshes = 0;
                    int invalidMeshes = 0;
                    foreach (var stackPair in meshDataDict)
                    {
                        foreach (var md in stackPair.Value)
                        {
                            if (md.Vertices != null && md.Vertices.Count > 0)
                                validMeshes++;
                            else
                                invalidMeshes++;
                        }
                    }

                    Debug.Log($"[TileLoad] {layerName}{tileIndexStr} {tile.Z}/{tile.X}/{tile.Y}: Calling CreateGo with {meshDataDict.Count} stacks, {validMeshes} valid meshes, {invalidMeshes} invalid meshes");

                    if (invalidMeshes > 0)
                    {
                        Debug.LogError($"[TileLoad] {layerName}{tileIndexStr} {tile.Z}/{tile.X}/{tile.Y}: Found {invalidMeshes} invalid meshes in meshDataDict! This should never happen!");
                    }

                    List<GameObject> layerObjects;
                    try
                    {
                        layerObjects = visualizer.CreateGo(tile, meshDataDict);
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[TileLoad] {layerName}{tileIndexStr} {tile.Z}/{tile.X}/{tile.Y}: CreateGo FAILED: {ex.Message}\n{ex.StackTrace}");
                        continue; // Skip this source layer
                    }

                    // Count POIs, roads, buildings, etc.
                    string objectType = sourceLayer == "place" ? "POIs" :
                                       sourceLayer == "road" ? "road segments" :
                                       sourceLayer == "building" ? "buildings" :
                                       sourceLayer == "water" ? "water features" : "objects";

                    
                    // DEBUG: Check mesh vertex counts immediately after CreateGo for tall buildings
                    if (sourceLayer == "building" && tileKey.Contains("14/4551/6176"))
                    {
                        for (int debugIdx = 0; debugIdx < layerObjects.Count; debugIdx++)
                        {
                            var go = layerObjects[debugIdx];
                            var mf = go.GetComponent<MeshFilter>();
                            if (mf != null && mf.sharedMesh != null)
                            {
                                int vertexCount = mf.sharedMesh.vertexCount;
                                // Log buildings with significant vertex counts (potential tall buildings)
                                if (vertexCount > 5000)
                                {
                                    Debug.Log($"[CreateGo IMMEDIATE] T7 building #{debugIdx}: {vertexCount} vertices in returned GameObject");
                                }
                            }
                        }
                    }

                    Debug.Log($"[TileLoad] {layerName}{tileIndexStr} {tile.Z}/{tile.X}/{tile.Y}: api.kilomeme.com ({apiTimeMs:F0}ms) → {layerData.FeatureCount()} {objectType} → {layerObjects.Count} {objectType}");

                    // DIAGNOSTIC: Check if GoModifiers (MaterialModifier) actually applied materials
                    if (layerObjects.Count > 0)
                    {
                        int withRenderer = 0, withMaterials = 0, totalMats = 0, withMesh = 0, totalVerts = 0;
                        foreach (var obj in layerObjects)
                        {
                            var mr = obj.GetComponent<MeshRenderer>();
                            if (mr != null) { withRenderer++; if (mr.sharedMaterials.Length > 0 && mr.sharedMaterials[0] != null) { withMaterials++; totalMats += mr.sharedMaterials.Length; } }
                            var mf = obj.GetComponent<MeshFilter>();
                            if (mf != null && mf.sharedMesh != null && mf.sharedMesh.vertexCount > 0) { withMesh++; totalVerts += mf.sharedMesh.vertexCount; }
                        }
                        string matName = "";
                        if (withMaterials > 0)
                        {
                            var firstMr = layerObjects[0].GetComponent<MeshRenderer>();
                            if (firstMr != null && firstMr.sharedMaterial != null) matName = $" mat='{firstMr.sharedMaterial.name}' shader='{firstMr.sharedMaterial.shader?.name ?? "NULL"}'";
                        }
                        Debug.Log($"[TileLoad] MATERIAL CHECK {sourceLayer}: {withRenderer}/{layerObjects.Count} MeshRenderer, {withMaterials} with materials ({totalMats} slots), {withMesh} with mesh ({totalVerts} total verts){matName}");
                    }

                    int goIndex = 0;

                    // Build a list of features for metadata assignment
                    List<VectorFeatureUnity> features = new List<VectorFeatureUnity>();
                    foreach (var stackPair in meshDataDict)
                    {
                        foreach (var md in stackPair.Value)
                        {
                            if (md.Feature != null)
                            {
                                features.Add(md.Feature);
                            }
                        }
                    }

                    foreach (var go in layerObjects)
                    {
                        // Add BuildingMetadata component for buildings
                        if (sourceLayer == "building" && goIndex < features.Count)
                        {
                            var metadata = go.AddComponent<BuildingMetadata>();
                            metadata.Initialize(features[goIndex].Properties, tileKey, tileIndex, goIndex);

                            // Use building name in GameObject name if available
                            string displayName = !string.IsNullOrEmpty(metadata.buildingName)
                                ? $"_({metadata.buildingName})"
                                : "";
                            go.name = tileIndex >= 0
                                ? $"Overture_{sourceLayer}_T{tileIndex}_{goIndex}{displayName}"
                                : $"Overture_{sourceLayer}_{goIndex}{displayName}";
                        }
                        else
                        {
                            // Non-building layers just get numbered names
                            go.name = tileIndex >= 0
                                ? $"Overture_{sourceLayer}_T{tileIndex}_{goIndex}"
                                : $"Overture_{sourceLayer}_{goIndex}";
                        }

                        goIndex++;
                        go.SetActive(true);
                        createdObjects.Add(go);

                        // Cache Y offset for this object based on its layer
                        if (visualizerKey != null && _layerYOffsets.TryGetValue(visualizerKey, out float yOff))
                        {
                            _objectYOffset[go.transform] = yOff;
                        }
                    }
                }
                else
                {
                    // No mesh geometry (e.g., POI labels which are created by POILabelModifier)
                    string objectType = sourceLayer == "place" ? "POIs" : "objects";
                    Debug.Log($"[TileLoad] {layerName}{tileIndexStr} {tile.Z}/{tile.X}/{tile.Y}: api.kilomeme.com ({apiTimeMs:F0}ms) → {layerData.FeatureCount()} {objectType} (no mesh, modifiers only)");
                }

                // Yield after each source layer to avoid blocking main thread too long
                yield return null;
            }

            // POI label anchors - name with tile index but DON'T add to tile frustum culling
            // POICanvasManager handles its own distance (1600m) and frustum culling
            // POILabelModifier removed (Mapbox dependency eliminated)

            // CHANGED: Append objects instead of overwriting
            if (!_tileObjects.ContainsKey(tileKey))
            {
                _tileObjects[tileKey] = new List<GameObject>();
            }
            _tileObjects[tileKey].AddRange(createdObjects);

            // Position tile objects in Web Mercator space (1 Unity unit = 1 meter)
            // Game coordinate system: player at (0,0,0), map center = player GPS
            // Mesh vertices are in normalized tile space (0-1), need scaling to meters
            PositionTileObjectsInMercator(tile, createdObjects, tileIndexStr);

            Debug.Log($"[TileLoad] {layerName}{tileIndexStr} {tile.Z}/{tile.X}/{tile.Y}: Rendering complete (spread across multiple frames to avoid lag)");
        }

        public void UpdateForView(TileId tile)
        {
            if (!_initialized) return;
            foreach (var visualizer in _visualizers.Values)
            {
                visualizer.UpdateForView(tile, _map.MapInformation);
            }
        }

        /// <summary>
        /// FLOATING ORIGIN: Re-position ALL loaded tiles relative to current map center.
        /// Called every frame so tiles slide when player moves (conveyor belt).
        /// Player stays at Unity (0,0,0), world moves around them.
        /// Only runs when the map center actually changes (performance optimization).
        /// </summary>
        public void UpdateAllTilePositions()
        {
            if (!_initialized || _tileObjects.Count == 0) return;

            // Current map center in Web Mercator
            var mapboxLatLng = _map.MapInformation.LatitudeLongitude;
            var mapCenterGPS = new LatitudeLongitude(mapboxLatLng.Latitude, mapboxLatLng.Longitude);
            Vector2d mapCenterMercator = Conversions.LatitudeLongitudeToWebMercator(mapCenterGPS);

            // Skip if map center hasn't moved (saves CPU when stationary)
            if (System.Math.Abs(mapCenterMercator.x - _lastMapCenterMercator.x) < 0.001 &&
                System.Math.Abs(mapCenterMercator.y - _lastMapCenterMercator.y) < 0.001)
                return;
            _lastMapCenterMercator = mapCenterMercator;

            foreach (var kvp in _tileObjects)
            {
                var objects = kvp.Value;
                if (objects == null || objects.Count == 0) continue;

                // Parse tile key "z/x/y" to get tile ID
                var parts = kvp.Key.Split('/');
                if (parts.Length != 3 ||
                    !int.TryParse(parts[0], out int z) ||
                    !int.TryParse(parts[1], out int x) ||
                    !int.TryParse(parts[2], out int y))
                    continue;

                // Tile NW corner in Web Mercator, relative to current map center
                var tileBoundsM = Conversions.TileBoundsInWebMercator(new TileId(z, x, y));
                float tileOriginX = (float)(tileBoundsM.minX - mapCenterMercator.x);
                float tileOriginZ = (float)(tileBoundsM.maxY - mapCenterMercator.y);
                float tileSize = (float)Conversions.TileEdgeSizeInMercator(z);

                foreach (var obj in objects)
                {
                    if (obj != null)
                    {
                        // Reposition ALL objects (including frustum-culled inactive ones)
                        // so they're correct when they become visible again
                        // Y offset per layer: water > ground, roads > water (prevents z-fighting)
                        float yOffset = _objectYOffset.TryGetValue(obj.transform, out float cachedY) ? cachedY : 0f;
                        obj.transform.localPosition = new Vector3(tileOriginX, yOffset, tileOriginZ);
                        obj.transform.localScale = new Vector3(tileSize, tileSize, tileSize);
                    }
                }
            }
        }

        /// <summary>
        /// Position tile objects in Web Mercator space (1 Unity unit = 1 meter).
        /// Mesh vertices are in normalized tile space (0-1 for X, 0 to -1 for Z).
        /// This method sets both position (tile NW corner) and scale (tile edge size in meters).
        /// 
        /// Coordinate mapping:
        ///   Mesh X: 0→1  = tile west→east  = Unity +X
        ///   Mesh Z: 0→-1 = tile north→south = Unity -Z  
        ///   Mesh Y: height in tile units → height in meters after scale
        /// </summary>
        private void PositionTileObjectsInMercator(TileId tile, List<GameObject> objects, string tileIndexStr = "")
        {
            if (objects == null || objects.Count == 0) return;

            // Tile bounds in Web Mercator (meters)
            var tileBoundsM = Conversions.TileBoundsInWebMercator(new TileId(tile.Z, tile.X, tile.Y));

            // Map center in Web Mercator
            var mapboxLatLng = _map.MapInformation.LatitudeLongitude;
            var mapCenterGPS = new LatitudeLongitude(mapboxLatLng.Latitude, mapboxLatLng.Longitude);
            Vector2d mapCenterMercator = Conversions.LatitudeLongitudeToWebMercator(mapCenterGPS);

            // Tile NW corner (origin for mesh vertices) relative to map center
            // Mesh (0,0,0) = tile top-left = NW corner
            // Mercator X → Unity X (east positive), Mercator Y → Unity Z (north positive)
            float tileOriginX = (float)(tileBoundsM.minX - mapCenterMercator.x);  // west edge
            float tileOriginZ = (float)(tileBoundsM.maxY - mapCenterMercator.y);  // north edge

            // Scale: converts 0-1 normalized tile space to Web Mercator meters
            float tileSize = (float)Conversions.TileEdgeSizeInMercator(tile.Z);

            int positionedCount = 0;
            foreach (var obj in objects)
            {
                if (obj != null)
                {
                    // Use per-object Y offset based on layer (water/road above ground to prevent z-fighting)
                    float yOffset = _objectYOffset.TryGetValue(obj.transform, out float y) ? y : 0f;
                    obj.transform.localPosition = new Vector3(tileOriginX, yOffset, tileOriginZ);
                    obj.transform.localScale = new Vector3(tileSize, tileSize, tileSize);
                    positionedCount++;
                }
            }

            Debug.Log($"[TileLoad]{tileIndexStr} {tile.Z}/{tile.X}/{tile.Y}: Positioned {positionedCount} objects at NW=({tileOriginX:F1}, {tileOriginZ:F1}) scale={tileSize:F1}m");
        }

        private struct TileBounds
        {
            public double North;
            public double South;
            public double East;
            public double West;
        }

        private TileBounds TileIdToBounds(TileId tile)
        {
            // Convert XYZ tile coordinates to GPS bounds
            int z = tile.Z;
            int x = tile.X;
            int y = tile.Y;
            int n = 1 << z; // 2^z

            double west = x / (double)n * 360.0 - 180.0;
            double east = (x + 1) / (double)n * 360.0 - 180.0;

            double northRad = System.Math.Atan(System.Math.Sinh(System.Math.PI * (1 - 2.0 * y / n)));
            double southRad = System.Math.Atan(System.Math.Sinh(System.Math.PI * (1 - 2.0 * (y + 1) / n)));
            double north = northRad * 180.0 / System.Math.PI;
            double south = southRad * 180.0 / System.Math.PI;

            return new TileBounds { North = north, South = south, East = east, West = west };
        }

        public void RemoveTile(TileId tile)
        {
            var tileKey = $"{tile.Z}/{tile.X}/{tile.Y}";

            if (!_initialized)
            {
                Debug.LogWarning($"[RemoveTile] {tileKey}: Renderer not initialized, cannot remove tile");
                return;
            }

            Debug.Log($"[RemoveTile] {tileKey}: Removing tile");

            // Deactivate objects immediately (visual feedback) but DO NOT DESTROY them manually
            // The visualizer handles object pooling and destruction logic via UnregisterTile
            if (_tileObjects.TryGetValue(tileKey, out var objects))
            {
                Debug.Log($"[RemoveTile] {tileKey}: Deactivating {objects.Count} objects");
                foreach (var go in objects)
                {
                    if (go != null)
                    {
                        go.SetActive(false);
                        _objectYOffset.Remove(go.transform);
                    }
                }
                _tileObjects.Remove(tileKey);
            }
            else
            {
                Debug.LogWarning($"[RemoveTile] {tileKey}: No objects found in _tileObjects dictionary");
            }

            // Clean up visibility tracking to prevent dictionary bloat
            _tileVisibility.Remove(tileKey);

            // This is the critical step: returns objects to pool or destroys them properly
            Debug.Log($"[RemoveTile] {tileKey}: Calling UnregisterTile on {_visualizers.Count} visualizers");
            foreach (var visualizer in _visualizers.Values)
            {
                visualizer.UnregisterTile(tile);
            }
        }

        public void ClearAllTiles()
        {
            if (!_initialized) return;

            // Iterate through all known keys and remove them properly
            // We must use a copy of keys since RemoveTile modifies the dictionary
            var keys = new List<string>(_tileObjects.Keys);
            int count = 0;
            
            foreach (var key in keys)
            {
                var parts = key.Split('/');
                if (parts.Length == 3 && 
                    int.TryParse(parts[0], out int z) && 
                    int.TryParse(parts[1], out int x) && 
                    int.TryParse(parts[2], out int y))
                {
                    RemoveTile(new TileId(z, x, y));
                    count++;
                }
            }
            
            // Just in case some were missed (bad key format?)
            foreach (var kvp in _tileObjects)
            {
                 foreach (var go in kvp.Value)
                 {
                     if (go != null) go.SetActive(false);
                 }
            }
            _tileObjects.Clear();
            Debug.Log($"[OvertureVectorRenderer] Cleared {count} tiles properly via UnregisterTile");
        }

        private Dictionary<string, bool> _tileVisibility = new Dictionary<string, bool>();
        private int _frustumCullFrame = 0;

        public void UpdateFrustumCulling()
        {
            if (!_initialized)
            {
                if (Time.frameCount % 300 == 0)
                    Debug.LogWarning("[Frustum] UpdateFrustumCulling: Renderer NOT initialized! _visualizers.Count=" + _visualizers.Count);
                return;
            }

            // Only run frustum culling every 10 frames to reduce overhead
            _frustumCullFrame++;
            if (_frustumCullFrame % 10 != 0) return;

            // Get main camera frustum planes
            Camera mainCamera = Camera.main;
            if (mainCamera == null) return;

            Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(mainCamera);

            // Debug: Log what's in _tileObjects every 5 seconds
            if (Time.frameCount % 300 == 0)
            {
                int totalObjects = 0;
                int labelAnchors = 0;
                foreach (var kvp in _tileObjects)
                {
                    if (kvp.Value != null)
                    {
                        totalObjects += kvp.Value.Count;
                        foreach (var go in kvp.Value)
                        {
                            if (go != null && go.name.Contains("LabelAnchor"))
                                labelAnchors++;
                        }
                    }
                }
                Debug.Log($"[Frustum] UpdateFrustumCulling running: {_tileObjects.Count} tiles, {totalObjects} total objects, {labelAnchors} LabelAnchors");
            }

            // Check each tile's visibility
            foreach (var kvp in _tileObjects)
            {
                string tileKey = kvp.Key;
                var objects = kvp.Value;
                if (objects == null || objects.Count == 0) continue;

                // Check if ANY object in this tile is visible
                bool tileVisible = false;
                foreach (var go in objects)
                {
                    if (go == null) continue;

                    // Check renderer bounds against frustum
                    // Must check even if inactive so we can reactivate when back in view!
                    var renderer = go.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        if (GeometryUtility.TestPlanesAABB(frustumPlanes, renderer.bounds))
                        {
                            tileVisible = true;
                            break; // At least one object visible, keep entire tile active
                        }
                    }
                    else
                    {
                        // For objects without renderers (e.g., POI label anchors), check if point is in frustum
                        Vector3 pos = go.transform.position;

                        // Debug first few POI positions every 5 seconds
                        if (Time.frameCount % 300 == 0 && go.name.Contains("LabelAnchor"))
                        {
                            Vector3 camPos = mainCamera.transform.position;
                            float dist = Vector3.Distance(camPos, pos);
                            Debug.Log($"[Frustum] Checking POI: {go.name} | Camera: ({camPos.x:F0}, {camPos.y:F0}, {camPos.z:F0}) | POI: ({pos.x:F0}, {pos.y:F0}, {pos.z:F0}) | Distance: {dist:F0}m");
                        }

                        // Test if point is inside all 6 frustum planes (proper frustum check)
                        bool insideFrustum = true;
                        foreach (var plane in frustumPlanes)
                        {
                            if (plane.GetDistanceToPoint(pos) < 0)
                            {
                                insideFrustum = false;
                                break;
                            }
                        }

                        if (insideFrustum)
                        {
                            tileVisible = true;
                            break;
                        }
                    }
                }

                // Only update if visibility changed
                if (!_tileVisibility.TryGetValue(tileKey, out bool wasVisible) || wasVisible != tileVisible)
                {
                    _tileVisibility[tileKey] = tileVisible;

                    // Activate/deactivate entire tile
                    foreach (var go in objects)
                    {
                        if (go != null)
                        {
                            go.SetActive(tileVisible);
                        }
                    }
                }
            }

            // Log stats every 5 seconds (300 frames at 60fps)
            if (Time.frameCount % 300 == 0)
            {
                // Count active/inactive objects per layer
                var layerStats = new Dictionary<string, (int active, int inactive)>();

                foreach (var kvp in _tileObjects)
                {
                    var objects = kvp.Value;
                    if (objects == null) continue;

                    foreach (var go in objects)
                    {
                        if (go == null) continue;

                        // Determine layer from object name
                        string layerName = "unknown";
                        if (go.name.Contains("building")) layerName = "building";
                        else if (go.name.Contains("road") || go.name.Contains("segment")) layerName = "road";
                        else if (go.name.Contains("place") || go.name.Contains("poi")) layerName = "place";
                        else if (go.name.Contains("water")) layerName = "water";
                        else if (go.name.Contains("LabelAnchor")) layerName = "labels";

                        if (!layerStats.ContainsKey(layerName))
                            layerStats[layerName] = (0, 0);

                        var stats = layerStats[layerName];
                        if (go.activeSelf)
                            stats.active++;
                        else
                            stats.inactive++;
                        layerStats[layerName] = stats;
                    }
                }

                // Log per-layer stats (in same format as POI labels)
                foreach (var kvp in layerStats)
                {
                    int total = kvp.Value.active + kvp.Value.inactive;
                    string layerDisplayName = kvp.Key switch
                    {
                        "building" => "Buildings",
                        "road" => "Roads",
                        "labels" => "POI Anchors",
                        "water" => "Water",
                        "place" => "Places",
                        _ => kvp.Key
                    };

                    // Count visible renderers for ALL layers
                    int visibleRenderers = 0;
                    string layerFilter = kvp.Key; // building, road, labels, water, place

                    foreach (var kvp2 in _tileObjects)
                    {
                        var objects = kvp2.Value;
                        if (objects == null) continue;

                        foreach (var go in objects)
                        {
                            if (go == null || !go.activeSelf) continue;

                            // Match layer type
                            bool isMatch = false;
                            if (layerFilter == "building" && go.name.Contains("building")) isMatch = true;
                            else if (layerFilter == "road" && (go.name.Contains("road") || go.name.Contains("segment"))) isMatch = true;
                            else if (layerFilter == "labels" && go.name.Contains("LabelAnchor")) isMatch = true;
                            else if (layerFilter == "water" && go.name.Contains("water")) isMatch = true;
                            else if (layerFilter == "place" && go.name.Contains("place")) isMatch = true;

                            if (!isMatch) continue;

                            var renderer = go.GetComponent<MeshRenderer>();
                            if (renderer != null && renderer.enabled)
                            {
                                visibleRenderers++;
                            }
                        }
                    }

                    float cullingEfficiency = total > 0 ? ((1f - visibleRenderers / (float)total) * 100f) : 0f;
                    Debug.Log($"[MEMORY PRESSURE] {layerDisplayName}: {total} in memory | {kvp.Value.inactive} tile-culled | {kvp.Value.active} active | {visibleRenderers} rendering | Culling: {cullingEfficiency:F1}%");
                }

                // Global mesh memory estimation
                long totalVerts = 0;
                long activeVerts = 0;
                int totalMeshObjects = 0;
                foreach (var kvp2 in _tileObjects)
                {
                    if (kvp2.Value == null) continue;
                    foreach (var go in kvp2.Value)
                    {
                        if (go == null) continue;
                        var mf = go.GetComponent<MeshFilter>();
                        if (mf != null && mf.sharedMesh != null)
                        {
                            int vc = mf.sharedMesh.vertexCount;
                            totalVerts += vc;
                            totalMeshObjects++;
                            if (go.activeInHierarchy) activeVerts += vc;
                        }
                    }
                }
                float totalMeshMB = (totalVerts * 80f) / (1024f * 1024f);
                float activeMeshMB = (activeVerts * 80f) / (1024f * 1024f);
                Debug.Log($"[MEMORY TOTAL] {totalMeshObjects} mesh objects | {totalVerts:N0} total verts (~{totalMeshMB:F1}MB) | {activeVerts:N0} active verts (~{activeMeshMB:F1}MB) | Tiles: {_tileObjects.Count} loaded, {_tileVisibility.Count(v => v.Value)} visible");
            }
        }

        private static string ResolveVisualizerKey(string sourceLayer)
        {
            return LayerMapping.TryGetValue(sourceLayer, out var mapped) ? mapped : sourceLayer;
        }

        /// <summary>Generate extruded line mesh for river centerlines (LINESTRING). Uses same approach as roads.</summary>
        private static void GenerateRiverLineMesh(
            VectorFeatureUnity feature,
            MeshData md,
            IMapInformation mapInfo)
        {
            if (feature.Points == null || feature.Points.Count == 0) return;

            // LINESTRING rivers = centerline only, no polygon. Assume skinny (no width in OSM/Overture).
            float widthMeters = 25f;
            if (feature.Properties != null && feature.Properties.TryGetValue("width", out var w) && w != null)
            {
                try
                {
                    float wf = Convert.ToSingle(w);
                    if (wf > 0 && wf < 500) widthMeters = wf;
                }
                catch { }
            }

            float latComp = mapInfo.GetLatitudeCompensationForLocation;
            double tileSize = Conversions.TileEdgeSizeInMercator(feature.TileId.Z);
            float metersToTile = 1.0f / ((float)tileSize * latComp);
            float widthInTile = widthMeters * metersToTile;
            float halfWidth = widthInTile * 0.5f;

            if (md.Triangles.Count == 0) md.Triangles.Add(new List<int>());
            if (md.UV.Count == 0) md.UV.Add(new List<Vector2>());
            Vector3 up = Vector3.up;

            foreach (var points in feature.Points)
            {
                if (points == null || points.Count < 2) continue;
                int startVertexIndex = md.Vertices.Count;

                for (int i = 0; i < points.Count; i++)
                {
                    Vector3 point = points[i];
                    Vector3 direction = i == 0 ? (points[i + 1] - points[i]).normalized
                        : i == points.Count - 1 ? (points[i] - points[i - 1]).normalized
                        : ((points[i + 1] - points[i - 1]) * 0.5f).normalized;
                    Vector3 perpendicular = Vector3.Cross(up, direction).normalized;

                    md.Vertices.Add(point - perpendicular * halfWidth);
                    md.Vertices.Add(point + perpendicular * halfWidth);
                    md.Normals.Add(up);
                    md.Normals.Add(up);
                    float t = (float)i / (points.Count - 1);
                    md.UV[0].Add(new Vector2(0, t));
                    md.UV[0].Add(new Vector2(1, t));
                }

                for (int i = 0; i < points.Count - 1; i++)
                {
                    int baseIndex = startVertexIndex + i * 2;
                    md.Triangles[0].Add(baseIndex);
                    md.Triangles[0].Add(baseIndex + 2);
                    md.Triangles[0].Add(baseIndex + 1);
                    md.Triangles[0].Add(baseIndex + 1);
                    md.Triangles[0].Add(baseIndex + 2);
                    md.Triangles[0].Add(baseIndex + 3);
                }
            }
        }
        
        /// <summary>Returns true if water feature is intermittent (dry washes, arroyos) - skip rendering in desert regions.</summary>
        private static bool IsIntermittentWater(Dictionary<string, object> properties)
        {
            if (properties == null || !properties.ContainsKey("is_intermittent")) return false;
            var val = properties["is_intermittent"];
            if (val == null) return false;
            if (val is bool b) return b;
            var s = val.ToString()?.ToLowerInvariant();
            return s == "true" || s == "1" || s == "yes";
        }

        /// <summary>Skip water classes that are often dry/irrigation in desert (canal, ditch, stream, drain).</summary>
        private static bool IsDesertWaterClass(Dictionary<string, object> properties)
        {
            if (properties == null) return false;
            var cls = GetWaterClass(properties);
            var sub = GetWaterSubtype(properties);
            return cls == "canal" || cls == "ditch" || cls == "stream" || cls == "drain" || cls == "irrigation"
                || sub == "canal" || sub == "ditch" || sub == "stream" || sub == "drain";
        }

        /// <summary>Skip ocean/sea - giant polygons, not relevant inland (El Paso).</summary>
        private static bool IsOceanOrSea(Dictionary<string, object> properties)
        {
            if (properties == null) return false;
            var cls = GetWaterClass(properties);
            var sub = GetWaterSubtype(properties);
            return cls == "ocean" || cls == "sea" || sub == "ocean" || sub == "sea";
        }

        /// <summary>
        /// Register a POI feature with TransmitterScanner for location beams/nearby panel.
        /// Replaces the old POILabelModifier pipeline.
        /// </summary>
        private void RegisterPOIWithScanner(VectorFeatureUnity feature, TileId tile)
        {
            if (TransmitterScanner.Instance == null) return;

            // Extract name
            string poiName = "Unknown";
            if (feature.Properties.ContainsKey("names"))
            {
                try
                {
                    string namesJson = feature.Properties["names"].ToString();
                    // Simple JSON parse for {"primary":"..."}
                    int idx = namesJson.IndexOf("\"primary\":\"");
                    if (idx >= 0)
                    {
                        idx += 11;
                        int end = namesJson.IndexOf("\"", idx);
                        if (end > idx) poiName = namesJson.Substring(idx, end - idx);
                    }
                }
                catch { }
            }
            if (poiName == "Unknown" && feature.Properties.ContainsKey("name"))
                poiName = feature.Properties["name"].ToString();

            // Extract category
            string primaryCategory = "";
            string mainGroup = "other";
            if (feature.Properties.ContainsKey("categories"))
            {
                try
                {
                    string catJson = feature.Properties["categories"].ToString();
                    int idx = catJson.IndexOf("\"primary\":\"");
                    if (idx >= 0)
                    {
                        idx += 11;
                        int end = catJson.IndexOf("\"", idx);
                        if (end > idx) primaryCategory = catJson.Substring(idx, end - idx);
                    }
                }
                catch { }
            }

            // Map to main category group (same logic as old POILabelModifier)
            if (primaryCategory.Contains("cafe") || primaryCategory.Contains("coffee") || primaryCategory.Contains("tea"))
                mainGroup = "coffee";
            else if (primaryCategory.Contains("bar") || primaryCategory.Contains("pub") || primaryCategory.Contains("brew"))
                mainGroup = "bar";
            else if (primaryCategory.Contains("restaurant") || primaryCategory.Contains("food") || primaryCategory.Contains("pizza") ||
                     primaryCategory.Contains("burger") || primaryCategory.Contains("diner") || primaryCategory.Contains("bakery"))
                mainGroup = "food";
            else if (primaryCategory.Contains("convenience") || primaryCategory.Contains("gas_station"))
                mainGroup = "convenience";

            // Calculate GPS from tile coordinates
            Vector2d latLon = new Vector2d();
            if (feature.Points != null && feature.Points.Count > 0 && feature.Points[0].Count > 0)
            {
                var point = feature.Points[0][0];
                double n = System.Math.Pow(2, tile.Z);
                double lon = (tile.X + point.x) / n * 360.0 - 180.0;
                double latRad = System.Math.Atan(System.Math.Sinh(System.Math.PI * (1 - 2 * (tile.Y + point.z) / n)));
                double lat = latRad * 180.0 / System.Math.PI;
                latLon = new Vector2d(lat, lon);
            }

            TransmitterScanner.Instance.RegisterTransmitter(poiName, primaryCategory, mainGroup, latLon);
        }

        private static string GetWaterClass(Dictionary<string, object> properties)
        {
            if (properties == null) return "";
            if (!properties.TryGetValue("class", out var v) || v == null) return "";
            return v.ToString()?.ToLowerInvariant() ?? "";
        }

        private static string GetWaterSubtype(Dictionary<string, object> properties)
        {
            if (properties == null) return "";
            if (!properties.TryGetValue("subtype", out var v) || v == null) return "";
            return v.ToString()?.ToLowerInvariant() ?? "";
        }
    }

    // Simplified Decoder - Keeps GZIP/Deflate support just in case
    internal static class PMTilesTileDecoder
    {
        public static bool TryParseVectorTile(byte[] payload, out MapboxVectorTile vectorTile)
        {
            vectorTile = null;
            if (payload == null || payload.Length == 0) return false;

            // 1. Try Raw
            if (TryParse(payload, out vectorTile)) return true;

            // 2. Try Gzip (Magic Bytes 1f 8b)
            if (payload.Length > 2 && payload[0] == 0x1f && payload[1] == 0x8b)
            {
                var decompressed = DecompressGzip(payload);
                if (decompressed != null && TryParse(decompressed, out vectorTile)) return true;
            }

            // 3. Try Deflate (No Header) - common in PMTiles but rare in web transport
            // Skipping for now unless needed.
            
            return false;
        }

        private static bool TryParse(byte[] data, out MapboxVectorTile tile)
        {
            tile = null;
            try {
                var dummyId = new global::Mapbox.BaseModule.Data.Tiles.CanonicalTileId(0, 0, 0);
                tile = new MapboxVectorTile(dummyId, "overture");
                // ByteData is read-only, set private field via reflection
                var field = typeof(global::Mapbox.BaseModule.Data.Tiles.ByteArrayTile)
                    .GetField("byteData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                field?.SetValue(tile, data);
                // Force parse by accessing Data property (triggers VectorTileReader creation)
                var dataField = typeof(global::Mapbox.BaseModule.Data.Tiles.ByteArrayTile)
                    .GetProperty("Data", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                dataField?.GetValue(tile);
                return tile.LayerNames()?.Count > 0;
            } catch { return false; }
        }

        private static byte[] DecompressGzip(byte[] data)
        {
            try {
                using var input = new MemoryStream(data);
                using var gzip = new GZipInputStream(input);
                using var output = new MemoryStream();
                gzip.CopyTo(output);
                return output.ToArray();
            } catch { return null; }
        }
    }
}
