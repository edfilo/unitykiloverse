using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// City/weather text at top-left of HUD. Uses K1L0CanvasRoot.HUD.
/// </summary>
public class CityWeatherOverlay : MonoBehaviour
{
    public static CityWeatherOverlay Instance { get; private set; }

    private TextMeshProUGUI label;
    private Text iconLabel;
    private static Font weatherGlyphFont;

    public static void Show(string city, string weather)
    {
        Show(city, weather, null);
    }

    public static void Show(string city, string weather, string glyphKey)
    {
        EnsureExists();
        if (Instance != null)
            Instance.Apply(city, weather, glyphKey);
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
        var textGO = new GameObject("CityWeatherLabel");
        textGO.transform.SetParent(K1L0CanvasRoot.HUD, false);

        var rt = textGO.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(12, -6);
        rt.sizeDelta = new Vector2(-144, 20);
        K1L0HudLayoutController.RegisterTopElement(rt, "CityWeatherLabel", 0, 20f, 20f);

        var layout = textGO.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 4f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = false;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;

        var textChild = new GameObject("CityWeatherText");
        textChild.transform.SetParent(textGO.transform, false);
        label = textChild.AddComponent<TextMeshProUGUI>();

        var font = Resources.Load<TMP_FontAsset>("Fonts/IBMPlexMono-Light SDF");
        if (font == null) font = Resources.Load<TMP_FontAsset>("Fonts/IBMPlexMono-Regular SDF");
        if (font == null) font = TMP_Settings.defaultFontAsset;
        label.font = font;

        label.fontSize = 12;
        label.color = new Color(0.75f, 0.85f, 1f, 0.85f);
        label.alignment = TextAlignmentOptions.TopLeft;
        label.text = "";
        label.raycastTarget = false;
        var labelLayout = textChild.AddComponent<LayoutElement>();
        labelLayout.flexibleWidth = 0f;
        labelLayout.preferredHeight = 20f;

        var iconChild = new GameObject("WeatherIcon");
        iconChild.transform.SetParent(textGO.transform, false);
        iconLabel = iconChild.AddComponent<Text>();
        iconLabel.font = LoadWeatherGlyphFont();
        iconLabel.fontSize = 13;
        iconLabel.color = label.color;
        iconLabel.alignment = TextAnchor.UpperLeft;
        iconLabel.text = "";
        iconLabel.raycastTarget = false;
        iconLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
        iconLabel.verticalOverflow = VerticalWrapMode.Overflow;
        var iconLayout = iconChild.AddComponent<LayoutElement>();
        iconLayout.preferredWidth = 18f;
        iconLayout.preferredHeight = 20f;

        Debug.Log("[CityWeatherOverlay] Created on K1L0CanvasRoot.HUD");
    }

    void Apply(string city, string weather, string glyphKey)
    {
        if (label == null) return;

        string display = "";
        if (!string.IsNullOrEmpty(city)) display = city;
        string weatherText = FormatWeatherText(weather);
        if (!string.IsNullOrEmpty(weatherText))
            display = string.IsNullOrEmpty(display) ? weatherText : $"{display}  {weatherText}";

        if (!string.IsNullOrEmpty(display))
            label.text = display.ToUpper();

        if (iconLabel != null)
        {
            string key = !string.IsNullOrWhiteSpace(glyphKey) ? glyphKey : weather;
            iconLabel.text = string.IsNullOrWhiteSpace(key) ? "" : WeatherGlyph(key);
        }
    }

    private static Font LoadWeatherGlyphFont()
    {
        if (weatherGlyphFont != null) return weatherGlyphFont;
        try
        {
            weatherGlyphFont = Font.CreateDynamicFontFromOSFont(
                new[] { "Apple Color Emoji", "SF Pro Text", "Helvetica Neue", "Arial" },
                24);
        }
        catch
        {
            weatherGlyphFont = null;
        }
        return weatherGlyphFont != null ? weatherGlyphFont : Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    private static string WeatherGlyph(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return "";
        switch (key.Trim().ToLowerInvariant())
        {
            case "sun": case "sunny": case "clear": case "01d": case "01n":
                return "☀";
            case "cloud": case "cloudy": case "overcast": case "03d": case "03n": case "04d": case "04n":
                return "☁";
            case "partly cloudy": case "partlycloudy": case "02d": case "02n":
                return "⛅";
            case "rain": case "rainy": case "drizzle": case "09d": case "09n": case "10d": case "10n":
                return "☔";
            case "snow": case "snowy": case "13d": case "13n":
                return "❄";
            case "storm": case "thunder": case "thunderstorm": case "11d": case "11n":
                return "⚡";
            case "fog": case "foggy": case "mist": case "50d": case "50n":
                return "≋";
            case "wind": case "windy":
                return "↝";
            default:
                return "☀";
        }
    }

    private static string FormatWeatherText(string weather)
    {
        if (string.IsNullOrWhiteSpace(weather)) return "";
        string value = weather.Trim();
        int degree = value.IndexOf('°');
        if (degree >= 0)
        {
            int end = degree + 1;
            if (end < value.Length && (value[end] == 'F' || value[end] == 'C' || value[end] == 'f' || value[end] == 'c'))
                end++;
            return value.Substring(0, end).Trim();
        }
        return value;
    }
}
