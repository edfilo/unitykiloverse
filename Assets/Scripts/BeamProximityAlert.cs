using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using System.Collections;

/// <summary>
/// Displays alerts when player gets within proximity range of light beams
/// </summary>
public class BeamProximityAlert : MonoBehaviour
{
    [Header("UI Settings")]
    [Tooltip("Duration to show alert in seconds")]
    public float alertDuration = 3f;
    // Removed duplicate fontSize tooltip
    [Tooltip("Font size for alert text")]
    public int fontSize = 36;

    [Tooltip("Alert text color")]
    public Color alertColor = new Color(1f, 0.8f, 0.3f, 1f); // Golden glow

    [Tooltip("Background color")]
    public Color backgroundColor = new Color(0f, 0f, 0f, 0.7f);

    [Header("Video Settings")]
    [Tooltip("If true, plays the video when a beam is collected. If false, just shows the alert.")]
    public bool playVideoOnTrigger = true;

    public string videoUrl = "https://cdn.kilo.gallery/moodboard/everything.mp4";
    public Shader pixelationShader;
    
    [Range(0.001f, 0.2f)]
    public float pixelSize = 0.02f;

    private VirtualGridSpawner spawner;
    private GameObject alertPanel;
    private Text alertText;
    private Coroutine hideCoroutine;

    // Video Components
    private GameObject videoCanvas;
    private VideoPlayer videoPlayer;
    private RawImage videoDisplay;
    private RenderTexture renderTexture;
    private bool isPlaying = false;

    void Start()
    {
        // Find VirtualGridSpawner
        spawner = FindFirstObjectByType<VirtualGridSpawner>();
        if (spawner == null)
        {
            Debug.LogError("[BeamProximityAlert] VirtualGridSpawner not found!");
            enabled = false;
            return;
        }

        // Subscribe to proximity event
        spawner.OnBeamProximityEntered += HandleBeamProximity;

        // Create UI
        CreateAlertUI();
        CreateVideoUI(); // Pre-create video UI


        Debug.Log("[BeamProximityAlert] Started - listening for beam proximity events");
    }

    void OnDestroy()
    {
        // Unsubscribe from event
        if (spawner != null)
        {
            spawner.OnBeamProximityEntered -= HandleBeamProximity;
        }

        if (renderTexture != null)
            renderTexture.Release();
    }

    void CreateAlertUI()
    {
        // Find main Canvas
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[BeamProximityAlert] No Canvas found in scene!");
            return;
        }

        // Create panel
        alertPanel = new GameObject("BeamProximityAlertPanel");
        alertPanel.transform.SetParent(canvas.transform, false);

        RectTransform panelRect = alertPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.8f); // Top center
        panelRect.anchorMax = new Vector2(0.5f, 0.8f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(400, 100);

        // Background
        Image bgImage = alertPanel.AddComponent<Image>();
        bgImage.color = backgroundColor;

        // Text
        GameObject textObj = new GameObject("AlertText");
        textObj.transform.SetParent(alertPanel.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;

        alertText = textObj.AddComponent<Text>();
        alertText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        alertText.fontSize = fontSize;
        alertText.color = alertColor;
        alertText.alignment = TextAnchor.MiddleCenter;
        alertText.text = "";

        // Start hidden
        alertPanel.SetActive(false);
    }

void HandleBeamProximity(int beamIndex, float distance, string beamName, string beamNoun)
    {
        if (alertPanel == null || alertText == null) return;

        // Show alert with name and archetype
        alertText.text = $"⚡ {beamName.ToUpper()} THE {beamNoun.ToUpper()} ⚡\n{distance:F1}m away";
        alertPanel.SetActive(true);

        // Log to console
        Debug.Log($"[BeamProximityAlert] 🎯 PROXIMITY ALERT! Beam #{beamIndex} '{beamName}' the {beamNoun} at {distance:F1}m");

        // Auto-hide after duration
        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
        }
        hideCoroutine = StartCoroutine(HideAfterDelay());

        // Trigger Video if not already playing
        if (!isPlaying)
        {
            // Disable the beam to prevent re-triggering immediately
            GameObject[] beams = spawner.GetBeams();
            if (beams != null && beamIndex >= 0 && beamIndex < beams.Length)
            {
                if (beams[beamIndex] != null)
                {
                    beams[beamIndex].SetActive(false);
                    Debug.Log($"[BeamProximityAlert] Disabled Beam #{beamIndex}");
                }
            }
            
            if (playVideoOnTrigger)
            {
                PlayVideo();
            }
        }
    }

    void PlayVideo()
    {
        isPlaying = true;
        
        Debug.Log($"[BeamProximityAlert] 🎬 Playing Video: {videoUrl}");

        if (videoCanvas == null) CreateVideoUI();
        
        videoCanvas.SetActive(true);
        videoPlayer.url = videoUrl;
        videoPlayer.Prepare();
        videoPlayer.Play();
    }

    public void CloseVideo()
    {
        if (videoPlayer != null) videoPlayer.Stop();
        if (videoCanvas != null) videoCanvas.SetActive(false);
        isPlaying = false;
        Debug.Log("[BeamProximityAlert] Video closed by user");
    }

    void CreateVideoUI()
    {
        if (videoCanvas != null) return;

        // Try to find shader if not assigned
        if (pixelationShader == null)
            pixelationShader = Shader.Find("Custom/Pixelation");

        // Canvas
        videoCanvas = new GameObject("BeamVideoCanvas");
        DontDestroyOnLoad(videoCanvas); 
        Canvas c = videoCanvas.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 32767; // Max sorting order
        videoCanvas.AddComponent<CanvasScaler>();
        videoCanvas.AddComponent<GraphicRaycaster>();

        // Background (Black)
        GameObject bgObj = new GameObject("VideoBackground");
        bgObj.transform.SetParent(videoCanvas.transform, false);
        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        
        Image bg = bgObj.AddComponent<Image>();
        bg.color = Color.black;

        // RawImage for Video
        GameObject rawImageObj = new GameObject("VideoDisplay");
        rawImageObj.transform.SetParent(videoCanvas.transform, false);
        videoDisplay = rawImageObj.AddComponent<RawImage>();
        
        // Stretch to fill
        RectTransform rt = videoDisplay.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // Video Player
        videoPlayer = videoCanvas.AddComponent<VideoPlayer>();
        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = true; 
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;

        // RenderTexture - high res for crisp video before shader
        renderTexture = new RenderTexture(Screen.width, Screen.height, 0);
        videoPlayer.targetTexture = renderTexture;
        videoDisplay.texture = renderTexture;

        // Apply Pixelation Shader
        if (pixelationShader != null)
        {
            Material mat = new Material(pixelationShader);
            mat.SetFloat("_PixelSize", pixelSize);
            videoDisplay.material = mat;
        }

        // --- Close Button ---
        GameObject btnObj = new GameObject("CloseButton");
        btnObj.transform.SetParent(videoCanvas.transform, false);
        
        RectTransform btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(1, 1); // Top Right
        btnRect.anchorMax = new Vector2(1, 1);
        btnRect.pivot = new Vector2(1, 1);
        btnRect.anchoredPosition = new Vector2(-40, -40);
        btnRect.sizeDelta = new Vector2(160, 60);

        Image btnBg = btnObj.AddComponent<Image>();
        btnBg.color = new Color(0.1f, 0.1f, 0.1f, 0.8f); // Dark semi-transparent

        Button btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(CloseVideo);

        // Button Text
        GameObject btnTextObj = new GameObject("Text");
        btnTextObj.transform.SetParent(btnObj.transform, false);
        RectTransform btnTextRect = btnTextObj.AddComponent<RectTransform>();
        btnTextRect.anchorMin = Vector2.zero;
        btnTextRect.anchorMax = Vector2.one;
        btnTextRect.sizeDelta = Vector2.zero;

        Text txt = btnTextObj.AddComponent<Text>();
        txt.text = "CLOSE";
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.white;
        txt.fontSize = 28;
        
        videoCanvas.SetActive(false);
    }

    IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(alertDuration);

        if (alertPanel != null)
        {
            alertPanel.SetActive(false);
        }
    }
}
