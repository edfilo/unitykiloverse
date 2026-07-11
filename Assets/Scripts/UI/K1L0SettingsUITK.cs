using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
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

    private string selectedSection = "PORTALS";
    private List<VisualElement> sectionContainers = new List<VisualElement>();
    private List<Button> tabButtons = new List<Button>();

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

        // ── Segment Controller (Tabs) ──
        tabButtons.Clear();
        var tabsContainer = new VisualElement();
        tabsContainer.style.flexDirection = FlexDirection.Column;
        tabsContainer.style.marginBottom = 8;
        panel.Add(tabsContainer);

        var tabRow1 = Row(24);
        tabRow1.style.marginBottom = 4;
        tabsContainer.Add(tabRow1);

        var tabRow2 = Row(24);
        tabsContainer.Add(tabRow2);

        var sections = new string[] { "PORTALS", "CAMERA", "LIGHTING", "BUILDINGS", "IDENTITY", "WEATHER", "EFFECTS" };
        foreach (var sec in sections)
        {
            var btn = new Button() { text = sec };
            StyleFlatButton(btn);
            btn.style.flexGrow = 1;
            btn.style.marginLeft = 2;
            btn.style.marginRight = 2;
            btn.style.fontSize = 9.5f;
            btn.clickable.clicked += () => SelectSection(sec);

            if (sec == "PORTALS" || sec == "CAMERA" || sec == "LIGHTING" || sec == "BUILDINGS")
                tabRow1.Add(btn);
            else
                tabRow2.Add(btn);

            tabButtons.Add(btn);
        }

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
        SelectSection(selectedSection);

        // ── Command buttons ──
        var footer = new VisualElement();
        footer.style.marginTop = 8;
        footer.style.flexDirection = FlexDirection.Column;
        panel.Add(footer);

        var rowA = Row(0); rowA.style.marginBottom = 6;
        rowA.Add(CommandButton("EDIT PROFILE", OnEditClick, true));
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
        sectionContainers.Clear();

        // ── 1. PORTALS ──
        var portals = CreateSectionContainer(body, "PORTALS");

        perfLabel = MonoLabel("> loading stats...");
        perfLabel.style.fontSize = 10;
        perfLabel.style.color = GreenFaint;
        perfLabel.style.whiteSpace = WhiteSpace.Normal;
        perfLabel.style.marginBottom = 6;
        portals.Add(perfLabel);

        beamLabel = MonoLabel("RING DEBUG\nPORTAL AUDIT: waiting...");
        beamLabel.style.fontSize = 10;
        beamLabel.style.color = GreenFaint;
        beamLabel.style.whiteSpace = WhiteSpace.Normal;
        beamLabel.style.marginBottom = 6;
        portals.Add(beamLabel);

        {
            var director = SignalDirectorV2.Instance ?? FindFirstObjectByType<SignalDirectorV2>();
            float minSteps = director != null ? director.ambientMinStepsToSpawn : PlayerPrefs.GetFloat("k1lo_ambientMinStepsToSpawn", 110f);
            float graceSteps = director != null ? director.momentumGraceSteps : PlayerPrefs.GetInt("k1lo_momentumGraceSteps", 50);
            float ttlMinutes = director != null ? director.ambientBeamTtlMinutes : PlayerPrefs.GetFloat("k1lo_ambientBeamTtlMinutes", 30f);
            float collectRadius = director != null ? director.ambientCollectRadiusMeters : PlayerPrefs.GetFloat("k1lo_ambientCollectRadiusMeters", 16f);
            Header(portals, "PORTAL SPAWN");
            Sliders(portals, "MIN STEPS", "ambientMinStepsToSpawn", 0f, 1000f, minSteps, v =>
            {
                var d = SignalDirectorV2.Instance ?? FindFirstObjectByType<SignalDirectorV2>();
                if (d != null) d.ambientMinStepsToSpawn = Mathf.RoundToInt(v);
            }, true);
            Sliders(portals, "RESET GRACE (steps)", "momentumGraceSteps", 10f, 500f, graceSteps, v =>
            {
                var d = SignalDirectorV2.Instance ?? FindFirstObjectByType<SignalDirectorV2>();
                int n = Mathf.Clamp(Mathf.RoundToInt(v), 10, 500);
                if (d != null) d.momentumGraceSteps = n;
                PlayerPrefs.SetInt("k1lo_momentumGraceSteps", n);
                UserPresenceManager.NotifyMomentumGraceStepsChanged(n);
            }, true);
            Sliders(portals, "EXPIRE MIN", "ambientBeamTtlMinutes", 1f, 240f, ttlMinutes, v =>
            {
                var d = SignalDirectorV2.Instance ?? FindFirstObjectByType<SignalDirectorV2>();
                if (d != null) d.ambientBeamTtlMinutes = Mathf.Clamp(v, 1f, 240f);
            }, true);
            Sliders(portals, "COLLECT RADIUS", "ambientCollectRadiusMeters", 1f, 100f, collectRadius, v =>
            {
                var d = SignalDirectorV2.Instance ?? FindFirstObjectByType<SignalDirectorV2>();
                if (d != null) d.ambientCollectRadiusMeters = Mathf.Clamp(v, 1f, 100f);
            }, true);
        }
        Toggle(portals, "PORTAL DIST", "showBeamDistanceLabels", SignalBeamBridge.ShowDistanceLabels, SignalBeamBridge.SetDistanceLabelsVisible);
        Toggle(portals, "STORIES", "showStoryStrip", StoriesStripVisibility.ShowStoryStrip, StoriesStripVisibility.SetStoryStripVisible);

        // ── 2. CAMERA ──
        var camera = CreateSectionContainer(body, "CAMERA");
        var cam = KiloWorld.Rendering.Systems.RenderManager.Instance?.profile?.camera;
        if (cam != null)
        {
            Header(camera, "CAMERA / GOD VIEW");
            Sliders(camera, "HEIGHT", "godPositionY", 10, 500, cam.godPositionY, v => { cam.godPositionY = v; ApplyCameraLiveUpdate(); });
            Sliders(camera, "DISTANCE", "godPositionZ", 10, 500, cam.godPositionZ, v => { cam.godPositionZ = v; ApplyCameraLiveUpdate(); });
            Sliders(camera, "PITCH", "godRotationX", -90, 90, cam.godRotationX, v => { cam.godRotationX = v; ApplyCameraLiveUpdate(); });
            Sliders(camera, "FAR CLIP", "farClipPlane", 100, 5000, cam.farClipPlane, v => { cam.farClipPlane = v; ApplyCameraLiveUpdate(); });
        }

        // ── 3. LIGHTING ──
        var lightingSec = CreateSectionContainer(body, "LIGHTING");
        var lighting = KiloWorld.Rendering.Systems.RenderManager.Instance?.profile?.lighting;
        if (lighting != null)
        {
            Header(lightingSec, "LIGHTING");
            Toggle(lightingSec, "MOONLIGHT", "moonlightEnabled", lighting.moonlightEnabled, v => { lighting.moonlightEnabled = v; KiloWorld.Rendering.Systems.RenderManager.Instance?.Apply(); });
            Sliders(lightingSec, "MOONLIGHT", "moonlightIntensity", 0f, 8f, lighting.moonlightIntensity, v => { lighting.moonlightIntensity = Mathf.Clamp(v, 0f, 8f); KiloWorld.Rendering.Systems.RenderManager.Instance?.Apply(); });
            Toggle(lightingSec, "AMBIENT", "ambientEnabled", lighting.ambientEnabled, v => { lighting.ambientEnabled = v; KiloWorld.Rendering.Systems.RenderManager.Instance?.Apply(); });
            Sliders(lightingSec, "AMBIENT", "ambientIntensity", 0f, 8f, lighting.ambientIntensity, v => { lighting.ambientIntensity = Mathf.Clamp(v, 0f, 8f); KiloWorld.Rendering.Systems.RenderManager.Instance?.Apply(); });
            Toggle(lightingSec, "SHADOWS", "enableShadows", lighting.enableShadows, v => { lighting.enableShadows = v; KiloWorld.Rendering.Systems.RenderManager.Instance?.Apply(); });
            Sliders(lightingSec, "SHADOW STR", "shadowStrength", 0f, 1f, lighting.shadowStrength, v => { lighting.shadowStrength = Mathf.Clamp01(v); KiloWorld.Rendering.Systems.RenderManager.Instance?.Apply(); });
            Sliders(lightingSec, "SHADOW DIST", "shadowDistance", 0f, 500f, lighting.shadowDistance, v => { lighting.shadowDistance = Mathf.Clamp(v, 0f, 500f); KiloWorld.Rendering.Systems.RenderManager.Instance?.Apply(); });
            Toggle(lightingSec, "SPOTLIGHT", "spotlightEnabled", lighting.spotlightEnabled, v => { lighting.spotlightEnabled = v; KiloWorld.Rendering.Systems.RenderManager.Instance?.Apply(); });
            Sliders(lightingSec, "SPOTLIGHT", "spotlightIntensity", 0f, 12f, lighting.spotlightIntensity, v => { lighting.spotlightIntensity = Mathf.Clamp(v, 0f, 12f); KiloWorld.Rendering.Systems.RenderManager.Instance?.Apply(); });
            Toggle(lightingSec, "REFLECTIONS", "reflectionsEnabled", lighting.reflectionsEnabled, v => { lighting.reflectionsEnabled = v; KiloWorld.Rendering.Systems.RenderManager.Instance?.Apply(); });
            Sliders(lightingSec, "REFLECTION", "reflectionIntensity", 0f, 2f, lighting.reflectionIntensity, v => { lighting.reflectionIntensity = Mathf.Clamp(v, 0f, 2f); KiloWorld.Rendering.Systems.RenderManager.Instance?.Apply(); });
        }

        // ── 4. BUILDINGS ──
        var buildings = CreateSectionContainer(body, "BUILDINGS");
        var bld = KiloWorld.Rendering.Systems.RenderManager.Instance?.profile?.buildings;
        if (bld != null)
        {
            Header(buildings, "BUILDINGS");
            Sliders(buildings, "WALL SMOOTH", "zossWallSmoothness", 0f, 1f, bld.zossWallSmoothness, v => { bld.zossWallSmoothness = Mathf.Clamp01(v); KiloWorld.Rendering.Systems.RenderManager.Instance?.Apply(); });
            Sliders(buildings, "WALL METAL", "zossWallMetallic", 0f, 1f, bld.zossWallMetallic, v => { bld.zossWallMetallic = Mathf.Clamp01(v); KiloWorld.Rendering.Systems.RenderManager.Instance?.Apply(); });
            Header(buildings, "WINDOW GLOW");
            Sliders(buildings, "INTENSITY", "zossEmissiveIntensity", 0f, 50f, bld.zossEmissiveIntensity, v => { bld.zossEmissiveIntensity = Mathf.Clamp(v, 0f, 50f); KiloWorld.Rendering.Systems.RenderManager.Instance?.Apply(); });
            Sliders(buildings, "SMOOTHNESS", "zossEmissiveSmoothness", 0f, 1f, bld.zossEmissiveSmoothness, v => { bld.zossEmissiveSmoothness = Mathf.Clamp01(v); KiloWorld.Rendering.Systems.RenderManager.Instance?.Apply(); });
            Sliders(buildings, "METALLIC", "zossEmissiveMetallic", 0f, 1f, bld.zossEmissiveMetallic, v => { bld.zossEmissiveMetallic = Mathf.Clamp01(v); KiloWorld.Rendering.Systems.RenderManager.Instance?.Apply(); });
        }

        // ── 5. IDENTITY ──
        var identity = CreateSectionContainer(body, "IDENTITY");
        BuildFaceSection(identity);
        BuildTransmissionsSection(identity);

        // ── 6. WEATHER ──
        var weather = CreateSectionContainer(body, "WEATHER");
        Sliders(weather, "MAP BRIGHT", "panelMapBrightness", 0f, 1f, K1L0HUD.PanelMapBrightness, K1L0HUD.SetPanelMapBrightness);

        var sky = KiloWorld.Rendering.Systems.RenderManager.Instance?.profile?.sky;
        if (sky != null)
        {
            Header(weather, "WEATHER / SKY");
            float manualHour = PlayerPrefs.GetFloat("k1lo_manualHour", 13.25f);
            KiloWorld.Rendering.Systems.RenderManager.ManualHour = manualHour;
            Sliders(weather, "TIME OF DAY", "manualHour", 0f, 24f, manualHour,
                v =>
                {
                    KiloWorld.Rendering.Systems.RenderManager.ManualHour = v;
                    KiloWorld.Rendering.Systems.RenderManager.NotifyManualSkyChanged();
                });
            WeatherRow(weather);

            float currentSkyFps = DynamicSkyVideoController.SkyTargetFps > 0.1f
                ? DynamicSkyVideoController.SkyTargetFps
                : PlayerPrefs.GetFloat("k1lo_skyTargetFps", 30f);
            Sliders(weather, "SKY SPEED (FPS)", "skyTargetFps", 1f, 60f, currentSkyFps,
                v => DynamicSkyVideoController.SetSkyFps(v));
        }

        // ── 7. EFFECTS ──
        var effects = CreateSectionContainer(body, "EFFECTS");
        var pfx = KiloWorld.Rendering.Systems.RenderManager.Instance?.profile?.postFX;
        if (pfx != null)
        {
            Header(effects, "COLOR GRADING");
            Sliders(effects, "BRIGHTNESS", "exposureFixedValue", -1f, 2f, pfx.exposureFixedValue, v => pfx.exposureFixedValue = v, true);
            Sliders(effects, "SATURATION", "saturation", -100, 100, pfx.saturation, v => pfx.saturation = v, true);
            Sliders(effects, "CONTRAST", "contrast", -100, 100, pfx.contrast, v => pfx.contrast = v, true);
            Sliders(effects, "HUE SHIFT", "hueShift", -100, 100, pfx.hueShift, v => pfx.hueShift = v, true);
            Sliders(effects, "TEMPERATURE", "temperature", -100, 100, pfx.temperature, v => pfx.temperature = v, true);
            Sliders(effects, "TINT", "tint", -100, 100, pfx.tint, v => pfx.tint = v, true);

            Header(effects, "BLOOM");
            Toggle(effects, "ENABLED", "bloomEnabled", pfx.bloomEnabled, v => pfx.bloomEnabled = v);
            Sliders(effects, "INTENSITY", "bloomIntensity", 0, 10, pfx.bloomIntensity, v => pfx.bloomIntensity = v);
            Sliders(effects, "THRESHOLD", "bloomThreshold", 0, 5, pfx.bloomThreshold, v => pfx.bloomThreshold = v);
            Sliders(effects, "SCATTER", "bloomScatter", 0, 1, pfx.bloomScatter, v => pfx.bloomScatter = v);

            Header(effects, "VIGNETTE");
            Toggle(effects, "ENABLED", "vignetteEnabled", pfx.vignetteEnabled, v => pfx.vignetteEnabled = v);
            Sliders(effects, "INTENSITY", "vignetteIntensity", 0, 1, pfx.vignetteIntensity, v => pfx.vignetteIntensity = v);
            Sliders(effects, "SMOOTHNESS", "vignetteSmoothness", 0.01f, 1, pfx.vignetteSmoothness, v => pfx.vignetteSmoothness = v);

            Header(effects, "CHROMATIC ABERRATION");
            Toggle(effects, "ENABLED", "chromaticEnabled", pfx.chromaticAberrationEnabled, v => pfx.chromaticAberrationEnabled = v);
            Sliders(effects, "INTENSITY", "chromaticIntensity", 0, 1, pfx.chromaticAberrationIntensity, v => pfx.chromaticAberrationIntensity = v);

            Header(effects, "LENS DISTORTION");
            Toggle(effects, "ENABLED", "lensDistEnabled", pfx.lensDistortionEnabled, v => pfx.lensDistortionEnabled = v);
            Sliders(effects, "INTENSITY", "lensDistIntensity", -1, 1, pfx.lensDistortionIntensity, v => pfx.lensDistortionIntensity = v);

            Header(effects, "DEPTH OF FIELD");
            Toggle(effects, "ENABLED", "dofEnabled", pfx.depthOfFieldEnabled, v => pfx.depthOfFieldEnabled = v);
            Sliders(effects, "FOCUS DIST", "focusDistance", 0.1f, 300, pfx.focusDistance, v => pfx.focusDistance = v);
            Sliders(effects, "APERTURE", "aperture", 0.05f, 32, pfx.aperture, v => pfx.aperture = v);
            Sliders(effects, "FOCAL LEN", "focalLength", 1, 300, pfx.focalLength, v => pfx.focalLength = v);

            Header(effects, "MOTION BLUR");
            Toggle(effects, "ENABLED", "motionBlurEnabled", pfx.motionBlurEnabled, v => pfx.motionBlurEnabled = v);
            Sliders(effects, "INTENSITY", "motionBlurIntensity", 0, 1, pfx.motionBlurIntensity, v => pfx.motionBlurIntensity = v);

            Header(effects, "FILM GRAIN");
            Toggle(effects, "ENABLED", "filmGrainEnabled", pfx.filmGrainEnabled, v => pfx.filmGrainEnabled = v);
            Sliders(effects, "INTENSITY", "filmGrainIntensity", 0, 1, pfx.filmGrainIntensity, v => pfx.filmGrainIntensity = v);
        }
    }
    private void SelectSection(string sectionName)
    {
        selectedSection = sectionName;
        for (int i = 0; i < tabButtons.Count; i++)
        {
            var btn = tabButtons[i];
            bool active = btn.text == sectionName;
            btn.style.backgroundColor = active ? Green : Color.black;
            btn.style.color = active ? Color.black : Green;
        }
        foreach (var entry in sectionContainers)
        {
            bool show = entry.name == sectionName;
            entry.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    VisualElement CreateSectionContainer(VisualElement parent, string name)
    {
        var container = new VisualElement();
        container.name = name;
        container.style.flexDirection = FlexDirection.Column;
        container.style.display = name == selectedSection ? DisplayStyle.Flex : DisplayStyle.None;
        parent.Add(container);
        sectionContainers.Add(container);
        return container;
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
            KiloWorld.Rendering.Systems.RenderManager.ManualWeatherOverrideEnabled = true;
            PlayerPrefs.SetFloat("k1lo_manualWeather", idx);
            PlayerPrefs.SetString("k1lo_manualWeatherGlyph", WeatherGlyphs[idx]);
            PlayerPrefs.SetInt("k1lo_manualWeatherOverrideEnabled", 1);
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
        string signal = K1L0NativeSessionBridge.IsAuthenticated ? "AUTHENTICATED" : "ANON";
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
        Debug.Log("[K1L0SettingsUITK] Unity auth UI is disabled; native overlay owns auth.");
    }

    void UpdateAuthButton()
    {
        if (authButton == null) return;
        bool loggedIn = K1L0NativeSessionBridge.IsAuthenticated;
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

    // ── Transmissions section ───────────────────────────────────────────────

    VisualElement txGrid;
    Label txStatusLabel;

    void BuildTransmissionsSection(VisualElement parent)
    {
        Header(parent, "MY TRANSMISSIONS");

        txStatusLabel = new Label("loading...");
        txStatusLabel.style.fontSize = 9;
        txStatusLabel.style.color = GreenDim;
        txStatusLabel.style.marginBottom = 4;
        parent.Add(txStatusLabel);

        txGrid = new VisualElement();
        txGrid.style.flexDirection = FlexDirection.Row;
        txGrid.style.flexWrap = Wrap.Wrap;
        txGrid.style.marginBottom = 8;
        parent.Add(txGrid);

        StartCoroutine(LoadTransmissions());
    }

    IEnumerator LoadTransmissions()
    {
        string uid = CurrentUserId();
        if (string.IsNullOrEmpty(uid) || APIManager.Instance == null)
        {
            if (txStatusLabel != null) txStatusLabel.text = "(not signed in)";
            yield break;
        }

        string resp = null;
        yield return APIManager.Instance.Get(
            $"/api/k1l0/v2/my-transmissions?userId={UnityWebRequest.EscapeURL(uid)}",
            (ok, r) => { if (ok) resp = r; }
        );

        if (string.IsNullOrEmpty(resp))
        {
            if (txStatusLabel != null) txStatusLabel.text = "(failed to load)";
            yield break;
        }

        var items = ParseTransmissionList(resp);
        if (txStatusLabel != null) txStatusLabel.text = items.Count == 0 ? "(no transmissions yet)" : "";

        foreach (var item in items)
            yield return AddTransmissionThumb(item);
    }

    IEnumerator AddTransmissionThumb(TxItem item)
    {
        string thumbUrl   = item.thumbUrl;
        string finalUrl   = item.finalUrl;
        string audioUrl   = item.audioUrl;
        string replyText  = item.selectedResponse;
        string parentUrl  = item.parentFinalUrl;
        bool   isReply    = !string.IsNullOrEmpty(replyText) && !string.IsNullOrEmpty(parentUrl);

        var card = new Button(() =>
        {
            if (string.IsNullOrEmpty(finalUrl)) return;
            var frame = TransmissionFrame.Instance;
            if (frame == null) { Application.OpenURL(finalUrl); return; }
            if (isReply)
                frame.ReplaySequence(parentUrl, finalUrl, audioUrl, thumbUrl);
            else
                frame.ReplayStoredTransmission(finalUrl, audioUrl, thumbUrl);
        });
        card.style.width = 76;
        card.style.height = 76;
        card.style.marginLeft = 3;
        card.style.marginRight = 3;
        card.style.marginTop = 3;
        card.style.marginBottom = 3;
        card.style.paddingLeft = 0;
        card.style.paddingRight = 0;
        card.style.paddingTop = 0;
        card.style.paddingBottom = 0;
        card.style.backgroundColor = new Color(0.04f, 0.08f, 0.04f, 1f);
        SetBorder(card, BorderCol, 1);
        SetBorderRadius(card, 4);
        txGrid?.Add(card);

        if (!string.IsNullOrEmpty(thumbUrl))
        {
            using (var req = UnityWebRequestTexture.GetTexture(thumbUrl))
            {
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                {
                    var img = new Image();
                    img.image = ((DownloadHandlerTexture)req.downloadHandler).texture;
                    img.scaleMode = ScaleMode.ScaleAndCrop;
                    img.style.position = Position.Absolute;
                    img.style.left = 0; img.style.right = 0;
                    img.style.top = 0; img.style.bottom = 0;
                    img.pickingMode = PickingMode.Ignore;
                    card.Add(img);
                }
            }
        }

        // Reply badge: shown at the bottom of the card, text = what was chosen
        if (isReply)
        {
            var badge = new Label(replyText);
            badge.style.position = Position.Absolute;
            badge.style.left = 0; badge.style.right = 0; badge.style.bottom = 0;
            badge.style.fontSize = 8;
            badge.style.color = Color.white;
            badge.style.backgroundColor = new Color(0f, 0f, 0f, 0.72f);
            badge.style.paddingLeft = 3; badge.style.paddingRight = 3;
            badge.style.paddingTop = 2; badge.style.paddingBottom = 2;
            badge.style.unityTextAlign = TextAnchor.MiddleCenter;
            badge.style.whiteSpace = WhiteSpace.Normal;
            badge.style.overflow = Overflow.Hidden;
            badge.pickingMode = PickingMode.Ignore;
            card.Add(badge);
        }
    }

    struct TxItem
    {
        public string thumbUrl;
        public string finalUrl;
        public string audioUrl;
        public string selectedResponse;  // non-empty = this is a reply; text is what was chosen
        public string parentFinalUrl;    // play this first before finalUrl
    }

    List<TxItem> ParseTransmissionList(string json)
    {
        var result = new List<TxItem>();
        int arrayStart = json.IndexOf('[');
        int arrayEnd = json.LastIndexOf(']');
        if (arrayStart < 0 || arrayEnd < 0) return result;
        string body = json.Substring(arrayStart, arrayEnd - arrayStart + 1);
        int pos = 0;
        while (pos < body.Length)
        {
            int objStart = body.IndexOf('{', pos);
            if (objStart < 0) break;
            int depth = 0;
            int objEnd = objStart;
            for (int i = objStart; i < body.Length; i++)
            {
                if (body[i] == '{') depth++;
                else if (body[i] == '}') { depth--; if (depth == 0) { objEnd = i; break; } }
            }
            string obj = body.Substring(objStart, objEnd - objStart + 1);
            var item = new TxItem
            {
                thumbUrl         = ExtractField(obj, "thumbUrl"),
                finalUrl         = ExtractField(obj, "finalUrl"),
                audioUrl         = ExtractField(obj, "audioUrl"),
                selectedResponse = ExtractField(obj, "selectedResponse"),
                parentFinalUrl   = ExtractField(obj, "parentFinalUrl"),
            };
            if (!string.IsNullOrEmpty(item.finalUrl))
                result.Add(item);
            pos = objEnd + 1;
        }
        return result;
    }

    // ── Face section ────────────────────────────────────────────────────────
    // The user's identity face. Persists via POST /api/user/face — the
    // transmit-v2 engine reads it back as the identity ref on every send, so
    // changing it here propagates instantly to the next transmission.
    Image faceImage;
    Label faceUrlLabel;
    Button faceRandomBtn;
    Button faceClearBtn;
    string currentFaceUrl;

    void BuildFaceSection(VisualElement parent)
    {
        Header(parent, "FACE");

        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.marginBottom = 8;

        faceImage = new Image();
        faceImage.style.width = 72;
        faceImage.style.height = 72;
        faceImage.style.borderTopLeftRadius = 36;
        faceImage.style.borderTopRightRadius = 36;
        faceImage.style.borderBottomLeftRadius = 36;
        faceImage.style.borderBottomRightRadius = 36;
        faceImage.style.backgroundColor = new Color(0.05f, 0.10f, 0.05f, 1f);
        faceImage.scaleMode = ScaleMode.ScaleAndCrop;
        row.Add(faceImage);

        var col = new VisualElement();
        col.style.flexGrow = 1;
        col.style.flexDirection = FlexDirection.Column;
        col.style.marginLeft = 10;

        faceUrlLabel = new Label("(no face set)");
        faceUrlLabel.style.fontSize = 9;
        faceUrlLabel.style.color = GreenDim;
        faceUrlLabel.style.whiteSpace = WhiteSpace.NoWrap;
        faceUrlLabel.style.overflow = Overflow.Hidden;
        faceUrlLabel.style.textOverflow = TextOverflow.Ellipsis;
        col.Add(faceUrlLabel);

        var btnRow = new VisualElement();
        btnRow.style.flexDirection = FlexDirection.Row;
        btnRow.style.marginTop = 6;

        faceRandomBtn = new Button(OnRandomFaceClicked) { text = "🎲 RANDOMIZE" };
        StyleFlatButton(faceRandomBtn);
        faceRandomBtn.style.flexGrow = 1;
        faceRandomBtn.style.height = 26;
        faceRandomBtn.style.marginRight = 4;
        btnRow.Add(faceRandomBtn);

        faceClearBtn = new Button(OnClearFaceClicked) { text = "CLEAR" };
        StyleFlatButton(faceClearBtn);
        faceClearBtn.style.width = 70;
        faceClearBtn.style.height = 26;
        btnRow.Add(faceClearBtn);

        col.Add(btnRow);
        row.Add(col);
        parent.Add(row);

        StartCoroutine(LoadCurrentFace());
    }

    string CurrentUserId() => DeviceIDManager.Instance != null ? DeviceIDManager.Instance.GetCurrentUserId() : "";

    IEnumerator LoadCurrentFace()
    {
        string uid = CurrentUserId();
        if (string.IsNullOrEmpty(uid) || APIManager.Instance == null) yield break;
        yield return APIManager.Instance.Get($"/api/user/face?userId={UnityWebRequest.EscapeURL(uid)}", (ok, resp) =>
        {
            if (!ok) return;
            string url = ExtractField(resp, "faceUrl");
            ApplyFace(url);
        });
    }

    void OnRandomFaceClicked()
    {
        if (faceRandomBtn != null) faceRandomBtn.SetEnabled(false);
        StartCoroutine(RandomizeFace());
    }

    IEnumerator RandomizeFace()
    {
        string uid = CurrentUserId();
        if (string.IsNullOrEmpty(uid) || APIManager.Instance == null)
        {
            if (faceRandomBtn != null) faceRandomBtn.SetEnabled(true);
            yield break;
        }
        // 1) Pull a fresh AI face URL.
        string newUrl = null;
        yield return APIManager.Instance.Get("/api/admin/random-selfie", (ok, resp) =>
        {
            if (ok) newUrl = ExtractField(resp, "url");
        });
        if (string.IsNullOrEmpty(newUrl))
        {
            if (faceRandomBtn != null) faceRandomBtn.SetEnabled(true);
            yield break;
        }
        // 2) Persist it on the user record.
        string payload = "{\"userId\":\"" + EscapeJson(uid) + "\",\"faceUrl\":\"" + EscapeJson(newUrl) + "\"}";
        yield return APIManager.Instance.Post("/api/user/face", payload, (ok, _) =>
        {
            if (ok) ApplyFace(newUrl);
        });
        if (faceRandomBtn != null) faceRandomBtn.SetEnabled(true);
    }

    void OnClearFaceClicked()
    {
        if (faceClearBtn != null) faceClearBtn.SetEnabled(false);
        StartCoroutine(ClearFace());
    }

    IEnumerator ClearFace()
    {
        string uid = CurrentUserId();
        if (string.IsNullOrEmpty(uid) || APIManager.Instance == null)
        {
            if (faceClearBtn != null) faceClearBtn.SetEnabled(true);
            yield break;
        }
        string payload = "{\"userId\":\"" + EscapeJson(uid) + "\",\"faceUrl\":\"\"}";
        yield return APIManager.Instance.Post("/api/user/face", payload, (ok, _) =>
        {
            if (ok) ApplyFace(null);
        });
        if (faceClearBtn != null) faceClearBtn.SetEnabled(true);
    }

    void ApplyFace(string url)
    {
        currentFaceUrl = url;
        if (faceUrlLabel != null)
            faceUrlLabel.text = string.IsNullOrEmpty(url) ? "(no face set)" : url;
        if (faceImage != null && !string.IsNullOrEmpty(url))
            StartCoroutine(DownloadFaceTexture(url));
        else if (faceImage != null)
            faceImage.image = null;
    }

    IEnumerator DownloadFaceTexture(string url)
    {
        using (var req = UnityWebRequestTexture.GetTexture(url))
        {
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success && faceImage != null)
                faceImage.image = ((DownloadHandlerTexture)req.downloadHandler).texture;
        }
    }

    // Minimal JSON field extractor (avoids pulling a full parser into this file).
    static string ExtractField(string json, string fieldName)
    {
        if (string.IsNullOrEmpty(json)) return null;
        string key = "\"" + fieldName + "\"";
        int idx = json.IndexOf(key);
        if (idx < 0) return null;
        int colon = json.IndexOf(':', idx);
        if (colon < 0) return null;
        int qStart = json.IndexOf('"', colon + 1);
        if (qStart < 0) return null;
        int qEnd = json.IndexOf('"', qStart + 1);
        if (qEnd < 0) return null;
        return json.Substring(qStart + 1, qEnd - qStart - 1);
    }

    static string EscapeJson(string s) => string.IsNullOrEmpty(s) ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
