using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays location (city) and weather at top-left with safe area offset
/// Auto-sizes height based on content using ContentSizeFitter
/// </summary>
public class WeatherView : MonoBehaviour
{
    [Header("UI Settings")]
    public Font customFont;
    public Color backgroundColor = new Color(0, 0, 0, 0.4f); // Semi-transparent black
    public Color textColor = Color.white;
    public int fontSize = 36;

    private GameObject containerObj;
    private Text locationText;
    private Text displayText;
    private RectTransform containerRect;
    private string lastLocation = "";
    private string lastWeather = "";

    void Start()
    {
        // Updated width to 900px
        CreateUI();
    }

    void CreateUI()
    {
        // Create container with background
        containerObj = new GameObject("WeatherContainer");
        containerObj.transform.SetParent(transform, false);
        containerObj.layer = 5; // UI Layer

        Image bg = containerObj.AddComponent<Image>();
        bg.color = backgroundColor;
        bg.raycastTarget = false;

        containerRect = containerObj.GetComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0, 1); // Top-left
        containerRect.anchorMax = new Vector2(0, 1);
        containerRect.pivot = new Vector2(0, 1);
        containerRect.sizeDelta = new Vector2(900, fontSize + 24); // Single-line height

        // Add VerticalLayoutGroup to keep container sizing consistent
        VerticalLayoutGroup vlg = containerObj.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(10, 10, 6, 6);
        vlg.childAlignment = TextAnchor.MiddleLeft;
        vlg.childControlHeight = true;
        vlg.childControlWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = false;

        Debug.Log($"[WeatherView] Container created: size={containerRect.sizeDelta}, bgColor={backgroundColor}, layer={containerObj.layer}");

        ContentSizeFitter fitter = containerObj.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        // Visible single-line display (city + weather)
        GameObject displayObj = new GameObject("LocationWeatherText");
        displayObj.transform.SetParent(containerObj.transform, false);
        displayText = displayObj.AddComponent<Text>();
        displayText.font = customFont ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        displayText.fontSize = fontSize;
        displayText.color = textColor;
        displayText.fontStyle = FontStyle.Bold;
        displayText.alignment = TextAnchor.MiddleLeft;
        displayText.text = "Finding location...";
        displayText.horizontalOverflow = HorizontalWrapMode.Overflow;
        displayText.verticalOverflow = VerticalWrapMode.Truncate;

        LayoutElement displayLE = displayObj.AddComponent<LayoutElement>();
        displayLE.preferredHeight = fontSize + 6;
        displayLE.flexibleWidth = 1f;

        Debug.Log($"[WeatherView] DisplayText created: fontSize={fontSize}, color={textColor}, text='{displayText.text}'");

        // Hidden location text for LocationLabelUI updates
        GameObject locationObj = new GameObject("LocationText");
        locationObj.transform.SetParent(transform, false);
        locationText = locationObj.AddComponent<Text>();
        locationText.font = customFont ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        locationText.fontSize = fontSize;
        locationText.color = new Color(0f, 0f, 0f, 0f);
        locationText.alignment = TextAnchor.MiddleLeft;
        locationText.text = "";
        locationText.horizontalOverflow = HorizontalWrapMode.Overflow;
        locationText.verticalOverflow = VerticalWrapMode.Truncate;
        locationText.raycastTarget = false;

        // Set up LocationLabelUI to update location text
        LocationLabelUI locationUI = gameObject.AddComponent<LocationLabelUI>();
        locationUI.locationText = locationText;

        Debug.Log("[WeatherView] Created weather view with auto-height");
    }

    public void UpdateWeather(string icon, string glyph, float tempF)
    {
        string weatherIcon = GetWeatherIcon(glyph);
        lastWeather = $"{Mathf.RoundToInt(tempF)}°F {weatherIcon}";
        UpdateCombinedText();
    }

    private string GetWeatherIcon(string glyph)
    {
        switch (glyph.ToLower())
        {
            case "sun":
            case "sunny":
            case "clear":
                return "☀";
            case "moon":
            case "night":
            case "clear-night":
                return "☾";
            case "cloud":
            case "cloudy":
            case "overcast":
                return "☁";
            case "partly cloudy":
            case "partlycloudy":
                return "⛅";
            case "rain":
            case "rainy":
            case "drizzle":
                return "🌧";
            case "snow":
            case "snowy":
                return "❄";
            case "storm":
            case "thunder":
            case "thunderstorm":
                return "⛈";
            case "fog":
            case "foggy":
            case "mist":
                return "🌫";
            case "wind":
            case "windy":
                return "🌬";
            default:
                return glyph;
        }
    }

    public void ApplySafeAreaOffset()
    {
        // Position is managed by UILayoutManager - do nothing here
        // containerRect position is relative to parent (WeatherView)
        if (containerRect == null) return;
        containerRect.anchoredPosition = Vector2.zero;

        RectTransform rootRect = GetComponent<RectTransform>();
        if (rootRect != null)
        {
            Debug.Log($"[WeatherView] ApplySafeAreaOffset called - Root position: {rootRect.anchoredPosition}, Container: {containerRect.anchoredPosition}");
        }
    }

    private static bool _loggedFirstUpdateAfterBoot;
    private void Update()
    {
        if (BootState.AllowPlayer && !_loggedFirstUpdateAfterBoot)
        {
            _loggedFirstUpdateAfterBoot = true;
            BootDiagnostics.Mark("WeatherView first Update after AllowPlayer");
        }
        // Editor: skip Update work for 2s after AllowPlayer (pile-up after broken-up boot)
        if (Application.isEditor && BootState.AllowPlayer && (Time.realtimeSinceStartup - BootState.AllowPlayerTime) < 2f)
            return;
        if (locationText == null) return;
        if (lastLocation != locationText.text)
        {
            lastLocation = locationText.text ?? "";
            UpdateCombinedText();
        }
    }

    /// <summary>Returns the current combined location/weather display text.</summary>
    public string GetDisplayText()
    {
        return displayText != null ? displayText.text : "";
    }

    private void UpdateCombinedText()
    {
        if (displayText == null) return;
        string location = string.IsNullOrWhiteSpace(lastLocation) ? "" : lastLocation.Trim();
        string weather = string.IsNullOrWhiteSpace(lastWeather) ? "" : lastWeather.Trim();
        
        string final = "Finding location...";
        if (!string.IsNullOrEmpty(location) && !string.IsNullOrEmpty(weather))
        {
            final = $"{location} · {weather}";
        }
        else if (!string.IsNullOrEmpty(location))
        {
            final = location;
        }
        else if (!string.IsNullOrEmpty(weather))
        {
            final = weather;
        }
        
        displayText.text = final;
        Debug.Log($"[WeatherView] Text updated: '{final}' (Loc='{location}', Wx='{weather}')");
    }
}
