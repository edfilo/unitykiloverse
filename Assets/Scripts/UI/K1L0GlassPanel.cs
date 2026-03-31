using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class K1L0GlassPanel : MonoBehaviour, IDragHandler, IPointerDownHandler
{
    private static readonly Color TerminalBlack = new Color(0.01f, 0.03f, 0.01f, 0.94f);
    private static readonly Color TerminalGreen = new Color(0.47f, 1f, 0.54f, 0.92f);
    private static readonly Color TerminalBorder = new Color(0.33f, 0.98f, 0.42f, 0.34f);

    public RectTransform contentArea;
    public Image background;
    public System.Action OnCloseClicked;

    private K1L0GlassChrome chrome;
    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;
    private RectTransform closeRectTransform;
    private Canvas parentCanvas;
    private bool isShowing;
    private float animProgress;
    private Coroutine revealRoutine;
    private const float AnimDuration = 0.25f;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
    }

    public void InitializeVisual(TMP_FontAsset font, Vector4 contentPadding, bool addInnerAccent = true)
    {
        chrome = K1L0GlassFactory.AttachChrome(transform, "Panel", K1L0GlassFactory.PanelStyle);
        background = chrome.blurFill;
        chrome.blurFill.material = null;
        chrome.blurFill.color = new Color(0f, 0f, 0f, 0f);
        chrome.overlay.color = new Color(0f, 0f, 0f, 0f);
        chrome.border.color = new Color(0f, 0f, 0f, 0f);
        chrome.shadow.color = new Color(0f, 0f, 0f, 0f);
        chrome.accent.color = new Color(0f, 0f, 0f, 0f);
        chrome.sheen.color = new Color(0f, 0f, 0f, 0f);

        GameObject contentGO = new GameObject("Content");
        contentGO.transform.SetParent(transform, false);
        contentArea = contentGO.AddComponent<RectTransform>();
        contentArea.anchorMin = Vector2.zero;
        contentArea.anchorMax = Vector2.one;
        contentArea.offsetMin = new Vector2(contentPadding.x + 2f, contentPadding.y);
        contentArea.offsetMax = new Vector2(-contentPadding.z - 8f, -contentPadding.w);

        CreateCloseButton(font);
    }

    private void CreateCloseButton(TMP_FontAsset font)
    {
        GameObject closeGO = new GameObject("CloseBtn");
        closeGO.transform.SetParent(transform, false);
        RectTransform closeRT = closeGO.AddComponent<RectTransform>();
        closeRT.anchorMin = new Vector2(1, 1);
        closeRT.anchorMax = new Vector2(1, 1);
        closeRT.pivot = new Vector2(1, 1);
        closeRT.anchoredPosition = new Vector2(-10f, -10f);
        closeRT.sizeDelta = new Vector2(34f, 34f);
        closeRectTransform = closeRT;

        K1L0GlassChrome closeChrome = K1L0GlassFactory.AttachChrome(closeGO.transform, "Close", K1L0GlassFactory.ControlStyle);
        closeChrome.blurFill.material = null;
        closeChrome.blurFill.color = new Color(0.02f, 0.07f, 0.02f, 0.96f);
        closeChrome.overlay.color = new Color(0.01f, 0.04f, 0.01f, 0.90f);
        closeChrome.border.color = TerminalBorder;
        closeChrome.accent.color = new Color(0.24f, 1f, 0.34f, 0.06f);
        closeChrome.sheen.color = new Color(0.45f, 1f, 0.55f, 0.03f);
        closeChrome.blurFill.raycastTarget = true;

        Button closeBtn = closeGO.AddComponent<Button>();
        closeBtn.targetGraphic = closeChrome.blurFill;
        closeBtn.onClick.AddListener(() => OnCloseClicked?.Invoke());

        GameObject xGO = new GameObject("X");
        xGO.transform.SetParent(closeGO.transform, false);
        RectTransform xRT = xGO.AddComponent<RectTransform>();
        xRT.anchorMin = Vector2.zero;
        xRT.anchorMax = Vector2.one;
        xRT.offsetMin = Vector2.zero;
        xRT.offsetMax = Vector2.zero;

        TextMeshProUGUI xTMP = xGO.AddComponent<TextMeshProUGUI>();
        xTMP.text = "×";
        xTMP.font = font;
        xTMP.fontSize = 20;
        xTMP.color = TerminalGreen;
        xTMP.alignment = TextAlignmentOptions.Center;
        xTMP.raycastTarget = false;
    }

    // --- Drag support ---
    public void OnPointerDown(PointerEventData eventData)
    {
        // Bring to front
        transform.SetAsLastSibling();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (rectTransform == null) return;
        if (parentCanvas == null)
            parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas == null) return;

        Vector2 delta = eventData.delta;
        if (parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            delta /= parentCanvas.scaleFactor;

        rectTransform.anchoredPosition += delta;
    }

    void LateUpdate()
    {
        if (isShowing && animProgress < 1f)
        {
            animProgress = Mathf.Min(1f, animProgress + Time.deltaTime / AnimDuration);
            ApplyAnim();
        }
        else if (!isShowing && animProgress > 0f)
        {
            animProgress = Mathf.Max(0f, animProgress - Time.deltaTime / AnimDuration);
            ApplyAnim();
            if (animProgress <= 0f)
                gameObject.SetActive(false);
        }
    }

    void ApplyAnim()
    {
        float t = 1f - Mathf.Pow(1f - animProgress, 3f);
        canvasGroup.alpha = t;
        transform.localScale = Vector3.one * (0.95f + 0.05f * t);
    }

    public void Show()
    {
        gameObject.SetActive(true);
        isShowing = true;
        BeginTerminalReveal();
    }

    public void Hide()
    {
        isShowing = false;
        if (animProgress <= 0f)
            gameObject.SetActive(false);
    }

    public bool IsVisible => isShowing;

    private void BeginTerminalReveal()
    {
        if (revealRoutine != null)
            StopCoroutine(revealRoutine);

        revealRoutine = StartCoroutine(TerminalRevealRoutine());
    }

    private IEnumerator TerminalRevealRoutine()
    {
        List<TextMeshProUGUI> texts = new List<TextMeshProUGUI>(GetComponentsInChildren<TextMeshProUGUI>(true));
        List<Button> buttons = new List<Button>(GetComponentsInChildren<Button>(true));

        foreach (Button button in buttons)
        {
            CanvasGroup group = button.GetComponent<CanvasGroup>();
            if (group == null)
                group = button.gameObject.AddComponent<CanvasGroup>();

            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        foreach (TextMeshProUGUI text in texts)
        {
            if (text == null || text.textInfo == null)
                continue;

            text.ForceMeshUpdate();
            int totalChars = text.textInfo.characterCount;
            if (totalChars <= 0)
                continue;

            text.maxVisibleCharacters = 0;
            float visible = 0f;
            float charsPerSecond = 180f;
            while (visible < totalChars)
            {
                visible += charsPerSecond * Time.unscaledDeltaTime;
                text.maxVisibleCharacters = Mathf.Min(totalChars, Mathf.FloorToInt(visible));
                yield return null;
            }

            text.maxVisibleCharacters = int.MaxValue;
        }

        float fade = 0f;
        while (fade < 1f)
        {
            fade += Time.unscaledDeltaTime * 10f;
            foreach (Button button in buttons)
            {
                CanvasGroup group = button != null ? button.GetComponent<CanvasGroup>() : null;
                if (group == null)
                    continue;

                group.alpha = Mathf.Clamp01(fade);
            }
            yield return null;
        }

        foreach (Button button in buttons)
        {
            CanvasGroup group = button != null ? button.GetComponent<CanvasGroup>() : null;
            if (group == null)
                continue;

            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }

        revealRoutine = null;
    }

    public bool TryHandleScreenPoint(Vector2 screenPoint, Camera eventCamera)
    {
        if (!isShowing || !gameObject.activeInHierarchy || closeRectTransform == null)
            return false;

        if (!RectTransformUtility.RectangleContainsScreenPoint(closeRectTransform, screenPoint, eventCamera))
            return false;

        OnCloseClicked?.Invoke();
        return true;
    }

    public Rect GetScreenRect()
    {
        if (rectTransform == null) return Rect.zero;
        Vector2 pos = rectTransform.anchoredPosition;
        Vector2 size = rectTransform.sizeDelta;
        return new Rect(pos.x, pos.y - size.y, size.x, size.y);
    }
}
