using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Steps hero widget (lower-right): live step count in a large hero font, with the
/// 24-hour and 7-day totals centered in a single row beneath it.
/// </summary>
public class MovementPanel : MonoBehaviour
{
    private Text heroValue;
    private TextMeshProUGUI subValue;
    private PedometerService pedometerService;
    private bool built;

    static TMP_FontAsset _interLight;
    static Font _cleanSans;
    static TMP_FontAsset LoadInterLight()
    {
        if (_interLight == null)
            _interLight = Resources.Load<TMP_FontAsset>("Fonts/Inter-Light SDF");
        return _interLight;
    }

    static Font LoadCleanSans()
    {
        if (_cleanSans != null) return _cleanSans;

        try
        {
            _cleanSans = Font.CreateDynamicFontFromOSFont(
                new[] { "SF Pro Display", "SF Pro Text", "Helvetica Neue", "Helvetica", "Arial" },
                96);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[MovementPanel] Failed to create clean sans font: {e.Message}");
        }

        return _cleanSans != null ? _cleanSans : Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    void Start()
    {
        pedometerService = FindFirstObjectByType<PedometerService>();
        EnsureUI();
        InvokeRepeating(nameof(UpdateMetrics), 0.2f, 1f); // 1s cadence for a live feel
    }

    void EnsureUI()
    {
        if (built) return;
        built = true;

        RectTransform root = GetComponent<RectTransform>();
        if (root == null) root = gameObject.AddComponent<RectTransform>();

        const float panelWidth = 280f;
        const float panelHeight = 116f;
        root.sizeDelta = new Vector2(panelWidth, panelHeight);

        Image bg = gameObject.AddComponent<Image>();
        bg.color = new Color(0, 0, 0, 0.4f);
        bg.raycastTarget = false;

        var font = LoadInterLight() ?? TMP_Settings.defaultFontAsset;

        // Hero: live step count, large + centered, fills the upper portion.
        var heroObj = new GameObject("HeroSteps");
        heroObj.transform.SetParent(transform, false);
        heroValue = heroObj.AddComponent<Text>();
        heroValue.font = LoadCleanSans();
        heroValue.fontSize = 58;
        heroValue.fontStyle = FontStyle.Normal;
        heroValue.color = Color.white;
        heroValue.text = "--";
        heroValue.alignment = TextAnchor.MiddleCenter;
        heroValue.horizontalOverflow = HorizontalWrapMode.Overflow;
        heroValue.verticalOverflow = VerticalWrapMode.Overflow;
        heroValue.raycastTarget = false;
        var hr = heroObj.GetComponent<RectTransform>();
        hr.anchorMin = new Vector2(0, 0.34f);
        hr.anchorMax = new Vector2(1, 1f);
        hr.offsetMin = Vector2.zero;
        hr.offsetMax = Vector2.zero;

        // Sub row: 24H and 7D totals, centered on a single line beneath the hero.
        var subObj = new GameObject("SubStats");
        subObj.transform.SetParent(transform, false);
        subValue = subObj.AddComponent<TextMeshProUGUI>();
        subValue.font = font;
        subValue.fontSize = 17f;
        subValue.color = new Color(0.72f, 0.72f, 0.72f, 1f);
        subValue.text = "24H --   ·   7D --";
        subValue.alignment = TextAlignmentOptions.Center;
        subValue.enableWordWrapping = false;
        subValue.overflowMode = TextOverflowModes.Overflow;
        var sr = subObj.GetComponent<RectTransform>();
        sr.anchorMin = new Vector2(0, 0f);
        sr.anchorMax = new Vector2(1, 0.33f);
        sr.offsetMin = new Vector2(0, 6f);
        sr.offsetMax = Vector2.zero;
    }

    void UpdateMetrics()
    {
        if (pedometerService == null)
        {
            pedometerService = FindFirstObjectByType<PedometerService>();
            if (pedometerService == null) return;
        }

        // Hero: live session step count. Fall back to today's 24h total when the
        // session count is still 0 so the hero is never a lonely "0".
        int live = pedometerService.stepCount;
        if (live <= 0 && pedometerService.stepsLast24Hours > 0)
            live = pedometerService.stepsLast24Hours;
        if (heroValue != null)
            heroValue.text = K1L0StepFormatter.Value(live);

        if (subValue != null)
            subValue.text = $"24H {Fmt(pedometerService.stepsLast24Hours)}   ·   7D {Fmt(pedometerService.stepsLast7Days)}";
    }

    static string Fmt(int steps) => steps >= 0 ? K1L0StepFormatter.Value(steps) : "--";
}
