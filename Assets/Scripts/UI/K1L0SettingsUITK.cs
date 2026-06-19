using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UIElements;
using KiloWorld.UI.Stories;

// UI Toolkit rebuild of the K1L0 settings screen.
//
// Why this exists: the old uGUI version (K1L0ProfileMode + SliderScrollBlocker) hand-rolled
// gesture arbitration between sliders and the scroll view via a global "activeSlider" flag and
// synthetic event forwarding. That dropped value updates mid-drag and let two drag handlers fight
// over the same pointer — the "sticking" sliders. UI Toolkit's pointer-capture model gives one
// element exclusive ownership of a touch until release (same as iOS UIKit touch tracking), so the
// ScrollView cannot steal a slider drag and vice-versa. No custom arbitration needed.
public class K1L0SettingsUITK : MonoBehaviour
{
    // Terminal palette (RGBA 0-1)
    static readonly Color Green = Color.white;
    static readonly Color GreenDim = new Color(1f, 1f, 1f, 0.55f);
    static readonly Color GreenFaint = new Color(1f, 1f, 1f, 0.72f);
    static readonly Color PanelBg = new Color(0.02f, 0.05f, 0.02f, 0.96f);
    static readonly Color RowBg = new Color(0.06f, 0.10f, 0.06f, 0.9f);
    static readonly Color BorderCol = new Color(0.30f, 0.96f, 0.38f, 0.30f);

    PanelSettings panelSettings;
    UIDocument uiDoc;
    Font font;
    System.Action onClose;

    Label perfLabel;
    Label beamLabel;
    Label headerLabel;
    Coroutine perfRoutine;
    bool built;
    VisualElement activeSliderElement;
    int activeSliderPointerId = -1;

    public void Initialize(Font uiFont, System.Action closeCallback)
    {
        font = uiFont;
        onClose = closeCallback;

        panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
        panelSettings.name = "K1L0SettingsPanelSettings";
        panelSettings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("K1L0RuntimeTheme");
        panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
        panelSettings.scale = 1f;
        panelSettings.sortingOrder = 50f; // above the uGUI HUD overlay canvases

        uiDoc = gameObject.AddComponent<UIDocument>();
        uiDoc.panelSettings = panelSettings;

        Build();
        Hide();
    }

    // ── Show / hide ────────────────────────────────────────────────

    public void Show()
    {
        if (uiDoc?.rootVisualElement == null) return;
        uiDoc.rootVisualElement.style.display = DisplayStyle.Flex;
        if (perfRoutine != null) StopCoroutine(perfRoutine);
        perfRoutine = StartCoroutine(UpdateStats());
        LoadProfile();
    }

    public void Hide()
    {
        if (uiDoc?.rootVisualElement != null)
            uiDoc.rootVisualElement.style.display = DisplayStyle.None;
        if (perfRoutine != null) { StopCoroutine(perfRoutine); perfRoutine = null; }
    }

    // ── Build the tree ─────────────────────────────────────────────

    void Build()
    {
        if (built) return;
        built = true;

        var root = uiDoc.rootVisualElement;
        root.style.flexGrow = 1;
        root.pickingMode = PickingMode.Ignore; // empty areas pass through
        if (font != null) root.style.unityFont = font;
        root.style.fontSize = 12;
        root.style.color = Green;

        // Full-screen, transparent panel (see the 3D scene behind it).
        var panel = new VisualElement();
        panel.style.position = Position.Absolute;
        panel.style.left = 0;
        panel.style.right = 0;
        panel.style.top = 0;
        panel.style.bottom = 0;
        panel.style.backgroundColor = new Color(0f, 0f, 0f, 0f); // transparent
        panel.style.paddingLeft = 16;
        panel.style.paddingRight = 16;
        panel.style.paddingTop = 54;  // clear the status bar / notch
        panel.style.paddingBottom = 30;
        root.Add(panel);

        // ── Title bar ──
        var titleBar = Row(22);
        titleBar.style.marginBottom = 6;
        var title = new Label("SETTINGS");
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.fontSize = 13;
        title.style.flexGrow = 1;
        titleBar.Add(title);

        var closeBtn = new Button(() => onClose?.Invoke()) { text = "✕" };
        StyleFlatButton(closeBtn);
        closeBtn.style.width = 26;
        closeBtn.style.height = 20;
        titleBar.Add(closeBtn);
        panel.Add(titleBar);

        // ── Profile header ──
        headerLabel = MonoLabel("> SIGNAL ---\n> DEVICE   ---");
        headerLabel.style.fontSize = 11;
        headerLabel.style.marginBottom = 6;
        headerLabel.style.whiteSpace = WhiteSpace.Normal;
        panel.Add(headerLabel);

        // ── Scroll body ──
        var scroll = new ScrollView(ScrollViewMode.Vertical);
        scroll.style.flexGrow = 1;
        scroll.mode = ScrollViewMode.Vertical;
        scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden; // clean, fling to scroll
        scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        // Inertial (kinetic) touch scrolling with elastic over-scroll, like iOS.
        scroll.touchScrollBehavior = ScrollView.TouchScrollBehavior.Elastic;
        scroll.elasticity = 0.1f;
        scroll.scrollDecelerationRate = 0.135f;
        panel.Add(scroll);
        var body = scroll.contentContainer;

        BuildControls(body);

        // ── Command buttons ──
        var footer = new VisualElement();
        footer.style.marginTop = 8;
        footer.style.flexDirection = FlexDirection.Column;
        panel.Add(footer);

        var rowA = Row(0); rowA.style.marginBottom = 6;
        rowA.Add(CommandButton("EDIT PROFILE", OnEditClick, true));
        rowA.Add(CommandButton("LOGOUT", OnAuthClick, false, () => authButton = LastButton));
        footer.Add(rowA);

        var rowB = Row(0);
        rowB.Add(CommandButton("PRODUCTION API OFF", OnProdApiToggle, true, () => prodButton = LastButton));
        rowB.Add(CommandButton("SEND SCREENSHOT", OnScreenshotClick, false));
        footer.Add(rowB);

        UpdateAuthButton();
        UpdateProdApiToggle();
    }

    // Builds every section + control, mirroring the original K1L0ProfileMode ordering.
    void BuildControls(VisualElement body)
    {
        // Perf + ring debug
        perfLabel = MonoLabel("> loading stats...");
        perfLabel.style.fontSize = 10;
        perfLabel.style.color = GreenFaint;
        perfLabel.style.whiteSpace = WhiteSpace.Normal;
        perfLabel.style.marginBottom = 6;
        body.Add(perfLabel);

        beamLabel = MonoLabel("RING DEBUG\nPORTAL AUDIT: waiting...");
        beamLabel.style.fontSize = 10;
        beamLabel.style.color = GreenFaint;
        beamLabel.style.whiteSpace = WhiteSpace.Normal;
        beamLabel.style.marginBottom = 6;
        body.Add(beamLabel);

        Header(body, "DEBUG");
        Toggle(body, "PORTAL DIST", "showBeamDistanceLabels", SignalBeamBridge.ShowDistanceLabels, SignalBeamBridge.SetDistanceLabelsVisible);
        Toggle(body, "STORIES", "showStoryStrip", StoriesStripVisibility.ShowStoryStrip, StoriesStripVisibility.SetStoryStripVisible);
        Sliders(body, "MAP BRIGHT", "panelMapBrightness", 0f, 1f, K1L0HUD.PanelMapBrightness, K1L0HUD.SetPanelMapBrightness);

        var director = SignalDirectorV2.Instance ?? FindFirstObjectByType<SignalDirectorV2>();
        float minSteps = director != null ? director.ambientMinStepsToSpawn : PlayerPrefs.GetFloat("k1lo_ambientMinStepsToSpawn", 200f);
        float graceMinutes = director != null ? director.momentumSessionGraceMinutes : PlayerPrefs.GetFloat("k1lo_momentumSessionGraceMinutes", 1.5f);
        float ttlMinutes = director != null ? director.ambientBeamTtlMinutes : PlayerPrefs.GetFloat("k1lo_ambientBeamTtlMinutes", 20f);
        float collectRadius = director != null ? director.ambientCollectRadiusMeters : PlayerPrefs.GetFloat("k1lo_ambientCollectRadiusMeters", 10f);
        Header(body, "PORTAL SPAWN");
        Sliders(body, "MIN STEPS", "ambientMinStepsToSpawn", 0f, 1000f, minSteps, v =>
        {
            var d = SignalDirectorV2.Instance ?? FindFirstObjectByType<SignalDirectorV2>();
            if (d != null) d.ambientMinStepsToSpawn = Mathf.RoundToInt(v);
        }, true);
        Sliders(body, "RESET GRACE", "momentumSessionGraceMinutes", 1f, 30f, graceMinutes, v =>
        {
            var d = SignalDirectorV2.Instance ?? FindFirstObjectByType<SignalDirectorV2>();
            if (d != null) d.momentumSessionGraceMinutes = Mathf.Clamp(v, 1f, 30f);
        });
        Sliders(body, "EXPIRE MIN", "ambientBeamTtlMinutes", 1f, 240f, ttlMinutes, v =>
        {
            var d = SignalDirectorV2.Instance ?? FindFirstObjectByType<SignalDirectorV2>();
            if (d != null) d.ambientBeamTtlMinutes = Mathf.Clamp(v, 1f, 240f);
        }, true);
        Sliders(body, "COLLECT RADIUS", "ambientCollectRadiusMeters", 1f, 100f, collectRadius, v =>
        {
            var d = SignalDirectorV2.Instance ?? FindFirstObjectByType<SignalDirectorV2>();
            if (d != null) d.ambientCollectRadiusMeters = Mathf.Clamp(v, 1f, 100f);
        }, true);

        var sky = KiloWorld.Rendering.Systems.RenderManager.Instance?.profile?.sky;
        if (sky != null)
        {
            Header(body, "AURORA");
            Toggle(body, "ENABLED", "auroraEnabled", sky.auroraEnabled, v => sky.auroraEnabled = v);
            Sliders(body, "INTENSITY", "auroraIntensity", 0f, 2f, sky.auroraIntensity, v => sky.auroraIntensity = v);
            Sliders(body, "HEIGHT", "auroraHeight", 20f, 300f, sky.auroraHeight, v => sky.auroraHeight = v);
            Sliders(body, "DISTANCE", "auroraDistance", 80f, 900f, sky.auroraDistance, v => sky.auroraDistance = v);
            Sliders(body, "WIDTH", "auroraWidth", 80f, 900f, sky.auroraWidth, v => sky.auroraWidth = v);
            Sliders(body, "DRIFT", "auroraDriftSpeed", 0f, 2f, sky.auroraDriftSpeed, v => sky.auroraDriftSpeed = v);

            Header(body, "MANUAL SKY (GPS OFF)");
            float manualHour = PlayerPrefs.GetFloat("k1lo_manualHour", 13f);
            KiloWorld.Rendering.Systems.RenderManager.ManualHour = manualHour;
            Sliders(body, "TIME OF DAY", "manualHour", 0f, 24f, manualHour,
                v =>
                {
                    KiloWorld.Rendering.Systems.RenderManager.ManualHour = v;
                    KiloWorld.Rendering.Systems.RenderManager.NotifyManualSkyChanged();
                });
            WeatherRow(body);
        }

        var pfx = KiloWorld.Rendering.Systems.RenderManager.Instance?.profile?.postFX;
        if (pfx != null)
        {
            Header(body, "COLOR GRADING");
            Sliders(body, "BRIGHTNESS", "exposureFixedValue", -1f, 2f, pfx.exposureFixedValue, v => pfx.exposureFixedValue = v, true);
            Sliders(body, "SATURATION", "saturation", -100, 100, pfx.saturation, v => pfx.saturation = v, true);
            Sliders(body, "CONTRAST", "contrast", -100, 100, pfx.contrast, v => pfx.contrast = v, true);
            Sliders(body, "HUE SHIFT", "hueShift", -100, 100, pfx.hueShift, v => pfx.hueShift = v, true);
            Sliders(body, "TEMPERATURE", "temperature", -100, 100, pfx.temperature, v => pfx.temperature = v, true);
            Sliders(body, "TINT", "tint", -100, 100, pfx.tint, v => pfx.tint = v, true);

            Header(body, "BLOOM");
            Toggle(body, "ENABLED", "bloomEnabled", pfx.bloomEnabled, v => pfx.bloomEnabled = v);
            Sliders(body, "INTENSITY", "bloomIntensity", 0, 10, pfx.bloomIntensity, v => pfx.bloomIntensity = v);
            Sliders(body, "THRESHOLD", "bloomThreshold", 0, 5, pfx.bloomThreshold, v => pfx.bloomThreshold = v);
            Sliders(body, "SCATTER", "bloomScatter", 0, 1, pfx.bloomScatter, v => pfx.bloomScatter = v);

            Header(body, "VIGNETTE");
            Toggle(body, "ENABLED", "vignetteEnabled", pfx.vignetteEnabled, v => pfx.vignetteEnabled = v);
            Sliders(body, "INTENSITY", "vignetteIntensity", 0, 1, pfx.vignetteIntensity, v => pfx.vignetteIntensity = v);
            Sliders(body, "SMOOTHNESS", "vignetteSmoothness", 0.01f, 1, pfx.vignetteSmoothness, v => pfx.vignetteSmoothness = v);

            Header(body, "CHROMATIC ABERRATION");
            Toggle(body, "ENABLED", "chromaticEnabled", pfx.chromaticAberrationEnabled, v => pfx.chromaticAberrationEnabled = v);
            Sliders(body, "INTENSITY", "chromaticIntensity", 0, 1, pfx.chromaticAberrationIntensity, v => pfx.chromaticAberrationIntensity = v);

            Header(body, "LENS DISTORTION");
            Toggle(body, "ENABLED", "lensDistEnabled", pfx.lensDistortionEnabled, v => pfx.lensDistortionEnabled = v);
            Sliders(body, "INTENSITY", "lensDistIntensity", -1, 1, pfx.lensDistortionIntensity, v => pfx.lensDistortionIntensity = v);

            Header(body, "DEPTH OF FIELD");
            Toggle(body, "ENABLED", "dofEnabled", pfx.depthOfFieldEnabled, v => pfx.depthOfFieldEnabled = v);
            Sliders(body, "FOCUS DIST", "focusDistance", 0.1f, 300, pfx.focusDistance, v => pfx.focusDistance = v);
            Sliders(body, "APERTURE", "aperture", 0.05f, 32, pfx.aperture, v => pfx.aperture = v);
            Sliders(body, "FOCAL LEN", "focalLength", 1, 300, pfx.focalLength, v => pfx.focalLength = v);

            Header(body, "MOTION BLUR");
            Toggle(body, "ENABLED", "motionBlurEnabled", pfx.motionBlurEnabled, v => pfx.motionBlurEnabled = v);
            Sliders(body, "INTENSITY", "motionBlurIntensity", 0, 1, pfx.motionBlurIntensity, v => pfx.motionBlurIntensity = v);

            Header(body, "FILM GRAIN");
            Toggle(body, "ENABLED", "filmGrainEnabled", pfx.filmGrainEnabled, v => pfx.filmGrainEnabled = v);
            Sliders(body, "INTENSITY", "filmGrainIntensity", 0, 1, pfx.filmGrainIntensity, v => pfx.filmGrainIntensity = v);
        }

        var cam = KiloWorld.Rendering.Systems.RenderManager.Instance?.profile?.camera;
        if (cam != null)
        {
            Header(body, "CAMERA / GOD VIEW");
            Sliders(body, "HEIGHT", "godPositionY", 10, 500, cam.godPositionY, v => { cam.godPositionY = v; ApplyCameraLiveUpdate(); });
            Sliders(body, "DISTANCE", "godPositionZ", 10, 500, cam.godPositionZ, v => { cam.godPositionZ = v; ApplyCameraLiveUpdate(); });
            Sliders(body, "PITCH", "godRotationX", -90, 90, cam.godRotationX, v => { cam.godRotationX = v; ApplyCameraLiveUpdate(); });
            Sliders(body, "FAR CLIP", "farClipPlane", 100, 5000, cam.farClipPlane, v => { cam.farClipPlane = v; ApplyCameraLiveUpdate(); });
        }
    }

    // ── Control builders ───────────────────────────────────────────

    void Header(VisualElement parent, string text)
    {
        var l = new Label($"── {text} ──");
        l.style.fontSize = 10;
        l.style.color = GreenDim;
        l.style.marginTop = 8;
        l.style.marginBottom = 2;
        parent.Add(l);
    }

    void Sliders(VisualElement parent, string label, string prefsKey, float min, float max,
        float current, System.Action<float> setter, bool wholeNumbers = false)
    {
        var row = Row(26);
        var name = new Label(label) { style = { width = Length.Percent(30), fontSize = 9.5f, color = Green } };
        var slider = new Slider(min, max) { value = current };
        slider.style.flexGrow = 1;
        slider.style.marginLeft = 4;
        slider.style.marginRight = 4;
        RegisterSliderPointerOwnership(slider);
        var val = new Label(Fmt(current)) { style = { width = 46, fontSize = 9, color = Green, unityTextAlign = TextAnchor.MiddleRight } };

        slider.RegisterValueChangedCallback(evt =>
        {
            if (!CanSliderApply(slider)) return;
            float v = wholeNumbers ? Mathf.Round(evt.newValue) : evt.newValue;
            setter(v);
            PlayerPrefs.SetFloat("k1lo_" + prefsKey, v);
            val.text = Fmt(v);
        });

        row.Add(name); row.Add(slider); row.Add(val);
        parent.Add(row);
    }

    void Toggle(VisualElement parent, string label, string prefsKey, bool current, System.Action<bool> setter)
    {
        var row = Row(26);
        var name = new Label(label) { style = { width = Length.Percent(30), fontSize = 9.5f, color = Green } };
        var spacer = new VisualElement { style = { flexGrow = 1 } };
        var btn = new Button { text = current ? "ON" : "OFF" };
        StyleFlatButton(btn);
        btn.style.width = 54;
        btn.style.color = Green;

        bool state = current;
        float lastToggleTime = -10f;
        btn.clicked += () =>
        {
            if (Time.unscaledTime - lastToggleTime < 0.25f) return;
            lastToggleTime = Time.unscaledTime;
            state = !state;
            setter(state);
            PlayerPrefs.SetFloat("k1lo_" + prefsKey, state ? 1f : 0f);
            btn.text = state ? "ON" : "OFF";
            btn.style.color = Green;
        };
        row.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation(), TrickleDown.TrickleDown);
        row.RegisterCallback<PointerUpEvent>(evt => evt.StopPropagation(), TrickleDown.TrickleDown);
        btn.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation(), TrickleDown.TrickleDown);
        btn.RegisterCallback<PointerUpEvent>(evt => evt.StopPropagation(), TrickleDown.TrickleDown);

        row.Add(name); row.Add(spacer); row.Add(btn);
        parent.Add(row);
    }

    static readonly string[] WeatherGlyphs = { "clear", "partly cloudy", "cloudy", "overcast", "rain", "snow", "fog", "storm" };
    static readonly string[] WeatherNames = { "CLEAR", "PARTLY", "CLOUDY", "OVERCAST", "RAIN", "SNOW", "FOG", "STORM" };

    void WeatherRow(VisualElement parent)
    {
        int curIdx = Mathf.Clamp(Mathf.RoundToInt(PlayerPrefs.GetFloat("k1lo_manualWeather", 0f)), 0, WeatherGlyphs.Length - 1);
        KiloWorld.Rendering.Systems.RenderManager.ManualWeatherGlyph = WeatherGlyphs[curIdx];

        var row = Row(26);
        var name = new Label("WEATHER") { style = { width = Length.Percent(30), fontSize = 9.5f, color = Green } };
        var slider = new SliderInt(0, WeatherGlyphs.Length - 1) { value = curIdx };
        slider.style.flexGrow = 1;
        slider.style.marginLeft = 4;
        slider.style.marginRight = 4;
        RegisterSliderPointerOwnership(slider);
        var val = new Label(WeatherNames[curIdx]) { style = { width = 70, fontSize = 9, color = Green, unityTextAlign = TextAnchor.MiddleRight } };

        slider.RegisterValueChangedCallback(evt =>
        {
            if (!CanSliderApply(slider)) return;
            int idx = Mathf.Clamp(evt.newValue, 0, WeatherGlyphs.Length - 1);
            KiloWorld.Rendering.Systems.RenderManager.ManualWeatherGlyph = WeatherGlyphs[idx];
            PlayerPrefs.SetFloat("k1lo_manualWeather", idx);
            PlayerPrefs.SetString("k1lo_manualWeatherGlyph", WeatherGlyphs[idx]);
            KiloWorld.Rendering.Systems.RenderManager.NotifyManualSkyChanged();
            val.text = WeatherNames[idx];
        });

        row.Add(name); row.Add(slider); row.Add(val);
        parent.Add(row);
    }

    // ── Style helpers ──────────────────────────────────────────────

    VisualElement Row(float height)
    {
        var r = new VisualElement();
        r.style.flexDirection = FlexDirection.Row;
        r.style.alignItems = Align.Center;
        if (height > 0) r.style.height = height;
        return r;
    }

    Label MonoLabel(string text)
    {
        var l = new Label(text);
        l.style.color = Green;
        return l;
    }

    Button LastButton;

    VisualElement CommandButton(string text, System.Action onClick, bool first, System.Action capture = null)
    {
        var btn = new Button(onClick) { text = text };
        StyleFlatButton(btn);
        btn.style.flexGrow = 1;
        btn.style.height = 30;
        btn.style.fontSize = 12;
        if (first) btn.style.marginRight = 6; else btn.style.marginLeft = 6;
        LastButton = btn;
        capture?.Invoke();
        return btn;
    }

    void StyleFlatButton(Button b)
    {
        b.style.backgroundColor = new Color(0.02f, 0.07f, 0.02f, 0.95f);
        b.style.color = Green;
        SetBorder(b, BorderCol, 1);
        SetBorderRadius(b, 4);
        b.style.unityFontStyleAndWeight = FontStyle.Bold;
        if (font != null) b.style.unityFont = font;
    }

    void RegisterSliderPointerOwnership(VisualElement slider)
    {
        slider.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (activeSliderElement != null && activeSliderElement != slider)
            {
                evt.StopImmediatePropagation();
                return;
            }
            activeSliderElement = slider;
            activeSliderPointerId = evt.pointerId;
            slider.CapturePointer(evt.pointerId);
        }, TrickleDown.TrickleDown);

        slider.RegisterCallback<PointerUpEvent>(evt => ReleaseSliderPointer(slider, evt.pointerId), TrickleDown.TrickleDown);
        slider.RegisterCallback<PointerCancelEvent>(evt => ReleaseSliderPointer(slider, evt.pointerId), TrickleDown.TrickleDown);
        slider.RegisterCallback<PointerCaptureOutEvent>(_ =>
        {
            if (activeSliderElement == slider)
            {
                activeSliderElement = null;
                activeSliderPointerId = -1;
            }
        });
    }

    bool CanSliderApply(VisualElement slider)
    {
        return activeSliderElement == null || activeSliderElement == slider;
    }

    void ReleaseSliderPointer(VisualElement slider, int pointerId)
    {
        if (activeSliderElement != slider || activeSliderPointerId != pointerId) return;
        if (slider.HasPointerCapture(pointerId)) slider.ReleasePointer(pointerId);
        activeSliderElement = null;
        activeSliderPointerId = -1;
    }

    static void SetBorder(VisualElement e, Color c, float w)
    {
        e.style.borderLeftColor = c; e.style.borderRightColor = c;
        e.style.borderTopColor = c; e.style.borderBottomColor = c;
        e.style.borderLeftWidth = w; e.style.borderRightWidth = w;
        e.style.borderTopWidth = w; e.style.borderBottomWidth = w;
    }

    static void SetBorderRadius(VisualElement e, float r)
    {
        e.style.borderTopLeftRadius = r; e.style.borderTopRightRadius = r;
        e.style.borderBottomLeftRadius = r; e.style.borderBottomRightRadius = r;
    }

    static string Fmt(float v) => Mathf.Abs(v) >= 10f ? $"{v:F0}" : $"{v:F2}";

    void ApplyCameraLiveUpdate()
    {
        var controller = Object.FindFirstObjectByType<KiloFirstPersonController>();
        if (controller != null) controller.ApplyCameraProfileNow();
    }

    // ── Stats coroutine ────────────────────────────────────────────

    IEnumerator UpdateStats()
    {
        while (true)
        {
            if (perfLabel != null)
            {
                float fps = 1f / Time.unscaledDeltaTime;
                long allocMB = Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024);
                float bat = SystemInfo.batteryLevel;
                string batStr = bat >= 0 ? $"{bat * 100f:F0}%" : "N/A";
                perfLabel.text =
                    $"> FPS {fps:F0}  MEM {allocMB}MB  BAT {batStr}\n" +
                    $"> GPU {SystemInfo.graphicsDeviceName}  {Screen.width}x{Screen.height}";
            }
            if (beamLabel != null)
            {
                var director = SignalDirectorV2.Instance;
                beamLabel.text = director != null ? director.GetBeamDebugTextForSettings() : "RING DEBUG\nSignalDirector missing";
            }
            yield return new WaitForSecondsRealtime(1f);
        }
    }

    // ── Profile + command button logic (ported from K1L0ProfileMode) ─

    Button authButton;
    Button prodButton;

    void LoadProfile()
    {
        string signal = FirebaseAuthManager.Instance != null && FirebaseAuthManager.Instance.isAuthenticated ? "AUTHENTICATED" : "ANON";
        string deviceId = DeviceIDManager.Instance != null ? DeviceIDManager.Instance.GetCurrentUserId() : "offline";
        RenderHeader(signal, deviceId);
        UpdateAuthButton();
    }

    void RenderHeader(string signal, string deviceId)
    {
        if (headerLabel == null) return;
        var sb = new StringBuilder(160);
        sb.AppendLine($"> SIGNAL   {signal}");
        string dev = string.IsNullOrEmpty(deviceId) ? "---" : (deviceId.Length <= 18 ? deviceId : deviceId.Substring(0, 18) + "...");
        sb.Append($"> DEVICE   {dev}");
        headerLabel.text = sb.ToString();
    }

    string ExtractStringField(string json, string fieldName)
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

    void OnEditClick()
    {
        var modal = Object.FindFirstObjectByType<ProfileEditorModal>();
        if (modal != null) modal.OpenModal();
    }

    void OnAuthClick()
    {
        var auth = FirebaseAuthManager.Instance;
        if (auth == null) return;
        if (auth.isAuthenticated)
        {
            auth.SignOut();
            LoadProfile();
            UpdateAuthButton();
        }
        else
        {
            var loginUI = Object.FindFirstObjectByType<LoginUI>();
            if (loginUI != null) loginUI.ShowLogin();
        }
    }

    void UpdateAuthButton()
    {
        if (authButton == null) return;
        bool loggedIn = FirebaseAuthManager.Instance != null && FirebaseAuthManager.Instance.isAuthenticated;
        authButton.text = loggedIn ? "LOGOUT" : "LOGIN";
        authButton.style.color = Green;
    }

    void OnScreenshotClick()
    {
        var ss = K1L0Screenshot.Instance;
        if (ss != null) ss.Capture();
    }

    void OnProdApiToggle()
    {
        APIManager.SetProductionOverride(!APIManager.IsProductionOverride());
        UpdateProdApiToggle();
    }

    void UpdateProdApiToggle()
    {
        if (prodButton == null) return;
        bool on = APIManager.IsProductionOverride();
        prodButton.text = on ? "PRODUCTION API ON" : "PRODUCTION API OFF";
        prodButton.style.color = Green;
    }

    void OnDestroy()
    {
        if (panelSettings != null) Destroy(panelSettings);
    }
}
