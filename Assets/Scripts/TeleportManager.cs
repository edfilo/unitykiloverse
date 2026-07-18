using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using System;
using System.Runtime.InteropServices;
using Kiloverse.Mapbox;
using KiloWorld.Rendering;
using KiloWorld.Rendering.Systems;

public class TeleportManager : MonoBehaviour
{
    [Header("Map Reference")]
    public KiloverseMapInfo map;
    private bool mapReady;
    private Coroutine pendingTeleport;
    private LatitudeLongitude? lastSimulatedBuildingLodCenter;
    private const double SimulatedBuildingLodRefreshMeters = 100.0;

    [Header("Buttons")]
    public Button teleportMenuButton;
    public GameObject teleportDropdown;
    public Button gpsButton;
    // public Text gpsReadout; // Removed

    [Header("Debug UI")]
    public bool showDebugButtons = false;
    
    [Header("Startup")]
    public bool teleportToHernandezOnStart = false;  // Changed to false - now uses profile startup location

    // Teleport Locations (Dynamic from Profile)
    private System.Collections.Generic.List<TeleportLocation> teleportLocations = new System.Collections.Generic.List<TeleportLocation>();

    private class TeleportLocation
    {
        public string name;
        public LatitudeLongitude coordinates;

        public TeleportLocation(string name, double lat, double lon)
        {
            this.name = name;
            this.coordinates = new LatitudeLongitude(lat, lon);
        }
    }

    [System.Serializable]
    private class NativeLocationModePayload
    {
        public string mode;
        public bool liveGps;
        public string name;
        public double latitude;
        public double longitude;
    }

    private LatitudeLongitude? _startupCoords; // Cached startup coordinates from profile

    private static bool s_receivedFirstMacLocation = false;
    private static double s_firstMacLat = 0;
    private static double s_firstMacLon = 0;

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern IntPtr K1L0CurrentNativeLocationModeJson();
#elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
    [DllImport("K1L0Overlay")]
    private static extern IntPtr K1L0CurrentNativeLocationModeJson();
#endif

    void Awake()
    {
        // Get profile and load startup location from KiloSettings
        var renderManager = FindFirstObjectByType<RenderManager>();

        if (renderManager != null && renderManager.profile != null)
        {
            var profile = renderManager.profile;

            // Load ALL Teleport Locations from Profile
            if (profile.teleportSettings != null && profile.teleportSettings.locations != null)
            {
                teleportLocations.Clear();
                foreach (var location in profile.teleportSettings.locations)
                {
                    if (location != null && !string.IsNullOrEmpty(location.name))
                    {
                        teleportLocations.Add(new TeleportLocation(location.name, location.latitude, location.longitude));
                        Debug.Log($"[TeleportManager] Loaded location: '{location.name}' ({location.latitude}, {location.longitude})");
                    }
                }

                Debug.Log($"[TeleportManager] Loaded {teleportLocations.Count} teleport locations from profile");
            }

            // Get startup coordinates from profile and cache for Start()
            profile.startupLocation.GetStartupCoordinates(out double lat, out double lon);
            _startupCoords = new LatitudeLongitude(lat, lon);

            Debug.Log($"[TeleportManager] Startup location from profile: {profile.startupLocation.startupLocation} ({lat}, {lon})");

            // Find map reference
            if (map == null)
            {
                map = FindFirstObjectByType<KiloverseMapInfo>();
            }

            // On mobile: PREVENT map initialization until GPS succeeds
            if (map != null && Application.isMobilePlatform)
            {
                map.InitializeOnStart = false;
                Debug.Log($"[TeleportManager] Mobile: Prevented map initialization, waiting for GPS in Start()");
            }
        }
        else
        {
            Debug.LogWarning("[TeleportManager] RenderManager or profile not found! Using default location.");
        }
    }

    void Start()
    {
        StartCoroutine(DeferredStart());
    }

    private IEnumerator DeferredStart()
    {
        while (!BootState.AllowTeleport)
        {
            yield return null;
        }

        BootDiagnostics.Mark("TeleportManager.Start");

        // Editor/Desktop: skip heavy UI/bootstrap and just apply startup location.
        if (!Application.isMobilePlatform)
        {
            // Label/render bootstrap still has to come up on desktop.
            if (FindObjectOfType<RuntimeLabelSetup>() == null)
            {
                new GameObject("AutoBootstrapper").AddComponent<RuntimeLabelSetup>().Setup();
            }

            if (map == null)
            {
                map = FindObjectOfType<KiloverseMapInfo>();
            }

            if (map != null)
            {
                mapReady = true;
                if (_startupCoords.HasValue && map.MapInformation != null)
                {
                    float currentZoom = (float)map.MapInformation.Zoom;
                    Debug.Log($"[TeleportManager] Editor mode startup location: {_startupCoords.Value.Latitude}, {_startupCoords.Value.Longitude}");
                    map.MapInformation.SetInformation(_startupCoords.Value, currentZoom);

                    // CRITICAL: Also set player GPS so OvertureMapManager loads tiles at the right coords
                    // KiloFirstPersonController reads from map in Start(), but Start() may run before we set the map
                    var player = FindFirstObjectByType<KiloFirstPersonController>();
                    if (player != null)
                    {
                        player.playerGPS = _startupCoords.Value;
                        Debug.Log($"[TeleportManager] Editor: Set player.playerGPS to startup location for tile loading");
                    }

                    Debug.Log($"[TeleportManager] Editor mode: Applied profile startup location: {_startupCoords.Value.Latitude}, {_startupCoords.Value.Longitude}");
                    if (map.MapboxMap == null)
                    {
                        StartCoroutine(EditorInitializeMap());
                    }
                }
            }

            BootDiagnostics.Mark("TeleportManager editor init done");
            yield break;
        }

        // FORCE BOOTSTRAP UI SYSTEMS
        // Since LabelSystemBootstrapper might be missing from the scene
        BootDiagnostics.Mark("TeleportManager RuntimeLabelSetup begin");
        new GameObject("AutoBootstrapper").AddComponent<RuntimeLabelSetup>().Setup();
        BootDiagnostics.Mark("TeleportManager RuntimeLabelSetup end");

        if (map == null)
        {
            map = FindObjectOfType<KiloverseMapInfo>();
        }

        if (map != null)
        {
            // KiloverseMapInfo is always ready (no async initialization)
            mapReady = true;

            // Apply startup location from profile (Editor only - mobile uses GPS)
            if (_startupCoords.HasValue && !Application.isMobilePlatform && map.MapInformation != null)
            {
                float currentZoom = (float)map.MapInformation.Zoom;
                map.MapInformation.SetInformation(_startupCoords.Value, currentZoom);
                Debug.Log($"[TeleportManager] Editor mode: Applied profile startup location: {_startupCoords.Value.Latitude}, {_startupCoords.Value.Longitude}");
            }
        }

        // Check if we need to create UI
        if (showDebugButtons && teleportMenuButton == null)
        {
            BootDiagnostics.Mark("TeleportManager CreateTeleportUI begin");
            CreateTeleportUI();
            BootDiagnostics.Mark("TeleportManager CreateTeleportUI end");
        }

        // AUTO-GPS BOOT
        // On mobile: Try GPS first; if unavailable, use profile default location
        // In editor: Use profile startup location (Pittsburgh/etc from settings)
        if (Application.isMobilePlatform)
        {
            Debug.Log("[TeleportManager] Mobile: Attempting auto-boot (GPS or default location)...");
            BootDiagnostics.Mark("TeleportManager AutoBootGPS start");
            StartCoroutine(AutoBootGPS());
        }
        else { }
    }

    private IEnumerator EditorInitializeMap()
    {
        BootDiagnostics.Mark("TeleportManager map.Initialize begin");
        // Let other startup tasks breathe for a frame before heavy init.
        yield return null;
        map.Initialize();
        BootDiagnostics.Mark("TeleportManager map.Initialize end");
    }

    IEnumerator AutoBootGPS()
    {
        BootDiagnostics.Mark("AutoBootGPS entered");
        Debug.Log("[TeleportManager] ========== AutoBootGPS() STARTED ==========");
        Debug.Log($"[TeleportManager] Time: {System.DateTime.Now:HH:mm:ss.fff}");

        // Wait for onboarding to complete (if active) - but add timeout to prevent infinite wait
        Debug.Log("[TeleportManager] Checking for OnboardingUI...");
        var onboarding = FindObjectOfType<OnboardingUI>();
        Debug.Log($"[TeleportManager] OnboardingUI found: {onboarding != null}");

        float timeout = 5f;
        while (FindObjectOfType<OnboardingUI>() != null && timeout > 0)
        {
            Debug.Log($"[TeleportManager] Waiting for OnboardingUI to complete... ({timeout:F1}s remaining)");
            yield return new WaitForSeconds(0.1f);
            timeout -= 0.1f;
        }

        if (timeout <= 0)
        {
            Debug.LogWarning("[TeleportManager] OnboardingUI wait timed out after 5 seconds, proceeding");
        }
        else
        {
            Debug.Log("[TeleportManager] ✓ OnboardingUI check complete");
        }

        // Wait for map to be ready first
        while (map == null || !mapReady)
        {
            yield return null;
        }

        if (TryGetStoredFixedNativeLocationMode(out var nativePayload))
        {
            Debug.Log($"[TeleportManager] Native fixed location boot: {nativePayload.mode} ({nativePayload.latitude:F6}, {nativePayload.longitude:F6})");
            ApplyNativeLocationMode(JsonUtility.ToJson(nativePayload));
            yield break;
        }

        // DELEGATE TO GPSLocationController - do NOT call Input.location.Start() here!
        // GPSLocationController handles all GPS initialization with proper iOS event-driven approach
        Debug.Log("[TeleportManager] Waiting for GPSLocationController to initialize GPS...");
        Debug.Log($"[TeleportManager] GPSLocationController.GPSReady = {GPSLocationController.GPSReady}");

        // Wait for GPSLocationController to finish initialization (no timeout - GPS is required)
        const float gpsTimeoutSeconds = 60f;
        float gpsWaitStart = Time.realtimeSinceStartup;
        while (!GPSLocationController.GPSReady)
        {
            if (Time.realtimeSinceStartup - gpsWaitStart > gpsTimeoutSeconds)
            {
                Debug.LogWarning("[TeleportManager] GPS wait timed out - using default location");
                BootDiagnostics.Mark("AutoBootGPS timeout");
                FallbackToProfileStartup();
                yield break;
            }
            Debug.Log("[TeleportManager] GPS not ready yet, waiting... (GPSLocationController is handling initialization)");
            yield return new WaitForSeconds(2f);
        }

        // GPS is now ready - check if we actually have a valid location
        Debug.Log("[TeleportManager] ✓ GPS ready. Status: " + Input.location.status);
        BootDiagnostics.Mark("AutoBootGPS GPS ready");

        if (Input.location.status == LocationServiceStatus.Running)
        {
            Debug.Log("[TeleportManager] GPS Running. Teleporting to GPS location...");
            TeleportToGPS();
        }
        else
        {
            // GPSReady was set but status is not Running (e.g. GPS failed, using fallback)
            Debug.Log("[TeleportManager] GPS unavailable - using default location from profile");
            FallbackToProfileStartup();
        }
    }

    // GPSFailsafe removed - GPS is required, no auto-fallback
    // If GPS fails, user will see error from GPSLocationController

    void FallbackToProfileStartup()
    {
        var renderManager = FindFirstObjectByType<RenderManager>();
        if (renderManager != null && renderManager.profile != null)
        {
            var profile = renderManager.profile;
            profile.startupLocation.GetStartupCoordinates(out double lat, out double lon);
            var coords = new LatitudeLongitude(lat, lon);

            Debug.Log($"[TeleportManager] Fallback: Using profile startup location (GPS unavailable): {profile.startupLocation.startupLocation} ({lat}, {lon})");

            // On mobile: Map was prevented from initializing, set location first, then initialize
            if (Application.isMobilePlatform && map != null && map.MapboxMap == null)
            {
                float currentZoom = map.MapInformation != null ? (float)map.MapInformation.Zoom : 16f;
                map.MapInformation.SetInformation(coords, currentZoom);

                // Update playerGPS before initializing map
                var player = FindObjectOfType<KiloFirstPersonController>();
                if (player != null)
                {
                    player.playerGPS = coords;
                    Debug.Log($"[TeleportManager] Fallback: Updated playerGPS to profile location: ({lat:F6}, {lon:F6})");
                }

                Debug.Log($"[TeleportManager] Fallback: Initializing map at profile location...");
                map.Initialize();
            }
            else
            {
                // Map already initialized, teleport normally
                TeleportToCoordinates(profile.startupLocation.startupLocation.ToString(), lat, lon);
            }
        }
        else
        {
            Debug.LogError("[TeleportManager] Cannot fall back to profile - RenderManager or profile not found!");
        }
    }

    void CreateTeleportUI()
    {
        GameObject canvasGO = GameObject.Find("TeleportCanvas");
        if (canvasGO == null)
        {
            canvasGO = new GameObject("TeleportCanvas");
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // MUCH higher than mobile controls (1)

            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);

            GraphicRaycaster raycaster = canvasGO.AddComponent<GraphicRaycaster>();
            raycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;
        }

        // Force sorting order
        Canvas canvasComp = canvasGO.GetComponent<Canvas>();
        if (canvasComp != null) canvasComp.sortingOrder = 100;

        // REMOVED GPS READOUT (Yellow text) per user request

        // Container at bottom-left for 'T' button
        GameObject container = GameObject.Find("ButtonContainer");
        if (container == null)
        {
            container = new GameObject("ButtonContainer");
            container.transform.SetParent(canvasGO.transform);
            RectTransform rt = container.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0); // Bottom-Left
            rt.anchorMax = new Vector2(0, 0);
            rt.pivot = new Vector2(0, 0);
            rt.anchoredPosition = new Vector2(20, 20); // Bottom-Left corner with padding
            rt.sizeDelta = new Vector2(200, 200); // Square button
        }

        // ... (Cleanup code omitted for brevity, assuming it runs) ...

        // 1. Create Dropdown Panel (Hidden initially)
        if (teleportDropdown == null)
        {
            teleportDropdown = new GameObject("TeleportDropdownPanel");
            teleportDropdown.transform.SetParent(canvasGO.transform, false);

            Image bg = teleportDropdown.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);

            RectTransform rt = teleportDropdown.GetComponent<RectTransform>();
            // Anchor to Bottom-Left, grow up
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(0, 0);
            rt.pivot = new Vector2(0, 0); 
            rt.anchoredPosition = new Vector2(20, 240); // Above the button
            rt.sizeDelta = new Vector2(350, 270);

            // ... (Layout group setup) ...
            VerticalLayoutGroup vlg = teleportDropdown.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(10, 10, 10, 10);
            vlg.spacing = 10;
            vlg.childControlHeight = false;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;

            teleportDropdown.SetActive(false);
        }

        // ... (Dropdown population) ...

        // 2. Create "Teleport" Menu Button (bottom-left corner) - White text only
        if (showDebugButtons && teleportMenuButton == null)
        {
            GameObject btnGO = new GameObject("TeleportButton");
            btnGO.transform.SetParent(container.transform, false);

            // Ensure button fills the container (200x200)
            RectTransform btnRect = btnGO.AddComponent<RectTransform>();
            btnRect.anchorMin = Vector2.zero;
            btnRect.anchorMax = Vector2.one;
            btnRect.offsetMin = Vector2.zero;
            btnRect.offsetMax = Vector2.zero;

            // Transparent background
            Image img = btnGO.AddComponent<Image>();
            img.color = Color.clear;

            Button btn = btnGO.AddComponent<Button>();
            btn.onClick.AddListener(ToggleMenu);
            teleportMenuButton = btn;

            // White text matching "A" button style
            GameObject textGO = new GameObject("Text");
            textGO.transform.SetParent(btnGO.transform, false);
            RectTransform textRT = textGO.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;

            Text txt = textGO.AddComponent<Text>();
            txt.text = "T";
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.fontSize = 100; // Big T
        }

        // 3. REMOVED Standalone GPS Button - now in dropdown menu

        // 4. Create PingButton if it doesn't exist
        if (showDebugButtons && FindObjectOfType<PingButton>() == null)
        {
            GameObject pingButtonGO = new GameObject("PingButton");
            pingButtonGO.AddComponent<PingButton>();
            Debug.Log("[TeleportManager] Created PingButton ('P' button above 'T')");
        }
    }

    void ToggleMenu()
    {
        if (teleportDropdown != null)
            teleportDropdown.SetActive(!teleportDropdown.activeSelf);
    }

    void CreateDropdownItem(Transform parent, string label, UnityEngine.Events.UnityAction action)
    {
        GameObject btnGO = new GameObject("Item_" + label);
        btnGO.transform.SetParent(parent, false);

        Image img = btnGO.AddComponent<Image>();
        img.color = new Color(0.25f, 0.25f, 0.25f);

        Button btn = btnGO.AddComponent<Button>();
        ColorBlock colors = btn.colors;
        colors.normalColor = new Color(0.25f, 0.25f, 0.25f);
        colors.highlightedColor = new Color(0.4f, 0.4f, 0.4f);
        colors.pressedColor = new Color(0.15f, 0.15f, 0.15f);
        btn.colors = colors;
        btn.onClick.AddListener(action);

        LayoutElement le = btnGO.AddComponent<LayoutElement>();
        le.minHeight = 70;
        le.preferredHeight = 70;
        le.flexibleHeight = 0;

        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(btnGO.transform, false);
        RectTransform textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(15, 0);
        textRT.offsetMax = new Vector2(-15, 0);

        Text txt = textGO.AddComponent<Text>();
        txt.text = label;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.white;
        txt.fontSize = 32; // Larger consistent size
        txt.resizeTextForBestFit = true;
        txt.resizeTextMinSize = 16;
        txt.resizeTextMaxSize = 32;
    }

    Button CreateButton(Transform parent, string name, string label, Color color, UnityEngine.Events.UnityAction action)
    {
        GameObject btnGO = new GameObject(name);
        btnGO.transform.SetParent(parent);

        Image img = btnGO.AddComponent<Image>();
        // Use UI/Default sprite to avoid magenta "missing sprite" background
        img.sprite = Resources.Load<Sprite>("UI/Skin/UISprite");
        if (img.sprite == null)
        {
            // Fallback: create a simple white texture as sprite
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            img.sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), Vector2.one * 0.5f);
        }
        img.color = color;
        img.type = Image.Type.Sliced;

        Button btn = btnGO.AddComponent<Button>();
        btn.onClick.AddListener(() =>
        {
            Debug.Log($"[TeleportManager] Button clicked: {name}");
            action();
        });

        RectTransform rt = btnGO.GetComponent<RectTransform>();
        // rt.sizeDelta is now controlled by the LayoutGroup, so we don't need to set it manually
        // But we can set a min size if we wanted to add a LayoutElement component.
        
        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(btnGO.transform);
        RectTransform textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;

        Text txt = textGO.AddComponent<Text>();
        txt.text = label;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.black;
        txt.fontSize = 28;
        txt.resizeTextForBestFit = true;
        txt.resizeTextMinSize = 10;
        txt.resizeTextMaxSize = 40;

        return btn;
    }

    public void TeleportToCoordinates(string name, double lat, double lon)
    {
        if (map == null)
        {
            Debug.LogError("[TeleportManager] Map is NULL!");
            return;
        }

        LatitudeLongitude coords = new LatitudeLongitude(lat, lon);

        if (Application.isMobilePlatform && map.MapboxMap == null)
        {
            Debug.Log($"[TeleportManager] Initializing mobile map at fixed location {name}: {lat}, {lon}");
            float currentZoom = map.MapInformation != null ? (float)map.MapInformation.Zoom : 16f;
            map.MapInformation.SetInformation(coords, currentZoom);

            var player = FindObjectOfType<KiloFirstPersonController>();
            if (player != null)
            {
                player.playerGPS = coords;
                player.transform.position = new Vector3(0, 2f, 0);
                Debug.Log($"[TeleportManager] Fixed boot: Updated playerGPS to ({lat:F6}, {lon:F6}) before map init");
            }

            ClearLocationMemoryForJump();
            map.Initialize();
            mapReady = true;
            return;
        }
        
        if (!mapReady || map.MapboxMap == null)
        {
            Debug.LogWarning("[TeleportManager] Map not ready yet, waiting...");
            if (pendingTeleport == null)
            {
                pendingTeleport = StartCoroutine(WaitAndTeleport(coords));
            }
            return;
        }

        Debug.Log($"[TeleportManager] Teleporting to {name}: {lat}, {lon}");
        LogNearbyPOIs();
        UpdateMapLocation(coords);
    }

    public void ApplyNativeLocationMode(string json)
    {
        NativeLocationModePayload payload = null;
        try
        {
            payload = JsonUtility.FromJson<NativeLocationModePayload>(json);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[TeleportManager] ApplyNativeLocationMode parse failed: {ex.Message}");
            return;
        }

        if (payload == null)
        {
            Debug.LogWarning("[TeleportManager] ApplyNativeLocationMode missing payload.");
            return;
        }

        if (payload.liveGps || string.Equals(payload.mode, "live", System.StringComparison.OrdinalIgnoreCase))
        {
            GPSLocationController.GPSDisabled = false;
            ClearLocationMemoryForJump();
            Debug.Log("[TeleportManager] Native location mode: live GPS");
            if (Application.isMobilePlatform)
                TeleportToGPS();
            return;
        }

        if (double.IsNaN(payload.latitude) || double.IsNaN(payload.longitude) ||
            (Mathf.Approximately((float)payload.latitude, 0f) && Mathf.Approximately((float)payload.longitude, 0f)))
        {
            Debug.LogWarning($"[TeleportManager] ApplyNativeLocationMode invalid coordinates mode={payload.mode}");
            return;
        }

        GPSLocationController.GPSDisabled = true;
        if (Application.isMobilePlatform && Input.location.status != LocationServiceStatus.Stopped)
            Input.location.Stop();
        ClearLocationMemoryForJump();
        TeleportToCoordinates(string.IsNullOrEmpty(payload.name) ? payload.mode : payload.name, payload.latitude, payload.longitude);
    }

    public void ApplyNativeSimulatedLocation(string json)
    {
        NativeLocationModePayload payload = null;
        try
        {
            payload = JsonUtility.FromJson<NativeLocationModePayload>(json);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[TeleportManager] ApplyNativeSimulatedLocation parse failed: {ex.Message}");
            return;
        }

        if (payload == null ||
            payload.liveGps ||
            double.IsNaN(payload.latitude) ||
            double.IsNaN(payload.longitude) ||
            (Mathf.Approximately((float)payload.latitude, 0f) && Mathf.Approximately((float)payload.longitude, 0f)))
        {
            return;
        }

        GPSLocationController.GPSDisabled = true;
        var target = new LatitudeLongitude(payload.latitude, payload.longitude);
        bool refreshBuildingLods = ShouldRefreshSimulatedBuildingLods(target);
        var player = FindObjectOfType<KiloFirstPersonController>();
        if (player != null)
        {
            player.playerGPS = target;
            player.transform.position = new Vector3(0f, player.transform.position.y, 0f);
        }

        if (map != null)
        {
            if (refreshBuildingLods)
            {
                // Building detail/simple/shell allocation happens while a tile
                // is generated. Reusing the same tiles after moving the test
                // player therefore leaves LOD centered on the old position.
                // Refresh only after meaningful movement to avoid churn while
                // nudging the live test location.
                var overture = FindFirstObjectByType<OvertureMapManager>();
                overture?.ClearLoadedTilesForLocationJump();
                lastSimulatedBuildingLodCenter = target;
                Debug.Log($"[TeleportManager] Refreshed building LOD after simulated move to ({target.Latitude:F6}, {target.Longitude:F6})");
            }
            map.SetPosition(target.Latitude, target.Longitude);
        }
    }

    private bool ShouldRefreshSimulatedBuildingLods(LatitudeLongitude target)
    {
        if (!lastSimulatedBuildingLodCenter.HasValue)
            return true;

        var previous = Conversions.LatitudeLongitudeToWebMercator(lastSimulatedBuildingLodCenter.Value);
        var next = Conversions.LatitudeLongitudeToWebMercator(target);
        double dx = next.x - previous.x;
        double dy = next.y - previous.y;
        return Math.Sqrt(dx * dx + dy * dy) >= SimulatedBuildingLodRefreshMeters;
    }

    public static bool StoredNativeLocationModeIsFixed()
    {
        return TryGetStoredFixedNativeLocationMode(out _);
    }

    private static bool TryGetStoredFixedNativeLocationMode(out NativeLocationModePayload payload)
    {
        payload = null;
#if (UNITY_IOS || UNITY_STANDALONE_OSX) && !UNITY_EDITOR
        IntPtr ptr = IntPtr.Zero;
        try
        {
            ptr = K1L0CurrentNativeLocationModeJson();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[TeleportManager] Native location mode query failed: {ex.Message}");
            return false;
        }

        if (ptr == IntPtr.Zero) return false;
        string json = Marshal.PtrToStringAnsi(ptr);
        if (string.IsNullOrWhiteSpace(json)) return false;

        try
        {
            payload = JsonUtility.FromJson<NativeLocationModePayload>(json);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[TeleportManager] Stored native location parse failed: {ex.Message}");
            payload = null;
            return false;
        }

        if (payload == null || payload.liveGps || string.Equals(payload.mode, "live", StringComparison.OrdinalIgnoreCase))
            return false;

        return !double.IsNaN(payload.latitude)
            && !double.IsNaN(payload.longitude)
            && !(Mathf.Approximately((float)payload.latitude, 0f) && Mathf.Approximately((float)payload.longitude, 0f));
#else
        return false;
#endif
    }

    public void TeleportToLocation(int index)
    {
        if (index < 0 || index >= teleportLocations.Count)
        {
            Debug.LogError($"[TeleportManager] Invalid location index: {index}");
            return;
        }

        var location = teleportLocations[index];
        Debug.Log($"[TeleportManager] TeleportToLocation ({index}): '{location.name}' called");

        if (map == null)
        {
            Debug.LogError("[TeleportManager] Map is NULL!");
            return;
        }

        if (!mapReady || map.MapboxMap == null)
        {
            Debug.LogWarning("[TeleportManager] Map not ready yet, waiting...");
            if (pendingTeleport == null)
            {
                pendingTeleport = StartCoroutine(WaitAndTeleport(location.coordinates));
            }
            return;
        }

        Debug.Log($"[TeleportManager] Teleporting to {location.name}: {location.coordinates.Latitude}, {location.coordinates.Longitude}");
        LogNearbyPOIs();
        UpdateMapLocation(location.coordinates);
    }

        public void TeleportToGPS()
        {
            Debug.Log("[TeleportManager] TeleportToGPS() called");

            if (!Input.location.isEnabledByUser)
            {
                Debug.LogWarning("[TeleportManager] Location services disabled by user.");
                return;
            }

            if (Input.location.status == LocationServiceStatus.Running)
            {
                var loc = Input.location.lastData;
                var latLon = new LatitudeLongitude(loc.latitude, loc.longitude);
                Debug.Log($"[TeleportManager] GPS SUCCESS! Live GPS: {latLon.Latitude}, {latLon.Longitude}");

                // On mobile first boot: Map was prevented from initializing
                // Set location first, THEN initialize map (prevents loading Pittsburgh tiles)
                if (Application.isMobilePlatform && map != null && map.MapboxMap == null)
                {
                    float currentZoom = map.MapInformation != null ? (float)map.MapInformation.Zoom : 16f;
                    map.MapInformation.SetInformation(latLon, currentZoom);

                    // Update playerGPS before initializing map (critical for tile loading!)
                    var player = FindObjectOfType<KiloFirstPersonController>();
                    if (player != null)
                    {
                        player.playerGPS = latLon;
                        Debug.Log($"[TeleportManager] GPS: Updated playerGPS to GPS location: ({latLon.Latitude:F6}, {latLon.Longitude:F6})");
                    }

                    Debug.Log($"[TeleportManager] GPS: Initializing map at GPS location (NO Pittsburgh tiles!)");
                    map.Initialize();
                }
                else
                {
                    // Map already initialized, teleport normally
                    LogNearbyPOIs();
                    UpdateMapLocation(latLon);
                }
            }
            else
            {
                Debug.LogWarning($"[TeleportManager] Location service not running. Status: {Input.location.status}");
                if (Input.location.status == LocationServiceStatus.Stopped)
                {
                    Input.location.Start();
                }
            }
        }
    
        private void LogNearbyPOIs()    {
        Debug.Log("\n========== NEARBY POI DUMP ==========");
        
        var player = GameObject.FindFirstObjectByType<KiloFirstPersonController>();
        Vector3 playerPos = player != null ? player.transform.position : Vector3.zero;
        
        // Find the POI layer parent
        var poiLayerParent = GameObject.Find("poi_label layer objects");
        if (poiLayerParent == null)
        {
            Debug.LogWarning("[TeleportManager] POI layer not loaded yet (this is normal on startup)");
            return;
        }
        
        // Get all child POIs
        var poiObjects = new System.Collections.Generic.List<GameObject>();
        for (int i = 0; i < poiLayerParent.transform.childCount; i++)
        {
            var child = poiLayerParent.transform.GetChild(i).gameObject;
            if (child.name.StartsWith("poi_label") && child.activeInHierarchy)
            {
                poiObjects.Add(child);
            }
        }
        
        Debug.Log($"Found {poiObjects.Count} active POI objects");
        
        poiObjects.Sort((a, b) => 
        {
            float distA = Vector3.Distance(playerPos, a.transform.position);
            float distB = Vector3.Distance(playerPos, b.transform.position);
            return distA.CompareTo(distB);
        });
        
        int count = Mathf.Min(20, poiObjects.Count);
        for (int i = 0; i < count; i++)
        {
            var poi = poiObjects[i];
            float distance = Vector3.Distance(playerPos, poi.transform.position);
            
            // Try to get POI name from TextMeshPro label
            string poiName = poi.name;
            var label = poi.transform.Find("Label_" + poi.name.Replace("poi_label ", ""));
            if (label != null)
            {
                var tmp = label.GetComponent<TMPro.TextMeshPro>();
                if (tmp != null) poiName = tmp.text;
            }
            
            Debug.Log($"POI #{i+1}: '{poiName}' | {distance:F0}m | {poi.name}");
        }
        
        Debug.Log("========================================\n");
    }

    
    private void UpdateMapLocation(LatitudeLongitude target)
    {
        Debug.Log($"[TeleportManager] UpdateMapLocation called with: {target.Latitude}, {target.Longitude}");

        if (map == null)
        {
            Debug.LogError("[TeleportManager] Map is NULL in UpdateMapLocation!");
            return;
        }

        ClearLocationMemoryForJump();

        // 1. Reset Player Position to (0,0,0) AND UPDATE playerGPS
        // CRITICAL: playerGPS must be updated or OvertureMapManager will load tiles for old location!
        CharacterController cc = null;
        var player = FindObjectOfType<KiloFirstPersonController>();
        if (player != null)
        {
            cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.transform.position = new Vector3(0, 2f, 0);

            // UPDATE playerGPS to new location (source of truth for tile loading)
            player.playerGPS = target;
            Debug.Log($"[TeleportManager] Updated playerGPS to: ({target.Latitude:F6}, {target.Longitude:F6})");

            if (cc != null) cc.enabled = true;
        }
        else
        {
            cc = FindObjectOfType<CharacterController>();
            if (cc != null)
            {
                cc.enabled = false;
                cc.transform.position = new Vector3(0, 2f, 0);
                cc.enabled = true;
            }
        }

        // 2. Update Map Location
        float currentZoom = (float)map.MapInformation.Zoom;
        Debug.Log($"[TeleportManager] Setting map to: {target.Latitude}, {target.Longitude} at zoom {currentZoom}");
        
        map.MapInformation.SetInformation(target, currentZoom);
        
        // 3. Force reload
        Debug.Log("[TeleportManager] Calling LoadMapView()...");
        map.MapboxMap.LoadMapView();

        // Vector module positioning update removed — Mapbox SDK eliminated, OvertureMapManager handles tile updates
        Debug.Log("[TeleportManager] Map view reloaded (OvertureMapManager handles tile repositioning)");

        Debug.Log("[TeleportManager] ✓ Teleport complete!");

        // 5. Force Collider Scan for new buildings
        if (BuildingColliderManager.Instance != null)
        {
            BuildingColliderManager.Instance.ForceUpdate();
        }
    }

    private void ClearLocationMemoryForJump()
    {
        var scanner = TransmitterScanner.Instance;
        if (scanner != null)
            scanner.ClearAll();

        var director = SignalDirectorV2.Instance;
        if (director != null)
            director.ApplyNativeWorldNearby("{\"ok\":true,\"includeBeams\":true,\"beams\":[]}");

        var overture = FindFirstObjectByType<OvertureMapManager>();
        if (overture != null)
            overture.ClearLoadedTilesForLocationJump();
    }

    private IEnumerator WaitAndTeleport(LatitudeLongitude target)
    {
        // KiloverseMapInfo is always ready (no async initialization)
        while (map == null || map.MapboxMap == null)
        {
            yield return null;
        }
        mapReady = true;
        pendingTeleport = null;
        UpdateMapLocation(target);
    }
}
