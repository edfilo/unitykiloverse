using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Kiloverse intro screen: a 300x300 collectible viewport showing a glowing,
// rotatable particle sphere whose surface is covered in a repeating sigil,
// with a Claude / Codex / ZPT tab menu beneath it.
public class IntroCollectibleScreen : MonoBehaviour
{
    [Header("Capture")]
    public bool autoCapture = true;
    public string capturePath = "/tmp/k1l0_intro.png";
    public float captureDelay = 2.0f;

    [Header("Look")]
    public Color pink = new Color(1.0f, 0.28f, 0.78f);
    public float sphereRadius = 1.0f;

    // runtime
    Transform collectible;
    Camera sphereCam;
    RenderTexture rt;
    GameObject claudePanel, codexPanel, zptPanel;
    Button claudeTab, codexTab, zptTab;
    Font font;

    void Start()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        BuildLighting();
        BuildCollectible();
        BuildSphereCamera();
        BuildUI();
        SelectTab(0);
        if (autoCapture) StartCoroutine(CaptureRoutine());
    }

    void Update()
    {
        if (collectible != null)
            collectible.Rotate(new Vector3(18f, 26f, 0f) * Time.deltaTime, Space.Self);
    }

    // ---------- procedural textures ----------

    // grayscale sigil mask: a ringed 6-spoke sigil that tiles
    Texture2D MakeSigilMask(int size)
    {
        var t = new Texture2D(size, size, TextureFormat.RGBA32, true);
        t.wrapMode = TextureWrapMode.Repeat;
        t.filterMode = FilterMode.Trilinear;
        Vector2 c = new Vector2(size * 0.5f, size * 0.5f);
        float ring = size * 0.37f;
        float ringW = size * 0.022f;
        float spokeIn = size * 0.07f;
        float spokeOut = size * 0.30f;
        float spokeW = size * 0.018f;
        var cols = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                float d = Mathf.Abs(Vector2.Distance(p, c) - ring) - ringW; // ring
                for (int k = 0; k < 6; k++)
                {
                    float a = k * Mathf.PI / 3f;
                    Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    Vector2 s0 = c + dir * spokeIn;
                    Vector2 s1 = c + dir * spokeOut;
                    float ds = DistSeg(p, s0, s1) - spokeW;
                    d = Mathf.Min(d, ds);
                }
                float dot = Vector2.Distance(p, c) - size * 0.045f; // center node
                d = Mathf.Min(d, dot);
                float aMask = Mathf.Clamp01(1f - Mathf.SmoothStep(0f, 2.0f, d));
                cols[y * size + x] = new Color(aMask, aMask, aMask, aMask);
            }
        }
        t.SetPixels(cols);
        t.Apply(true);
        return t;
    }

    // soft round particle dot
    Texture2D MakeDot(int size)
    {
        var t = new Texture2D(size, size, TextureFormat.RGBA32, true);
        t.wrapMode = TextureWrapMode.Clamp;
        Vector2 c = new Vector2(size * 0.5f, size * 0.5f);
        var cols = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c) / (size * 0.5f);
                float a = Mathf.Clamp01(1f - d);
                a = a * a * a;
                cols[y * size + x] = new Color(1, 1, 1, a);
            }
        t.SetPixels(cols);
        t.Apply(true);
        return t;
    }

    static float DistSeg(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Vector2.Dot(ab, ab));
        return Vector2.Distance(p, a + ab * t);
    }

    Texture2D Tint(Texture2D mask, Color bg, Color fg)
    {
        int w = mask.width, h = mask.height;
        var t = new Texture2D(w, h, TextureFormat.RGBA32, true);
        t.wrapMode = TextureWrapMode.Repeat;
        t.filterMode = FilterMode.Trilinear;
        var src = mask.GetPixels();
        var dst = new Color[src.Length];
        for (int i = 0; i < src.Length; i++)
        {
            float m = src[i].a;
            dst[i] = Color.Lerp(bg, fg, m);
            dst[i].a = 1f;
        }
        t.SetPixels(dst);
        t.Apply(true);
        return t;
    }

    // ---------- scene build ----------

    void BuildLighting()
    {
        var sun = new GameObject("KeyLight");
        var l = sun.AddComponent<Light>();
        l.type = LightType.Directional;
        l.color = new Color(1f, 0.85f, 0.95f);
        l.intensity = 1.4f;
        sun.transform.rotation = Quaternion.Euler(40f, -35f, 0f);

        var rim = new GameObject("RimLight");
        var rl = rim.AddComponent<Light>();
        rl.type = LightType.Point;
        rl.color = pink;
        rl.intensity = 6f;
        rl.range = 12f;
        rim.transform.position = new Vector3(-2.2f, 0.5f, -2.2f);

        var fill = new GameObject("FillLight");
        var fl = fill.AddComponent<Light>();
        fl.type = LightType.Point;
        fl.color = new Color(0.5f, 0.4f, 1f);
        fl.intensity = 3f;
        fl.range = 12f;
        fill.transform.position = new Vector3(2.5f, -1.0f, -1.5f);

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.06f, 0.03f, 0.10f);
    }

    void BuildCollectible()
    {
        var mask = MakeSigilMask(256);
        var albedo = Tint(mask, new Color(0.06f, 0.015f, 0.10f), new Color(0.55f, 0.18f, 0.5f));
        var emis = Tint(mask, Color.black, pink);
        var dot = MakeDot(64);

        var root = new GameObject("Collectible");
        root.transform.position = Vector3.zero;
        collectible = root.transform;

        // core lit sphere with tiled sigil + emission
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "SigilSphere";
        sphere.transform.SetParent(root.transform, false);
        sphere.transform.localScale = Vector3.one * (sphereRadius * 2f);
        Destroy(sphere.GetComponent<Collider>());
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetTexture("_BaseMap", albedo);
        mat.SetTextureScale("_BaseMap", new Vector2(6f, 4f));
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Metallic", 0.65f);
        mat.SetFloat("_Smoothness", 0.85f);
        mat.EnableKeyword("_EMISSION");
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        mat.SetTexture("_EmissionMap", emis);
        mat.SetTextureScale("_EmissionMap", new Vector2(6f, 4f));
        mat.SetColor("_EmissionColor", pink * 2.4f);
        sphere.GetComponent<MeshRenderer>().material = mat;

        // shell of glowing particles
        var psGO = new GameObject("ParticleShell");
        psGO.transform.SetParent(root.transform, false);
        var ps = psGO.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.startLifetime = 4.5f;
        main.startSpeed = 0.02f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.075f);
        main.startColor = pink * 1.6f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 2000;
        var emission = ps.emission;
        emission.rateOverTime = 380f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = sphereRadius * 1.04f;
        shape.radiusThickness = 0f;
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;
        var rend = psGO.GetComponent<ParticleSystemRenderer>();
        var pmat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        pmat.SetTexture("_BaseMap", dot);
        pmat.SetColor("_BaseColor", pink * 2.2f);
        pmat.SetFloat("_Surface", 1f);     // transparent
        pmat.SetFloat("_Blend", 1f);       // additive
        pmat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        pmat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        pmat.SetFloat("_ZWrite", 0f);
        pmat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        pmat.renderQueue = 3200;
        rend.material = pmat;
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        ps.Play();
    }

    void BuildSphereCamera()
    {
        rt = new RenderTexture(720, 720, 24, RenderTextureFormat.ARGBHalf);
        rt.antiAliasing = 4;
        rt.Create();

        var camGO = new GameObject("SphereCamera");
        sphereCam = camGO.AddComponent<Camera>();
        sphereCam.transform.position = new Vector3(0f, 0f, -3.4f);
        sphereCam.transform.LookAt(Vector3.zero);
        sphereCam.clearFlags = CameraClearFlags.SolidColor;
        sphereCam.backgroundColor = Color.black;
        sphereCam.fieldOfView = 38f;
        sphereCam.targetTexture = rt;
        var data = sphereCam.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

        // bloom / tonemap volume
        var volGO = new GameObject("PostVolume");
        var vol = volGO.AddComponent<Volume>();
        vol.isGlobal = true;
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        vol.profile = profile;
        var bloom = profile.Add<Bloom>(true);
        bloom.intensity.Override(2.6f);
        bloom.threshold.Override(0.75f);
        bloom.scatter.Override(0.78f);
        bloom.tint.Override(pink);
        var tm = profile.Add<Tonemapping>(true);
        tm.mode.Override(TonemappingMode.ACES);
        var ca = profile.Add<ColorAdjustments>(true);
        ca.postExposure.Override(0.35f);
        ca.saturation.Override(12f);
    }

    // ---------- UI ----------

    void BuildUI()
    {
        // main cam so screen has a clear color even behind overlay
        var mainGO = new GameObject("MainCamera");
        mainGO.tag = "MainCamera";
        var mc = mainGO.AddComponent<Camera>();
        mc.clearFlags = CameraClearFlags.SolidColor;
        mc.backgroundColor = Color.black;
        mc.cullingMask = 0;
        mc.transform.position = new Vector3(0, 0, -20);

        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        var canvasGO = new GameObject("IntroCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        canvasGO.AddComponent<GraphicRaycaster>();

        // black background
        var bg = NewImage("BG", canvasGO.transform, new Color(0.01f, 0.01f, 0.015f, 1f));
        Stretch(bg.rectTransform);

        // title
        var title = NewText("Title", canvasGO.transform, "KILOVERSE  •  UNIQUE COLLECTIBLE", 20, FontStyle.Bold, new Color(1f, 0.8f, 0.95f));
        Anchor(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 250), new Vector2(520, 30));
        title.alignment = TextAnchor.MiddleCenter;

        // Claude content panel: the 300x300 collectible viewport
        claudePanel = new GameObject("ClaudePanel", typeof(RectTransform));
        claudePanel.transform.SetParent(canvasGO.transform, false);
        Anchor((RectTransform)claudePanel.transform, new Vector2(0.5f, 0.5f), new Vector2(0, 55), new Vector2(360, 380));

        var frame = NewImage("Frame", claudePanel.transform, new Color(0.04f, 0.02f, 0.06f, 1f));
        Anchor(frame.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(312, 312));

        var view = new GameObject("Viewport", typeof(RawImage));
        var raw = view.GetComponent<RawImage>();
        raw.texture = rt;
        view.transform.SetParent(claudePanel.transform, false);
        Anchor(raw.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(300, 300));

        var cap = NewText("Caption", claudePanel.transform, "K1L0 SIGIL ORB  —  1 of 1", 14, FontStyle.Normal, new Color(0.75f, 0.6f, 0.85f));
        Anchor(cap.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -135), new Vector2(320, 22));
        cap.alignment = TextAnchor.MiddleCenter;

        // placeholder panels
        codexPanel = MakePlaceholder(canvasGO.transform, "Codex", "CODEX");
        zptPanel = MakePlaceholder(canvasGO.transform, "ZPT", "ZPT");

        // tab bar
        var bar = new GameObject("TabBar", typeof(RectTransform));
        bar.transform.SetParent(canvasGO.transform, false);
        Anchor((RectTransform)bar.transform, new Vector2(0.5f, 0.5f), new Vector2(0, -165), new Vector2(312, 46));
        claudeTab = MakeTab(bar.transform, "Claude", -104, 0);
        codexTab = MakeTab(bar.transform, "Codex", 0, 1);
        zptTab = MakeTab(bar.transform, "ZPT", 104, 2);
    }

    GameObject MakePlaceholder(Transform parent, string name, string label)
    {
        var p = new GameObject(name + "Panel", typeof(RectTransform));
        p.transform.SetParent(parent, false);
        Anchor((RectTransform)p.transform, new Vector2(0.5f, 0.5f), new Vector2(0, 55), new Vector2(360, 380));
        var frame = NewImage("Frame", p.transform, new Color(0.03f, 0.03f, 0.05f, 1f));
        Anchor(frame.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(312, 312));
        var t = NewText("Lock", p.transform, label + "\n◇ locked", 16, FontStyle.Normal, new Color(0.4f, 0.4f, 0.5f));
        Anchor(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(300, 60));
        t.alignment = TextAnchor.MiddleCenter;
        return p;
    }

    Button MakeTab(Transform parent, string label, float x, int index)
    {
        var go = new GameObject("Tab_" + label, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.08f, 0.06f, 0.1f, 1f);
        Anchor(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(x, 0), new Vector2(98, 40));
        var btn = go.AddComponent<Button>();
        var txt = NewText("Label", go.transform, label, 15, FontStyle.Bold, Color.white);
        Stretch(txt.rectTransform);
        txt.alignment = TextAnchor.MiddleCenter;
        btn.onClick.AddListener(() => SelectTab(index));
        return btn;
    }

    void SelectTab(int index)
    {
        claudePanel.SetActive(index == 0);
        codexPanel.SetActive(index == 1);
        zptPanel.SetActive(index == 2);
        Style(claudeTab, index == 0);
        Style(codexTab, index == 1);
        Style(zptTab, index == 2);
    }

    void Style(Button b, bool active)
    {
        var img = b.GetComponent<Image>();
        img.color = active ? new Color(0.55f, 0.16f, 0.5f, 1f) : new Color(0.08f, 0.06f, 0.1f, 1f);
        var t = b.GetComponentInChildren<Text>();
        t.color = active ? Color.white : new Color(0.55f, 0.5f, 0.6f);
    }

    // ---------- ui helpers ----------

    Image NewImage(string name, Transform parent, Color c)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = c;
        return img;
    }

    Text NewText(string name, Transform parent, string s, int size, FontStyle style, Color c)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font = font;
        t.text = s;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = c;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    void Anchor(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    IEnumerator CaptureRoutine()
    {
        yield return new WaitForSeconds(captureDelay);
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(capturePath, 1);
        Debug.Log("[IntroCollectible] screenshot written to " + capturePath);
    }
}
