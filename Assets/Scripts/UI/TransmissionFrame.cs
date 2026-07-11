using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using UnityEngine.Video;
using TMPro;
using System.Collections;

/// <summary>
/// Full-screen terminal frame shown when entering a location or pursuit signal.
/// Displays transmission content (dialog, character, location) with typewriter reveal.
/// </summary>
public class TransmissionFrame : MonoBehaviour
{
    private static TransmissionFrame _instance;
    public static TransmissionFrame Instance => _instance;

    private static readonly Color TerminalGreen = new Color(0.47f, 1f, 0.54f, 1f);
    private static readonly Color TerminalDim = new Color(0.47f, 1f, 0.54f, 0.5f);

    private GameObject frameRoot;
    private TextMeshProUGUI locationHeader; // big location name at top
    private TextMeshProUGUI headerText;     // smaller meta line
    private TextMeshProUGUI bodyText;
    private TextMeshProUGUI statusText;
    private RawImage heroImage;
    private RawImage videoImage;            // overlays heroImage once video is ready
    private VideoPlayer videoPlayer;
    private RenderTexture videoRT;
    private TransmissionFXScheduler fxScheduler;
    private Button closeBtn;
    private GameObject acceptBtnGO;
    private Button acceptBtn;
    private TextMeshProUGUI acceptBtnLabel;
    private CanvasGroup canvasGroup;
    private Coroutine typewriterRoutine;
    private Coroutine imageLoadRoutine;
    private string pendingLocationName;
    private string pendingCategory;
    private string currentImageUrl;
    private string currentVideoUrl;
    private string currentAudioUrl;
    private AudioSource musicSource;
    private Coroutine musicLoadRoutine;
    private string pinnedStoryId;           // the storyId this frame is showing
    private bool shotDelivered;             // true once OnTransmissionReady has been handled
    private string pinnedCharacter;
    private string pinnedArtifact;
    private string pinnedShotStage;         // last status from OnShotProgress
    private int pinnedShotNumber;
    private bool acceptRequired;
    private string acceptArtifact;
    private string acceptStoryId;
    private bool sendTransmissionMode;
    private bool dismissOnlyMode;
    private bool initialized;
    private bool contrastActive;
    private Vector2 bodyDefaultAnchorMin;
    private Vector2 bodyDefaultAnchorMax;
    private Vector2 bodyDefaultPivot;
    private Vector2 bodyDefaultAnchoredPosition;
    private Vector2 bodyDefaultSizeDelta;
    private TextAlignmentOptions bodyDefaultAlignment;
    private float bodyDefaultFontSize;
    private Vector2 acceptDefaultAnchorMin;
    private Vector2 acceptDefaultAnchorMax;
    private Vector2 acceptDefaultPivot;
    private Vector2 acceptDefaultAnchoredPosition;
    private Vector2 acceptDefaultSizeDelta;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    public void Initialize()
    {
        if (initialized) return;
        if (K1L0CanvasRoot.Modal == null)
        {
            Debug.LogWarning("[TransmissionFrame] Initialize deferred: K1L0CanvasRoot.Modal missing");
            return;
        }

        CreateUI();
        initialized = frameRoot != null && canvasGroup != null;
        if (!initialized)
        {
            Debug.LogWarning("[TransmissionFrame] Initialize failed: UI roots missing");
            return;
        }

        // Subscribe to transmission events
        var tm = FindFirstObjectByType<TransmissionManager>();
        if (tm != null)
        {
            tm.OnTransmissionReady += OnTransmissionReady;
            tm.OnStoryShellReady += OnStoryShellReady;
            tm.OnShotProgress += OnShotProgress;
            Debug.Log("[TransmissionFrame] Subscribed to OnTransmissionReady + OnStoryShellReady + OnShotProgress");
        }

        frameRoot.SetActive(false);
    }

    void OnDestroy()
    {
        var tm = FindFirstObjectByType<TransmissionManager>();
        if (tm != null)
        {
            tm.OnTransmissionReady -= OnTransmissionReady;
            tm.OnStoryShellReady -= OnStoryShellReady;
            tm.OnShotProgress -= OnShotProgress;
        }
        if (_instance == this) _instance = null;
    }

	    void CreateUI()
	    {
	        // Full-screen overlay on Modal canvas (highest sorting order)
	        frameRoot = new GameObject("TransmissionFrame", typeof(RectTransform));
	        frameRoot.transform.SetParent(K1L0CanvasRoot.Modal, false);
	        // Ensure this sits above any other modal children without creating
	        // a nested Canvas (nested Canvases can break CanvasScaler sizing on iOS).
	        frameRoot.transform.SetAsLastSibling();

	        var rootRt = frameRoot.GetComponent<RectTransform>();
	        rootRt.anchorMin = Vector2.zero;
	        rootRt.anchorMax = Vector2.one;
	        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        canvasGroup = frameRoot.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;

        // Transparent hit target; content/text provide their own contrast.
        var bg = frameRoot.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0f);
        bg.raycastTarget = true;
        K1L0GlassFactory.AttachTerminalStatic(frameRoot.transform, "TransmissionStatic");

        var font = Resources.Load<TMP_FontAsset>("Fonts/IBMPlexMono-Regular SDF");
        if (font == null) font = TMP_Settings.defaultFontAsset;

        var cardGO = new GameObject("StoryCard");
        cardGO.transform.SetParent(frameRoot.transform, false);
        var cardRt = cardGO.AddComponent<RectTransform>();
        cardRt.anchorMin = Vector2.zero;
        cardRt.anchorMax = Vector2.one;
        cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.offsetMin = Vector2.zero;
        cardRt.offsetMax = Vector2.zero;

        // No card background — drop shadow on text provides legibility
        // directly over the hero image.

        // Hero image — fills the entire card
        var heroGO = new GameObject("HeroImage");
        heroGO.transform.SetParent(cardGO.transform, false);
        var heroRt = heroGO.AddComponent<RectTransform>();
        heroRt.anchorMin = Vector2.zero;
        heroRt.anchorMax = Vector2.one;
        heroRt.offsetMin = Vector2.zero;
        heroRt.offsetMax = Vector2.zero;
        heroImage = heroGO.AddComponent<RawImage>();
        heroImage.color = new Color(1f, 1f, 1f, 0f);
        heroImage.raycastTarget = false;

        // Video layer — sits above hero image, same rect, hidden until videoUrl arrives
        var videoGO = new GameObject("HeroVideo");
        videoGO.transform.SetParent(cardGO.transform, false);
        var videoRt = videoGO.AddComponent<RectTransform>();
        videoRt.anchorMin = Vector2.zero;
        videoRt.anchorMax = Vector2.one;
        videoRt.offsetMin = Vector2.zero;
        videoRt.offsetMax = Vector2.zero;
        videoImage = videoGO.AddComponent<RawImage>();
        videoImage.color = new Color(1f, 1f, 1f, 0f);
        videoImage.raycastTarget = false;

        // 480p 9:16 render target — matches backend resolution
        videoRT = new RenderTexture(576, 1024, 0, RenderTextureFormat.ARGB32);
        videoRT.Create();
        videoImage.texture = videoRT;

        videoPlayer = videoGO.AddComponent<VideoPlayer>();
        videoPlayer.source = VideoSource.Url;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = videoRT;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.None; // dialog audio is separate
        videoPlayer.isLooping = true;
        videoPlayer.playOnAwake = false;
        videoPlayer.waitForFirstFrame = true;
        videoPlayer.prepareCompleted += OnVideoPrepared;
        videoPlayer.errorReceived += (vp, msg) => Debug.LogWarning($"[TransmissionFrame] Video error: {msg}");

        // Client-side post-FX (chroma shift, grain, vignette, crop/zoom cuts, etc.) —
        // ported from the old server-side ffmpeg composite. Re-rolls a schedule on every Prepare.
        fxScheduler = videoGO.AddComponent<TransmissionFXScheduler>();
        fxScheduler.sourceSize = new Vector2(videoRT.width, videoRT.height);
        fxScheduler.Bind(videoImage, videoPlayer);

        // Music — ACE-Step generated mp3 streamed from CDN; loops while the slide is open.
        musicSource = frameRoot.AddComponent<AudioSource>();
        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicSource.volume = 0.85f;

        // No top/bottom scrims — drop shadow on text handles legibility directly over the image.

        // Location header — overlaid near top-left of the card
        var locHeadGO = new GameObject("LocHeader");
        locHeadGO.transform.SetParent(cardGO.transform, false);
        var locHeadRt = locHeadGO.AddComponent<RectTransform>();
        locHeadRt.anchorMin = new Vector2(0f, 1f);
        locHeadRt.anchorMax = new Vector2(1f, 1f);
        locHeadRt.pivot = new Vector2(0f, 1f);
        locHeadRt.anchoredPosition = new Vector2(20f, -20f);
        locHeadRt.sizeDelta = new Vector2(-40f, 70f);
        locationHeader = locHeadGO.AddComponent<TextMeshProUGUI>();
        locationHeader.font = font;
        locationHeader.fontSize = 28;
        locationHeader.color = TerminalGreen;
        locationHeader.alignment = TextAlignmentOptions.TopLeft;
        locationHeader.enableWordWrapping = true;
        locationHeader.raycastTarget = false;
        locationHeader.fontStyle = FontStyles.Bold;

        // Meta line — source/status under the location
        var headerGO = new GameObject("Header");
        headerGO.transform.SetParent(cardGO.transform, false);
        var headerRt = headerGO.AddComponent<RectTransform>();
        headerRt.anchorMin = new Vector2(0f, 1f);
        headerRt.anchorMax = new Vector2(1f, 1f);
        headerRt.pivot = new Vector2(0f, 1f);
        headerRt.anchoredPosition = new Vector2(20f, -90f);
        headerRt.sizeDelta = new Vector2(-40f, 80f);
        headerText = headerGO.AddComponent<TextMeshProUGUI>();
        headerText.font = font;
        headerText.fontSize = 13;
        headerText.color = TerminalDim;
        headerText.alignment = TextAlignmentOptions.TopLeft;
        headerText.enableWordWrapping = true;
        headerText.raycastTarget = false;

        // Dialog body — overlaid at bottom of the card
        var bodyGO = new GameObject("Body");
        bodyGO.transform.SetParent(cardGO.transform, false);
        var bodyRt = bodyGO.AddComponent<RectTransform>();
        bodyRt.anchorMin = new Vector2(0f, 0f);
        bodyRt.anchorMax = new Vector2(1f, 0f);
        bodyRt.pivot = new Vector2(0f, 0f);
        bodyRt.anchoredPosition = new Vector2(20f, 50f);
        bodyRt.sizeDelta = new Vector2(-40f, 200f);
        bodyText = bodyGO.AddComponent<TextMeshProUGUI>();
        bodyText.font = font;
        bodyText.fontSize = 20;
        bodyText.color = TerminalGreen;
        bodyText.alignment = TextAlignmentOptions.BottomLeft;
        bodyText.enableWordWrapping = true;
        bodyText.raycastTarget = false;
        bodyDefaultAnchorMin = bodyRt.anchorMin;
        bodyDefaultAnchorMax = bodyRt.anchorMax;
        bodyDefaultPivot = bodyRt.pivot;
        bodyDefaultAnchoredPosition = bodyRt.anchoredPosition;
        bodyDefaultSizeDelta = bodyRt.sizeDelta;
        bodyDefaultAlignment = bodyText.alignment;
        bodyDefaultFontSize = bodyText.fontSize;

        // Status line — very bottom of the card
        var statusGO = new GameObject("Status");
        statusGO.transform.SetParent(cardGO.transform, false);
        var statusRt = statusGO.AddComponent<RectTransform>();
        statusRt.anchorMin = new Vector2(0f, 0f);
        statusRt.anchorMax = new Vector2(1f, 0f);
        statusRt.pivot = new Vector2(0f, 0f);
        statusRt.anchoredPosition = new Vector2(20f, 16f);
        statusRt.sizeDelta = new Vector2(-40f, 24f);
        statusText = statusGO.AddComponent<TextMeshProUGUI>();
        statusText.font = font;
        statusText.fontSize = 11;
        statusText.color = TerminalDim;
        statusText.alignment = TextAlignmentOptions.BottomLeft;
        statusText.raycastTarget = false;

        closeBtn = K1L0GlassFactory.CreateTerminalCloseButton(frameRoot.transform, font, Hide);

        // ACCEPT button (shown for transmitter transmissions)
        acceptBtnGO = new GameObject("AcceptBtn");
        acceptBtnGO.transform.SetParent(cardGO.transform, false);
        var acceptRt = acceptBtnGO.AddComponent<RectTransform>();
        acceptRt.anchorMin = new Vector2(0.5f, 0f);
        acceptRt.anchorMax = new Vector2(0.5f, 0f);
        acceptRt.pivot = new Vector2(0.5f, 0f);
        acceptRt.anchoredPosition = new Vector2(0f, 270f);
        acceptRt.sizeDelta = new Vector2(520f, 86f);

        var acceptBg = acceptBtnGO.AddComponent<Image>();
        acceptBg.sprite = K1L0GlassFactory.ControlRectSprite;
        acceptBg.type = Image.Type.Sliced;
        acceptBg.color = new Color(0.02f, 0.09f, 0.02f, 0.92f);
        acceptBg.raycastTarget = true;

        acceptBtn = acceptBtnGO.AddComponent<Button>();
        acceptBtn.targetGraphic = acceptBg;
        acceptBtn.transition = Selectable.Transition.None;
        acceptBtn.onClick.AddListener(OnAcceptTapped);

        var acceptLabelGO = new GameObject("Label");
        acceptLabelGO.transform.SetParent(acceptBtnGO.transform, false);
        var acceptLabelRt = acceptLabelGO.AddComponent<RectTransform>();
        acceptLabelRt.anchorMin = Vector2.zero;
        acceptLabelRt.anchorMax = Vector2.one;
        acceptLabelRt.offsetMin = Vector2.zero;
        acceptLabelRt.offsetMax = Vector2.zero;

        acceptBtnLabel = acceptLabelGO.AddComponent<TextMeshProUGUI>();
        acceptBtnLabel.font = font;
        acceptBtnLabel.fontSize = 34;
        acceptBtnLabel.text = "[ACCEPT]";
        acceptBtnLabel.color = TerminalGreen;
        acceptBtnLabel.alignment = TextAlignmentOptions.Center;
        acceptBtnLabel.fontStyle = FontStyles.Bold;
        acceptBtnLabel.raycastTarget = false;
        acceptDefaultAnchorMin = acceptRt.anchorMin;
        acceptDefaultAnchorMax = acceptRt.anchorMax;
        acceptDefaultPivot = acceptRt.pivot;
        acceptDefaultAnchoredPosition = acceptRt.anchoredPosition;
        acceptDefaultSizeDelta = acceptRt.sizeDelta;

        acceptBtnGO.SetActive(false);
    }

    private void UseDefaultBodyLayout()
    {
        if (bodyText == null) return;
        var rt = bodyText.rectTransform;
        rt.anchorMin = bodyDefaultAnchorMin;
        rt.anchorMax = bodyDefaultAnchorMax;
        rt.pivot = bodyDefaultPivot;
        rt.anchoredPosition = bodyDefaultAnchoredPosition;
        rt.sizeDelta = bodyDefaultSizeDelta;
        bodyText.alignment = bodyDefaultAlignment;
        bodyText.fontSize = bodyDefaultFontSize;
    }

    private void UseArtifactBodyLayout()
    {
        if (bodyText == null) return;
        var rt = bodyText.rectTransform;
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(-90f, 180f);
        bodyText.alignment = TextAlignmentOptions.Center;
        bodyText.fontSize = 30f;
    }

    private void UseDefaultAcceptLayout()
    {
        if (acceptBtnGO == null) return;
        var rt = acceptBtnGO.GetComponent<RectTransform>();
        if (rt == null) return;
        rt.anchorMin = acceptDefaultAnchorMin;
        rt.anchorMax = acceptDefaultAnchorMax;
        rt.pivot = acceptDefaultPivot;
        rt.anchoredPosition = acceptDefaultAnchoredPosition;
        rt.sizeDelta = acceptDefaultSizeDelta;
    }

    private void UseArtifactAcceptLayout()
    {
        if (acceptBtnGO == null) return;
        var rt = acceptBtnGO.GetComponent<RectTransform>();
        if (rt == null) return;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, -140f);
        rt.sizeDelta = new Vector2(340f, 78f);
    }

    /// <summary>Play parent video once (no loop), then switch to responseUrl on completion.</summary>
    public void ReplaySequence(string parentUrl, string responseUrl, string audioUrl, string thumbUrl)
    {
        // Start with the parent video playing once (no loop).
        ReplayStoredTransmission(parentUrl, "", thumbUrl, "TRANSMISSION");
        if (videoPlayer == null) return;
        videoPlayer.isLooping = false;

        void OnEnd(VideoPlayer vp)
        {
            vp.loopPointReached -= OnEnd;
            vp.isLooping = true;
            currentVideoUrl = "";  // force URL update in ReplayStoredTransmission
            ReplayStoredTransmission(responseUrl, audioUrl, thumbUrl, "REPLY");
        }
        videoPlayer.loopPointReached += OnEnd;
    }

    /// <summary>Replay a stored transmission (user-section tap) — opens the frame and plays raw WAN video + separate music track with client FX.</summary>
    public void ReplayStoredTransmission(string videoUrl, string audioUrl, string thumbUrl, string locationName = "")
    {
        if (!initialized || frameRoot == null || canvasGroup == null)
            Initialize();
        if (frameRoot == null || canvasGroup == null) return;

        frameRoot.SetActive(true);
        frameRoot.transform.SetAsLastSibling();
        if (!contrastActive)
        {
            K1L0ModalHudMode.Begin();
            K1L0ModalContrastMode.Begin(this);
            contrastActive = true;
        }
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        // Minimal chrome: hide accept/close affordances differences and just show the playback.
        sendTransmissionMode = false;
        dismissOnlyMode = true;
        acceptRequired = false;
        if (acceptBtnGO != null) acceptBtnGO.SetActive(true);
        if (acceptBtnLabel != null) acceptBtnLabel.text = "[DONE]";
        if (closeBtn != null) closeBtn.gameObject.SetActive(true);
        if (locationHeader != null) locationHeader.text = string.IsNullOrEmpty(locationName) ? "TRANSMISSION" : locationName.ToUpperInvariant();
        if (headerText != null) headerText.text = "";
        if (bodyText != null) bodyText.text = "";
        if (statusText != null) statusText.text = "";

        if (!string.IsNullOrEmpty(thumbUrl) && thumbUrl != currentImageUrl)
        {
            currentImageUrl = thumbUrl;
            if (imageLoadRoutine != null) StopCoroutine(imageLoadRoutine);
            imageLoadRoutine = StartCoroutine(LoadImageRoutine(thumbUrl));
        }
        if (!string.IsNullOrEmpty(videoUrl) && videoUrl != currentVideoUrl && videoPlayer != null)
        {
            currentVideoUrl = videoUrl;
            videoPlayer.Stop();
            videoPlayer.url = videoUrl;
            videoPlayer.Prepare();
        }
        if (!string.IsNullOrEmpty(audioUrl) && audioUrl != currentAudioUrl)
        {
            currentAudioUrl = audioUrl;
            if (musicLoadRoutine != null) StopCoroutine(musicLoadRoutine);
            musicLoadRoutine = StartCoroutine(LoadMusicRoutine(audioUrl));
        }
    }

    /// <summary>Show frame immediately with loading state when proximity is met.</summary>
    public void ShowLoading(string locationName, string category, string storyId = null)
    {
        if (!initialized || frameRoot == null || canvasGroup == null)
            Initialize();
        if (frameRoot == null || canvasGroup == null)
        {
            Debug.LogWarning("[TransmissionFrame] ShowLoading failed: not initialized");
            return;
        }

        pendingLocationName = locationName;
        pendingCategory = category;
        pinnedStoryId = storyId;
        shotDelivered = false;
        pinnedCharacter = null;
        pinnedArtifact = null;
        pinnedShotStage = null;
        pinnedShotNumber = 0;
        acceptRequired = false;
        acceptArtifact = null;
        acceptStoryId = null;
        sendTransmissionMode = false;
        dismissOnlyMode = false;
        frameRoot.SetActive(true);
        frameRoot.transform.SetAsLastSibling();
        if (!contrastActive)
        {
            K1L0ModalHudMode.Begin();
            K1L0ModalContrastMode.Begin(this);
            contrastActive = true;
        }
        // Ensure it's visible immediately even if a coroutine gets interrupted.
        canvasGroup.alpha = 1f;
        Debug.Log($"[TransmissionFrame] ShowLoading active={frameRoot.activeInHierarchy} alpha={canvasGroup.alpha} loc='{locationName}' storyId='{storyId}'");
        UseDefaultBodyLayout();
        UseDefaultAcceptLayout();

        // Hide the HUD (pursuit/location rows + ENTER frame) while the
        // transmission slide is open so they don't flash behind the modal.
        var dir = SignalDirectorV2.Instance;
        if (dir != null) dir.SuppressHUD(true);

        // Transmitter UX: minimal header + clear generation status.
        locationHeader.text = "TRANSMITTER";
        headerText.text = "";
        bodyText.text = "";
        statusText.text = "";
        if (acceptBtnGO != null) acceptBtnGO.SetActive(false);
        if (closeBtn != null) closeBtn.gameObject.SetActive(true);

        // Clear any previous image
        if (heroImage != null)
        {
            heroImage.texture = null;
            heroImage.color = new Color(1f, 1f, 1f, 0f);
        }
        currentImageUrl = null;

        // Reset video state so the next shot can bind a new clip
        if (videoPlayer != null && videoPlayer.isPlaying) videoPlayer.Stop();
        if (videoImage != null) videoImage.color = new Color(1f, 1f, 1f, 0f);
        if (videoRT != null)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = videoRT;
            GL.Clear(true, true, Color.clear);
            RenderTexture.active = prev;
        }
        currentVideoUrl = null;

        if (typewriterRoutine != null) StopCoroutine(typewriterRoutine);
        typewriterRoutine = StartCoroutine(LoadingBlink());

        // If the story shell is already primed, seed it now; otherwise header
        // will flip once OnStoryShellReady fires.
        var tm = TransmissionManager.Instance;
        if (tm != null && !string.IsNullOrEmpty(storyId))
        {
            var shell = tm.GetStoryShell(storyId);
            if (!string.IsNullOrEmpty(shell.character))
            {
                pinnedCharacter = shell.character;
                pinnedArtifact = shell.objectName;
                // Premise is LLM-only context — never shown to the user.
            }
        }

        RefreshHeader();
        // FadeIn is nice-to-have; keep it but we've already forced alpha to 1.
        StartCoroutine(FadeIn());
    }

    /// <summary>
    /// Artifact-beam UX: no story generation yet. Just a transmitting banner + accept button.
    /// </summary>
    public void ShowArtifactTransmitting(string artifactName, string fromLabel)
    {
        if (!initialized || frameRoot == null || canvasGroup == null)
            Initialize();
        if (frameRoot == null || canvasGroup == null)
        {
            Debug.LogWarning("[TransmissionFrame] ShowArtifactTransmitting failed: not initialized");
            return;
        }

        shotDelivered = false;
        pinnedStoryId = null;
        pinnedCharacter = null;
        pinnedArtifact = null;
        pinnedShotStage = null;
        pinnedShotNumber = 0;

        acceptRequired = true;
        acceptArtifact = string.IsNullOrWhiteSpace(artifactName) ? null : artifactName.Trim();
        acceptStoryId = null;
        sendTransmissionMode = false;
        dismissOnlyMode = false;

        frameRoot.SetActive(true);
        frameRoot.transform.SetAsLastSibling();
        if (!contrastActive)
        {
            K1L0ModalHudMode.Begin();
            K1L0ModalContrastMode.Begin(this);
            contrastActive = true;
        }
        canvasGroup.alpha = 1f;

        var dir = SignalDirectorV2.Instance;
        if (dir != null) dir.SuppressHUD(true);

        string a = string.IsNullOrWhiteSpace(artifactName) ? "ARTIFACT" : artifactName.Trim();
        string from = string.IsNullOrWhiteSpace(fromLabel) ? "UNKNOWN" : fromLabel.Trim();

        locationHeader.text = "";
        headerText.text = "";
        statusText.text = "";
        UseArtifactBodyLayout();
        UseArtifactAcceptLayout();
        bodyText.text = $"{from} is sending a {a}";

        if (typewriterRoutine != null) StopCoroutine(typewriterRoutine);
        typewriterRoutine = null;

        if (heroImage != null)
        {
            heroImage.texture = null;
            heroImage.color = new Color(1f, 1f, 1f, 0f);
        }
        currentImageUrl = null;

        if (videoPlayer != null && videoPlayer.isPlaying) videoPlayer.Stop();
        if (videoImage != null) videoImage.color = new Color(1f, 1f, 1f, 0f);
        currentVideoUrl = null;

        if (acceptBtnGO != null) acceptBtnGO.SetActive(true);
        if (acceptBtnLabel != null) acceptBtnLabel.text = "[ACCEPT]";
        if (closeBtn != null) closeBtn.gameObject.SetActive(true);

        StartCoroutine(FadeIn());
    }

    void RefreshHeader()
    {
        if (shotDelivered) return;
        if (locationHeader != null && string.Equals(locationHeader.text, "TRANSMITTER", System.StringComparison.OrdinalIgnoreCase))
        {
            headerText.text = "";
            statusText.text = "";
            return;
        }

        string status;
        if (string.IsNullOrEmpty(pinnedCharacter))
        {
            status = "INTERCEPTING SIGNAL";
        }
        else if (string.IsNullOrEmpty(pinnedShotStage))
        {
            status = "AWAITING SHOT";
        }
        else
        {
            switch (pinnedShotStage)
            {
                case "generating":      status = "WRITING TRANSMISSION"; break;
                case "image_ready":     status = "VOICING DIALOG"; break;
                case "rendering_video": status = "ANIMATING FRAME"; break;
                case "ready":           status = "DECODED"; break;
                default:                status = pinnedShotStage.ToUpper(); break;
            }
        }

        if (!string.IsNullOrEmpty(pinnedCharacter))
        {
            string charName = FirstName(pinnedCharacter).ToUpper();
            string seeking = !string.IsNullOrEmpty(pinnedArtifact) ? $"\n> SENDING: {pinnedArtifact}" : "";
            string shotLine = pinnedShotNumber > 0 ? $"\n> SHOT: {pinnedShotNumber}" : "";
            headerText.text = $"> FROM: {charName}{seeking}{shotLine}\n> STATUS: {status}";
        }
        else
        {
            string cat = (pendingCategory ?? "unknown").ToUpper();
            headerText.text = $"> CATEGORY: {cat}\n> STATUS: {status}";
        }
    }

    static string FirstName(string fullName)
    {
        if (string.IsNullOrEmpty(fullName)) return "";
        int sp = fullName.IndexOf(' ');
        return sp > 0 ? fullName.Substring(0, sp) : fullName;
    }

    void OnStoryShellReady(string storyId, string character, string artifact, string premise)
    {
        if (!frameRoot.activeSelf) return;
        if (string.IsNullOrEmpty(pinnedStoryId)) return;
        if (storyId != pinnedStoryId) return;
        if (shotDelivered) return;

        pinnedCharacter = character;
        pinnedArtifact = artifact;
        // Premise is LLM-only context — never shown to the user.
        RefreshHeader();
    }

    void OnShotProgress(string storyId, int shotNumber, string stage, bool hasImage, bool hasAudio)
    {
        if (!frameRoot.activeSelf) return;
        if (string.IsNullOrEmpty(pinnedStoryId) || storyId != pinnedStoryId) return;
        if (shotDelivered) return;

        pinnedShotNumber = shotNumber;
        pinnedShotStage = stage;
        RefreshHeader();
    }

    void OnTransmissionReady(TransmissionData data)
    {
        if (!frameRoot.activeSelf) return;
        // Ignore shots from other stories — stay pinned to what the user entered.
        if (!string.IsNullOrEmpty(pinnedStoryId) && data.storyId != pinnedStoryId) return;

        // Re-fire for the same shot means the video URL just landed after the
        // still was shown. Apply the URL updates only; don't rerun typewriter
        // or touch the header.
        bool isRefire = shotDelivered && data.shotNumber == pinnedShotNumber;
        if (isRefire)
        {
            if (!string.IsNullOrEmpty(data.videoUrl) && data.videoUrl != currentVideoUrl)
            {
                currentVideoUrl = data.videoUrl;
                if (videoPlayer != null)
                {
                    videoPlayer.Stop();
                    videoPlayer.url = data.videoUrl;
                    videoPlayer.Prepare();
                }
            }
            if (!string.IsNullOrEmpty(data.audioUrl) && data.audioUrl != currentAudioUrl)
            {
                currentAudioUrl = data.audioUrl;
                if (musicLoadRoutine != null) StopCoroutine(musicLoadRoutine);
                musicLoadRoutine = StartCoroutine(LoadMusicRoutine(data.audioUrl));
            }
            return;
        }

        shotDelivered = true;
        pinnedShotNumber = data.shotNumber;
        if (typewriterRoutine != null) StopCoroutine(typewriterRoutine);
        UseDefaultBodyLayout();
        UseDefaultAcceptLayout();

        bool isTransmitter = string.Equals(data.transmissionType, "transmitter", System.StringComparison.OrdinalIgnoreCase);
        bool isLocation = string.Equals(data.transmissionType, "location", System.StringComparison.OrdinalIgnoreCase);

        // New rule: transmitter screen stays minimal; artifacts are the only thing you "accept".
        if (isTransmitter)
        {
            locationHeader.text = "TRANSMITTER";
            headerText.text = "";
            statusText.text = "";
            sendTransmissionMode = true;
            dismissOnlyMode = false;
            acceptRequired = false;
            acceptArtifact = null;
            acceptStoryId = null;
            if (acceptBtnGO != null) acceptBtnGO.SetActive(true);
            if (acceptBtnLabel != null) acceptBtnLabel.text = "[TRANSMIT]";
            if (closeBtn != null) closeBtn.gameObject.SetActive(true);
        }
        else
        {
            if (isLocation)
            {
                sendTransmissionMode = false;
                dismissOnlyMode = true;
                acceptRequired = false;
                acceptArtifact = null;
                acceptStoryId = null;
                if (acceptBtnGO != null) acceptBtnGO.SetActive(true);
                if (acceptBtnLabel != null) acceptBtnLabel.text = "[DONE]";
                if (closeBtn != null) closeBtn.gameObject.SetActive(true);
                statusText.text = "";
            }
            else
            {
            // Artifact: accept artifacts only.
            sendTransmissionMode = false;
            dismissOnlyMode = false;
            acceptRequired = true;
            acceptArtifact = !string.IsNullOrEmpty(data.specialItem) ? data.specialItem : pinnedArtifact;
            acceptStoryId = data.storyId;
            if (acceptBtnGO != null) acceptBtnGO.SetActive(true);
            if (acceptBtnLabel != null)
            {
                string a = string.IsNullOrWhiteSpace(acceptArtifact) ? "ARTIFACT" : acceptArtifact.Trim();
                acceptBtnLabel.text = $"[ACCEPT {a.ToUpper()}]";
            }
            if (closeBtn != null) closeBtn.gameObject.SetActive(true);
            statusText.text = "";
            }
        }

        // Load the image asynchronously
        if (!string.IsNullOrEmpty(data.imageUrl) && data.imageUrl != currentImageUrl)
        {
            currentImageUrl = data.imageUrl;
            if (imageLoadRoutine != null) StopCoroutine(imageLoadRoutine);
            imageLoadRoutine = StartCoroutine(LoadImageRoutine(data.imageUrl));
        }

        // Kick off video playback once a url lands — fades over the still image
        if (!string.IsNullOrEmpty(data.videoUrl) && data.videoUrl != currentVideoUrl)
        {
            currentVideoUrl = data.videoUrl;
            if (videoPlayer != null)
            {
                videoPlayer.Stop();
                videoPlayer.url = data.videoUrl;
                videoPlayer.Prepare();
            }
        }

        // Stream the ACE-Step music url and play it on loop.
        if (!string.IsNullOrEmpty(data.audioUrl) && data.audioUrl != currentAudioUrl)
        {
            currentAudioUrl = data.audioUrl;
            if (musicLoadRoutine != null) StopCoroutine(musicLoadRoutine);
            musicLoadRoutine = StartCoroutine(LoadMusicRoutine(data.audioUrl));
        }

        // Dialog body intentionally blank — the visual is the video, the
        // narrative is the song generated from the dialog as lyrics.
        bodyText.text = "";
    }

    void OnAcceptTapped()
    {
        if (sendTransmissionMode)
        {
            Debug.Log("[TransmissionFrame] Send Transmission tapped");
            Hide();
            return;
        }

        if (dismissOnlyMode)
        {
            Hide();
            return;
        }

        if (acceptRequired)
        {
            var tm = TransmissionManager.Instance;
            if (tm != null && !string.IsNullOrEmpty(acceptArtifact))
            {
                tm.AcceptItem(acceptArtifact, acceptStoryId, pinnedShotNumber);
            }
        }
        Hide();
    }

    IEnumerator LoadMusicRoutine(string url)
    {
        if (musicSource == null) yield break;
        // .mp3 from CDN — use AudioType.MPEG.
        using (var req = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.MPEG))
        {
            // DownloadHandlerAudioClip default is "stream once decoded"; that's fine here.
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[TransmissionFrame] Music load failed ({url}): {req.error}");
                yield break;
            }
            var clip = DownloadHandlerAudioClip.GetContent(req);
            if (clip == null)
            {
                Debug.LogWarning($"[TransmissionFrame] Music decode failed for {url}");
                yield break;
            }
            // Skip if a newer URL came in while we were loading.
            if (currentAudioUrl != url) yield break;
            musicSource.Stop();
            musicSource.clip = clip;
            musicSource.Play();
            Debug.Log($"[TransmissionFrame] Music playing: {url}");
        }
        musicLoadRoutine = null;
    }

    void OnVideoPrepared(VideoPlayer vp)
    {
        if (fxScheduler != null)
        {
            float len = vp.frameCount > 0 && vp.frameRate > 0.01f ? (float)(vp.frameCount / vp.frameRate) : 7.5f;
            fxScheduler.RollSchedule(len);
        }
        vp.Play();
        if (videoImage != null) videoImage.color = Color.white;
    }

    IEnumerator LoadImageRoutine(string url)
    {
        using (UnityWebRequest req = UnityWebRequestTexture.GetTexture(url))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[TransmissionFrame] Image load failed: {req.error} ({url})");
                yield break;
            }
            var tex = DownloadHandlerTexture.GetContent(req);
            if (heroImage != null && tex != null)
            {
                heroImage.texture = tex;
                heroImage.color = Color.white;
            }
        }
        imageLoadRoutine = null;
    }

    IEnumerator TypewriterReveal(string fullText)
    {
        bodyText.text = fullText;
        bodyText.maxVisibleCharacters = 0;
        bodyText.ForceMeshUpdate();

        int total = bodyText.textInfo.characterCount;
        float visible = 0f;
        float charsPerSecond = 60f;

        while (visible < total)
        {
            visible += charsPerSecond * Time.unscaledDeltaTime;
            bodyText.maxVisibleCharacters = Mathf.Min(total, Mathf.FloorToInt(visible));
            yield return null;
        }

        bodyText.maxVisibleCharacters = int.MaxValue;
        typewriterRoutine = null;
    }

    IEnumerator LoadingBlink()
    {
        string[] spin = { "|", "/", "-", "\\" };
        int i = 0;
        while (true)
        {
            if (statusText != null)
                statusText.text = $"Generating transmission {spin[i % spin.Length]}";
            bodyText.text = "";
            i++;
            yield return new WaitForSecondsRealtime(0.12f);
        }
    }

    IEnumerator FadeIn()
    {
        canvasGroup.alpha = 0f;
        float t = 0f;
        while (t < 0.3f)
        {
            t += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Clamp01(t / 0.3f);
            yield return null;
        }
        canvasGroup.alpha = 1f;
    }

    public void Hide()
    {
        if (typewriterRoutine != null)
        {
            StopCoroutine(typewriterRoutine);
            typewriterRoutine = null;
        }
        if (imageLoadRoutine != null)
        {
            StopCoroutine(imageLoadRoutine);
            imageLoadRoutine = null;
        }
        if (videoPlayer != null && videoPlayer.isPlaying) videoPlayer.Stop();
        if (fxScheduler != null) fxScheduler.Stop();
        currentVideoUrl = null;
        if (musicLoadRoutine != null) { StopCoroutine(musicLoadRoutine); musicLoadRoutine = null; }
        if (musicSource != null && musicSource.isPlaying) musicSource.Stop();
        currentAudioUrl = null;
        StartCoroutine(FadeOutAndClose());
    }

    IEnumerator FadeOutAndClose()
    {
        float t = 0f;
        while (t < 0.2f)
        {
            t += Time.unscaledDeltaTime;
            canvasGroup.alpha = 1f - Mathf.Clamp01(t / 0.2f);
            yield return null;
        }
        canvasGroup.alpha = 0f;
        frameRoot.SetActive(false);
        if (contrastActive)
        {
            K1L0ModalContrastMode.End(this);
            K1L0ModalHudMode.End();
            contrastActive = false;
        }

        // Restore HUD now that the transmission slide is dismissed.
        var directorRestore = SignalDirectorV2.Instance;
        if (directorRestore != null) directorRestore.SuppressHUD(false);

        // Transition signal to Resolved when frame is closed
        var director = SignalDirectorV2.Instance;
        if (director != null)
        {
            var loc = director.GetLocationTransmission();
            if (loc != null && loc.state == SignalState.Interpreting)
            {
                director.TransitionTo(loc, SignalState.Resolved);
            }
        }
    }
}
