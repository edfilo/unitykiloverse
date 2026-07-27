using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
// Mapbox SDK types used for scene component lookup, tile parsing, and visualizer calls
using MapboxVectorTile = global::Mapbox.BaseModule.Data.Tiles.VectorTile;
using MapboxVLMS = global::Mapbox.VectorModule.Unity.VectorLayerModuleScript;
using MapboxVLVO = global::Mapbox.VectorModule.Unity.VectorLayerVisualizerObject;
using MapboxIVLV = global::Mapbox.VectorModule.IVectorLayerVisualizer;
using MapboxVLV = global::Mapbox.VectorModule.VectorLayerVisualizer;
using MapboxModStack = global::Mapbox.VectorModule.MeshGeneration.ModifierStack;
using MapboxTileId = global::Mapbox.BaseModule.Data.Tiles.CanonicalTileId;
using MapboxMeshData = global::Mapbox.BaseModule.Data.MeshData;
using MapboxIMapInfo = global::Mapbox.BaseModule.Map.IMapInformation;
using MapboxFeature = global::Mapbox.BaseModule.Utilities.VectorFeatureUnity;
using UnityEngine;
using UnityEngine.Networking;
using ICSharpCode.SharpZipLib.GZip;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Kiloverse.Mapbox
{
    internal static class K1L0RuntimeDiagnostics
    {
        // Unity's non-development iOS player still executes Debug.Log calls.
        // Keep verbose map traces in Editor/development builds, while Release
        // retains warnings, errors, and the dedicated tile-stream event file.
        public static bool VerboseMapLogs => Application.isEditor || Debug.isDebugBuild;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ConfigurePlayerLogging()
        {
            if (VerboseMapLogs) return;
            Debug.unityLogger.filterLogType = LogType.Warning;
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
        }

        public static void MapLog(string message)
        {
            if (VerboseMapLogs) Debug.Log(message);
        }
    }

    internal static class K1L0TileStreamLog
    {
        private static StreamWriter writer;
        private static readonly object Gate = new object();
        public static string Path { get; private set; } = "";

        public static void Begin()
        {
            lock (Gate)
            {
                End();
                try
                {
                    string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
                    Path = System.IO.Path.Combine(Application.persistentDataPath, $"k1l0-tile-stream-{stamp}.log");
                    writer = new StreamWriter(Path, false) { AutoFlush = true };
                    Write("SESSION", $"platform={Application.platform} version={Application.version} unity={Application.unityVersion}");
                    Debug.Log($"[TileStream] Session log: {Path}");
                }
                catch (Exception ex)
                {
                    writer = null;
                    Debug.LogWarning($"[TileStream] Could not open session log: {ex.Message}");
                }
            }
        }

        public static void Write(string phase, string detail)
        {
            lock (Gate)
            {
                if (writer == null) return;
                writer.WriteLine($"{DateTime.UtcNow:O}\t{Time.realtimeSinceStartup:F3}\t{phase}\t{detail}");
            }
        }

        public static void End()
        {
            lock (Gate)
            {
                if (writer == null) return;
                try { writer.Dispose(); } catch { }
                writer = null;
            }
        }
    }

    /// <summary>
    /// Orchestrates the Overture maps using the server-side /xyz/ tile proxy.
    /// Replaces the complex client-side PMTiles reader with simple HTTP requests.
    /// </summary>
    public class OvertureMapManager : MonoBehaviour
    {
        private void Awake()
        {
            K1L0TileStreamLog.Begin();
            Debug.Log("[OvertureMapManager] ===== AWAKE CALLED =====");
            Debug.Log($"[OvertureMapManager] Component enabled: {enabled}");
            Debug.Log($"[OvertureMapManager] GameObject active: {gameObject.activeInHierarchy}");
        }

        // On-screen debug overlay
        public static string _debugStatus = "INIT";
        public static int _tilesFetched = 0;
        public static int _tilesRendered = 0;
        public static string _lastTileUrl = "";
        public static string _lastTileResult = "";
        public static string _buildingCenterTile = "";
        public static string _buildingLoadedTiles = "";
        public static int _buildingLoadedCount = 0;
        public static int _buildingRequestingCount = 0;
        public static string _lastBuildingRender = "";
        public static int _buildingDetailedCount = 0;
        public static int _buildingSimpleCount = 0;
        public static int _buildingShellCount = 0;
        public static int _buildingLoadedVertices = 0;
        public static int _buildingVisibleVertices = 0;
        public static int _buildingVisibleRenderers = 0;
        public static int _buildingTinyRenderers = 0;
        public static int _buildingTinyVertices = 0;
        public static int _buildingFarRenderers = 0;

        public static string RenderDebugJson()
        {
            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "\"render\":{{\"tilesFetched\":{0},\"tilesRendered\":{1},\"lastTile\":\"{2}\",\"lastTileResult\":\"{3}\",\"buildingCenter\":\"{4}\",\"buildingLoaded\":{5},\"buildingRequesting\":{6},\"buildingTiles\":\"{7}\",\"lastBuilding\":\"{8}\",\"buildingDetailed\":{9},\"buildingSimple\":{10},\"buildingShell\":{11},\"buildingVertices\":{12},\"buildingVisibleVertices\":{13},\"activeSignals\":{14},\"spawnedBeams\":{15},\"visibleBeams\":{16},\"buildingVisibleRenderers\":{17},\"buildingTinyRenderers\":{18},\"buildingTinyVertices\":{19},\"buildingFarRenderers\":{20}}}",
                _tilesFetched,
                _tilesRendered,
                EscapeJson(_lastTileUrl),
                EscapeJson(_lastTileResult),
                EscapeJson(_buildingCenterTile),
                _buildingLoadedCount,
                _buildingRequestingCount,
                EscapeJson(_buildingLoadedTiles),
                EscapeJson(_lastBuildingRender),
                _buildingDetailedCount,
                _buildingSimpleCount,
                _buildingShellCount,
                _buildingLoadedVertices,
                _buildingVisibleVertices,
                SignalBeamBridge.ActiveSignalCount,
                SignalBeamBridge.SpawnedBeamCount,
                SignalBeamBridge.VisibleBeamCount,
                _buildingVisibleRenderers,
                _buildingTinyRenderers,
                _buildingTinyVertices,
                _buildingFarRenderers
            );
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private void OnGUI()
        {
        }

        private void OnDisable()
        {
            Debug.LogError("[OvertureMapManager] ❌ COMPONENT WAS DISABLED!");
#if UNITY_EDITOR
            EditorApplication.update -= EditorTick;
#endif
        }

        private void OnDestroy()
        {
            Debug.LogError("[OvertureMapManager] ❌ COMPONENT WAS DESTROYED!");
#if UNITY_EDITOR
            EditorApplication.update -= EditorTick;
#endif
            K1L0TileStreamLog.End();
        }

#if UNITY_EDITOR
        /// <summary>
        /// EditorApplication.update callback — replaces MonoBehaviour.Update() in editor.
        /// Update() mysteriously stops firing after frame 2 on this component (other MBs unaffected).
        /// </summary>
        private void EditorTick()
        {
            if (!Application.isPlaying) return;
            if (this == null || !enabled || !gameObject.activeInHierarchy) return;
            DoUpdate();
        }
#endif

        [Header("Map")]
        [SerializeField] private KiloverseMapInfo _map;
        public KiloverseMapInfo map => _map;
        [SerializeField] private Camera playerCamera; // For frustum culling
        [SerializeField] private int zoomLevel = 14; // Overture 2025-01-22 maxZoom is 14
        [SerializeField] private float tilePollInterval = 1f;
        [SerializeField] private float buildingLodRefreshMeters = 40f;
        [SerializeField] private bool logOvertureLayers = true;

        [Header("Visualizers (ScriptableObjects)")]
        [SerializeField] private MapboxIVLV buildingVisualizer;
        [SerializeField] private MapboxIVLV roadVisualizer;
        [SerializeField] private MapboxIVLV poiVisualizer;
        [SerializeField] private MapboxIVLV waterVisualizer;

        private XYZTileFetcher m_BuildingsFetcher;
        private XYZTileFetcher m_TransportationFetcher;
        private XYZTileFetcher m_BaseFetcher;

        private readonly List<XYZLayer> m_Layers = new List<XYZLayer>();
        private OvertureVectorRenderer m_Renderer;
        private float m_LastPoll;
        private Vector2d? m_LastTilePollCenter;
        private Vector3 m_LastTilePollCameraForward;
        private bool m_HasLastTilePollCameraForward;
        private Vector2d? m_LastBuildingLodCenter;
        private Vector2d? m_LastObservedPlayerCenter;
        private float m_LastPlayerMovementAt;
        private readonly Queue<TileId> m_BuildingLodRefreshQueue = new Queue<TileId>();
        private const float BuildingLodSettleSeconds = 1.25f;
        private const float BuildingLodMinimumRefreshMeters = 100f;
        private const double TilePollMovementThresholdMeters = 3d;
        private const float TilePollHeadingThresholdDegrees = 8f;
        [Header("Editor Safety")]
        [SerializeField] private int editorWarmupFrames = 10;
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
        // Coroutines don't resume on this MonoBehaviour in editor (unknown cause).
        // Run init synchronously since AllowMap/GPSReady are already true at frame 1.
        SynchronousInitInEditor();
        // WORKAROUND: MonoBehaviour.Update() stops firing after frame 2 on this component
        // (cause unknown — other MBs on same GO work fine). Use EditorApplication.update instead.
        EditorApplication.update -= EditorTick;
        EditorApplication.update += EditorTick;
        Debug.Log("[OvertureMapManager] Registered EditorApplication.update callback");
#else
        StartCoroutine(InitializeAfterGPS());
#endif
    }

    [Header("Editor: set true to skip map/tiles (faster boot, no map)")]
    [SerializeField] private bool editorSkipMapInit = false;

    private void SynchronousInitInEditor()
    {
        Debug.Log("[OvertureMapManager] SynchronousInitInEditor ENTERED");

        if (_map == null)
            _map = FindObjectOfType<KiloverseMapInfo>();
        if (_map == null)
        {
            Debug.LogError("[OvertureMapManager] ❌ KiloverseMapInfo not found!");
            enabled = false;
            return;
        }

        CreateTileFetchers(ResolveTileBaseURLImmediate());
        Debug.Log("[OvertureMapManager] Tile fetchers created");

        var vectorLayerModule = FindFirstObjectByType<MapboxVLMS>();
        _debugStatus = $"VLMS: {(vectorLayerModule != null ? "FOUND" : "NULL")}";

        if (vectorLayerModule != null)
        {
            Debug.Log($"[OvertureMapManager] Found VectorLayerModuleScript on: {vectorLayerModule.gameObject.name}");
            var layerVisListField = typeof(MapboxVLMS).GetField("_layerVisualizers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (layerVisListField != null)
            {
                var visualizerList = layerVisListField.GetValue(vectorLayerModule) as System.Collections.IList;
                if (visualizerList != null)
                {
                    Debug.Log($"[OvertureMapManager] Found {visualizerList.Count} visualizers");
                    foreach (var item in visualizerList)
                    {
                        try
                        {
                            var visualizerObject = item as MapboxVLVO;
                            if (visualizerObject != null)
                            {
                                var unityContext = new global::Mapbox.BaseModule.Unity.UnityContext();
                                var mapboxMapInfo = new MapboxMapInfoAdapter(_map);
                                var visualizer = visualizerObject.ConstructLayerVisualizer(mapboxMapInfo, unityContext);
                                Debug.Log($"[OvertureMapManager] ✓ Constructed '{visualizerObject.name}': {visualizer != null}");

                                if (visualizer != null)
                                {
                                    var visualizerImpl = visualizer as MapboxVLV;
                                    if (visualizerImpl != null)
                                    {
                                        var stacks = visualizerImpl.GetModStacks;
                                        Debug.Log($"[OvertureMapManager] ✓ '{visualizerObject.name}' has {stacks?.Count ?? 0} modifier stacks");
                                    }

                                    string visualizerName = visualizerObject.name.ToLower();
                                    if (visualizerName.Contains("building")) buildingVisualizer = visualizer;
                                    else if (visualizerName.Contains("road")) roadVisualizer = visualizer;
                                    else if (visualizerName.Contains("poi") || visualizerName.Contains("place")) poiVisualizer = visualizer;
                                    else if (visualizerName.Contains("water")) waterVisualizer = visualizer;
                                    Debug.Log($"[OvertureMapManager] ✓ Registered: {visualizerObject.name}");
                                }
                            }
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogError($"[OvertureMapManager] Exception: {ex.Message}\n{ex.StackTrace}");
                        }
                    }
                }
                else Debug.LogError("[OvertureMapManager] _layerVisualizers field is null!");
            }
            else Debug.LogError("[OvertureMapManager] Could not find _layerVisualizers field!");
        }
        else Debug.LogError("[OvertureMapManager] VectorLayerModuleScript not found!");

        _debugStatus = $"VIS: B={buildingVisualizer != null} R={roadVisualizer != null} P={poiVisualizer != null} W={waterVisualizer != null}";
        Debug.Log($"[OvertureMapManager] Visualizers: Building={buildingVisualizer != null}, Road={roadVisualizer != null}, POI={poiVisualizer != null}, Water={waterVisualizer != null}");

        var visualizers = new Dictionary<string, MapboxIVLV>();
        if (buildingVisualizer != null) visualizers["building"] = buildingVisualizer;
        if (roadVisualizer != null) visualizers["road"] = roadVisualizer;
        if (poiVisualizer != null) visualizers["poi_label"] = poiVisualizer;
        if (waterVisualizer != null) visualizers["water"] = waterVisualizer;

        m_Renderer = new OvertureVectorRenderer(_map, visualizers, logOvertureLayers);

        // Inline InitializeWhenReady (coroutines don't work on this MB in editor)
        Debug.Log("[OvertureMapManager] Initializing KiloverseMapbox custom SDK...");
        m_Renderer.InitializeVisualizers();
        RegisterLayers();
        Debug.Log($"[OvertureMapManager] ✓ Initialization complete. Layers: {m_Layers.Count}");
        BootDiagnostics.Mark("OvertureMapManager sync init done");
    }

    private IEnumerator InitializeAfterGPS()
    {
        BootDiagnostics.Mark("Overture wait GPS");
        Debug.Log("[OvertureMapManager] Waiting for GPS...");
        _debugStatus = "WAITING AllowMap";

        while (!BootState.AllowMap)
        {
            yield return null;
        }
        BootDiagnostics.Mark("Overture map allowed");
        _debugStatus = "WAITING GPS";

        float gpsWaitStart = Time.realtimeSinceStartup;
        while (!GPSLocationController.GPSReady && Time.realtimeSinceStartup - gpsWaitStart < 10f)
        {
            yield return null;
        }

        if (GPSLocationController.GPSReady)
        {
            _debugStatus = "GPS READY";
            BootDiagnostics.Mark("Overture GPS ready");
        }
        else
        {
            _debugStatus = "GPS TIMEOUT; INIT MAP";
            BootDiagnostics.Mark("Overture GPS timeout; continuing");
            Debug.LogWarning("[OvertureMapManager] GPS not ready after 10s; initializing tiles from current map/profile position.");
        }
#if UNITY_EDITOR
        // Yield a few frames to let BootSequence settle before heavy Find/Construct
        for (int i = 0; i < 5; i++) yield return null;
        BootDiagnostics.Mark("Overture after editor yield");
#endif
        Debug.Log("[OvertureMapManager] ✓ Initializing Overture tiles...");

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

        string tileBaseURL = null;
        yield return ResolveTileBaseURL(url => tileBaseURL = url);
        CreateTileFetchers(tileBaseURL);

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
        var vectorLayerModule = FindFirstObjectByType<MapboxVLMS>();
        if (Application.isEditor)
            BootDiagnostics.Mark("Overture after Find VectorLayerModule");
        yield return null;

        _debugStatus = $"VLMS: {(vectorLayerModule != null ? "FOUND" : "NULL")}";
        if (vectorLayerModule != null)
        {
            Debug.Log($"[OvertureMapManager] Found VectorLayerModuleScript on GameObject: {vectorLayerModule.gameObject.name}");

            // Get the _layerVisualizers field (it's a List<VectorLayerVisualizerObject>)
            var layerVisListField = typeof(MapboxVLMS).GetField("_layerVisualizers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

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
                            var visualizerObject = item as MapboxVLVO;
                            Debug.Log($"[OvertureMapManager] Cast result: {visualizerObject != null}");

                            if (visualizerObject != null)
                            {
                                Debug.Log($"[OvertureMapManager] Visualizer object name: {visualizerObject.name}");

                                // CRITICAL FIX: Call ConstructLayerVisualizer() to properly initialize the visualizer WITH modifier stacks
                                // The _layerVisualizer field is null until this method is called
                                // This method creates the visualizer and adds all modifier stacks from _modifierStackObjects
                                MapboxIVLV visualizer = null;
                                try
                                {
                                    // Create Mapbox types required by ConstructLayerVisualizer
                                    var unityContext = new global::Mapbox.BaseModule.Unity.UnityContext();
                                    var mapboxMapInfo = new MapboxMapInfoAdapter(_map);

                                    if (Application.isEditor)
                                        BootDiagnostics.Mark($"Overture before Construct {visualizerObject.name}");
                                    visualizer = visualizerObject.ConstructLayerVisualizer(mapboxMapInfo, unityContext);
                                    if (Application.isEditor)
                                        BootDiagnostics.Mark($"Overture after Construct {visualizerObject.name}");
                                    Debug.Log($"[OvertureMapManager] ✓ Constructed visualizer '{visualizerObject.name}': {visualizer != null}");

                                    // Verify modifier stacks were added
                                    if (visualizer != null)
                                    {
                                        var visualizerImpl = visualizer as MapboxVLV;
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

        _debugStatus = $"VIS: B={buildingVisualizer != null} R={roadVisualizer != null} P={poiVisualizer != null} W={waterVisualizer != null}";
        Debug.Log($"[OvertureMapManager] Loaded visualizers: Building={buildingVisualizer != null}, Road={roadVisualizer != null}, POI={poiVisualizer != null}, Water={waterVisualizer != null}");
        yield return null;

        // Create visualizer dictionary
        var visualizers = new Dictionary<string, MapboxIVLV>();
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
#if !UNITY_EDITOR
            DoUpdate();
#endif
        }

private int _doUpdateCallCount;
private void DoUpdate()
        {
            _doUpdateCallCount++;
            if (_doUpdateCallCount <= 3) Debug.Log($"[OvertureMapManager] DoUpdate #{_doUpdateCallCount} f={Time.frameCount} enabled={enabled} active={gameObject.activeInHierarchy}");
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
            if (!_loggedEditorSkipMapInitHint && editorSkipMapInit && _doUpdateCallCount > 60)
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

            // Editor: use call count instead of frameCount for warmup (EditorApplication.update doesn't advance frames)
            if (Application.isEditor)
            {
                if (_doUpdateCallCount < editorWarmupFrames)
                {
                    return;
                }
            }

            if (_doUpdateCallCount == 130) Debug.Log($"[OvertureMapManager] tick130: GPS={GPSLocationController.GPSReady} m_Layers={m_Layers?.Count} m_Renderer={m_Renderer != null} PostBoot={BootState.PostBootGracePeriodElapsed}");

            // Do not block tile loading forever on GPS. Boot/Teleport can seed the map
            // from a profile fallback, and Overture still needs to draw from that position.
            if (!GPSLocationController.GPSReady && _map != null && !_map.HasGPSPosition)
            {
                if (_doUpdateCallCount % 300 == 0) // Log periodically
                {
                    Debug.Log("[OvertureMapManager] Waiting for map position before loading tiles...");
                }
                return;
            }

            // While boot is still "waiting for tiles", do minimal work so we don't lock up the frame
            // NOTE: Use realtimeSinceStartup instead of Time.time — in editor, EditorApplication.update
            // doesn't advance Time.time (it stays frozen at the frame 1 value).
            float now = Time.realtimeSinceStartup;
            bool waitingForFirstTiles = !BootState.FirstTilesLoaded;
            if (!waitingForFirstTiles && _map != null && m_Renderer != null)
            {
                // Keep floating-origin movement smooth. The renderer itself
                // returns immediately when the map center has not changed.
                m_Renderer.UpdateAllTilePositions();
            }

            // Initial streaming and queued LOD work keep polling until every
            // desired tile is resolved. Once settled, do no tile/frustum work
            // while the player and camera remain still.
            if (!ShouldPollMapTiles(waitingForFirstTiles)) return;
            float effectivePollInterval = waitingForFirstTiles ? 2f : tilePollInterval;
            if (now - m_LastPoll < effectivePollInterval) return;
            m_LastPoll = now;

            // Skip heavy debug block while waiting for first tiles
            if (waitingForFirstTiles && _doUpdateCallCount % 300 == 0)
            {
                BootDiagnostics.Mark("OvertureMapManager polling for first tile");
            }
            if (K1L0RuntimeDiagnostics.VerboseMapLogs &&
                !waitingForFirstTiles && _doUpdateCallCount % 300 == 0)
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
            RecordMapTilePollState();

            // Frustum culling - skip while waiting for first tiles (nothing to cull yet)
            if (waitingForFirstTiles) { /* skip */ }
            else if (m_Renderer == null)
            {
                if (_doUpdateCallCount % 300 == 0)
                    Debug.LogError("[OvertureMapManager] m_Renderer is NULL! Cannot run frustum culling!");
            }
            else
            {
                m_Renderer.UpdateFrustumCulling();
            }
        }

        private bool ShouldPollMapTiles(bool waitingForFirstTiles)
        {
            if (waitingForFirstTiles || m_BuildingLodRefreshQueue.Count > 0) return true;
            if (m_Layers.Any(layer => !layer.IsSettled)) return true;
            if (_map == null || _map.MapInformation == null || !m_LastTilePollCenter.HasValue) return true;

            var mapCenter = _map.MapInformation.LatitudeLongitude;
            Vector2d current = Conversions.LatitudeLongitudeToWebMercator(
                new LatitudeLongitude(mapCenter.Latitude, mapCenter.Longitude));
            double dx = current.x - m_LastTilePollCenter.Value.x;
            double dy = current.y - m_LastTilePollCenter.Value.y;
            if (System.Math.Sqrt(dx * dx + dy * dy) >= TilePollMovementThresholdMeters) return true;

            if (playerCamera == null) playerCamera = Camera.main;
            return playerCamera != null &&
                   (!m_HasLastTilePollCameraForward ||
                    Vector3.Angle(m_LastTilePollCameraForward, playerCamera.transform.forward) >=
                    TilePollHeadingThresholdDegrees);
        }

        private void RecordMapTilePollState()
        {
            if (_map != null && _map.MapInformation != null)
            {
                var mapCenter = _map.MapInformation.LatitudeLongitude;
                m_LastTilePollCenter = Conversions.LatitudeLongitudeToWebMercator(
                    new LatitudeLongitude(mapCenter.Latitude, mapCenter.Longitude));
            }

            if (playerCamera == null) playerCamera = Camera.main;
            if (playerCamera == null) return;
            m_LastTilePollCameraForward = playerCamera.transform.forward;
            m_HasLastTilePollCameraForward = true;
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
            Debug.Log($"[OvertureMapManager] Registered {m_Layers.Count} XYZ layers.");
        }

        public void ClearLoadedTilesForLocationJump()
        {
            Debug.Log("[OvertureMapManager] Clearing loaded tiles for native location jump.");
            m_BuildingLodRefreshQueue.Clear();
            m_LastTilePollCenter = null;
            m_HasLastTilePollCameraForward = false;
            m_LastBuildingLodCenter = null;
            m_LastObservedPlayerCenter = null;
            m_Renderer?.ClearAllTiles();
            m_Renderer?.PurgePooledEntities();
            foreach (var layer in m_Layers)
            {
                layer.ClearLoadedState();
            }
            StartCoroutine(ReclaimLocationJumpMemory());
        }

        private IEnumerator ReclaimLocationJumpMemory()
        {
            // Let Destroy() retire purged meshes/GameObjects before asking Unity
            // to release now-unreferenced assets and managed wrappers.
            yield return null;
            yield return null;
            yield return Resources.UnloadUnusedAssets();
            System.GC.Collect();
            Debug.Log("[OvertureMapManager] Location-jump memory reclamation complete.");
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
                    K1L0RuntimeDiagnostics.MapLog($"[FRUSTUM-CENTER] z{zoom}/{tileX}/{tileY} Unity=({tileCenterUnity.x:F1},{tileCenterUnity.y:F1},{tileCenterUnity.z:F1}) Size=({tileWidthUnity:F1}x{tileHeightUnity:F1}) InFrustum={inFrustum}");
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
            K1L0RuntimeDiagnostics.MapLog($"[FRUSTUM] z{zoom}: {visibleTiles.Count} tiles visible (checked {checkedCount})");
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
                    if (timeSinceAllowPlayer < 5f && _doUpdateCallCount % 120 != 0) // Only search every ~2 seconds for first 5 seconds
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
                if (_doUpdateCallCount % 60 == 0)
                    K1L0RuntimeDiagnostics.MapLog($"[OvertureMapManager] No player yet - using map center for tiles: ({playerLatLon.Latitude:F4}, {playerLatLon.Longitude:F4})");
            }

            RefreshBuildingLodsIfNeeded(playerLatLon);

            // Debug current position periodically
            if (K1L0RuntimeDiagnostics.VerboseMapLogs &&
                _doUpdateCallCount % 300 == 0 && playerController != null)
            {
                Vector3 playerWorldPos = playerController.transform.position;
                Debug.Log($"[TileLoad] Player GPS: ({playerLatLon.Latitude:F6}, {playerLatLon.Longitude:F6}) | Unity position: ({playerWorldPos.x:F1}, {playerWorldPos.y:F1}, {playerWorldPos.z:F1})");
            }

            // Update each layer independently
            if (K1L0RuntimeDiagnostics.VerboseMapLogs && _doUpdateCallCount % 300 == 0)
            {
                Debug.Log($"[TileLoad] Updating {m_Layers.Count} layers: {string.Join(", ", m_Layers.Select(l => l.Name))}");
            }

            // Land and water share z12, so reuse their identical frustum result
            // instead of calculating the same 25 AABB tests twice per poll.
            var visibleTilesByZoom = new Dictionary<int, HashSet<TileId>>();
            foreach (var layer in m_Layers)
            {
                // Buildings: player-centered 3x3 grid.
                // Roads/land/water: frustum-based.
                bool useFrustumLoading = !layer.Name.Contains("Buildings");
                HashSet<TileId> visibleTiles = null;

                if (useFrustumLoading)
                {
                    int zoom = layer.GetMaxZoom();
                    if (!visibleTilesByZoom.TryGetValue(zoom, out visibleTiles))
                    {
                        visibleTiles = GetVisibleTilesInFrustum(playerLatLon, zoom);
                        visibleTilesByZoom[zoom] = visibleTiles;
                    }
                    K1L0RuntimeDiagnostics.MapLog(
                        $"[FRUSTUM] {layer.Name} z{zoom}: {visibleTiles.Count} tiles visible");
                }

                if (layer.Name.Contains("Buildings"))
                {
                    m_Renderer?.SetPlayerCullCenter(playerLatLon);
                }

                // Use _map as coroutine host — coroutines on OvertureMapManager stall in editor (MB lifecycle broken)
                layer.UpdateTilesForPosition(playerLatLon, _map, useFrustumLoading, visibleTiles);
            }
        }

        private void RefreshBuildingLodsIfNeeded(LatitudeLongitude playerLatLon)
        {
            Vector2d current = Conversions.LatitudeLongitudeToWebMercator(playerLatLon);
            float now = Time.realtimeSinceStartup;
            if (!m_LastObservedPlayerCenter.HasValue)
            {
                m_LastObservedPlayerCenter = current;
                m_LastPlayerMovementAt = now;
            }
            else
            {
                double observedDx = current.x - m_LastObservedPlayerCenter.Value.x;
                double observedDy = current.y - m_LastObservedPlayerCenter.Value.y;
                if (System.Math.Sqrt(observedDx * observedDx + observedDy * observedDy) >= 0.25d)
                {
                    m_LastObservedPlayerCenter = current;
                    m_LastPlayerMovementAt = now;
                }
            }

            if (!m_LastBuildingLodCenter.HasValue)
            {
                m_LastBuildingLodCenter = current;
                return;
            }

            XYZLayer buildings = m_Layers.FirstOrDefault(layer => layer.Name.Contains("Buildings"));
            if (buildings == null) return;

            // Finish one queued tile before considering another. Network and
            // geometry work remain bounded by XYZLayer's existing in-flight
            // limits, but the rest of the city stays visible and allocated.
            if (m_BuildingLodRefreshQueue.Count > 0)
            {
                if (!buildings.IsSettled) return;
                TileId tile = m_BuildingLodRefreshQueue.Dequeue();
                if (buildings.InvalidateLoadedTile(tile))
                {
                    m_Renderer?.ClearBuildingTile(tile);
                    K1L0TileStreamLog.Write("LOD_TILE_INVALIDATE",
                        $"tile={tile.Z}/{tile.X}/{tile.Y} remaining={m_BuildingLodRefreshQueue.Count}");
                }
                return;
            }

            double dx = current.x - m_LastBuildingLodCenter.Value.x;
            double dy = current.y - m_LastBuildingLodCenter.Value.y;
            double movedMeters = System.Math.Sqrt(dx * dx + dy * dy);
            if (movedMeters < Mathf.Max(BuildingLodMinimumRefreshMeters, buildingLodRefreshMeters)) return;
            // Never tear down/rebuild the 3x3 building grid while arrow/GPS
            // movement is actively changing the map origin. Refresh once after
            // movement settles so input and camera animation remain responsive.
            if (now - m_LastPlayerMovementAt < BuildingLodSettleSeconds) return;
            if (!buildings.IsSettled) return;

            m_Renderer?.RefreshIndividualBuildingFacades();
            foreach (TileId tile in BuildingLodTilesNearPlayer(playerLatLon, buildings.GetMaxZoom()))
                if (buildings.ContainsLoadedTile(tile)) m_BuildingLodRefreshQueue.Enqueue(tile);
            m_LastBuildingLodCenter = current;
            K1L0TileStreamLog.Write("LOD_QUEUE",
                $"movedMeters={movedMeters:F1} tiles={m_BuildingLodRefreshQueue.Count}");
            Debug.Log($"[OvertureMapManager] Queued {m_BuildingLodRefreshQueue.Count} nearby building tiles for incremental LOD refresh after {movedMeters:F0}m.");
        }

        private static IEnumerable<TileId> BuildingLodTilesNearPlayer(LatitudeLongitude playerLatLon, int zoom)
        {
            TileId center = Conversions.LatitudeLongitudeToTileId(playerLatLon, zoom);
            Vector2d player = Conversions.LatitudeLongitudeToWebMercator(playerLatLon);
            var candidates = new List<(TileId tile, double distanceSq)>();
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                var tile = new TileId(center.Z, center.X + dx, center.Y + dy);
                var bounds = Conversions.TileBoundsInWebMercator(tile);
                double nearestX = Math.Max(bounds.minX, Math.Min(player.x, bounds.maxX));
                double nearestY = Math.Max(bounds.minY, Math.Min(player.y, bounds.maxY));
                double ox = player.x - nearestX;
                double oy = player.y - nearestY;
                double distanceSq = ox * ox + oy * oy;
                // Only rebuild tiles capable of contributing detailed or
                // emissive facades around the player. Far silhouette tiles do
                // not change when the player walks a few hundred metres.
                if (distanceSq <= 1200d * 1200d)
                    candidates.Add((tile, distanceSq));
            }
            return candidates.OrderBy(candidate => candidate.distanceSq).Select(candidate => candidate.tile);
        }

        private void CreateTileFetchers(string baseURL)
        {
#if UNITY_STANDALONE_OSX
            // The Mac visual-lab player runs beside kiloworld-api. Its map can
            // initialize before APIManager Auto finishes probing, which used to
            // freeze the first production URL into every XYZ fetcher. Always use
            // the healthy local proxy on Mac; device builds retain normal Auto.
            baseURL = "http://localhost:3000";
#endif
            if (string.IsNullOrWhiteSpace(baseURL))
            {
                baseURL = ResolveTileBaseURLImmediate();
            }
            m_BuildingsFetcher = new XYZTileFetcher($"{baseURL}/xyz/buildings/{{z}}/{{x}}/{{y}}.mvt");
            m_TransportationFetcher = new XYZTileFetcher($"{baseURL}/xyz/transportation/{{z}}/{{x}}/{{y}}.mvt");
            m_BaseFetcher = new XYZTileFetcher($"{baseURL}/xyz/base/{{z}}/{{x}}/{{y}}.mvt");
            Debug.Log($"[OvertureMapManager] Tile API base: {baseURL}");
        }

        private string ResolveTileBaseURLImmediate()
        {
#if UNITY_EDITOR || UNITY_STANDALONE_OSX
            string baseURL = null;
            try
            {
                baseURL = APIManager.Instance != null ? APIManager.Instance.GetBaseURL() : null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[OvertureMapManager] Could not read APIManager base URL: {ex.Message}");
            }
            return string.IsNullOrWhiteSpace(baseURL) ? "http://localhost:3000" : baseURL;
#else
            // iPhone map tiles always use the public API tunnel. The native
            // overlay can resolve LAN/tunnel independently, but map startup
            // must not stall on an unreachable private address or freeze that
            // address into all four XYZ fetchers when the phone leaves Wi-Fi.
            return "https://api-tunnel.kilo.gallery";
#endif
        }

        private IEnumerator ResolveTileBaseURL(Action<string> onComplete)
        {
            yield return null;
#if UNITY_EDITOR || UNITY_STANDALONE_OSX
            var api = APIManager.Instance;
            string resolved = null;
            if (api != null)
            {
                resolved = api.GetBaseURL();
                if (string.IsNullOrWhiteSpace(resolved) &&
                    api.GetCurrentEnvironment() == APIManager.APIEnvironment.Auto)
                {
                    // Auto deliberately has no URL until it probes connectivity.
                    // The old map boot path interpreted that temporary null as a
                    // permanent failure and locked tiles to the LAN-only IP even
                    // while the native overlay was successfully using the tunnel.
                    yield return api.TryAutoConnect((_, url) => resolved = url);
                }
            }
            if (string.IsNullOrWhiteSpace(resolved))
                resolved = ResolveTileBaseURLImmediate();
            Debug.Log($"[OvertureMapManager] Resolved local tile API base: {resolved}");
#else
            // Device tiles prefer the tunnel, but must keep working when the
            // local bridge is offline. Probe production before freezing a base
            // URL into the XYZ fetchers.
            string resolved = null;
            string[] candidates =
            {
                "https://api-tunnel.kilo.gallery",
                "https://api.kilomeme.com"
            };
            foreach (string candidate in candidates)
            {
                using (var request = UnityWebRequest.Get($"{candidate}/health"))
                {
                    request.timeout = 8;
                    yield return request.SendWebRequest();
                    if (request.result == UnityWebRequest.Result.Success &&
                        request.responseCode >= 200 && request.responseCode < 300)
                    {
                        resolved = candidate;
                        break;
                    }
                }
            }
            if (string.IsNullOrWhiteSpace(resolved))
                resolved = "https://api.kilomeme.com";
            Debug.Log($"[OvertureMapManager] Using device tile API base: {resolved}");
#endif
            onComplete?.Invoke(resolved);
        }
    }

    public class XYZTileFetcher
    {
        private readonly string _urlTemplate;
        private sealed class PayloadCacheEntry
        {
            public byte[] Data;
            public float LastAccess;
            public int SizeBytes;
        }

        // Keep compressed MVT payloads separate from rendered geometry. An LOD
        // refresh can then rebuild nearby facades without another HTTP request.
        // The byte budget matters more than entry count because dense road tiles
        // can be several MB while ordinary building tiles are often under 200 KB.
        private readonly Dictionary<string, PayloadCacheEntry> _payloadCache = new();
        private long _payloadCacheBytes;
        private const float PayloadCacheTtlSeconds = 600f;
        private const int PayloadCacheMaxEntries = 24;
        private const long PayloadCacheMaxBytes = 24L * 1024L * 1024L;

        public XYZTileFetcher(string urlTemplate)
        {
            _urlTemplate = urlTemplate;
        }

        public IEnumerator FetchTile(int zoom, int x, int y, Action<byte[], bool> onComplete)
        {
            var url = _urlTemplate
                .Replace("{z}", zoom.ToString())
                .Replace("{x}", x.ToString())
                .Replace("{y}", y.ToString());

            float now = Time.realtimeSinceStartup;
            if (_payloadCache.TryGetValue(url, out var cached))
            {
                if (now - cached.LastAccess <= PayloadCacheTtlSeconds)
                {
                    cached.LastAccess = now;
                    Debug.Log($"[XYZTileFetcher] CACHE {zoom}/{x}/{y}: {cached.SizeBytes} bytes");
                    K1L0TileStreamLog.Write("FETCH_CACHE", $"tile={zoom}/{x}/{y} bytes={cached.SizeBytes}");
                    onComplete?.Invoke(cached.Data, true);
                    yield break;
                }
                RemovePayloadCacheEntry(url, cached);
            }

            // Log to BootLog so we can see if a request is in flight when editor freezes
            string layerName = "tile";
            if (_urlTemplate.Contains("places")) layerName = "places";
            else if (_urlTemplate.Contains("buildings")) layerName = "buildings";
            else if (_urlTemplate.Contains("transportation")) layerName = "roads";
            else if (_urlTemplate.Contains("base")) layerName = "base";
            BootDiagnostics.Mark($"Tile API START {layerName} {zoom}/{x}/{y}");
            K1L0TileStreamLog.Write("FETCH_START", $"layer={layerName} tile={zoom}/{x}/{y} url={url}");
            Debug.Log($"[XYZTileFetcher] Requesting {url}");
            OvertureMapManager._lastTileUrl = $"{layerName}/{zoom}/{x}/{y}";

            float startTime = Time.realtimeSinceStartup;
            using (var request = UnityWebRequest.Get(url))
            {
                // Cellular tunnel responses can contain multi-megabyte road
                // tiles. Eight seconds was too aggressive and produced blank
                // maps on otherwise healthy remote connections.
                request.timeout = Application.isEditor ? 15 : 20;
                yield return request.SendWebRequest();

                float elapsed = (Time.realtimeSinceStartup - startTime) * 1000f;
                long responseCode = request.responseCode;
                if (request.result == UnityWebRequest.Result.Success)
                {
                    var data = request.downloadHandler.data;
                    int sizeBytes = data != null ? data.Length : 0;
                    StorePayloadCacheEntry(url, data, sizeBytes);
                    BootDiagnostics.Mark($"Tile API DONE {layerName} {zoom}/{x}/{y} ok {(int)elapsed}ms");
                    K1L0TileStreamLog.Write("FETCH_DONE", $"layer={layerName} tile={zoom}/{x}/{y} ms={elapsed:F0} bytes={sizeBytes} status={responseCode}");
                    Debug.Log($"[XYZTileFetcher] ✓ {layerName} {zoom}/{x}/{y}: {responseCode} | {sizeBytes} bytes | {elapsed:F0}ms");
                    OvertureMapManager._tilesFetched++;
                    OvertureMapManager._lastTileResult = $"OK {sizeBytes}B {elapsed:F0}ms";
                    OvertureMapManager._debugStatus = $"TILES:{OvertureMapManager._tilesFetched}";
                    if (data != null && data.Length > 0)
                    {
                        onComplete?.Invoke(data, true);
                    }
                    else
                    {
                        OvertureMapManager._lastTileResult = $"EMPTY 0B";
                        Debug.LogWarning($"[XYZTileFetcher] ⚠️ Empty tile {zoom}/{x}/{y} (0 bytes)");
                        // A successful empty/204 response is a real empty tile
                        // and may be cached by the layer.
                        onComplete?.Invoke(null, true);
                    }
                }
                else
                {
                    BootDiagnostics.Mark($"Tile API DONE {layerName} {zoom}/{x}/{y} fail {responseCode}");
                    string responseBody = request.downloadHandler?.text ?? "";
                    OvertureMapManager._lastTileResult = $"FAIL {responseCode} {request.error}";
                    OvertureMapManager._debugStatus = $"ERR:{responseCode}";
                    Debug.LogError($"[XYZTileFetcher] ❌ {layerName} {zoom}/{x}/{y}: {responseCode} | {request.error}\nResponse: {(responseBody.Length > 200 ? responseBody.Substring(0, 200) + "..." : responseBody)}");
                    K1L0TileStreamLog.Write("FETCH_FAIL", $"layer={layerName} tile={zoom}/{x}/{y} ms={elapsed:F0} status={responseCode} error={request.error}");
                    // Network/HTTP failures must remain retryable. Previously
                    // these were indistinguishable from HTTP 204 and became
                    // permanently cached blank areas.
                    onComplete?.Invoke(null, false);
                }
            }
        }

        private void StorePayloadCacheEntry(string url, byte[] data, int sizeBytes)
        {
            if (_payloadCache.TryGetValue(url, out var previous))
                RemovePayloadCacheEntry(url, previous);

            _payloadCache[url] = new PayloadCacheEntry
            {
                Data = data,
                SizeBytes = Mathf.Max(0, sizeBytes),
                LastAccess = Time.realtimeSinceStartup
            };
            _payloadCacheBytes += Mathf.Max(0, sizeBytes);
            TrimPayloadCache();
        }

        private void TrimPayloadCache()
        {
            float now = Time.realtimeSinceStartup;
            foreach (var stale in _payloadCache
                .Where(pair => now - pair.Value.LastAccess > PayloadCacheTtlSeconds)
                .ToList())
            {
                RemovePayloadCacheEntry(stale.Key, stale.Value);
            }

            while (_payloadCache.Count > PayloadCacheMaxEntries ||
                   _payloadCacheBytes > PayloadCacheMaxBytes)
            {
                var oldest = _payloadCache.OrderBy(pair => pair.Value.LastAccess).First();
                RemovePayloadCacheEntry(oldest.Key, oldest.Value);
            }
        }

        private void RemovePayloadCacheEntry(string url, PayloadCacheEntry entry)
        {
            if (_payloadCache.Remove(url))
                _payloadCacheBytes = Math.Max(0L, _payloadCacheBytes - entry.SizeBytes);
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
        private readonly HashSet<TileId> _desiredTiles = new HashSet<TileId>();
        private readonly Dictionary<TileId, float> _tileRetryAfter = new Dictionary<TileId, float>();
        private readonly Dictionary<TileId, int> _tileFailureCount = new Dictionary<TileId, int>();
        private TileId? _lastCenterTile;
        private int _globalTileIndex = 0; // Never reset, keeps counting across all tile loads

        // Tiered tile cache - tuned to actual tile sizes and walking speed (~5km/h)
        //   z12 (~10km tiles): Player crosses one every ~2 hours. Keep 10 min, max 12 tiles.
        //   z14 (~2.5km tiles): Player crosses one every ~30 min. Keep 5 min, max 16 tiles.
        //   z15 (~1.2km tiles): Player crosses one every ~15 min. Keep 2 min, max 20 tiles.
        private readonly Dictionary<TileId, float> _tileCacheTime = new Dictionary<TileId, float>();
        private float TileCacheTimeout => MaxZoom <= 12 ? 600f : MaxZoom <= 14 ? 300f : 120f;
        private int MaxLoadedTiles => MaxZoom <= 12 ? 12 : MaxZoom <= 14 ? 16 : 20;
        private bool IsBuildingsLayer => Name.Contains("Buildings");
        private int _generation;
        public bool IsSettled =>
            _requestingTiles.Count == 0 &&
            _renderingTiles.Count == 0 &&
            _desiredTiles.All(tile => _loadedTiles.Contains(tile));

        public bool ContainsLoadedTile(TileId tile) => _loadedTiles.Contains(tile);

        public bool InvalidateLoadedTile(TileId tile)
        {
            if (!_loadedTiles.Remove(tile)) return false;
            _tileCacheTime.Remove(tile);
            _tileRetryAfter.Remove(tile);
            _tileFailureCount.Remove(tile);
            return true;
        }

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

        public void ClearLoadedState()
        {
            _generation++;
            _loadedTiles.Clear();
            _requestingTiles.Clear();
            _renderingTiles.Clear();
            _desiredTiles.Clear();
            _tileCacheTime.Clear();
            _tileRetryAfter.Clear();
            _tileFailureCount.Clear();
            _lastCenterTile = null;
            if (IsBuildingsLayer)
            {
                OvertureMapManager._buildingLoadedCount = 0;
                OvertureMapManager._buildingRequestingCount = 0;
                OvertureMapManager._buildingLoadedTiles = "";
                OvertureMapManager._lastBuildingRender = "cleared";
            }
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

            _desiredTiles.Clear();
            _desiredTiles.UnionWith(newTiles);

            if (IsBuildingsLayer)
            {
                OvertureMapManager._buildingCenterTile = $"{currentCenter.Z}/{currentCenter.X}/{currentCenter.Y}";
                OvertureMapManager._buildingLoadedCount = _loadedTiles.Count;
                OvertureMapManager._buildingRequestingCount = _requestingTiles.Count;
                OvertureMapManager._buildingLoadedTiles = string.Join(",", _loadedTiles.Select(t => $"{t.Z}/{t.X}/{t.Y}").OrderBy(s => s));
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
            // Fetch completion moves a tile from requesting -> rendering. Rendering
            // is deliberately spread across frames, so it must remain ineligible
            // here until its completion callback moves it into loaded. Otherwise a
            // fast RAM-cache response can enqueue the same expensive mesh repeatedly.
            tilesToAdd.ExceptWith(_renderingTiles);
            float realtimeNow = Time.realtimeSinceStartup;
            tilesToAdd.RemoveWhere(tile =>
                _tileRetryAfter.TryGetValue(tile, out float retryAt) && realtimeNow < retryAt);

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
                    K1L0RuntimeDiagnostics.MapLog($"[TileLoad] {Name}: MEMORY CAP evicted {evictionCandidates.Count} oldest tiles (limit={MaxLoadedTiles}, loaded={_loadedTiles.Count})");
                }
            }

            // Log ONCE when tiles change (reduced spam)
            if (tilesToAdd.Count > 0 || tilesToRemove.Count > 0)
            {
                string mode = useFrustum ? "FRUSTUM" : "GRID";
                K1L0RuntimeDiagnostics.MapLog($"[TileLoad] {Name} ({mode}): +{tilesToAdd.Count} tiles, -{tilesToRemove.Count} tiles | Loaded: {_loadedTiles.Count}/{MaxLoadedTiles}, Cached: {_tileCacheTime.Count}, CacheTimeout: {TileCacheTimeout}s");
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
            // Completed vector requests all resume on Unity's main thread.
            // Pace every player build, not only the editor, so a tile boundary
            // cannot start a large burst of mesh construction in one frame.
            int maxNewPerPoll = Application.isEditor ? 2 : (IsBuildingsLayer ? 1 : 3);
            int maxInFlight = Application.isEditor ? 4 : (IsBuildingsLayer ? 3 : 8);
            int added = 0;
            foreach (var tile in tilesToAdd
                .OrderBy(t =>
                {
                    long dx = (long)t.X - currentCenter.X;
                    long dy = (long)t.Y - currentCenter.Y;
                    return dx * dx + dy * dy;
                }))
            {
                if (added >= maxNewPerPoll ||
                    _requestingTiles.Count + _renderingTiles.Count >= maxInFlight) break;

                string tileKey = $"{tile.Z}/{tile.X}/{tile.Y}";
                _tileRequestTimes[tileKey] = Time.realtimeSinceStartup;

                _requestingTiles.Add(tile); // Mark as requesting (will move to _loadedTiles after successful load)
                K1L0TileStreamLog.Write("LAYER_REQUEST",
                    $"layer={Name} tile={tile.Z}/{tile.X}/{tile.Y} requesting={_requestingTiles.Count} rendering={_renderingTiles.Count}");
                coroutineHost.StartCoroutine(RequestTile(tile, _globalTileIndex, _generation));
                _globalTileIndex++;
                added++;
            }

            _lastCenterTile = new TileId(currentCenter.Z, currentCenter.X, currentCenter.Y);
        }

public IEnumerator RequestTile(TileId tile, int tileIndex, int generation)
        {
            yield return Fetcher.FetchTile(tile.Z, tile.X, tile.Y, (data, requestSucceeded) =>
            {
                // Ignore a response belonging to a building-LOD generation
                // that was invalidated while the network request was active.
                if (generation != _generation) return;
                if (!requestSucceeded)
                {
                    _requestingTiles.Remove(tile);
                    int failures = _tileFailureCount.TryGetValue(tile, out int previous) ? previous + 1 : 1;
                    _tileFailureCount[tile] = failures;
                    float retryDelay = Mathf.Min(30f, 2f * Mathf.Pow(2f, Mathf.Min(4, failures - 1)));
                    _tileRetryAfter[tile] = Time.realtimeSinceStartup + retryDelay;
                    Debug.LogWarning($"[TileLoad] {Name}: retrying {tile.Z}/{tile.X}/{tile.Y} in {retryDelay:F0}s after fetch failure #{failures}.");
                    return;
                }

                _tileRetryAfter.Remove(tile);
                _tileFailureCount.Remove(tile);
                if (data != null && data.Length > 0)
                {
                    _requestingTiles.Remove(tile);
                    _renderingTiles.Add(tile);
                    if (Renderer == null)
                    {
                        _renderingTiles.Remove(tile);
                        Debug.LogError($"[TileLoad] {Name}: renderer unavailable for {tile.Z}/{tile.X}/{tile.Y}");
                        return;
                    }
                    Renderer.RenderTile(tile, data, SourceLayers, Name, tileIndex, (rendered, error) =>
                    {
                        // A location/LOD refresh may invalidate this coroutine
                        // while it is yielding across frames.
                        if (generation != _generation) return;
                        _renderingTiles.Remove(tile);
                        if (!rendered)
                        {
                            _loadedTiles.Remove(tile);
                            _tileRetryAfter[tile] = Time.realtimeSinceStartup + 2f;
                            Debug.LogError($"[TileLoad] {Name}: RenderTile FAILED for {tile.Z}/{tile.X}/{tile.Y}: {error}");
                            return;
                        }
                        _loadedTiles.Add(tile);
                        if (_loadedTiles.Count == 1)
                            BootState.SetFirstTilesLoaded();
                    });
                }
                else
                {
                    // A successful 0-byte response means this layer has no data
                    // for the tile. Treat that as a resolved/loaded empty tile;
                    // removing it from requesting without caching it caused the
                    // visibility poll to request the same empties forever.
                    _requestingTiles.Remove(tile);
                    _loadedTiles.Add(tile);
                    if (_loadedTiles.Count == 1)
                        BootState.SetFirstTilesLoaded();
                }
            });
        }
    }

    public class OvertureVectorRenderer
    {
        private readonly KiloverseMapInfo _map;
        private readonly Dictionary<string, MapboxIVLV> _visualizers =
            new Dictionary<string, MapboxIVLV>();
        private readonly Dictionary<string, List<GameObject>> _tileObjects =
            new Dictionary<string, List<GameObject>>();
        private readonly HashSet<string> _loggedTiles = new HashSet<string>();
        private readonly bool _logLayers;
        private static bool _waterTileStructureLogged;
        private bool _initialized;
        private Vector2d _lastMapCenterMercator;
        private Vector2d? _playerCullCenterMercator;
        private readonly MapboxMapInfoAdapter _mapboxMapInfo;

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
        // Each vertex ≈ 80 bytes (pos 12 + normal 12 + tangent 16 + uv0 8 + uv1 8 + indices ~24)
        // Buildings get real caps because dense waterfront/downtown tiles can contain thousands of
        // window/door quads before renderer culling ever gets a chance to disable them.
        private const int VERTEX_BUDGET_BUILDING = 900000;
        private const int VERTEX_BUDGET_ROAD     = int.MaxValue;
        private const int VERTEX_BUDGET_WATER    = int.MaxValue;
        private const int VERTEX_BUDGET_DEFAULT  = int.MaxValue;
        private const int FEATURE_CAP_BUILDING   = int.MaxValue;
        private const int FEATURE_CAP_ROAD       = int.MaxValue;
        private const int FEATURE_CAP_DEFAULT    = int.MaxValue;
        private const int FEATURES_PER_YIELD     = 10;       // Keep input/rotation responsive while dense tiles stream
        // A global quality allocation is more predictable than per-tile quotas:
        // sparse places can spend all available detail on their few buildings,
        // while dense cities cannot multiply the allowance by nine loaded tiles.
        private const int BUILDING_DETAIL_GLOBAL_LIMIT = 96;
        // Streaming tiles arrive sequentially. Without a per-tile share, the
        // first dense tile can consume all 96 detailed slots and leave a
        // physically adjacent tile simplified when the player stands near an
        // edge. Eighteen preserves rich center/cardinal neighborhoods while
        // the global cap still bounds total geometry.
        private const int BUILDING_DETAIL_PER_TILE_LIMIT = 18;
        private const int BUILDING_DETAIL_BACKGROUND_LIMIT = 48;
        private const float BUILDING_IMMEDIATE_DETAIL_RADIUS_METERS = 300f;
        private const int BUILDING_SIMPLE_GLOBAL_LIMIT = 1000;
        private const int BUILDING_DETAIL_VERTEX_LIMIT = 400000;
        private const int BUILDING_SIMPLE_VERTEX_LIMIT = 250000;
        private const int BUILDING_SHELL_VERTEX_LIMIT = 250000;
        private const float BUILDING_VISIBLE_RADIUS_METERS = 2200f;
        private const float BUILDING_DETAIL_RADIUS_METERS = 350f;
        private const float BUILDING_EMISSIVE_RADIUS_METERS = 850f;

        private sealed class BuildingTileStats
        {
            public int detailed;
            public int simple;
            public int shell;
            public int detailedVertices;
            public int simpleVertices;
            public int shellVertices;
            public int TotalVertices => detailedVertices + simpleVertices + shellVertices;
        }

        private readonly Dictionary<string, BuildingTileStats> _buildingStatsByTile = new();
        private int _globalDetailedBuildings;
        private int _globalSimpleBuildings;
        private int _globalShellBuildings;
        private int _globalDetailedVertices;
        private int _globalSimpleVertices;
        private int _globalShellVertices;

        public OvertureVectorRenderer(KiloverseMapInfo map, Dictionary<string, MapboxIVLV> visualizers, bool logLayers)
        {
            _map = map;
            _logLayers = logLayers;
            _mapboxMapInfo = new MapboxMapInfoAdapter(map);

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
                var visualizer = kvp.Value as MapboxVLV;
                if (visualizer != null)
                {
                    var rootField = typeof(MapboxVLV).GetField("_layerRootObject", BindingFlags.NonPublic | BindingFlags.Instance);
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

                    // Initialize the visualizer (sets up modifier stacks and object pools)
                    try
                    {
                        if (_map != null && _map.gameObject != null)
                        {
                            _map.StartCoroutine(InitializeVisualizerCoroutine(visualizer, kvp.Key));
                        }
                        else
                        {
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

        public void SetPlayerCullCenter(LatitudeLongitude playerLatLon)
        {
            _playerCullCenterMercator = Conversions.LatitudeLongitudeToWebMercator(playerLatLon);
        }

        private IEnumerator InitializeVisualizerCoroutine(MapboxVLV visualizer, string key)
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

public void RenderTile(
            TileId tile,
            byte[] payload,
            string[] sourceLayers,
            string layerName = "",
            int tileIndex = -1,
            Action<bool, string> onComplete = null)
        {
            _map.StartCoroutine(RenderTileTrackedCoroutine(
                tile, payload, sourceLayers, layerName, tileIndex, onComplete));
        }

private IEnumerator RenderTileTrackedCoroutine(
            TileId tile,
            byte[] payload,
            string[] sourceLayers,
            string layerName,
            int tileIndex,
            Action<bool, string> onComplete)
        {
            float renderStartedAt = Time.realtimeSinceStartup;
            K1L0TileStreamLog.Write("RENDER_START", $"layer={layerName} tile={tile.Z}/{tile.X}/{tile.Y} bytes={payload?.Length ?? 0}");
            var render = RenderTileCoroutine(tile, payload, sourceLayers, layerName, tileIndex);
            while (true)
            {
                bool hasNext;
                object yielded = null;
                try
                {
                    hasNext = render.MoveNext();
                    if (hasNext) yielded = render.Current;
                }
                catch (Exception ex)
                {
                    K1L0TileStreamLog.Write("RENDER_FAIL", $"layer={layerName} tile={tile.Z}/{tile.X}/{tile.Y} ms={(Time.realtimeSinceStartup - renderStartedAt) * 1000f:F0} error={ex.Message}");
                    onComplete?.Invoke(false, ex.Message);
                    yield break;
                }

                if (!hasNext) break;
                yield return yielded;
            }
            K1L0TileStreamLog.Write("RENDER_DONE", $"layer={layerName} tile={tile.Z}/{tile.X}/{tile.Y} ms={(Time.realtimeSinceStartup - renderStartedAt) * 1000f:F0}");
            onComplete?.Invoke(true, null);
        }

private IEnumerator RenderTileCoroutine(TileId tile, byte[] payload, string[] sourceLayers, string layerName = "", int tileIndex = -1)
        {
            // Editor: yield immediately to spread burst when many tiles complete at once (prevents lockup)
            if (Application.isEditor) yield return null;

            string tileIndexStr = tileIndex >= 0 ? $" tile #{tileIndex}" : "";
            if (!_initialized) InitializeVisualizers();
            if (!_initialized)
            {
                 throw new InvalidOperationException("Failed to initialize visualizers. Check VectorLayerModuleScript.");
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
                throw new InvalidDataException($"Failed to parse vector tile {tileKey}");
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
                var visualizerImpl = visualizer as MapboxVLV;
                if (visualizerImpl != null)
                {
                    var rootField = typeof(MapboxVLV).GetField("_layerRootObject", BindingFlags.NonPublic | BindingFlags.Instance);
                    var rootTransform = rootField?.GetValue(visualizerImpl) as Transform;
                    if (rootTransform == null || rootTransform.gameObject == null)
                    {
                        Debug.LogError($"[Overture]{tileIndexStr} Visualizer '{visualizerKey}' has no layer root object! Skipping tile {tileKey}.");
                        continue;
                    }
                    Debug.Log($"[TileLoad] {layerName}{tileIndexStr} {tileKey}: Layer root exists: {rootTransform.gameObject.name} (active={rootTransform.gameObject.activeInHierarchy})");
                }

                var layerData = vectorTile.GetLayer(sourceLayer);
                var meshDataDict = new Dictionary<int, HashSet<MapboxMeshData>>();

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
                        var distantBuildingBatches = sourceLayer == "building"
                            ? new List<MapboxMeshData>()
                            : null;
                        var roadBatches = sourceLayer == "segment"
                            ? new List<MapboxMeshData>()
                            : null;
                        var waterBatches = sourceLayer == "water"
                            ? new List<MapboxMeshData>()
                            : null;
                        int effectiveFeatureCount = Mathf.Min(featureCount, featureCap);
                        if (featureCount > featureCap)
                        {
                            Debug.Log($"[TileLoad] {layerName}{tileIndexStr} {tileKey} '{sourceLayer}': Feature cap hit — {featureCount} features, capped to {featureCap}");
                        }

                        // Process each building tile from nearest to farthest. Tile
                        // requests are also center-first, so the global counters
                        // converge on the nearest buildings rather than source order.
                        int[] featureOrder = null;
                        if (sourceLayer == "building")
                        {
                            var priorities = new List<(int index, float detailPriority)>(effectiveFeatureCount);
                            float priorityFrameStartedAt = Time.realtimeSinceStartup;
                            for (int index = 0; index < effectiveFeatureCount; index++)
                            {
                                var rawFeature = layerData.GetFeature(index);
                                var geometry = rawFeature.Geometry<float>(0);
                                double sumX = 0d, sumY = 0d;
                                double minX = double.MaxValue, maxX = double.MinValue;
                                double minY = double.MaxValue, maxY = double.MinValue;
                                int points = 0;
                                foreach (var part in geometry)
                                foreach (var point in part)
                                {
                                    double x = point.X / 4096d;
                                    double y = -point.Y / 4096d;
                                    sumX += x;
                                    sumY += y;
                                    minX = System.Math.Min(minX, x); maxX = System.Math.Max(maxX, x);
                                    minY = System.Math.Min(minY, y); maxY = System.Math.Max(maxY, y);
                                    points++;
                                }
                                float distance = points > 0
                                    ? EstimateNormalizedFeatureDistanceMeters(tile, sumX / points, sumY / points)
                                    : float.MaxValue;
                                double tileMeters = Conversions.TileEdgeSizeInMercator(tile.Z);
                                float footprintMeters = points > 0
                                    ? (float)(System.Math.Max(maxX - minX, maxY - minY) * tileMeters)
                                    : 1f;
                                float sizeWeight = Mathf.Sqrt(Mathf.Clamp(footprintMeters, 8f, 80f) / 8f);
                                priorities.Add((index, distance / sizeWeight));

                                if ((index + 1) % FEATURES_PER_YIELD == 0 ||
                                    Time.realtimeSinceStartup - priorityFrameStartedAt >= 0.0025f)
                                {
                                    yield return null;
                                    priorityFrameStartedAt = Time.realtimeSinceStartup;
                                }
                            }
                            featureOrder = priorities
                                .OrderBy(candidate => candidate.detailPriority)
                                .Select(candidate => candidate.index)
                                .ToArray();
                        }

                        var tileBuildingStats = sourceLayer == "building"
                            ? new BuildingTileStats()
                            : null;
                        int featuresPerYield = Application.isEditor ? 5 : FEATURES_PER_YIELD;
                        float featureFrameStartedAt = Time.realtimeSinceStartup;
                        for (int i = 0; i < effectiveFeatureCount; i++)
                        {
                            // Bound both work count and wall-clock time. Features
                            // have wildly different polygon complexity, so a
                            // feature-count limit alone cannot prevent hitches.
                            if (i > 0 && (i % featuresPerYield == 0 ||
                                Time.realtimeSinceStartup - featureFrameStartedAt >= 0.0025f))
                            {
                                yield return null;
                                featureFrameStartedAt = Time.realtimeSinceStartup;
                            }

                            int featureIndex = featureOrder != null ? featureOrder[i] : i;
                            var feature = layerData.GetFeature(featureIndex);

                            // Convert to Mapbox VectorFeatureUnity (Mapbox modifier stacks expect Mapbox types)
                            var featureUnity = new MapboxFeature();
                            featureUnity.Properties = feature.GetProperties();

                            // BUG FIX: Mapbox.VectorTile.VectorTileReader.dll doubles num_floors values
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
                            featureUnity.TileId = new MapboxTileId(tile.Z, tile.X, tile.Y);
                            featureUnity.FeatureId = (long)feature.Id;
                            
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

                            bool useMergedBuildingLod = false;
                            bool preserveIndividualBuilding = false;
                            var buildingLod = ZossBuildingStack.BuildingLODMode.Detailed;
                            if (sourceLayer == "building")
                            {
                                float featureDistance = EstimateFeatureDistanceFromMapCenterMeters(tile, featureUnity);
                                float footprintDistance = EstimateFeatureFootprintDistanceFromMapCenterMeters(tile, featureUnity);
                                if (footprintDistance > BUILDING_VISIBLE_RADIUS_METERS)
                                    continue;

                                // Buildings near enough for the player/avatar to
                                // enter must retain their own renderer and mesh.
                                // Tile-level merging destroys the one-footprint
                                // identity BuildingFlattener needs at edges.
                                // Keep a generous player neighborhood unmerged.
                                // Large footprints can have a centroid well over
                                // 120m away while their edge contains the player;
                                // merging those buildings destroys the individual
                                // metadata/footprint required by BuildingFlattener.
                                preserveIndividualBuilding = footprintDistance <= 300f;

                                float detailRadius = Mathf.Clamp(
                                    PlayerPrefs.GetFloat("k1lo_buildingDetailRadius", BUILDING_DETAIL_RADIUS_METERS),
                                    BUILDING_IMMEDIATE_DETAIL_RADIUS_METERS, 1200f);
                                float emissiveRadius = Mathf.Clamp(
                                    PlayerPrefs.GetFloat("k1lo_buildingEmissiveRadius", BUILDING_EMISSIVE_RADIUS_METERS),
                                    detailRadius, BUILDING_VISIBLE_RADIUS_METERS);
                                bool immediateDetail = featureDistance <= BUILDING_IMMEDIATE_DETAIL_RADIUS_METERS;
                                bool detailEligible = featureDistance <= detailRadius;
                                bool hasDetailAllocation = detailEligible && (immediateDetail
                                    ? _globalDetailedBuildings < BUILDING_DETAIL_GLOBAL_LIMIT
                                    : tileBuildingStats.detailed < BUILDING_DETAIL_PER_TILE_LIMIT &&
                                      _globalDetailedBuildings < BUILDING_DETAIL_BACKGROUND_LIMIT);

                                if (hasDetailAllocation &&
                                    _globalDetailedVertices < BUILDING_DETAIL_VERTEX_LIMIT)
                                {
                                    buildingLod = ZossBuildingStack.BuildingLODMode.Detailed;
                                    _globalDetailedBuildings++;
                                    tileBuildingStats.detailed++;
                                }
                                else if (featureDistance <= emissiveRadius &&
                                         _globalSimpleBuildings < BUILDING_SIMPLE_GLOBAL_LIMIT &&
                                         _globalSimpleVertices < BUILDING_SIMPLE_VERTEX_LIMIT)
                                {
                                    buildingLod = ZossBuildingStack.BuildingLODMode.SingleWindow;
                                    _globalSimpleBuildings++;
                                    tileBuildingStats.simple++;
                                }
                                else
                                {
                                    if (_globalShellVertices >= BUILDING_SHELL_VERTEX_LIMIT)
                                        continue;
                                    buildingLod = ZossBuildingStack.BuildingLODMode.Silhouette;
                                    _globalShellBuildings++;
                                    tileBuildingStats.shell++;
                                }
                                useMergedBuildingLod = buildingLod != ZossBuildingStack.BuildingLODMode.Detailed &&
                                    !preserveIndividualBuilding;
                                featureUnity.Properties["_k1lo_lod"] = buildingLod.ToString();
                                featureUnity.Properties["_k1lo_lod_distance_m"] = featureDistance;
                                featureUnity.Properties["_k1lo_merged_lod"] = useMergedBuildingLod;
                            }

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
                                    var riverMd = new MapboxMeshData();
                                    riverMd.Feature = featureUnity;
                                    GenerateRiverLineMesh(featureUnity, riverMd, _mapboxMapInfo);
                                    if (riverMd.Vertices != null && riverMd.Vertices.Count > 0)
                                    {
                                        cumulativeVerts += riverMd.Vertices.Count;
                                        processedFeatures++;
                                        if (waterRendered < 2)
                                            Debug.Log($"[Water] {tileKey} RENDER LINESTRING #{waterRendered} class={cls} subtype={sub} verts={totalVerts} (as line)");
                                        waterRendered++;
                                        AppendBatchedMesh(waterBatches, riverMd);
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

                            var md = new MapboxMeshData();
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
                                if (i == 0 && sourceLayer == "building")
                                    Debug.Log($"[DIAG] Building #{i} has NO geometry! Points={featureUnity.Points?.Count ?? -1}, [0].Count={(featureUnity.Points != null && featureUnity.Points.Count > 0 ? featureUnity.Points[0]?.Count ?? -1 : -1)}");
                                continue;
                            }
                            if (i == 0 && sourceLayer == "building")
                                Debug.Log($"[DIAG] Building #{i} HAS geometry: pts.Count={featureUnity.Points.Count}, pts[0].Count={featureUnity.Points[0].Count}");

                            
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
                                if (i == 0 && sourceLayer == "building")
                                    Debug.Log($"[DIAG] Pre-RunMeshModifiers: feature pts={featureUnity.Points?.Count}/{featureUnity.Points?[0]?.Count}, stack={stack?.GetType().Name}, mapInfo={_mapboxMapInfo != null}");
                                var previousBuildingLod = ZossBuildingStack.CurrentLOD;
                                if (sourceLayer == "building")
                                {
                                    ZossBuildingStack.CurrentLOD = buildingLod;
                                    ZossBuildingStack.CurrentViewerPosition = GetViewerPositionInTile(tile);
                                    ZossBuildingStack.HasCurrentViewerPosition = true;
                                }

                                try
                                {
                                    md = stack.RunMeshModifiers(featureUnity, md, _mapboxMapInfo);
                                }
                                finally
                                {
                                    if (sourceLayer == "building")
                                        ZossBuildingStack.CurrentLOD = previousBuildingLod;
                                }
                                if (i == 0 && sourceLayer == "building")
                                    Debug.Log($"[DIAG] Post-RunMeshModifiers: verts={md.Vertices?.Count ?? -1}, tris={md.Triangles?.Count ?? -1}");
                            }
                            catch (System.Exception ex)
                            {
                                // Only log first error per tile to avoid spam
                                if (i < 3)
                                {
                                    Debug.LogWarning($"[Overture]{tileIndexStr} Tile {tile.Z}/{tile.X}/{tile.Y} layer '{sourceLayer}': Mesh generation failed on feature #{i}\nException: {ex.GetType().Name}\nMessage: {ex.Message}\nStack: {ex.StackTrace}");
                                }
                                continue; // Skip this feature
                            }

                            // Only add MeshData with valid vertices to avoid CreateGo crashes
                            if (md.Vertices != null && md.Vertices.Count > 0)
                            {
                                if (sourceLayer == "building")
                                {
                                    int globalBuildingVertices = _globalDetailedVertices + _globalSimpleVertices + _globalShellVertices;
                                    if (globalBuildingVertices + md.Vertices.Count > VERTEX_BUDGET_BUILDING)
                                    {
                                        // Undo the provisional count reservation for
                                        // the building that could not fit the budget.
                                        if (buildingLod == ZossBuildingStack.BuildingLODMode.Detailed)
                                        {
                                            _globalDetailedBuildings--; tileBuildingStats.detailed--;
                                        }
                                        else if (buildingLod == ZossBuildingStack.BuildingLODMode.SingleWindow)
                                        {
                                            _globalSimpleBuildings--; tileBuildingStats.simple--;
                                        }
                                        else
                                        {
                                            _globalShellBuildings--; tileBuildingStats.shell--;
                                        }
                                        budgetExceeded = true;
                                        Debug.LogWarning($"[MEMORY] Global building vertex budget reached at {globalBuildingVertices:N0}/{VERTEX_BUDGET_BUILDING:N0}; remaining farther buildings skipped.");
                                        break;
                                    }

                                    if (buildingLod == ZossBuildingStack.BuildingLODMode.Detailed)
                                    {
                                        _globalDetailedVertices += md.Vertices.Count;
                                        tileBuildingStats.detailedVertices += md.Vertices.Count;
                                    }
                                    else if (buildingLod == ZossBuildingStack.BuildingLODMode.SingleWindow)
                                    {
                                        _globalSimpleVertices += md.Vertices.Count;
                                        tileBuildingStats.simpleVertices += md.Vertices.Count;
                                    }
                                    else
                                    {
                                        _globalShellVertices += md.Vertices.Count;
                                        tileBuildingStats.shellVertices += md.Vertices.Count;
                                    }
                                }
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

                                if (sourceLayer == "building" && useMergedBuildingLod)
                                {
                                    AppendBatchedMesh(distantBuildingBatches, md);
                                }
                                else if (sourceLayer == "segment")
                                {
                                    // Roads share one material and do not require
                                    // per-segment GameObjects. Batch them per tile
                                    // to remove thousands of renderers/draw calls.
                                    AppendBatchedMesh(roadBatches, md);
                                }
                                else if (sourceLayer == "water")
                                {
                                    // Water polygons share one material. Keeping
                                    // every tiny pond/shore fragment as its own
                                    // renderer created 8k+ renderers around
                                    // Hernandez Park for only ~37k vertices.
                                    // Batch per tile just like roads; water has no
                                    // per-feature runtime interaction requirement.
                                    AppendBatchedMesh(waterBatches, md);
                                }
                                else
                                {
                                    if (!meshDataDict.ContainsKey(stackPair.Key)) meshDataDict.Add(stackPair.Key, new HashSet<MapboxMeshData>());
                                    meshDataDict[stackPair.Key].Add(md);
                                }
                            }
                        }

                        if (distantBuildingBatches != null && distantBuildingBatches.Count > 0)
                        {
                            if (!meshDataDict.ContainsKey(stackPair.Key)) meshDataDict.Add(stackPair.Key, new HashSet<MapboxMeshData>());
                            foreach (var batch in distantBuildingBatches)
                                meshDataDict[stackPair.Key].Add(batch);
                        }
                        if (roadBatches != null && roadBatches.Count > 0)
                        {
                            if (!meshDataDict.ContainsKey(stackPair.Key)) meshDataDict.Add(stackPair.Key, new HashSet<MapboxMeshData>());
                            foreach (var batch in roadBatches)
                                meshDataDict[stackPair.Key].Add(batch);
                        }
                        if (waterBatches != null && waterBatches.Count > 0)
                        {
                            if (!meshDataDict.ContainsKey(stackPair.Key)) meshDataDict.Add(stackPair.Key, new HashSet<MapboxMeshData>());
                            foreach (var batch in waterBatches)
                                meshDataDict[stackPair.Key].Add(batch);
                        }

                        if (sourceLayer == "building" && tileBuildingStats != null)
                        {
                            _buildingStatsByTile[tileKey] = tileBuildingStats;
                            PublishBuildingStats();
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
                        layerObjects = visualizerImpl.CreateGo(new MapboxTileId(tile.Z, tile.X, tile.Y), meshDataDict);
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

                    if (layerName.Contains("Buildings"))
                        OvertureMapManager._lastBuildingRender = $"{tile.Z}/{tile.X}/{tile.Y} {layerData.FeatureCount()}->{layerObjects.Count}";
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
                    List<MapboxFeature> features = new List<MapboxFeature>();
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
                        if (sourceLayer == "building" &&
                            PlayerPrefs.GetFloat("k1lo_buildingsVisible", 1f) < 0.5f)
                        {
                            foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
                            {
                                renderer.forceRenderingOff = true;
                                renderer.enabled = false;
                            }
                        }
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
                    if (layerName.Contains("Buildings"))
                        OvertureMapManager._lastBuildingRender = $"{tile.Z}/{tile.X}/{tile.Y} {layerData.FeatureCount()}->0 no_mesh";
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

            if (layerName.IndexOf("road", StringComparison.OrdinalIgnoreCase) >= 0)
                ApplyRoadBrightnessToObjects(createdObjects, PlayerPrefs.GetFloat("k1lo_roadValue", .88f));

            // Position tile objects in Web Mercator space (1 Unity unit = 1 meter)
            // Game coordinate system: player at (0,0,0), map center = player GPS
            // Mesh vertices are in normalized tile space (0-1), need scaling to meters
            PositionTileObjectsInMercator(tile, createdObjects, tileIndexStr);

            Debug.Log($"[TileLoad] {layerName}{tileIndexStr} {tile.Z}/{tile.X}/{tile.Y}: Rendering complete (spread across multiple frames to avoid lag)");
            BootState.MarkTileRenderComplete(layerName);
        }

        /// <summary>
        /// Refreshes already-generated roads without rebuilding map tiles. The
        /// old setting only updated PlayerPrefs, leaving every live renderer on
        /// its original material color until the next app launch/tile load.
        /// </summary>
        public void ApplyRoadBrightness(float value)
        {
            value = Mathf.Clamp01(value);
            int objectCount = 0;
            foreach (var tile in _tileObjects)
            {
                var objects = tile.Value;
                if (objects == null) continue;
                foreach (var go in objects)
                {
                    if (go == null) continue;
                    string n = go.name ?? string.Empty;
                    if (n.IndexOf("road", StringComparison.OrdinalIgnoreCase) < 0 &&
                        n.IndexOf("segment", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    ApplyRoadBrightnessToObject(go, value);
                    objectCount++;
                }
            }
            Debug.Log($"[OvertureMapManager] Applied road brightness {value:F2} to {objectCount} road objects.");
        }

        private static void ApplyRoadBrightnessToObjects(IEnumerable<GameObject> objects, float value)
        {
            if (objects == null) return;
            foreach (var go in objects)
                if (go != null) ApplyRoadBrightnessToObject(go, value);
        }

        private static void ApplyRoadBrightnessToObject(GameObject go, float value)
        {
            float roadDayness = PlayerPrefs.GetInt("k1lo_visualNightOverride", 0) == 1
                ? 0f : Mathf.Clamp01((KiloWorld.Rendering.Systems.RenderManager.LiveSunAltitudeDeg + 4f) / 14f);
            float dayValue = PlayerPrefs.GetFloat("k1lo_dayRoadValue", .32f);
            value = Mathf.Lerp(value, dayValue, roadDayness);
            foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
            {
                var shared = renderer.sharedMaterial;
                if (shared == null) continue;
                Color source = shared.HasProperty("_BaseColor") ? shared.GetColor("_BaseColor")
                    : shared.HasProperty("_Color") ? shared.GetColor("_Color") : Color.gray;
                float hue = PlayerPrefs.GetFloat("k1lo_roadHue", .62f);
                float saturation = PlayerPrefs.GetFloat("k1lo_roadSaturation", .08f);
                Color adjusted = Color.HSVToRGB(Mathf.Repeat(hue, 1f), Mathf.Clamp01(saturation), Mathf.Clamp01(value));
                adjusted.a = source.a;
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                if (shared.HasProperty("_BaseColor")) block.SetColor("_BaseColor", adjusted);
                if (shared.HasProperty("_Color")) block.SetColor("_Color", adjusted);
                if (shared.HasProperty("_EmissionColor"))
                {
                    float glow = Mathf.Clamp01(PlayerPrefs.GetFloat("k1lo_roadGlow", .34f)) * (1f - roadDayness);
                    block.SetColor("_EmissionColor", adjusted * (glow * 1.8f));
                    shared.EnableKeyword("_EMISSION");
                }
                renderer.SetPropertyBlock(block);
            }
        }

        private float EstimateFeatureDistanceFromMapCenterMeters(TileId tile, MapboxFeature feature)
        {
            if (_map == null || _map.MapInformation == null || feature?.Points == null || feature.Points.Count == 0)
                return 0f;

            int pointCount = 0;
            double sumX = 0;
            double sumZ = 0;
            foreach (var ring in feature.Points)
            {
                if (ring == null) continue;
                foreach (var point in ring)
                {
                    sumX += point.x;
                    sumZ += point.z;
                    pointCount++;
                }
            }

            if (pointCount == 0)
                return 0f;

            double normalizedX = sumX / pointCount;
            double normalizedZ = sumZ / pointCount;

            return EstimateNormalizedFeatureDistanceMeters(tile, normalizedX, normalizedZ);
        }

        // Distance to the footprint itself, rather than its centroid. This is
        // important for large or tile-edge buildings: their centroid may be
        // hundreds of metres away even while the player is inside the polygon.
        // Such a building must remain an individual renderer so the runtime
        // flattener can suppress it without hiding a whole merged tile batch.
        private float EstimateFeatureFootprintDistanceFromMapCenterMeters(TileId tile, MapboxFeature feature)
        {
            if (_map == null || _map.MapInformation == null || feature?.Points == null)
                return 0f;

            var tileBoundsM = Conversions.TileBoundsInWebMercator(new TileId(tile.Z, tile.X, tile.Y));
            double tileSize = Conversions.TileEdgeSizeInMercator(tile.Z);
            Vector2d viewer;
            if (_playerCullCenterMercator.HasValue)
                viewer = _playerCullCenterMercator.Value;
            else
            {
                var ll = _map.MapInformation.LatitudeLongitude;
                viewer = Conversions.LatitudeLongitudeToWebMercator(
                    new LatitudeLongitude(ll.Latitude, ll.Longitude));
            }

            double bestSq = double.MaxValue;
            foreach (var ring in feature.Points)
            {
                if (ring == null || ring.Count == 0) continue;
                for (int i = 0; i < ring.Count; i++)
                {
                    Vector3 aPoint = ring[i];
                    Vector3 bPoint = ring[(i + 1) % ring.Count];
                    double ax = tileBoundsM.minX + aPoint.x * tileSize;
                    double ay = tileBoundsM.maxY + aPoint.z * tileSize;
                    double bx = tileBoundsM.minX + bPoint.x * tileSize;
                    double by = tileBoundsM.maxY + bPoint.z * tileSize;
                    double abx = bx - ax;
                    double aby = by - ay;
                    double denom = abx * abx + aby * aby;
                    double t = denom > 0.000001
                        ? System.Math.Max(0d, System.Math.Min(1d,
                            ((viewer.x - ax) * abx + (viewer.y - ay) * aby) / denom))
                        : 0d;
                    double dx = viewer.x - (ax + abx * t);
                    double dy = viewer.y - (ay + aby * t);
                    bestSq = System.Math.Min(bestSq, dx * dx + dy * dy);
                }
            }

            return bestSq < double.MaxValue ? (float)System.Math.Sqrt(bestSq) : 0f;
        }

        private float EstimateNormalizedFeatureDistanceMeters(TileId tile, double normalizedX, double normalizedZ)
        {
            if (_map == null || _map.MapInformation == null)
                return 0f;

            var tileBoundsM = Conversions.TileBoundsInWebMercator(new TileId(tile.Z, tile.X, tile.Y));
            double tileSize = Conversions.TileEdgeSizeInMercator(tile.Z);
            double featureMercatorX = tileBoundsM.minX + normalizedX * tileSize;
            double featureMercatorY = tileBoundsM.maxY + normalizedZ * tileSize;

            Vector2d cullCenterMercator;
            if (_playerCullCenterMercator.HasValue)
            {
                cullCenterMercator = _playerCullCenterMercator.Value;
            }
            else
            {
                var mapboxLatLng = _map.MapInformation.LatitudeLongitude;
                var mapCenterGPS = new LatitudeLongitude(mapboxLatLng.Latitude, mapboxLatLng.Longitude);
                cullCenterMercator = Conversions.LatitudeLongitudeToWebMercator(mapCenterGPS);
            }

            double dx = featureMercatorX - cullCenterMercator.x;
            double dy = featureMercatorY - cullCenterMercator.y;
            return (float)System.Math.Sqrt(dx * dx + dy * dy);
        }

        private Vector3 GetViewerPositionInTile(TileId tile)
        {
            var tileBoundsM = Conversions.TileBoundsInWebMercator(new TileId(tile.Z, tile.X, tile.Y));
            double tileSize = Conversions.TileEdgeSizeInMercator(tile.Z);
            Vector2d viewer;
            if (_playerCullCenterMercator.HasValue)
                viewer = _playerCullCenterMercator.Value;
            else
            {
                var ll = _map.MapInformation.LatitudeLongitude;
                viewer = Conversions.LatitudeLongitudeToWebMercator(new LatitudeLongitude(ll.Latitude, ll.Longitude));
            }
            return new Vector3(
                (float)((viewer.x - tileBoundsM.minX) / tileSize),
                0f,
                (float)((viewer.y - tileBoundsM.maxY) / tileSize));
        }

        public void UpdateForView(TileId tile)
        {
            if (!_initialized) return;
            foreach (var visualizer in _visualizers.Values)
            {
                visualizer.UpdateForView(MapboxTypeBridge.ToMapbox(tile), _mapboxMapInfo);
            }
        }

        private void PublishBuildingStats()
        {
            OvertureMapManager._buildingDetailedCount = Mathf.Max(0, _globalDetailedBuildings);
            OvertureMapManager._buildingSimpleCount = Mathf.Max(0, _globalSimpleBuildings);
            OvertureMapManager._buildingShellCount = Mathf.Max(0, _globalShellBuildings);
            OvertureMapManager._buildingLoadedVertices = Mathf.Max(0,
                _globalDetailedVertices + _globalSimpleVertices + _globalShellVertices);
            int visibleVertices = 0;
            foreach (var pair in _buildingStatsByTile)
            {
                if (_tileVisibility.TryGetValue(pair.Key, out bool visible) && visible)
                    visibleVertices += pair.Value.TotalVertices;
            }
            OvertureMapManager._buildingVisibleVertices = Mathf.Max(0, visibleVertices);
        }

        private void ReleaseBuildingStats(string tileKey)
        {
            if (!_buildingStatsByTile.TryGetValue(tileKey, out var stats) || stats == null) return;
            _globalDetailedBuildings -= stats.detailed;
            _globalSimpleBuildings -= stats.simple;
            _globalShellBuildings -= stats.shell;
            _globalDetailedVertices -= stats.detailedVertices;
            _globalSimpleVertices -= stats.simpleVertices;
            _globalShellVertices -= stats.shellVertices;
            _buildingStatsByTile.Remove(tileKey);
            PublishBuildingStats();
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
            ReleaseBuildingStats(tileKey);

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
                visualizer.UnregisterTile(MapboxTypeBridge.ToMapbox(tile));
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
            _buildingStatsByTile.Clear();
            _globalDetailedBuildings = _globalSimpleBuildings = _globalShellBuildings = 0;
            _globalDetailedVertices = _globalSimpleVertices = _globalShellVertices = 0;
            PublishBuildingStats();
            Debug.Log($"[OvertureVectorRenderer] Cleared {count} tiles properly via UnregisterTile");
        }

        public void ClearBuildingTiles()
        {
            if (!_initialized || !_visualizers.TryGetValue("building", out var buildingVisualizer)) return;

            var keys = new List<string>(_tileObjects.Keys);
            int removedObjects = 0;
            int refreshedTiles = 0;
            foreach (string key in keys)
            {
                string[] parts = key.Split('/');
                if (parts.Length != 3 ||
                    !int.TryParse(parts[0], out int z) ||
                    !int.TryParse(parts[1], out int x) ||
                    !int.TryParse(parts[2], out int y)) continue;

                if (!_tileObjects.TryGetValue(key, out var objects) || objects == null) continue;
                bool hadBuildings = false;
                for (int i = objects.Count - 1; i >= 0; i--)
                {
                    GameObject go = objects[i];
                    if (!IsBuildingObject(go)) continue;
                    hadBuildings = true;
                    objects.RemoveAt(i);
                    if (go != null)
                    {
                        go.SetActive(false);
                        _objectYOffset.Remove(go.transform);
                    }
                    removedObjects++;
                }
                if (!hadBuildings) continue;

                var tile = new TileId(z, x, y);
                buildingVisualizer.UnregisterTile(MapboxTypeBridge.ToMapbox(tile));
                ReleaseBuildingStats(key);
                refreshedTiles++;
            }

            _globalDetailedBuildings = _globalSimpleBuildings = _globalShellBuildings = 0;
            _globalDetailedVertices = _globalSimpleVertices = _globalShellVertices = 0;
            _buildingStatsByTile.Clear();
            PublishBuildingStats();
            Debug.Log($"[OvertureVectorRenderer] Cleared building layer only: {removedObjects} objects across {refreshedTiles} tiles.");
        }

        public void ClearBuildingTile(TileId tile)
        {
            if (!_initialized || !_visualizers.TryGetValue("building", out var buildingVisualizer)) return;
            string key = $"{tile.Z}/{tile.X}/{tile.Y}";
            if (!_tileObjects.TryGetValue(key, out var objects) || objects == null) return;

            int removedObjects = 0;
            for (int i = objects.Count - 1; i >= 0; i--)
            {
                GameObject go = objects[i];
                if (!IsBuildingObject(go)) continue;
                objects.RemoveAt(i);
                if (go != null)
                {
                    go.SetActive(false);
                    _objectYOffset.Remove(go.transform);
                }
                removedObjects++;
            }

            buildingVisualizer.UnregisterTile(MapboxTypeBridge.ToMapbox(tile));
            ReleaseBuildingStats(key);
            Debug.Log($"[OvertureVectorRenderer] Cleared {removedObjects} building objects from tile {key} for incremental LOD refresh.");
        }

        public void RefreshIndividualBuildingFacades()
        {
            float detailRadius = Mathf.Clamp(
                PlayerPrefs.GetFloat("k1lo_buildingDetailRadius", BUILDING_DETAIL_RADIUS_METERS),
                BUILDING_IMMEDIATE_DETAIL_RADIUS_METERS, 1200f);
            float emissiveRadius = Mathf.Clamp(
                PlayerPrefs.GetFloat("k1lo_buildingEmissiveRadius", BUILDING_EMISSIVE_RADIUS_METERS),
                detailRadius, BUILDING_VISIBLE_RADIUS_METERS);
            int scanned = 0, changed = 0;

            foreach (var pair in _tileObjects)
            foreach (GameObject go in pair.Value)
            {
                if (go == null) continue;
                var metadata = go.GetComponent<BuildingMetadata>();
                if (metadata == null || metadata.mergedLodBatch) continue;
                var renderer = go.GetComponent<Renderer>();
                if (renderer == null) continue;
                scanned++;
                Vector3 nearest = renderer.bounds.ClosestPoint(Vector3.zero);
                float distance = new Vector2(nearest.x, nearest.z).magnitude;
                float radius = metadata.generatedLod == ZossBuildingStack.BuildingLODMode.Detailed.ToString()
                    ? detailRadius : emissiveRadius;
                float threshold = metadata.facadeGeometryVisible ? radius + 40f : Mathf.Max(0f, radius - 40f);
                bool shouldShow = distance <= threshold;
                if (metadata.SetFacadeGeometryVisible(shouldShow)) changed++;
            }

            K1L0TileStreamLog.Write("LOD_FACADE_SCAN",
                $"scanned={scanned} changed={changed} detailRadius={detailRadius:F0} emissiveRadius={emissiveRadius:F0}");
        }

        private static bool IsBuildingObject(GameObject go)
        {
            if (go == null) return false;
            if ((go.name ?? string.Empty).IndexOf("building", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            Transform parent = go.transform.parent;
            return parent != null &&
                (parent.name ?? string.Empty).IndexOf("building", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public void PurgePooledEntities()
        {
            int stacks = 0;
            foreach (var visualizer in _visualizers.Values)
            {
                if (visualizer == null || visualizer.GetModStacks == null) continue;
                foreach (var stack in visualizer.GetModStacks.Values)
                {
                    PurgeModifierStackPool(stack);
                    stacks++;
                }
            }
            Debug.Log($"[OvertureVectorRenderer] Purged pooled entities from {stacks} modifier stacks.");
        }

        private static void PurgeModifierStackPool(MapboxModStack stack)
        {
            if (stack == null) return;
            var poolField = typeof(MapboxModStack).GetField("_objectPool", BindingFlags.Instance | BindingFlags.NonPublic);
            var pool = poolField?.GetValue(stack);
            if (pool == null) return;
            var queueField = pool.GetType().GetField("_objects", BindingFlags.Instance | BindingFlags.NonPublic);
            var queue = queueField?.GetValue(pool) as System.Collections.IEnumerable;
            if (queue != null)
            {
                foreach (var pooled in queue)
                {
                    if (pooled == null) continue;
                    var pooledType = pooled.GetType();
                    var mesh = pooledType.GetField("Mesh")?.GetValue(pooled) as Mesh;
                    var gameObject = pooledType.GetField("GameObject")?.GetValue(pooled) as GameObject;
                    if (mesh != null) UnityEngine.Object.Destroy(mesh);
                    if (gameObject != null) UnityEngine.Object.Destroy(gameObject);
                }
            }
            queueField?.FieldType.GetMethod("Clear")?.Invoke(queueField.GetValue(pool), null);
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
            if (K1L0RuntimeDiagnostics.VerboseMapLogs && Time.frameCount % 300 == 0)
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
                        if (K1L0RuntimeDiagnostics.VerboseMapLogs &&
                            Time.frameCount % 300 == 0 && go.name.Contains("LabelAnchor"))
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
                            if (go.name.IndexOf("building", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                bool buildingsVisible = PlayerPrefs.GetFloat("k1lo_buildingsVisible", 1f) >= 0.5f;
                                var metadata = go.GetComponent<BuildingMetadata>();
                                bool runtimeFlattened = metadata != null && metadata.runtimeFlattened;
                                foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
                                {
                                    // BuildingFlattener owns renderer suppression
                                    // while the player's green circle intersects
                                    // this footprint. A frustum/tile transition
                                    // must not resurrect the roof around them.
                                    renderer.forceRenderingOff = !buildingsVisible || runtimeFlattened;
                                    renderer.enabled = buildingsVisible && !runtimeFlattened;
                                }
                            }
                        }
                    }
                }

                // The modifier/material pipeline can re-enable renderers without
                // changing tile visibility. When the diagnostic world toggle is
                // off, enforce suppression on every culling pass rather than
                // only when the tile enters or leaves the frustum.
                if (PlayerPrefs.GetFloat("k1lo_buildingsVisible", 1f) < 0.5f)
                {
                    foreach (var go in objects)
                    {
                        if (go == null ||
                            go.name.IndexOf("building", StringComparison.OrdinalIgnoreCase) < 0)
                            continue;
                        foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
                        {
                            renderer.forceRenderingOff = true;
                            renderer.enabled = false;
                        }
                        // Renderer/material modifiers can replace or re-enable a
                        // renderer after the live command. Disable the Overture
                        // building object itself so no child renderer can draw.
                        if (go.activeSelf)
                            go.SetActive(false);
                    }
                }
            }
            PublishProjectedBuildingStats(mainCamera, frustumPlanes);
            PublishBuildingStats();

            // Log stats every 5 seconds (300 frames at 60fps)
            if (K1L0RuntimeDiagnostics.VerboseMapLogs && Time.frameCount % 300 == 0)
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

        private void PublishProjectedBuildingStats(Camera camera, Plane[] frustumPlanes)
        {
            int visibleRenderers = 0;
            int tinyRenderers = 0;
            int tinyVertices = 0;
            int farRenderers = 0;
            float focalPixels = camera.orthographic
                ? camera.pixelHeight / Mathf.Max(0.01f, camera.orthographicSize * 2f)
                : camera.pixelHeight / (2f * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f));

            foreach (var pair in _tileObjects)
            {
                if (!_tileVisibility.TryGetValue(pair.Key, out bool tileVisible) || !tileVisible || pair.Value == null)
                    continue;

                foreach (var go in pair.Value)
                {
                    if (go == null || !go.activeInHierarchy ||
                        !go.name.Contains("building", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var renderer = go.GetComponent<Renderer>();
                    if (renderer == null || !renderer.enabled ||
                        !GeometryUtility.TestPlanesAABB(frustumPlanes, renderer.bounds))
                        continue;

                    visibleRenderers++;
                    float distance = Vector3.Distance(camera.transform.position, renderer.bounds.center);
                    if (distance > 1000f) farRenderers++;

                    float projectedPixels = renderer.bounds.size.magnitude * focalPixels / Mathf.Max(0.1f, distance);
                    if (projectedPixels < 4f)
                    {
                        tinyRenderers++;
                        var filter = go.GetComponent<MeshFilter>();
                        if (filter != null && filter.sharedMesh != null)
                            tinyVertices += filter.sharedMesh.vertexCount;
                    }
                }
            }

            OvertureMapManager._buildingVisibleRenderers = visibleRenderers;
            OvertureMapManager._buildingTinyRenderers = tinyRenderers;
            OvertureMapManager._buildingTinyVertices = tinyVertices;
            OvertureMapManager._buildingFarRenderers = farRenderers;
        }

        private const int MESH_BATCH_VERTEX_LIMIT = 60000;

        private static void AppendBatchedMesh(List<MapboxMeshData> batches, MapboxMeshData source)
        {
            if (batches == null || source == null || source.Vertices == null || source.Vertices.Count == 0) return;
            MapboxMeshData target = batches.Count > 0 ? batches[batches.Count - 1] : null;
            if (target == null || target.Vertices.Count + source.Vertices.Count > MESH_BATCH_VERTEX_LIMIT)
            {
                target = new MapboxMeshData { Feature = source.Feature, PositionInTile = source.PositionInTile };
                batches.Add(target);
            }

            int vertexOffset = target.Vertices.Count;
            target.Vertices.AddRange(source.Vertices);
            target.Normals.AddRange(source.Normals);
            target.Tangents.AddRange(source.Tangents);
            target.Colors.AddRange(source.Colors);
            foreach (int edgeIndex in source.Edges)
                target.Edges.Add(edgeIndex + vertexOffset);

            while (target.Triangles.Count < source.Triangles.Count)
                target.Triangles.Add(new List<int>());
            for (int submesh = 0; submesh < source.Triangles.Count; submesh++)
                foreach (int index in source.Triangles[submesh])
                    target.Triangles[submesh].Add(index + vertexOffset);

            while (target.UV.Count < source.UV.Count)
                target.UV.Add(new List<Vector2>());
            for (int channel = 0; channel < source.UV.Count; channel++)
                target.UV[channel].AddRange(source.UV[channel]);
        }

        private static string ResolveVisualizerKey(string sourceLayer)
        {
            return LayerMapping.TryGetValue(sourceLayer, out var mapped) ? mapped : sourceLayer;
        }

        /// <summary>Generate extruded line mesh for river centerlines (LINESTRING). Uses same approach as roads.</summary>
        private static void GenerateRiverLineMesh(
            MapboxFeature feature,
            MapboxMeshData md,
            MapboxIMapInfo mapInfo)
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
        private void RegisterPOIWithScanner(MapboxFeature feature, TileId tile)
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
        public static bool TryParseVectorTile(byte[] payload, out global::Mapbox.VectorTile.VectorTile vectorTile)
        {
            vectorTile = null;
            if (payload == null || payload.Length == 0) return false;
            try {
                var decompressed = global::Mapbox.BaseModule.Utilities.Compression.Decompress(payload);
                vectorTile = new global::Mapbox.VectorTile.VectorTile(decompressed);
                return vectorTile.LayerNames()?.Count > 0;
            } catch (Exception ex) {
                Debug.LogError($"[TileDecoder] {ex.GetType().Name}: {ex.Message}");
                return false;
            }
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

    /// <summary>
    /// Adapter that wraps KiloverseMapInfo to satisfy Mapbox's IMapInformation interface.
    /// Required because ConstructLayerVisualizer expects Mapbox types.
    /// </summary>
    internal class MapboxMapInfoAdapter : global::Mapbox.BaseModule.Map.IMapInformation
    {
        private readonly KiloverseMapInfo _map;
        public MapboxMapInfoAdapter(KiloverseMapInfo map) { _map = map; }

        public global::Mapbox.BaseModule.Data.Vector2d.LatitudeLongitude LatitudeLongitude =>
            new global::Mapbox.BaseModule.Data.Vector2d.LatitudeLongitude(_map.Center.Latitude, _map.Center.Longitude);
        public float Pitch => 0f;
        public float Bearing => 0f;
        public float Scale => Conversions.GetTileScaleInMeters((float)_map.Center.Latitude, _map.Zoom);
        public int AbsoluteZoom => _map.Zoom;
        public float Zoom => _map.Zoom;
        public global::Mapbox.BaseModule.Data.Vector2d.Vector2d CenterMercator
        {
            get
            {
                var m = Conversions.LatitudeLongitudeToWebMercator(new LatitudeLongitude(_map.Center.Latitude, _map.Center.Longitude));
                return new global::Mapbox.BaseModule.Data.Vector2d.Vector2d(m.x, m.y);
            }
        }
        public float GetLatitudeCompensationForLocation => (float)System.Math.Cos(_map.Center.Latitude * UnityEngine.Mathf.Deg2Rad);
        public float GetScaleFor(float zoom) => Conversions.GetTileScaleInMeters((float)_map.Center.Latitude, (int)zoom);
        public void Initialize() { }
        public void Initialize(global::Mapbox.BaseModule.Data.Vector2d.LatitudeLongitude ll) { _map.SetPosition(ll.Latitude, ll.Longitude); }
        public void SetLatitudeLongitude(global::Mapbox.BaseModule.Data.Vector2d.LatitudeLongitude ll) { _map.SetPosition(ll.Latitude, ll.Longitude); }
        public void SetInformation(global::Mapbox.BaseModule.Data.Vector2d.LatitudeLongitude? ll, float? zoom = null, float? pitch = null, float? bearing = null, float? scale = null)
        {
            if (ll.HasValue) _map.SetPosition(ll.Value.Latitude, ll.Value.Longitude);
            if (zoom.HasValue) _map.SetZoom((int)zoom.Value);
        }
        public event System.Action<global::Mapbox.BaseModule.Map.IMapInformation> SetView { add { } remove { } }
        public event System.Action<global::Mapbox.BaseModule.Map.IMapInformation> ViewChanged { add { } remove { } }
        public event System.Action<global::Mapbox.BaseModule.Map.IMapInformation> LatitudeLongitudeChanged { add { } remove { } }
        public event System.Action<global::Mapbox.BaseModule.Map.IMapInformation> WorldScaleChanged { add { } remove { } }
        public System.Func<global::Mapbox.BaseModule.Data.Tiles.CanonicalTileId, float, float, float> QueryElevation { get; set; }
    }

    /// <summary>
    /// Converts between Kiloverse and Mapbox types at the visualizer boundary.
    /// </summary>
    internal static class MapboxTypeBridge
    {
        public static MapboxTileId ToMapbox(TileId t) => new MapboxTileId(t.Z, t.X, t.Y);

        public static MapboxMeshData ToMapbox(MeshData k)
        {
            var m = new MapboxMeshData();
            m.Vertices = k.Vertices;
            m.Normals = k.Normals;
            m.Tangents = k.Tangents;
            m.Triangles = k.Triangles;
            m.UV = k.UV;
            m.Colors = k.Colors;
            m.Edges = k.Edges;
            m.PositionInTile = k.PositionInTile;
            // Feature is left null — Mapbox visualizer doesn't need it for CreateGo
            return m;
        }

        public static Dictionary<int, HashSet<MapboxMeshData>> ToMapbox(Dictionary<int, HashSet<MeshData>> dict)
        {
            var result = new Dictionary<int, HashSet<MapboxMeshData>>();
            foreach (var kvp in dict)
            {
                var set = new HashSet<MapboxMeshData>();
                foreach (var md in kvp.Value)
                    set.Add(ToMapbox(md));
                result[kvp.Key] = set;
            }
            return result;
        }
    }
}
