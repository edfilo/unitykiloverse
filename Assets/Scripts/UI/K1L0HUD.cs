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
    private K1L0GlassPanel statusPanel;
    private K1L0GlassPanel profilePanel;
    private K1L0NearbyMode nearbyMode;
    private K1L0StatusMode statusMode;
    private K1L0ProfileMode profileMode;
    private K1L0GlassPanel[] panels;
    private bool[] panelOpen = new bool[3];

    private TMP_FontAsset monoFont;
    private TMP_FontAsset monoFontLight;
    private bool initialized;
    private Image sceneDimmer;
    private float dimmerTarget;
    private const float DimmerAlpha = 0.55f;
    private const float DimmerSpeed = 4f;

    public static Sprite RoundedRectSprite { get; private set; }

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
        DestroyExistingCanvas();
        CreateCanvas();
        EnsureEventSystem();
        CreateSceneDimmer();
        CreateWeatherBar();
        CreateMemLabel();
        CreateDock();
        CreatePanels();

        EnsureStoriesUI();
        EnsureScreenshot();
        EnsureLocationBeams();
        HideOldUI();
        Debug.Log("[K1L0HUD] Initialized successfully");

#if UNITY_EDITOR
        TogglePanel(0, true);
        Debug.Log("[K1L0HUD] Auto-opened nearby panel");
        demoTimer = Time.realtimeSinceStartup + 0.01f;
#endif
    }

    void CreateCanvas()
    {
        GameObject go = new GameObject("K1L0_Canvas");
        go.transform.SetParent(transform, false);
        canvas = go.AddComponent<Canvas>();

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 5000;

        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(390, 844);
        scaler.matchWidthOrHeight = 1f;

        go.AddComponent<GraphicRaycaster>();

        GameObject safeGO = new GameObject("SafeArea");
        safeGO.transform.SetParent(go.transform, false);
        safeArea = safeGO.AddComponent<RectTransform>();

        Rect safe = Screen.safeArea;
        Vector2 anchorMin = safe.position / new Vector2(Screen.width, Screen.height);
        Vector2 anchorMax = (safe.position + safe.size) / new Vector2(Screen.width, Screen.height);
        safeArea.anchorMin = anchorMin;
        safeArea.anchorMax = anchorMax;
        safeArea.offsetMin = Vector2.zero;
        safeArea.offsetMax = Vector2.zero;
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
        GameObject go = new GameObject("WeatherBar");
        go.transform.SetParent(safeArea, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(12, -6);
        rt.sizeDelta = new Vector2(-24, 18);

        weatherText = go.AddComponent<TextMeshProUGUI>();
        weatherText.font = monoFontLight;
        weatherText.fontSize = 15;
        weatherText.color = new Color(0.75f, 0.85f, 1f, 0.7f);
        weatherText.alignment = TextAlignmentOptions.TopLeft;
        weatherText.text = "";
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

    private static readonly float panelTopY = -96f;

    void CreatePanels()
    {
        Vector2 centeredPos = new Vector2(0, panelTopY);

        nearbyPanel = CreateOnePanel("NearbyPanel", centeredPos, new Vector2(0f, 500f), new Vector4(12f, 18f, 12f, 48f));
        nearbyPanel.OnCloseClicked = () => TogglePanel(0, false);
        GameObject nearbyGO = new GameObject("NearbyMode");
        nearbyMode = nearbyGO.AddComponent<K1L0NearbyMode>();
        nearbyMode.Initialize(nearbyPanel.contentArea, monoFont);
        nearbyPanel.gameObject.SetActive(false);

        statusPanel = CreateOnePanel("StatusPanel", centeredPos, new Vector2(0f, 356f), new Vector4(12f, 18f, 12f, 48f));
        statusPanel.OnCloseClicked = () => TogglePanel(1, false);
        GameObject statusGO = new GameObject("StatusMode");
        statusMode = statusGO.AddComponent<K1L0StatusMode>();
        statusMode.Initialize(statusPanel.contentArea, monoFont);
        statusPanel.gameObject.SetActive(false);

        profilePanel = CreateOnePanel("ProfilePanel", centeredPos, new Vector2(0f, 344f), new Vector4(12f, 18f, 12f, 48f));
        profilePanel.OnCloseClicked = () => TogglePanel(2, false);
        GameObject profileGO = new GameObject("ProfileMode");
        profileMode = profileGO.AddComponent<K1L0ProfileMode>();
        profileMode.Initialize(profilePanel.contentArea, monoFont);
        profilePanel.gameObject.SetActive(false);

        panels = new K1L0GlassPanel[] { nearbyPanel, statusPanel, profilePanel };
    }

    void HandleDockTouchFallback()
    {
        if (Input.touchCount == 0)
            return;

        Camera eventCamera = canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera ? canvas.worldCamera : null;

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.phase != TouchPhase.Began)
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

            if (!handled && dock != null && dock.TryHandleScreenPoint(touch.position, eventCamera))
                handled = true;

            if (handled)
                break;
        }
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
        panelRT.anchorMin = new Vector2(0f, 1);
        panelRT.anchorMax = new Vector2(1f, 1);
        panelRT.pivot = new Vector2(0.5f, 1);
        panelRT.anchoredPosition = pos;
        panelRT.sizeDelta = new Vector2(-18f, size.y);

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
        if (FindFirstObjectByType<K1L0LocationBeams>() == null)
        {
            var go = new GameObject("K1L0LocationBeams");
            var beams = go.AddComponent<K1L0LocationBeams>();
            beams.Initialize(monoFont);
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
        TogglePanel(index, !panelOpen[index]);
    }

    void TogglePanel(int index, bool show)
    {
        if (show)
        {
            // Close any other open panel first — only 1 at a time
            for (int i = 0; i < panels.Length; i++)
            {
                if (i != index && panelOpen[i])
                {
                    panelOpen[i] = false;
                    panels[i].Hide();
                    dock.SetActiveButton(i, false);
                }
            }
            panelOpen[index] = true;
            panels[index].Show();
        }
        else
        {
            panelOpen[index] = false;
            panels[index].Hide();
        }
        dock.SetActiveButton(index, show);

        bool anyOpen = false;
        for (int i = 0; i < panelOpen.Length; i++)
            if (panelOpen[i]) { anyOpen = true; break; }
        dimmerTarget = anyOpen ? DimmerAlpha : 0f;
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
