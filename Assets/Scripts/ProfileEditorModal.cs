using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Comprehensive profile editor modal for user information
/// Updates firstName, callSign, instagram, web, and bio in Firestore
/// </summary>
public class ProfileEditorModal : MonoBehaviour
{
    [Header("UI Settings")]
    public Color panelColor = new Color(0.1f, 0.1f, 0.1f, 0.95f);
    public Color inputFieldColor = new Color(0.2f, 0.2f, 0.2f, 1f);
    public Color textColor = Color.white;
    public Color buttonColor = new Color(0.2f, 0.6f, 1.0f); // Blue
    public Color cancelButtonColor = new Color(0.6f, 0.1f, 0.1f); // Red
    public bool showDebugButton = true; // Enabled by default to show "U" button

    private GameObject canvasGO;
    private GameObject modalOverlay;
    private InputField firstNameInput;
    private InputField callSignInput;
    private InputField instagramInput;
    private InputField webInput;
    private InputField bioInput;
    private Text statusText;
    private bool isOpen = false;
    private Button openButton;

    void Start()
    {
        CreateUI();
    }

    void CreateUI()
    {
        // Create persistent canvas
        canvasGO = new GameObject("ProfileEditorCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000; // Very high priority
        DontDestroyOnLoad(canvasGO);

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);

        canvasGO.AddComponent<GraphicRaycaster>();

        // Create open button (bottom-right corner)
        if (showDebugButton)
        {
            CreateOpenButton(canvas);
        }

        // Create modal overlay (hidden by default)
        CreateModal(canvas);

        modalOverlay.SetActive(false);
    }

    void CreateOpenButton(Canvas canvas)
    {
        GameObject btnContainer = new GameObject("ProfileButtonContainer");
        btnContainer.transform.SetParent(canvas.transform, false);

        RectTransform btnRect = btnContainer.AddComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(1, 1); // Top-right
        btnRect.anchorMax = new Vector2(1, 1);
        btnRect.pivot = new Vector2(1, 1);
        btnRect.anchoredPosition = new Vector2(-20, -60);
        btnRect.sizeDelta = new Vector2(100, 100);

        GameObject btnGO = new GameObject("ProfileButton");
        btnGO.transform.SetParent(btnContainer.transform, false);

        RectTransform btnRectInner = btnGO.AddComponent<RectTransform>();
        btnRectInner.anchorMin = Vector2.zero;
        btnRectInner.anchorMax = Vector2.one;
        btnRectInner.offsetMin = Vector2.zero;
        btnRectInner.offsetMax = Vector2.zero;

        Image img = btnGO.AddComponent<Image>();
        img.color = Color.clear;

        openButton = btnGO.AddComponent<Button>();
        openButton.onClick.AddListener(OpenModal);

        // "U" text for "User Profile"
        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(btnGO.transform, false);
        RectTransform textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;

        Text txt = textGO.AddComponent<Text>();
        txt.text = "\u2699";
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = new Color(1f, 1f, 1f, 0.7f);
        txt.fontSize = 50;

        Debug.Log("[ProfileEditorModal] Created profile button");
    }

    void CreateModal(Canvas canvas)
    {
        // Full-screen dark overlay
        modalOverlay = new GameObject("ProfileModalOverlay");
        modalOverlay.transform.SetParent(canvas.transform, false);

        Image overlayBg = modalOverlay.AddComponent<Image>();
        overlayBg.color = new Color(0, 0, 0, 0.8f);

        RectTransform overlayRect = modalOverlay.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        // Center panel
        GameObject panel = new GameObject("ProfilePanel");
        RectTransform panelRect = panel.AddComponent<RectTransform>(); // Add RectTransform FIRST
        panel.transform.SetParent(modalOverlay.transform, false);

        Image panelBg = panel.AddComponent<Image>();
        panelBg.color = panelColor;
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(900, 1400);

        // Header
        CreateText(panel.transform, "Header", "Edit Profile", 60, new Vector2(0, 600), new Vector2(800, 100), TextAnchor.MiddleCenter);

        // Input fields with labels
        float yPos = 480;
        float spacing = 160;

        CreateLabeledInput(panel.transform, "FirstName", "First Name:", ref firstNameInput, new Vector2(0, yPos), false);
        yPos -= spacing;

        CreateLabeledInput(panel.transform, "CallSign", "Call Sign:", ref callSignInput, new Vector2(0, yPos), false);
        yPos -= spacing;

        CreateLabeledInput(panel.transform, "Instagram", "Instagram:", ref instagramInput, new Vector2(0, yPos), false);
        yPos -= spacing;

        CreateLabeledInput(panel.transform, "Web", "Website:", ref webInput, new Vector2(0, yPos), false);
        yPos -= spacing;

        CreateLabeledInput(panel.transform, "Bio", "Bio:", ref bioInput, new Vector2(0, yPos), true, 300);

        // Status text
        GameObject statusGO = CreateText(panel.transform, "StatusText", "", 32, new Vector2(0, -420), new Vector2(800, 60), TextAnchor.MiddleCenter);
        statusText = statusGO.GetComponent<Text>();
        statusText.color = Color.yellow;

        // Buttons
        CreateButton(panel.transform, "SaveButton", "Save", new Vector2(-200, -580), new Vector2(300, 100), buttonColor, OnSaveClick);
        CreateButton(panel.transform, "CancelButton", "Cancel", new Vector2(200, -580), new Vector2(300, 100), cancelButtonColor, CloseModal);

        Debug.Log("[ProfileEditorModal] Created modal UI");
    }

    void CreateLabeledInput(Transform parent, string name, string labelText, ref InputField inputField, Vector2 position, bool multiline = false, float height = 80)
    {
        // Label
        CreateText(parent, $"{name}Label", labelText, 40, position + new Vector2(-300, 0), new Vector2(200, 60), TextAnchor.MiddleLeft);

        // Input field
        GameObject inputGO = new GameObject($"{name}Input");
        inputGO.transform.SetParent(parent, false);

        RectTransform inputRect = inputGO.AddComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0.5f, 0.5f);
        inputRect.anchorMax = new Vector2(0.5f, 0.5f);
        inputRect.pivot = new Vector2(0.5f, 0.5f);
        inputRect.anchoredPosition = position + new Vector2(100, 0);
        inputRect.sizeDelta = new Vector2(600, height);

        Image inputBg = inputGO.AddComponent<Image>();
        inputBg.color = inputFieldColor;

        inputField = inputGO.AddComponent<InputField>();
        inputField.textComponent = CreateText(inputGO.transform, "Text", "", 36, Vector2.zero, new Vector2(-20, -20), multiline ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft, false).GetComponent<Text>();

        if (multiline)
        {
            inputField.lineType = InputField.LineType.MultiLineNewline;
            inputField.textComponent.horizontalOverflow = HorizontalWrapMode.Wrap;
            inputField.textComponent.verticalOverflow = VerticalWrapMode.Overflow;
        }

        // Placeholder
        GameObject placeholderGO = CreateText(inputGO.transform, "Placeholder", $"Enter {labelText.TrimEnd(':')}...", 36, Vector2.zero, new Vector2(-20, -20), multiline ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft, false);
        Text placeholderText = placeholderGO.GetComponent<Text>();
        placeholderText.color = new Color(0.5f, 0.5f, 0.5f, 0.7f);
        inputField.placeholder = placeholderText;
    }

    GameObject CreateText(Transform parent, string name, string text, int fontSize, Vector2 position, Vector2 size, TextAnchor alignment, bool addRect = true, Color? color = null)
    {
        GameObject textGO = new GameObject(name);
        textGO.transform.SetParent(parent, false);

        if (addRect)
        {
            RectTransform textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = position;
            textRect.sizeDelta = size;
        }
        else
        {
            RectTransform textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10, 10);
            textRect.offsetMax = new Vector2(-10, -10);
        }

        Text txt = textGO.AddComponent<Text>();
        txt.text = text;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = fontSize;
        txt.color = color ?? textColor;
        txt.alignment = alignment;

        return textGO;
    }

    void CreateButton(Transform parent, string name, string label, Vector2 position, Vector2 size, Color color, UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnGO = new GameObject(name);
        btnGO.transform.SetParent(parent, false);

        RectTransform btnRect = btnGO.AddComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.5f, 0.5f);
        btnRect.anchorMax = new Vector2(0.5f, 0.5f);
        btnRect.pivot = new Vector2(0.5f, 0.5f);
        btnRect.anchoredPosition = position;
        btnRect.sizeDelta = size;

        Image btnBg = btnGO.AddComponent<Image>();
        btnBg.color = color;

        Button btn = btnGO.AddComponent<Button>();
        btn.onClick.AddListener(onClick);

        CreateText(btnGO.transform, "Text", label, 40, Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter, false);
    }

    public void OpenModal()
    {
        if (isOpen) return;

        isOpen = true;
        modalOverlay.SetActive(true);
        statusText.text = "";

        // Load current profile data
        StartCoroutine(LoadProfileData());

        Debug.Log("[ProfileEditorModal] Modal opened");
    }

    public void CloseModal()
    {
        isOpen = false;
        modalOverlay.SetActive(false);
        Debug.Log("[ProfileEditorModal] Modal closed");
    }

    IEnumerator LoadProfileData()
    {
        // Get User ID (Firebase UID or device ID fallback)
        string userId = DeviceIDManager.Instance.GetCurrentUserId();

        statusText.text = "Loading profile...";
        statusText.color = Color.yellow;

        // Check if FirebaseRestClient is available
        if (FirebaseRestClient.Instance == null)
        {
            Debug.LogWarning("[ProfileEditorModal] FirebaseRestClient not available");
            statusText.text = "Firebase not initialized";
            statusText.color = Color.red;
            yield break;
        }

        // Fetch current profile from Firestore
        FirebaseRestClient.Instance.GetFirestoreData("users", userId,
            (response) => {
                try
                {
                    var profile = JsonUtility.FromJson<UserProfile>(response);

                    firstNameInput.text = profile.firstName ?? "";
                    callSignInput.text = profile.callSign ?? "";
                    instagramInput.text = profile.instagram ?? "";
                    webInput.text = profile.web ?? "";
                    bioInput.text = profile.bio ?? "";

                    statusText.text = "";
                    Debug.Log("[ProfileEditorModal] Profile loaded successfully");
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[ProfileEditorModal] Failed to parse profile: {e.Message}");
                    statusText.text = "Failed to load profile data";
                    statusText.color = Color.red;
                }
            },
            (error) => {
                Debug.LogWarning($"[ProfileEditorModal] Failed to load profile: {error}");
                statusText.text = "Could not load profile (using defaults)";
                statusText.color = Color.yellow;
            }
        );

        yield break;
    }

    void OnSaveClick()
    {
        StartCoroutine(SaveProfile());
    }

    IEnumerator SaveProfile()
    {
        // Get User ID (Firebase UID or device ID fallback)
        string userId = DeviceIDManager.Instance.GetCurrentUserId();

        // Check if FirebaseRestClient is available
        if (FirebaseRestClient.Instance == null)
        {
            statusText.text = "Firebase not initialized";
            statusText.color = Color.red;
            Debug.LogError("[ProfileEditorModal] FirebaseRestClient not available");
            yield break;
        }

        // Build data object
        var profileData = new System.Collections.Generic.Dictionary<string, object>
        {
            { "userId", userId },
            { "firstName", firstNameInput.text.Trim() },
            { "callSign", callSignInput.text.Trim() },
            { "instagram", instagramInput.text.Trim() },
            { "web", webInput.text.Trim() },
            { "bio", bioInput.text.Trim() }
        };

        statusText.text = "Saving...";
        statusText.color = Color.yellow;

        Debug.Log($"[ProfileEditorModal] Saving profile to Firestore: {userId}");

        // Write directly to Firestore
        FirebaseRestClient.Instance.SetFirestoreData("users", userId, profileData,
            (response) => {
                statusText.text = "✓ Saved successfully!";
                statusText.color = Color.green;
                Debug.Log($"[ProfileEditorModal] Profile saved: {response}");

                // Auto-close after 2 seconds
                StartCoroutine(AutoClose());
            },
            (error) => {
                statusText.text = $"✗ Save failed: {error}";
                statusText.color = Color.red;
                Debug.LogError($"[ProfileEditorModal] Save failed: {error}");
            }
        );

        yield break;
    }

    IEnumerator AutoClose()
    {
        yield return new WaitForSeconds(2f);
        CloseModal();
    }

    [System.Serializable]
    private class UserProfile
    {
        public string firstName;
        public string callSign;
        public string instagram;
        public string web;
        public string bio;
    }
}
