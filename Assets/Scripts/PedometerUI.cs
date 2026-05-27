using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

public class PedometerUI : MonoBehaviour
{
    [Header("UI Settings")]
    public Font customFont; // Assign in Inspector
    // Force recompile
    public float slideDuration = 0.5f;
    public Color barColor = new Color(0.2f, 0.8f, 0.2f);
    public Color barHighlightColor = new Color(0.3f, 1f, 0.3f);

    [Header("Visibility")]
    public bool showButton = true; // Set to false to hide button

    private GameObject canvasGO;
    private RectTransform modalPanel;
    private RectTransform graphContainer;
    private Text totalStepsText;
    private Text steps24hText;
    private Text steps7dText;
    private Text weatherText;
    private Text locationText; // City/town label
    private bool isModalOpen = false;
    private Button userButton;

    private PedometerService pedometerService;

    // Public method to update weather from UserPresenceManager
    public void UpdateWeather(string icon, string glyph, float tempF)
    {
        if (weatherText != null)
        {
            string weatherIcon = GetWeatherIcon(glyph);
            // Debug.Log($"[PedometerUI] Updating weather: {glyph} -> {weatherIcon} {tempF}°F");
            // Rich text size tag removed as it caused disappearing text
            weatherText.text = $"{Mathf.RoundToInt(tempF)}°F {weatherIcon}";
        }
        else
        {
            Debug.LogWarning($"[PedometerUI] UpdateWeather called but weatherText is NULL! ({glyph} {tempF}°F)");
        }
    }

    private string GetWeatherIcon(string glyph)
    {
        switch (glyph.ToLower())
        {
            case "sun":
            case "sunny":
            case "clear":
                return "☀";
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
                return glyph; // Fallback to original text
        }
    }

void Start()
    {
        Debug.Log("[PedometerUI] ========== START CALLED ==========");

        // LocationsPanel is created by UILayoutManager (single owner)

        // 0.5 Ensure Stories UI exists
        if (FindFirstObjectByType<KiloWorld.UI.Stories.StoriesUIBootstrapper>() == null)
        {
            GameObject storiesGO = new GameObject("StoriesUI");
            storiesGO.AddComponent<KiloWorld.UI.Stories.StoriesUIBootstrapper>();
            Debug.Log("[PedometerUI] Created StoriesUIBootstrapper.");
        }

        // LocationEnterTrigger removed (Mapbox dependency eliminated)

        pedometerService = FindObjectOfType<PedometerService>();
        CreateUI();
        
        // Delayed initial update to allow PedometerService.Start() to run first
        Invoke(nameof(UpdateStepCounters), 0.5f);
        
        InvokeRepeating(nameof(UpdateStepCounters), 2f, 5f); // Update every 5 seconds starting after 2s

        // Load 7-day data on startup to populate the 7d counter
        if (pedometerService != null)
        {
            pedometerService.GetLast7DaysSteps((history) => {
                // The callback in PedometerService already updates stepsLast7Days
                // This will be picked up by UpdateStepCounters
            });
        }
    }

    void UpdateStepCounters()
    {
        // LocationEnterTrigger removed (Mapbox dependency eliminated)

        if (pedometerService == null) return;

        if (steps24hText != null)
        {
            int steps24h = pedometerService.stepsLast24Hours;
            int steps7d = pedometerService.stepsLast7Days;
            
            // Format: "12,345 (24h)   45,678 (7d)"
            // Use placeholders if data is still loading (-1)
            string s24 = steps24h >= 0 ? $"{steps24h:N0}" : "...";
            string s7d = steps7d >= 0 ? $"{steps7d:N0}" : "...";
            
            steps24hText.text = $"{s24} (24h)   {s7d} (7d)";
        }

    }
    private Font GetFont()
    {
        if (customFont != null) return customFont;
        
        // Use built-in default font (LegacyRuntime is the name for the default font in code)
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        
        // If still null, try just searching for default font in Resources
        if (font == null) font = Font.CreateDynamicFontFromOSFont("Helvetica", 16);
        
        return font;
    }

    void Update()
    {
        // Safety Check: Ensure Canvas exists and is enabled
        if (canvasGO != null)
        {
            Canvas c = canvasGO.GetComponent<Canvas>();
            if (c != null && !c.enabled)
            {
                Debug.LogWarning("[PedometerUI] Canvas was disabled! Re-enabling...");
                c.enabled = true;
            }
        }
        
        // Debug Position - DISABLED SPAM
        /*
        if (Time.frameCount % 300 == 0)
        {
            if (weatherText != null)
            {
                var rt = weatherText.GetComponent<RectTransform>();
                Debug.Log($"[PedometerUI] Weather: text='{weatherText.text}', color={weatherText.color}, fontSize={weatherText.fontSize}, enabled={weatherText.enabled}, active={weatherText.gameObject.activeInHierarchy}");
                Debug.Log($"[PedometerUI] Weather Pos: {rt.position} (Screen Space), Anchored: {rt.anchoredPosition}");
            }
            if (locationText != null)
            {
                Debug.Log($"[PedometerUI] Location: text='{locationText.text}', color={locationText.color}, active={locationText.gameObject.activeInHierarchy}");
            }
        }
        */
    }

    void CreateUI()
    {
        if (canvasGO != null) return; // Prevent duplicates

        Debug.Log("[PedometerUI] Creating UI...");

        // 1. Create Canvas
        canvasGO = new GameObject("PedometerCanvas");
        canvasGO.layer = 5; // UI Layer
        
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 3000; // Very high order to ensure it's on top
        
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        
        canvasGO.AddComponent<GraphicRaycaster>();

        // Ensure EventSystem exists
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        // 2. Create User Icon Button (Top Right)
        GameObject buttonObj = new GameObject("UserButton");
        buttonObj.layer = 5;
        buttonObj.transform.SetParent(canvasGO.transform, false);
        
        Image btnImage = buttonObj.AddComponent<Image>();
        btnImage.color = Color.clear; // Make transparent
        
        Button btn = buttonObj.AddComponent<Button>();
        btn.onClick.AddListener(ToggleModal);
        userButton = btn;

        RectTransform btnRect = buttonObj.GetComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(1, 0); // Bottom-Right
        btnRect.anchorMax = new Vector2(1, 0);
        btnRect.pivot = new Vector2(1, 0);
        btnRect.sizeDelta = new Vector2(200, 200); 
        // Position with safe area margin (20px padding from bottom/right)
        btnRect.anchoredPosition = new Vector2(-20, 20); 

        // Hide button if disabled
        buttonObj.SetActive(showButton);

        // Add an icon text or image inside
        GameObject iconTextObj = new GameObject("IconText");
        iconTextObj.layer = 5;
        iconTextObj.transform.SetParent(buttonObj.transform, false);
        Text iconText = iconTextObj.AddComponent<Text>();
        iconText.text = "A"; // Changed to 'A'
        iconText.font = GetFont();
        iconText.fontSize = 100;
        iconText.alignment = TextAnchor.MiddleCenter;
        iconText.color = Color.white;
        iconText.raycastTarget = false;
        RectTransform iconRect = iconTextObj.GetComponent<RectTransform>();
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;

        // --- NEW STEP COUNTERS (TOP LEFT) ---
        Debug.Log("[PedometerUI] Creating StepCounts container (top-left UI)");
        GameObject stepCountsObj = new GameObject("StepCounts");
        stepCountsObj.layer = 5;
        stepCountsObj.transform.SetParent(canvasGO.transform, false);

        // Background (Black with low alpha, no rounding for now)
        Image bg = stepCountsObj.AddComponent<Image>();
        bg.color = new Color(0, 0, 0, 0.4f); // Faded black
        bg.maskable = false;
        bg.raycastTarget = false; 

        RectTransform stepCountsRect = stepCountsObj.GetComponent<RectTransform>();
        stepCountsRect.anchorMin = new Vector2(0, 1);
        stepCountsRect.anchorMax = new Vector2(0, 1);
        stepCountsRect.pivot = new Vector2(0, 1);
        // LOWERED POSITION: moved down to clear larger stories (-300)
        stepCountsRect.anchoredPosition = new Vector2(20, -300); 
        stepCountsRect.sizeDelta = new Vector2(250, 90); 
        Debug.Log($"[PedometerUI] StepCounts container created at position {stepCountsRect.anchoredPosition} with size {stepCountsRect.sizeDelta}");

        // 1. City Name (Row 1)
        GameObject locationObj = new GameObject("LocationText");
        locationObj.transform.SetParent(stepCountsObj.transform, false);
        locationText = locationObj.AddComponent<Text>();
        locationText.font = GetFont();
        locationText.fontSize = 12;
        locationText.color = Color.white;
        locationText.fontStyle = FontStyle.Bold;
        locationText.alignment = TextAnchor.UpperLeft;
        locationText.text = "Finding location..."; // Placeholder
        locationText.horizontalOverflow = HorizontalWrapMode.Overflow;
        locationText.raycastTarget = false;
        
        RectTransform locationRect = locationObj.GetComponent<RectTransform>();
        locationRect.anchorMin = new Vector2(0, 1);
        locationRect.anchorMax = new Vector2(1, 1);
        locationRect.pivot = new Vector2(0, 1);
        locationRect.anchoredPosition = new Vector2(10, -8);
        locationRect.sizeDelta = new Vector2(230, 18);

        // Create LocationLabelUI component to manage city/town updates from tiles
        LocationLabelUI locationUI = gameObject.AddComponent<LocationLabelUI>();
        locationUI.locationText = locationText;

        // 2. Weather (Row 2)
        GameObject weatherObj = new GameObject("WeatherText");
        weatherObj.transform.SetParent(stepCountsObj.transform, false);
        weatherText = weatherObj.AddComponent<Text>();
        weatherText.font = GetFont();
        weatherText.fontSize = 12;
        weatherText.color = Color.white;
        weatherText.alignment = TextAnchor.UpperLeft;
        weatherText.text = "...";
        weatherText.horizontalOverflow = HorizontalWrapMode.Overflow;
        weatherText.supportRichText = true; // Enable rich text for emoji sizing
        weatherText.raycastTarget = false;
        
        RectTransform weatherRect = weatherObj.GetComponent<RectTransform>();
        weatherRect.anchorMin = new Vector2(0, 1);
        weatherRect.anchorMax = new Vector2(1, 1);
        weatherRect.pivot = new Vector2(0, 1);
        weatherRect.anchoredPosition = new Vector2(10, -28);
        weatherRect.sizeDelta = new Vector2(230, 18);

        // 3. STEPS Label (Row 3)
        GameObject stepsLabelObj = new GameObject("StepsLabel");
        stepsLabelObj.transform.SetParent(stepCountsObj.transform, false);
        Text stepsLabel = stepsLabelObj.AddComponent<Text>();
        stepsLabel.font = GetFont();
        stepsLabel.fontSize = 12;
        stepsLabel.color = Color.white;
        stepsLabel.alignment = TextAnchor.UpperLeft;
        stepsLabel.text = "STEPS";
        stepsLabel.raycastTarget = false;
        
        RectTransform stepsLabelRect = stepsLabelObj.GetComponent<RectTransform>();
        stepsLabelRect.anchorMin = new Vector2(0, 1);
        stepsLabelRect.anchorMax = new Vector2(1, 1);
        stepsLabelRect.pivot = new Vector2(0, 1);
        stepsLabelRect.anchoredPosition = new Vector2(10, -48);
        stepsLabelRect.sizeDelta = new Vector2(230, 18);

        // 4. Steps Counts Line (Row 4)
        GameObject steps24hObj = new GameObject("StepsLine");
        steps24hObj.transform.SetParent(stepCountsObj.transform, false);
        steps24hText = steps24hObj.AddComponent<Text>(); // Reusing variable
        steps24hText.font = GetFont();
        steps24hText.fontSize = 12;
        steps24hText.color = Color.white;
        steps24hText.alignment = TextAnchor.UpperLeft;
        steps24hText.text = "...";
        steps24hText.horizontalOverflow = HorizontalWrapMode.Overflow;
        steps24hText.raycastTarget = false;
        
        RectTransform steps24hRect = steps24hObj.GetComponent<RectTransform>();
        steps24hRect.anchorMin = new Vector2(0, 1);
        steps24hRect.anchorMax = new Vector2(1, 1);
        steps24hRect.pivot = new Vector2(0, 1);
        steps24hRect.anchoredPosition = new Vector2(10, -68);
        steps24hRect.sizeDelta = new Vector2(230, 18);

        // Clear unused references to avoid confusion/errors in Update
        this.steps7dText = null; 
        
        // Debug.Log($"[PedometerUI] ✓ Top-left UI complete");

        // 3. Create Modal Panel
        GameObject panelObj = new GameObject("PedometerModal");
        panelObj.transform.SetParent(canvasGO.transform, false);
        
        Image panelImg = panelObj.AddComponent<Image>();
        panelImg.color = new Color(0.95f, 0.95f, 0.95f, 1f); // Off-white background
        
        modalPanel = panelObj.GetComponent<RectTransform>();
        modalPanel.anchorMin = new Vector2(0, 0);
        modalPanel.anchorMax = new Vector2(1, 0); // Bottom stretch
        modalPanel.pivot = new Vector2(0.5f, 0);
        modalPanel.sizeDelta = new Vector2(0, 1400); // Taller modal for character list
        modalPanel.anchoredPosition = new Vector2(0, -2000); // Start hidden below screen

        // Add Content to Modal
        // Title
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(panelObj.transform, false);
        Text titleText = titleObj.AddComponent<Text>();
        titleText.text = "Activity";
        titleText.font = GetFont();
        titleText.fontSize = 70;
        titleText.color = Color.black;
        titleText.alignment = TextAnchor.MiddleCenter;
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.sizeDelta = new Vector2(0, 100);
        titleRect.anchoredPosition = new Vector2(0, -50);

        // Total Steps Today
        GameObject stepsObj = new GameObject("TotalSteps");
        stepsObj.transform.SetParent(panelObj.transform, false);
        totalStepsText = stepsObj.AddComponent<Text>();
        totalStepsText.text = "Loading...";
        totalStepsText.font = GetFont();
        totalStepsText.fontSize = 120;
        totalStepsText.fontStyle = FontStyle.Bold;
        totalStepsText.color = barColor;
        totalStepsText.alignment = TextAnchor.MiddleCenter;
        RectTransform stepsRect = stepsObj.GetComponent<RectTransform>();
        stepsRect.anchorMin = new Vector2(0, 1);
        stepsRect.anchorMax = new Vector2(1, 1);
        stepsRect.pivot = new Vector2(0.5f, 1);
        stepsRect.sizeDelta = new Vector2(0, 150);
        stepsRect.anchoredPosition = new Vector2(0, -150);

        // REMOVED: Characters List

        // Graph Container - Expanded to fill middle space
        GameObject graphObj = new GameObject("GraphContainer");
        graphObj.transform.SetParent(panelObj.transform, false);
        graphContainer = graphObj.AddComponent<RectTransform>();
        graphContainer.anchorMin = new Vector2(0.1f, 0.4f); // Start higher up (middle)
        graphContainer.anchorMax = new Vector2(0.9f, 0.7f); // End below title/steps
        graphContainer.offsetMin = Vector2.zero;
        graphContainer.offsetMax = Vector2.zero;
        
        // --- AUDIO DROP INTEGRATION MOVED TO STORIES UI ---
        
        // Close Button - Top Right
        GameObject closeBtnObj = new GameObject("CloseButton");
        closeBtnObj.transform.SetParent(panelObj.transform, false);
        Image closeBtnImage = closeBtnObj.AddComponent<Image>();
        closeBtnImage.color = Color.clear;
        Button closeBtn = closeBtnObj.AddComponent<Button>();
        closeBtn.onClick.AddListener(ToggleModal);
        
        GameObject closeTxtObj = new GameObject("Text");
        closeTxtObj.transform.SetParent(closeBtnObj.transform, false);
        Text ct = closeTxtObj.AddComponent<Text>();
        ct.text = "✕";
        ct.font = GetFont();
        ct.fontSize = 60;
        ct.color = Color.gray;
        ct.alignment = TextAnchor.MiddleCenter;
        RectTransform ctRect = closeTxtObj.GetComponent<RectTransform>();
        ctRect.anchorMin = Vector2.zero;
        ctRect.anchorMax = Vector2.one;
        
        RectTransform closeRect = closeBtnObj.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1, 1);
        closeRect.anchorMax = new Vector2(1, 1);
        closeRect.pivot = new Vector2(1, 1);
        closeRect.sizeDelta = new Vector2(100, 100);
        closeRect.anchoredPosition = new Vector2(-20, -20);
    }

    public void ToggleModal()
    {
        isModalOpen = !isModalOpen;
        
        if (isModalOpen)
        {
            // Slide Up
            StartCoroutine(AnimateModal(new Vector2(0, 0)));
            RefreshData();
        }
        else
        {
            // Slide Down
            StartCoroutine(AnimateModal(new Vector2(0, -2000)));
        }
    }

    private System.Collections.IEnumerator AnimateModal(Vector2 targetPos)
    {
        if (modalPanel == null) yield break;

        Vector2 startPos = modalPanel.anchoredPosition;
        float elapsed = 0f;
        
        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / slideDuration;
            // Smooth step
            t = t * t * (3f - 2f * t);
            
            if (modalPanel != null)
                modalPanel.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
            yield return null;
        }
        if (modalPanel != null)
            modalPanel.anchoredPosition = targetPos;
    }

    private void RefreshData()
    {
        if (pedometerService == null || totalStepsText == null) return;
        
        totalStepsText.text = "Loading...";
        
        pedometerService.GetLast7DaysSteps((history) => {
            if (totalStepsText == null) return;

            if (history == null || history.Count == 0)
            {
                totalStepsText.text = "No Data";
                return;
            }
            
            // Update Today's steps (first item if sorted desc, or find today)
            var todayData = history.FirstOrDefault(x => x.date == System.DateTime.Today);
            totalStepsText.text = $"{todayData.steps:N0} Steps";

            BuildGraph(history);
        });
    }

    private void BuildGraph(List<PedometerService.DailyStepData> history)
    {
        if (graphContainer == null) return;

        // Clear old graph
        foreach (Transform child in graphContainer)
        {
            Destroy(child.gameObject);
        }
        
        // Sort by date ascending for graph (Mon -> Sun)
        var sortedHistory = history.OrderBy(x => x.date).ToList();
        
        int maxSteps = sortedHistory.Max(x => x.steps);
        if (maxSteps < 100) maxSteps = 100; // Minimum scale
        
        float widthPerBar = graphContainer.rect.width / sortedHistory.Count;
        float barWidth = widthPerBar * 0.6f; // 60% width, 40% gap
        
        for (int i = 0; i < sortedHistory.Count; i++)
        {
            var data = sortedHistory[i];
            float normalizedHeight = (float)data.steps / maxSteps;
            
            GameObject barObj = new GameObject($"Bar_{i}");
            barObj.transform.SetParent(graphContainer, false);
            
            Image barImg = barObj.AddComponent<Image>();
            barImg.color = (data.date == System.DateTime.Today) ? barHighlightColor : barColor;
            
            RectTransform barRect = barObj.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0, 0);
            barRect.anchorMax = new Vector2(0, 0);
            barRect.pivot = new Vector2(0.5f, 0);
            
            float xPos = (i * widthPerBar) + (widthPerBar / 2);
            barRect.anchoredPosition = new Vector2(xPos, 0);
            barRect.sizeDelta = new Vector2(barWidth, graphContainer.rect.height * normalizedHeight);
            
            // Add Label (Day Name)
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(barObj.transform, false);
            Text labelText = labelObj.AddComponent<Text>();
            labelText.text = data.date.ToString("ddd"); // Mon, Tue...
            labelText.font = customFont ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelText.fontSize = 30;
            labelText.color = Color.gray;
            labelText.alignment = TextAnchor.MiddleCenter;
            
            RectTransform labelRect = labelObj.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0.5f, 0);
            labelRect.anchorMax = new Vector2(0.5f, 0);
            labelRect.pivot = new Vector2(0.5f, 1);
            labelRect.sizeDelta = new Vector2(100, 40);
            labelRect.anchoredPosition = new Vector2(0, -10); // Below bar
            
            // Add Step Count Label (Above bar)
            GameObject countObj = new GameObject("Count");
            countObj.transform.SetParent(barObj.transform, false);
            Text countText = countObj.AddComponent<Text>();
            countText.text = data.steps.ToString();
            countText.font = customFont ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            countText.fontSize = 24;
            countText.color = Color.black;
            countText.alignment = TextAnchor.MiddleCenter;
            
            RectTransform countRect = countObj.GetComponent<RectTransform>();
            countRect.anchorMin = new Vector2(0.5f, 1);
            countRect.anchorMax = new Vector2(0.5f, 1);
            countRect.pivot = new Vector2(0.5f, 0);
            countRect.sizeDelta = new Vector2(100, 30);
            countRect.anchoredPosition = new Vector2(0, 5); // Above bar
        }
    }
}
