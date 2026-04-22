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
    private Button closeBtn;
    private CanvasGroup canvasGroup;
    private Coroutine typewriterRoutine;
    private Coroutine imageLoadRoutine;
    private string pendingLocationName;
    private string pendingCategory;
    private string currentImageUrl;
    private string currentVideoUrl;
    private string pinnedStoryId;           // the storyId this frame is showing
    private bool shotDelivered;             // true once OnTransmissionReady has been handled
    private string pinnedCharacter;
    private string pinnedArtifact;
    private string pinnedShotStage;         // last status from OnShotProgress
    private int pinnedShotNumber;

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
        CreateUI();

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
        frameRoot = new GameObject("TransmissionFrame");
        frameRoot.transform.SetParent(K1L0CanvasRoot.Modal, false);

        // Override sorting to be on top of everything
        var overrideCanvas = frameRoot.AddComponent<Canvas>();
        overrideCanvas.overrideSorting = true;
        overrideCanvas.sortingOrder = 10500;
        frameRoot.AddComponent<GraphicRaycaster>();

        var rootRt = frameRoot.GetComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        canvasGroup = frameRoot.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;

        // Black background (letterbox around the portrait card)
        var bg = frameRoot.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.96f);
        bg.raycastTarget = true;

        var font = Resources.Load<TMP_FontAsset>("Fonts/IBMPlexMono-Regular SDF");
        if (font == null) font = TMP_Settings.defaultFontAsset;

        // ── Portrait story card (9:16), centered, fits parent ──────────────
        var cardGO = new GameObject("StoryCard");
        cardGO.transform.SetParent(frameRoot.transform, false);
        var cardRt = cardGO.AddComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.sizeDelta = new Vector2(900f, 1600f); // will be scaled by fitter
        var cardFitter = cardGO.AddComponent<AspectRatioFitter>();
        cardFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        cardFitter.aspectRatio = 9f / 16f;

        // Card background (dark, so the image frame is visible even before load)
        var cardBg = cardGO.AddComponent<Image>();
        cardBg.color = new Color(0.02f, 0.04f, 0.02f, 1f);
        cardBg.raycastTarget = false;

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

        // Top gradient scrim for legibility of the header/meta overlay
        var topScrimGO = new GameObject("TopScrim");
        topScrimGO.transform.SetParent(cardGO.transform, false);
        var topScrimRt = topScrimGO.AddComponent<RectTransform>();
        topScrimRt.anchorMin = new Vector2(0f, 0.72f);
        topScrimRt.anchorMax = new Vector2(1f, 1f);
        topScrimRt.offsetMin = Vector2.zero;
        topScrimRt.offsetMax = Vector2.zero;
        var topScrim = topScrimGO.AddComponent<Image>();
        topScrim.color = new Color(0f, 0f, 0f, 0.55f);
        topScrim.raycastTarget = false;

        // Bottom scrim for dialog legibility
        var botScrimGO = new GameObject("BottomScrim");
        botScrimGO.transform.SetParent(cardGO.transform, false);
        var botScrimRt = botScrimGO.AddComponent<RectTransform>();
        botScrimRt.anchorMin = new Vector2(0f, 0f);
        botScrimRt.anchorMax = new Vector2(1f, 0.30f);
        botScrimRt.offsetMin = Vector2.zero;
        botScrimRt.offsetMax = Vector2.zero;
        var botScrim = botScrimGO.AddComponent<Image>();
        botScrim.color = new Color(0f, 0f, 0f, 0.65f);
        botScrim.raycastTarget = false;

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

        // Close button — top right, floating on letterbox edge of the card
        var closeBtnGO = new GameObject("CloseBtn");
        closeBtnGO.transform.SetParent(cardGO.transform, false);
        var closeBtnRt = closeBtnGO.AddComponent<RectTransform>();
        closeBtnRt.anchorMin = new Vector2(1f, 1f);
        closeBtnRt.anchorMax = new Vector2(1f, 1f);
        closeBtnRt.pivot = new Vector2(1f, 1f);
        closeBtnRt.anchoredPosition = new Vector2(-12f, -12f);
        closeBtnRt.sizeDelta = new Vector2(44f, 44f);

        var closeBg = closeBtnGO.AddComponent<Image>();
        closeBg.color = new Color(0f, 0f, 0f, 0f); // Invisible hit area
        closeBg.raycastTarget = true;

        closeBtn = closeBtnGO.AddComponent<Button>();
        closeBtn.targetGraphic = closeBg;
        closeBtn.transition = Selectable.Transition.None;
        closeBtn.onClick.AddListener(Hide);

        var xGO = new GameObject("X");
        xGO.transform.SetParent(closeBtnGO.transform, false);
        var xRt = xGO.AddComponent<RectTransform>();
        xRt.anchorMin = Vector2.zero;
        xRt.anchorMax = Vector2.one;
        xRt.offsetMin = Vector2.zero;
        xRt.offsetMax = Vector2.zero;
        var xTmp = xGO.AddComponent<TextMeshProUGUI>();
        xTmp.font = font;
        xTmp.fontSize = 28;
        xTmp.text = "×";
        xTmp.color = TerminalGreen;
        xTmp.alignment = TextAlignmentOptions.Center;
        xTmp.raycastTarget = false;
    }

    /// <summary>Show frame immediately with loading state when proximity is met.</summary>
    public void ShowLoading(string locationName, string category, string storyId = null)
    {
        pendingLocationName = locationName;
        pendingCategory = category;
        pinnedStoryId = storyId;
        shotDelivered = false;
        pinnedCharacter = null;
        pinnedArtifact = null;
        pinnedShotStage = null;
        pinnedShotNumber = 0;
        frameRoot.SetActive(true);

        locationHeader.text = (locationName ?? "UNKNOWN LOCATION").ToUpper();
        bodyText.text = "";
        statusText.text = "> decoding transmission — stand by_";

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
                if (!string.IsNullOrEmpty(shell.premise))
                {
                    if (typewriterRoutine != null) StopCoroutine(typewriterRoutine);
                    typewriterRoutine = null;
                    bodyText.text = shell.premise;
                }
            }
        }

        RefreshHeader();
        StartCoroutine(FadeIn());
    }

    void RefreshHeader()
    {
        if (shotDelivered) return;

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
            string charName = pinnedCharacter.ToUpper();
            string seeking = !string.IsNullOrEmpty(pinnedArtifact) ? $"\n> SEEKING: {pinnedArtifact}" : "";
            string shotLine = pinnedShotNumber > 0 ? $"\n> SHOT: {pinnedShotNumber}" : "";
            headerText.text = $"> SOURCE: {charName}{seeking}{shotLine}\n> STATUS: {status}";
        }
        else
        {
            string cat = (pendingCategory ?? "unknown").ToUpper();
            headerText.text = $"> CATEGORY: {cat}\n> STATUS: {status}";
        }
    }

    void OnStoryShellReady(string storyId, string character, string artifact, string premise)
    {
        if (!frameRoot.activeSelf) return;
        if (string.IsNullOrEmpty(pinnedStoryId)) return;
        if (storyId != pinnedStoryId) return;
        if (shotDelivered) return;

        pinnedCharacter = character;
        pinnedArtifact = artifact;
        if (!string.IsNullOrEmpty(premise))
        {
            // Stop the decoding animation and show the premise statically.
            if (typewriterRoutine != null) StopCoroutine(typewriterRoutine);
            typewriterRoutine = null;
            bodyText.text = premise;
        }
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

        shotDelivered = true;
        if (typewriterRoutine != null) StopCoroutine(typewriterRoutine);

        string charName = !string.IsNullOrEmpty(data.character) ? data.character.ToUpper() : "UNKNOWN";
        // Keep the header pinned to the signal the user actually tapped.
        // TransmissionData.locationName can leak an unrelated LocationTransmission
        // (the nearest active POI) into pursuit-beam transmissions.
        if (string.IsNullOrEmpty(pendingLocationName) && !string.IsNullOrEmpty(data.locationName))
            locationHeader.text = data.locationName.ToUpper();
        headerText.text = $"> SOURCE: {charName}\n> SHOT: {data.shotNumber}";
        statusText.text = $"> transmission decoded — tap × to dismiss";

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

        // Typewriter reveal the dialog
        typewriterRoutine = StartCoroutine(TypewriterReveal(data.dialog));
    }

    void OnVideoPrepared(VideoPlayer vp)
    {
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
        string[] frames = { "decoding .", "decoding ..", "decoding ...", "decoding" };
        int i = 0;
        while (true)
        {
            bodyText.text = $"\n\n    {frames[i % frames.Length]}";
            i++;
            yield return new WaitForSecondsRealtime(0.4f);
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
        currentVideoUrl = null;
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
