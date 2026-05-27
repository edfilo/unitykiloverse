using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Single owner of all UI canvases. Created once, never destroyed.
/// Systems request a parent transform instead of creating their own canvas.
///
/// Usage:
///   var parent = K1L0CanvasRoot.HUD;   // for dock, panels, weather, controls
///   var parent = K1L0CanvasRoot.World;  // for POI labels, beams, 3D-anchored UI
///   var parent = K1L0CanvasRoot.Modal;  // for login, onboarding, stories, alerts
/// </summary>
public class K1L0CanvasRoot : MonoBehaviour
{
    private static K1L0CanvasRoot _instance;

    // Canvas GameObjects
    private Canvas _worldCanvas;
    private Canvas _hudCanvas;
    private Canvas _modalCanvas;

    // Safe area containers (anchored to screen safe area)
    private RectTransform _worldSafe;
    private RectTransform _hudSafe;
    private RectTransform _modalSafe;

    /// <summary>Parent for POI labels, beams, 3D-anchored overlays. sortingOrder=100</summary>
    public static RectTransform World => EnsureExists()._worldSafe;

    /// <summary>Parent for dock, panels, weather bar, controls. sortingOrder=1000</summary>
    public static RectTransform HUD => EnsureExists()._hudSafe;

    /// <summary>Parent for login, onboarding, stories viewer, alerts. sortingOrder=10000</summary>
    public static RectTransform Modal => EnsureExists()._modalSafe;

    /// <summary>Direct canvas access when needed (e.g. for sorting order tweaks)</summary>
    public static Canvas WorldCanvas => EnsureExists()._worldCanvas;
    public static Canvas HUDCanvas => EnsureExists()._hudCanvas;
    public static Canvas ModalCanvas => EnsureExists()._modalCanvas;

    static K1L0CanvasRoot EnsureExists()
    {
        if (_instance != null) return _instance;

        var go = new GameObject("[K1L0_UI]");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<K1L0CanvasRoot>();
        _instance.Build();
        Debug.Log("[K1L0CanvasRoot] Created UI root with 3 canvases");
        return _instance;
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Build()
    {
        _worldCanvas = CreateCanvas("K1L0_WorldCanvas", 100);
        _hudCanvas   = CreateCanvas("K1L0_HUDCanvas", 1000);
        _modalCanvas = CreateCanvas("K1L0_ModalCanvas", 10000);

        _worldSafe = CreateSafeArea(_worldCanvas);
        _hudSafe   = CreateSafeArea(_hudCanvas);
        _modalSafe = CreateSafeArea(_modalCanvas);
    }

    Canvas CreateCanvas(string name, int sortOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = sortOrder;

        var scaler = go.AddComponent<CanvasScaler>();
        // Aspect-aware: portrait phones use ScaleWithScreenSize (so UI
        // sizes feel right on a 6"-ish handset). Other aspects (Mac windows,
        // tablets, landscape) use ConstantPixelSize so the HUD doesn't
        // get distorted by extreme width-to-height ratios.
        float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
        bool portraitPhoneShape = aspect > 0.40f && aspect < 0.65f;
        if (portraitPhoneShape)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(390, 844);
            scaler.matchWidthOrHeight = 1f; // height-match — natural for phones
        }
        else
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f; // UI at literal pixels; OS handles Retina
        }

        go.AddComponent<GraphicRaycaster>();

        return canvas;
    }

    RectTransform CreateSafeArea(Canvas canvas)
    {
        var go = new GameObject("SafeArea");
        go.transform.SetParent(canvas.transform, false);
        var rt = go.AddComponent<RectTransform>();

        Rect safe = Screen.safeArea;
        Vector2 anchorMin = safe.position / new Vector2(Screen.width, Screen.height);
        Vector2 anchorMax = (safe.position + safe.size) / new Vector2(Screen.width, Screen.height);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        return rt;
    }
}
