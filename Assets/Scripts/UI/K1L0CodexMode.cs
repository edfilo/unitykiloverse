using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public class K1L0CodexMode : MonoBehaviour
{
    private static readonly Color Pink = new Color(1f, 0.18f, 0.76f, 1f);
    private static readonly Color Purple = new Color(0.42f, 0.16f, 1f, 1f);
    // Claude identity: warm Anthropic "spark" palette on a light ground, not Codex's dark pink ball.
    private static readonly Color ClaudeCoral = new Color(0.851f, 0.467f, 0.341f, 1f); // #D97757
    private static readonly Color ClaudeEmber = new Color(0.96f, 0.62f, 0.40f, 1f);
    private static readonly Color ClaudeCream = new Color(0.937f, 0.918f, 0.882f, 1f); // #EFEAE1
    private static readonly Vector3 SceneOrigin = new Vector3(8000f, -8000f, 4000f);
    private const int CollectibleLayer = 31;

    private enum OrbStyle { Codex, Claude }

    private Transform collectible;
    private GameObject visualsRoot;
    private Camera sphereCamera;
    private RenderTexture renderTexture;
    private Volume postVolume;
    private Bloom bloom;
    private Tonemapping tonemap;
    private ColorAdjustments colorAdjust;
    private OrbStyle currentStyle = OrbStyle.Codex;
    private Button codexStyleButton;
    private Button claudeStyleButton;
    private Image uiBackground;
    private Image uiFrame;
    private TextMeshProUGUI uiTitle;
    private TextMeshProUGUI uiCaption;
    private TMP_FontAsset font;
    private bool initialized;
    private bool screenshotQueued;

    public void Initialize(RectTransform parent, TMP_FontAsset monoFont)
    {
        font = monoFont;

        RectTransform root = gameObject.AddComponent<RectTransform>();
        root.SetParent(parent, false);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;

        BuildRenderScene();
        BuildUI(root);
        initialized = true;
    }

    private void OnEnable()
    {
        if (initialized && !screenshotQueued)
        {
            screenshotQueued = true;
            StartCoroutine(CaptureOpenedScreen());
        }
    }

    private void Update()
    {
        if (collectible == null)
            return;

        collectible.Rotate(new Vector3(8f, 16f, 0f) * Time.deltaTime, Space.Self);
    }

    public void RotateCollectible(Vector2 delta)
    {
        if (collectible == null)
            return;

        collectible.Rotate(Vector3.up, -delta.x * 0.35f, Space.World);
        collectible.Rotate(Vector3.right, delta.y * 0.35f, Space.World);
    }

    private void BuildRenderScene()
    {
        renderTexture = new RenderTexture(900, 900, 24, RenderTextureFormat.ARGBHalf)
        {
            antiAliasing = 4,
            name = "K1L0CodexCollectibleRT"
        };
        renderTexture.Create();

        GameObject root = new GameObject("K1L0_CodexCollectible");
        root.transform.position = SceneOrigin;
        collectible = root.transform;
        SetLayer(root);

        GameObject cameraGO = new GameObject("K1L0_CodexCamera");
        cameraGO.transform.position = SceneOrigin + new Vector3(0f, 0f, -4.25f);
        cameraGO.transform.LookAt(SceneOrigin);
        sphereCamera = cameraGO.AddComponent<Camera>();
        sphereCamera.clearFlags = CameraClearFlags.SolidColor;
        sphereCamera.backgroundColor = Color.black;
        sphereCamera.fieldOfView = 35f;
        sphereCamera.nearClipPlane = 0.03f;
        sphereCamera.farClipPlane = 20f;
        sphereCamera.cullingMask = 1 << CollectibleLayer;
        sphereCamera.targetTexture = renderTexture;
        SetLayer(cameraGO);

        UniversalAdditionalCameraData cameraData = sphereCamera.GetUniversalAdditionalCameraData();
        if (cameraData != null)
        {
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        }

        GameObject volumeGO = new GameObject("K1L0_CodexPost");
        postVolume = volumeGO.AddComponent<Volume>();
        postVolume.isGlobal = true;
        postVolume.priority = 500f;
        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        postVolume.profile = profile;
        bloom = profile.Add<Bloom>(true);
        tonemap = profile.Add<Tonemapping>(true);
        tonemap.mode.Override(TonemappingMode.ACES);
        colorAdjust = profile.Add<ColorAdjustments>(true);

        ApplyStyle(currentStyle);
    }

    private void ApplyStyle(OrbStyle style)
    {
        currentStyle = style;

        if (visualsRoot != null)
            Destroy(visualsRoot);
        visualsRoot = new GameObject("Visuals");
        visualsRoot.transform.SetParent(collectible, false);
        SetLayer(visualsRoot);

        if (style == OrbStyle.Codex)
        {
            if (sphereCamera != null)
                sphereCamera.backgroundColor = Color.black;
            if (tonemap != null)
                tonemap.mode.Override(TonemappingMode.ACES);
            BuildLighting(visualsRoot.transform);
            BuildSphere(visualsRoot.transform);
            if (bloom != null)
            {
                bloom.intensity.Override(1.55f);
                bloom.threshold.Override(0.95f);
                bloom.scatter.Override(0.58f);
                bloom.tint.Override(Pink);
            }
            if (colorAdjust != null)
            {
                colorAdjust.postExposure.Override(0.25f);
                colorAdjust.contrast.Override(18f);
                colorAdjust.saturation.Override(15f);
            }
        }
        else
        {
            // Warm ground so the spark reads as Claude — the opposite of Codex's black void.
            if (sphereCamera != null)
                sphereCamera.backgroundColor = new Color(0.11f, 0.075f, 0.055f, 1f);
            // Neutral tonemap keeps the coral from desaturating to white the way ACES does.
            if (tonemap != null)
                tonemap.mode.Override(TonemappingMode.Neutral);
            BuildClaudeLighting(visualsRoot.transform);
            BuildClaudeSpark(visualsRoot.transform);
            if (bloom != null)
            {
                bloom.intensity.Override(0.65f);
                bloom.threshold.Override(1.1f);
                bloom.scatter.Override(0.6f);
                bloom.tint.Override(ClaudeEmber);
            }
            if (colorAdjust != null)
            {
                colorAdjust.postExposure.Override(0f);
                colorAdjust.contrast.Override(8f);
                colorAdjust.saturation.Override(22f);
            }
        }

        ApplyChrome(style);
        UpdateStyleButtons();
    }

    private void ApplyChrome(OrbStyle style)
    {
        bool claude = style == OrbStyle.Claude;
        if (uiBackground != null)
            uiBackground.color = claude ? ClaudeCream : Color.black;
        if (uiFrame != null)
            uiFrame.color = claude ? new Color(0.86f, 0.83f, 0.78f, 1f) : new Color(0.025f, 0f, 0.04f, 1f);
        if (uiTitle != null)
        {
            uiTitle.text = claude ? "CLAUDE" : "CODEX";
            uiTitle.color = claude ? ClaudeCoral : new Color(1f, 0.74f, 0.96f, 1f);
        }
        if (uiCaption != null)
        {
            uiCaption.text = claude
                ? "K1L0 CLAUDE SPARK  /  UNIQUE COLLECTIBLE  /  1 OF 1"
                : "K1L0 CODEX ORB  /  UNIQUE COLLECTIBLE  /  1 OF 1";
            uiCaption.color = claude ? new Color(0.55f, 0.36f, 0.27f, 1f) : new Color(0.86f, 0.56f, 0.94f, 1f);
        }
    }

    private void BuildLighting(Transform parent)
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.05f, 0.02f, 0.075f, 1f);

        AddLight(parent, "CodexKey", LightType.Directional, new Color(1f, 0.72f, 0.94f), 1.15f, new Vector3(38f, -32f, 0f), Vector3.zero, 10f);
        AddLight(parent, "CodexRimPink", LightType.Point, Pink, 4.2f, Vector3.zero, new Vector3(-2.3f, 0.7f, -1.9f), 8f);
        AddLight(parent, "CodexRimPurple", LightType.Point, Purple, 2.6f, Vector3.zero, new Vector3(2.2f, -1.0f, -2.0f), 8f);
    }

    private void AddLight(Transform parent, string name, LightType type, Color color, float intensity, Vector3 rotation, Vector3 localPosition, float range)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = Quaternion.Euler(rotation);
        SetLayer(go);

        Light light = go.AddComponent<Light>();
        light.type = type;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.cullingMask = 1 << CollectibleLayer;
    }

    private void BuildSphere(Transform parent)
    {
        Texture2D sigil = MakeSigilMask(256);
        Texture2D albedo = Tint(sigil, new Color(0.035f, 0.005f, 0.055f, 1f), new Color(0.74f, 0.15f, 0.64f, 1f));
        Texture2D emission = Tint(sigil, Color.black, Pink);

        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "CodexSigilSphere";
        sphere.transform.SetParent(parent, false);
        sphere.transform.localScale = Vector3.one * 2f;
        Destroy(sphere.GetComponent<Collider>());
        SetLayer(sphere);

        Material material = new Material(FindShader("Universal Render Pipeline/Lit", "Standard"));
        material.SetTexture("_BaseMap", albedo);
        material.SetTexture("_MainTex", albedo);
        material.SetTextureScale("_BaseMap", new Vector2(7f, 5f));
        material.SetTextureScale("_MainTex", new Vector2(7f, 5f));
        material.SetColor("_BaseColor", Color.white);
        material.SetColor("_Color", Color.white);
        material.SetFloat("_Metallic", 0.78f);
        material.SetFloat("_Smoothness", 0.92f);
        material.EnableKeyword("_EMISSION");
        material.SetTexture("_EmissionMap", emission);
        material.SetTextureScale("_EmissionMap", new Vector2(7f, 5f));
        material.SetColor("_EmissionColor", Pink * 1.45f);
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        sphere.GetComponent<MeshRenderer>().material = material;

        BuildParticleShell(parent);
    }

    private void BuildParticleShell(Transform parent)
    {
        GameObject shell = new GameObject("CodexParticleShell");
        shell.transform.SetParent(parent, false);
        SetLayer(shell);

        ParticleSystem particles = shell.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.startLifetime = 5f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.005f, 0.025f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.014f, 0.044f);
        main.startColor = new ParticleSystem.MinMaxGradient(Pink * 1.15f, Purple);
        main.maxParticles = 1500;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 260f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 1.035f;
        shape.radiusThickness = 0f;

        ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
        color.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(new Color(1f, 0.7f, 1f), 0.55f),
                new GradientColorKey(Purple, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.2f),
                new GradientAlphaKey(0.9f, 0.75f),
                new GradientAlphaKey(0f, 1f)
            });
        color.color = gradient;

        ParticleSystemRenderer renderer = shell.GetComponent<ParticleSystemRenderer>();
        Material particleMaterial = new Material(FindShader("Universal Render Pipeline/Particles/Unlit", "Particles/Standard Unlit"));
        particleMaterial.SetTexture("_BaseMap", MakeDot(64));
        particleMaterial.SetTexture("_MainTex", MakeDot(64));
        particleMaterial.SetColor("_BaseColor", Pink * 1.35f);
        particleMaterial.SetColor("_Color", Pink * 1.35f);
        particleMaterial.SetFloat("_Surface", 1f);
        particleMaterial.SetFloat("_Blend", 1f);
        particleMaterial.SetFloat("_SrcBlend", (float)BlendMode.One);
        particleMaterial.SetFloat("_DstBlend", (float)BlendMode.One);
        particleMaterial.SetFloat("_ZWrite", 0f);
        particleMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        particleMaterial.renderQueue = 3200;
        renderer.material = particleMaterial;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        particles.Play();
    }

    private void BuildClaudeLighting(Transform parent)
    {
        // Soft, warm, mostly ambient — the spark is emissive, so lighting just shapes the edges.
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.22f, 0.16f, 0.13f, 1f);

        AddLight(parent, "ClaudeKey", LightType.Directional, new Color(1f, 0.82f, 0.62f), 0.55f, new Vector3(35f, -28f, 0f), Vector3.zero, 10f);
        AddLight(parent, "ClaudeWarmRim", LightType.Point, ClaudeCoral, 5.5f, Vector3.zero, new Vector3(-1.8f, 1.2f, -2.2f), 10f);
    }

    // The Claude orb: a sphere whose surface carries a from-scratch coral "spark" burst,
    // wrapped with a particle skin pinned to the surface. Everything lives on the sphere —
    // no outward spikes, no free-floating dust.
    private void BuildClaudeSpark(Transform parent)
    {
        Texture2D spark = MakeClaudeSparkMask(256);
        Texture2D albedo = Tint(spark, new Color(0.10f, 0.045f, 0.03f, 1f), new Color(0.62f, 0.27f, 0.18f, 1f));
        Texture2D emission = Tint(spark, Color.black, ClaudeCoral);

        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "ClaudeSparkSphere";
        sphere.transform.SetParent(parent, false);
        sphere.transform.localScale = Vector3.one * 2f;
        Destroy(sphere.GetComponent<Collider>());
        SetLayer(sphere);

        Material material = new Material(FindShader("Universal Render Pipeline/Lit", "Standard"));
        material.SetTexture("_BaseMap", albedo);
        material.SetTexture("_MainTex", albedo);
        material.SetTextureScale("_BaseMap", new Vector2(4f, 3f));
        material.SetTextureScale("_MainTex", new Vector2(4f, 3f));
        material.SetColor("_BaseColor", Color.white);
        material.SetColor("_Color", Color.white);
        material.SetFloat("_Metallic", 0.45f);
        material.SetFloat("_Smoothness", 0.7f);
        material.EnableKeyword("_EMISSION");
        material.SetTexture("_EmissionMap", emission);
        material.SetTextureScale("_EmissionMap", new Vector2(4f, 3f));
        material.SetColor("_EmissionColor", ClaudeCoral * 1.1f);
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        sphere.GetComponent<MeshRenderer>().material = material;

        BuildClaudeSurfaceParticles(parent);
    }

    // Embers that sit ON the sphere surface and twinkle in place — emitted from the
    // shell with zero speed, so nothing drifts off the surface.
    private void BuildClaudeSurfaceParticles(Transform parent)
    {
        GameObject shell = new GameObject("ClaudeSurfaceEmbers");
        shell.transform.SetParent(parent, false);
        SetLayer(shell);

        ParticleSystem particles = shell.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.startLifetime = 2.6f;
        main.startSpeed = 0f; // pinned to the surface — no radial drift
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
        main.startColor = new ParticleSystem.MinMaxGradient(ClaudeEmber, ClaudeCoral);
        main.maxParticles = 1200;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 420f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 1.0f;          // sphere primitive scaled x2 → surface radius 1.0
        shape.radiusThickness = 0f;   // emit exactly on the shell, not the interior

        ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
        color.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(ClaudeEmber, 0f),
                new GradientColorKey(ClaudeCoral, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.4f),
                new GradientAlphaKey(0f, 1f)
            });
        color.color = gradient;

        ParticleSystemRenderer renderer = shell.GetComponent<ParticleSystemRenderer>();
        Material particleMaterial = new Material(FindShader("Universal Render Pipeline/Particles/Unlit", "Particles/Standard Unlit"));
        particleMaterial.SetTexture("_BaseMap", MakeDot(64));
        particleMaterial.SetTexture("_MainTex", MakeDot(64));
        particleMaterial.SetColor("_BaseColor", ClaudeEmber * 1.6f);
        particleMaterial.SetColor("_Color", ClaudeEmber * 1.6f);
        particleMaterial.SetFloat("_Surface", 1f);
        particleMaterial.SetFloat("_Blend", 1f);
        particleMaterial.SetFloat("_SrcBlend", (float)BlendMode.One);
        particleMaterial.SetFloat("_DstBlend", (float)BlendMode.One);
        particleMaterial.SetFloat("_ZWrite", 0f);
        particleMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        particleMaterial.renderQueue = 3200;
        renderer.material = particleMaterial;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        particles.Play();
    }

    // A from-scratch radial spark: 12 tapered rays of alternating length fanning from a
    // central node — the Claude/Anthropic burst, distinct from Codex's ringed wheel sigil.
    private static Texture2D MakeClaudeSparkMask(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Trilinear
        };
        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        const int rays = 12;
        float halfWidth = size * 0.05f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f) - center;
                float best = 0f;
                for (int k = 0; k < rays; k++)
                {
                    float angle = k * (Mathf.PI * 2f / rays);
                    Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    float len = ((k & 1) == 0 ? 0.46f : 0.30f) * size;
                    float along = Vector2.Dot(p, dir);
                    if (along < 0f || along > len)
                        continue;
                    float perp = Mathf.Abs(Vector2.Dot(p, new Vector2(-dir.y, dir.x)));
                    float taper = halfWidth * (1f - along / len); // taper to a point at the tip
                    best = Mathf.Max(best, Mathf.Clamp01(1f - perp / Mathf.Max(taper, 0.5f)));
                }
                // Bright central node.
                float node = Mathf.Clamp01(1f - p.magnitude / (size * 0.07f));
                best = Mathf.Max(best, node);
                pixels[y * size + x] = new Color(best, best, best, best);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(true);
        return texture;
    }

    private void BuildUI(RectTransform root)
    {
        uiBackground = AddImage("CodexBlack", root, Color.black);
        Stretch(uiBackground.rectTransform);
        uiBackground.raycastTarget = false;

        uiTitle = AddText("CodexTitle", root, "CODEX", 22f, new Color(1f, 0.74f, 0.96f, 1f));
        Anchor(uiTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(240f, 32f));
        uiTitle.alignment = TextAlignmentOptions.Center;

        uiFrame = AddImage("CodexFrame", root, new Color(0.025f, 0f, 0.04f, 1f));
        Anchor(uiFrame.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 28f), new Vector2(314f, 314f));
        uiFrame.raycastTarget = false;

        GameObject view = new GameObject("CodexViewport", typeof(RectTransform), typeof(RawImage), typeof(CodexDragSurface));
        view.transform.SetParent(root, false);
        RawImage raw = view.GetComponent<RawImage>();
        raw.texture = renderTexture;
        raw.color = Color.white;
        raw.raycastTarget = true;
        Anchor(raw.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 28f), new Vector2(300f, 300f));
        view.GetComponent<CodexDragSurface>().owner = this;

        uiCaption = AddText("CodexCaption", root, "K1L0 CODEX ORB  /  UNIQUE COLLECTIBLE  /  1 OF 1", 12f, new Color(0.86f, 0.56f, 0.94f, 1f));
        Anchor(uiCaption.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -150f), new Vector2(330f, 22f));
        uiCaption.alignment = TextAlignmentOptions.Center;

        GameObject toggleBar = new GameObject("CodexStyleToggle", typeof(RectTransform));
        toggleBar.transform.SetParent(root, false);
        Anchor((RectTransform)toggleBar.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -180f), new Vector2(300f, 32f));

        codexStyleButton = AddStyleButton(toggleBar.transform, "CODEX", -72f, OrbStyle.Codex);
        claudeStyleButton = AddStyleButton(toggleBar.transform, "CLAUDE", 72f, OrbStyle.Claude);

        ApplyChrome(currentStyle);
        UpdateStyleButtons();
    }

    private Button AddStyleButton(Transform parent, string label, float x, OrbStyle style)
    {
        GameObject go = new GameObject("StyleBtn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = new Color(0.08f, 0.05f, 0.12f, 1f);
        Anchor(image.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(132f, 30f));

        TextMeshProUGUI text = AddText("StyleBtnLabel_" + label, go.transform, label, 14f, Color.white);
        Stretch(text.rectTransform);
        text.alignment = TextAlignmentOptions.Center;

        Button button = go.GetComponent<Button>();
        button.onClick.AddListener(() => ApplyStyle(style));
        return button;
    }

    private void UpdateStyleButtons()
    {
        StyleButton(codexStyleButton, currentStyle == OrbStyle.Codex);
        StyleButton(claudeStyleButton, currentStyle == OrbStyle.Claude);
    }

    private void StyleButton(Button button, bool active)
    {
        if (button == null)
            return;

        Image image = button.GetComponent<Image>();
        if (image != null)
            image.color = active ? new Color(0.62f, 0.16f, 0.55f, 1f) : new Color(0.08f, 0.05f, 0.12f, 1f);

        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null)
            label.color = active ? Color.white : new Color(0.6f, 0.5f, 0.66f, 1f);
    }

    private IEnumerator CaptureOpenedScreen()
    {
        yield return new WaitForSecondsRealtime(1.5f);
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot("/tmp/k1l0_codex.png", 1);
        Debug.Log("[K1L0CodexMode] screenshot saved to /tmp/k1l0_codex.png");
    }

    private static Texture2D MakeSigilMask(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Trilinear
        };
        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                float d = Mathf.Abs(Vector2.Distance(p, center) - size * 0.34f) - size * 0.018f;
                d = Mathf.Min(d, Mathf.Abs(p.x - center.x) - size * 0.014f);
                d = Mathf.Min(d, Mathf.Abs(p.y - center.y) - size * 0.014f);

                for (int i = 0; i < 4; i++)
                {
                    float angle = (45f + i * 90f) * Mathf.Deg2Rad;
                    Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    d = Mathf.Min(d, DistanceToSegment(p, center - dir * size * 0.27f, center + dir * size * 0.27f) - size * 0.012f);
                }

                float node = Vector2.Distance(p, center) - size * 0.052f;
                d = Mathf.Min(d, node);
                float alpha = Mathf.Clamp01(1f - Mathf.SmoothStep(0f, 2.2f, d));
                pixels[y * size + x] = new Color(alpha, alpha, alpha, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(true);
        return texture;
    }

    private static Texture2D MakeDot(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center) / (size * 0.5f);
                float alpha = Mathf.Pow(Mathf.Clamp01(1f - d), 3.2f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(true);
        return texture;
    }

    private static Texture2D Tint(Texture2D mask, Color background, Color foreground)
    {
        Texture2D texture = new Texture2D(mask.width, mask.height, TextureFormat.RGBA32, true)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = TextureFilterMode(mask)
        };
        Color[] source = mask.GetPixels();
        Color[] output = new Color[source.Length];
        for (int i = 0; i < source.Length; i++)
        {
            output[i] = Color.Lerp(background, foreground, source[i].a);
            output[i].a = 1f;
        }

        texture.SetPixels(output);
        texture.Apply(true);
        return texture;
    }

    private static FilterMode TextureFilterMode(Texture2D texture)
    {
        return texture != null ? texture.filterMode : FilterMode.Bilinear;
    }

    private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / Vector2.Dot(ab, ab));
        return Vector2.Distance(point, a + ab * t);
    }

    private Image AddImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private TextMeshProUGUI AddText(string name, Transform parent, string value, float size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.enableWordWrapping = false;
        text.raycastTarget = false;
        return text;
    }

    private static void Stretch(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private static void Anchor(RectTransform rectTransform, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rectTransform.anchorMin = anchor;
        rectTransform.anchorMax = anchor;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = position;
        rectTransform.sizeDelta = size;
    }

    private static Shader FindShader(string preferred, string fallback)
    {
        Shader shader = Shader.Find(preferred);
        return shader != null ? shader : Shader.Find(fallback);
    }

    private static void SetLayer(GameObject go)
    {
        go.layer = CollectibleLayer;
        foreach (Transform child in go.transform)
            SetLayer(child.gameObject);
    }

    private void OnDestroy()
    {
        if (sphereCamera != null)
            Destroy(sphereCamera.gameObject);
        if (collectible != null)
            Destroy(collectible.gameObject);
        if (postVolume != null)
            Destroy(postVolume.gameObject);
        if (renderTexture != null)
            renderTexture.Release();
    }

    private class CodexDragSurface : MonoBehaviour, IDragHandler
    {
        public K1L0CodexMode owner;

        public void OnDrag(PointerEventData eventData)
        {
            if (owner != null)
                owner.RotateCollectible(eventData.delta);
        }
    }
}
