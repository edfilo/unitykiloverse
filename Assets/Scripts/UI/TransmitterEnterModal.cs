using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TransmitterEnterModal : MonoBehaviour
{
    public static TransmitterEnterModal Instance { get; private set; }

    private TMP_FontAsset font;
    private GameObject root;
    private RectTransform panelRt;
    private RectTransform listRt;
    private GameObject inputRowGO;
    private TextMeshProUGUI promptLabel;
    private TMP_InputField actionInput;
    private Button submitButton;
    private Button closeButton;
    private TextMeshProUGUI statusLabel;
    private Button pttButton;
    private TextMeshProUGUI pttButtonLabel;
    private GameObject transmittingGO;
    private TextMeshProUGUI transmittingText;
    private TextMeshProUGUI transmittingSpinner;
    private int spinnerIndex;
    private float nextSpinnerTick;
    private bool isSubmitting;
    private bool isRecording;
    private bool contrastActive;
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

    private readonly List<GameObject> artifactButtons = new List<GameObject>();
    private Signal activeSignal;
    private string selectedArtifact;

    private static readonly Color TerminalGreen = new Color(0.56f, 1f, 0.62f, 1f);
    private static readonly Color TerminalDim = new Color(0.56f, 1f, 0.62f, 0.65f);

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
    }

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

    void BuildUI()
    {
        root = new GameObject("TransmitterEnterModal", typeof(RectTransform));
        root.transform.SetParent(K1L0CanvasRoot.Modal, false);
        var rootRt = (RectTransform)root.transform;
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        // Dimmer
        var dimGO = new GameObject("Dim", typeof(RectTransform), typeof(Image));
        dimGO.transform.SetParent(root.transform, false);
        var dimRt = (RectTransform)dimGO.transform;
        dimRt.anchorMin = Vector2.zero;
        dimRt.anchorMax = Vector2.one;
        dimRt.offsetMin = Vector2.zero;
        dimRt.offsetMax = Vector2.zero;
        dimGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.99f);

        // Panel
        var panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(root.transform, false);
        panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(680f, 760f);
        panelRt.anchoredPosition = Vector2.zero;

        var chrome = K1L0GlassFactory.AttachChrome(panel.transform, "TxModal", K1L0GlassFactory.PanelStyle);
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
        K1L0GlassFactory.AttachTerminalStatic(panel.transform, "TxStatic");

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
        title.fontSize = 28f;
        title.color = TerminalGreen;
        title.alignment = TextAlignmentOptions.TopLeft;
        title.text = "> MUSICAL TRANSMITTER";

        closeButton = K1L0GlassFactory.CreateTerminalCloseButton(panel.transform, font, Hide);

        // Artifact list container
        var listGO = new GameObject("ArtifactList", typeof(RectTransform));
        listGO.transform.SetParent(panel.transform, false);
        listRt = listGO.GetComponent<RectTransform>();
        listRt.anchorMin = new Vector2(0f, 0.35f);
        listRt.anchorMax = new Vector2(1f, 0.92f);
        listRt.offsetMin = new Vector2(20f, 0f);
        listRt.offsetMax = new Vector2(-20f, 0f);

        var listLayout = listGO.AddComponent<GridLayoutGroup>();
        listLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        listLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
        listLayout.childAlignment = TextAnchor.UpperLeft;
        listLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        listLayout.constraintCount = 3;
        listLayout.cellSize = new Vector2(120f, 120f);
        listLayout.spacing = new Vector2(10f, 10f);

        // Prompt
        var promptGO = new GameObject("Prompt", typeof(RectTransform));
        promptGO.transform.SetParent(panel.transform, false);
        var promptRt = promptGO.GetComponent<RectTransform>();
        promptRt.anchorMin = new Vector2(0f, 0.26f);
        promptRt.anchorMax = new Vector2(1f, 0.35f);
        promptRt.offsetMin = new Vector2(20f, 0f);
        promptRt.offsetMax = new Vector2(-20f, 0f);

        promptLabel = promptGO.AddComponent<TextMeshProUGUI>();
        promptLabel.font = font;
        promptLabel.fontSize = 18f;
        promptLabel.color = TerminalDim;
        promptLabel.alignment = TextAlignmentOptions.TopLeft;
        promptLabel.text = "Select item to transmit.";

        // Input row
        inputRowGO = new GameObject("InputRow", typeof(RectTransform));
        inputRowGO.transform.SetParent(panel.transform, false);
        var inputRt = inputRowGO.GetComponent<RectTransform>();
        inputRt.anchorMin = new Vector2(0f, 0.12f);
        inputRt.anchorMax = new Vector2(1f, 0.26f);
        inputRt.offsetMin = new Vector2(20f, 0f);
        inputRt.offsetMax = new Vector2(-20f, 0f);

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

        var placeholder = MakeInputText(tfGO.transform, "Placeholder", "<i>what would you like to do…</i>", TerminalDim);
        var text = MakeInputText(tfGO.transform, "Text", "", TerminalGreen);
        actionInput.placeholder = placeholder;
        actionInput.textComponent = text;
        actionInput.pointSize = 16f;
        actionInput.text = "";

        // Buttons row
        var buttonsGO = new GameObject("Buttons", typeof(RectTransform));
        buttonsGO.transform.SetParent(panel.transform, false);
        var buttonsRt = buttonsGO.GetComponent<RectTransform>();
        buttonsRt.anchorMin = new Vector2(0f, 0.02f);
        buttonsRt.anchorMax = new Vector2(1f, 0.12f);
        buttonsRt.offsetMin = new Vector2(20f, 0f);
        buttonsRt.offsetMax = new Vector2(-20f, 0f);

        var h = buttonsGO.AddComponent<HorizontalLayoutGroup>();
        h.childAlignment = TextAnchor.MiddleRight;
        h.childControlWidth = false;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = true;
        h.spacing = 10f;

        pttButton = MakeButton(buttonsGO.transform, "Record", "RECORD");
        pttButtonLabel = pttButton.GetComponentInChildren<TextMeshProUGUI>(true);
        pttButton.onClick.AddListener(ToggleRecord);

        submitButton = MakeButton(buttonsGO.transform, "Submit", "SEND");
        submitButton.onClick.AddListener(OnSubmit);

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
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.text = label;

        return btn;
    }

    Button MakeArtifactButton(string artifact)
    {
        var go = new GameObject($"Artifact_{artifact}", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(listRt, false);
        artifactButtons.Add(go);

        go.AddComponent<LayoutElement>();

        var img = go.GetComponent<Image>();
        img.sprite = K1L0GlassFactory.ControlRectSprite;
        img.type = Image.Type.Sliced;
        img.color = new Color(0.02f, 0.07f, 0.02f, 0.70f);

        var btn = go.GetComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.targetGraphic = img;

        var labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(go.transform, false);
        var lrt = labelGO.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = new Vector2(14f, 30f);
        lrt.offsetMax = new Vector2(-14f, -6f);

        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.fontSize = 12f;
        tmp.color = TerminalGreen;
        tmp.alignment = TextAlignmentOptions.Top;
        tmp.enableWordWrapping = true;
        tmp.text = artifact;

        var selectGO = new GameObject("SelectLabel", typeof(RectTransform));
        selectGO.transform.SetParent(go.transform, false);
        var srt = selectGO.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0f, 0f);
        srt.anchorMax = new Vector2(1f, 0f);
        srt.pivot = new Vector2(0.5f, 0f);
        srt.anchoredPosition = new Vector2(0f, 8f);
        srt.sizeDelta = new Vector2(0f, 22f);

        var selectTmp = selectGO.AddComponent<TextMeshProUGUI>();
        selectTmp.font = font;
        selectTmp.fontSize = 11f;
        selectTmp.color = TerminalGreen;
        selectTmp.alignment = TextAlignmentOptions.Center;
        selectTmp.text = "SELECT";
        selectTmp.raycastTarget = false;

        btn.onClick.AddListener(() => SelectArtifact(artifact));
        return btn;
    }

    void SelectArtifact(string artifact)
    {
        selectedArtifact = artifact;
        if (promptLabel != null)
        {
            promptLabel.text = $"What would you like to do with the {artifact}?";
            promptLabel.color = TerminalGreen;
        }
        if (statusLabel != null) statusLabel.text = "";

        // After selecting an item, hide the items and reveal the form.
        if (listRt != null) listRt.gameObject.SetActive(false);
        if (inputRowGO != null) inputRowGO.SetActive(true);
        if (submitButton != null) submitButton.gameObject.SetActive(true);
        if (pttButton != null) pttButton.gameObject.SetActive(true);
    }

    void ClearArtifacts()
    {
        for (int i = 0; i < artifactButtons.Count; i++)
        {
            if (artifactButtons[i] != null) Destroy(artifactButtons[i]);
        }
        artifactButtons.Clear();
    }

    public void Show(Signal transmitterSignal)
    {
        activeSignal = transmitterSignal;
        ApplySafeAreaSizing();
        selectedArtifact = null;
        if (actionInput != null) actionInput.text = "";
        if (statusLabel != null) statusLabel.text = "";
        isSubmitting = false;
        if (transmittingGO != null) transmittingGO.SetActive(false);
        if (submitButton != null)
        {
            submitButton.interactable = true;
            submitButton.gameObject.SetActive(false);
        }
        if (closeButton != null) closeButton.gameObject.SetActive(true);
        if (pttButton != null) pttButton.gameObject.SetActive(false);
        if (listRt != null) listRt.gameObject.SetActive(true);
        if (promptLabel != null)
        {
            promptLabel.gameObject.SetActive(true);
            promptLabel.text = "Select item to transmit.";
            promptLabel.color = TerminalDim;
        }
        if (inputRowGO != null) inputRowGO.SetActive(false);
        StopRecordingIfNeeded();

        ClearArtifacts();
        RefreshInventory();

        if (root != null) root.SetActive(true);
        if (!contrastActive)
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
        var artifacts = tm != null ? tm.GetKnownArtifacts() : new List<string>();
        if (artifacts.Count == 0)
        {
            promptLabel.text = "No items yet. Walk to a blue portal, press enter portal, then press ACCEPT.";
            promptLabel.color = TerminalDim;
            if (submitButton != null) submitButton.interactable = false;
            selectedArtifact = null;
            return;
        }

        promptLabel.text = "Select item to transmit.";
        promptLabel.color = TerminalDim;
        if (submitButton != null) submitButton.interactable = true;
        for (int i = 0; i < artifacts.Count; i++)
            MakeArtifactButton(artifacts[i]);
    }

    public void Hide()
    {
        activeSignal = null;
        selectedArtifact = null;
        StopRecordingIfNeeded();
        if (root != null) root.SetActive(false);
        if (contrastActive)
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
            if (pttButtonLabel != null) pttButtonLabel.text = "STOP";
            statusLabel.text = "Recording…";
        }
        catch (System.Exception e)
        {
            statusLabel.text = $"Mic error: {e.Message}";
            isRecording = false;
            if (pttButtonLabel != null) pttButtonLabel.text = "RECORD";
        }
    }

    void StopRecordingIfNeeded()
    {
        if (!isRecording)
        {
            if (pttButtonLabel != null) pttButtonLabel.text = "RECORD";
            return;
        }

        try { KiloSpeechRecognizer.Stop(); } catch { }
        isRecording = false;
        if (pttButtonLabel != null) pttButtonLabel.text = "RECORD";

        float dur = Mathf.Max(0f, Time.unscaledTime - recordingStartTime);
        statusLabel.text = $"Recorded {dur:F1}s.";
    }

    void OnSubmit()
    {
        if (activeSignal == null)
        {
            Hide();
            return;
        }
        if (string.IsNullOrWhiteSpace(selectedArtifact))
        {
            statusLabel.text = "Pick an artifact first.";
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

        var tm = TransmissionManager.Instance;
        if (tm != null)
        {
            Debug.Log($"[TransmitterEnterModal] SEND artifact='{selectedArtifact}' actionLen={(actionInput != null ? actionInput.text.Length : 0)} signal={activeSignal.id}");
            tm.StartTransmitterInteraction(activeSignal, selectedArtifact, action);
        }
        else
        {
            statusLabel.text = "TransmissionManager missing.";
            submitButton.interactable = true;
            return;
        }

        // Keep modal open; hide controls and show "Transmitting".
        isSubmitting = true;
        spinnerIndex = -1;
        nextSpinnerTick = 0f;
        if (submitButton != null) submitButton.gameObject.SetActive(false);
        if (closeButton != null) closeButton.gameObject.SetActive(true);
        if (pttButton != null) pttButton.gameObject.SetActive(false);
        if (listRt != null) listRt.gameObject.SetActive(false);
        if (promptLabel != null) promptLabel.gameObject.SetActive(false);
        if (inputRowGO != null) inputRowGO.SetActive(false);
        if (transmittingGO != null) transmittingGO.SetActive(true);
        if (statusLabel != null) statusLabel.text = "";
    }
}
