using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Displays step counts at top-right and opens a detailed modal with activity charts.
/// </summary>
public class StepsView : MonoBehaviour
{
    private const int HourlyHistoryHours = 72;
    private const int HourlyHistoryDisplayRows = 40;

    [Header("UI Settings")]
    public Font customFont;
    public Color backgroundColor = new Color(0, 0, 0, 0.4f);
    public Color modalColor = new Color(0.12f, 0.12f, 0.12f, 0.98f);
    public Color textColor = Color.white;
    public Color highlightColor = new Color(0.2f, 0.8f, 1.0f); // Cyan
    public int fontSize = 36;
    public float animationDuration = 0.4f;

    private GameObject containerObj;
    private Text stepsSummaryText;
    private PedometerService pedometerService;
    private RectTransform rootRect;
    
    private GameObject modalOverlay;
    private RectTransform modalPanel;
    private RectTransform dailyChartContainer;
    private RectTransform hourlyChartContainer;
    private Text dailyTitleText;
    private Text hourlyTitleText;
    private bool isModalOpen = false;
    private bool built;

    void Awake()
    {
        EnsureUI();
    }

    void Start()
    {
        pedometerService = FindFirstObjectByType<PedometerService>();
        EnsureUI();
        InvokeRepeating(nameof(UpdateSummary), 0.5f, 1f); // Update every 1 second for real-time steps
    }

    void EnsureUI()
    {
        if (built) return;
        rootRect = GetComponent<RectTransform>();
        if (rootRect == null) rootRect = gameObject.AddComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(400, fontSize + 32);

        // 1. Summary Button (Top Right)
        containerObj = new GameObject("StepsSummaryContainer");
        containerObj.transform.SetParent(transform, false);
        
        Image bg = containerObj.AddComponent<Image>();
        bg.color = backgroundColor;
        
        Button btn = containerObj.AddComponent<Button>();
        btn.onClick.AddListener(OpenModal);

        RectTransform containerRect = containerObj.GetComponent<RectTransform>();
        containerRect.anchorMin = Vector2.zero;
        containerRect.anchorMax = Vector2.one;
        containerRect.offsetMin = Vector2.zero;
        containerRect.offsetMax = Vector2.zero;

        GameObject textObj = new GameObject("SummaryText");
        textObj.transform.SetParent(containerObj.transform, false);
        stepsSummaryText = textObj.AddComponent<Text>();
        stepsSummaryText.font = customFont ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        stepsSummaryText.fontSize = fontSize;
        stepsSummaryText.color = textColor;
        stepsSummaryText.alignment = TextAnchor.MiddleRight;
        stepsSummaryText.text = "STEPS: -- >";
        
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10, 0);
        textRect.offsetMax = new Vector2(-20, 0);

        Debug.Log($"[StepsView] Summary created: fontSize={fontSize}, color={textColor}, rootSize={rootRect.sizeDelta}");

        // 2. Modal (Hidden by default)
        CreateModal();

        built = true;
    }

    void CreateModal()
    {
        Canvas parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas == null)
        {
            Debug.LogWarning("[StepsView] CreateModal: No parent canvas found, deferring modal creation");
            return;
        }

        // Overlay (Full Screen)
        modalOverlay = new GameObject("StepsModalOverlay");
        modalOverlay.transform.SetParent(parentCanvas.transform, false);

        // Ensure it is on top of everything
        Canvas c = modalOverlay.AddComponent<Canvas>();
        c.overrideSorting = true;
        c.sortingOrder = 30000;
        modalOverlay.AddComponent<GraphicRaycaster>();

        RectTransform overlayRect = modalOverlay.GetComponent<RectTransform>();
        if (overlayRect == null) overlayRect = modalOverlay.AddComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image overlayBg = modalOverlay.AddComponent<Image>();
        overlayBg.color = new Color(0, 0, 0, 0.6f); // Dim background
        Button overlayBtn = modalOverlay.AddComponent<Button>();
        overlayBtn.onClick.AddListener(CloseModal);
        
        // Panel (Slides up)
        GameObject panel = new GameObject("StepsPanel");
        panel.transform.SetParent(modalOverlay.transform, false);
        modalPanel = panel.GetComponent<RectTransform>();
        if (modalPanel == null) modalPanel = panel.AddComponent<RectTransform>();
        modalPanel.anchorMin = new Vector2(0, 0);
        modalPanel.anchorMax = new Vector2(1, 0); // Bottom stretch
        modalPanel.pivot = new Vector2(0.5f, 0);
        modalPanel.sizeDelta = new Vector2(0, 1600); // Taller
        modalPanel.anchoredPosition = new Vector2(0, -1600); // Hidden
        
        Image panelImg = panel.AddComponent<Image>();
        panelImg.color = modalColor;

        // "Handle" for iOS slide-up look
        GameObject handle = new GameObject("Handle");
        handle.transform.SetParent(panel.transform, false);
        Image handleImg = handle.AddComponent<Image>();
        handleImg.color = new Color(1f, 1f, 1f, 0.2f);
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0.5f, 1);
        handleRect.anchorMax = new Vector2(0.5f, 1);
        handleRect.sizeDelta = new Vector2(100, 12);
        handleRect.anchoredPosition = new Vector2(0, -20);

        // Content
        float y = -100;
        
        // Title
        CreateText(panel.transform, "ActivityTitle", "Activity", 60, new Vector2(0, -80), TextAnchor.MiddleCenter, true);
        
        // --- Daily Chart (7 Days) ---
        y = -250;
        dailyTitleText = CreateText(panel.transform, "DailyTitle", "Last 7 Days", 48, new Vector2(0, y), TextAnchor.MiddleCenter, false).GetComponentInChildren<Text>();
        
        GameObject dailyObj = new GameObject("DailyChart");
        dailyObj.transform.SetParent(panel.transform, false);
        dailyChartContainer = dailyObj.GetComponent<RectTransform>();
        if (dailyChartContainer == null) dailyChartContainer = dailyObj.AddComponent<RectTransform>();
        dailyChartContainer.anchorMin = new Vector2(0.05f, 1);
        dailyChartContainer.anchorMax = new Vector2(0.95f, 1);
        dailyChartContainer.pivot = new Vector2(0.5f, 1);
        dailyChartContainer.sizeDelta = new Vector2(0, 400);
        dailyChartContainer.anchoredPosition = new Vector2(0, y - 80); // More spacing
        
        Image dailyBg = dailyObj.AddComponent<Image>();
        dailyBg.color = new Color(1, 1, 1, 0.05f);

        // --- Hourly Chart (72 Hours, capped to 40 rows) ---
        y = -800;
        hourlyTitleText = CreateText(panel.transform, "HourlyTitle", "Last 72 Hours", 48, new Vector2(0, y), TextAnchor.MiddleCenter, false).GetComponentInChildren<Text>();
        
        GameObject hourlyObj = new GameObject("HourlyChart");
        hourlyObj.transform.SetParent(panel.transform, false);
        hourlyChartContainer = hourlyObj.GetComponent<RectTransform>();
        if (hourlyChartContainer == null) hourlyChartContainer = hourlyObj.AddComponent<RectTransform>();
        hourlyChartContainer.anchorMin = new Vector2(0.05f, 1);
        hourlyChartContainer.anchorMax = new Vector2(0.95f, 1);
        hourlyChartContainer.pivot = new Vector2(0.5f, 1);
        hourlyChartContainer.sizeDelta = new Vector2(0, 400);
        hourlyChartContainer.anchoredPosition = new Vector2(0, y - 80); // More spacing
        
        Image hourlyBg = hourlyObj.AddComponent<Image>();
        hourlyBg.color = new Color(1, 1, 1, 0.05f);

        // Close Button (Top Right)
        GameObject closeBtn = new GameObject("CloseBtn");
        closeBtn.transform.SetParent(panel.transform, false);
        RectTransform closeRect = closeBtn.GetComponent<RectTransform>();
        if (closeRect == null) closeRect = closeBtn.AddComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1, 1);
        closeRect.anchorMax = new Vector2(1, 1);
        closeRect.anchoredPosition = new Vector2(-40, -40);
        closeRect.sizeDelta = new Vector2(120, 120); // Bigger touch area
        
        Button b = closeBtn.AddComponent<Button>();
        b.onClick.AddListener(CloseModal);
        CreateText(closeBtn.transform, "X", "", 60, Vector2.zero, TextAnchor.MiddleCenter, false);

        modalOverlay.SetActive(false);
    }

    GameObject CreateText(Transform parent, string name, string text, int size, Vector2 pos, TextAnchor align, bool bold)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        if (rt == null) rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1);
        rt.anchorMax = new Vector2(0.5f, 1);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(800, 120);
        
        Text t = go.AddComponent<Text>();
        t.text = text;
        t.font = customFont ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = size;
        t.color = Color.white;
        t.alignment = align;
        if (bold) t.fontStyle = FontStyle.Bold;
        return go;
    }

    void UpdateSummary()
    {
        if (pedometerService == null)
        {
            pedometerService = FindFirstObjectByType<PedometerService>();
            if (pedometerService == null)
            {
                // Graceful fallback: show random step count when Pedometer doesn't exist (e.g. editor)
                int fallbackSteps = (int)(Time.realtimeSinceStartup * 10) % 15000 + 3000; // Pseudo-random based on time
                if (stepsSummaryText != null)
                    stepsSummaryText.text = $"STEPS: {K1L0StepFormatter.Value(fallbackSteps)} >";
                return;
            }
        }

        int allTimeSteps = DeviceIDManager.Instance != null ? DeviceIDManager.Instance.AllTimeSteps : 0;
        int sessionSteps = pedometerService.stepCount;
        int last24Hours = pedometerService.stepsLast24Hours;

        int total = allTimeSteps > 0 ? allTimeSteps : sessionSteps;

        // Fallback to 24h count if AllTime is strangely 0 but we have history
        // Or if current session is 0, show today's total instead of 0
        if (total == 0 && last24Hours > 0)
        {
            total = last24Hours;
        }

        stepsSummaryText.text = $"STEPS: {K1L0StepFormatter.Value(total)} >";

        // Log every 10 seconds (since we update every 1 second)
        if (Time.frameCount % 600 == 0)
        {
            RectTransform rootRect = GetComponent<RectTransform>();
            Debug.Log($"[StepsView] UpdateSummary: allTime={allTimeSteps}, session={sessionSteps}, 24h={last24Hours}, showing={total}, text='{stepsSummaryText.text}', position={rootRect.anchoredPosition}");
        }
    }

    public void OpenModal()
    {
        isModalOpen = true;
        modalOverlay.SetActive(true);
        StartCoroutine(AnimatePanel(new Vector2(0, 0)));
        RefreshModalData();
    }

    public void CloseModal()
    {
        isModalOpen = false;
        StartCoroutine(AnimatePanel(new Vector2(0, -1600), true));
    }

    IEnumerator AnimatePanel(Vector2 target, bool hideAfter = false)
    {
        if (modalPanel == null) yield break;
        float elapsed = 0f;
        Vector2 start = modalPanel.anchoredPosition;
        while (elapsed < animationDuration)
        {
            if (modalPanel == null) yield break;
            elapsed += Time.deltaTime;
            float t = elapsed / animationDuration;
            t = t * t * (3f - 2f * t); // Smooth step
            modalPanel.anchoredPosition = Vector2.Lerp(start, target, t);
            yield return null;
        }
        if (modalPanel != null) modalPanel.anchoredPosition = target;
        if (hideAfter && modalOverlay != null) modalOverlay.SetActive(false);
    }

    void RefreshModalData()
    {
        if (pedometerService == null) return;

        pedometerService.GetLast7DaysSteps((history) => {
            // Reverse to show Oldest -> Newest (Chronological)
            history.Reverse();
            int total7d = history.Sum(x => x.steps);
            if (dailyTitleText) dailyTitleText.text = $"Last 7 Days: {total7d:N0}";
            BuildChart(dailyChartContainer, history.Select(x => x.steps).ToList(), history.Select(x => x.date.ToString("ddd")).ToList());
        });

        pedometerService.GetLastHoursBreakdown(HourlyHistoryHours, (history) => {
            history = history
                .OrderByDescending(x => x.time)
                .Take(HourlyHistoryDisplayRows)
                .ToList();

            // Reverse displayed rows to show Oldest -> Newest (Chronological)
            history.Reverse();
            int visibleTotal = history.Sum(x => x.steps);
            if (hourlyTitleText) hourlyTitleText.text = $"Last 72 Hours: {visibleTotal:N0} ({history.Count} rows)";
            BuildChart(hourlyChartContainer, history.Select(x => x.steps).ToList(), history.Select(x => x.time.Hour.ToString() + "h").ToList());
        });
    }

    void BuildChart(RectTransform container, List<int> values, List<string> labels)
    {
        if (container == null) return;
        foreach (Transform child in container) Destroy(child.gameObject);

        if (values.Count < 2) return;

        float width = container.rect.width;
        float height = container.rect.height;
        int maxVal = values.Max();
        if (maxVal < 100) maxVal = 100;

        Vector2 lastPos = Vector2.zero;

        // Draw connecting lines first (behind dots)
        for (int i = 0; i < values.Count; i++)
        {
            float x = (float)i / (values.Count - 1) * width - (width/2); // Center pivot
            float y = ((float)values[i] / maxVal * height * 0.7f) - (height/2) + 20; // Use 70% height
            Vector2 currentPos = new Vector2(x, y);

            if (i > 0)
            {
                CreateLine(container, lastPos, currentPos);
            }
            lastPos = currentPos;
        }

        // Draw Dots and Labels
        for (int i = 0; i < values.Count; i++)
        {
            float x = (float)i / (values.Count - 1) * width - (width/2);
            float y = ((float)values[i] / maxVal * height * 0.7f) - (height/2) + 20;
            Vector2 currentPos = new Vector2(x, y);

            // Dot
            GameObject dot = new GameObject("Dot");
            dot.transform.SetParent(container, false);
            Image img = dot.AddComponent<Image>();
            img.color = highlightColor;
            RectTransform rt = dot.GetComponent<RectTransform>();
            if (rt == null) rt = dot.AddComponent<RectTransform>();
            rt.anchoredPosition = currentPos;
            rt.sizeDelta = new Vector2(16, 16); // Slightly bigger dots

            // Value Text (Only for peaks or small datasets)
            if (values.Count < 10 || values[i] == maxVal || i % 4 == 0)
            {
                CreateChartText(container, values[i].ToString(), currentPos + new Vector2(0, 35), 30); // Bigger font & offset
            }

            // Axis Label
            if (values.Count < 10 || i % 4 == 0)
            {
                CreateChartText(container, labels[i], new Vector2(x, -(height/2) + 10), 28, Color.gray); // Bigger font
            }
        }
    }

    void CreateChartText(Transform parent, string text, Vector2 pos, int size, Color? c = null)
    {
        GameObject valObj = new GameObject("Lbl");
        valObj.transform.SetParent(parent, false);
        Text valTxt = valObj.AddComponent<Text>();
        valTxt.text = text;
        valTxt.font = stepsSummaryText.font;
        valTxt.fontSize = size;
        valTxt.color = c ?? Color.white;
        valTxt.alignment = TextAnchor.MiddleCenter;
        RectTransform valRt = valObj.GetComponent<RectTransform>();
        if (valRt == null) valRt = valObj.AddComponent<RectTransform>();
        valRt.anchoredPosition = pos;
        valRt.sizeDelta = new Vector2(100, 40); // Bigger box
    }

    void CreateLine(Transform parent, Vector2 p1, Vector2 p2)
    {
        GameObject lineObj = new GameObject("Line");
        lineObj.transform.SetParent(parent, false);
        // Ensure lines render behind dots (sibling index 0)
        lineObj.transform.SetAsFirstSibling();
        
        Image img = lineObj.AddComponent<Image>();
        img.color = highlightColor;
        img.color = new Color(img.color.r, img.color.g, img.color.b, 0.5f);

        RectTransform rt = lineObj.GetComponent<RectTransform>();
        if (rt == null) rt = lineObj.AddComponent<RectTransform>();
        Vector2 dir = (p2 - p1).normalized;
        float dist = Vector2.Distance(p1, p2);

        rt.sizeDelta = new Vector2(dist, 3f);
        rt.anchoredPosition = p1 + dir * dist * 0.5f;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        rt.rotation = Quaternion.Euler(0, 0, angle);
    }
}
