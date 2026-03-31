using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Top-right status panel with 4 metrics: Movement, Hydration, Listeners, Dust
/// Movement and Hydration have progress bars; Listeners and Dust show values.
/// </summary>
public class MovementPanel : MonoBehaviour
{
    private const int MOVEMENT_GOAL = 15000;
    private const int HYDRATION_GOAL = 8; // glasses of water

    private Image movementBarFill;
    private Image hydrationBarFill;
    private TextMeshProUGUI movementValue;
    private TextMeshProUGUI hydrationValue;
    private TextMeshProUGUI listenersValue;
    private TextMeshProUGUI dustValue;
    private PedometerService pedometerService;
    private bool built;

    // Simulated values
    private int listeners;
    private float dust;

    static TMP_FontAsset _interLight;
    static TMP_FontAsset LoadInterLight()
    {
        if (_interLight == null)
            _interLight = Resources.Load<TMP_FontAsset>("Fonts/Inter-Light SDF");
        return _interLight;
    }

    void Start()
    {
        pedometerService = FindFirstObjectByType<PedometerService>();

        // Simulated values
        var rng = new System.Random(System.DateTime.Now.DayOfYear);
        listeners = rng.Next(12, 847);
        dust = rng.Next(10, 100) / 10f;

        EnsureUI();
        InvokeRepeating(nameof(UpdateMetrics), 0.5f, 2f);
    }

    void EnsureUI()
    {
        if (built) return;
        built = true;

        RectTransform root = GetComponent<RectTransform>();
        if (root == null) root = gameObject.AddComponent<RectTransform>();

        float rowHeight = 42f;
        float panelWidth = 260f;
        float panelHeight = rowHeight * 4 + 16f; // 4 rows + padding
        root.sizeDelta = new Vector2(panelWidth, panelHeight);

        Image bg = gameObject.AddComponent<Image>();
        bg.color = new Color(0, 0, 0, 0.4f);
        bg.raycastTarget = false;

        float y = -8f;
        var font = LoadInterLight() ?? TMP_Settings.defaultFontAsset;

        // Row 1: Movement (progress bar)
        movementValue = CreateRow(font, "Movement", ref y, rowHeight, out movementBarFill,
            new Color(0.2f, 0.8f, 1f, 0.9f));

        // Row 2: Hydration (progress bar)
        hydrationValue = CreateRow(font, "Hydration", ref y, rowHeight, out hydrationBarFill,
            new Color(0.3f, 0.9f, 0.5f, 0.9f));

        // Row 3: Listeners (value only)
        listenersValue = CreateRow(font, "Listeners", ref y, rowHeight, out _, default);

        // Row 4: Dust (value only)
        dustValue = CreateRow(font, "Dust", ref y, rowHeight, out _, default);
    }

    TextMeshProUGUI CreateRow(TMP_FontAsset font, string label, ref float y, float rowHeight,
        out Image barFillOut, Color barColor)
    {
        barFillOut = null;
        float labelFontSize = 19f;
        float valueFontSize = 16f;
        bool hasBar = barColor.a > 0f;

        // Label (left)
        var labelObj = new GameObject(label + "Label");
        labelObj.transform.SetParent(transform, false);
        var labelTmp = labelObj.AddComponent<TextMeshProUGUI>();
        labelTmp.font = font;
        labelTmp.fontSize = labelFontSize;
        labelTmp.color = Color.white;
        labelTmp.text = label;
        labelTmp.alignment = TextAlignmentOptions.TopLeft;
        var lr = labelObj.GetComponent<RectTransform>();
        lr.anchorMin = new Vector2(0, 1);
        lr.anchorMax = new Vector2(0.5f, 1);
        lr.pivot = new Vector2(0, 1);
        lr.anchoredPosition = new Vector2(12, y);
        lr.sizeDelta = new Vector2(0, hasBar ? 20f : rowHeight);

        // Value (right)
        var valObj = new GameObject(label + "Value");
        valObj.transform.SetParent(transform, false);
        var valTmp = valObj.AddComponent<TextMeshProUGUI>();
        valTmp.font = font;
        valTmp.fontSize = valueFontSize;
        valTmp.color = new Color(0.7f, 0.7f, 0.7f, 1f);
        valTmp.text = "--";
        valTmp.alignment = TextAlignmentOptions.TopRight;
        var vr = valObj.GetComponent<RectTransform>();
        vr.anchorMin = new Vector2(0.5f, 1);
        vr.anchorMax = new Vector2(1, 1);
        vr.pivot = new Vector2(1, 1);
        vr.anchoredPosition = new Vector2(-12, y);
        vr.sizeDelta = new Vector2(0, hasBar ? 20f : rowHeight);

        if (hasBar)
        {
            // Bar background
            var barBgObj = new GameObject(label + "BarBg");
            barBgObj.transform.SetParent(transform, false);
            var barBg = barBgObj.AddComponent<Image>();
            barBg.color = new Color(1, 1, 1, 0.1f);
            barBg.raycastTarget = false;
            var bgR = barBgObj.GetComponent<RectTransform>();
            bgR.anchorMin = new Vector2(0, 1);
            bgR.anchorMax = new Vector2(1, 1);
            bgR.pivot = new Vector2(0, 1);
            bgR.anchoredPosition = new Vector2(12, y - 22f);
            bgR.sizeDelta = new Vector2(-24, 12f);

            // Bar fill
            var barObj = new GameObject(label + "BarFill");
            barObj.transform.SetParent(barBgObj.transform, false);
            var fill = barObj.AddComponent<Image>();
            fill.color = barColor;
            fill.raycastTarget = false;
            var fr = barObj.GetComponent<RectTransform>();
            fr.anchorMin = Vector2.zero;
            fr.anchorMax = new Vector2(0, 1);
            fr.offsetMin = Vector2.zero;
            fr.offsetMax = Vector2.zero;
            barFillOut = fill;
        }

        y -= rowHeight;
        return valTmp;
    }

    void UpdateMetrics()
    {
        if (pedometerService == null)
        {
            pedometerService = FindFirstObjectByType<PedometerService>();
            if (pedometerService == null) return;
        }

        // Movement
        int steps = pedometerService.stepsLast48Hours;
        if (steps < 0) steps = pedometerService.stepsLast24Hours;
        if (steps < 0) steps = 0;
        float moveRatio = Mathf.Clamp01((float)steps / MOVEMENT_GOAL);
        if (movementValue != null)
            movementValue.text = $"{steps:N0} / {MOVEMENT_GOAL:N0}";
        if (movementBarFill != null)
            movementBarFill.GetComponent<RectTransform>().anchorMax = new Vector2(moveRatio, 1);

        // Hydration (simulated: random 2-7 glasses)
        int glasses = new System.Random(System.DateTime.Now.Hour + System.DateTime.Now.DayOfYear).Next(2, 7);
        float hydRatio = Mathf.Clamp01((float)glasses / HYDRATION_GOAL);
        if (hydrationValue != null)
            hydrationValue.text = $"{glasses} / {HYDRATION_GOAL}";
        if (hydrationBarFill != null)
            hydrationBarFill.GetComponent<RectTransform>().anchorMax = new Vector2(hydRatio, 1);

        // Listeners
        if (listenersValue != null)
            listenersValue.text = $"{listeners:N0}";

        // Dust
        if (dustValue != null)
            dustValue.text = $"{dust:F1} oz";
    }
}
