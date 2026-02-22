using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Displays ping response messages with auto-height using ContentSizeFitter
/// </summary>
public class MessageView : MonoBehaviour
{
    [Header("UI Settings")]
    public TMP_FontAsset customFont;
    public Color backgroundColor = new Color(0, 0, 0, 0.4f);
    public Color textColor = Color.white;
    public int fontSize = 42;

    private GameObject containerObj;
    private TextMeshProUGUI messageText;
    private RectTransform containerRect;
    private RectTransform rootRect;
    private LayoutElement rootLayout;

    // Singleton access for PingButton to update message
    public static MessageView Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        CreateUI();
    }

    void CreateUI()
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
        rootRect.sizeDelta = new Vector2(800, 0);

        rootLayout = GetComponent<LayoutElement>();
        if (rootLayout == null)
        {
            rootLayout = gameObject.AddComponent<LayoutElement>();
        }
        rootLayout.minHeight = 0f;
        rootLayout.preferredHeight = 0f;
        rootLayout.flexibleHeight = 0f;

        // Create container with background
        containerObj = new GameObject("MessageContainer");
        containerObj.transform.SetParent(transform, false);
        containerObj.layer = 5; // UI Layer

        Image bg = containerObj.AddComponent<Image>();
        bg.color = backgroundColor;
        bg.raycastTarget = false;

        containerRect = containerObj.GetComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0, 1); // Top-left
        containerRect.anchorMax = new Vector2(0, 1);
        containerRect.pivot = new Vector2(0, 1);
        containerRect.sizeDelta = new Vector2(800, 100); // Wider for messages

        // Add VerticalLayoutGroup
        VerticalLayoutGroup vlg = containerObj.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(10, 10, 10, 10);
        vlg.spacing = 5;
        vlg.childControlHeight = true;
        vlg.childControlWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = true;

        // Add ContentSizeFitter for auto-height
        ContentSizeFitter fitter = containerObj.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        // Message Text
        GameObject textObj = new GameObject("MessageText");
        textObj.transform.SetParent(containerObj.transform, false);
        messageText = textObj.AddComponent<TextMeshProUGUI>();
        messageText.font = customFont ?? TMP_Settings.defaultFontAsset;
        messageText.fontSize = fontSize;
        messageText.color = textColor;
        messageText.alignment = TextAlignmentOptions.TopLeft;
        messageText.text = ""; // Empty initially
        messageText.enableWordWrapping = true;
        messageText.richText = true;

        // No fixed height - will grow with content
        LayoutElement textLE = textObj.AddComponent<LayoutElement>();
        textLE.flexibleHeight = 1;

        // Hide container if no message
        containerObj.SetActive(false);

        UpdateRootLayout();
        Debug.Log("[MessageView] Created message view with auto-height");
    }

    /// <summary>
    /// Update message text (called by PingButton)
    /// </summary>
    public void SetMessage(string message, Color? color = null)
    {
        if (messageText != null)
        {
            messageText.text = message;
            messageText.color = color ?? textColor;

            // Show/hide container based on message content
            containerObj.SetActive(!string.IsNullOrEmpty(message));

            // Force layout rebuild (UpdateRootLayout has recursion guard)
            UpdateRootLayout();
        }
    }

    /// <summary>
    /// Clear message
    /// </summary>
    public void ClearMessage()
    {
        SetMessage("");
    }

    void UpdateRootLayout()
    {
        if (rootRect == null || rootLayout == null || containerRect == null) return;
        if (UILayoutManager.LayoutRebuildInProgress) return; // Recursion guard
        if (!containerObj.activeSelf)
        {
            rootLayout.minHeight = 0f;
            rootLayout.preferredHeight = 0f;
            rootRect.sizeDelta = new Vector2(rootRect.sizeDelta.x, 0f);
            return;
        }

        UILayoutManager.LayoutRebuildInProgress = true;
        try
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(containerRect);
            float height = Mathf.Max(containerRect.rect.height, 20f);
            rootRect.sizeDelta = new Vector2(rootRect.sizeDelta.x, height);
            rootLayout.minHeight = height;
            rootLayout.preferredHeight = height;
        }
        finally
        {
            UILayoutManager.LayoutRebuildInProgress = false;
        }
    }
}
