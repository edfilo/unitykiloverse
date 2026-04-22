using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Profiling;
using UnityEngine.UI;
using KiloWorld.Rendering;

public class K1L0ProfileMode : MonoBehaviour
{
    private static readonly Color TerminalGreen = new Color(0.56f, 1f, 0.62f, 1f);
    private static readonly Color TerminalDim = new Color(0.56f, 1f, 0.62f, 1f);
    private static readonly Color LineBgColor = new Color(0.01f, 0.02f, 0.01f, 0.82f);
    private const float LineBgPadH = 4f;
    private const float LineBgPadV = 1f;
    private const float RowH = 26f;
    private const float RowGap = 2f;
    private const float HeaderH = 20f;
    private const float SectionGap = 8f;

    private TMP_FontAsset monoFont;
    private TextMeshProUGUI bodyText;
    private TextMeshProUGUI authBtnLabel;
    private TextMeshProUGUI prodApiLabel;
    private K1L0GlassChrome authChrome;
    private K1L0GlassChrome prodChrome;
    private RectTransform lineBgContainer;
    private readonly List<Image> lineBgPool = new List<Image>();

    private static Slider activeSlider;
    public static void SetActiveSlider(Slider s) { activeSlider = s; }
    private TextMeshProUGUI perfText;
    private RectTransform scrollContent;
    private ScrollRect panelScrollRect;
    private Coroutine perfRoutine;

    public void Initialize(RectTransform parent, TMP_FontAsset font)
    {
        monoFont = font;

        RectTransform rt = gameObject.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(8f, 8f);
        rt.offsetMax = new Vector2(-8f, -8f);

        bodyText = CreateBodyText(rt);
        CreatePostFXPanel(rt);

        // ── Command buttons (bottom) ──
        float btnBase = 0f;
        var editBtn = CreateCommandButton(rt, "EditBtn", new Vector2(0f, 0f), new Vector2(0.48f, 0f), new Vector2(0f, 0f), new Vector2(0f, btnBase), new Vector2(0f, 32f), "EDIT PROFILE");
        editBtn.btn.onClick.AddListener(OnEditClick);

        var authBtn = CreateCommandButton(rt, "AuthBtn", new Vector2(0.52f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0f, btnBase), new Vector2(0f, 32f), "LOGOUT");
        authBtn.btn.onClick.AddListener(OnAuthClick);
        authBtnLabel = authBtn.label;
        authChrome = authBtn.chrome;

        var prodBtn = CreateCommandButton(rt, "ProdApiBtn", new Vector2(0f, 0f), new Vector2(0.48f, 0f), new Vector2(0f, 0f), new Vector2(0f, btnBase + 38f), new Vector2(0f, 32f), "PRODUCTION API OFF");
        prodBtn.btn.onClick.AddListener(OnProdApiToggle);
        prodApiLabel = prodBtn.label;
        prodChrome = prodBtn.chrome;

        var screenshotBtn = CreateCommandButton(rt, "ScreenshotBtn", new Vector2(0.52f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0f, btnBase + 38f), new Vector2(0f, 32f), "SEND SCREENSHOT");
        screenshotBtn.btn.onClick.AddListener(OnScreenshotClick);

        LoadProfile();
        UpdateAuthButton();
        UpdateProdApiToggle();
    }

    private TextMeshProUGUI CreateBodyText(RectTransform parent)
    {
        // Line background container
        GameObject bgGO = new GameObject("LineBgs", typeof(RectTransform));
        bgGO.transform.SetParent(parent, false);
        lineBgContainer = bgGO.GetComponent<RectTransform>();
        lineBgContainer.anchorMin = new Vector2(0f, 0.84f);
        lineBgContainer.anchorMax = new Vector2(1f, 1f);
        lineBgContainer.offsetMin = new Vector2(0f, 0f);
        lineBgContainer.offsetMax = new Vector2(-6f, 0f);

        GameObject go = new GameObject("ProfileBody", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.84f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.offsetMin = new Vector2(0f, 0f);
        rt.offsetMax = new Vector2(-6f, 0f);

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = monoFont;
        tmp.fontSize = 13f;
        tmp.color = TerminalDim;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.richText = true;
        return tmp;
    }

    // ────────────────────────────────────────────
    //  PostFX scroll panel
    // ────────────────────────────────────────────

    private void CreatePostFXPanel(RectTransform parent)
    {
        // Scroll view sits between body text and buttons
        var scrollGO = new GameObject("PostFXScroll", typeof(RectTransform));
        scrollGO.transform.SetParent(parent, false);
        var scrollRT = scrollGO.GetComponent<RectTransform>();
        scrollRT.anchorMin = new Vector2(0f, 0.14f);
        scrollRT.anchorMax = new Vector2(1f, 0.83f);
        scrollRT.offsetMin = Vector2.zero;
        scrollRT.offsetMax = new Vector2(-2f, 0f);

        // Viewport
        var viewportGO = new GameObject("Viewport", typeof(RectTransform));
        viewportGO.transform.SetParent(scrollGO.transform, false);
        var viewportRT = viewportGO.GetComponent<RectTransform>();
        viewportRT.anchorMin = Vector2.zero;
        viewportRT.anchorMax = Vector2.one;
        viewportRT.offsetMin = Vector2.zero;
        viewportRT.offsetMax = Vector2.zero;
        viewportGO.AddComponent<RectMask2D>();

        // Content
        var contentGO = new GameObject("Content", typeof(RectTransform));
        contentGO.transform.SetParent(viewportGO.transform, false);
        scrollContent = contentGO.GetComponent<RectTransform>();
        scrollContent.anchorMin = new Vector2(0, 1);
        scrollContent.anchorMax = new Vector2(1, 1);
        scrollContent.pivot = new Vector2(0.5f, 1);
        scrollContent.anchoredPosition = Vector2.zero;

        panelScrollRect = scrollGO.AddComponent<ScrollRect>();
        panelScrollRect.content = scrollContent;
        panelScrollRect.viewport = viewportRT;
        panelScrollRect.horizontal = false;
        panelScrollRect.vertical = true;
        panelScrollRect.movementType = ScrollRect.MovementType.Elastic;
        panelScrollRect.scrollSensitivity = 30f;

        // Build all controls
        var pfx = KiloWorld.Rendering.Systems.RenderManager.Instance?.profile?.postFX;
        if (pfx == null) return;

        float y = 0;

        // Perf stats
        y = AddPerfLabel(scrollContent, y);

        // ── COLOR GRADING ──
        y = AddHeader(scrollContent, y, "COLOR GRADING");
        y = AddSlider(scrollContent, y, "SATURATION", "saturation", -100, 100, pfx.saturation, v => pfx.saturation = v, true);
        y = AddSlider(scrollContent, y, "CONTRAST", "contrast", -100, 100, pfx.contrast, v => pfx.contrast = v, true);
        y = AddSlider(scrollContent, y, "HUE SHIFT", "hueShift", -100, 100, pfx.hueShift, v => pfx.hueShift = v, true);
        y = AddSlider(scrollContent, y, "TEMPERATURE", "temperature", -100, 100, pfx.temperature, v => pfx.temperature = v, true);
        y = AddSlider(scrollContent, y, "TINT", "tint", -100, 100, pfx.tint, v => pfx.tint = v, true);
        y = AddSlider(scrollContent, y, "EXPOSURE", "exposure", -5, 5, pfx.exposureFixedValue, v => pfx.exposureFixedValue = v);

        // ── BLOOM ──
        y = AddHeader(scrollContent, y, "BLOOM");
        y = AddToggle(scrollContent, y, "ENABLED", "bloomEnabled", pfx.bloomEnabled, v => pfx.bloomEnabled = v);
        y = AddSlider(scrollContent, y, "INTENSITY", "bloomIntensity", 0, 10, pfx.bloomIntensity, v => pfx.bloomIntensity = v);
        y = AddSlider(scrollContent, y, "THRESHOLD", "bloomThreshold", 0, 5, pfx.bloomThreshold, v => pfx.bloomThreshold = v);
        y = AddSlider(scrollContent, y, "SCATTER", "bloomScatter", 0, 1, pfx.bloomScatter, v => pfx.bloomScatter = v);

        // ── VIGNETTE ──
        y = AddHeader(scrollContent, y, "VIGNETTE");
        y = AddToggle(scrollContent, y, "ENABLED", "vignetteEnabled", pfx.vignetteEnabled, v => pfx.vignetteEnabled = v);
        y = AddSlider(scrollContent, y, "INTENSITY", "vignetteIntensity", 0, 1, pfx.vignetteIntensity, v => pfx.vignetteIntensity = v);
        y = AddSlider(scrollContent, y, "SMOOTHNESS", "vignetteSmoothness", 0.01f, 1, pfx.vignetteSmoothness, v => pfx.vignetteSmoothness = v);

        // ── CHROMATIC ABERRATION ──
        y = AddHeader(scrollContent, y, "CHROMATIC ABERRATION");
        y = AddToggle(scrollContent, y, "ENABLED", "chromaticEnabled", pfx.chromaticAberrationEnabled, v => pfx.chromaticAberrationEnabled = v);
        y = AddSlider(scrollContent, y, "INTENSITY", "chromaticIntensity", 0, 1, pfx.chromaticAberrationIntensity, v => pfx.chromaticAberrationIntensity = v);

        // ── LENS DISTORTION ──
        y = AddHeader(scrollContent, y, "LENS DISTORTION");
        y = AddToggle(scrollContent, y, "ENABLED", "lensDistEnabled", pfx.lensDistortionEnabled, v => pfx.lensDistortionEnabled = v);
        y = AddSlider(scrollContent, y, "INTENSITY", "lensDistIntensity", -1, 1, pfx.lensDistortionIntensity, v => pfx.lensDistortionIntensity = v);

        // ── DEPTH OF FIELD ──
        y = AddHeader(scrollContent, y, "DEPTH OF FIELD");
        y = AddToggle(scrollContent, y, "ENABLED", "dofEnabled", pfx.depthOfFieldEnabled, v => pfx.depthOfFieldEnabled = v);
        y = AddSlider(scrollContent, y, "FOCUS DIST", "focusDistance", 0.1f, 300, pfx.focusDistance, v => pfx.focusDistance = v);
        y = AddSlider(scrollContent, y, "APERTURE", "aperture", 0.05f, 32, pfx.aperture, v => pfx.aperture = v);
        y = AddSlider(scrollContent, y, "FOCAL LEN", "focalLength", 1, 300, pfx.focalLength, v => pfx.focalLength = v);

        // ── MOTION BLUR ──
        y = AddHeader(scrollContent, y, "MOTION BLUR");
        y = AddToggle(scrollContent, y, "ENABLED", "motionBlurEnabled", pfx.motionBlurEnabled, v => pfx.motionBlurEnabled = v);
        y = AddSlider(scrollContent, y, "INTENSITY", "motionBlurIntensity", 0, 1, pfx.motionBlurIntensity, v => pfx.motionBlurIntensity = v);

        // ── FILM GRAIN ──
        y = AddHeader(scrollContent, y, "FILM GRAIN");
        y = AddToggle(scrollContent, y, "ENABLED", "filmGrainEnabled", pfx.filmGrainEnabled, v => pfx.filmGrainEnabled = v);
        y = AddSlider(scrollContent, y, "INTENSITY", "filmGrainIntensity", 0, 1, pfx.filmGrainIntensity, v => pfx.filmGrainIntensity = v);

        // ── CAMERA (GOD VIEW) ──
        var cam = KiloWorld.Rendering.Systems.RenderManager.Instance?.profile?.camera;
        if (cam != null)
        {
            y = AddHeader(scrollContent, y, "CAMERA / GOD VIEW");
            y = AddSlider(scrollContent, y, "HEIGHT", "godPositionY", 10, 500, cam.godPositionY, v => cam.godPositionY = v);
            y = AddSlider(scrollContent, y, "DISTANCE", "godPositionZ", 10, 500, cam.godPositionZ, v => cam.godPositionZ = v);
            y = AddSlider(scrollContent, y, "PITCH", "godRotationX", 0, 90, cam.godRotationX, v => cam.godRotationX = v);
            y = AddSlider(scrollContent, y, "FAR CLIP", "farClipPlane", 100, 5000, cam.farClipPlane, v => cam.farClipPlane = v);
        }

        y -= 10f; // bottom padding
        scrollContent.sizeDelta = new Vector2(0, Mathf.Abs(y));
    }

    private float AddPerfLabel(RectTransform content, float y)
    {
        float h = 42f;
        var go = new GameObject("PerfStats", typeof(RectTransform));
        go.transform.SetParent(content, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.anchoredPosition = new Vector2(0, y);
        rt.sizeDelta = new Vector2(0, h);

        perfText = go.AddComponent<TextMeshProUGUI>();
        perfText.font = monoFont;
        perfText.fontSize = 10f;
        perfText.color = new Color(0.5f, 1f, 0.55f, 0.7f);
        perfText.alignment = TextAlignmentOptions.TopLeft;
        perfText.enableWordWrapping = true;
        perfText.richText = true;
        perfText.text = "> loading stats...";
        return y - h - SectionGap;
    }

    private float AddHeader(RectTransform content, float y, string text)
    {
        y -= SectionGap;
        var go = new GameObject($"Hdr_{text}", typeof(RectTransform));
        go.transform.SetParent(content, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.anchoredPosition = new Vector2(0, y);
        rt.sizeDelta = new Vector2(0, HeaderH);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = monoFont;
        tmp.fontSize = 10f;
        tmp.color = new Color(0.45f, 0.85f, 0.5f, 0.5f);
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.text = $"── {text} ──";
        return y - HeaderH;
    }

    private float AddSlider(RectTransform content, float y, string label, string prefsKey,
        float min, float max, float current, System.Action<float> setter, bool wholeNumbers = false)
    {
        var rowGO = new GameObject($"S_{prefsKey}", typeof(RectTransform));
        rowGO.transform.SetParent(content, false);
        var rowRT = rowGO.GetComponent<RectTransform>();
        rowRT.anchorMin = new Vector2(0, 1);
        rowRT.anchorMax = new Vector2(1, 1);
        rowRT.pivot = new Vector2(0.5f, 1);
        rowRT.anchoredPosition = new Vector2(0, y);
        rowRT.sizeDelta = new Vector2(0, RowH);

        // Label
        var labTmp = AddText(rowGO.transform, "Lab", new Vector2(0, 0), new Vector2(0.32f, 1), label, 9.5f, TerminalDim, TextAlignmentOptions.Left);

        // Value label
        var valTmp = AddText(rowGO.transform, "Val", new Vector2(0.85f, 0), new Vector2(1, 1), FormatFloat(current), 9f, TerminalGreen, TextAlignmentOptions.Right);

        // Slider
        var slider = CreateSliderVisual(rowGO.transform, new Vector2(0.33f, 0.15f), new Vector2(0.84f, 0.85f), min, max, current, wholeNumbers);
        var s = slider; // capture for closure
        slider.onValueChanged.AddListener(v => {
            if (activeSlider != null && activeSlider != s) return;
            setter(v);
            PlayerPrefs.SetFloat("k1lo_" + prefsKey, v);
            valTmp.text = FormatFloat(v);
        });

        return y - RowH - RowGap;
    }

    private float AddToggle(RectTransform content, float y, string label, string prefsKey,
        bool current, System.Action<bool> setter)
    {
        var rowGO = new GameObject($"T_{prefsKey}", typeof(RectTransform));
        rowGO.transform.SetParent(content, false);
        var rowRT = rowGO.GetComponent<RectTransform>();
        rowRT.anchorMin = new Vector2(0, 1);
        rowRT.anchorMax = new Vector2(1, 1);
        rowRT.pivot = new Vector2(0.5f, 1);
        rowRT.anchoredPosition = new Vector2(0, y);
        rowRT.sizeDelta = new Vector2(0, RowH);

        // Label
        AddText(rowGO.transform, "Lab", new Vector2(0, 0), new Vector2(0.32f, 1), label, 9.5f, TerminalDim, TextAlignmentOptions.Left);

        // Value label
        var valTmp = AddText(rowGO.transform, "Val", new Vector2(0.85f, 0), new Vector2(1, 1), current ? "ON" : "OFF", 9f, TerminalGreen, TextAlignmentOptions.Right);

        // Slider as 0/1 toggle
        var slider = CreateSliderVisual(rowGO.transform, new Vector2(0.33f, 0.15f), new Vector2(0.84f, 0.85f), 0, 1, current ? 1f : 0f, true);
        var s = slider; // capture for closure
        slider.onValueChanged.AddListener(v => {
            if (activeSlider != null && activeSlider != s) return;
            bool on = v >= 0.5f;
            setter(on);
            PlayerPrefs.SetFloat("k1lo_" + prefsKey, on ? 1f : 0f);
            valTmp.text = on ? "ON" : "OFF";
            valTmp.color = on ? TerminalGreen : new Color(0.5f, 0.5f, 0.5f, 0.6f);
        });
        if (!current) valTmp.color = new Color(0.5f, 0.5f, 0.5f, 0.6f);

        return y - RowH - RowGap;
    }

    private TextMeshProUGUI AddText(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
        string text, float fontSize, Color color, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = monoFont;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = align;
        tmp.text = text;
        tmp.raycastTarget = false;
        return tmp;
    }

    private Slider CreateSliderVisual(Transform parent, Vector2 anchorMin, Vector2 anchorMax,
        float min, float max, float value, bool wholeNumbers)
    {
        var go = new GameObject("Slider", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // Background
        var bg = new GameObject("Bg", typeof(RectTransform));
        bg.transform.SetParent(go.transform, false);
        FillRect(bg.GetComponent<RectTransform>());
        bg.AddComponent<Image>().color = new Color(0.06f, 0.10f, 0.06f, 0.9f);

        // Fill area
        var fa = new GameObject("FA", typeof(RectTransform));
        fa.transform.SetParent(go.transform, false);
        FillRect(fa.GetComponent<RectTransform>());

        var fill = new GameObject("Fill", typeof(RectTransform));
        fill.transform.SetParent(fa.transform, false);
        var fillRT = fill.GetComponent<RectTransform>();
        FillRect(fillRT);
        fill.AddComponent<Image>().color = new Color(0.47f, 1f, 0.54f, 0.25f);

        // Handle area
        var ha = new GameObject("HA", typeof(RectTransform));
        ha.transform.SetParent(go.transform, false);
        FillRect(ha.GetComponent<RectTransform>());

        var handle = new GameObject("H", typeof(RectTransform));
        handle.transform.SetParent(ha.transform, false);
        var hRT = handle.GetComponent<RectTransform>();
        hRT.sizeDelta = new Vector2(20f, 0f);
        hRT.anchorMin = new Vector2(0, 0);
        hRT.anchorMax = new Vector2(0, 1);
        var hImg = handle.AddComponent<Image>();
        hImg.color = TerminalGreen;

        var slider = go.AddComponent<Slider>();
        slider.fillRect = fillRT;
        slider.handleRect = hRT;
        slider.targetGraphic = hImg;
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = wholeNumbers;
        slider.value = value;

        // Block ScrollRect + claim exclusive active slider on touch
        var blocker = go.AddComponent<SliderScrollBlocker>();
        blocker.scrollRect = panelScrollRect;
        blocker.slider = slider;

        return slider;
    }

    private static void FillRect(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static string FormatFloat(float v)
    {
        if (Mathf.Abs(v) >= 10f) return $"{v:F0}";
        return $"{v:F2}";
    }

    // ────────────────────────────────────────────
    //  Command buttons
    // ────────────────────────────────────────────

    private (Button btn, K1L0GlassChrome chrome, TextMeshProUGUI label) CreateCommandButton(
        RectTransform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 position,
        Vector2 sizeDelta,
        string label)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = position;
        rt.sizeDelta = sizeDelta;

        K1L0GlassChrome chrome = K1L0GlassFactory.AttachChrome(go.transform, name, K1L0GlassFactory.ControlStyle);
        chrome.blurFill.material = null;
        chrome.blurFill.color = new Color(0.02f, 0.07f, 0.02f, 0.95f);
        chrome.overlay.color = new Color(0.01f, 0.04f, 0.01f, 0.90f);
        chrome.border.color = new Color(0.30f, 0.96f, 0.38f, 0.26f);
        chrome.accent.color = new Color(0.22f, 1f, 0.32f, 0.08f);
        chrome.sheen.color = new Color(0f, 0f, 0f, 0f);
        chrome.blurFill.raycastTarget = true;

        Button button = go.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = chrome.blurFill;

        GameObject labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(go.transform, false);
        RectTransform labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = Vector2.zero;
        labelRT.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.font = monoFont;
        tmp.fontSize = 12.5f;
        tmp.color = TerminalGreen;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.text = label;
        tmp.raycastTarget = false;

        return (button, chrome, tmp);
    }

    // ────────────────────────────────────────────
    //  Lifecycle
    // ────────────────────────────────────────────

    private void OnEnable()
    {
        LoadProfile();
        UpdateAuthButton();
        UpdateProdApiToggle();
        if (perfRoutine != null) StopCoroutine(perfRoutine);
        perfRoutine = StartCoroutine(UpdatePerfStats());
    }

    private void OnDisable()
    {
        if (perfRoutine != null)
        {
            StopCoroutine(perfRoutine);
            perfRoutine = null;
        }
    }

    private IEnumerator UpdatePerfStats()
    {
        while (true)
        {
            if (perfText != null)
            {
                float fps = 1f / Time.unscaledDeltaTime;
                long allocMB = Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024);
                float bat = SystemInfo.batteryLevel;
                string batStr = bat >= 0 ? $"{bat * 100f:F0}%" : "N/A";

                perfText.text =
                    $"> FPS <color=#BCFFC5>{fps:F0}</color>  MEM <color=#BCFFC5>{allocMB}MB</color>  BAT <color=#BCFFC5>{batStr}</color>\n" +
                    $"> GPU <color=#BCFFC5>{SystemInfo.graphicsDeviceName}</color>  {Screen.width}x{Screen.height}";
            }
            yield return new WaitForSecondsRealtime(1f);
        }
    }

    // ────────────────────────────────────────────
    //  Profile data
    // ────────────────────────────────────────────

    private void LoadProfile()
    {
        string callSign = "---";
        string channel = "---";
        string signal = FirebaseAuthManager.Instance != null && FirebaseAuthManager.Instance.isAuthenticated ? "AUTHENTICATED" : "ANON";
        string deviceId = DeviceIDManager.Instance != null ? DeviceIDManager.Instance.GetCurrentUserId() : "offline";

        RenderProfileText(callSign, signal, channel, deviceId);

        if (FirebaseRestClient.Instance != null && DeviceIDManager.Instance != null)
        {
            string userId = DeviceIDManager.Instance.GetCurrentUserId();
            FirebaseRestClient.Instance.GetFirestoreData("users", userId,
                (response) =>
                {
                    try
                    {
                        callSign = ExtractStringField(response, "callSign") ?? "---";
                        channel = ExtractStringField(response, "instagram");
                        channel = string.IsNullOrEmpty(channel) ? "---" : "@" + channel;
                        RenderProfileText(callSign, signal, channel, deviceId);
                    }
                    catch
                    {
                        RenderProfileText(callSign, signal, channel, deviceId);
                    }
                },
                _ => { });
        }
    }

    private string ExtractStringField(string json, string fieldName)
    {
        string key = $"\"{fieldName}\"";
        int idx = json.IndexOf(key);
        if (idx < 0) return null;
        string sv = "\"stringValue\":\"";
        int svIdx = json.IndexOf(sv, idx);
        if (svIdx < 0 || svIdx - idx > 60) return null;
        int start = svIdx + sv.Length;
        int end = json.IndexOf('"', start);
        if (end < 0) return null;
        return json.Substring(start, end - start);
    }

    private void RenderProfileText(string callSign, string signal, string channel, string deviceId)
    {
        StringBuilder sb = new StringBuilder(256);
        sb.AppendLine($"> CALLSIGN <color=#BCFFC5>{callSign}</color>  SIGNAL <color=#BCFFC5>{signal}</color>");
        sb.AppendLine($"> CHANNEL  <color=#BCFFC5>{channel}</color>");
        sb.Append($"> DEVICE   <color=#BCFFC5>{TrimDevice(deviceId)}</color>");
        bodyText.text = sb.ToString();
        UpdateLineBackgrounds();
    }

    private string TrimDevice(string deviceId)
    {
        if (string.IsNullOrEmpty(deviceId)) return "---";
        return deviceId.Length <= 18 ? deviceId : deviceId.Substring(0, 18) + "...";
    }

    private void UpdateLineBackgrounds()
    {
        bodyText.ForceMeshUpdate();
        TMP_TextInfo info = bodyText.textInfo;
        int lineCount = info.lineCount;

        int visibleIdx = 0;
        for (int i = 0; i < lineCount; i++)
        {
            TMP_LineInfo line = info.lineInfo[i];
            if (line.characterCount == 0) continue;

            Image bg;
            if (visibleIdx < lineBgPool.Count)
            {
                bg = lineBgPool[visibleIdx];
                bg.gameObject.SetActive(true);
            }
            else
            {
                GameObject bgObj = new GameObject($"LineBg{visibleIdx}");
                bgObj.transform.SetParent(lineBgContainer, false);
                bg = bgObj.AddComponent<Image>();
                bg.raycastTarget = false;
                lineBgPool.Add(bg);
            }

            bg.color = LineBgColor;
            RectTransform rect = bg.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            float lineTop = line.lineExtents.max.y;
            float lineBottom = line.lineExtents.min.y;
            float lineHeight = lineTop - lineBottom + LineBgPadV * 2f;
            float lineWidth = line.lineExtents.max.x - line.lineExtents.min.x + LineBgPadH * 2f;
            float centerX = (line.lineExtents.min.x + line.lineExtents.max.x) * 0.5f;
            float centerY = (lineTop + lineBottom) * 0.5f;

            rect.anchoredPosition = new Vector2(centerX, centerY);
            rect.sizeDelta = new Vector2(lineWidth, lineHeight);
            visibleIdx++;
        }

        for (int i = visibleIdx; i < lineBgPool.Count; i++)
        {
            lineBgPool[i].gameObject.SetActive(false);
        }
    }

    // ────────────────────────────────────────────
    //  Button handlers
    // ────────────────────────────────────────────

    private void OnEditClick()
    {
        ProfileEditorModal modal = Object.FindFirstObjectByType<ProfileEditorModal>();
        if (modal != null) modal.OpenModal();
    }

    private void OnAuthClick()
    {
        FirebaseAuthManager auth = FirebaseAuthManager.Instance;
        if (auth == null) return;

        if (auth.isAuthenticated)
        {
            auth.SignOut();
            LoadProfile();
            UpdateAuthButton();
        }
        else
        {
            LoginUI loginUI = Object.FindFirstObjectByType<LoginUI>();
            if (loginUI != null) loginUI.ShowLogin();
        }
    }

    private void UpdateAuthButton()
    {
        if (authBtnLabel == null) return;
        bool loggedIn = FirebaseAuthManager.Instance != null && FirebaseAuthManager.Instance.isAuthenticated;
        authBtnLabel.text = loggedIn ? "LOGOUT" : "LOGIN";
        if (authChrome != null)
        {
            authChrome.baseAccent = loggedIn ? new Color(0.70f, 0.20f, 0.20f, 0.24f) : new Color(0.22f, 1f, 0.32f, 0.14f);
            authChrome.SetAccent(1f);
        }
    }

    private void OnScreenshotClick()
    {
        K1L0Screenshot ss = K1L0Screenshot.Instance;
        if (ss != null) ss.Capture();
    }

    private void OnProdApiToggle()
    {
        bool current = APIManager.IsProductionOverride();
        APIManager.SetProductionOverride(!current);
        UpdateProdApiToggle();
    }

    private void UpdateProdApiToggle()
    {
        if (prodApiLabel == null) return;
        bool on = APIManager.IsProductionOverride();
        prodApiLabel.text = on ? "PRODUCTION API ON" : "PRODUCTION API OFF";
        if (prodChrome != null)
        {
            prodChrome.baseAccent = on ? new Color(0.22f, 1f, 0.32f, 0.18f) : new Color(0.18f, 0.72f, 1f, 0.16f);
            prodChrome.SetAccent(1f);
        }
    }

    [System.Serializable]
    private class ProfileData
    {
        public string callSign;
        public string instagram;
    }
}

public class SliderScrollBlocker : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public ScrollRect scrollRect;
    public Slider slider;

    public void OnPointerDown(PointerEventData eventData)
    {
        K1L0ProfileMode.SetActiveSlider(slider);
        if (scrollRect != null) scrollRect.enabled = false;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        K1L0ProfileMode.SetActiveSlider(null);
        if (scrollRect != null) scrollRect.enabled = true;
    }
}
