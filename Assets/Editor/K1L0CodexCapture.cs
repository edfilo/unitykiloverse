#if UNITY_EDITOR
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class K1L0CodexCapture
{
    private const string OutputPath = "/tmp/k1l0_codex.png";
    private const string ClaudeOutputPath = "/tmp/k1l0_claude.png";

    // Batchmode entrypoint: render both orb styles to PNGs.
    public static void CaptureAll()
    {
        Capture();
        CaptureStyle("Claude", ClaudeOutputPath);
    }

    [MenuItem("Tools/K1L0 Capture Codex Screen")]
    public static void Capture()
    {
        CaptureStyle("Codex", OutputPath);
    }

    [MenuItem("Tools/K1L0 Capture Claude Spark")]
    public static void CaptureClaude()
    {
        CaptureStyle("Claude", ClaudeOutputPath);
    }

    private static void CaptureStyle(string styleName, string outputPath)
    {
        GameObject canvasGO = new GameObject("K1L0CodexCaptureCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        GameObject screenGO = new GameObject("K1L0CodexCaptureScreen");
        K1L0CodexMode mode = screenGO.AddComponent<K1L0CodexMode>();
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts/IBMPlexMono-Regular SDF") ?? TMP_Settings.defaultFontAsset;

        // Set the style BEFORE Initialize so only the chosen orb is ever built.
        // (In edit-mode batchmode Destroy() is deferred, so switching styles after
        // Initialize would leave both orbs in the scene.)
        SetStyleByName(mode, styleName);
        mode.Initialize((RectTransform)canvasGO.transform, font);
        mode.RotateCollectible(new Vector2(58f, -34f));

        Transform collectible = GetPrivateField<Transform>(mode, "collectible");
        ParticleSystem[] particles = collectible != null
            ? collectible.GetComponentsInChildren<ParticleSystem>(true)
            : screenGO.GetComponentsInChildren<ParticleSystem>(true);
        foreach (ParticleSystem particleSystem in particles)
            particleSystem.Simulate(1.8f, true, true, true);

        Camera camera = GetPrivateField<Camera>(mode, "sphereCamera");
        RenderTexture source = GetPrivateField<RenderTexture>(mode, "renderTexture");
        if (camera == null || source == null)
        {
            Debug.LogError("[K1L0CodexCapture] Missing codex camera/render texture.");
            Object.DestroyImmediate(canvasGO);
            return;
        }

        camera.Render();
        RenderTexture target = RenderTexture.GetTemporary(300, 300, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(source, target);

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        Texture2D png = new Texture2D(300, 300, TextureFormat.RGBA32, false);
        png.ReadPixels(new Rect(0, 0, 300, 300), 0, 0);
        png.Apply();
        File.WriteAllBytes(outputPath, png.EncodeToPNG());
        RenderTexture.active = previous;

        RenderTexture.ReleaseTemporary(target);
        Object.DestroyImmediate(png);

        // Full teardown: the orb/camera/post are separate scene roots (not children of
        // canvasGO) and OnDestroy uses deferred Destroy(), which no-ops in edit mode.
        // Destroy them immediately so the next capture starts from a clean scene.
        if (collectible != null) Object.DestroyImmediate(collectible.gameObject);
        if (camera != null) Object.DestroyImmediate(camera.gameObject);
        GameObject post = GameObject.Find("K1L0_CodexPost");
        if (post != null) Object.DestroyImmediate(post);
        if (source != null) source.Release();
        Object.DestroyImmediate(screenGO);
        Object.DestroyImmediate(canvasGO);

        Debug.Log($"[K1L0CodexCapture] Saved {outputPath}");
    }

    private static void SetStyleByName(K1L0CodexMode mode, string styleName)
    {
        System.Type modeType = mode.GetType();
        System.Type enumType = modeType.GetNestedType("OrbStyle", BindingFlags.NonPublic);
        FieldInfo styleField = modeType.GetField("currentStyle", BindingFlags.Instance | BindingFlags.NonPublic);
        if (enumType == null || styleField == null)
        {
            Debug.LogError("[K1L0CodexCapture] Could not reflect currentStyle/OrbStyle.");
            return;
        }
        styleField.SetValue(mode, System.Enum.Parse(enumType, styleName));
    }

    private static T GetPrivateField<T>(object target, string fieldName) where T : class
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        return field != null ? field.GetValue(target) as T : null;
    }
}
#endif
