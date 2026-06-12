using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Kiloverse.Mapbox;

public class LocationsPanel : MonoBehaviour
{
    [Header("UI Settings")]
    public Font customFont;
    public Color panelColor = new Color(0.1f, 0.1f, 0.1f, 0.95f);
    public Color textColor = Color.white;
    public Color highlightColor = new Color(0.2f, 0.8f, 1.0f); // Cyan

    private GameObject canvasGO;
    private RectTransform modalPanel;
    private RectTransform listContainer;
    private bool isModalOpen = false;
    private Button openButton;
    private Text buttonText;
    private GameObject enterButton;
    private Text enterButtonText;
    private Button filterToggleBtn;
    private Text filterToggleText;
    private Button filterGroupBtn;
    private Text filterGroupText;
    private RectTransform rootRect;
    private LayoutElement rootLayout;
    private RectTransform buttonRect;

    private TransmitterScanner scanner;
    private const float ENTER_DISTANCE_THRESHOLD = 30f; // meters
    private Dictionary<string, Image> filterButtonImages = new Dictionary<string, Image>();
    private string currentFilter = "all";

    void Start()
    {
        StartCoroutine(DeferredStart());
    }

    IEnumerator DeferredStart()
    {
        yield return null; // Let frame settle
        scanner = TransmitterScanner.Instance;
        yield return null;

        yield return StartCoroutine(CreateUICoroutine());
        yield return null;

        StartCoroutine(UpdateRoutine());
        StartCoroutine(UpdateButtonTextRoutine());
        SetFilter("all"); // Default
    }

    // ... (UpdateRoutine, UpdateButtonTextRoutine, UpdateButtonText, UpdateEnterButton remain same) ...

    void SetFilter(string category)
    {
        currentFilter = category;
        if (scanner != null)
        {
            // Always enable filtering so "all" respects the 4 main groups instead of showing everything
            scanner.disableFiltering = false;
            scanner.SetCategoryFilter(category);
            RefreshList();
        }

        // Update visuals
        foreach (var kvp in filterButtonImages)
        {
            kvp.Value.color = (kvp.Key == category) ? highlightColor : new Color(0.2f, 0.2f, 0.2f, 1f);
        }
    }

    void CreateFilterButton(Transform parent, string content, string category, bool isText = false)
    {
        GameObject btnObj = new GameObject($"Filter_{category}");
        btnObj.transform.SetParent(parent, false);

        Image btnBg = btnObj.AddComponent<Image>();
        btnBg.color = new Color(0.2f, 0.2f, 0.2f, 1f); // Default gray background
        
        Button btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(() => SetFilter(category));

        // Add to dictionary for highlighting
        filterButtonImages[category] = btnBg;
        
        // Layout Element
        LayoutElement le = btnObj.AddComponent<LayoutElement>();
        le.preferredWidth = 100;
        le.preferredHeight = 100;

        if (isText)
        {
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            
            Text text = textObj.AddComponent<Text>();
            text.text = content;
            text.font = customFont ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 32; // Smaller for "ALL"
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.fontStyle = FontStyle.Bold;
            
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
        }
        else
        {
            // Icon Image
            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(btnObj.transform, false);
            
            Image iconImg = iconObj.AddComponent<Image>();
            Sprite s = Resources.Load<Sprite>(content);
            if (s != null)
            {
                iconImg.sprite = s;
                iconImg.preserveAspect = true; 
            }
            else
            {
                // Fallback if sprite missing
                Debug.LogWarning($"[LocationsPanel] Missing icon: {content}");
                iconImg.color = Color.red; 
            }

            RectTransform iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.2f, 0.2f); // Padding
            iconRect.anchorMax = new Vector2(0.8f, 0.8f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
        }
    }

    IEnumerator UpdateRoutine()
    {
        while (true)
        {
            if (isModalOpen)
            {
                RefreshList();
            }
            yield return new WaitForSeconds(1.0f);
        }
    }

    IEnumerator UpdateButtonTextRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(1.0f);
            UpdateButtonText();
            UpdateEnterButton();
        }
    }

    void UpdateButtonText()
    {
        if (buttonText == null || scanner == null) return;

        var all = scanner.GetNearestUnfiltered(3);

        // DEBUG: Log scanner state AND check if POI tiles exist
        if (Time.frameCount % 300 == 0) // Every ~5 seconds
        {
            var playerPos = Camera.main ? Camera.main.transform.position : Vector3.zero;
            Debug.Log($"[LocationsPanel] Scanner found {all.Count} locations. Player at {playerPos}");
        }

        if (all.Count == 0)
        {
            buttonText.text = "nearby\n...";
            return;
        }

        var closestList = all;

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("nearby");

        // Use dynamic stride
        float stride = 0.762f;
        var pedometer = FindFirstObjectByType<PedometerService>();
        if (pedometer != null) stride = pedometer.EstimatedStrideLength;

        for (int i = 0; i < closestList.Count; i++)
        {
            var item = closestList[i];
            
            // Calculate distance string
            float miles = item.Distance * 0.000621371f;
            int steps = Mathf.RoundToInt(item.Distance / stride);
            string distStr = miles <= 0.4f ? $"{steps}st" : $"{miles:F1}mi";
            
            // Truncate name if too long
            string name = item.Name;
            if (name.Length > 15) name = name.Substring(0, 14) + "…";
            
            sb.AppendLine($"{i+1}. {name} ({distStr})");
        }
        
        sb.Append("more >");

        buttonText.text = sb.ToString();
        UpdateRootLayout();
    }

    void UpdateEnterButton()
    {
        /* DISABLED: Backend handles entry logic
        if (enterButton == null || enterButtonText == null || scanner == null) return;

        var all = scanner.GetAll();
        if (all.Count == 0)
        {
            enterButton.SetActive(false);
            return;
        }

        // Find closest location
        var closest = all[0];

        // Show button if within 30 meters
        if (closest.Distance <= ENTER_DISTANCE_THRESHOLD)
        {
            enterButton.SetActive(true);
            enterButtonText.text = $"Enter {closest.Name}";
        }
        else
        {
            enterButton.SetActive(false);
        }
        */
    }

    IEnumerator CreateUICoroutine()
    {
        rootRect = GetComponent<RectTransform>();
        if (rootRect == null)
        {
            rootRect = gameObject.AddComponent<RectTransform>();
        }
        rootRect.anchorMin = new Vector2(0, 1);
        rootRect.anchorMax = new Vector2(0, 1);
        rootRect.pivot = new Vector2(0, 1);
        rootRect.anchoredPosition = Vector2.zero;
        rootRect.sizeDelta = new Vector2(900, 0);

        rootLayout = GetComponent<LayoutElement>();
        if (rootLayout == null)
        {
            rootLayout = gameObject.AddComponent<LayoutElement>();
        }
        rootLayout.minHeight = 0f;
        rootLayout.preferredHeight = 0f;
        rootLayout.flexibleHeight = 0f;

        // Use a dedicated canvas for the locations UI so it's anchored to the screen.
        Canvas parentCanvas = null;
        GameObject existingCanvas = GameObject.Find("LocationsCanvas");
        if (existingCanvas != null)
        {
            parentCanvas = existingCanvas.GetComponent<Canvas>();
        }

        if (parentCanvas == null)
        {
            yield return null; // Yield before creating new Canvas (can trigger layout)
            GameObject canvasObj = new GameObject("LocationsCanvas");
            parentCanvas = canvasObj.AddComponent<Canvas>();
            parentCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            parentCanvas.sortingOrder = 3001;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);

            canvasObj.AddComponent<GraphicRaycaster>();
        }

        canvasGO = parentCanvas.gameObject;
        yield return null;

        // 1. Open Button (anchored to lower-left, outside vertical layout)
        GameObject buttonObj = new GameObject("LocationsButton");
        buttonObj.transform.SetParent(canvasGO.transform, false); // Parent to canvas so it doesn't affect layout
        
        Image btnImage = buttonObj.AddComponent<Image>();
        btnImage.color = new Color(0,0,0,0.5f); // Semi-transparent black
        
        Button btn = buttonObj.AddComponent<Button>();
        btn.onClick.AddListener(ToggleModal);
        openButton = btn;

        RectTransform btnRect = buttonObj.GetComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0f, 0f);
        btnRect.anchorMax = new Vector2(0f, 0f);
        btnRect.pivot = new Vector2(0f, 0f);
        btnRect.anchoredPosition = new Vector2(20f, 20f);
        btnRect.sizeDelta = new Vector2(700, 0); // Width fixed, height from fitter
        buttonRect = btnRect;

        // Add Layout Group to button for padding/sizing
        VerticalLayoutGroup btnLayout = buttonObj.AddComponent<VerticalLayoutGroup>();
        btnLayout.padding = new RectOffset(20, 20, 20, 20);
        btnLayout.childControlHeight = true;
        btnLayout.childControlWidth = true;
        btnLayout.childForceExpandHeight = false;
        btnLayout.childForceExpandWidth = true;

        // Add ContentSizeFitter to button so it grows with text
        ContentSizeFitter btnFitter = buttonObj.AddComponent<ContentSizeFitter>();
        btnFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        btnFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        // Create Text Container inside Button
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(buttonObj.transform, false);
        
        buttonText = textObj.AddComponent<Text>();
        buttonText.text = "nearby\n...";
        buttonText.font = customFont ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        buttonText.fontSize = 36; // Increased to match Pedometer (was 32)
        buttonText.alignment = TextAnchor.UpperLeft;
        buttonText.color = Color.white;
        // Text RectTransform is controlled by parent LayoutGroup, no manual sizing needed

        UpdateRootLayout();
        yield return null; // Yield after layout (can block)

        /* DISABLED: Enter Location Button handled by backend
        // 1.5. Create "Enter Location" button (center-bottom, hidden by default)
        enterButton = new GameObject("EnterButton");
        enterButton.transform.SetParent(canvasGO.transform, false);

        Image enterBtnImage = enterButton.AddComponent<Image>();
        enterBtnImage.color = new Color(0.2f, 0.8f, 0.2f, 0.9f); // Green

        Button enterBtn = enterButton.AddComponent<Button>();
        enterBtn.onClick.AddListener(OnEnterLocation);

        RectTransform enterBtnRect = enterButton.GetComponent<RectTransform>();
        enterBtnRect.anchorMin = new Vector2(0.5f, 0);
        enterBtnRect.anchorMax = new Vector2(0.5f, 0);
        enterBtnRect.pivot = new Vector2(0.5f, 0);
        enterBtnRect.sizeDelta = new Vector2(400, 80);
        enterBtnRect.anchoredPosition = new Vector2(0, 120); // Above bottom, centered

        enterButtonText = new GameObject("Text").AddComponent<Text>();
        enterButtonText.transform.SetParent(enterButton.transform, false);
        enterButtonText.text = "Enter Location";
        enterButtonText.font = customFont ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        enterButtonText.fontSize = 40;
        enterButtonText.alignment = TextAnchor.MiddleCenter;
        enterButtonText.color = Color.white;
        enterButtonText.fontStyle = FontStyle.Bold;
        enterButtonText.rectTransform.anchorMin = Vector2.zero;
        enterButtonText.rectTransform.anchorMax = Vector2.one;

        // Hide by default until within range
        enterButton.SetActive(false);
        */

        // 2. Modal Panel
        GameObject panelObj = new GameObject("LocationsModal");
        panelObj.transform.SetParent(canvasGO.transform, false);
        
        Image panelImg = panelObj.AddComponent<Image>();
        panelImg.color = panelColor;
        
        modalPanel = panelObj.GetComponent<RectTransform>();
        modalPanel.anchorMin = new Vector2(0.5f, 0.5f);
        modalPanel.anchorMax = new Vector2(0.5f, 0.5f);
        modalPanel.pivot = new Vector2(0.5f, 0.5f);
        modalPanel.sizeDelta = new Vector2(900, 1200);
        modalPanel.anchoredPosition = new Vector2(0, 0);
        
        // Hide initially
        panelObj.SetActive(false);

        // --- NEW FILTER ROW (Top) ---
        GameObject filterRow = new GameObject("FilterRow");
        filterRow.transform.SetParent(panelObj.transform, false);
        
        RectTransform filterRect = filterRow.AddComponent<RectTransform>();
        filterRect.anchorMin = new Vector2(0, 1);
        filterRect.anchorMax = new Vector2(1, 1);
        filterRect.pivot = new Vector2(0.5f, 1);
        filterRect.sizeDelta = new Vector2(0, 120);
        filterRect.anchoredPosition = new Vector2(0, -20);

        HorizontalLayoutGroup filterLayout = filterRow.AddComponent<HorizontalLayoutGroup>();
        filterLayout.childAlignment = TextAnchor.MiddleCenter;
        filterLayout.childControlWidth = false; // Fixed width buttons
        filterLayout.childControlHeight = true;
        filterLayout.spacing = 20;
        filterLayout.padding = new RectOffset(20, 20, 10, 10);

        // Create Filter Buttons (Resources.Load x4 - yield after to avoid blocking)
        CreateFilterButton(filterRow.transform, "ALL", "all", isText: true);
        yield return null;
        CreateFilterButton(filterRow.transform, "Icons/coffee", "coffee");
        CreateFilterButton(filterRow.transform, "Icons/drink", "bar");
        CreateFilterButton(filterRow.transform, "Icons/food", "food");
        CreateFilterButton(filterRow.transform, "Icons/convenience", "convenience");
        yield return null;

        // Close Button
        GameObject closeObj = new GameObject("CloseBtn");
        closeObj.transform.SetParent(panelObj.transform, false);
        // Explicitly add RectTransform before Button adds Image which requires it? No, Button adds Image which adds CanvasRenderer.
        // The error says "CloseBtn" has no RectTransform but script accesses it.
        // GameObject constructor makes Transform, but for UI we need RectTransform.
        // Start by adding RectTransform immediately.
        RectTransform closeRect = closeObj.AddComponent<RectTransform>();
        
        Button closeBtn = closeObj.AddComponent<Button>();
        closeBtn.onClick.AddListener(ToggleModal);
        
        Text closeTxt = new GameObject("Txt").AddComponent<Text>();
        closeTxt.transform.SetParent(closeObj.transform, false);
        closeTxt.text = "";
        closeTxt.font = customFont ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        closeTxt.fontSize = 40; // Reduced from 50
        closeTxt.alignment = TextAnchor.MiddleCenter;
        closeTxt.rectTransform.anchorMin = Vector2.zero;
        closeTxt.rectTransform.anchorMax = Vector2.one;
        
        closeRect.anchorMin = new Vector2(1, 1);
        closeRect.anchorMax = new Vector2(1, 1);
        closeRect.sizeDelta = new Vector2(100, 100);
        closeRect.anchoredPosition = new Vector2(-20, -20);

        // Scroll View Container
        GameObject scrollViewObj = new GameObject("ScrollView");
        scrollViewObj.transform.SetParent(panelObj.transform, false);
        RectTransform scrollViewRect = scrollViewObj.AddComponent<RectTransform>();
        scrollViewRect.anchorMin = new Vector2(0.05f, 0.05f);
        scrollViewRect.anchorMax = new Vector2(0.95f, 0.9f);
        scrollViewRect.offsetMin = Vector2.zero;
        scrollViewRect.offsetMax = Vector2.zero;

        // Add ScrollRect
        UnityEngine.UI.ScrollRect scrollRect = scrollViewObj.AddComponent<UnityEngine.UI.ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;

        // Viewport (masks content)
        GameObject viewportObj = new GameObject("Viewport");
        viewportObj.transform.SetParent(scrollViewObj.transform, false);
        RectTransform viewportRect = viewportObj.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;

        UnityEngine.UI.Mask mask = viewportObj.AddComponent<UnityEngine.UI.Mask>();
        mask.showMaskGraphic = false;
        Image maskImage = viewportObj.AddComponent<Image>();
        maskImage.color = Color.white; // Must be opaque for mask to define area

        scrollRect.viewport = viewportRect;

        // Content (actual list container)
        GameObject listObj = new GameObject("Content");
        listObj.transform.SetParent(viewportObj.transform, false);
        listContainer = listObj.AddComponent<RectTransform>();
        listContainer.anchorMin = new Vector2(0, 1);
        listContainer.anchorMax = new Vector2(1, 1);
        listContainer.pivot = new Vector2(0.5f, 1);
        listContainer.anchoredPosition = Vector2.zero;
        listContainer.sizeDelta = new Vector2(0, 0); // Height will grow with content

        scrollRect.content = listContainer;

        VerticalLayoutGroup vlg = listObj.AddComponent<VerticalLayoutGroup>();
        vlg.childControlHeight = true; // Let VLG control height based on LayoutElements
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childForceExpandWidth = true;
        vlg.spacing = 20;
        vlg.padding = new RectOffset(10, 10, 10, 10);

        // ContentSizeFitter to auto-resize content based on children
        ContentSizeFitter csf = listObj.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        yield return null;
    }

    void UpdateRootLayout()
    {
        if (rootRect == null || rootLayout == null || buttonRect == null) return;
        if (UILayoutManager.LayoutRebuildInProgress) return; // Recursion guard
        UILayoutManager.LayoutRebuildInProgress = true;
        try
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(buttonRect);
            float height = Mathf.Max(buttonRect.rect.height, 20f);
            rootRect.sizeDelta = new Vector2(rootRect.sizeDelta.x, height);
            rootLayout.minHeight = height;
            rootLayout.preferredHeight = height;
        }
        finally
        {
            UILayoutManager.LayoutRebuildInProgress = false;
        }
    }

    void ToggleModal()
    {
        isModalOpen = !isModalOpen;
        modalPanel.gameObject.SetActive(isModalOpen);
        if (isModalOpen)
        {
            if (scanner == null) scanner = FindObjectOfType<TransmitterScanner>();
            RefreshList();
        }
    }

    void OnEnterLocation()
    {
        /* DISABLED: Backend handles entry logic
        if (scanner == null) return;

        var all = scanner.GetAll();
        if (all.Count == 0) return;

        var closest = all[0];
        if (closest.Distance <= ENTER_DISTANCE_THRESHOLD)
        {
            Debug.Log($"[LocationsPanel] Entering location: {closest.Name}");
            StartCoroutine(SendEnterPing(closest));
        }
        */
    }

    /* DISABLED: Backend handles entry logic
    System.Collections.IEnumerator SendEnterPing(TransmitterScanner.TransmitterData location)
    {
        // ... (method body) ...
    }
    */

    void RefreshList()
    {
        if (scanner == null)
        {
            Debug.LogError("[LocationsPanel] Scanner is null!");
            return;
        }

        // Clear existing items
        foreach (Transform child in listContainer)
        {
            Destroy(child.gameObject);
        }

        var all = scanner.GetAll(); // Already sorted by distance

        if (currentFilter == "all")
        {
            RenderGroupedList(all);
        }
        else
        {
            foreach (var item in all)
            {
                Debug.Log($"[UI] Creating item: {item.Name} ({item.Distance:F1}m)");
                CreateListItem(item);
            }
        }

        if (all.Count == 0)
        {
            CreateListItem(new TransmitterScanner.TransmitterData { Name = "Scanning...", Class = "", Type = "", Distance = 0 });
        }
        
        // Force update layout (with recursion guard)
        if (!UILayoutManager.LayoutRebuildInProgress)
        {
            UILayoutManager.LayoutRebuildInProgress = true;
            try { LayoutRebuilder.ForceRebuildLayoutImmediate(listContainer); }
            finally { UILayoutManager.LayoutRebuildInProgress = false; }
        }
    }

    void CreateListItem(TransmitterScanner.TransmitterData data)
    {
        // Debug.Log($"[LocationsPanel] Creating item for: {data.Name}");
        GameObject itemObj = new GameObject($"Item_{data.Name}");
        itemObj.transform.SetParent(listContainer, false);
        
        // Ensure LayoutElement for VerticalLayoutGroup
        LayoutElement le = itemObj.AddComponent<LayoutElement>();
        le.minHeight = 140; // Increased to prevent clipping
        le.preferredHeight = 140;
        le.flexibleHeight = 0;
        
        Text text = itemObj.AddComponent<Text>();
        text.font = customFont ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 32; // Consistent size
        text.color = textColor;
        text.alignment = TextAnchor.MiddleLeft;
        
        // Use dynamic stride length if available
        float stride = 0.762f;
        var pedometer = FindFirstObjectByType<PedometerService>();
        if (pedometer != null) stride = pedometer.EstimatedStrideLength;

        // Calculate steps (meters / meters-per-step)
        int steps = Mathf.RoundToInt(data.Distance / stride);
        
        // Calculate miles (1 meter = 0.000621371 miles)
        float miles = data.Distance * 0.000621371f;
        string distStr;
        
        if (miles <= 0.4f)
        {
            // Show Steps if close enough
            distStr = $"{steps:N0} steps";
        }
        else
        {
            distStr = $"{miles:F1}mi";
        }
        
        string displayText = $"{data.Name}";
        if (data.Name != "Scanning...")
        {
             // Format: "• Name (Class -> Type)\n  [500 steps NW]"
             string classType = string.IsNullOrEmpty(data.Class) ? data.Type : $"{data.Class} -> {data.Type}";
             if (string.IsNullOrEmpty(classType)) classType = "POI";
             
             string typesDebug = "";
             if (data.TypesRaw != null && data.TypesRaw.Length > 0)
             {
                 typesDebug = $"\n  types: {string.Join(", ", data.TypesRaw.Take(6))}";
             }

             displayText = $"• {data.Name} ({classType})\n  [{distStr} {data.Direction}]{typesDebug}";
        }
        text.text = displayText;

        RectTransform rt = itemObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, 100); // Height of item
    }

    void RenderGroupedList(List<TransmitterScanner.TransmitterData> items)
    {
        var grouped = new Dictionary<string, List<TransmitterScanner.TransmitterData>>
        {
            { "coffee", new List<TransmitterScanner.TransmitterData>() },
            { "bar", new List<TransmitterScanner.TransmitterData>() },
            { "food", new List<TransmitterScanner.TransmitterData>() },
            { "convenience", new List<TransmitterScanner.TransmitterData>() },
            { "other", new List<TransmitterScanner.TransmitterData>() }
        };

        foreach (var item in items)
        {
            var key = string.IsNullOrEmpty(item.MainCategoryGroup) ? "other" : item.MainCategoryGroup;
            if (!grouped.ContainsKey(key))
            {
                grouped[key] = new List<TransmitterScanner.TransmitterData>();
            }
            grouped[key].Add(item);
        }

        RenderGroupSection("☕ Coffee & Cafes", grouped["coffee"]);
        RenderGroupSection("🍺 Bars & Pubs", grouped["bar"]);
        RenderGroupSection("🍽️ Food", grouped["food"]);
        RenderGroupSection("🏪 Convenience", grouped["convenience"]);
        RenderGroupSection("📍 Other", grouped["other"]);
    }

    void RenderGroupSection(string title, List<TransmitterScanner.TransmitterData> entries)
    {
        if (entries == null || entries.Count == 0) return;

        CreateHeaderItem(title);
        foreach (var item in entries)
        {
            Debug.Log($"[UI] Creating item: {item.Name} ({item.Distance:F1}m)");
            CreateListItem(item);
        }
    }

    void CreateHeaderItem(string title)
    {
        GameObject headerObj = new GameObject($"Header_{title}");
        headerObj.transform.SetParent(listContainer, false);

        LayoutElement le = headerObj.AddComponent<LayoutElement>();
        le.minHeight = 60;
        le.preferredHeight = 60;
        le.flexibleHeight = 0;

        Text text = headerObj.AddComponent<Text>();
        text.font = customFont ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 30;
        text.color = new Color(0.7f, 0.9f, 1.0f);
        text.alignment = TextAnchor.MiddleLeft;
        text.text = title;

        RectTransform rt = headerObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, 60);
    }
}
