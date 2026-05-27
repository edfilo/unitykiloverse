using UnityEngine;
using TMPro;

/// <summary>
/// City/weather text at top-left of HUD. Uses K1L0CanvasRoot.HUD.
/// </summary>
public class CityWeatherOverlay : MonoBehaviour
{
    public static CityWeatherOverlay Instance { get; private set; }

    private TextMeshProUGUI label;

    public static void Show(string city, string weather)
    {
        EnsureExists();
        if (Instance != null)
            Instance.Apply(city, weather);
    }

    static void EnsureExists()
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
        // Parent to the shared HUD safe area — no private canvas
        var textGO = new GameObject("CityWeatherLabel");
        textGO.transform.SetParent(K1L0CanvasRoot.HUD, false);

        var rt = textGO.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(12, -6);
        rt.sizeDelta = new Vector2(-144, 20);
        K1L0HudLayoutController.RegisterTopElement(rt, "CityWeatherLabel", 0, 20f, 20f);

        label = textGO.AddComponent<TextMeshProUGUI>();

        var font = Resources.Load<TMP_FontAsset>("Fonts/IBMPlexMono-Light SDF");
        if (font == null) font = Resources.Load<TMP_FontAsset>("Fonts/IBMPlexMono-Regular SDF");
        if (font == null) font = TMP_Settings.defaultFontAsset;
        label.font = font;

        label.fontSize = 12;
        label.color = new Color(0.75f, 0.85f, 1f, 0.85f);
        label.alignment = TextAlignmentOptions.TopLeft;
        label.text = "";
        label.raycastTarget = false;

        Debug.Log("[CityWeatherOverlay] Created on K1L0CanvasRoot.HUD");
    }

    void Apply(string city, string weather)
    {
        if (label == null) return;

        string display = "";
        if (!string.IsNullOrEmpty(city)) display = city;
        if (!string.IsNullOrEmpty(weather))
            display = string.IsNullOrEmpty(display) ? weather : $"{display}  {weather}";

        if (!string.IsNullOrEmpty(display))
            label.text = display.ToUpper();
    }
}
