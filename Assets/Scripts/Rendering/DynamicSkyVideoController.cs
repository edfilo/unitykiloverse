using System;
using System.IO;
using KiloWorld.Rendering.Systems;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Video;

public sealed class DynamicSkyVideoController : MonoBehaviour
{
    private const int TextureWidth = 1080;
    private const int TextureHeight = 1920;

    [SerializeField, Range(0.01f, 2f)] private float playbackSpeed = 0.1f;

    public static DynamicSkyVideoController Instance { get; private set; }
    public static bool IsActive => Instance != null && Instance.enabled && Instance.videoMaterial != null;

    private VideoPlayer videoPlayer;
    private RenderTexture renderTexture;
    private Material videoMaterial;
    private Transform skyPlane;
    private MeshRenderer skyPlaneRenderer;
    private string activeClipName;
    private float nextSelectionTime;
    private bool playingBackward;
    private int observedManualSkyRevision = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<DynamicSkyVideoController>() != null) return;

        var host = new GameObject("K1L0 Dynamic Sky Video");
        DontDestroyOnLoad(host);
        host.AddComponent<DynamicSkyVideoController>();
    }

    private void Awake()
    {
        Instance = this;

        renderTexture = new RenderTexture(TextureWidth, TextureHeight, 0, RenderTextureFormat.ARGB32)
        {
            name = "K1L0 Weather Sky Video",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            useMipMap = false
        };
        renderTexture.Create();

        videoPlayer = gameObject.AddComponent<VideoPlayer>();
        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = false;
        videoPlayer.playbackSpeed = playbackSpeed;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = renderTexture;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
        videoPlayer.skipOnDrop = true;
        videoPlayer.waitForFirstFrame = false;
        videoPlayer.source = VideoSource.Url;

        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Texture");
        if (shader == null)
        {
            Debug.LogWarning("[DynamicSkyVideo] No unlit shader found for sky video plane.");
            enabled = false;
            return;
        }

        videoMaterial = new Material(shader) { name = "K1L0 Video Weather Sky Plane" };
        videoMaterial.renderQueue = 1000;
        SetVideoTexture(videoMaterial, renderTexture);
        EnsureSkyPlane();
        ApplyVideoSurface(forceGi: true);
        SelectAndPlay(force: true);
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextSelectionTime)
        {
            nextSelectionTime = Time.unscaledTime + 8f;
            SelectAndPlay(force: false);
        }

        if (observedManualSkyRevision != RenderManager.ManualSkyRevision)
        {
            observedManualSkyRevision = RenderManager.ManualSkyRevision;
            SelectAndPlay(force: false);
        }

        UpdatePingPongPlayback();
    }

    private void LateUpdate()
    {
        ApplyVideoSurface(forceGi: false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (videoPlayer != null) videoPlayer.loopPointReached -= HandleLoopPointReached;
        if (videoPlayer != null) videoPlayer.Stop();
        if (renderTexture != null) renderTexture.Release();
        if (skyPlane != null) Destroy(skyPlane.gameObject);
        if (videoMaterial != null) Destroy(videoMaterial);
    }

    private void SelectAndPlay(bool force)
    {
        string clipName = ChooseClipName();
        if (!force && string.Equals(activeClipName, clipName, StringComparison.OrdinalIgnoreCase)) return;

        string fullPath = Path.Combine(Application.streamingAssetsPath, "WeatherVideos", clipName);
        if (!File.Exists(fullPath))
        {
            Debug.LogWarning($"[DynamicSkyVideo] Missing sky video: {fullPath}");
            return;
        }

        activeClipName = clipName;
        playingBackward = false;
        videoPlayer.prepareCompleted -= HandlePrepared;
        videoPlayer.prepareCompleted += HandlePrepared;
        videoPlayer.errorReceived -= HandleVideoError;
        videoPlayer.errorReceived += HandleVideoError;
        videoPlayer.loopPointReached -= HandleLoopPointReached;
        videoPlayer.loopPointReached += HandleLoopPointReached;
        videoPlayer.playbackSpeed = playbackSpeed;
        videoPlayer.url = ToVideoUrl(fullPath);
        videoPlayer.Prepare();
        Debug.Log($"[DynamicSkyVideo] Preparing {clipName} at {videoPlayer.url}");
    }

    private void HandlePrepared(VideoPlayer player)
    {
        playingBackward = false;
        player.playbackSpeed = playbackSpeed;
        player.time = 0.0;
        player.Play();
        ApplyVideoSurface(forceGi: true);
        Debug.Log($"[DynamicSkyVideo] Playing {activeClipName} ping-pong speed={playbackSpeed:0.00}");
    }

    private void HandleLoopPointReached(VideoPlayer player)
    {
        if (player.length <= 0.0) return;

        playingBackward = true;
        player.playbackSpeed = 0.0f;
        player.Pause();
        player.time = Math.Max(0.0, player.length - 0.05);
    }

    private void UpdatePingPongPlayback()
    {
        if (!playingBackward || videoPlayer == null || !videoPlayer.isPrepared) return;

        double nextTime = videoPlayer.time - Time.unscaledDeltaTime * Math.Max(0.01f, playbackSpeed);
        if (nextTime <= 0.02)
        {
            playingBackward = false;
            videoPlayer.time = 0.0;
            videoPlayer.playbackSpeed = playbackSpeed;
            videoPlayer.Play();
            return;
        }

        videoPlayer.time = nextTime;
    }

    private void HandleVideoError(VideoPlayer player, string message)
    {
        Debug.LogWarning($"[DynamicSkyVideo] Video error for {activeClipName}: {message}");
    }

    private string ChooseClipName()
    {
        string glyph = GPSLocationController.GPSDisabled
            ? RenderManager.ManualWeatherGlyph
            : RenderManager.WeatherGlyph;
        glyph = (glyph ?? string.Empty).ToLowerInvariant();

        bool night = IsNight();
        if (glyph.Contains("thunder") || glyph.Contains("storm")) return "thunder.mp4";
        if (glyph.Contains("rain") || glyph.Contains("drizzle") || glyph.Contains("shower"))
            return night ? "raining-night.mp4" : "raining-day.mp4";
        if (glyph.Contains("cloud") || glyph.Contains("fog") || glyph.Contains("haze") || glyph.Contains("overcast"))
            return night ? "cloud-night-1.mp4" : "cloud-day-1.mp4";
        return night ? "clear-night.mp4" : "clear-day.mp4";
    }

    private static bool IsNight()
    {
        if (GPSLocationController.GPSDisabled)
        {
            float hour = Mathf.Repeat(RenderManager.ManualHour, 24f);
            return hour < 6f || hour >= 19f;
        }

        int localHour = DateTime.Now.Hour;
        return localHour < 6 || localHour >= 19;
    }

    private void ApplyVideoSurface(bool forceGi)
    {
        EnsureSkyPlane();
        if (skyPlane == null || videoMaterial == null) return;

        if (RenderSettings.ambientMode != UnityEngine.Rendering.AmbientMode.Skybox)
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
        RenderSettings.ambientIntensity = 0.75f;

        if (forceGi) DynamicGI.UpdateEnvironment();
    }

    public static void ForceApply()
    {
        Instance?.ApplyVideoSurface(forceGi: false);
    }

    private static string ToVideoUrl(string fullPath)
    {
        if (fullPath.StartsWith("file://", StringComparison.OrdinalIgnoreCase) ||
            fullPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            fullPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return fullPath;
        return "file://" + fullPath;
    }

    private void EnsureSkyPlane()
    {
        Camera cam = Camera.main;
        if (cam == null || videoMaterial == null) return;

        if (skyPlane == null)
        {
            var plane = GameObject.CreatePrimitive(PrimitiveType.Quad);
            plane.name = "K1L0 Video Sky Plane";
            var collider = plane.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            skyPlane = plane.transform;
            skyPlaneRenderer = plane.GetComponent<MeshRenderer>();
            skyPlaneRenderer.sharedMaterial = videoMaterial;
            skyPlaneRenderer.shadowCastingMode = ShadowCastingMode.Off;
            skyPlaneRenderer.receiveShadows = false;
            Debug.Log("[DynamicSkyVideo] Created horizon-anchored video sky plane.");
        }

        float distance = Mathf.Max(30f, Mathf.Min(cam.farClipPlane * 0.82f, 900f));
        float viewHeight = 2f * distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float height = viewHeight * 1.9f;
        float width = height * cam.aspect * 1.35f;
        Quaternion yawOnly = Quaternion.Euler(0f, cam.transform.eulerAngles.y, 0f);
        Vector3 forward = yawOnly * Vector3.forward;
        float horizonBottomY = cam.transform.position.y - 1.5f;

        skyPlane.SetParent(null, true);
        skyPlane.position = cam.transform.position + forward * distance + Vector3.up * (horizonBottomY - cam.transform.position.y + height * 0.5f);
        skyPlane.rotation = yawOnly;
        skyPlane.localScale = new Vector3(width, height, 1f);
        skyPlane.gameObject.SetActive(true);
    }

    private static void SetVideoTexture(Material material, Texture texture)
    {
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
    }
}
