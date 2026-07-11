using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine.Networking;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using KiloWorld.Rendering.Systems;
using KiloWorld.UI.Stories;

public class K1L0HUD : MonoBehaviour
{
    private static readonly string[] ManualWeatherGlyphs =
    {
        "clear",
        "partly cloudy",
        "cloudy",
        "overcast",
        "rain",
        "snow",
        "fog",
        "storm"
    };

    private Canvas canvas;
    private RectTransform safeArea;
    private TextMeshProUGUI weatherText;
    private TextMeshProUGUI memText;
    private Image terminalToggleBg;
    private TextMeshProUGUI terminalToggleGlyph;
    private Button transmitButton;
    private bool hudUserVisible = true;

    public static K1L0HUD Instance { get; private set; }

    /// <summary>Set the city/weather text directly from ping response.</summary>
    public void SetWeatherText(string city, string weatherInfo)
    {
        Debug.Log($"[K1L0HUD] SetWeatherText called: city='{city}', weather='{weatherInfo}', weatherText null={weatherText == null}");
        if (weatherText == null) return;
        string display = "";
        if (!string.IsNullOrEmpty(city)) display = city;
        if (!string.IsNullOrEmpty(weatherInfo))
            display = string.IsNullOrEmpty(display) ? weatherInfo : $"{display} · {weatherInfo}";
        if (!string.IsNullOrEmpty(display))
        {
            weatherText.text = display.ToUpper();
            Debug.Log($"[K1L0HUD] Set weatherText to: '{weatherText.text}'");
        }
    }

    private K1L0Dock dock;

    // Each mode has its own panel
    private K1L0GlassPanel nearbyPanel;
    private K1L0GlassPanel codexPanel;
    private K1L0GlassPanel profilePanel;
    private K1L0GlassPanel transmitterPanel;
    private K1L0NearbyMode nearbyMode;
    private K1L0CodexMode codexMode;
    private K1L0StatusMode statusMode;
    private K1L0ProfileMode profileMode;
    private TransmitterEnterModal transmitterMode;
    private K1L0GlassPanel[] panels;
    private bool[] panelOpen = new bool[4];

    private TMP_FontAsset monoFont;
    private TMP_FontAsset monoFontLight;
    private bool initialized;
    private Image sceneDimmer;
    private float dimmerTarget;
    private const string PanelMapBrightnessPref = "k1lo_panelMapBrightness.v2";
    private const float DimmerSpeed = 4f;
    private float lastDockDebugUploadTime = -999f;
    private float lastNativeStepStatePushTime = -999f;
    private string lastNativeStepStateSignature = "";

    public static Sprite RoundedRectSprite { get; private set; }
    public static float PanelMapBrightness => Mathf.Clamp01(PlayerPrefs.GetFloat(PanelMapBrightnessPref, 0.34f));
    private static bool NativeOverlayOwnsHud
    {
        get
        {
#if (UNITY_IOS || UNITY_STANDALONE_OSX) && !UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }
    }
    public static bool IsSurveillanceCameraOn => Instance != null && Instance._mapVisible;

    void Awake()
    {
        Instance = this;
        if (gameObject.name != "K1L0HUD")
            gameObject.name = "K1L0HUD";
        SubscribeNativeTransmissionResults();
        SubscribeNativeAuth();
    }

    void OnDestroy()
    {
        UnsubscribeNativeTransmissionResults();
        UnsubscribeNativeAuth();
    }

    // ---- Native transmission-result bridge -----------------------------------------------
    // Forward TransmissionManager.OnTransmissionReady to the native Swift overlay so the
    // mobile user sees image+music+lyrics inside the K1L0 HUD without leaving native UI.
    // The Swift entry point K1L0DeliverTransmissionResult is defined in K1L0WeatherOverlay.swift.

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void K1L0DeliverTransmissionResult(string json);
    [DllImport("__Internal")] private static extern void K1L0DeliverUserMetadataSaveResult(string json);
    [DllImport("__Internal")] private static extern void K1L0DeliverNativeAuthState(string json);
    [DllImport("__Internal")] private static extern void K1L0DeliverStepState(string json);
#elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
    [DllImport("K1L0Overlay")] private static extern void K1L0DeliverTransmissionResult(string json);
    [DllImport("K1L0Overlay")] private static extern void K1L0DeliverUserMetadataSaveResult(string json);
    [DllImport("K1L0Overlay")] private static extern void K1L0DeliverNativeAuthState(string json);
    [DllImport("K1L0Overlay")] private static extern void K1L0DeliverStepState(string json);
#else
    private static void K1L0DeliverTransmissionResult(string json) { /* no-op in editor */ }
    private static void K1L0DeliverUserMetadataSaveResult(string json) { /* no-op in editor */ }
    private static void K1L0DeliverNativeAuthState(string json) { /* no-op in editor */ }
    private static void K1L0DeliverStepState(string json) { /* no-op in editor */ }
#endif

    private bool nativeTransmissionSubscribed;
    private bool nativeAuthSubscribed;

    private void SubscribeNativeTransmissionResults()
    {
        if (nativeTransmissionSubscribed) return;
        if (TransmissionManager.Instance == null)
        {
            StartCoroutine(WaitForTransmissionManager());
            return;
        }
        TransmissionManager.Instance.OnTransmissionReady += HandleTransmissionReady;
        nativeTransmissionSubscribed = true;
    }

    private void SubscribeNativeAuth()
    {
        if (nativeAuthSubscribed) return;
        if (FirebaseAuthManager.Instance != null)
        {
            FirebaseAuthManager.Instance.OnAuthStateChanged += HandleNativeAuthStateChanged;
            FirebaseAuthManager.Instance.OnAuthError += HandleNativeAuthError;
        }
        if (AppleSignInHandler.Instance != null)
        {
            AppleSignInHandler.Instance.OnAppleSignInSuccess += HandleNativeAppleSignInSuccess;
            AppleSignInHandler.Instance.OnAppleSignInFailed += HandleNativeAppleSignInFailed;
        }
        nativeAuthSubscribed = true;
        SendNativeAuthState("auth ready.");
    }

    private IEnumerator WaitForTransmissionManager()
    {
        while (TransmissionManager.Instance == null) yield return null;
        if (!nativeTransmissionSubscribed)
        {
            TransmissionManager.Instance.OnTransmissionReady += HandleTransmissionReady;
            nativeTransmissionSubscribed = true;
        }
    }

    private void UnsubscribeNativeTransmissionResults()
    {
        if (!nativeTransmissionSubscribed) return;
        if (TransmissionManager.Instance != null)
            TransmissionManager.Instance.OnTransmissionReady -= HandleTransmissionReady;
        nativeTransmissionSubscribed = false;
    }

    private void UnsubscribeNativeAuth()
    {
        if (!nativeAuthSubscribed) return;
        if (FirebaseAuthManager.Instance != null)
        {
            FirebaseAuthManager.Instance.OnAuthStateChanged -= HandleNativeAuthStateChanged;
            FirebaseAuthManager.Instance.OnAuthError -= HandleNativeAuthError;
        }
        if (AppleSignInHandler.Instance != null)
        {
            AppleSignInHandler.Instance.OnAppleSignInSuccess -= HandleNativeAppleSignInSuccess;
            AppleSignInHandler.Instance.OnAppleSignInFailed -= HandleNativeAppleSignInFailed;
        }
        nativeAuthSubscribed = false;
    }

    [Serializable]
    private struct NativeTransmissionResultPayload
    {
        public string status;
        public string imageUrl;
        public string videoUrl;
        public string audioUrl;
        public string lyrics;
        public string responsePlot;
        public string[] responseOptions;
    }

    [Serializable]
    private struct NativeAuthStatePayload
    {
        public string userId;
        public string email;
        public string displayName;
        public bool isAuthenticated;
        public string status;
    }

    private static void DeliverNativeTransmissionStatus(string status, string message)
    {
        try
        {
            var payload = new NativeTransmissionResultPayload
            {
                status = status ?? "",
                imageUrl = "",
                videoUrl = "",
                audioUrl = "",
                lyrics = "",
                responsePlot = message ?? "",
                responseOptions = Array.Empty<string>(),
            };
            K1L0DeliverTransmissionResult(JsonUtility.ToJson(payload));
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[K1L0HUD] native transmission status deliver failed: {ex.Message}");
        }
    }

    private void HandleTransmissionReady(TransmissionData data)
    {
        if (data == null) return;
        var payload = new NativeTransmissionResultPayload
        {
            status = data.hasVideo ? "video_ready" : (data.hasImage ? "image_ready" : "received"),
            imageUrl = data.imageUrl ?? "",
            videoUrl = data.videoUrl ?? "",
            audioUrl = data.audioUrl ?? "",
            // Forwards-compatible: TransmissionData.lyrics will surface here once the backend
            // populates it. Until then this field is empty and the Swift sheet shows a placeholder.
            lyrics = TryReadLyrics(data),
            responsePlot = data.responsePlot ?? "",
            responseOptions = data.responseOptions ?? Array.Empty<string>(),
        };
        try
        {
            string json = JsonUtility.ToJson(payload);
            K1L0DeliverTransmissionResult(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[K1L0HUD] native transmission deliver failed: {e.Message}");
        }
    }

    private static string TryReadLyrics(TransmissionData data)
    {
        // Read via reflection so this compiles cleanly whether or not TransmissionData has a
        // `lyrics` field yet — when the backend starts returning lyrics and TransmissionData
        // exposes them, this picks them up with no code change here.
        var field = data.GetType().GetField("lyrics");
        if (field != null && field.FieldType == typeof(string))
            return (field.GetValue(data) as string) ?? "";
        return "";
    }

    public static void SetPanelMapBrightness(float value)
    {
        PlayerPrefs.SetFloat(PanelMapBrightnessPref, Mathf.Clamp01(value));
        PlayerPrefs.Save();
        if (Instance != null) Instance.RefreshPanelDimmer();
    }

    void Start()
    {
        if (initialized) return;
#if UNITY_EDITOR
        DoInitialize();
#else
        StartCoroutine(InitializeHUD());
#endif
    }

    IEnumerator InitializeHUD()
    {
        float waited = 0f;
        while (!BootState.AllowPlayer && waited < 10f)
        {
            yield return new WaitForSecondsRealtime(0.25f);
            waited += 0.25f;
        }
        yield return null;
        DoInitialize();
    }

    private float _lastWeatherPoll = -999f;

    void Update()
    {
        if (!oldUIHidden)
            TryHideOldUI();

        if (NativeOverlayOwnsHud)
        {
            PushNativeStepStateIfNeeded();
            if (K1L0CanvasRoot.HUD != null && K1L0CanvasRoot.HUD.gameObject.activeSelf)
                K1L0CanvasRoot.HUD.gameObject.SetActive(false);
            return;
        }

        HandleDockTouchFallback();

        if (sceneDimmer != null)
        {
            Color c = sceneDimmer.color;
            float current = c.a;
            if (!Mathf.Approximately(current, dimmerTarget))
            {
                c.a = Mathf.MoveTowards(current, dimmerTarget, DimmerSpeed * Time.deltaTime);
                sceneDimmer.color = c;
            }
        }

        // Poll city/weather from Update (coroutines unreliable in this project)
        if (weatherText != null && Time.realtimeSinceStartup - _lastWeatherPoll > 5f)
        {
            _lastWeatherPoll = Time.realtimeSinceStartup;
            PollWeatherText();
        }
    }

    void PollWeatherText()
    {
        string result = null;

        var wv = FindFirstObjectByType<WeatherView>();
        if (wv != null)
        {
            string txt = wv.GetDisplayText();
            if (!string.IsNullOrEmpty(txt) && txt != "Finding location...")
                result = txt;
        }

        if (result == null)
        {
            var ll = FindFirstObjectByType<LocationLabelUI>();
            if (ll != null)
            {
                string city = ll.GetCityName();
                if (!string.IsNullOrEmpty(city))
                    result = city;
            }
        }

        Debug.Log($"[K1L0HUD] PollWeatherText: result='{result}', weatherText null={weatherText == null}, wv exists={wv != null}");
        if (result != null && weatherText != null)
            weatherText.text = result.ToUpper();
    }

    void PushNativeStepStateIfNeeded()
    {
#if (UNITY_IOS || UNITY_STANDALONE_OSX) && !UNITY_EDITOR
        if (Time.realtimeSinceStartup - lastNativeStepStatePushTime < 0.5f)
            return;

        var pedometer = FindFirstObjectByType<PedometerService>();
        if (pedometer == null)
            return;

        int liveSteps = Mathf.Max(0, pedometer.walkWindowSteps > 0 ? pedometer.walkWindowSteps : pedometer.stepCount);
        int steps24h = Mathf.Max(0, pedometer.stepsLast24Hours);
        int steps7d = Mathf.Max(0, pedometer.stepsLast7Days);
        double latitude = double.NaN;
        double longitude = double.NaN;
        var player = FindFirstObjectByType<KiloFirstPersonController>();
        if (player != null)
        {
            latitude = player.playerGPS.Latitude;
            longitude = player.playerGPS.Longitude;
        }

        string latText = double.IsNaN(latitude) ? "null" : latitude.ToString("R", CultureInfo.InvariantCulture);
        string lonText = double.IsNaN(longitude) ? "null" : longitude.ToString("R", CultureInfo.InvariantCulture);
        string locationSignature = double.IsNaN(latitude) || double.IsNaN(longitude)
            ? "noloc"
            : $"{Math.Round(latitude, 5).ToString(CultureInfo.InvariantCulture)}|{Math.Round(longitude, 5).ToString(CultureInfo.InvariantCulture)}";
        string signature = $"{liveSteps}|{steps24h}|{steps7d}|{locationSignature}";
        if (signature == lastNativeStepStateSignature)
            return;

        lastNativeStepStatePushTime = Time.realtimeSinceStartup;
        lastNativeStepStateSignature = signature;
        string json = $"{{\"liveSteps\":{liveSteps},\"steps24h\":{steps24h},\"steps7d\":{steps7d},\"latitude\":{latText},\"longitude\":{lonText}}}";
        try { K1L0DeliverStepState(json); }
        catch (Exception e) { Debug.LogWarning($"[K1L0HUD] Native step state push failed: {e.Message}"); }
#endif
    }

    void DoInitialize()
    {
        if (initialized) return;
        initialized = true;
        Instance = this;
        monoFont = Resources.Load<TMP_FontAsset>("Fonts/IBMPlexMono-Regular SDF");
        monoFontLight = Resources.Load<TMP_FontAsset>("Fonts/IBMPlexMono-Light SDF");
        if (monoFont == null)
            monoFont = Resources.Load<TMP_FontAsset>("Fonts/Inter-Regular SDF");
        if (monoFontLight == null)
            monoFontLight = monoFont;
        if (monoFont == null)
        {
            monoFont = TMP_Settings.defaultFontAsset;
            monoFontLight = monoFont;
        }

        RoundedRectSprite = K1L0GlassFactory.RoundedRectSprite;

        K1L0SceneCapture.EnsureExists();
        UseSharedCanvas();
        EnsureEventSystem();

        if (NativeOverlayOwnsHud)
        {
            EnsureScreenshot();
            HideOldUI();
            if (K1L0CanvasRoot.HUD != null)
                K1L0CanvasRoot.HUD.gameObject.SetActive(false);
            Debug.Log("[K1L0HUD] Native overlay owns HUD; legacy Unity HUD visuals disabled");
            return;
        }

        CreateSceneDimmer();
        CreateWeatherBar();
        CreateDock();
        CreateTerminalHudToggle();
        CreateSurveillanceCamBadge();
        CreatePanels();
        CreateTransmitStatusCard();

        EnsureStoriesUI();
        EnsureScreenshot();
        // Disabled: legacy POI location beams (K1L0LocationBeams).
        // Locations are now represented as SignalDirectorV2 LocationTransmission signals instead.
        EnsureTransmissionFrame();
        HideOldUI();
        Debug.Log("[K1L0HUD] Initialized successfully");

        TogglePanel(1, true);
        Debug.Log("[K1L0HUD] Auto-opened codex panel");
#if UNITY_EDITOR
        demoTimer = Time.realtimeSinceStartup + 0.01f;
#endif
    }

    void UseSharedCanvas()
    {
        canvas = K1L0CanvasRoot.HUDCanvas;
        safeArea = K1L0CanvasRoot.HUD;
        Debug.Log("[K1L0HUD] Using K1L0CanvasRoot.HUD");
    }

    void EnsureEventSystem()
    {
        EventSystem eventSystem = EventSystem.current;

        if (eventSystem == null)
        {
            GameObject existing = GameObject.Find("EventSystem");
            if (existing != null)
                eventSystem = existing.GetComponent<EventSystem>();
        }

        if (eventSystem == null)
        {
            GameObject eventSystemGO = new GameObject("EventSystem");
            eventSystem = eventSystemGO.AddComponent<EventSystem>();
        }

        EnsureInputModules(eventSystem.gameObject);
    }

    void EnsureInputModules(GameObject eventSystemGO)
    {
#if ENABLE_INPUT_SYSTEM
        if (eventSystemGO.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>() == null)
            eventSystemGO.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#endif

        if (eventSystemGO.GetComponent<StandaloneInputModule>() == null)
            eventSystemGO.AddComponent<StandaloneInputModule>();
    }

    void DestroyExistingCanvas()
    {
        Transform existing = transform.Find("K1L0_Canvas");
        if (existing == null)
            return;

#if UNITY_EDITOR
        DestroyImmediate(existing.gameObject);
#else
        Destroy(existing.gameObject);
#endif
    }

    void CreateSceneDimmer()
    {
        GameObject go = new GameObject("SceneDimmer");
        go.transform.SetParent(canvas.transform, false);
        go.transform.SetAsFirstSibling();
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        sceneDimmer = go.AddComponent<Image>();
        sceneDimmer.color = new Color(0f, 0f, 0f, 0f);
        sceneDimmer.raycastTarget = false;
    }

    void CreateWeatherBar()
    {
        // CityWeatherOverlay is now the single owner of the top city/weather row.
        // Keeping a second WeatherBar here caused duplicate top-left HUD text.
        weatherText = null;
    }

    void CreateMemLabel()
    {
        GameObject go = new GameObject("MemLabel");
        go.transform.SetParent(safeArea, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(1, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(1, 1);
        rt.anchoredPosition = new Vector2(-12, -6);
        rt.sizeDelta = new Vector2(120, 16);

        memText = go.AddComponent<TextMeshProUGUI>();
        memText.font = monoFontLight;
        memText.fontSize = 11;
        memText.color = new Color(0.56f, 1f, 0.62f, 0.6f);
        memText.alignment = TextAlignmentOptions.TopRight;
        memText.text = "";

        StartCoroutine(PollMemory());
    }

    IEnumerator PollMemory()
    {
        while (true)
        {
            if (memText != null)
            {
                long reserved = UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong();
                long mb = reserved > 0 ? reserved / (1024 * 1024) : System.GC.GetTotalMemory(false) / (1024 * 1024);
                memText.text = $"{mb} MB";
            }
            yield return new WaitForSeconds(1f);
        }
    }

    void CreateDock()
    {
        GameObject dockGO = new GameObject("Dock");
        dock = dockGO.AddComponent<K1L0Dock>();
        dock.Initialize(safeArea, monoFont);
        dock.OnButtonTapped = OnDockButtonTapped;
    }

    // Lower-right circular badge holding the surveillance-camera glyph — a small
    // "you are being watched" motif for the K1L0 HUD.
    void CreateSurveillanceCamBadge()
    {
        const float diameter = 100f;   // circle size
        const float camSize = 80f;     // camera glyph size inside

        var go = new GameObject("SurveillanceCamBadge", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(safeArea != null ? safeArea : K1L0CanvasRoot.HUD, false);
        go.transform.SetAsLastSibling();
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-16f, 66f);   // lower-right, just above the dock/menu bar
        rt.sizeDelta = new Vector2(diameter, diameter);

        var circle = go.GetComponent<Image>();
        circle.sprite = MakeCircleSprite(128);
        circle.type = Image.Type.Simple;
        circle.color = new Color(0f, 0f, 0f, 0.55f);     // translucent dark disc
        circle.raycastTarget = true;                     // tappable toggle

        // Tap toggles the map (surveillance mode). Map is OFF by default.
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = circle;
        btn.onClick.AddListener(ToggleMap);

        // Thin green ring on the rim.
        var ringGO = new GameObject("Ring", typeof(RectTransform), typeof(Image));
        ringGO.transform.SetParent(go.transform, false);
        var ringRt = ringGO.GetComponent<RectTransform>();
        ringRt.anchorMin = Vector2.zero; ringRt.anchorMax = Vector2.one;
        ringRt.offsetMin = Vector2.zero; ringRt.offsetMax = Vector2.zero;
        var ring = ringGO.GetComponent<Image>();
        ring.sprite = MakeRingSprite(128, 8);
        ring.color = new Color(0.47f, 1f, 0.54f, 0.7f);
        ring.raycastTarget = false;
        _camRingImage = ring;

        // Camera glyph.
        var camGO = new GameObject("CamGlyph", typeof(RectTransform), typeof(Image));
        camGO.transform.SetParent(go.transform, false);
        var camRt = camGO.GetComponent<RectTransform>();
        camRt.anchorMin = new Vector2(0.5f, 0.5f);
        camRt.anchorMax = new Vector2(0.5f, 0.5f);
        camRt.pivot = new Vector2(0.5f, 0.5f);
        camRt.anchoredPosition = Vector2.zero;
        camRt.sizeDelta = new Vector2(camSize, camSize);
        var cam = camGO.GetComponent<Image>();
        cam.sprite = Resources.Load<Sprite>("Icons/SurveillanceCam");
        cam.preserveAspect = true;
        cam.raycastTarget = false;
        if (cam.sprite == null) Debug.LogWarning("[K1L0HUD] SurveillanceCam sprite not found at Resources/Icons/SurveillanceCam");

        // Map off by default → black screen until the camera is tapped.
        SetMapVisible(false);
    }

    private bool _mapVisible = true;
    private Image _camRingImage;
    private Camera _worldCam;
    private CameraClearFlags _savedClearFlags;
    private Color _savedBgColor;
    private int _savedCullingMask;
    private bool _camStateSaved;
    private float _lastMapToggleTime = -999f;

    private Camera WorldCam()
    {
        if (_worldCam == null) _worldCam = Camera.main;
        return _worldCam;
    }

    void ToggleMap()
    {
        if (Time.unscaledTime - _lastMapToggleTime < 0.35f)
        {
            Debug.Log("[K1L0HUD] Suppressed duplicate surveillance camera toggle");
            return;
        }
        _lastMapToggleTime = Time.unscaledTime;
        SetMapVisible(!_mapVisible);
    }

    // "Map off" = black the whole world (buildings, roads, sky, etc.) by culling
    // everything on the main camera and clearing to solid black. The HUD canvas
    // is screen-space overlay, so it still renders on top of the black.
    void SetMapVisible(bool visible)
    {
        _mapVisible = visible;
        var cam = WorldCam();
        if (cam != null)
        {
            if (!_camStateSaved)
            {
                _savedClearFlags = cam.clearFlags;
                _savedBgColor = cam.backgroundColor;
                _savedCullingMask = cam.cullingMask;
                _camStateSaved = true;
            }
            if (visible)
            {
                cam.clearFlags = _savedClearFlags;
                cam.backgroundColor = _savedBgColor;
                cam.cullingMask = _savedCullingMask != 0 ? _savedCullingMask : ~0;
                var playerController = FindFirstObjectByType<KiloFirstPersonController>();
                if (playerController != null)
                    playerController.SetMapModalCameraActive(true);
                Debug.Log($"[K1L0HUD] Map visible: camera={cam.name} cullingMask={cam.cullingMask} clearFlags={cam.clearFlags}");
            }
            else
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                cam.cullingMask = 0;   // render nothing 3D → black
                Debug.Log($"[K1L0HUD] Map hidden: camera={cam.name}");
            }
        }
        // Ring glows green when the map is ON, dim red when surveillance/off.
        if (_camRingImage != null)
            _camRingImage.color = visible
                ? new Color(0.47f, 1f, 0.54f, 0.7f)
                : new Color(1f, 0.35f, 0.35f, 0.7f);
    }

    static Sprite MakeCircleSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        float r = size * 0.5f, cx = r, cy = r;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                float a = Mathf.Clamp01(r - d);   // 1px AA edge
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    static Sprite MakeRingSprite(int size, int thickness)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        float r = size * 0.5f, cx = r, cy = r, inner = r - thickness;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                float a = Mathf.Clamp01(r - d) * Mathf.Clamp01(d - inner);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    void CreateTerminalHudToggle()
    {
        return;

        GameObject go = new GameObject("TerminalHUDToggle", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(K1L0CanvasRoot.Modal, false);
        go.transform.SetAsFirstSibling();

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-12f, -12f);
        rt.sizeDelta = new Vector2(58f, 46f);

        terminalToggleBg = go.GetComponent<Image>();
        terminalToggleBg.sprite = K1L0GlassFactory.ControlRectSprite;
        terminalToggleBg.type = Image.Type.Sliced;
        terminalToggleBg.color = new Color(0f, 0f, 0f, 0.92f);
        terminalToggleBg.raycastTarget = true;

        var titleBar = new GameObject("TitleBar", typeof(RectTransform), typeof(Image));
        titleBar.transform.SetParent(go.transform, false);
        var titleRt = titleBar.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.offsetMin = new Vector2(5f, -12f);
        titleRt.offsetMax = new Vector2(-5f, -5f);
        var titleImg = titleBar.GetComponent<Image>();
        titleImg.color = new Color(0.78f, 0.78f, 0.78f, 0.9f);
        titleImg.raycastTarget = false;

        var glyphGO = new GameObject("Glyph", typeof(RectTransform));
        glyphGO.transform.SetParent(go.transform, false);
        var glyphRt = glyphGO.GetComponent<RectTransform>();
        glyphRt.anchorMin = Vector2.zero;
        glyphRt.anchorMax = Vector2.one;
        glyphRt.offsetMin = new Vector2(6f, 10f);
        glyphRt.offsetMax = new Vector2(-6f, -3f);

        terminalToggleGlyph = glyphGO.AddComponent<TextMeshProUGUI>();
        terminalToggleGlyph.font = monoFont;
        terminalToggleGlyph.fontSize = 19f;
        terminalToggleGlyph.alignment = TextAlignmentOptions.Center;
        terminalToggleGlyph.text = ">_";
        terminalToggleGlyph.raycastTarget = false;

        var btn = go.GetComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.targetGraphic = terminalToggleBg;
        btn.onClick.AddListener(ToggleHudVisible);

        RefreshTerminalToggle();
    }

    void CreateTransmitStatusCard()
    {
        K1L0TransmitStatusCard.Create(safeArea, monoFont, ShowTransmitterPanel);
    }

    void CreateTransmitButton()
    {
        if (transmitButton != null) return;

        GameObject go = new GameObject("TransmitButton", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(safeArea, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(190f, 54f);
        rt.anchoredPosition = new Vector2(0f, 86f);

        var img = go.GetComponent<Image>();
        img.sprite = K1L0GlassFactory.ControlRectSprite;
        img.type = Image.Type.Sliced;
        img.color = new Color(0f, 0f, 0f, 0.96f);
        img.raycastTarget = true;

        transmitButton = go.GetComponent<Button>();
        transmitButton.transition = Selectable.Transition.None;
        transmitButton.targetGraphic = img;
        transmitButton.onClick.AddListener(OpenTransmitModal);

        var labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(go.transform, false);
        var labelRt = labelGO.GetComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;

        var label = labelGO.AddComponent<TextMeshProUGUI>();
        label.font = monoFont;
        label.fontSize = 20f;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.text = "[TRANSMIT]";
        label.raycastTarget = false;
    }

    void OpenTransmitModal()
    {
        EnsureTransmitterEnterModal();
        var modal = TransmitterEnterModal.Instance;
        if (modal != null)
            modal.Show(null);
    }

    public void ShowTransmitterPanel()
    {
        TogglePanel(3, true);
    }

    void ToggleHudVisible()
    {
        SetHudVisible(!hudUserVisible);
    }

    public void SetNativeOverlayMode(string enabled)
    {
        bool nativeOverlay = enabled == "1" || string.Equals(enabled, "true", System.StringComparison.OrdinalIgnoreCase);
        if (NativeOverlayOwnsHud)
        {
            hudUserVisible = false;
            if (K1L0CanvasRoot.HUD != null)
                K1L0CanvasRoot.HUD.gameObject.SetActive(false);
            return;
        }
        SetHudVisible(!nativeOverlay);
    }

    public void SetNativeMapVisible(string enabled)
    {
        bool visible = enabled == "1" || string.Equals(enabled, "true", System.StringComparison.OrdinalIgnoreCase);
        SetMapVisible(visible);
    }

    public void SetNativePanelOpen(string enabled)
    {
        bool open = enabled == "1" || string.Equals(enabled, "true", System.StringComparison.OrdinalIgnoreCase);
        DynamicSkyVideoController.SetNativePanelOpen(open);
        SkyModeWorldVisibility.SetSkyMode(open);
        KiloFirstPersonController.SetNativePanelOpen(open);
        if (!NativeOverlayOwnsHud)
            ApplyMapHudSuppression(open);
    }

    public void PlayNativeBeamCollectSound(string _)
    {
        var director = SignalDirectorV2.Instance;
        if (director != null)
            director.PlayAmbientPortalCollectSound();
    }

    public void ApplyNativeWorldNearby(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;

        var scanner = TransmitterScanner.Instance;
        if (scanner != null)
            scanner.ApplyNativeWorldNearby(json);

        var director = SignalDirectorV2.Instance;
        if (director != null)
            director.ApplyNativeWorldNearby(json);
    }

    public void ApplyNativeLocationMode(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;
        var teleporter = FindFirstObjectByType<TeleportManager>();
        if (teleporter != null)
            teleporter.ApplyNativeLocationMode(json);
        else
            Debug.LogWarning("[K1L0HUD] ApplyNativeLocationMode: TeleportManager not found.");
    }

    public void ApplyNativeSimulatedLocation(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;
        var teleporter = FindFirstObjectByType<TeleportManager>();
        if (teleporter != null)
            teleporter.ApplyNativeSimulatedLocation(json);
        else
            Debug.LogWarning("[K1L0HUD] ApplyNativeSimulatedLocation: TeleportManager not found.");
    }

    // Native overlay (Swift) forwards the resolved cloak/helmet texture URLs
    // here after a metadata load/save or identity render so the 3D avatar stays
    // in sync with the user-panel design. Without this bridge the freshly
    // generated textures never reach the meshes.
    public void ApplyNativeIdentitySkin(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;
        try
        {
            var skin = JsonUtility.FromJson<NativeIdentitySkinPayload>(json);
            K1L0PlayerIdentitySkinApplier.ApplyFromMetadata(
                skin.helmetUrl ?? "",
                skin.cloakUrl ?? "",
                skin.avatarUrl ?? "",
                skin.helmetDesign ?? "",
                skin.cloakDesign ?? "",
                skin.helmetTextureUrl ?? "",
                skin.cloakTextureUrl ?? "",
                skin.skinRevision);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[K1L0HUD] ApplyNativeIdentitySkin parse failed: {ex.Message}");
        }
    }

    [Serializable]
    private class NativeIdentitySkinPayload
    {
        public string helmetUrl;
        public string cloakUrl;
        public string avatarUrl;
        public string helmetTextureUrl;
        public string cloakTextureUrl;
        public long skinRevision;
        public string helmetDesign;
        public string cloakDesign;
    }

    public void BeginNativeAppleSignIn(string _)
    {
        SubscribeNativeAuth();
        if (AppleSignInHandler.Instance == null || !AppleSignInHandler.Instance.IsAvailable())
        {
            SendNativeAuthState("apple sign in is unavailable on this build.");
            return;
        }
        SendNativeAuthState("opening apple sign in...");
        AppleSignInHandler.Instance.SignIn();
    }

    public void LogoutNativeSession(string _)
    {
        SubscribeNativeAuth();
        if (FirebaseAuthManager.Instance != null)
            FirebaseAuthManager.Instance.SignOut();

        PlayerPrefs.DeleteKey("FirebaseUserId");
        PlayerPrefs.DeleteKey("K1L0UserId");
        PlayerPrefs.DeleteKey("FirebaseEmail");
        PlayerPrefs.DeleteKey("FirebaseDisplayName");
        PlayerPrefs.Save();
        SendNativeAuthState("signed out.");
    }

    private void HandleNativeAppleSignInSuccess(string idToken, string nonce, string fullName, string appleUserId)
    {
        SendNativeAuthState("connecting apple id...");
        if (FirebaseAuthManager.Instance == null)
        {
            SendNativeAuthState("auth manager is unavailable.");
            return;
        }
        FirebaseAuthManager.Instance.SignInWithNativeApple(appleUserId, fullName, "", idToken);
    }

    private void HandleNativeAppleSignInFailed(string error)
    {
        SendNativeAuthState(string.IsNullOrWhiteSpace(error) ? "apple sign in failed." : error);
    }

    private void HandleNativeAuthError(string error)
    {
        SendNativeAuthState(string.IsNullOrWhiteSpace(error) ? "auth failed." : error);
    }

    private void HandleNativeAuthStateChanged(bool authenticated)
    {
        SendNativeAuthState(authenticated ? "signed in." : "signed out.");
    }

    private void SendNativeAuthState(string status)
    {
        try
        {
            var auth = FirebaseAuthManager.Instance;
            var payload = new NativeAuthStatePayload
            {
                userId = auth != null && auth.isAuthenticated ? (auth.userId ?? "") : PlayerPrefs.GetString("FirebaseUserId", ""),
                email = auth != null ? (auth.email ?? "") : PlayerPrefs.GetString("FirebaseEmail", ""),
                displayName = auth != null ? (auth.displayName ?? "") : PlayerPrefs.GetString("FirebaseDisplayName", ""),
                isAuthenticated = auth != null ? auth.isAuthenticated : !string.IsNullOrWhiteSpace(PlayerPrefs.GetString("FirebaseUserId", "")),
                status = status ?? ""
            };
            K1L0DeliverNativeAuthState(JsonUtility.ToJson(payload));
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[K1L0HUD] native auth deliver failed: {ex.Message}");
        }
    }

    public void BeginNativeTransmission(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload)) return;
        NativeTransmissionPayload parsed = null;
        try { parsed = JsonUtility.FromJson<NativeTransmissionPayload>(payload); }
        catch (Exception ex)
        {
            Debug.LogWarning($"[K1L0HUD] Native transmission payload parse failed: {ex.Message}");
            DeliverNativeTransmissionStatus("error", $"payload parse failed: {ex.Message}");
            return;
        }

        if (parsed == null)
        {
            Debug.LogWarning("[K1L0HUD] Native transmission payload missing");
            DeliverNativeTransmissionStatus("error", "transmission payload missing");
            return;
        }

        StartCoroutine(BeginNativeTransmissionCoroutine(parsed));
    }

    public void SaveNativeUserMetadata(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload)) return;
        NativeUserMetadataPayload parsed = null;
        try { parsed = JsonUtility.FromJson<NativeUserMetadataPayload>(payload); }
        catch (Exception ex)
        {
            Debug.LogWarning($"[K1L0HUD] Native user metadata parse failed: {ex.Message}");
            return;
        }

        if (parsed == null)
        {
            Debug.LogWarning("[K1L0HUD] Native user metadata missing");
            return;
        }

        StartCoroutine(SaveNativeUserMetadataCoroutine(parsed));
    }

    public void LoadNativeUserMetadata(string unused)
    {
        StartCoroutine(LoadNativeUserMetadataCoroutine());
    }

    private IEnumerator LoadNativeUserMetadataCoroutine()
    {
        var tm = TransmissionManager.Instance ?? FindFirstObjectByType<TransmissionManager>();
        string userId = K1L0NativeSessionBridge.ResolveUserId("");
        if (string.IsNullOrWhiteSpace(userId))
            userId = tm != null ? tm.GetUserIdForClient() : null;
        if (string.IsNullOrWhiteSpace(userId)) userId = "anon";

        if (APIManager.Instance == null)
        {
            DeliverUserMetadataSaveResult(false, "api unavailable");
            yield break;
        }

        string response = null;
        bool ok = false;
        string endpoint = $"/api/k1l0/user/metadata?userId={UnityWebRequest.EscapeURL(userId)}";
        yield return APIManager.Instance.Get(endpoint, (success, resp) =>
        {
            ok = success;
            response = resp;
        });

        if (!ok || string.IsNullOrWhiteSpace(response))
        {
            DeliverUserMetadataSaveResult(false, string.IsNullOrWhiteSpace(response) ? "metadata load failed" : response);
            yield break;
        }

        NativeUserMetadataSaveResultPayload result = null;
        try { result = JsonUtility.FromJson<NativeUserMetadataSaveResultPayload>(response); }
        catch (Exception ex)
        {
            DeliverUserMetadataSaveResult(false, $"metadata parse failed: {ex.Message}");
            yield break;
        }

        if (result == null || !result.ok)
        {
            DeliverUserMetadataSaveResult(false, result != null ? result.error : "metadata load failed");
            yield break;
        }

        if (string.IsNullOrWhiteSpace(result.status))
            result.status = "user metadata loaded.";
        K1L0PlayerIdentitySkinApplier.ApplyFromMetadata(
            result.helmetUrl,
            result.cloakUrl,
            result.avatarUrl,
            result.helmetDesign,
            result.cloakDesign,
            result.helmetTextureUrl,
            result.cloakTextureUrl,
            result.skinRevision);
        DeliverUserMetadataSaveResult(result);
    }

    private IEnumerator SaveNativeUserMetadataCoroutine(NativeUserMetadataPayload payload)
    {
        string selfieUrl = "";
        if (!string.IsNullOrWhiteSpace(payload.selfiePath))
            yield return UploadNativeTransmissionPhoto(payload.selfiePath, url => selfieUrl = url);

        var tm = TransmissionManager.Instance ?? FindFirstObjectByType<TransmissionManager>();
        string userId = K1L0NativeSessionBridge.ResolveUserId("");
        if (string.IsNullOrWhiteSpace(userId))
            userId = tm != null ? tm.GetUserIdForClient() : null;
        if (string.IsNullOrWhiteSpace(userId)) userId = "anon";

        if (APIManager.Instance == null)
        {
            DeliverUserMetadataSaveResult(false, "api unavailable");
            yield break;
        }

        var request = new NativeUserMetadataApiRequest
        {
            userId = userId,
            name = payload.name ?? "",
            callsign = payload.callsign ?? "",
            cloakDesign = payload.cloakDesign ?? "",
            helmetDesign = payload.helmetDesign ?? "",
            selfieUrl = selfieUrl
        };

        bool ok = false;
        string response = null;
        yield return APIManager.Instance.Post("/api/k1l0/user/metadata", JsonUtility.ToJson(request), (success, resp) =>
        {
            ok = success;
            response = resp;
        });

        if (!ok || string.IsNullOrWhiteSpace(response))
        {
            DeliverUserMetadataSaveResult(false, string.IsNullOrWhiteSpace(response) ? "metadata save failed" : response, selfieUrl);
            yield break;
        }

        NativeUserMetadataSaveResultPayload saved = null;
        try { saved = JsonUtility.FromJson<NativeUserMetadataSaveResultPayload>(response); }
        catch (Exception ex)
        {
            DeliverUserMetadataSaveResult(false, $"metadata save parse failed: {ex.Message}", selfieUrl);
            yield break;
        }

        if (saved == null || !saved.ok)
        {
            DeliverUserMetadataSaveResult(false, saved != null ? saved.error : "metadata save failed", selfieUrl);
            yield break;
        }

        Debug.Log($"[K1L0HUD] Native user metadata saved via API user={userId} selfie={(string.IsNullOrWhiteSpace(saved.selfieUrl) ? "none" : "yes")}");
        K1L0PlayerIdentitySkinApplier.ApplyFromMetadata(
            saved.helmetUrl,
            saved.cloakUrl,
            saved.avatarUrl,
            payload.helmetDesign,
            payload.cloakDesign,
            saved.helmetTextureUrl,
            saved.cloakTextureUrl,
            saved.skinRevision);

        string effectiveSelfieUrl = !string.IsNullOrWhiteSpace(selfieUrl) ? selfieUrl : saved.selfieUrl;
        DeliverUserMetadataSaveResult(true, "", effectiveSelfieUrl, saved.helmetUrl, saved.cloakUrl, saved.avatarUrl, "", saved.helmetTextureUrl, saved.cloakTextureUrl, saved.skinRevision);
        if (!string.IsNullOrWhiteSpace(effectiveSelfieUrl))
            StartCoroutine(RenderNativeUserIdentityCoroutine(userId, payload, effectiveSelfieUrl));
        else
            Debug.LogWarning("[K1L0HUD] Skipping identity render — no selfie attached or stored.");
    }

    private IEnumerator RenderNativeUserIdentityCoroutine(string userId, NativeUserMetadataPayload payload, string selfieUrl)
    {
        DeliverUserMetadataSaveResult(true, "", selfieUrl, "", "", "", "building identity...");
        var req = new NativeUserIdentityRenderRequest
        {
            userId = userId,
            selfieUrl = selfieUrl,
            name = payload.name ?? "",
            callsign = payload.callsign ?? "",
            cloakDesign = payload.cloakDesign ?? "",
            helmetDesign = payload.helmetDesign ?? ""
        };

        bool ok = false;
        string response = null;
        yield return APIManager.Instance.Post("/api/k1l0/user/identity/render", JsonUtility.ToJson(req), (success, resp) =>
        {
            ok = success;
            response = resp;
        });

        if (!ok || string.IsNullOrWhiteSpace(response))
        {
            DeliverUserMetadataSaveResult(false, "identity render failed", selfieUrl);
            yield break;
        }

        NativeUserIdentityRenderResponse parsed = null;
        try { parsed = JsonUtility.FromJson<NativeUserIdentityRenderResponse>(response); }
        catch { parsed = null; }

        if (parsed == null || !parsed.ok)
        {
            DeliverUserMetadataSaveResult(false, parsed != null ? parsed.error : "identity render parse failed", selfieUrl);
            yield break;
        }

        K1L0PlayerIdentitySkinApplier.ApplyFromMetadata(
            parsed.helmetUrl,
            parsed.cloakUrl,
            parsed.avatarUrl,
            payload.helmetDesign,
            payload.cloakDesign,
            parsed.helmetTextureUrl,
            parsed.cloakTextureUrl,
            parsed.skinRevision);
        DeliverUserMetadataSaveResult(true, "", selfieUrl, parsed.helmetUrl, parsed.cloakUrl, parsed.avatarUrl, "identity rendered.", parsed.helmetTextureUrl, parsed.cloakTextureUrl, parsed.skinRevision);
    }

    private IEnumerator BeginNativeTransmissionCoroutine(NativeTransmissionPayload payload)
    {
        string imageUrl = "";
        if (!string.IsNullOrWhiteSpace(payload.photoPath))
        {
            yield return UploadNativeTransmissionPhoto(payload.photoPath, url => imageUrl = url);
            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                Debug.LogWarning($"[K1L0HUD] Native transmission photo upload failed path={payload.photoPath}");
                DeliverNativeTransmissionStatus("error", "photo upload failed");
                yield break;
            }
        }

        var tm = TransmissionManager.Instance ?? FindFirstObjectByType<TransmissionManager>();
        if (tm == null)
        {
            Debug.LogWarning("[K1L0HUD] Native transmission failed: TransmissionManager missing");
            DeliverNativeTransmissionStatus("error", "TransmissionManager missing");
            yield break;
        }

        string message = string.IsNullOrWhiteSpace(payload.message) ? "transmit this signal" : payload.message.Trim();
        string mood = string.IsNullOrWhiteSpace(payload.mood) ? "wired" : payload.mood.Trim();
        string element = string.IsNullOrWhiteSpace(payload.element) ? "" : payload.element.Trim();
        Debug.Log($"[K1L0HUD] Native transmission start mood='{mood}' image={(string.IsNullOrWhiteSpace(imageUrl) ? "none" : "yes")}");
        DeliverNativeTransmissionStatus("queued", "transmission queued");
        tm.StartTransmitterInteraction(null, element, message, imageUrl, mood);
    }

    private IEnumerator UploadNativeTransmissionPhoto(string path, System.Action<string> done)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            done?.Invoke(null);
            yield break;
        }

        byte[] bytes;
        try { bytes = File.ReadAllBytes(path); }
        catch
        {
            done?.Invoke(null);
            yield break;
        }

        var tm = TransmissionManager.Instance ?? FindFirstObjectByType<TransmissionManager>();
        if (APIManager.Instance == null)
        {
            Debug.LogWarning("[K1L0HUD] Native transmission photo upload failed: APIManager missing");
            done?.Invoke(null);
            yield break;
        }
        var req = new NativeImageUploadRequest
        {
            userId = K1L0NativeSessionBridge.ResolveUserId(tm != null ? tm.GetUserIdForClient() : "anon"),
            filename = Path.GetFileName(path),
            contentType = path.ToLowerInvariant().EndsWith(".png") ? "image/png" : "image/jpeg",
            imageBase64 = Convert.ToBase64String(bytes)
        };

        bool ok = false;
        string response = null;
        yield return APIManager.Instance.Post("/api/k1l0/upload-image", JsonUtility.ToJson(req), (success, resp) =>
        {
            ok = success;
            response = resp;
        });

        if (!ok || string.IsNullOrWhiteSpace(response))
        {
            done?.Invoke(null);
            yield break;
        }

        var parsed = JsonUtility.FromJson<NativeImageUploadResponse>(response);
        done?.Invoke(parsed != null && parsed.ok ? parsed.url : null);
    }

    private static string SanitizeFirebaseKey(string value)
    {
        if (string.IsNullOrEmpty(value)) return "anon";
        return value
            .Replace(".", "_")
            .Replace("#", "_")
            .Replace("$", "_")
            .Replace("[", "_")
            .Replace("]", "_")
            .Replace("/", "_");
    }

    private static void DeliverUserMetadataSaveResult(bool ok, string error, string selfieUrl = "", string helmetUrl = "", string cloakUrl = "", string avatarUrl = "", string status = "", string helmetTextureUrl = "", string cloakTextureUrl = "", long skinRevision = 0)
    {
        try
        {
            var payload = new NativeUserMetadataSaveResultPayload
            {
                ok = ok,
                error = error ?? "",
                selfieUrl = selfieUrl ?? "",
                helmetUrl = helmetUrl ?? "",
                cloakUrl = cloakUrl ?? "",
                avatarUrl = avatarUrl ?? "",
                helmetTextureUrl = helmetTextureUrl ?? "",
                cloakTextureUrl = cloakTextureUrl ?? "",
                skinRevision = skinRevision,
                status = status ?? ""
            };
            K1L0DeliverUserMetadataSaveResult(JsonUtility.ToJson(payload));
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[K1L0HUD] native metadata save result deliver failed: {ex.Message}");
        }
    }

    private static void DeliverUserMetadataSaveResult(NativeUserMetadataSaveResultPayload payload)
    {
        try { K1L0DeliverUserMetadataSaveResult(JsonUtility.ToJson(payload)); }
        catch (Exception ex) { Debug.LogWarning($"[K1L0HUD] native metadata save result deliver failed: {ex.Message}"); }
    }

    public void SetNativeSetting(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload)) return;
        int split = payload.IndexOf('=');
        if (split <= 0) return;

        string key = payload.Substring(0, split);
        string value = payload.Substring(split + 1);
        bool boolValue = value == "1" || value.Equals("true", System.StringComparison.OrdinalIgnoreCase);
        float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float floatValue);

        var rm = RenderManager.Instance;
        var profile = rm != null ? rm.profile : null;
        if (profile != null)
        {
            switch (key)
            {
                case "saturation":
                    profile.postFX.saturation = floatValue;
                    SaveFloat("saturation", floatValue);
                    break;
                case "contrast":
                    profile.postFX.contrast = floatValue;
                    SaveFloat("contrast", floatValue);
                    break;
                case "mapBrightness":
                    profile.postFX.exposureFixedValue = floatValue;
                    profile.postFX.exposureEnabled = true;
                    SaveFloat("exposureFixedValue", floatValue);
                    break;
                case "hueShift":
                    profile.postFX.hueShift = floatValue;
                    SaveFloat("hueShift", floatValue);
                    break;
                case "temperature":
                    profile.postFX.temperature = floatValue;
                    profile.postFX.temperatureOverride = true;
                    SaveFloat("temperature", floatValue);
                    break;
                case "tint":
                    profile.postFX.tint = floatValue;
                    profile.postFX.tintOverride = true;
                    SaveFloat("tint", floatValue);
                    break;
                case "bloomEnabled":
                    profile.postFX.bloomEnabled = boolValue;
                    SaveBool("bloomEnabled", boolValue);
                    break;
                case "bloomIntensity":
                    profile.postFX.bloomIntensity = floatValue;
                    SaveFloat("bloomIntensity", floatValue);
                    break;
                case "bloomThreshold":
                    profile.postFX.bloomThreshold = floatValue;
                    SaveFloat("bloomThreshold", floatValue);
                    break;
                case "bloomScatter":
                    profile.postFX.bloomScatter = Mathf.Clamp01(floatValue);
                    SaveFloat("bloomScatter", profile.postFX.bloomScatter);
                    break;
                case "vignetteEnabled":
                    profile.postFX.vignetteEnabled = boolValue;
                    SaveBool("vignetteEnabled", boolValue);
                    break;
                case "vignetteIntensity":
                    profile.postFX.vignetteIntensity = Mathf.Clamp01(floatValue);
                    SaveFloat("vignetteIntensity", profile.postFX.vignetteIntensity);
                    break;
                case "vignetteSmoothness":
                    profile.postFX.vignetteSmoothness = Mathf.Clamp(floatValue, 0.01f, 1f);
                    SaveFloat("vignetteSmoothness", profile.postFX.vignetteSmoothness);
                    break;
                case "chromaticEnabled":
                    profile.postFX.chromaticAberrationEnabled = boolValue;
                    SaveBool("chromaticEnabled", boolValue);
                    break;
                case "chromaticIntensity":
                    profile.postFX.chromaticAberrationIntensity = Mathf.Clamp01(floatValue);
                    SaveFloat("chromaticIntensity", profile.postFX.chromaticAberrationIntensity);
                    break;
                case "lensDistEnabled":
                    profile.postFX.lensDistortionEnabled = boolValue;
                    SaveBool("lensDistEnabled", boolValue);
                    break;
                case "lensDistIntensity":
                    profile.postFX.lensDistortionIntensity = Mathf.Clamp(floatValue, -1f, 1f);
                    SaveFloat("lensDistIntensity", profile.postFX.lensDistortionIntensity);
                    break;
                case "dofEnabled":
                    profile.postFX.depthOfFieldEnabled = boolValue;
                    SaveBool("dofEnabled", boolValue);
                    break;
                case "focusDistance":
                    profile.postFX.focusDistance = Mathf.Clamp(floatValue, 0.1f, 300f);
                    SaveFloat("focusDistance", profile.postFX.focusDistance);
                    break;
                case "aperture":
                    profile.postFX.aperture = Mathf.Clamp(floatValue, 0.05f, 32f);
                    SaveFloat("aperture", profile.postFX.aperture);
                    break;
                case "focalLength":
                    profile.postFX.focalLength = Mathf.Clamp(floatValue, 1f, 300f);
                    SaveFloat("focalLength", profile.postFX.focalLength);
                    break;
                case "motionBlurEnabled":
                    profile.postFX.motionBlurEnabled = boolValue;
                    SaveBool("motionBlurEnabled", boolValue);
                    break;
                case "motionBlurIntensity":
                    profile.postFX.motionBlurIntensity = Mathf.Clamp01(floatValue);
                    SaveFloat("motionBlurIntensity", profile.postFX.motionBlurIntensity);
                    break;
                case "filmGrainEnabled":
                    profile.postFX.filmGrainEnabled = boolValue;
                    SaveBool("filmGrainEnabled", boolValue);
                    break;
                case "filmGrainIntensity":
                    profile.postFX.filmGrainIntensity = Mathf.Clamp01(floatValue);
                    SaveFloat("filmGrainIntensity", profile.postFX.filmGrainIntensity);
                    break;
                case "godPositionY":
                    profile.camera.godPositionY = floatValue;
                    SaveFloat("godPositionY", floatValue);
                    ApplyCameraProfile();
                    break;
                case "godPositionZ":
                    profile.camera.godPositionZ = floatValue;
                    SaveFloat("godPositionZ", floatValue);
                    ApplyCameraProfile();
                    break;
                case "godRotationX":
                    profile.camera.godRotationX = floatValue;
                    SaveFloat("godRotationX", floatValue);
                    ApplyCameraProfile();
                    break;
                case "farClipPlane":
                    profile.camera.farClipPlane = Mathf.Max(50f, floatValue);
                    SaveFloat("farClipPlane", profile.camera.farClipPlane);
                    break;
                case "moonlightEnabled":
                    profile.lighting.moonlightEnabled = boolValue;
                    SaveBool("moonlightEnabled", boolValue);
                    break;
                case "moonlightManualOverride":
                    SaveBool("moonlightManualOverride", boolValue);
                    break;
                case "moonlightIntensity":
                    profile.lighting.moonlightIntensity = Mathf.Clamp(floatValue, 0f, 8f);
                    SaveFloat("moonlightIntensity", profile.lighting.moonlightIntensity);
                    break;
                case "moonlightRed":
                    profile.lighting.moonlightColor.r = Mathf.Clamp(floatValue, 0f, 2f);
                    SaveFloat("moonlightRed", profile.lighting.moonlightColor.r);
                    break;
                case "moonlightGreen":
                    profile.lighting.moonlightColor.g = Mathf.Clamp(floatValue, 0f, 2f);
                    SaveFloat("moonlightGreen", profile.lighting.moonlightColor.g);
                    break;
                case "moonlightBlue":
                    profile.lighting.moonlightColor.b = Mathf.Clamp(floatValue, 0f, 2f);
                    SaveFloat("moonlightBlue", profile.lighting.moonlightColor.b);
                    break;
                case "moonlightPitch":
                    profile.lighting.moonlightRotation.x = Mathf.Clamp(floatValue, -180f, 180f);
                    SaveFloat("moonlightPitch", profile.lighting.moonlightRotation.x);
                    break;
                case "moonlightYaw":
                    profile.lighting.moonlightRotation.y = Mathf.Clamp(floatValue, -180f, 180f);
                    SaveFloat("moonlightYaw", profile.lighting.moonlightRotation.y);
                    break;
                case "moonlightRoll":
                    profile.lighting.moonlightRotation.z = Mathf.Clamp(floatValue, -180f, 180f);
                    SaveFloat("moonlightRoll", profile.lighting.moonlightRotation.z);
                    break;
                case "ambientEnabled":
                    profile.lighting.ambientEnabled = boolValue;
                    SaveBool("ambientEnabled", boolValue);
                    break;
                case "ambientIntensity":
                    profile.lighting.ambientIntensity = Mathf.Clamp(floatValue, 0f, 8f);
                    SaveFloat("ambientIntensity", profile.lighting.ambientIntensity);
                    break;
                // (enableShadows / shadowStrength / shadowDistance handlers removed.)
                case "spotlightEnabled":
                    profile.lighting.spotlightEnabled = boolValue;
                    if (boolValue && profile.lighting.spotlightIntensity <= 0.01f)
                        profile.lighting.spotlightIntensity = 1f;
                    SaveBool("spotlightEnabled", boolValue);
                    SaveFloat("spotlightIntensity", profile.lighting.spotlightIntensity);
                    break;
                case "spotlightIntensity":
                    profile.lighting.spotlightIntensity = Mathf.Clamp(floatValue, 0f, 12f);
                    SaveFloat("spotlightIntensity", profile.lighting.spotlightIntensity);
                    break;
                // (reflectionsEnabled / reflectionIntensity handlers removed.)
                case "fogConstantDensity":
                    profile.volumetricFog.constantDensity = boolValue;
                    SaveBool("fogConstantDensity", boolValue);
                    break;
                case "fogDensity":
                    profile.volumetricFog.density = Mathf.Clamp(floatValue, 0f, 3f);
                    SaveFloat("fogDensity", profile.volumetricFog.density);
                    if (rm != null) rm.dayFogDensity = profile.volumetricFog.density;
                    break;
                case "fogNoiseStrength":
                    profile.volumetricFog.noiseStrength = Mathf.Clamp(floatValue, 0f, 3f);
                    SaveFloat("fogNoiseStrength", profile.volumetricFog.noiseStrength);
                    if (rm != null) rm.dayFogNoiseStrength = profile.volumetricFog.noiseStrength;
                    break;
                case "fogNoiseScale":
                    profile.volumetricFog.noiseScale = Mathf.Clamp(floatValue, 0.1f, 80f);
                    SaveFloat("fogNoiseScale", profile.volumetricFog.noiseScale);
                    if (rm != null) rm.dayFogNoiseScale = profile.volumetricFog.noiseScale;
                    break;
                case "fogBrightness":
                    profile.volumetricFog.brightness = Mathf.Clamp(floatValue, 0f, 2f);
                    SaveFloat("fogBrightness", profile.volumetricFog.brightness);
                    if (rm != null) rm.dayFogBrightness = profile.volumetricFog.brightness;
                    break;
                case "fogScatteringIntensity":
                    profile.volumetricFog.scatteringIntensity = Mathf.Clamp(floatValue, 0f, 4f);
                    SaveFloat("fogScatteringIntensity", profile.volumetricFog.scatteringIntensity);
                    if (rm != null) rm.dayFogScatteringIntensity = profile.volumetricFog.scatteringIntensity;
                    break;
                case "fogHeight":
                    profile.volumetricFog.customHeight = true;
                    profile.volumetricFog.height = Mathf.Clamp(floatValue, 0f, 500f);
                    SaveBool("fogCustomHeight", true);
                    SaveFloat("fogHeight", profile.volumetricFog.height);
                    if (rm != null) rm.dayFogHeight = profile.volumetricFog.height;
                    break;
                case "fogDistantFog":
                    profile.volumetricFog.distantFog = boolValue;
                    SaveBool("fogDistantFog", boolValue);
                    break;
                case "fogDistantDensity":
                    profile.volumetricFog.distantFogDistanceDensity = Mathf.Clamp(floatValue, 0f, 2f);
                    SaveFloat("fogDistantDensity", profile.volumetricFog.distantFogDistanceDensity);
                    if (rm != null) rm.dayFogDistantDensity = profile.volumetricFog.distantFogDistanceDensity;
                    break;
                case "fogDistantStart":
                    profile.volumetricFog.distantFogStartDistance = Mathf.Clamp(floatValue, 0f, 12000f);
                    SaveFloat("fogDistantStart", profile.volumetricFog.distantFogStartDistance);
                    if (rm != null) rm.dayFogDistantStart = profile.volumetricFog.distantFogStartDistance;
                    break;
                case "fogNativeLights":
                    profile.volumetricFog.enableNativeLights = boolValue;
                    SaveBool("fogNativeLights", boolValue);
                    break;
                case "fogNativeLightsMultiplier":
                    profile.volumetricFog.nativeLightsMultiplier = Mathf.Clamp(floatValue, 0f, 10f);
                    SaveFloat("fogNativeLightsMultiplier", profile.volumetricFog.nativeLightsMultiplier);
                    break;

                // Night Fog & Ground Settings
                case "fogDensity_night":
                    SaveFloat("fogDensity_night", floatValue);
                    if (rm != null) rm.nightFogDensity = floatValue;
                    break;
                case "fogNoiseStrength_night":
                    SaveFloat("fogNoiseStrength_night", floatValue);
                    if (rm != null) rm.nightFogNoiseStrength = floatValue;
                    break;
                case "fogNoiseScale_night":
                    SaveFloat("fogNoiseScale_night", floatValue);
                    if (rm != null) rm.nightFogNoiseScale = floatValue;
                    break;
                case "fogBrightness_night":
                    SaveFloat("fogBrightness_night", floatValue);
                    if (rm != null) rm.nightFogBrightness = floatValue;
                    break;
                case "fogScatteringIntensity_night":
                    SaveFloat("fogScatteringIntensity_night", floatValue);
                    if (rm != null) rm.nightFogScatteringIntensity = floatValue;
                    break;
                case "fogHeight_night":
                    SaveFloat("fogHeight_night", floatValue);
                    if (rm != null) rm.nightFogHeight = floatValue;
                    break;
                case "fogDistantDensity_night":
                    SaveFloat("fogDistantDensity_night", floatValue);
                    if (rm != null) rm.nightFogDistantDensity = floatValue;
                    break;
                case "fogDistantStart_night":
                    SaveFloat("fogDistantStart_night", floatValue);
                    if (rm != null) rm.nightFogDistantStart = floatValue;
                    break;
                case "groundHue_night":
                    SaveFloat("groundHue_night", floatValue);
                    if (rm != null) rm.nightGroundHue = floatValue;
                    break;
                case "groundSaturation_night":
                    SaveFloat("groundSaturation_night", floatValue);
                    if (rm != null) rm.nightGroundSaturation = floatValue;
                    break;

                case "groundHue":
                {
                    Color.RGBToHSV(profile.ground.groundColor, out _, out var s, out var v);
                    if (v < 0.05f) v = 0.5f; // default white reads as v=1; this guards against pure-black
                    profile.ground.groundColor = Color.HSVToRGB(Mathf.Clamp01(floatValue), Mathf.Max(0.05f, s), v);
                    SaveFloat("groundHue", floatValue);
                    if (rm != null) rm.dayGroundHue = floatValue;
                    break;
                }
                case "groundSaturation":
                {
                    Color.RGBToHSV(profile.ground.groundColor, out var h, out _, out var v);
                    if (v < 0.05f) v = 0.5f;
                    profile.ground.groundColor = Color.HSVToRGB(h, Mathf.Clamp01(floatValue), v);
                    SaveFloat("groundSaturation", floatValue);
                    if (rm != null) rm.dayGroundSaturation = floatValue;
                    break;
                }
                case "zossEmissiveIntensity":
                    profile.buildings.zossEmissiveIntensity = Mathf.Clamp(floatValue, 0f, 50f);
                    SaveFloat("zossEmissiveIntensity", profile.buildings.zossEmissiveIntensity);
                    break;
                case "zossEmissiveSmoothness":
                    profile.buildings.zossEmissiveSmoothness = Mathf.Clamp01(floatValue);
                    SaveFloat("zossEmissiveSmoothness", profile.buildings.zossEmissiveSmoothness);
                    break;
                case "zossEmissiveMetallic":
                    profile.buildings.zossEmissiveMetallic = Mathf.Clamp01(floatValue);
                    SaveFloat("zossEmissiveMetallic", profile.buildings.zossEmissiveMetallic);
                    break;
                case "zossEmissiveHue":
                {
                    // Recolor the window-glow emissive material by replacing the
                    // hue channel of the current color (keeps S/V).
                    Color.RGBToHSV(profile.buildings.zossEmissiveColor, out _, out var s, out var v);
                    var c = Color.HSVToRGB(Mathf.Clamp01(floatValue), s, Mathf.Max(0.01f, v));
                    profile.buildings.zossEmissiveColor = c;
                    profile.buildings.zossEmissiveEmission = c;
                    SaveFloat("zossEmissiveHue", floatValue);
                    break;
                }
                case "zossEmissiveSaturation":
                {
                    Color.RGBToHSV(profile.buildings.zossEmissiveColor, out var h, out _, out var v);
                    var c = Color.HSVToRGB(h, Mathf.Clamp01(floatValue), Mathf.Max(0.01f, v));
                    profile.buildings.zossEmissiveColor = c;
                    profile.buildings.zossEmissiveEmission = c;
                    SaveFloat("zossEmissiveSaturation", floatValue);
                    break;
                }
                case "skyTargetFps":
                    DynamicSkyVideoController.SetSkyFps(floatValue);
                    break;
            }
        }

        switch (key)
        {
            case "beamDistanceLabels":
                SignalBeamBridge.SetDistanceLabelsVisible(boolValue);
                break;
            case "beamDebug":
                ProfileEditorModal.SetBeamDebugVisible(boolValue);
                break;
            case "perfOverlay":
                ProfileEditorModal.SetPerfOverlayVisible(boolValue);
                break;
            case "showStoryStrip":
                StoriesStripVisibility.SetStoryStripVisible(boolValue);
                break;
            case "panelMapBrightness":
                SetPanelMapBrightness(floatValue);
                break;
            case "musicRadioEnabled":
                PlayerPrefs.SetInt("k1lo_musicRadioEnabled", boolValue ? 1 : 0);
                break;
            case "manualHour":
                KiloWorld.Rendering.Systems.RenderManager.ManualHour = Mathf.Repeat(floatValue, 24f);
                PlayerPrefs.SetFloat("k1lo_manualHour", KiloWorld.Rendering.Systems.RenderManager.ManualHour);
                KiloWorld.Rendering.Systems.RenderManager.NotifyManualSkyChanged();
                break;
            case "manualWeather":
                int weatherIndex = Mathf.Clamp(Mathf.RoundToInt(floatValue), 0, ManualWeatherGlyphs.Length - 1);
                KiloWorld.Rendering.Systems.RenderManager.ManualWeatherGlyph = ManualWeatherGlyphs[weatherIndex];
                KiloWorld.Rendering.Systems.RenderManager.ManualWeatherOverrideEnabled = true;
                PlayerPrefs.SetFloat("k1lo_manualWeather", weatherIndex);
                PlayerPrefs.SetString("k1lo_manualWeatherGlyph", ManualWeatherGlyphs[weatherIndex]);
                PlayerPrefs.SetInt("k1lo_manualWeatherOverrideEnabled", 1);
                KiloWorld.Rendering.Systems.RenderManager.NotifyManualSkyChanged();
                break;
            case "skyVideoUrl":
                DynamicSkyVideoController.SetVideoUrl(value);
                break;
            case "experimentalLayeredSky":
                DynamicSkyVideoController.SetExperimentalLayeredSky(boolValue);
                break;
            case "layeredSkyTopHue":
            case "layeredSkyHorizonHue":
            case "layeredCloudOpacity":
            case "layeredCloudSpeed":
            case "layeredCloudScale":
            case "layeredCloudContrast":
                DynamicSkyVideoController.SetExperimentalSkyFloat(key, floatValue);
                break;
            case "testSkyOverride":
                KiloWorld.Rendering.Systems.RenderManager.TestSkyOverrideEnabled = boolValue;
                PlayerPrefs.SetInt("k1lo_testSkyOverride", boolValue ? 1 : 0);
                KiloWorld.Rendering.Systems.RenderManager.NotifyManualSkyChanged();
                break;
            case "transmissionFizzyEdges":
                PlayerPrefs.SetInt("k1lo_transmissionFizzyEdges", boolValue ? 1 : 0);
                break;
            case "ambientMinStepsToSpawn":
                SetSignalFloat("k1lo_ambientMinStepsToSpawn", Mathf.Clamp(floatValue, 0f, 2000f));
                break;
            case "momentumGraceSteps":
                SetSignalFloat("k1lo_momentumGraceSteps", Mathf.Clamp(floatValue, 10f, 500f));
                break;
            case "ambientBeamTtlMinutes":
                SetSignalFloat("k1lo_ambientBeamTtlMinutes", Mathf.Clamp(floatValue, 1f, 240f));
                break;
            case "ambientCollectRadiusMeters":
                SetSignalFloat("k1lo_ambientCollectRadiusMeters", Mathf.Clamp(floatValue, 1f, 100f));
                break;
        }

        PlayerPrefs.Save();
        rm?.Apply();
        Debug.Log($"[K1L0HUD] Native setting {key}={value}");
    }

    public void CaptureSnapshot(string mode)
    {
        EnsureScreenshot();
        var ss = K1L0Screenshot.Instance;
        if (ss == null)
        {
            Debug.LogWarning("[K1L0HUD] Snapshot requested but K1L0Screenshot is missing.");
            return;
        }

        bool analyze = string.IsNullOrWhiteSpace(mode) ||
                       mode == "1" ||
                       mode.Equals("true", System.StringComparison.OrdinalIgnoreCase) ||
                       mode.Equals("analyze", System.StringComparison.OrdinalIgnoreCase);
        ss.Capture(analyze);
    }

    private static void SaveFloat(string key, float value)
    {
        PlayerPrefs.SetFloat("k1lo_" + key, value);
    }

    private static void SaveBool(string key, bool value)
    {
        PlayerPrefs.SetFloat("k1lo_" + key, value ? 1f : 0f);
    }

    private static void SetSignalFloat(string key, float value)
    {
        PlayerPrefs.SetFloat(key, value);
        var director = FindFirstObjectByType<SignalDirectorV2>();
        if (director == null) return;
        if (key == "k1lo_ambientMinStepsToSpawn")
            director.ambientMinStepsToSpawn = Mathf.RoundToInt(value);
        else if (key == "k1lo_momentumGraceSteps")
        {
            int n = Mathf.Clamp(Mathf.RoundToInt(value), 10, 500);
            director.momentumGraceSteps = n;
            PlayerPrefs.SetInt(key, n);
            UserPresenceManager.NotifyMomentumGraceStepsChanged(n);
        }
        else if (key == "k1lo_ambientBeamTtlMinutes")
            director.ambientBeamTtlMinutes = value;
        else if (key == "k1lo_ambientCollectRadiusMeters")
            director.ambientCollectRadiusMeters = value;
    }

    private static void ApplyCameraProfile()
    {
        var playerController = FindFirstObjectByType<KiloFirstPersonController>();
        if (playerController != null)
            playerController.ApplyCameraProfileNow();
    }

    void SetHudVisible(bool visible)
    {
        if (NativeOverlayOwnsHud)
        {
            hudUserVisible = false;
            if (K1L0CanvasRoot.HUD != null)
                K1L0CanvasRoot.HUD.gameObject.SetActive(false);
            return;
        }

        hudUserVisible = visible;

        if (!visible)
        {
            for (int i = 0; i < panelOpen.Length; i++)
            {
                if (!panelOpen[i]) continue;
                panelOpen[i] = false;
                if (panels != null && panels[i] != null) panels[i].Hide();
                if (dock != null) dock.SetActiveButton(i, false);
            }
            RefreshPanelDimmer();
        }

        var hud = K1L0CanvasRoot.HUD;
        if (hud != null) hud.gameObject.SetActive(visible);

        ApplyMapHudSuppression(!visible);

        RefreshTerminalToggle();
    }

    void ApplyMapHudSuppression(bool suppress)
    {
        K1L0HudLayoutController.SetMapHudVisible(hudUserVisible && !suppress);
        SignalBeamBridge.SetHudSuppressed(suppress);
        POILabelBridge.SetHudSuppressed(suppress);

        var director = SignalDirectorV2.Instance;
        if (director != null) director.SuppressHUD(suppress);
    }

    void RefreshTerminalToggle()
    {
        if (terminalToggleBg != null)
            terminalToggleBg.color = hudUserVisible ? new Color(0f, 0f, 0f, 0.92f) : new Color(0.02f, 0.18f, 0.04f, 0.96f);
        if (terminalToggleGlyph != null)
            terminalToggleGlyph.color = hudUserVisible ? new Color(0.56f, 1f, 0.62f, 1f) : new Color(0.56f, 1f, 0.62f, 0.55f);
    }

    void RefreshPanelDimmer()
    {
        bool anyOpen = false;
        for (int i = 0; i < panelOpen.Length; i++)
            if (panelOpen[i]) { anyOpen = true; break; }

        dimmerTarget = anyOpen ? 1f - PanelMapBrightness : 0f;
    }

    private static readonly float panelTopY = -96f;

    void CreatePanels()
    {
        Vector2 centeredPos = new Vector2(0, panelTopY);

        nearbyPanel = CreateOnePanel("NearbyPanel", centeredPos, new Vector2(0f, 500f), new Vector4(18f, 24f, 18f, 96f));
        nearbyPanel.draggable = false;
        nearbyPanel.OnCloseClicked = () => TogglePanel(0, false);
        GameObject nearbyGO = new GameObject("NearbyMode");
        nearbyMode = nearbyGO.AddComponent<K1L0NearbyMode>();
        nearbyMode.Initialize(nearbyPanel.contentArea, monoFont);
        nearbyPanel.gameObject.SetActive(false);

        codexPanel = CreateOnePanel("StatusPanel", centeredPos, new Vector2(0f, 520f), new Vector4(18f, 24f, 18f, 96f));
        codexPanel.draggable = false;
        codexPanel.OnCloseClicked = () => TogglePanel(1, false);
        GameObject statusGO = new GameObject("StatusMode");
        statusMode = statusGO.AddComponent<K1L0StatusMode>();
        statusMode.Initialize(codexPanel.contentArea, monoFont);
        codexPanel.gameObject.SetActive(false);

        profilePanel = CreateOnePanel("ProfilePanel", centeredPos, new Vector2(0f, 620f), new Vector4(18f, 24f, 18f, 96f));
        profilePanel.draggable = false;
        profilePanel.OnCloseClicked = () => TogglePanel(2, false);
        GameObject profileGO = new GameObject("ProfileMode");
        profileMode = profileGO.AddComponent<K1L0ProfileMode>();
        profileMode.Initialize(profilePanel.contentArea, monoFont);
        profilePanel.gameObject.SetActive(false);

        transmitterPanel = CreateOnePanel("TransmitterPanel", centeredPos, new Vector2(0f, 620f), new Vector4(18f, 24f, 18f, 96f));
        transmitterPanel.draggable = false;
        transmitterPanel.OnCloseClicked = () => TogglePanel(3, false);
        GameObject transmitterGO = new GameObject("TransmitterMode");
        transmitterMode = transmitterGO.AddComponent<TransmitterEnterModal>();
        transmitterMode.InitializeEmbedded(transmitterPanel.contentArea);
        transmitterPanel.gameObject.SetActive(false);

        panels = new K1L0GlassPanel[] { nearbyPanel, codexPanel, profilePanel, transmitterPanel };
    }

    void HandleDockTouchFallback()
    {
        Camera eventCamera = canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera ? canvas.worldCamera : null;

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.phase != TouchPhase.Began)
                continue;

            if (TryHandleDockPoint(touch.position, eventCamera, "touch-began"))
                break;

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.fingerId))
                continue;

            bool handled = false;
            if (panels != null)
            {
                for (int panelIndex = 0; panelIndex < panels.Length; panelIndex++)
                {
                    if (panels[panelIndex] != null && panels[panelIndex].TryHandleScreenPoint(touch.position, eventCamera))
                    {
                        handled = true;
                        break;
                    }
                }
            }

            if (!handled && TryHandlePanelButtonFallback(touch.position))
                handled = true;

            if (handled)
                break;
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (TryHandleDockPoint(Input.mousePosition, eventCamera, "mouse-down"))
                return;

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;
        }
    }

    bool TryHandleDockPoint(Vector2 point, Camera eventCamera, string source)
    {
        if (dock == null)
            return false;

        bool hit = false;
        string error = null;
        try
        {
            hit = dock.TryHandleScreenPoint(point, eventCamera);
        }
        catch (System.Exception e)
        {
            error = e.GetType().Name + ": " + e.Message;
            Debug.LogError($"[K1L0HUD] Dock handling exception: {error}");
        }

        UploadDockDebug(source, point, hit, error);
        return hit;
    }

    void UploadDockDebug(string source, Vector2 point, bool hit, string error = null)
    {
        if (Time.realtimeSinceStartup - lastDockDebugUploadTime < 1.0f)
            return;

        lastDockDebugUploadTime = Time.realtimeSinceStartup;
        string dockSummary = dock != null ? dock.GetDebugHitSummary(point) : "dock=null";
        string json =
            "{" +
            "\"kind\":\"dock-input\"," +
            "\"source\":\"" + EscapeJson(source) + "\"," +
            "\"hit\":" + (hit ? "true" : "false") + "," +
            "\"x\":" + point.x.ToString("F0", System.Globalization.CultureInfo.InvariantCulture) + "," +
            "\"y\":" + point.y.ToString("F0", System.Globalization.CultureInfo.InvariantCulture) + "," +
            "\"screen\":\"" + Screen.width + "x" + Screen.height + "\"," +
            "\"safeArea\":\"" + EscapeJson(Screen.safeArea.ToString()) + "\"," +
            "\"hudActive\":" + (K1L0CanvasRoot.HUD != null && K1L0CanvasRoot.HUD.gameObject.activeInHierarchy ? "true" : "false") + "," +
            "\"error\":\"" + EscapeJson(error ?? "") + "\"," +
            "\"panels\":\"" + EscapeJson(BuildPanelDebugState()) + "\"," +
            "\"dock\":\"" + EscapeJson(dockSummary) + "\"" +
            "}";

        StartCoroutine(APIManager.Instance.Post("/beam-debug", json, (success, response) =>
        {
            Debug.Log($"[K1L0HUD] Dock debug upload success={success} response={response}");
        }));
    }

    static string EscapeJson(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
    }

    string BuildPanelDebugState()
    {
        if (panels == null) return "panels=null";
        System.Text.StringBuilder sb = new System.Text.StringBuilder(256);
        for (int i = 0; i < panels.Length; i++)
        {
            K1L0GlassPanel panel = panels[i];
            if (panel == null)
            {
                sb.Append($" p{i}=null");
                continue;
            }

            RectTransform rt = panel.transform as RectTransform;
            Vector2 pos = rt != null ? rt.anchoredPosition : Vector2.zero;
            Vector2 size = rt != null ? rt.sizeDelta : Vector2.zero;
            sb.Append($" p{i}:open={(i < panelOpen.Length && panelOpen[i])} activeSelf={panel.gameObject.activeSelf} activeHier={panel.gameObject.activeInHierarchy} visible={panel.IsVisible} alpha={panel.CurrentAlpha:F2} pos=({pos.x:F0},{pos.y:F0}) size=({size.x:F0},{size.y:F0})");
        }
        return sb.ToString();
    }

    bool TryHandlePanelButtonFallback(Vector2 screenPoint)
    {
        if (EventSystem.current == null || panels == null)
            return false;

        bool anyPanelOpen = false;
        for (int i = 0; i < panelOpen.Length; i++)
        {
            if (panelOpen[i])
            {
                anyPanelOpen = true;
                break;
            }
        }

        if (!anyPanelOpen)
            return false;

        PointerEventData eventData = new PointerEventData(EventSystem.current)
        {
            position = screenPoint
        };

        List<RaycastResult> results = new List<RaycastResult>(16);
        EventSystem.current.RaycastAll(eventData, results);

        foreach (RaycastResult result in results)
        {
            GameObject go = result.gameObject;
            if (go == null)
                continue;

            Canvas hitCanvas = go.GetComponentInParent<Canvas>();
            if (hitCanvas == null || hitCanvas.name != "K1L0_Canvas")
                continue;

            Button button = go.GetComponentInParent<Button>();
            if (button == null || !button.IsActive() || !button.IsInteractable())
                continue;

            button.onClick.Invoke();
            return true;
        }

        return false;
    }

    K1L0GlassPanel CreateOnePanel(string name, Vector2 pos, Vector2 size, Vector4 contentPadding)
    {
        GameObject panelGO = new GameObject(name);
        panelGO.transform.SetParent(safeArea, false);

        RectTransform panelRT = panelGO.AddComponent<RectTransform>();
        panelRT.anchorMin = Vector2.zero;
        panelRT.anchorMax = Vector2.one;
        panelRT.pivot = new Vector2(0.5f, 0.5f);
        panelRT.anchoredPosition = Vector2.zero;
        panelRT.sizeDelta = Vector2.zero;
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;

        K1L0GlassPanel gp = panelGO.AddComponent<K1L0GlassPanel>();
        gp.InitializeVisual(monoFont, contentPadding);
        return gp;
    }

    private bool oldUIHidden;

    void EnsureStoriesUI()
    {
        var storiesBootstrapper = FindFirstObjectByType<KiloWorld.UI.Stories.StoriesUIBootstrapper>();
        if (storiesBootstrapper == null)
        {
            GameObject storiesGO = new GameObject("StoriesUI");
            storiesBootstrapper = storiesGO.AddComponent<KiloWorld.UI.Stories.StoriesUIBootstrapper>();
        }
        storiesBootstrapper.Build();

#if UNITY_EDITOR
        // In editor, apply test data so circles are visible without API
        var strip = FindFirstObjectByType<KiloWorld.UI.Stories.StoriesStrip>();
        if (strip != null)
        {
            var testData = storiesBootstrapper.gameObject.GetComponent<KiloWorld.UI.Stories.StoriesTestDataSource>();
            if (testData == null)
                testData = storiesBootstrapper.gameObject.AddComponent<KiloWorld.UI.Stories.StoriesTestDataSource>();
            testData.Configure(strip, true);
        }
#endif
    }

    void EnsureLocationBeams()
    {
        // Intentionally disabled.
    }

    void EnsureTransmissionFrame()
    {
        if (FindFirstObjectByType<TransmissionFrame>() == null)
        {
            var go = new GameObject("TransmissionFrame");
            var frame = go.AddComponent<TransmissionFrame>();
            frame.Initialize();
        }
    }

    void EnsureTransmitterEnterModal()
    {
        if (FindFirstObjectByType<TransmitterEnterModal>() == null)
        {
            var go = new GameObject("TransmitterEnterModal");
            var modal = go.AddComponent<TransmitterEnterModal>();
            modal.Initialize();
        }
    }

    void EnsureScreenshot()
    {
        if (FindFirstObjectByType<K1L0Screenshot>() == null)
        {
            var go = new GameObject("K1L0Screenshot");
            go.AddComponent<K1L0Screenshot>();
            DontDestroyOnLoad(go);
        }
    }

    void HideOldUI()
    {
        oldUIHidden = false;
    }

    void TryHideOldUI()
    {
        int hidden = 0;
        string[] canvasNames = { "PedometerCanvas" };
        foreach (string name in canvasNames)
        {
            var go = GameObject.Find(name);
            if (go != null)
            {
                var c = go.GetComponent<Canvas>();
                if (c != null && c.enabled)
                {
                    c.enabled = false;
                    Debug.Log($"[K1L0HUD] Hid {name}");
                }
                hidden++;
            }
        }
        if (hidden >= 1)
            oldUIHidden = true;
    }

    void OnDockButtonTapped(int index)
    {
        if (index < 0) return;
        Debug.Log($"[K1L0HUD] Dock button tapped index={index} currentlyOpen={panelOpen[index]}");
        TogglePanel(index, !panelOpen[index]);
    }

    void TogglePanel(int index, bool show)
    {
        if (index < 0 || index >= panelOpen.Length)
        {
            Debug.LogWarning($"[K1L0HUD] TogglePanel ignored invalid index={index}");
            return;
        }

        if (panels == null || index >= panels.Length || panels[index] == null)
        {
            Debug.LogWarning($"[K1L0HUD] TogglePanel rebuilding missing panels for index={index} panelsNull={panels == null}");
            CreatePanels();
        }

        if (panels == null || index >= panels.Length || panels[index] == null)
        {
            Debug.LogError($"[K1L0HUD] TogglePanel failed: panel index={index} is still missing");
            return;
        }

        if (show)
        {
            // Close any other open panel first — only 1 at a time
            for (int i = 0; i < panels.Length; i++)
            {
                if (i != index && panelOpen[i])
                {
                    panelOpen[i] = false;
                    if (panels[i] != null) panels[i].Hide();
                    if (dock != null) dock.SetActiveButton(i, false);
                }
            }
            panelOpen[index] = true;
            if (index == 3 && transmitterMode != null) transmitterMode.Show(null);
            panels[index].Show();
        }
        else
        {
            panelOpen[index] = false;
            if (index == 3 && transmitterMode != null) transmitterMode.Hide();
            if (panels[index] != null) panels[index].Hide();
        }
        if (dock != null) dock.SetActiveButton(index, show);

        bool anyOpen = false;
        for (int i = 0; i < panelOpen.Length; i++)
            if (panelOpen[i]) { anyOpen = true; break; }
        RefreshPanelDimmer();

        // Hide teasers when a panel is open (panels are now transparent)
        ApplyMapHudSuppression(anyOpen);
    }


#if UNITY_EDITOR
    private float demoTimer = -1f;
    private bool demoScreenshotTaken;

    void LateUpdate()
    {
        // Auto-screenshot 2 seconds after panels open
        if (demoTimer > 0f && !demoScreenshotTaken && Time.realtimeSinceStartup - demoTimer > 2f)
        {
            demoScreenshotTaken = true;
            string path = System.IO.Path.Combine(Application.temporaryCachePath, "k1l0_auto.png");
            ScreenCapture.CaptureScreenshot(path, 1);
            Debug.Log($"[K1L0HUD] Auto screenshot to {path}");
        }

        if (Input.GetKeyDown(KeyCode.Alpha1)) OnDockButtonTapped(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) OnDockButtonTapped(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) OnDockButtonTapped(2);
        if (Input.GetKeyDown(KeyCode.Alpha0))
        {
            for (int i = 0; i < 3; i++) TogglePanel(i, false);
        }
        if (Input.GetKeyDown(KeyCode.F12))
        {
            ScreenCapture.CaptureScreenshot("/tmp/k1l0_screenshot.png", 1);
            Debug.Log("[K1L0HUD] F12 screenshot saved");
        }
    }
#endif
}

[Serializable]
class NativeTransmissionPayload
{
    public string element;
    public string message;
    public string photoPath;
    public string mood;
}

[Serializable]
class NativeUserMetadataPayload
{
    public string name;
    public string callsign;
    public string cloakDesign;
    public string helmetDesign;
    public string selfiePath;
}

[Serializable]
class NativeUserMetadataApiRequest
{
    public string userId;
    public string name;
    public string callsign;
    public string cloakDesign;
    public string helmetDesign;
    public string selfieUrl;
}

[Serializable]
class NativeUserMetadataSaveResultPayload
{
    public bool ok;
    public string error;
    public string name;
    public string callsign;
    public string cloakDesign;
    public string helmetDesign;
    public string selfieUrl;
    public string helmetUrl;
    public string cloakUrl;
    public string avatarUrl;
    public string helmetTextureUrl;
    public string cloakTextureUrl;
    public long skinRevision;
    public string status;
}

[Serializable]
class NativeUserIdentityRenderRequest
{
    public string userId;
    public string selfieUrl;
    public string name;
    public string callsign;
    public string cloakDesign;
    public string helmetDesign;
}

[Serializable]
class NativeUserIdentityRenderResponse
{
    public bool ok;
    public string error;
    public string helmetUrl;
    public string cloakUrl;
    public string avatarUrl;
    public string helmetTextureUrl;
    public string cloakTextureUrl;
    public long skinRevision;
}

[Serializable]
class NativeImageUploadRequest
{
    public string userId;
    public string filename;
    public string contentType;
    public string imageBase64;
}

[Serializable]
class NativeImageUploadResponse
{
    public bool ok;
    public string url;
    public string error;
}
