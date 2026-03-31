using UnityEngine;
using TMPro;

/// <summary>
/// Material Design Icons helper. Loads a subsetted MDI TTF from Resources
/// and provides Unicode constants + a runtime TMP_FontAsset.
/// Codepoints are in Supplementary PUA (above U+FFFF) so we use strings (surrogate pairs).
/// </summary>
public static class MDIIcons
{
    // -- Category icons (strings because codepoints > U+FFFF) --
    public static readonly string Grid       = char.ConvertFromUtf32(0xF0570);  // view-grid (ALL)
    public static readonly string Coffee     = char.ConvertFromUtf32(0xF0176);  // coffee
    public static readonly string Cocktail   = char.ConvertFromUtf32(0xF0356);  // glass-cocktail
    public static readonly string ForkKnife  = char.ConvertFromUtf32(0xF05F2);  // food-fork-drink
    public static readonly string GasStation = char.ConvertFromUtf32(0xF0298);  // gas-station

    // -- Weather icons --
    public static readonly string Sunny        = char.ConvertFromUtf32(0xF0599);  // weather-sunny
    public static readonly string Cloudy       = char.ConvertFromUtf32(0xF0590);  // weather-cloudy
    public static readonly string PartlyCloudy = char.ConvertFromUtf32(0xF0595);  // weather-partly-cloudy
    public static readonly string Rainy        = char.ConvertFromUtf32(0xF0597);  // weather-rainy
    public static readonly string Snowy        = char.ConvertFromUtf32(0xF0598);  // weather-snowy
    public static readonly string Lightning    = char.ConvertFromUtf32(0xF0593);  // weather-lightning
    public static readonly string Fog          = char.ConvertFromUtf32(0xF0591);  // weather-fog
    public static readonly string Windy        = char.ConvertFromUtf32(0xF059D);  // weather-windy

    private static TMP_FontAsset _fontAsset;

    /// <summary>
    /// Returns a TMP_FontAsset created at runtime from the MDI TTF.
    /// Cached after first call.
    /// </summary>
    public static TMP_FontAsset GetFontAsset()
    {
        if (_fontAsset != null) return _fontAsset;

        Font font = Resources.Load<Font>("Fonts/MDIIcons");
        if (font == null)
        {
            Debug.LogWarning("[MDIIcons] Could not load Fonts/MDIIcons from Resources");
            return null;
        }

        _fontAsset = TMP_FontAsset.CreateFontAsset(font);
        if (_fontAsset != null)
        {
            _fontAsset.name = "MDIIcons SDF";
            Debug.Log("[MDIIcons] Created runtime TMP_FontAsset from MDIIcons.ttf");
        }

        return _fontAsset;
    }

    /// <summary>
    /// Map a weather glyph key (from WeatherView) to the MDI weather icon string.
    /// </summary>
    public static string WeatherIcon(string glyphKey)
    {
        if (string.IsNullOrEmpty(glyphKey)) return Sunny;
        switch (glyphKey.ToLower())
        {
            case "sun": case "sunny": case "clear":
                return Sunny;
            case "cloud": case "cloudy": case "overcast":
                return Cloudy;
            case "partly cloudy": case "partlycloudy":
                return PartlyCloudy;
            case "rain": case "rainy": case "drizzle":
                return Rainy;
            case "snow": case "snowy":
                return Snowy;
            case "storm": case "thunder": case "thunderstorm":
                return Lightning;
            case "fog": case "foggy": case "mist":
                return Fog;
            case "wind": case "windy":
                return Windy;
            default:
                return Sunny;
        }
    }

    /// <summary>
    /// Map a category filter key to the MDI category icon string.
    /// </summary>
    public static string CategoryIcon(string category)
    {
        switch (category)
        {
            case "coffee":      return Coffee;
            case "bar":         return Cocktail;
            case "food":        return ForkKnife;
            case "convenience": return GasStation;
            default:            return Grid;
        }
    }
}
