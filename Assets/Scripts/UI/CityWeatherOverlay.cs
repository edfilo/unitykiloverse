using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Standalone persistent city/weather overlay at top of screen.
/// Bypasses K1L0HUD canvas (which gets destroyed). Same DontDestroyOnLoad
/// pattern as POILabelBridge.
/// </summary>
public class CityWeatherOverlay : MonoBehaviour
{
    public static CityWeatherOverlay Instance { get; private set; }

    private TextMeshProUGUI label;
    private string pendingCity;
    private string pendingWeather;

    public static void Show(string city, string weather)
    {
        EnsureExists();
        if (Instance != null)
            Instance.Apply(city, weather);
    }

    public static void EnsureExists()
    {
        if (Instance != null) return;

        var go = new GameObject("CityWeatherOverlay");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<CityWeatherOverlay>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        CreateUI();
    }

    void CreateUI()
    {
        // Canvas
        var canvasGO = new GameObject("CWO_Canvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 6000;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(390, 844);
        scaler.matchWidthOrHeight = 1f;

        // Safe area container
        var safeGO = new GameObject("SafeArea");
        safeGO.transform.SetParent(canvasGO.transform, false);
        var safeRT = safeGO.AddComponent<RectTransform>();

        Rect safe = Screen.safeArea;
        Vector2 anchorMin = safe.position / new Vector2(Screen.width, Screen.height);
        Vector2 anchorMax = (safe.position + safe.size) / new Vector2(Screen.width, Screen.height);
        safeRT.anchorMin = anchorMin;
        safeRT.anchorMax = anchorMax;
        safeRT.offsetMin = Vector2.zero;
        safeRT.offsetMax = Vector2.zero;

        // Text label at top-left of safe area
        var textGO = new GameObject("CityWeatherLabel");
        textGO.transform.SetParent(safeRT, false);
        var rt = textGO.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(12, -6);
        rt.sizeDelta = new Vector2(-24, 22);

        label = textGO.AddComponent<TextMeshProUGUI>();

        // Try to load the same font K1L0HUD uses
        var font = Resources.Load<TMP_FontAsset>("Fonts/IBMPlexMono-Light SDF");
        if (font == null) font = Resources.Load<TMP_FontAsset>("Fonts/IBMPlexMono-Regular SDF");
        if (font == null) font = Resources.Load<TMP_FontAsset>("Fonts/Inter-Regular SDF");
        if (font == null) font = TMP_Settings.defaultFontAsset;
        label.font = font;

        label.fontSize = 15;
        label.color = new Color(0.75f, 0.85f, 1f, 0.85f);
        label.alignment = TextAlignmentOptions.TopLeft;
        label.text = "";

        Debug.Log("[CityWeatherOverlay] UI created");
    }

    void Apply(string city, string weather)
    {
        if (label == null)
        {
            Debug.LogWarning("[CityWeatherOverlay] label is null, cannot apply");
            return;
        }

        string display = "";
        if (!string.IsNullOrEmpty(city)) display = city;
        if (!string.IsNullOrEmpty(weather))
            display = string.IsNullOrEmpty(display) ? weather : $"{display}  {weather}";

        if (!string.IsNullOrEmpty(display))
        {
            label.text = display.ToUpper();
            Debug.Log($"[CityWeatherOverlay] Set text: '{label.text}'");
        }
    }
}
