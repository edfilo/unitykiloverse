using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class OnboardingUI : MonoBehaviour
{
    private GameObject canvasGO;
    private Button enterButton;
    private Text enterText;
    public Font customFont;

    // References to UIs we want to hide
    private Canvas pedometerCanvas;
    private Canvas locationsCanvas;
    private Canvas teleportCanvas;

    void Start()
    {
        // BYPASS: Skip onboarding entirely for now (button not working on iOS)
        Debug.Log("[OnboardingUI] BYPASSED - destroying immediately");
        PlayerPrefs.SetInt("HasEnteredKiloverse", 1);
        PlayerPrefs.Save();
        Destroy(gameObject);
        return;

        // Old code kept for reference:
        /*
        bool hasEntered = PlayerPrefs.GetInt("HasEnteredKiloverse", 0) == 1;
        Debug.Log($"[OnboardingUI] Checking status. HasEntered: {hasEntered}");

        if (hasEntered)
        {
            Debug.Log("[OnboardingUI] Skipping onboarding (Already entered before).");
            Destroy(gameObject);
            return;
        }

        EnsureEventSystem();
        CreateUI();
        HideOtherUIs();
        */
    }

    void EnsureEventSystem()
    {
        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            Debug.Log("[OnboardingUI] Created EventSystem for button input");
        }
        else
        {
            Debug.Log("[OnboardingUI] EventSystem already exists");
        }
    }

    void HideOtherUIs()
    {
        // Find other canvases by name or component
        GameObject pedometerGO = GameObject.Find("PedometerCanvas");
        if (pedometerGO != null) 
        {
            pedometerCanvas = pedometerGO.GetComponent<Canvas>();
            if (pedometerCanvas != null) pedometerCanvas.enabled = false;
        }

        GameObject locationsGO = GameObject.Find("LocationsCanvas");
        if (locationsGO != null) 
        {
            locationsCanvas = locationsGO.GetComponent<Canvas>();
            if (locationsCanvas != null) locationsCanvas.enabled = false;
        }

        GameObject teleportGO = GameObject.Find("TeleportCanvas");
        if (teleportGO != null) 
        {
            teleportCanvas = teleportGO.GetComponent<Canvas>();
            if (teleportCanvas != null) teleportCanvas.enabled = false;
        }
    }

    void ShowOtherUIs()
    {
        if (pedometerCanvas != null) pedometerCanvas.enabled = true;
        if (locationsCanvas != null) locationsCanvas.enabled = true;
        if (teleportCanvas != null) teleportCanvas.enabled = true;
    }

    void CreateUI()
    {
        canvasGO = new GameObject("OnboardingCanvas");
        canvasGO.transform.SetParent(this.transform); // Keep hierarchy clean
        
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 4000; // Topmost
        
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f; // Balance between width and height
        
        canvasGO.AddComponent<GraphicRaycaster>();

        // Center Button
        GameObject buttonObj = new GameObject("EnterButton");
        buttonObj.transform.SetParent(canvasGO.transform, false);
        
        Image btnImg = buttonObj.AddComponent<Image>();
        btnImg.color = Color.clear; 
        
        enterButton = buttonObj.AddComponent<Button>();
        enterButton.onClick.AddListener(OnEnterClicked);

        RectTransform btnRect = buttonObj.GetComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0, 0);
        btnRect.anchorMax = new Vector2(1, 1); // Make it full screen for easy clicking
        btnRect.offsetMin = Vector2.zero;
        btnRect.offsetMax = Vector2.zero;

        // Text
        GameObject textObj = new GameObject("EnterText");
        textObj.transform.SetParent(buttonObj.transform, false);
        enterText = textObj.AddComponent<Text>();
        enterText.text = "E N T E R";
        
        // Font Fallback Strategy (Unity 6 removed Arial.ttf, use LegacyRuntime.ttf)
        if (customFont != null) {
            enterText.font = customFont;
        } else {
            try {
                enterText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            } catch {
                enterText.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); // Old Unity versions
            }
        }

        enterText.fontSize = 80;
        enterText.color = new Color(1f, 1f, 1f, 0.8f); // Ghostly white
        enterText.alignment = TextAnchor.MiddleCenter;
        
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }

    void OnEnterClicked()
    {
        Debug.Log("[OnboardingUI] ========== ENTER BUTTON CLICKED ==========");
        Debug.Log("[OnboardingUI] User tapped the screen to enter Kiloverse");
        PlayerPrefs.SetInt("HasEnteredKiloverse", 1);
        PlayerPrefs.Save();
        Debug.Log("[OnboardingUI] Saved HasEnteredKiloverse flag");

        ShowOtherUIs();
        Destroy(canvasGO);
        Destroy(gameObject);
        Debug.Log("[OnboardingUI] Onboarding complete - UI destroyed");
    }
}
