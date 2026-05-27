using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LocationExchangeModal : MonoBehaviour
{
    public static LocationExchangeModal Instance { get; private set; }

    private enum Step
    {
        Offer,
        SendAsk,
        Action,
        Submitting
    }

    private static readonly Color TerminalGreen = new Color(0.56f, 1f, 0.62f, 1f);
    private static readonly Color TerminalDim = new Color(0.56f, 1f, 0.62f, 0.65f);

    private TMP_FontAsset font;
    private GameObject root;
    private RectTransform panelRt;
    private TextMeshProUGUI titleLabel;
    private TextMeshProUGUI promptLabel;
    private TextMeshProUGUI statusLabel;
    private TMP_InputField inputField;
    private RectTransform buttonsRt;
    private Button recordButton;
    private TextMeshProUGUI recordButtonLabel;
    private Button cancelButton;
    private Button closeButton;
    private Button nextButton;
    private TextMeshProUGUI nextButtonLabel;
    private GameObject transmittingGO;
    private TextMeshProUGUI transmittingSpinner;

    private Signal activeSignal;
    private Step step;
    private string claimedItem;
    private string claimedAction;
    private bool isRecording;
    private bool isSubmitting;
    private bool contrastActive;
    private string lastTranscript;
    private float recordingStartTime;
    private float nextTranscriptPoll;
    private float nextSpinnerTick;
    private int spinnerIndex;
    private Coroutine promptTypeCoroutine;
    private static readonly string[] FallbackSenders =
    {
        "Mara", "Theo", "June", "Cass", "Iris", "Vale", "Nico", "Orla",
        "Milo", "Zara", "Lena", "Otis", "Sable", "Remy", "Vera", "Jules"
    };

    public void Initialize()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        font = Resources.Load<TMP_FontAsset>("Fonts/IBMPlexMono-Regular SDF");
        if (font == null) font = Resources.Load<TMP_FontAsset>("Fonts/Inter-Regular SDF");
        if (font == null) font = TMP_Settings.defaultFontAsset;

        BuildUI();
        ApplySafeAreaSizing();
        Hide();
    }

    void Update()
    {
        if (isSubmitting && transmittingGO != null && transmittingGO.activeInHierarchy && Time.unscaledTime >= nextSpinnerTick)
        {
            nextSpinnerTick = Time.unscaledTime + 0.09f;
            spinnerIndex = (spinnerIndex + 1) % 4;
            if (transmittingSpinner != null)
            {
                transmittingSpinner.text = spinnerIndex switch
                {
                    0 => "|",
                    1 => "/",
                    2 => "-",
                    _ => "\\"
                };
            }
        }

        if (!isRecording || root == null || !root.activeInHierarchy || Time.unscaledTime < nextTranscriptPoll) return;
        nextTranscriptPoll = Time.unscaledTime + 0.20f;

        string t = KiloSpeechRecognizer.GetLatestText();
        if (!string.IsNullOrEmpty(t) && t != lastTranscript)
        {
            lastTranscript = t;
            if (inputField != null) inputField.text = t;
            if (statusLabel != null) statusLabel.text = "Listening… (transcribing)";
        }
        else if (string.IsNullOrEmpty(lastTranscript))
        {
            float dur = Mathf.Max(0f, Time.unscaledTime - recordingStartTime);
            if (dur > 1.0f && statusLabel != null) statusLabel.text = "Listening… (no words yet)";
        }
    }

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
        panelRt.sizeDelta = new Vector2(Mathf.Max(320f, w), Mathf.Max(380f, h));
    }

    private void BuildUI()
    {
        root = new GameObject("LocationExchangeModal", typeof(RectTransform));
        root.transform.SetParent(K1L0CanvasRoot.Modal, false);
        var rootRt = (RectTransform)root.transform;
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        var dimGO = new GameObject("Dim", typeof(RectTransform), typeof(Image));
        dimGO.transform.SetParent(root.transform, false);
        var dimRt = (RectTransform)dimGO.transform;
        dimRt.anchorMin = Vector2.zero;
        dimRt.anchorMax = Vector2.one;
        dimRt.offsetMin = Vector2.zero;
        dimRt.offsetMax = Vector2.zero;
        dimGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.99f);

        var panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(root.transform, false);
        panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(680f, 620f);

        var chrome = K1L0GlassFactory.AttachChrome(panel.transform, "LocationExchange", K1L0GlassFactory.PanelStyle);
        if (chrome != null)
        {
            chrome.blurFill.color = new Color(0f, 0f, 0f, 0f);
            chrome.overlay.color = new Color(0f, 0f, 0f, 0f);
            chrome.border.color = new Color(0.56f, 1f, 0.62f, 0.18f);
            chrome.accent.color = new Color(0f, 0f, 0f, 0f);
            chrome.sheen.color = new Color(0f, 0f, 0f, 0f);
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
        K1L0GlassFactory.AttachTerminalStatic(panel.transform, "LocationStatic");
        closeButton = K1L0GlassFactory.CreateTerminalCloseButton(panel.transform, font, Hide);

        titleLabel = MakeText(panel.transform, "Title", 26f, TerminalGreen, TextAlignmentOptions.TopLeft);
        var titleRt = (RectTransform)titleLabel.transform;
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.offsetMin = new Vector2(20f, -78f);
        titleRt.offsetMax = new Vector2(-20f, -18f);

        promptLabel = MakeText(panel.transform, "Prompt", 20f, TerminalGreen, TextAlignmentOptions.TopLeft);
        var promptRt = (RectTransform)promptLabel.transform;
        promptRt.anchorMin = new Vector2(0f, 0.55f);
        promptRt.anchorMax = new Vector2(1f, 0.86f);
        promptRt.offsetMin = new Vector2(20f, 0f);
        promptRt.offsetMax = new Vector2(-20f, 0f);
        promptLabel.enableWordWrapping = true;

        var inputBg = new GameObject("InputBg", typeof(RectTransform), typeof(Image));
        inputBg.transform.SetParent(panel.transform, false);
        var inputBgRt = (RectTransform)inputBg.transform;
        inputBgRt.anchorMin = new Vector2(0f, 0.24f);
        inputBgRt.anchorMax = new Vector2(1f, 0.50f);
        inputBgRt.offsetMin = new Vector2(20f, 0f);
        inputBgRt.offsetMax = new Vector2(-20f, 0f);
        var inputImg = inputBg.GetComponent<Image>();
        inputImg.sprite = K1L0GlassFactory.ControlRectSprite;
        inputImg.type = Image.Type.Sliced;
        inputImg.color = new Color(0.02f, 0.05f, 0.02f, 0.95f);

        var tfGO = new GameObject("InputField", typeof(RectTransform));
        tfGO.transform.SetParent(inputBg.transform, false);
        var tfRt = (RectTransform)tfGO.transform;
        tfRt.anchorMin = Vector2.zero;
        tfRt.anchorMax = Vector2.one;
        tfRt.offsetMin = new Vector2(14f, 10f);
        tfRt.offsetMax = new Vector2(-14f, -10f);

        inputField = tfGO.AddComponent<TMP_InputField>();
        inputField.lineType = TMP_InputField.LineType.MultiLineNewline;
        var placeholder = MakeInputText(tfGO.transform, "Placeholder", "", TerminalDim);
        var text = MakeInputText(tfGO.transform, "Text", "", TerminalGreen);
        inputField.placeholder = placeholder;
        inputField.textComponent = text;
        inputField.pointSize = 16f;

        var buttonsGO = new GameObject("Buttons", typeof(RectTransform));
        buttonsGO.transform.SetParent(panel.transform, false);
        buttonsRt = (RectTransform)buttonsGO.transform;
        buttonsRt.anchorMin = new Vector2(0f, 0.08f);
        buttonsRt.anchorMax = new Vector2(1f, 0.19f);
        buttonsRt.offsetMin = new Vector2(20f, 0f);
        buttonsRt.offsetMax = new Vector2(-20f, 0f);

        var layout = buttonsGO.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        layout.spacing = 10f;

        recordButton = MakeButton(buttonsGO.transform, "Record", "RECORD", 132f);
        recordButtonLabel = recordButton.GetComponentInChildren<TextMeshProUGUI>(true);
        recordButton.onClick.AddListener(ToggleRecord);

        cancelButton = MakeButton(buttonsGO.transform, "No", "[no]", 96f);
        cancelButton.onClick.AddListener(OnNo);

        nextButton = MakeButton(buttonsGO.transform, "Yes", "[yes]", 104f);
        nextButtonLabel = nextButton.GetComponentInChildren<TextMeshProUGUI>(true);
        nextButton.onClick.AddListener(OnNext);

        statusLabel = MakeText(panel.transform, "Status", 14f, TerminalDim, TextAlignmentOptions.MidlineLeft);
        var statusRt = (RectTransform)statusLabel.transform;
        statusRt.anchorMin = new Vector2(0f, 0.0f);
        statusRt.anchorMax = new Vector2(1f, 0.06f);
        statusRt.offsetMin = new Vector2(20f, 0f);
        statusRt.offsetMax = new Vector2(-20f, 0f);

        transmittingGO = new GameObject("Transmitting", typeof(RectTransform));
        transmittingGO.transform.SetParent(panel.transform, false);
        var txRt = (RectTransform)transmittingGO.transform;
        txRt.anchorMin = Vector2.zero;
        txRt.anchorMax = Vector2.one;
        txRt.offsetMin = new Vector2(20f, 20f);
        txRt.offsetMax = new Vector2(-20f, -20f);

        var txText = transmittingGO.AddComponent<TextMeshProUGUI>();
        txText.font = font;
        txText.fontSize = 28f;
        txText.color = TerminalGreen;
        txText.alignment = TextAlignmentOptions.Center;
        txText.text = "transmitting";

        var spinGO = new GameObject("Spinner", typeof(RectTransform));
        spinGO.transform.SetParent(transmittingGO.transform, false);
        var spinRt = (RectTransform)spinGO.transform;
        spinRt.anchorMin = new Vector2(0.5f, 0.5f);
        spinRt.anchorMax = new Vector2(0.5f, 0.5f);
        spinRt.pivot = new Vector2(0.5f, 0.5f);
        spinRt.sizeDelta = new Vector2(32f, 32f);
        spinRt.anchoredPosition = new Vector2(0f, -36f);

        transmittingSpinner = spinGO.AddComponent<TextMeshProUGUI>();
        transmittingSpinner.font = font;
        transmittingSpinner.fontSize = 28f;
        transmittingSpinner.color = TerminalDim;
        transmittingSpinner.alignment = TextAlignmentOptions.Center;
        transmittingSpinner.text = "|";
        transmittingGO.SetActive(false);
    }

    private TextMeshProUGUI MakeText(Transform parent, string name, float size, Color color, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.raycastTarget = false;
        return tmp;
    }

    private TextMeshProUGUI MakeInputText(Transform parent, string name, string initialText, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
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

    private Button MakeButton(Transform parent, string name, string label, float width)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(width, 44f);

        var img = go.GetComponent<Image>();
        img.sprite = K1L0GlassFactory.ControlRectSprite;
        img.type = Image.Type.Sliced;
        img.color = new Color(0.02f, 0.07f, 0.02f, 0.95f);

        var btn = go.GetComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.targetGraphic = img;

        var textGO = new GameObject("Label", typeof(RectTransform));
        textGO.transform.SetParent(go.transform, false);
        var textRt = (RectTransform)textGO.transform;
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.fontSize = 17f;
        tmp.color = TerminalGreen;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.text = label;
        return btn;
    }

    public void Show(Signal locationSignal)
    {
        activeSignal = locationSignal;
        ApplySafeAreaSizing();
        StopRecordingIfNeeded();
        isSubmitting = false;
        claimedAction = null;
        claimedItem = null;
        step = Step.Offer;
        if (inputField != null) inputField.text = "";
        if (inputField != null) inputField.gameObject.SetActive(false);
        if (statusLabel != null) statusLabel.text = "";
        if (transmittingGO != null) transmittingGO.SetActive(false);
        if (recordButton != null) recordButton.gameObject.SetActive(false);
        if (cancelButton != null)
        {
            cancelButton.gameObject.SetActive(true);
            cancelButton.interactable = true;
        }
        if (closeButton != null) closeButton.gameObject.SetActive(true);
        if (nextButton != null)
        {
            nextButton.gameObject.SetActive(true);
            nextButton.interactable = true;
        }

        var tm = TransmissionManager.Instance;
        claimedItem = ResolveSignalItem(locationSignal, tm);
        ApplyOfferStep();
        if (root != null) root.SetActive(true);
        if (!contrastActive)
        {
            K1L0ModalHudMode.Begin();
            K1L0ModalContrastMode.Begin(this);
            contrastActive = true;
        }

        if (tm != null && IsRealLocation())
            StartCoroutine(tm.FetchLocationExchangeObject(locationSignal, item =>
            {
                if (root == null || !root.activeInHierarchy || step != Step.Offer) return;
                if (string.IsNullOrWhiteSpace(item)) return;
                claimedItem = item.Trim();
                ApplyOfferStep();
            }));
    }

    public void Hide()
    {
        StopRecordingIfNeeded();
        StopPromptTypewriter();
        activeSignal = null;
        claimedAction = null;
        claimedItem = null;
        isSubmitting = false;
        if (root != null) root.SetActive(false);
        if (contrastActive)
        {
            K1L0ModalContrastMode.End(this);
            K1L0ModalHudMode.End();
            contrastActive = false;
        }
    }

    private string ResolveSignalItem(Signal sig, TransmissionManager tm)
    {
        if (sig != null && !string.IsNullOrWhiteSpace(sig.specialItem)) return sig.specialItem.Trim();
        return tm != null ? tm.GetLocationExchangeObject(sig) : "mineral";
    }

    private int GetQuantityGrams()
    {
        string seed = activeSignal != null && !string.IsNullOrWhiteSpace(activeSignal.id) ? activeSignal.id : claimedItem ?? "k1l0";
        int hash = Mathf.Abs(seed.GetHashCode());
        return 1 + (hash % 99);
    }

    private string GetSenderName()
    {
        if (activeSignal != null && !string.IsNullOrWhiteSpace(activeSignal.character))
            return activeSignal.character.Trim();
        string seed = activeSignal != null && !string.IsNullOrWhiteSpace(activeSignal.id) ? activeSignal.id : GetLocationName();
        int index = Mathf.Abs(seed.GetHashCode()) % FallbackSenders.Length;
        return FallbackSenders[index];
    }

    private string GetLocationName()
    {
        if (activeSignal != null && !string.IsNullOrWhiteSpace(activeSignal.locationName))
            return activeSignal.locationName.Trim();
        return GetGpsCoordinateTitle();
    }

    private string GetGpsCoordinateTitle()
    {
        if (activeSignal == null) return "gps --, --";
        return $"gps {activeSignal.latitude:F5}, {activeSignal.longitude:F5}";
    }

    private bool IsRealLocation()
    {
        return activeSignal != null && activeSignal.transmissionType == TransmissionType.Location;
    }

    private void ApplyOfferStep()
    {
        step = Step.Offer;
        StopRecordingIfNeeded();
        string item = string.IsNullOrWhiteSpace(claimedItem) ? "material" : claimedItem.Trim();
        string message = IsRealLocation()
            ? $"{GetSenderName()} left {GetQuantityGrams()} grams of {item}."
            : $"{GetSenderName()} sent {GetQuantityGrams()} grams of {item}.";
        if (titleLabel != null)
        {
            titleLabel.fontSize = IsRealLocation() ? 26f : 14f;
            titleLabel.text = IsRealLocation() ? $"> {GetLocationName().ToUpper()}" : $"> {GetGpsCoordinateTitle()}";
        }
        SetPromptText($"{message}\nwould you like to accept?");
        PlaceButtonsBelowQuestion();
        if (inputField != null) inputField.gameObject.SetActive(false);
        if (recordButton != null) recordButton.gameObject.SetActive(false);
        if (cancelButton != null) cancelButton.gameObject.SetActive(true);
        if (nextButton != null) nextButton.gameObject.SetActive(true);
        if (nextButtonLabel != null) nextButtonLabel.text = "[yes]";
        var noLabel = cancelButton != null ? cancelButton.GetComponentInChildren<TextMeshProUGUI>(true) : null;
        if (noLabel != null) noLabel.text = "[no]";
        if (statusLabel != null) statusLabel.text = "";
    }

    private void ApplySendAskStep(string status)
    {
        step = Step.SendAsk;
        StopRecordingIfNeeded();
        SetPromptText("Send transmission.");
        PlaceButtonsBelowQuestion();
        if (inputField != null) inputField.gameObject.SetActive(false);
        if (recordButton != null) recordButton.gameObject.SetActive(false);
        if (cancelButton != null) cancelButton.gameObject.SetActive(true);
        if (nextButton != null) nextButton.gameObject.SetActive(true);
        if (nextButtonLabel != null) nextButtonLabel.text = "[yes]";
        var noLabel = cancelButton != null ? cancelButton.GetComponentInChildren<TextMeshProUGUI>(true) : null;
        if (noLabel != null) noLabel.text = "[no]";
        if (statusLabel != null) statusLabel.text = status;
    }

    private void ApplyActionStep()
    {
        step = Step.Action;
        SetPromptText("What do you want to transmit?");
        PlaceButtonsAtBottom();
        if (inputField != null)
        {
            inputField.gameObject.SetActive(true);
            inputField.text = "";
            if (inputField.placeholder is TextMeshProUGUI ph) ph.text = "<i>type transmission…</i>";
            inputField.ActivateInputField();
        }
        if (recordButton != null) recordButton.gameObject.SetActive(true);
        if (cancelButton != null) cancelButton.gameObject.SetActive(false);
        if (nextButton != null) nextButton.gameObject.SetActive(true);
        if (nextButtonLabel != null) nextButtonLabel.text = "SEND";
        if (statusLabel != null) statusLabel.text = "";
    }

    private void OnNext()
    {
        if (activeSignal == null)
        {
            Hide();
            return;
        }

        if (step == Step.Offer)
        {
            TransmissionManager.Instance?.AcceptItem(claimedItem, null, 0);
            ApplySendAskStep($"Accepted {claimedItem}.");
            return;
        }

        if (step == Step.SendAsk)
        {
            ApplyActionStep();
            return;
        }

        if (step != Step.Action) return;

        string value = inputField != null ? inputField.text : null;
        if (string.IsNullOrWhiteSpace(value))
        {
            if (statusLabel != null) statusLabel.text = "Type what you want to transmit.";
            return;
        }
        claimedAction = value.Trim();
        var tm = TransmissionManager.Instance;
        if (tm == null)
        {
            if (statusLabel != null) statusLabel.text = "TransmissionManager missing.";
            return;
        }

        step = Step.Submitting;
        isSubmitting = true;
        spinnerIndex = -1;
        nextSpinnerTick = 0f;
        if (recordButton != null) recordButton.gameObject.SetActive(false);
        if (cancelButton != null) cancelButton.gameObject.SetActive(false);
        if (closeButton != null) closeButton.gameObject.SetActive(true);
        if (nextButton != null) nextButton.gameObject.SetActive(false);
        if (inputField != null) inputField.gameObject.SetActive(false);
        if (promptLabel != null) promptLabel.gameObject.SetActive(false);
        if (statusLabel != null) statusLabel.text = "";
        if (transmittingGO != null) transmittingGO.SetActive(true);

        Debug.Log($"[LocationExchangeModal] TRANSMIT item='{claimedItem}' actionLen={claimedAction?.Length ?? 0} signal={activeSignal.id}");
        tm.StartTransmitterInteraction(activeSignal, claimedItem, claimedAction);
    }

    private void OnNo()
    {
        if (step == Step.Offer)
        {
            ApplySendAskStep($"Declined {claimedItem}.");
            return;
        }
        if (step == Step.SendAsk)
        {
            Hide();
            return;
        }
        Hide();
    }

    private void PlaceButtonsBelowQuestion()
    {
        if (buttonsRt == null) return;
        buttonsRt.anchorMin = new Vector2(0f, 0.48f);
        buttonsRt.anchorMax = new Vector2(1f, 0.56f);
        buttonsRt.offsetMin = new Vector2(20f, 0f);
        buttonsRt.offsetMax = new Vector2(-20f, 0f);
        if (nextButton != null) nextButton.transform.SetSiblingIndex(0);
        if (cancelButton != null) cancelButton.transform.SetSiblingIndex(1);
    }

    private void PlaceButtonsAtBottom()
    {
        if (buttonsRt == null) return;
        buttonsRt.anchorMin = new Vector2(0f, 0.08f);
        buttonsRt.anchorMax = new Vector2(1f, 0.19f);
        buttonsRt.offsetMin = new Vector2(20f, 0f);
        buttonsRt.offsetMax = new Vector2(-20f, 0f);
        if (recordButton != null) recordButton.transform.SetSiblingIndex(0);
        if (nextButton != null) nextButton.transform.SetSiblingIndex(1);
    }

    private void SetPromptText(string text)
    {
        if (promptLabel == null) return;
        StopPromptTypewriter();
        promptTypeCoroutine = StartCoroutine(TypePrompt(text ?? ""));
    }

    private void StopPromptTypewriter()
    {
        if (promptTypeCoroutine == null) return;
        StopCoroutine(promptTypeCoroutine);
        promptTypeCoroutine = null;
    }

    private IEnumerator TypePrompt(string text)
    {
        promptLabel.text = "";
        for (int i = 0; i < text.Length; i++)
        {
            promptLabel.text = text.Substring(0, i + 1);
            yield return new WaitForSecondsRealtime(0.012f);
        }
        promptTypeCoroutine = null;
    }

    private void ToggleRecord()
    {
        if (isRecording)
        {
            StopRecordingIfNeeded();
            if (statusLabel != null) statusLabel.text = "Recorded.";
            if (inputField != null) inputField.ActivateInputField();
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
            if (recordButtonLabel != null) recordButtonLabel.text = "STOP";
            if (statusLabel != null) statusLabel.text = "Recording…";
        }
        catch (System.Exception e)
        {
            if (statusLabel != null) statusLabel.text = $"Mic error: {e.Message}";
            isRecording = false;
            if (recordButtonLabel != null) recordButtonLabel.text = "RECORD";
        }
    }

    private void StopRecordingIfNeeded()
    {
        if (!isRecording)
        {
            if (recordButtonLabel != null) recordButtonLabel.text = "RECORD";
            return;
        }

        try { KiloSpeechRecognizer.Stop(); } catch { }
        isRecording = false;
        if (recordButtonLabel != null) recordButtonLabel.text = "RECORD";
    }
}
