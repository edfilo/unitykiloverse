using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Manages vertical layout of UI components with auto-sizing
    /// Layout order: Weather -> Stories -> Steps -> Locations -> Message
/// Each component uses ContentSizeFitter for auto-height based on content
/// </summary>
public class UILayoutManager : MonoBehaviour
{
    [Header("References")]
    public WeatherView weatherView;
    public StepsView stepsView;
    public LocationsPanel locationsPanel;
    public MessageView messageView;

    private GameObject canvasGO;
    private GameObject layoutContainerGO;
    private RectTransform layoutContainerRect;
    /// <summary>Shared recursion guard - prevents layout rebuilds from triggering cascading rebuilds.</summary>
    public static bool LayoutRebuildInProgress { get; set; }

    void Start()
    {
        Debug.Log("[UILayoutManager] Start called - initializing layout...");
        StartCoroutine(InitializeLayout());
    }

    IEnumerator InitializeLayout()
    {
        Debug.Log("[UILayoutManager] Waiting for bootstrapper...");
        yield return new WaitForSeconds(0.5f);

        CreateCanvas();
        yield return null;
        CreateLayoutContainer();
        yield return null;
        InstantiateViews();
        yield return null;
        ApplySafeAreaOffset();
        yield return null; // Let layout settle before heavy Stories build
        EnsureStoriesUI(); // Stories last - Build() does most work (sprites, many GameObjects)
        yield return null;
        StartCoroutine(DeferredLocationsPanel()); // LocationsPanel after boot (avoids lockup)
        Debug.Log("[UILayoutManager] Initialization complete.");
    }

    void CreateCanvas()
    {
        // Use GameObject.Find by name - much faster than FindObjectsOfType<Canvas> which searches ALL objects
        var pedometerCanvas = GameObject.Find("PedometerCanvas");
        var storiesCanvas = GameObject.Find("StoriesCanvas");
        if (pedometerCanvas != null)
        {
            canvasGO = pedometerCanvas;
            Debug.Log("[UILayoutManager] Found existing PedometerCanvas, using it");
        }
        else if (storiesCanvas != null)
        {
            canvasGO = storiesCanvas;
            Debug.Log("[UILayoutManager] Found existing StoriesCanvas, using it");
        }

        if (canvasGO == null)
        {
            // Create shared canvas - use PedometerCanvas name for StoriesUIBootstrapper compatibility
            canvasGO = new GameObject("PedometerCanvas");
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 2999; // Just below Stories to avoid conflicts

            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();
            Debug.Log("[UILayoutManager] Created PedometerCanvas for UI components");
        }

        // Ensure EventSystem exists - GameObject.Find is faster than FindFirstObjectByType for singletons
        if (GameObject.Find("EventSystem") == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }
    }

    IEnumerator DeferredLocationsPanel()
    {
        // Wait for boot to complete so LocationsPanel's CreateUI (Resources.Load, layout) doesn't block
        while (!BootState.AllowPlayer)
        {
            yield return new WaitForSeconds(0.5f);
        }
        yield return new WaitForSeconds(3f); // Buffer after AllowPlayer

        var existingPanel = FindFirstObjectByType<LocationsPanel>();
        if (existingPanel != null)
        {
            locationsPanel = existingPanel;
            locationsPanel.transform.SetParent(layoutContainerGO.transform, false);
            Debug.Log("[UILayoutManager] Using existing LocationsPanel (deferred)");
        }
        else if (locationsPanel == null)
        {
            Debug.Log("[UILayoutManager] Creating LocationsPanel (deferred after boot)...");
            GameObject locationsGO = new GameObject("LocationsPanel");
            locationsGO.AddComponent<RectTransform>();
            locationsGO.transform.SetParent(layoutContainerGO.transform, false);
            locationsPanel = locationsGO.AddComponent<LocationsPanel>();
        }
        else
        {
            locationsPanel.transform.SetParent(layoutContainerGO.transform, false);
        }
    }

    void EnsureStoriesUI()
    {
        var storiesBootstrapper = FindFirstObjectByType<KiloWorld.UI.Stories.StoriesUIBootstrapper>();
        if (storiesBootstrapper == null)
        {
            GameObject storiesGO = new GameObject("StoriesUI");
            storiesBootstrapper = storiesGO.AddComponent<KiloWorld.UI.Stories.StoriesUIBootstrapper>();
            Debug.Log("[UILayoutManager] Created StoriesUIBootstrapper.");
        }

        storiesBootstrapper.Build();
    }

    void CreateLayoutContainer()
    {
        // Create parent container for vertical layout
        layoutContainerGO = new GameObject("LayoutContainer");
        layoutContainerGO.transform.SetParent(canvasGO.transform, false);
        layoutContainerGO.layer = 5; // UI Layer

        layoutContainerRect = layoutContainerGO.AddComponent<RectTransform>();
        layoutContainerRect.anchorMin = new Vector2(0, 1); // Top-left
        layoutContainerRect.anchorMax = new Vector2(0, 1);
        layoutContainerRect.pivot = new Vector2(0, 1);
        layoutContainerRect.sizeDelta = new Vector2(900, 1000); // Initial size, will grow
        layoutContainerRect.anchoredPosition = new Vector2(20, -20); // Padding from top-left

        // Add VerticalLayoutGroup for auto-stacking
        VerticalLayoutGroup vlg = layoutContainerGO.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.spacing = 20; // Space between components
        vlg.childControlHeight = true; // Use child preferred heights
        vlg.childControlWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = true; // Force children to expand to full width

        // Add ContentSizeFitter so container grows with children
        ContentSizeFitter fitter = layoutContainerGO.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        Debug.Log("[UILayoutManager] Created layout container with VerticalLayoutGroup");
    }

    void InstantiateViews()
    {
        Debug.Log("[UILayoutManager] InstantiateViews: Starting...");
        // 1. Weather View (positioned separately at top, NOT in vertical layout)
        if (weatherView == null)
        {
            Debug.Log("[UILayoutManager] Creating new WeatherView...");
            GameObject weatherGO = new GameObject("WeatherView");
            weatherGO.AddComponent<RectTransform>(); // Add RectTransform for UI
            weatherGO.transform.SetParent(canvasGO.transform, false); // Parent to canvas, not layout container
            weatherView = weatherGO.AddComponent<WeatherView>();
        }
        else
        {
            Debug.Log("[UILayoutManager] Using existing WeatherView...");
            weatherView.transform.SetParent(canvasGO.transform, false); // Parent to canvas, not layout container
        }

        // Position Weather at the very top with safe area
        RectTransform weatherRect = weatherView.GetComponent<RectTransform>();
        if (weatherRect != null)
        {
            float safeAreaOffset = GetSafeAreaOffset();
            float topEdgePadding = 50f; // Increased top padding (was 0)
            weatherRect.anchorMin = new Vector2(0, 1);
            weatherRect.anchorMax = new Vector2(0, 1);
            weatherRect.pivot = new Vector2(0, 1);
            weatherRect.anchoredPosition = new Vector2(20, -(safeAreaOffset + topEdgePadding));
            Debug.Log($"[UILayoutManager] WeatherView positioned at {weatherRect.anchoredPosition}");
        }

        // 2. Stories strip will be positioned after we know weather height
        Debug.Log("[UILayoutManager] Starting IntegrateStoriesButton coroutine...");
        StartCoroutine(IntegrateStoriesButton());

        // 3. Steps View (positioned separately at top-right)
        if (stepsView == null)
        {
            GameObject stepsGO = new GameObject("StepsView");
            stepsGO.AddComponent<RectTransform>(); // Add RectTransform for UI
            stepsGO.transform.SetParent(canvasGO.transform, false); // Parent to canvas
            stepsView = stepsGO.AddComponent<StepsView>();
        }
        else
        {
            stepsView.transform.SetParent(canvasGO.transform, false);
        }

        // Position Steps at the top-right
        RectTransform stepsRect = stepsView.GetComponent<RectTransform>();
        if (stepsRect != null)
        {
            float safeAreaOffset = GetSafeAreaOffset();
            float topEdgePadding = 50f; // Increased top padding (was 0)
            stepsRect.anchorMin = new Vector2(1, 1); // Top-right
            stepsRect.anchorMax = new Vector2(1, 1);
            stepsRect.pivot = new Vector2(1, 1);
            stepsRect.anchoredPosition = new Vector2(-20, -(safeAreaOffset + topEdgePadding));
            Debug.Log($"[UILayoutManager] StepsView positioned at {stepsRect.anchoredPosition}");
        }

        // 4. Locations Panel - deferred to DeferredLocationsPanel() coroutine (avoids boot lockup)

        // 5. Message View (bottom)
        if (messageView == null)
        {
            Debug.Log("[UILayoutManager] Creating new MessageView...");
            GameObject messageGO = new GameObject("MessageView");
            messageGO.AddComponent<RectTransform>(); // Add RectTransform for UI
            messageGO.transform.SetParent(layoutContainerGO.transform, false);
            messageView = messageGO.AddComponent<MessageView>();
        }
        else
        {
            messageView.transform.SetParent(layoutContainerGO.transform, false);
        }

        Debug.Log("[UILayoutManager] InstantiateViews: Complete.");
    }

    void ApplySafeAreaOffset()
    {
        // Calculate safe area offset for top of screen
        float topInset = GetSafeAreaOffset();
        float topEdgePadding = 50f; // Match InstantiateViews() padding

        // Update Weather position (it's at the very top)
        if (weatherView != null)
        {
            RectTransform weatherRect = weatherView.GetComponent<RectTransform>();
            if (weatherRect != null)
            {
                weatherRect.anchoredPosition = new Vector2(20, -(topInset + topEdgePadding));
            }

            // Also tell WeatherView about safe area
            weatherView.ApplySafeAreaOffset();
        }

        // Layout container position is handled in IntegrateStoriesButton()
        // after we know the heights of Weather and Stories

        Debug.Log($"[UILayoutManager] Applied safe area offset: {topInset}px");
    }

    void Update()
    {
        if (!BootState.PostBootGracePeriodElapsed) return;
        if (LayoutRebuildInProgress) return;
        if (layoutContainerRect != null && Time.frameCount % 60 == 0) // Reduced from 30 to 60 (every ~1s)
        {
            LayoutRebuildInProgress = true;
            try { LayoutRebuilder.ForceRebuildLayoutImmediate(layoutContainerRect); }
            finally { LayoutRebuildInProgress = false; }
        }
    }

    IEnumerator IntegrateStoriesButton()
    {
        // Wait for StoriesUI to create its strip and WeatherView to set up
        // Since we already wait 0.5s in InitializeLayout, don't wait too long here
        yield return new WaitForSeconds(0.2f);

        float safeAreaOffset = GetSafeAreaOffset();
        float topEdgePadding = 0f;
        Canvas.ForceUpdateCanvases(); // Ensure layout is up to date
        float weatherHeight = GetWeatherHeight();
        float weatherToStoriesGap = 8f;
        float storiesToLayoutGap = 16f;

        // Find the Stories strip root (it's created by StoriesUIBootstrapper)
        GameObject storiesStrip = GameObject.Find("StoriesStripRoot");

        if (storiesStrip != null)
        {
            // Position Stories below Weather
            RectTransform storiesRect = storiesStrip.GetComponent<RectTransform>();
            float stripHeight = 240f; // From StoriesUIBootstrapper

            if (storiesRect != null)
            {
                if (storiesRect.rect.height > 0f)
                {
                    stripHeight = storiesRect.rect.height;
                }

                float storiesTop = safeAreaOffset + topEdgePadding + weatherHeight + weatherToStoriesGap;
                storiesRect.anchoredPosition = new Vector2(0f, -storiesTop);
                Debug.Log($"[UILayoutManager] Positioned Stories at Y={storiesRect.anchoredPosition.y}, height={stripHeight}");
            }

            // Position our vertical layout container below Stories strip
            if (layoutContainerRect != null)
            {
                float storiesBottom = Mathf.Abs(storiesRect != null ? storiesRect.anchoredPosition.y : 0f) + stripHeight;
                float totalOffset = storiesBottom + storiesToLayoutGap;
                layoutContainerRect.anchoredPosition = new Vector2(20, -totalOffset);
                Debug.Log($"[UILayoutManager] Positioned layout container at Y={-totalOffset}");
            }

            Debug.Log("[UILayoutManager] Layout order: SafeArea -> Weather -> Stories -> Steps/Locations/Message");
        }
        else
        {
            // No Stories strip, position layout container below Weather only
            if (layoutContainerRect != null)
            {
                float totalOffset = safeAreaOffset + topEdgePadding + weatherHeight + 40f;
                layoutContainerRect.anchoredPosition = new Vector2(20, -totalOffset);
                Debug.Log("[UILayoutManager] No Stories found, positioned layout below Weather");
            }
        }
    }

    float GetWeatherHeight()
    {
        float fallbackHeight = 100f;
        if (weatherView == null) return fallbackHeight;

        RectTransform weatherRect = weatherView.GetComponent<RectTransform>();
        RectTransform[] rects = weatherView.GetComponentsInChildren<RectTransform>();
        for (int i = 0; i < rects.Length; i++)
        {
            if (rects[i] == null || rects[i] == weatherRect) continue;
            if (rects[i].name == "WeatherContainer" && rects[i].rect.height > 0f)
            {
                return rects[i].rect.height;
            }
        }

        if (weatherRect != null && weatherRect.rect.height > 0f)
        {
            return weatherRect.rect.height;
        }

        for (int i = 0; i < rects.Length; i++)
        {
            if (rects[i] == null || rects[i] == weatherRect) continue;
            if (rects[i].rect.height > 0f)
            {
                return rects[i].rect.height;
            }
        }

        return fallbackHeight;
    }

    float GetSafeAreaOffset()
    {
        float topInsetPixels = Screen.height - Screen.safeArea.yMax;
        if (topInsetPixels < 0f) topInsetPixels = 0f;

        Canvas canvas = canvasGO.GetComponent<Canvas>();
        float scaleFactor = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
        return topInsetPixels / scaleFactor;
    }
}
