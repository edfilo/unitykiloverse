using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class K1L0HUD : MonoBehaviour
{
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

    public static Sprite RoundedRectSprite { get; private set; }
    public static float PanelMapBrightness => Mathf.Clamp01(PlayerPrefs.GetFloat(PanelMapBrightnessPref, 0.01f));
    public static bool IsSurveillanceCameraOn => Instance != null && Instance._mapVisible;

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
        CreateSceneDimmer();
        CreateWeatherBar();
        CreateDock();
        CreateTerminalHudToggle();
        CreateSurveillanceCamBadge();
        CreatePanels();

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

    // Top-right circular badge holding the surveillance-camera glyph — a small
    // "you are being watched" motif for the K1L0 HUD.
    void CreateSurveillanceCamBadge()
    {
        const float diameter = 100f;   // circle size
        const float camSize = 80f;     // camera glyph size inside

        var go = new GameObject("SurveillanceCamBadge", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(safeArea != null ? safeArea : K1L0CanvasRoot.HUD, false);
        go.transform.SetAsLastSibling();
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-16f, -16f);   // inset from top-right safe-area corner
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
        KiloWorld.Rendering.Systems.RenderManager.SurveillanceActive = visible;   // aurora sky reflects camera on/off
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
                cam.cullingMask = _savedCullingMask;
            }
            else
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                cam.cullingMask = 0;   // render nothing 3D → black
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

    void ToggleHudVisible()
    {
        SetHudVisible(!hudUserVisible);
    }

    void SetHudVisible(bool visible)
    {
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
