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
            // Repeat horizontally so the cylindrical band's seam at u=0/u=1
            // tiles cleanly. Vertical stays clamped (band has finite height).
            wrapModeU = TextureWrapMode.Repeat,
            wrapModeV = TextureWrapMode.Clamp,
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
        // Render both sides of the cylinder triangles — guarantees visibility
        // regardless of how winding ends up resolving on URP Unlit. Disable
        // ZWrite so the band sits behind the world (terrain/buildings paint on
        // top). URP Unlit exposes _Cull as float (0=Off, 1=Front, 2=Back) and
        // _ZWrite (0=Off, 1=On). Falls through silently on unsupported keys.
        if (videoMaterial.HasProperty("_Cull")) videoMaterial.SetFloat("_Cull", 0f);
        if (videoMaterial.HasProperty("_ZWrite")) videoMaterial.SetFloat("_ZWrite", 0f);
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

    // ── Horizon-anchored cylindrical sky band ──────────────────────────────
    // Camera sits inside a procedural cylinder whose side band is mapped with
    // the video RenderTexture. The cylinder NEVER rotates with the camera —
    // it only follows the player's XZ so it stays centered. As the player
    // turns, they sweep across different angular slices of the band → the
    // video reads as world-space sky instead of a glued billboard.
    private const int SkyCylinderSegments = 48;
    private const float SkyCylinderRadius = 600f;
    private const float SkyCylinderHeight = 480f;
    // How far below the camera the band's bottom sits. Negative drops it
    // below eye-line so the bottom edge lines up roughly with the horizon
    // (the rest of the band fills the sky above).
    private const float SkyCylinderHorizonOffset = -40f;
    // How many times the video repeats around the cylinder. 1 = one wrap
    // (full unique content per direction — turning the camera reveals new
    // sky). 2+ = tiled which hides the seam but also makes 180° turns look
    // identical, which reads as "the sky is following me" even though it
    // isn't. Keep at 1.0 unless the seam is too obvious for your content.
    private const float SkyCylinderTileCount = 1f;

    private void EnsureSkyPlane()
    {
        Camera cam = Camera.main;
        if (cam == null || videoMaterial == null) return;

        if (skyPlane == null)
        {
            var dome = new GameObject("K1L0 Video Sky Cylinder");
            var mf = dome.AddComponent<MeshFilter>();
            mf.sharedMesh = BuildInsideOutCylinderBand(
                SkyCylinderSegments, SkyCylinderRadius, SkyCylinderHeight, SkyCylinderTileCount);
            skyPlaneRenderer = dome.AddComponent<MeshRenderer>();
            skyPlaneRenderer.sharedMaterial = videoMaterial;
            skyPlaneRenderer.shadowCastingMode = ShadowCastingMode.Off;
            skyPlaneRenderer.receiveShadows = false;
            skyPlane = dome.transform;
            Debug.Log($"[DynamicSkyVideo] Built horizon-anchored cylinder: r={SkyCylinderRadius} h={SkyCylinderHeight} segs={SkyCylinderSegments} tiles={SkyCylinderTileCount}");
        }

        // World-anchored — follow player horizontally, sit at horizon, never
        // inherit yaw. The video stays put in world space as the camera turns.
        Vector3 camPos = cam.transform.position;
        skyPlane.SetParent(null, true);
        skyPlane.position = new Vector3(camPos.x, camPos.y + SkyCylinderHorizonOffset, camPos.z);
        skyPlane.rotation = Quaternion.identity;
        skyPlane.localScale = Vector3.one;
        skyPlane.gameObject.SetActive(true);

        // One-shot sanity log so we can verify on-device that the cylinder is
        // tracking position but NOT rotation. If yaw ever leaks in, world rot
        // would diverge from identity and we'd see it here.
        if (!loggedAnchorOnce)
        {
            loggedAnchorOnce = true;
            Debug.Log($"[DynamicSkyVideo] Anchor sanity: camYaw={cam.transform.eulerAngles.y:F1}° skyRot={skyPlane.eulerAngles.y:F1}° skyPos={skyPlane.position} camPos={camPos}");
        }
    }

    private bool loggedAnchorOnce;

    // Procedural cylinder side-band, triangles wound to face INWARD so the
    // camera sees the video on the inside surface. UVs wrap the texture
    // `tileCount` times around the circumference, full-height vertically.
    private static Mesh BuildInsideOutCylinderBand(int segments, float radius, float height, float tileCount)
    {
        var mesh = new Mesh { name = "K1L0 Sky Cylinder Band" };
        int ringVerts = segments + 1; // +1 so the seam has matching UV at u=tileCount
        var verts = new Vector3[ringVerts * 2];
        var uvs = new Vector2[verts.Length];
        var tris = new int[segments * 6];

        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            float a = t * Mathf.PI * 2f;
            float x = Mathf.Cos(a) * radius;
            float z = Mathf.Sin(a) * radius;
            int bottom = i * 2;
            int top = i * 2 + 1;
            verts[bottom] = new Vector3(x, 0f, z);
            verts[top]    = new Vector3(x, height, z);
            uvs[bottom]   = new Vector2(t * tileCount, 0f);
            uvs[top]      = new Vector2(t * tileCount, 1f);
        }

        // Inward-facing winding (reverse of standard cylinder so the inside
        // surface renders to the camera that lives at the centre).
        for (int i = 0; i < segments; i++)
        {
            int v0 = i * 2;
            int v1 = i * 2 + 1;
            int v2 = (i + 1) * 2;
            int v3 = (i + 1) * 2 + 1;
            int o = i * 6;
            tris[o + 0] = v0;
            tris[o + 1] = v2;
            tris[o + 2] = v1;
            tris[o + 3] = v1;
            tris[o + 4] = v2;
            tris[o + 5] = v3;
        }

        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        // Big bounds so frustum culling never accidentally hides the band
        // (the procedural triangles' computed bounds are correct, but a
        // large explicit bound is bulletproof for camera-anchored geometry).
        mesh.bounds = new Bounds(new Vector3(0f, height * 0.5f, 0f), Vector3.one * radius * 4f);
        return mesh;
    }

    private static void SetVideoTexture(Material material, Texture texture)
    {
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
    }
}
