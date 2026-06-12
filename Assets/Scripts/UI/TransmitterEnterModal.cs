using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityEngine.Video;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

public class TransmitterEnterModal : MonoBehaviour
{
    public static TransmitterEnterModal Instance { get; private set; }

    private TMP_FontAsset font;
    private GameObject root;
    private RectTransform panelRt;
    private RectTransform stackContentRt;
    private RectTransform listRt;
    private GameObject setupRowGO;
    private LayoutElement setupLayoutElement;
    private GameObject photoLineGO;
    private GameObject photoPreviewGO;
    private RawImage photoPreviewImage;
    private Button photoButton;
    private TextMeshProUGUI photoButtonLabel;
    private GameObject spiritsPromptGO;
    private GameObject spiritsGO;
    private GameObject selectedMaterialGO;
    private TextMeshProUGUI selectedMaterialLabel;
    private Button confirmMaterialButton;
    private TextMeshProUGUI photoLabel;
    private Slider spiritsSlider;
    private TextMeshProUGUI spiritsValueLabel;
    private GameObject inputRowGO;
    private GameObject buttonsRowGO;
    private TextMeshProUGUI promptLabel;
    private TMP_InputField actionInput;
    private Button submitButton;
    private Button closeButton;
    private TextMeshProUGUI statusLabel;
    private Button pttButton;
    private TextMeshProUGUI pttButtonLabel;
    private Button stopTransmitButton;
    private GameObject transmittingGO;
    private TextMeshProUGUI transmittingText;
    private TextMeshProUGUI transmittingSpinner;
    private GameObject videoPreviewGO;
    private RawImage videoPreviewImage;
    private VideoPlayer videoPreviewPlayer;
    private RenderTexture videoPreviewRT;
    private string currentPreviewVideoUrl;
    private int spinnerIndex;
    private float nextSpinnerTick;
    private bool isSubmitting;
    private bool isRecording;
    private bool contrastActive;
    private bool subscribedToTransmissionReady;
    private bool embeddedMode;
    private bool spiritsSelected;
    private bool suppressSpiritsChange;
    private string selectedPhotoPath;
    private string selectedPhotoUrl;
    private string selectedSpirits = "medium";
    private string pendingArtifact;
    private Coroutine elementTypeCoroutine;
    private Coroutine photoFallbackCoroutine;
    private float recordingStartTime;
    private string lastTranscript;
    private float nextTranscriptPoll;

    void Update()
    {
        if (isSubmitting && transmittingGO != null && transmittingGO.activeInHierarchy)
        {
            if (Time.unscaledTime >= nextSpinnerTick)
            {
                nextSpinnerTick = Time.unscaledTime + 0.09f;
                spinnerIndex = (spinnerIndex + 1) % 4;
                if (transmittingSpinner != null)
                    transmittingSpinner.text = spinnerIndex switch
                    {
                        0 => "|",
                        1 => "/",
                        2 => "-",
                        _ => "\\"
                    };
            }
        }

        if (!isRecording) return;
        if (root == null || !root.activeInHierarchy) return;

        if (Time.unscaledTime < nextTranscriptPoll) return;
        nextTranscriptPoll = Time.unscaledTime + 0.20f;

        string t = KiloSpeechRecognizer.GetLatestText();
        if (!string.IsNullOrEmpty(t) && t != lastTranscript)
        {
            lastTranscript = t;
            if (actionInput != null) actionInput.text = t;
            if (statusLabel != null) statusLabel.text = "Listening… (transcribing)";
        }
        else if (string.IsNullOrEmpty(lastTranscript))
        {
            float dur = Mathf.Max(0f, Time.unscaledTime - recordingStartTime);
            if (dur > 1.0f && statusLabel != null) statusLabel.text = "Listening… (no words yet)";
        }
    }

    private readonly List<GameObject> materialButtons = new List<GameObject>();
    private readonly List<Image> materialRowImages = new List<Image>();
    private readonly List<TextMeshProUGUI> materialRowLabels = new List<TextMeshProUGUI>();
    private Signal activeSignal;
    private string selectedArtifact;

    private static readonly Color TerminalGreen = new Color(0.56f, 1f, 0.62f, 1f);
    private static readonly Color TerminalDim = new Color(0.56f, 1f, 0.62f, 0.65f);

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void K1L0PickPhoto(string gameObjectName, string callbackName);
#endif

    private void ApplySafeAreaSizing()
    {
        if (panelRt == null) return;
        var canvas = root != null ? root.GetComponentInParent<Canvas>() : null;
        float scale = canvas != null ? Mathf.Max(0.0001f, canvas.scaleFactor) : 1f;
        Rect safe = Screen.safeArea;
        float safeW = safe.width / scale;
        float safeH = safe.height / scale;

        float w = safeW - 24f;
        float h = safeH - 24f;
        w = Mathf.Max(320f, w);
        h = Mathf.Max(420f, h);

        panelRt.sizeDelta = new Vector2(w, h);
        Vector2 safeCenter = safe.center;
        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        panelRt.anchoredPosition = (safeCenter - screenCenter) / scale;
    }

    public void Initialize()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        gameObject.name = "TransmitterEnterModalBridge";
        DontDestroyOnLoad(gameObject);

        font = Resources.Load<TMP_FontAsset>("Fonts/IBMPlexMono-Regular SDF");
        if (font == null) font = Resources.Load<TMP_FontAsset>("Fonts/Inter-Regular SDF");
        if (font == null) font = TMP_Settings.defaultFontAsset;

        BuildUI(null, false);
        SubscribeTransmissionReady();
        ApplySafeAreaSizing();
        Hide();
    }

    public void InitializeEmbedded(RectTransform parent)
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        gameObject.name = "TransmitterPanelBridge";
        embeddedMode = true;

        font = Resources.Load<TMP_FontAsset>("Fonts/IBMPlexMono-Regular SDF");
        if (font == null) font = Resources.Load<TMP_FontAsset>("Fonts/Inter-Regular SDF");
        if (font == null) font = TMP_Settings.defaultFontAsset;

        BuildUI(parent, true);
        SubscribeTransmissionReady();
        Hide();
    }

    void OnDestroy()
    {
        UnsubscribeTransmissionReady();
        if (videoPreviewPlayer != null)
            videoPreviewPlayer.prepareCompleted -= OnInlineVideoPrepared;
        if (videoPreviewRT != null)
        {
            videoPreviewRT.Release();
            Destroy(videoPreviewRT);
            videoPreviewRT = null;
        }
    }

    void BuildUI(RectTransform parentOverride, bool embedded)
    {
        root = new GameObject("TransmitterEnterModalUI", typeof(RectTransform));
        root.transform.SetParent(parentOverride != null ? parentOverride : K1L0CanvasRoot.HUDCanvas.transform, false);
        var rootRt = (RectTransform)root.transform;
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        // Dimmer
        if (!embedded)
        {
            var dimGO = new GameObject("Dim", typeof(RectTransform), typeof(Image));
            dimGO.transform.SetParent(root.transform, false);
            var dimRt = (RectTransform)dimGO.transform;
            dimRt.anchorMin = Vector2.zero;
            dimRt.anchorMax = Vector2.one;
            dimRt.offsetMin = Vector2.zero;
            dimRt.offsetMax = Vector2.zero;
            var dimImage = dimGO.GetComponent<Image>();
            dimImage.color = new Color(0f, 0f, 0f, 0.99f);
            dimImage.raycastTarget = false;
        }

        // Panel
        var panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(root.transform, false);
        panelRt = panel.GetComponent<RectTransform>();
        if (embedded)
        {
            panelRt.anchorMin = Vector2.zero;
            panelRt.anchorMax = Vector2.one;
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;
        }
        else
        {
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(680f, 760f);
            panelRt.anchoredPosition = Vector2.zero;
        }

        if (!embedded)
        {
            var chrome = K1L0GlassFactory.AttachChrome(panel.transform, "TxModal", K1L0GlassFactory.PanelStyle);
            if (chrome != null)
            {
                chrome.blurFill.color = new Color(0f, 0f, 0f, 0f);
                chrome.overlay.color = new Color(0f, 0f, 0f, 0f);
                chrome.border.color = new Color(0.56f, 1f, 0.62f, 0.18f);
                chrome.accent.color = new Color(0f, 0f, 0f, 0f);
                chrome.sheen.color = new Color(0f, 0f, 0f, 0f);
            }
        }
        var darkPlate = new GameObject("DarkPlate", typeof(RectTransform), typeof(Image));
        darkPlate.transform.SetParent(panel.transform, false);
        var darkPlateRt = darkPlate.GetComponent<RectTransform>();
        darkPlateRt.anchorMin = Vector2.zero;
        darkPlateRt.anchorMax = Vector2.one;
        darkPlateRt.offsetMin = Vector2.zero;
        darkPlateRt.offsetMax = Vector2.zero;
        var darkPlateImg = darkPlate.GetComponent<Image>();
        darkPlateImg.sprite = K1L0GlassFactory.ControlRectSprite;
        darkPlateImg.type = Image.Type.Sliced;
        darkPlateImg.color = new Color(0f, 0f, 0f, 0f);
        darkPlateImg.raycastTarget = false;
        if (!embedded) K1L0GlassFactory.AttachTerminalStatic(panel.transform, "TxStatic");

        // Title
        var titleGO = new GameObject("Title", typeof(RectTransform));
        titleGO.transform.SetParent(panel.transform, false);
        var titleRt = titleGO.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.sizeDelta = new Vector2(0f, 70f);
        titleRt.anchoredPosition = new Vector2(0f, -18f);

        var title = titleGO.AddComponent<TextMeshProUGUI>();
        title.font = font;
        title.fontSize = 18f;
        title.color = TerminalGreen;
        title.alignment = TextAlignmentOptions.TopLeft;
        title.text = "Transmitter";

        if (!embedded)
        {
            closeButton = K1L0GlassFactory.CreateTerminalCloseButton(panel.transform, font, Hide);
            var closeRt = closeButton.transform as RectTransform;
            if (closeRt != null)
            {
                closeRt.sizeDelta = new Vector2(48f, 48f);
                closeRt.anchoredPosition = new Vector2(-4f, -10f);
            }
        }

        var scrollGO = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect));
        scrollGO.transform.SetParent(panel.transform, false);
        var scrollRt = scrollGO.GetComponent<RectTransform>();
        scrollRt.anchorMin = Vector2.zero;
        scrollRt.anchorMax = Vector2.one;
        scrollRt.offsetMin = new Vector2(20f, 28f);
        scrollRt.offsetMax = new Vector2(-20f, -86f);

        var viewportGO = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        viewportGO.transform.SetParent(scrollGO.transform, false);
        var viewportRt = viewportGO.GetComponent<RectTransform>();
        viewportRt.anchorMin = Vector2.zero;
        viewportRt.anchorMax = Vector2.one;
        viewportRt.offsetMin = Vector2.zero;
        viewportRt.offsetMax = Vector2.zero;
        var viewportImage = viewportGO.GetComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0f);
        viewportImage.raycastTarget = true;

        var contentGO = new GameObject("Content", typeof(RectTransform));
        contentGO.transform.SetParent(viewportGO.transform, false);
        stackContentRt = contentGO.GetComponent<RectTransform>();
        stackContentRt.anchorMin = new Vector2(0f, 1f);
        stackContentRt.anchorMax = new Vector2(1f, 1f);
        stackContentRt.pivot = new Vector2(0.5f, 1f);
        stackContentRt.anchoredPosition = Vector2.zero;
        stackContentRt.sizeDelta = new Vector2(0f, 0f);

        var stackLayout = contentGO.AddComponent<VerticalLayoutGroup>();
        stackLayout.childAlignment = TextAnchor.UpperLeft;
        stackLayout.childControlWidth = true;
        stackLayout.childControlHeight = false;
        stackLayout.childForceExpandWidth = true;
        stackLayout.childForceExpandHeight = false;
        stackLayout.spacing = 0f;
        stackLayout.padding = new RectOffset(0, 0, 0, 0);

        var contentFitter = contentGO.AddComponent<ContentSizeFitter>();
        contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = scrollGO.GetComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.inertia = true;
        scroll.scrollSensitivity = 28f;
        scroll.viewport = viewportRt;
        scroll.content = stackContentRt;

        setupRowGO = new GameObject("SetupRow", typeof(RectTransform));
        setupRowGO.transform.SetParent(stackContentRt, false);
        var setupRt = setupRowGO.GetComponent<RectTransform>();
        setupRt.sizeDelta = new Vector2(0f, 104f);
        setupLayoutElement = setupRowGO.AddComponent<LayoutElement>();
        setupLayoutElement.preferredHeight = 104f;
        setupLayoutElement.flexibleHeight = 0f;

        var setupLayout = setupRowGO.AddComponent<VerticalLayoutGroup>();
        setupLayout.childAlignment = TextAnchor.UpperLeft;
        setupLayout.childControlWidth = true;
        setupLayout.childControlHeight = false;
        setupLayout.childForceExpandWidth = true;
        setupLayout.childForceExpandHeight = false;
        setupLayout.spacing = 0f;

        photoLineGO = new GameObject("PhotoLine", typeof(RectTransform));
        photoLineGO.transform.SetParent(setupRowGO.transform, false);
        photoLineGO.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 44f);
        var photoLine = photoLineGO.AddComponent<HorizontalLayoutGroup>();
        photoLine.childAlignment = TextAnchor.MiddleLeft;
        photoLine.childControlWidth = false;
        photoLine.childControlHeight = true;
        photoLine.childForceExpandWidth = false;
        photoLine.childForceExpandHeight = true;
        photoLine.spacing = 10f;

        photoButton = MakeButton(photoLineGO.transform, "Photo", "TAKE PHOTO");
        photoButtonLabel = photoButton.GetComponentInChildren<TextMeshProUGUI>(true);
        photoButton.GetComponent<RectTransform>().sizeDelta = new Vector2(190f, 44f);
        photoButton.onClick.AddListener(SelectPhoto);

        var photoStatusGO = new GameObject("PhotoStatus", typeof(RectTransform));
        photoStatusGO.transform.SetParent(photoLineGO.transform, false);
        photoStatusGO.GetComponent<RectTransform>().sizeDelta = new Vector2(130f, 44f);
        photoLabel = photoStatusGO.AddComponent<TextMeshProUGUI>();
        photoLabel.font = font;
        photoLabel.fontSize = 12f;
        photoLabel.color = TerminalDim;
        photoLabel.alignment = TextAlignmentOptions.BottomLeft;
        photoLabel.text = "no photo";
        photoLabel.enableWordWrapping = true;

        photoPreviewGO = new GameObject("PhotoPreview", typeof(RectTransform));
        photoPreviewGO.transform.SetParent(setupRowGO.transform, false);
        var previewRt = photoPreviewGO.GetComponent<RectTransform>();
        previewRt.sizeDelta = new Vector2(0f, 106f);
        var previewLayout = photoPreviewGO.AddComponent<HorizontalLayoutGroup>();
        previewLayout.childAlignment = TextAnchor.MiddleLeft;
        previewLayout.childControlWidth = false;
        previewLayout.childControlHeight = false;
        previewLayout.childForceExpandWidth = false;
        previewLayout.childForceExpandHeight = false;
        previewLayout.padding = new RectOffset(0, 0, 0, 10);

        var previewImageGO = new GameObject("PhotoPreviewImage", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
        previewImageGO.transform.SetParent(photoPreviewGO.transform, false);
        previewImageGO.GetComponent<RectTransform>().sizeDelta = new Vector2(72f, 96f);
        photoPreviewImage = previewImageGO.GetComponent<RawImage>();
        photoPreviewImage.color = Color.white;
        photoPreviewImage.raycastTarget = false;
        var photoFitter = previewImageGO.GetComponent<AspectRatioFitter>();
        photoFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        photoFitter.aspectRatio = 0.75f;
        photoPreviewGO.SetActive(false);

        spiritsPromptGO = new GameObject("SpiritsPrompt", typeof(RectTransform));
        spiritsPromptGO.transform.SetParent(setupRowGO.transform, false);
        spiritsPromptGO.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 30f);
        var spiritsPromptElement = spiritsPromptGO.AddComponent<LayoutElement>();
        spiritsPromptElement.preferredHeight = 30f;
        spiritsPromptElement.flexibleHeight = 0f;
        var spiritsPromptLabel = spiritsPromptGO.AddComponent<TextMeshProUGUI>();
        spiritsPromptLabel.font = font;
        spiritsPromptLabel.fontSize = 15f;
        spiritsPromptLabel.color = TerminalGreen;
        spiritsPromptLabel.alignment = TextAlignmentOptions.BottomLeft;
        spiritsPromptLabel.text = "How are your spirits?  Slide to adjust.";
        spiritsPromptLabel.enableWordWrapping = true;
        spiritsPromptGO.SetActive(false);

        spiritsGO = new GameObject("Spirits", typeof(RectTransform), typeof(Image), typeof(Slider));
        spiritsGO.transform.SetParent(setupRowGO.transform, false);
        spiritsGO.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 56f);
        var spiritsElement = spiritsGO.AddComponent<LayoutElement>();
        spiritsElement.preferredHeight = 56f;
        spiritsElement.flexibleWidth = 1f;
        var spiritsBg = spiritsGO.GetComponent<Image>();
        spiritsBg.sprite = null;
        spiritsBg.type = Image.Type.Simple;
        spiritsBg.color = new Color(0.72f, 0.75f, 0.72f, 1f);
        var spiritsBorder = spiritsGO.AddComponent<Outline>();
        spiritsBorder.effectColor = Color.white;
        spiritsBorder.effectDistance = new Vector2(2f, 2f);

        spiritsSlider = spiritsGO.GetComponent<Slider>();
        spiritsSlider.minValue = 0f;
        spiritsSlider.maxValue = 1f;
        spiritsSlider.value = 0.5f;
        spiritsSlider.targetGraphic = spiritsBg;
        spiritsSlider.onValueChanged.AddListener(_ => UpdateSpiritsPrompt());

        var fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillGO.transform.SetParent(spiritsGO.transform, false);
        var fillRt = fillGO.GetComponent<RectTransform>();
        fillRt.anchorMin = new Vector2(0f, 0f);
        fillRt.anchorMax = new Vector2(0.5f, 1f);
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;
        fillGO.GetComponent<Image>().color = new Color(0.08f, 0.78f, 0.20f, 0.88f);
        spiritsSlider.fillRect = fillRt;

        var handleGO = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleGO.transform.SetParent(spiritsGO.transform, false);
        var handleRt = handleGO.GetComponent<RectTransform>();
        handleRt.sizeDelta = new Vector2(20f, 52f);
        handleGO.GetComponent<Image>().color = Color.white;
        spiritsSlider.handleRect = handleRt;

        spiritsValueLabel = MakeOverlayLabel(spiritsGO.transform, "SpiritsLabel", "SPIRITS: MEDIUM");
        spiritsValueLabel.color = Color.black;

        // Rare-earth element list container
        var listGO = new GameObject("RareEarthList", typeof(RectTransform));
        listGO.transform.SetParent(stackContentRt, false);
        listRt = listGO.GetComponent<RectTransform>();
        listRt.sizeDelta = new Vector2(0f, 0f);
        var listElement = listGO.AddComponent<LayoutElement>();
        listElement.preferredHeight = 0f;
        listElement.flexibleHeight = 0f;

        var listLayout = listGO.AddComponent<VerticalLayoutGroup>();
        listLayout.childAlignment = TextAnchor.UpperLeft;
        listLayout.childControlWidth = true;
        listLayout.childControlHeight = true;
        listLayout.childForceExpandWidth = true;
        listLayout.childForceExpandHeight = false;
        listLayout.spacing = 0f;

        selectedMaterialGO = new GameObject("SelectedMaterial", typeof(RectTransform));
        selectedMaterialGO.transform.SetParent(stackContentRt, false);
        var selectedRt = selectedMaterialGO.GetComponent<RectTransform>();
        selectedRt.sizeDelta = new Vector2(0f, 88f);
        var selectedElement = selectedMaterialGO.AddComponent<LayoutElement>();
        selectedElement.preferredHeight = 88f;
        selectedElement.flexibleHeight = 0f;

        var selectedLayout = selectedMaterialGO.AddComponent<VerticalLayoutGroup>();
        selectedLayout.childAlignment = TextAnchor.UpperLeft;
        selectedLayout.childControlWidth = true;
        selectedLayout.childControlHeight = false;
        selectedLayout.childForceExpandWidth = true;
        selectedLayout.childForceExpandHeight = false;
        selectedLayout.spacing = 8f;

        var selectedBarGO = new GameObject("SelectedBar", typeof(RectTransform), typeof(Image));
        selectedBarGO.transform.SetParent(selectedMaterialGO.transform, false);
        selectedBarGO.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 38f);
        var selectedBarImg = selectedBarGO.GetComponent<Image>();
        selectedBarImg.sprite = K1L0GlassFactory.ControlRectSprite;
        selectedBarImg.type = Image.Type.Sliced;
        selectedBarImg.color = new Color(0.02f, 0.22f, 0.04f, 0.95f);

        selectedMaterialLabel = MakeOverlayLabel(selectedBarGO.transform, "SelectedLabel", "");
        selectedMaterialLabel.alignment = TextAlignmentOptions.MidlineLeft;
        selectedMaterialLabel.margin = new Vector4(12f, 0f, 12f, 0f);

        confirmMaterialButton = MakeButton(selectedMaterialGO.transform, "ConfirmMaterial", "SELECT");
        confirmMaterialButton.GetComponent<RectTransform>().sizeDelta = new Vector2(160f, 42f);
        confirmMaterialButton.onClick.AddListener(ConfirmSelectedMaterial);
        selectedMaterialGO.SetActive(false);

        // Prompt
        var promptGO = new GameObject("Prompt", typeof(RectTransform));
        promptGO.transform.SetParent(stackContentRt, false);
        var promptRt = promptGO.GetComponent<RectTransform>();
        promptRt.sizeDelta = new Vector2(0f, 64f);
        var promptElement = promptGO.AddComponent<LayoutElement>();
        promptElement.preferredHeight = 64f;
        promptElement.flexibleHeight = 0f;

        promptLabel = promptGO.AddComponent<TextMeshProUGUI>();
        promptLabel.font = font;
        promptLabel.fontSize = 18f;
        promptLabel.color = TerminalDim;
        promptLabel.alignment = TextAlignmentOptions.TopLeft;
        promptLabel.text = "Select a photo. Set spirits. Select an element to transmit.";

        // Input row
        inputRowGO = new GameObject("InputRow", typeof(RectTransform));
        inputRowGO.transform.SetParent(stackContentRt, false);
        var inputRt = inputRowGO.GetComponent<RectTransform>();
        inputRt.sizeDelta = new Vector2(0f, 112f);
        var inputElement = inputRowGO.AddComponent<LayoutElement>();
        inputElement.preferredHeight = 112f;
        inputElement.flexibleHeight = 0f;

        // Input background
        var inputBgGO = new GameObject("InputBg", typeof(RectTransform), typeof(Image));
        inputBgGO.transform.SetParent(inputRowGO.transform, false);
        var inputBgRt = inputBgGO.GetComponent<RectTransform>();
        inputBgRt.anchorMin = new Vector2(0f, 0f);
        inputBgRt.anchorMax = new Vector2(1f, 1f);
        inputBgRt.offsetMin = Vector2.zero;
        inputBgRt.offsetMax = Vector2.zero;
        var inputBgImg = inputBgGO.GetComponent<Image>();
        inputBgImg.sprite = K1L0GlassFactory.ControlRectSprite;
        inputBgImg.type = Image.Type.Sliced;
        inputBgImg.color = new Color(0.02f, 0.05f, 0.02f, 0.95f);

        // TMP InputField
        var tfGO = new GameObject("InputField", typeof(RectTransform));
        tfGO.transform.SetParent(inputRowGO.transform, false);
        var tfRt = tfGO.GetComponent<RectTransform>();
        tfRt.anchorMin = new Vector2(0f, 0f);
        tfRt.anchorMax = new Vector2(1f, 1f);
        tfRt.offsetMin = new Vector2(14f, 10f);
        tfRt.offsetMax = new Vector2(-14f, -10f);

        actionInput = tfGO.AddComponent<TMP_InputField>();
        actionInput.lineType = TMP_InputField.LineType.MultiLineNewline;

        var placeholder = MakeInputText(tfGO.transform, "Placeholder", "", TerminalDim);
        var text = MakeInputText(tfGO.transform, "Text", "", TerminalGreen);
        actionInput.placeholder = placeholder;
        actionInput.textComponent = text;
        actionInput.pointSize = 16f;
        actionInput.text = "";

        // Buttons row
        buttonsRowGO = new GameObject("Buttons", typeof(RectTransform));
        buttonsRowGO.transform.SetParent(stackContentRt, false);
        var buttonsRt = buttonsRowGO.GetComponent<RectTransform>();
        buttonsRt.sizeDelta = new Vector2(0f, 132f);
        var buttonsElement = buttonsRowGO.AddComponent<LayoutElement>();
        buttonsElement.preferredHeight = 132f;
        buttonsElement.flexibleHeight = 0f;

        var v = buttonsRowGO.AddComponent<VerticalLayoutGroup>();
        v.childAlignment = TextAnchor.UpperLeft;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        v.spacing = 8f;

        pttButton = MakeButton(buttonsRowGO.transform, "Record", "> RECORD");
        pttButtonLabel = pttButton.GetComponentInChildren<TextMeshProUGUI>(true);
        StyleTerminalActionButton(pttButton);
        pttButton.onClick.AddListener(ToggleRecord);

        submitButton = MakeButton(buttonsRowGO.transform, "Submit", "> START TRANSMISSION");
        StyleTerminalActionButton(submitButton);
        submitButton.onClick.AddListener(OnSubmit);

        videoPreviewGO = new GameObject("VideoPreview", typeof(RectTransform));
        videoPreviewGO.transform.SetParent(stackContentRt, false);
        var videoPreviewRt = videoPreviewGO.GetComponent<RectTransform>();
        videoPreviewRt.sizeDelta = new Vector2(0f, 124f);
        var videoPreviewElement = videoPreviewGO.AddComponent<LayoutElement>();
        videoPreviewElement.preferredHeight = 124f;
        videoPreviewElement.flexibleHeight = 0f;
        var videoPreviewLayout = videoPreviewGO.AddComponent<HorizontalLayoutGroup>();
        videoPreviewLayout.childAlignment = TextAnchor.MiddleLeft;
        videoPreviewLayout.childControlWidth = false;
        videoPreviewLayout.childControlHeight = false;
        videoPreviewLayout.childForceExpandWidth = false;
        videoPreviewLayout.childForceExpandHeight = false;

        var videoImageGO = new GameObject("VideoThumbnail", typeof(RectTransform), typeof(RawImage), typeof(VideoPlayer));
        videoImageGO.transform.SetParent(videoPreviewGO.transform, false);
        videoImageGO.GetComponent<RectTransform>().sizeDelta = new Vector2(176f, 112f);
        videoPreviewImage = videoImageGO.GetComponent<RawImage>();
        videoPreviewImage.color = Color.white;
        videoPreviewRT = new RenderTexture(352, 224, 0, RenderTextureFormat.ARGB32);
        videoPreviewRT.Create();
        videoPreviewImage.texture = videoPreviewRT;
        videoPreviewPlayer = videoImageGO.GetComponent<VideoPlayer>();
        videoPreviewPlayer.playOnAwake = false;
        videoPreviewPlayer.isLooping = true;
        videoPreviewPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPreviewPlayer.targetTexture = videoPreviewRT;
        videoPreviewPlayer.audioOutputMode = VideoAudioOutputMode.None;
        videoPreviewPlayer.prepareCompleted += OnInlineVideoPrepared;
        videoPreviewGO.SetActive(false);

        listGO.transform.SetSiblingIndex(0);
        setupRowGO.transform.SetSiblingIndex(1);
        promptGO.transform.SetSiblingIndex(2);
        inputRowGO.transform.SetSiblingIndex(3);
        buttonsRowGO.transform.SetSiblingIndex(4);
        videoPreviewGO.transform.SetSiblingIndex(5);

        // Transmitting overlay (shown after SEND)
        transmittingGO = new GameObject("Transmitting", typeof(RectTransform));
        transmittingGO.transform.SetParent(panel.transform, false);
        var txRt = (RectTransform)transmittingGO.transform;
        txRt.anchorMin = new Vector2(0f, 0f);
        txRt.anchorMax = new Vector2(1f, 1f);
        txRt.offsetMin = new Vector2(20f, 20f);
        txRt.offsetMax = new Vector2(-20f, -20f);

        transmittingText = transmittingGO.AddComponent<TextMeshProUGUI>();
        transmittingText.font = font;
        transmittingText.fontSize = 26f;
        transmittingText.color = TerminalGreen;
        transmittingText.alignment = TextAlignmentOptions.Center;
        transmittingText.text = "transmitting";

        var spinGO = new GameObject("Spinner", typeof(RectTransform));
        spinGO.transform.SetParent(transmittingGO.transform, false);
        var spinRt = (RectTransform)spinGO.transform;
        spinRt.anchorMin = new Vector2(0.5f, 0.5f);
        spinRt.anchorMax = new Vector2(0.5f, 0.5f);
        spinRt.pivot = new Vector2(0.5f, 0.5f);
        spinRt.sizeDelta = new Vector2(32f, 32f);
        spinRt.anchoredPosition = new Vector2(0f, -34f);

        transmittingSpinner = spinGO.AddComponent<TextMeshProUGUI>();
        transmittingSpinner.font = font;
        transmittingSpinner.fontSize = 28f;
        transmittingSpinner.color = TerminalDim;
        transmittingSpinner.alignment = TextAlignmentOptions.Center;
        transmittingSpinner.text = "|";
        transmittingSpinner.gameObject.SetActive(false);

        stopTransmitButton = MakeButton(transmittingGO.transform, "StopTransmit", "STOP TRANSMITTING");
        var stopRt = stopTransmitButton.GetComponent<RectTransform>();
        stopRt.anchorMin = new Vector2(0.5f, 0.5f);
        stopRt.anchorMax = new Vector2(0.5f, 0.5f);
        stopRt.pivot = new Vector2(0.5f, 0.5f);
        stopRt.sizeDelta = new Vector2(260f, 44f);
        stopRt.anchoredPosition = new Vector2(0f, -90f);
        stopTransmitButton.onClick.AddListener(StopTransmitting);

        transmittingGO.SetActive(false);

        // Status
        var statusGO = new GameObject("Status", typeof(RectTransform));
        statusGO.transform.SetParent(panel.transform, false);
        var statusRt = statusGO.GetComponent<RectTransform>();
        statusRt.anchorMin = new Vector2(0f, 0.0f);
        statusRt.anchorMax = new Vector2(1f, 0.02f);
        statusRt.offsetMin = new Vector2(20f, 0f);
        statusRt.offsetMax = new Vector2(-20f, 0f);

        statusLabel = statusGO.AddComponent<TextMeshProUGUI>();
        statusLabel.font = font;
        statusLabel.fontSize = 14f;
        statusLabel.color = TerminalDim;
        statusLabel.alignment = TextAlignmentOptions.MidlineLeft;
        statusLabel.text = "";
    }

    TextMeshProUGUI MakeInputText(Transform parent, string name, string initialText, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.fontSize = 16f;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.enableWordWrapping = true;
        tmp.text = initialText;
        return tmp;
    }

    TextMeshProUGUI MakeOverlayLabel(Transform parent, string name, string label)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.fontSize = 13f;
        tmp.color = TerminalGreen;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        tmp.text = label;
        return tmp;
    }

    Button MakeButton(Transform parent, string name, string label)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(160f, 44f);

        var img = go.GetComponent<Image>();
        img.sprite = K1L0GlassFactory.ControlRectSprite;
        img.type = Image.Type.Sliced;
        img.color = new Color(0.02f, 0.07f, 0.02f, 0.95f);

        var btn = go.GetComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.targetGraphic = img;

        var textGO = new GameObject("Label", typeof(RectTransform));
        textGO.transform.SetParent(go.transform, false);
        var textRt = textGO.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.fontSize = 18f;
        tmp.color = TerminalGreen;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.margin = new Vector4(12f, 0f, 12f, 0f);
        tmp.text = FormatButtonLabel(label);

        return btn;
    }

    string FormatButtonLabel(string label)
    {
        if (string.IsNullOrWhiteSpace(label)) return "[]";
        string trimmed = label.Trim();
        if (trimmed.StartsWith("[") && trimmed.EndsWith("]")) return trimmed;
        return $"[{trimmed}]";
    }

    void StyleTerminalActionButton(Button button)
    {
        if (button == null) return;
        var rt = button.transform as RectTransform;
        if (rt != null) rt.sizeDelta = new Vector2(0f, 62f);
        var layout = button.gameObject.GetComponent<LayoutElement>();
        if (layout == null) layout = button.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = 62f;
        layout.preferredHeight = 62f;
        layout.flexibleWidth = 1f;

        var img = button.GetComponent<Image>();
        if (img != null)
        {
            img.color = new Color(0.00f, 0.14f, 0.035f, 0.98f);
            var outline = button.gameObject.GetComponent<Outline>();
            if (outline == null) outline = button.gameObject.AddComponent<Outline>();
            outline.effectColor = TerminalGreen;
            outline.effectDistance = new Vector2(2f, 2f);
        }

        var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null)
        {
            label.fontSize = 18f;
            label.color = TerminalGreen;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.margin = new Vector4(18f, 0f, 18f, 0f);
        }
    }

    Button MakeMaterialButton(TransmissionManager.InventoryMaterial item)
    {
        string material = item != null ? item.material : "";
        int grams = item != null ? item.grams : 0;
        var go = new GameObject($"RareEarth_{material}", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(listRt, false);
        materialButtons.Add(go);

        var layout = go.AddComponent<LayoutElement>();
        layout.minHeight = 22f;
        layout.preferredHeight = 22f;
        layout.flexibleHeight = 0f;

        var img = go.GetComponent<Image>();
        img.sprite = null;
        img.type = Image.Type.Simple;
        img.color = new Color(0f, 0f, 0f, 0f);
        materialRowImages.Add(img);

        var btn = go.GetComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.targetGraphic = img;

        var labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(go.transform, false);
        var lrt = labelGO.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = new Vector2(2f, 0f);
        lrt.offsetMax = new Vector2(-2f, 0f);

        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.fontSize = 16f;
        tmp.color = TerminalGreen;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.enableWordWrapping = false;
        tmp.text = "";
        materialRowLabels.Add(tmp);

        string optionText = $"> {material.ToUpper()}  {Mathf.Max(0, grams)}g";
        StartCoroutine(TypeMaterialOption(tmp, optionText, materialButtons.Count * 0.06f));

        btn.onClick.AddListener(() => SelectMaterialOption(material, img, tmp));
        return btn;
    }

    System.Collections.IEnumerator TypeMaterialOption(TextMeshProUGUI target, string text, float delay)
    {
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
        if (target == null) yield break;
        target.text = "";
        for (int i = 0; i < text.Length; i++)
        {
            if (target == null) yield break;
            target.text = text.Substring(0, i + 1);
            yield return new WaitForSecondsRealtime(0.010f);
        }
    }

    void SelectMaterialOption(string artifact, Image rowImage, TextMeshProUGUI rowLabel)
    {
        pendingArtifact = artifact;
        selectedArtifact = artifact;
        HighlightSelectedMaterial(rowImage, rowLabel);
        if (setupRowGO != null) setupRowGO.SetActive(true);
        bool hasPhoto = !string.IsNullOrWhiteSpace(selectedPhotoPath);
        if (spiritsPromptGO != null) spiritsPromptGO.SetActive(hasPhoto);
        if (spiritsGO != null) spiritsGO.SetActive(hasPhoto);
        bool readyForAction = hasPhoto && spiritsSelected;
        if (inputRowGO != null) inputRowGO.SetActive(readyForAction);
        if (buttonsRowGO != null) buttonsRowGO.SetActive(readyForAction);
        if (submitButton != null) submitButton.gameObject.SetActive(readyForAction);
        if (pttButton != null) pttButton.gameObject.SetActive(readyForAction);
        if (!hasPhoto) SetPromptText("");
        else SetPromptText(spiritsSelected ? BuildSpiritsQuestion() : "");
        UpdateSetupHeight(photoPreviewGO != null && photoPreviewGO.activeSelf);
        if (statusLabel != null) statusLabel.text = "";
    }

    void HighlightSelectedMaterial(Image selectedImage, TextMeshProUGUI selectedLabel)
    {
        for (int i = 0; i < materialRowImages.Count; i++)
        {
            if (materialRowImages[i] != null)
                materialRowImages[i].color = materialRowImages[i] == selectedImage
                    ? new Color(0.00f, 0.19f, 0.055f, 0.95f)
                    : new Color(0f, 0f, 0f, 0f);
        }
        for (int i = 0; i < materialRowLabels.Count; i++)
        {
            if (materialRowLabels[i] != null)
                materialRowLabels[i].color = materialRowLabels[i] == selectedLabel ? Color.white : TerminalGreen;
        }
    }

    void ConfirmSelectedMaterial()
    {
        if (string.IsNullOrWhiteSpace(pendingArtifact))
        {
            if (statusLabel != null) statusLabel.text = "Pick a rare earth element first.";
            return;
        }

        selectedArtifact = pendingArtifact;
        SetPromptText(BuildSpiritsQuestion());
        if (statusLabel != null) statusLabel.text = "";

        // After confirming an item, hide the terminal options and reveal the camera/form step.
        if (listRt != null) listRt.gameObject.SetActive(false);
        if (selectedMaterialGO != null) selectedMaterialGO.SetActive(false);
        if (setupRowGO != null) setupRowGO.SetActive(true);
        if (inputRowGO != null) inputRowGO.SetActive(true);
        if (buttonsRowGO != null) buttonsRowGO.SetActive(true);
        if (submitButton != null) submitButton.gameObject.SetActive(true);
        if (pttButton != null) pttButton.gameObject.SetActive(true);
    }

    void SetPromptText(string text)
    {
        if (promptLabel == null) return;
        if (elementTypeCoroutine != null)
        {
            StopCoroutine(elementTypeCoroutine);
            elementTypeCoroutine = null;
        }
        promptLabel.color = TerminalGreen;
        elementTypeCoroutine = StartCoroutine(TypePrompt(text ?? ""));
    }

    System.Collections.IEnumerator TypePrompt(string text)
    {
        promptLabel.text = "";
        for (int i = 0; i < text.Length; i++)
        {
            if (promptLabel == null) yield break;
            promptLabel.text = text.Substring(0, i + 1);
            yield return new WaitForSecondsRealtime(0.012f);
        }
        elementTypeCoroutine = null;
    }

    void ClearArtifacts()
    {
        for (int i = 0; i < materialButtons.Count; i++)
        {
            if (materialButtons[i] != null) Destroy(materialButtons[i]);
        }
        materialButtons.Clear();
        materialRowImages.Clear();
        materialRowLabels.Clear();
        SetMaterialListHeight(0);
    }

    void SetMaterialListHeight(int itemCount)
    {
        if (listRt == null) return;
        float height = Mathf.Max(0f, itemCount * 22f);
        listRt.sizeDelta = new Vector2(listRt.sizeDelta.x, height);
        var layout = listRt.GetComponent<LayoutElement>();
        if (layout != null) layout.preferredHeight = height;
    }

    void SubscribeTransmissionReady()
    {
        if (subscribedToTransmissionReady) return;
        var tm = TransmissionManager.Instance != null ? TransmissionManager.Instance : FindFirstObjectByType<TransmissionManager>();
        if (tm == null) return;
        tm.OnTransmissionReady += OnTransmissionReady;
        subscribedToTransmissionReady = true;
    }

    void UnsubscribeTransmissionReady()
    {
        if (!subscribedToTransmissionReady) return;
        var tm = TransmissionManager.Instance != null ? TransmissionManager.Instance : FindFirstObjectByType<TransmissionManager>();
        if (tm != null) tm.OnTransmissionReady -= OnTransmissionReady;
        subscribedToTransmissionReady = false;
    }

    void OnTransmissionReady(TransmissionData data)
    {
        if (root == null || !root.activeInHierarchy) return;
        if (!isSubmitting) return;
        if (data == null || !string.Equals(data.transmissionType, "transmitter", System.StringComparison.OrdinalIgnoreCase)) return;
        if (string.IsNullOrWhiteSpace(data.videoUrl)) return;

        ShowInlineVideo(data.videoUrl);
    }

    void ShowInlineVideo(string videoUrl)
    {
        isSubmitting = false;
        currentPreviewVideoUrl = videoUrl;
        if (transmittingGO != null) transmittingGO.SetActive(false);
        if (promptLabel != null) promptLabel.gameObject.SetActive(true);
        SetPromptText("transmission ready");
        if (videoPreviewGO != null) videoPreviewGO.SetActive(true);
        if (videoPreviewPlayer != null)
        {
            videoPreviewPlayer.Stop();
            videoPreviewPlayer.url = videoUrl;
            videoPreviewPlayer.Prepare();
        }
        if (statusLabel != null) statusLabel.text = "";
    }

    void OnInlineVideoPrepared(VideoPlayer player)
    {
        if (player == null || player != videoPreviewPlayer) return;
        player.Play();
    }

    public void Show(Signal transmitterSignal)
    {
        SubscribeTransmissionReady();
        activeSignal = transmitterSignal;
        if (!embeddedMode) ApplySafeAreaSizing();
        selectedArtifact = null;
        pendingArtifact = null;
        spiritsSelected = false;
        selectedPhotoPath = null;
        selectedPhotoUrl = null;
        RefreshPhotoLabel();
        suppressSpiritsChange = true;
        if (spiritsSlider != null) spiritsSlider.value = 0.5f;
        suppressSpiritsChange = false;
        if (actionInput != null) actionInput.text = "";
        if (statusLabel != null) statusLabel.text = "";
        isSubmitting = false;
        currentPreviewVideoUrl = null;
        if (videoPreviewPlayer != null) videoPreviewPlayer.Stop();
        if (videoPreviewGO != null) videoPreviewGO.SetActive(false);
        if (transmittingGO != null) transmittingGO.SetActive(false);
        if (submitButton != null)
        {
            submitButton.interactable = true;
            submitButton.gameObject.SetActive(false);
        }
        if (closeButton != null) closeButton.gameObject.SetActive(true);
        if (pttButton != null) pttButton.gameObject.SetActive(false);
        if (buttonsRowGO != null) buttonsRowGO.SetActive(false);
        if (setupRowGO != null) setupRowGO.SetActive(false);
        if (spiritsPromptGO != null) spiritsPromptGO.SetActive(false);
        if (spiritsGO != null) spiritsGO.SetActive(false);
        if (listRt != null) listRt.gameObject.SetActive(true);
        if (selectedMaterialGO != null) selectedMaterialGO.SetActive(false);
        if (promptLabel != null)
        {
            promptLabel.gameObject.SetActive(true);
            promptLabel.text = "";
            promptLabel.color = TerminalDim;
        }
        if (inputRowGO != null) inputRowGO.SetActive(false);
        StopRecordingIfNeeded();

        ClearArtifacts();
        RefreshInventory();

        if (root != null) root.SetActive(true);
        if (!embeddedMode && !contrastActive)
        {
            K1L0ModalHudMode.Begin();
            K1L0ModalContrastMode.Begin(this);
            contrastActive = true;
        }
    }

    public void RefreshInventory()
    {
        if (promptLabel == null) return;
        ClearArtifacts();

        var tm = TransmissionManager.Instance;
        var materials = tm != null ? tm.GetRareEarthInventory() : new List<TransmissionManager.InventoryMaterial>();
        if (materials.Count == 0)
        {
            SetPromptText("No rare earth elements yet. Enter a portal and press [yes] to accept.");
            if (submitButton != null) submitButton.interactable = false;
            selectedArtifact = null;
            pendingArtifact = null;
            return;
        }

        SetPromptText("Select an element to transmit.");
        if (submitButton != null) submitButton.interactable = true;
        for (int i = 0; i < materials.Count; i++)
            MakeMaterialButton(materials[i]);
        SetMaterialListHeight(materials.Count);
    }

    void UpdateSpiritsPrompt()
    {
        float value = spiritsSlider != null ? spiritsSlider.value : 0.5f;
        selectedSpirits = value < 0.34f ? "low" : (value > 0.66f ? "high" : "medium");
        if (spiritsValueLabel != null) spiritsValueLabel.text = $"SPIRITS: {selectedSpirits.ToUpper()}";
        if (suppressSpiritsChange) return;
        if (string.IsNullOrWhiteSpace(selectedArtifact)) return;
        if (string.IsNullOrWhiteSpace(selectedPhotoPath)) return;

        spiritsSelected = true;
        if (inputRowGO != null) inputRowGO.SetActive(true);
        if (buttonsRowGO != null) buttonsRowGO.SetActive(true);
        if (submitButton != null) submitButton.gameObject.SetActive(true);
        if (pttButton != null) pttButton.gameObject.SetActive(true);
        if (promptLabel != null)
        {
            SetPromptText(BuildSpiritsQuestion());
        }
    }

    string BuildSpiritsQuestion()
    {
        return $"why are spirits {selectedSpirits}?";
    }

    void SelectPhoto()
    {
#if UNITY_IOS && !UNITY_EDITOR
        StartPhotoFallbackWatcher();
        K1L0PickPhoto(gameObject.name, nameof(OnPhotoPicked));
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "/usr/bin/osascript",
                Arguments = "-e \"POSIX path of (choose file of type {\\\"public.image\\\"} with prompt \\\"Select transmission photo\\\")\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            using (var process = Process.Start(psi))
            {
                selectedPhotoPath = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit(30000);
            }
        }
        catch (System.Exception e)
        {
            if (statusLabel != null) statusLabel.text = $"Photo picker failed: {e.Message}";
        }
#else
        selectedPhotoPath = "photo-selected";
#endif
        RefreshPhotoLabel();
    }

    void StartPhotoFallbackWatcher()
    {
        if (photoFallbackCoroutine != null)
        {
            StopCoroutine(photoFallbackCoroutine);
            photoFallbackCoroutine = null;
        }
        photoFallbackCoroutine = StartCoroutine(WaitForPhotoFallback());
    }

    System.Collections.IEnumerator WaitForPhotoFallback()
    {
        string fallbackPath = Path.Combine(Application.persistentDataPath, "k1l0_tx_latest.jpg");
        System.DateTime startedAt = System.DateTime.UtcNow;
        if (File.Exists(fallbackPath))
        {
            try { File.Delete(fallbackPath); } catch { }
        }

        for (int i = 0; i < 120; i++)
        {
            yield return new WaitForSecondsRealtime(0.25f);
            if (string.Equals(selectedPhotoPath, fallbackPath, System.StringComparison.Ordinal))
                yield break;
            if (!File.Exists(fallbackPath))
                continue;

            System.DateTime writtenAt;
            try { writtenAt = File.GetLastWriteTimeUtc(fallbackPath); }
            catch { continue; }

            if (writtenAt < startedAt.AddSeconds(-1))
                continue;

            UnityEngine.Debug.Log($"[TransmitterEnterModal] Photo fallback armed path='{fallbackPath}' written={writtenAt:o}");
            photoFallbackCoroutine = null;
            OnPhotoPicked(fallbackPath);
            yield break;
        }

        photoFallbackCoroutine = null;
    }

    public void OnPhotoPicked(string path)
    {
        if (photoFallbackCoroutine != null)
        {
            StopCoroutine(photoFallbackCoroutine);
            photoFallbackCoroutine = null;
        }
        selectedPhotoPath = string.IsNullOrWhiteSpace(path) ? null : path.Trim();
        selectedPhotoUrl = null;
        UnityEngine.Debug.Log($"[TransmitterEnterModal] OnPhotoPicked path='{selectedPhotoPath ?? ""}' exists={(!string.IsNullOrWhiteSpace(selectedPhotoPath) && File.Exists(selectedPhotoPath))}");
        if (statusLabel != null)
            statusLabel.text = string.IsNullOrWhiteSpace(selectedPhotoPath) ? "Photo cancelled." : "Photo loaded.";
        RefreshPhotoLabel();
    }

    void RefreshPhotoLabel()
    {
        if (photoLabel != null)
            photoLabel.text = string.IsNullOrWhiteSpace(selectedPhotoPath) ? "" : "photo selected";
        if (photoButtonLabel != null)
            photoButtonLabel.text = string.IsNullOrWhiteSpace(selectedPhotoPath) ? "TAKE PHOTO" : "RETAKE PHOTO";

        bool hasPreview = false;
        if (photoPreviewImage != null && !string.IsNullOrWhiteSpace(selectedPhotoPath) && File.Exists(selectedPhotoPath))
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(selectedPhotoPath);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (tex.LoadImage(bytes))
                {
                    photoPreviewImage.texture = tex;
                    var fitter = photoPreviewImage.GetComponent<AspectRatioFitter>();
                    if (fitter != null && tex.height > 0)
                        fitter.aspectRatio = (float)tex.width / tex.height;
                    hasPreview = true;
                }
            }
            catch { hasPreview = false; }
        }

        if (photoPreviewGO != null) photoPreviewGO.SetActive(hasPreview);
        if (!string.IsNullOrWhiteSpace(selectedArtifact) && !string.IsNullOrWhiteSpace(selectedPhotoPath))
        {
            if (spiritsPromptGO != null) spiritsPromptGO.SetActive(true);
            if (spiritsGO != null) spiritsGO.SetActive(true);
            if (!spiritsSelected)
            {
                if (inputRowGO != null) inputRowGO.SetActive(false);
                if (buttonsRowGO != null) buttonsRowGO.SetActive(false);
                SetPromptText("");
            }
        }
        UpdateSetupHeight(hasPreview);
    }

    void UpdateSetupHeight(bool showPhotoPreview)
    {
        float height = 44f;
        if (showPhotoPreview) height += 106f;
        if (spiritsPromptGO != null && spiritsPromptGO.activeSelf) height += 38f;
        if (spiritsGO != null && spiritsGO.activeSelf) height += 64f;
        height = Mathf.Max(44f, height);
        if (setupLayoutElement != null) setupLayoutElement.preferredHeight = height;
        if (setupRowGO != null)
        {
            var rt = setupRowGO.GetComponent<RectTransform>();
            if (rt != null) rt.sizeDelta = new Vector2(rt.sizeDelta.x, height);
        }
    }

    public void Hide()
    {
        activeSignal = null;
        selectedArtifact = null;
        pendingArtifact = null;
        if (elementTypeCoroutine != null)
        {
            StopCoroutine(elementTypeCoroutine);
            elementTypeCoroutine = null;
        }
        StopRecordingIfNeeded();
        if (root != null) root.SetActive(false);
        if (!embeddedMode && contrastActive)
        {
            K1L0ModalContrastMode.End(this);
            K1L0ModalHudMode.End();
            contrastActive = false;
        }
    }

    void ToggleRecord()
    {
        if (isRecording)
        {
            StopRecordingIfNeeded();
            statusLabel.text = "Recorded. Press SEND.";
            if (actionInput != null) actionInput.ActivateInputField();
            return;
        }

        try
        {
            KiloSpeechRecognizer.RequestAuthorization();
            KiloSpeechRecognizer.ClearLatestText();
            lastTranscript = "";
            KiloSpeechRecognizer.Start();
            recordingStartTime = Time.unscaledTime;
            isRecording = true;
            if (pttButtonLabel != null) pttButtonLabel.text = "[STOP]";
            statusLabel.text = "Recording…";
        }
        catch (System.Exception e)
        {
            statusLabel.text = $"Mic error: {e.Message}";
            isRecording = false;
                if (pttButtonLabel != null) pttButtonLabel.text = "[RECORD]";
        }
    }

    void StopRecordingIfNeeded()
    {
        if (!isRecording)
        {
            if (pttButtonLabel != null) pttButtonLabel.text = "[RECORD]";
            return;
        }

        try { KiloSpeechRecognizer.Stop(); } catch { }
        isRecording = false;
        if (pttButtonLabel != null) pttButtonLabel.text = "[RECORD]";

        float dur = Mathf.Max(0f, Time.unscaledTime - recordingStartTime);
        statusLabel.text = $"Recorded {dur:F1}s.";
    }

    void OnSubmit()
    {
        if (string.IsNullOrWhiteSpace(selectedArtifact))
        {
            statusLabel.text = "Pick a rare earth element first.";
            return;
        }
        if (string.IsNullOrWhiteSpace(selectedPhotoPath))
        {
            statusLabel.text = "Take a photo first.";
            return;
        }
        if (!spiritsSelected)
        {
            statusLabel.text = "Select spirits first.";
            return;
        }
        string action = actionInput != null ? actionInput.text : null;
        if (string.IsNullOrWhiteSpace(action))
        {
            statusLabel.text = "Type what you want to do.";
            return;
        }

        statusLabel.text = "SENDING…";
        submitButton.interactable = false;

        StartCoroutine(SubmitTransmission(action));
    }

    System.Collections.IEnumerator SubmitTransmission(string action)
    {
        string imageUrl = selectedPhotoUrl;
        if (string.IsNullOrWhiteSpace(imageUrl) && !string.IsNullOrWhiteSpace(selectedPhotoPath))
        {
            yield return UploadSelectedPhoto(url => imageUrl = url);
            selectedPhotoUrl = imageUrl;
            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                if (statusLabel != null) statusLabel.text = "Photo upload failed.";
                if (submitButton != null) submitButton.interactable = true;
                yield break;
            }
        }

        // v2 sends image + mood as dedicated fields, so `message` is just the
        // raw user action. (No more stuffing photo URL / spirits into the text.)
        var tm = TransmissionManager.Instance;
        if (tm != null)
        {
            UnityEngine.Debug.Log($"[TransmitterEnterModal] SEND material='{selectedArtifact}' actionLen={action.Length} signal={(activeSignal != null ? activeSignal.id : "none")}");
            tm.StartTransmitterInteraction(activeSignal, selectedArtifact, action, imageUrl, selectedSpirits);
        }
        else
        {
            statusLabel.text = "TransmissionManager missing.";
            submitButton.interactable = true;
            yield break;
        }

        // Keep the terminal stack visible and append/update the status inline.
        isSubmitting = true;
        spinnerIndex = -1;
        nextSpinnerTick = 0f;
        if (submitButton != null) submitButton.gameObject.SetActive(false);
        if (closeButton != null) closeButton.gameObject.SetActive(true);
        if (pttButton != null) pttButton.gameObject.SetActive(false);
        if (buttonsRowGO != null) buttonsRowGO.SetActive(false);
        if (promptLabel != null) promptLabel.gameObject.SetActive(true);
        SetPromptText("transmitting...");
        if (transmittingGO != null) transmittingGO.SetActive(false);
        if (statusLabel != null) statusLabel.text = "";
    }

    System.Collections.IEnumerator UploadSelectedPhoto(System.Action<string> done)
    {
        string path = selectedPhotoPath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            done?.Invoke(null);
            yield break;
        }

        byte[] bytes;
        try { bytes = File.ReadAllBytes(path); }
        catch { done?.Invoke(null); yield break; }

        var req = new K1L0ImageUploadRequest
        {
            userId = TransmissionManager.Instance != null ? TransmissionManager.Instance.GetUserIdForClient() : "anon",
            filename = Path.GetFileName(path),
            contentType = path.ToLowerInvariant().EndsWith(".png") ? "image/png" : "image/jpeg",
            imageBase64 = System.Convert.ToBase64String(bytes)
        };
        string body = JsonUtility.ToJson(req);
        bool ok = false;
        string response = null;
        yield return APIManager.Instance.Post("/api/k1l0/upload-image", body, (success, resp) =>
        {
            ok = success;
            response = resp;
        });
        if (!ok || string.IsNullOrWhiteSpace(response))
        {
            done?.Invoke(null);
            yield break;
        }
        var parsed = JsonUtility.FromJson<K1L0ImageUploadResponse>(response);
        done?.Invoke(parsed != null && parsed.ok ? parsed.url : null);
    }

    string BuildTransmissionDirective(string action, string imageUrl)
    {
        string photo = string.IsNullOrWhiteSpace(imageUrl) ? "none" : imageUrl;
        return $"spirits are {selectedSpirits}. selected photo: {photo}. user answer: {action}";
    }

    void StopTransmitting()
    {
        isSubmitting = false;
        if (transmittingGO != null) transmittingGO.SetActive(false);
        if (promptLabel != null)
        {
            promptLabel.gameObject.SetActive(true);
            promptLabel.text = "Transmission stopped.";
            promptLabel.color = TerminalDim;
        }
        if (closeButton != null) closeButton.gameObject.SetActive(true);
        if (submitButton != null)
        {
            submitButton.interactable = true;
            submitButton.gameObject.SetActive(true);
        }
        if (inputRowGO != null) inputRowGO.SetActive(true);
        if (setupRowGO != null) setupRowGO.SetActive(true);
        if (buttonsRowGO != null) buttonsRowGO.SetActive(true);
        if (pttButton != null) pttButton.gameObject.SetActive(true);
        if (statusLabel != null) statusLabel.text = "";
    }

    [System.Serializable]
    private class K1L0ImageUploadRequest
    {
        public string userId;
        public string filename;
        public string contentType;
        public string imageBase64;
    }

    [System.Serializable]
    private class K1L0ImageUploadResponse
    {
        public bool ok;
        public string url;
        public string error;
    }
}
