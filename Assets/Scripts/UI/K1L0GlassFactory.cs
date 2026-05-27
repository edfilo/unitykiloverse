using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public struct K1L0GlassStyle
{
    public string materialKey;
    public Color glassTint;
    public float fillAlpha;
    public float backdropInfluence;
    public float luminanceCompression;
    public float distortion;
    public float centerDarken;
    public float edgeLift;
    public Color overlayColor;
    public Color borderColor;
    public Color sheenColor;
    public Color accentColor;
    public Color shadowColor;
    public Vector2 shadowOffset;
}

public static class K1L0ModalContrastMode
{
    private static int depth;
    private static bool hasSavedContrast;
    private static float savedContrast;
    private static int fadeToken;

    public static void Begin(MonoBehaviour owner)
    {
        var pfx = KiloWorld.Rendering.Systems.RenderManager.Instance?.profile?.postFX;
        if (pfx == null) return;

        if (depth == 0)
        {
            savedContrast = pfx.contrast;
            hasSavedContrast = true;
            FadeTo(owner, -100f, 0.18f);
        }

        depth++;
    }

    public static void End(MonoBehaviour owner)
    {
        if (depth > 0) depth--;
        if (depth != 0 || !hasSavedContrast) return;

        FadeTo(owner, savedContrast, 0.22f);
        hasSavedContrast = false;
    }

    private static void FadeTo(MonoBehaviour owner, float target, float duration)
    {
        fadeToken++;
        if (owner != null && owner.isActiveAndEnabled)
            owner.StartCoroutine(FadeContrast(target, duration, fadeToken));
        else
            SetContrast(target);
    }

    private static IEnumerator FadeContrast(float target, float duration, int token)
    {
        var pfx = KiloWorld.Rendering.Systems.RenderManager.Instance?.profile?.postFX;
        if (pfx == null) yield break;

        float start = pfx.contrast;
        float t = 0f;
        while (t < duration)
        {
            if (token != fadeToken) yield break;
            t += Time.unscaledDeltaTime;
            float k = duration > 0f ? Mathf.Clamp01(t / duration) : 1f;
            pfx.contrast = Mathf.Lerp(start, target, k);
            yield return null;
        }

        if (token == fadeToken) pfx.contrast = target;
    }

    private static void SetContrast(float value)
    {
        var pfx = KiloWorld.Rendering.Systems.RenderManager.Instance?.profile?.postFX;
        if (pfx != null) pfx.contrast = value;
    }
}

public static class K1L0ModalHudMode
{
    private static int depth;
    private static bool savedHudActive;
    private static bool hasSavedHud;

    public static void Begin()
    {
        var hud = K1L0CanvasRoot.HUD;
        if (hud == null) return;

        if (depth == 0)
        {
            savedHudActive = hud.gameObject.activeSelf;
            hasSavedHud = true;
            hud.gameObject.SetActive(false);
            SignalBeamBridge.SetHudSuppressed(true);
            POILabelBridge.SetHudSuppressed(true);
        }

        depth++;
    }

    public static void End()
    {
        if (depth > 0) depth--;
        if (depth != 0 || !hasSavedHud) return;

        var hud = K1L0CanvasRoot.HUD;
        if (hud != null) hud.gameObject.SetActive(savedHudActive);
        SignalBeamBridge.SetHudSuppressed(false);
        POILabelBridge.SetHudSuppressed(false);
        hasSavedHud = false;
    }
}

public sealed class K1L0GlassChrome
{
    public Image shadow;
    public Image blurFill;
    public Image overlay;
    public Image border;
    public Image accent;
    public Image sheen;
    public RectTransform sheenMask;

    public Color baseOverlay;
    public Color baseBorder;
    public Color baseAccent;
    public Color baseSheen;

    public void SetAccent(float amount)
    {
        if (accent == null)
            return;

        amount = Mathf.Clamp01(amount);
        accent.color = new Color(baseAccent.r, baseAccent.g, baseAccent.b, baseAccent.a * amount);

        if (border != null)
            border.color = Color.Lerp(baseBorder, new Color(baseAccent.r, baseAccent.g, baseAccent.b, Mathf.Max(baseBorder.a, baseAccent.a)), amount * 0.45f);

        if (overlay != null)
            overlay.color = Color.Lerp(baseOverlay, new Color(baseOverlay.r * 0.9f, baseOverlay.g * 0.96f, baseOverlay.b, baseOverlay.a * 0.92f), amount * 0.25f);

        if (sheen != null)
            sheen.color = Color.Lerp(baseSheen, new Color(baseSheen.r, baseSheen.g, baseSheen.b, baseSheen.a * 1.35f), amount * 0.35f);
    }
}

public static class K1L0GlassFactory
{
    private static readonly Dictionary<string, Material> SharedMaterials = new Dictionary<string, Material>();
    private static Sprite roundedRectSprite;
    private static Sprite roundedBorderSprite;
    private static Sprite dockRectSprite;
    private static Sprite dockBorderSprite;
    private static Sprite controlRectSprite;
    private static Sprite controlBorderSprite;
    private static Sprite filterRectSprite;
    private static Sprite filterBorderSprite;

    public static Sprite RoundedRectSprite => roundedRectSprite != null ? roundedRectSprite : roundedRectSprite = CreateRoundedRectSprite(256, 48, 7);
    public static Sprite RoundedBorderSprite => roundedBorderSprite != null ? roundedBorderSprite : roundedBorderSprite = CreateRoundedBorderSprite(256, 48, 7, 2);
    public static Sprite DockRectSprite => dockRectSprite != null ? dockRectSprite : dockRectSprite = CreateRoundedRectSprite(256, 54, 8);
    public static Sprite DockBorderSprite => dockBorderSprite != null ? dockBorderSprite : dockBorderSprite = CreateRoundedBorderSprite(256, 54, 8, 2);
    public static Sprite ControlRectSprite => controlRectSprite != null ? controlRectSprite : controlRectSprite = CreateRoundedRectSprite(256, 52, 7);
    public static Sprite ControlBorderSprite => controlBorderSprite != null ? controlBorderSprite : controlBorderSprite = CreateRoundedBorderSprite(256, 52, 7, 2);
    public static Sprite FilterRectSprite => filterRectSprite != null ? filterRectSprite : filterRectSprite = CreateRoundedRectSprite(256, 44, 6);
    public static Sprite FilterBorderSprite => filterBorderSprite != null ? filterBorderSprite : filterBorderSprite = CreateRoundedBorderSprite(256, 44, 6, 2);

    public static K1L0GlassStyle DockButtonStyle => new K1L0GlassStyle
    {
        materialKey = "dock",
        glassTint = new Color(0.050f, 0.070f, 0.106f, 1f),
        fillAlpha = 0.54f,
        backdropInfluence = 0.38f,
        luminanceCompression = 2.2f,
        distortion = 0.0008f,
        centerDarken = 0.05f,
        edgeLift = 0.06f,
        overlayColor = new Color(0.014f, 0.022f, 0.040f, 0.10f),
        borderColor = new Color(0.60f, 0.78f, 0.96f, 0.24f),
        sheenColor = new Color(0.92f, 0.97f, 1f, 0.010f),
        accentColor = new Color(0.34f, 0.82f, 1f, 0.10f),
        shadowColor = new Color(0.01f, 0.02f, 0.05f, 0.18f),
        shadowOffset = new Vector2(0f, -3f)
    };

    public static K1L0GlassStyle PanelStyle => new K1L0GlassStyle
    {
        materialKey = "panel",
        glassTint = new Color(0.056f, 0.078f, 0.128f, 1f),
        fillAlpha = 0.54f,
        backdropInfluence = 0.48f,
        luminanceCompression = 2.05f,
        distortion = 0.0011f,
        centerDarken = 0.08f,
        edgeLift = 0.08f,
        overlayColor = new Color(0.018f, 0.026f, 0.046f, 0.24f),
        borderColor = new Color(0.56f, 0.74f, 0.94f, 0.22f),
        sheenColor = new Color(0.94f, 0.98f, 1f, 0.028f),
        accentColor = new Color(0.30f, 0.78f, 1f, 0.18f),
        shadowColor = new Color(0.01f, 0.02f, 0.05f, 0.20f),
        shadowOffset = new Vector2(0f, -3f)
    };

    public static K1L0GlassStyle ControlStyle => new K1L0GlassStyle
    {
        materialKey = "control",
        glassTint = new Color(0.064f, 0.088f, 0.138f, 1f),
        fillAlpha = 0.50f,
        backdropInfluence = 0.48f,
        luminanceCompression = 1.9f,
        distortion = 0.0012f,
        centerDarken = 0.07f,
        edgeLift = 0.07f,
        overlayColor = new Color(0.016f, 0.026f, 0.048f, 0.22f),
        borderColor = new Color(0.56f, 0.74f, 0.92f, 0.20f),
        sheenColor = new Color(0.94f, 0.98f, 1f, 0.024f),
        accentColor = new Color(0.28f, 0.82f, 1f, 0.24f),
        shadowColor = new Color(0.01f, 0.02f, 0.05f, 0.18f),
        shadowOffset = new Vector2(0f, -1.5f)
    };

    public static K1L0GlassStyle FilterStyle => new K1L0GlassStyle
    {
        materialKey = "filter",
        glassTint = new Color(0.064f, 0.088f, 0.138f, 1f),
        fillAlpha = 0.52f,
        backdropInfluence = 0.48f,
        luminanceCompression = 1.9f,
        distortion = 0.0012f,
        centerDarken = 0.05f,
        edgeLift = 0.05f,
        overlayColor = new Color(0.016f, 0.026f, 0.048f, 0.36f),
        borderColor = new Color(0.54f, 0.72f, 0.90f, 0.22f),
        sheenColor = new Color(0.94f, 0.98f, 1f, 0.050f),
        accentColor = new Color(0.28f, 0.82f, 1f, 0.30f),
        shadowColor = new Color(0.01f, 0.02f, 0.05f, 0.20f),
        shadowOffset = new Vector2(0f, -1.25f)
    };

    public static K1L0GlassChrome AttachChrome(Transform parent, string namePrefix, K1L0GlassStyle style)
    {
        var chrome = new K1L0GlassChrome();
        Sprite fillSprite = RoundedRectSprite;
        Sprite borderSprite = RoundedBorderSprite;

        if (style.materialKey == "dock")
        {
            fillSprite = DockRectSprite;
            borderSprite = DockBorderSprite;
        }
        else if (style.materialKey == "control")
        {
            fillSprite = ControlRectSprite;
            borderSprite = ControlBorderSprite;
        }
        else if (style.materialKey == "filter")
        {
            fillSprite = FilterRectSprite;
            borderSprite = FilterBorderSprite;
        }

        chrome.shadow = CreateStretchImage(parent, $"{namePrefix}_Shadow", fillSprite, style.shadowColor, 0f);
        SetOffsets(chrome.shadow.rectTransform, style.shadowOffset, style.shadowOffset);

        chrome.blurFill = CreateStretchImage(parent, $"{namePrefix}_Blur", fillSprite, Color.white, 0f);
        chrome.blurFill.material = GetSharedGlassMaterial(style);
        chrome.blurFill.maskable = true;

        chrome.overlay = CreateStretchImage(parent, $"{namePrefix}_Overlay", fillSprite, style.overlayColor, 1.5f);
        chrome.border = CreateStretchImage(parent, $"{namePrefix}_Border", borderSprite, style.borderColor, 0f);
        chrome.accent = CreateStretchImage(parent, $"{namePrefix}_Accent", fillSprite, new Color(style.accentColor.r, style.accentColor.g, style.accentColor.b, 0f), -2f);

        GameObject sheenMaskGO = new GameObject($"{namePrefix}_SheenMask", typeof(RectTransform), typeof(RectMask2D));
        sheenMaskGO.transform.SetParent(parent, false);
        RectTransform sheenMaskRT = sheenMaskGO.GetComponent<RectTransform>();
        sheenMaskRT.anchorMin = new Vector2(0f, 0.58f);
        sheenMaskRT.anchorMax = Vector2.one;
        sheenMaskRT.offsetMin = new Vector2(5f, 0f);
        sheenMaskRT.offsetMax = new Vector2(-5f, -4f);
        chrome.sheenMask = sheenMaskRT;

        chrome.sheen = CreateStretchImage(sheenMaskGO.transform, $"{namePrefix}_Sheen", fillSprite, style.sheenColor, 0f);
        chrome.sheen.rectTransform.offsetMin = new Vector2(-4f, -14f);
        chrome.sheen.rectTransform.offsetMax = new Vector2(4f, 12f);

        chrome.baseOverlay = style.overlayColor;
        chrome.baseBorder = style.borderColor;
        chrome.baseAccent = style.accentColor;
        chrome.baseSheen = style.sheenColor;

        return chrome;
    }

    public static TextMeshProUGUI CreateLabel(Transform parent, string name, TMP_FontAsset font, float size, TextAlignmentOptions alignment, Color color, bool raycast = false)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.fontSize = size;
        label.alignment = alignment;
        label.color = color;
        label.raycastTarget = raycast;
        return label;
    }

    public static Image CreateSeparator(Transform parent, string name, float y, Color color, float inset = 0f)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.offsetMin = new Vector2(inset, -0.5f);
        rt.offsetMax = new Vector2(-inset, 0.5f);
        rt.sizeDelta = new Vector2(0f, 1f);

        Image image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    public static K1L0ModalStaticEffect AttachTerminalStatic(Transform parent, string name = "TerminalStatic")
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(RawImage), typeof(K1L0ModalStaticEffect));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        RawImage image = go.GetComponent<RawImage>();
        image.raycastTarget = false;
        image.color = new Color(0.47f, 1f, 0.54f, 0.045f);

        var effect = go.GetComponent<K1L0ModalStaticEffect>();
        effect.Configure(image);
        return effect;
    }

    public static Button CreateTerminalCloseButton(Transform parent, TMP_FontAsset font, UnityAction onClick)
    {
        GameObject go = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-20f, -20f);
        rt.sizeDelta = new Vector2(72f, 72f);

        Image img = go.GetComponent<Image>();
        img.sprite = ControlRectSprite;
        img.type = Image.Type.Sliced;
        img.color = new Color(0f, 0f, 0f, 1f);

        Button button = go.GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = img;
        if (onClick != null) button.onClick.AddListener(onClick);

        var labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(go.transform, false);
        var labelRt = labelGO.GetComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;

        var label = labelGO.AddComponent<TextMeshProUGUI>();
        label.font = font != null ? font : TMP_Settings.defaultFontAsset;
        label.fontSize = 42f;
        label.color = new Color(0.56f, 1f, 0.62f, 1f);
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.text = "×";
        return button;
    }

    public static void ClearCachedMaterials()
    {
        foreach (var kvp in SharedMaterials)
        {
            if (kvp.Value != null)
                Object.Destroy(kvp.Value);
        }

        SharedMaterials.Clear();
    }

    private static Material GetSharedGlassMaterial(K1L0GlassStyle style)
    {
        if (SharedMaterials.TryGetValue(style.materialKey, out Material existing) && existing != null)
            return existing;

        Shader shader = Shader.Find("K1L0/UIFrostedGlass");
        if (shader == null)
            return null;

        Material material = new Material(shader);
        material.name = $"K1L0Glass_{style.materialKey}";
        material.SetColor("_GlassTint", style.glassTint);
        material.SetFloat("_FillAlpha", style.fillAlpha);
        material.SetFloat("_BackdropInfluence", style.backdropInfluence);
        material.SetFloat("_LumaCompression", style.luminanceCompression);
        material.SetFloat("_Distortion", style.distortion);
        material.SetFloat("_CenterDarken", style.centerDarken);
        material.SetFloat("_EdgeLift", style.edgeLift);
        SharedMaterials[style.materialKey] = material;
        return material;
    }

    private static Image CreateStretchImage(Transform parent, string name, Sprite sprite, Color color, float inset)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);

        Image image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void SetOffsets(RectTransform rectTransform, Vector2 offsetMin, Vector2 offsetMax)
    {
        rectTransform.offsetMin = offsetMin;
        rectTransform.offsetMax = offsetMax;
    }

    private static Sprite CreateRoundedRectSprite(int size, int radius)
    {
        return CreateRoundedRectSprite(size, size, radius);
    }

    private static Sprite CreateRoundedRectSprite(int width, int height, int radius)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        int clampedRadius = Mathf.Min(radius, Mathf.Min(width, height) / 2 - 1);
        float r = clampedRadius;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float dx = 0f;
                float dy = 0f;

                if (x < r)
                    dx = r - x;
                else if (x > width - 1 - r)
                    dx = x - (width - 1 - r);

                if (y < r)
                    dy = r - y;
                else if (y > height - 1 - r)
                    dy = y - (height - 1 - r);

                if (dx > 0f && dy > 0f)
                {
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist < r - 1f)
                        tex.SetPixel(x, y, Color.white);
                    else if (dist < r)
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, r - dist));
                    else
                        tex.SetPixel(x, y, Color.clear);
                }
                else
                {
                    tex.SetPixel(x, y, Color.white);
                }
            }
        }

        tex.Apply();
        int border = GetSliceBorder(width, height, clampedRadius, 2);
        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
    }

    private static Sprite CreateRoundedBorderSprite(int size, int radius, int thickness)
    {
        return CreateRoundedBorderSprite(size, size, radius, thickness);
    }

    private static Sprite CreateRoundedBorderSprite(int width, int height, int radius, int thickness)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        int clampedRadius = Mathf.Min(radius, Mathf.Min(width, height) / 2 - 1);
        float r = clampedRadius;
        float t = thickness;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float px = Mathf.Abs(x - (width - 1) * 0.5f);
                float py = Mathf.Abs(y - (height - 1) * 0.5f);
                float halfW = (width - 1) * 0.5f;
                float halfH = (height - 1) * 0.5f;
                float qx = Mathf.Max(px - halfW + r, 0f);
                float qy = Mathf.Max(py - halfH + r, 0f);
                float dist = Mathf.Sqrt(qx * qx + qy * qy) - r;

                float outerEdge = 1f - Mathf.Clamp01(-dist);
                float innerEdge = Mathf.Clamp01(-(dist + t) + 1f);
                float alpha = outerEdge * innerEdge;

                if (dist > -1f && dist < 0f)
                    alpha *= -dist;

                float innerDist = -(dist + t);
                if (innerDist > -1f && innerDist < 0f)
                    alpha *= innerDist + 1f;

                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(alpha)));
            }
        }

        tex.Apply();
        int sliceBorder = GetSliceBorder(width, height, clampedRadius, thickness + 2);
        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(sliceBorder, sliceBorder, sliceBorder, sliceBorder));
    }

    private static int GetSliceBorder(int width, int height, int radius, int padding)
    {
        int minDimension = Mathf.Min(width, height);
        int maxReasonableBorder = Mathf.Max(4, Mathf.FloorToInt(minDimension * 0.15f));
        int roundedBorder = Mathf.RoundToInt(radius * 0.32f) + padding;
        return Mathf.Clamp(roundedBorder, 4, maxReasonableBorder);
    }
}

public class K1L0ModalStaticEffect : MonoBehaviour
{
    private RawImage image;
    private Texture2D texture;
    private Color32[] pixels;
    private float nextTick;

    public void Configure(RawImage target)
    {
        image = target;
        if (texture == null)
        {
            texture = new Texture2D(96, 96, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Repeat;
            pixels = new Color32[96 * 96];
        }
        image.texture = texture;
        WriteNoise();
    }

    private void OnEnable()
    {
        if (image == null) image = GetComponent<RawImage>();
        if (image != null && texture == null) Configure(image);
        nextTick = 0f;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextTick) return;
        nextTick = Time.unscaledTime + 0.075f;
        WriteNoise();
        if (image != null)
        {
            image.color = new Color(0.47f, 1f, 0.54f, Random.Range(0.028f, 0.070f));
            image.uvRect = new Rect(Random.value, Random.value, 1f + Random.value * 0.08f, 1f);
        }
    }

    private void WriteNoise()
    {
        if (texture == null || pixels == null) return;
        for (int i = 0; i < pixels.Length; i++)
        {
            byte v = (byte)Random.Range(80, 255);
            byte a = (byte)Random.Range(12, 58);
            pixels[i] = new Color32(v, v, v, a);
        }
        texture.SetPixels32(pixels);
        texture.Apply(false);
    }
}
