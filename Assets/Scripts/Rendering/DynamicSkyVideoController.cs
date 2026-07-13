using System;
using KiloWorld.Rendering.Systems;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class DynamicSkyVideoController : MonoBehaviour
{
    [Serializable]
    public sealed class EnvironmentSnapshot
    {
        public float solarAltitude, solarAzimuth, cloudOpacity, cloudSpeed,
            cloudScale, cloudContrast, topHue, midHue, horizonHue,
            nightBlackness, rain, aurora;
        public int effect;
        public bool bypassWeather;
    }

    public static void ApplyEnvironmentJson(string json)
    {
        var state = JsonUtility.FromJson<EnvironmentSnapshot>(json);
        if (state == null) return;
        PlayerPrefs.SetFloat("k1lo_nativeSunAltitude", state.solarAltitude);
        PlayerPrefs.SetFloat("k1lo_nativeSunAzimuth", state.solarAzimuth);
        PlayerPrefs.SetFloat("k1lo_layeredBypassWeather", state.bypassWeather ? 1 : 0);
        PlayerPrefs.SetFloat("k1lo_layeredSkyEffect", state.effect);
        PlayerPrefs.SetFloat("k1lo_layeredCloudOpacity", state.cloudOpacity);
        PlayerPrefs.SetFloat("k1lo_layeredCloudSpeed", state.cloudSpeed);
        PlayerPrefs.SetFloat("k1lo_layeredCloudScale", state.cloudScale);
        PlayerPrefs.SetFloat("k1lo_layeredCloudContrast", state.cloudContrast);
        PlayerPrefs.SetFloat("k1lo_layeredSkyTopHue", state.topHue);
        PlayerPrefs.SetFloat("k1lo_layeredSkyMidHue", state.midHue);
        PlayerPrefs.SetFloat("k1lo_layeredSkyHorizonHue", state.horizonHue);
        PlayerPrefs.SetFloat("k1lo_layeredNightBlackness", state.nightBlackness);
        PlayerPrefs.SetFloat("k1lo_layeredRain", state.rain);
        PlayerPrefs.SetFloat("k1lo_layeredAurora", state.aurora);
        Instance?.ApplyExperimentalParameters();
    }
    private const int TextureWidth = 1080;
    private const int TextureHeight = 1920;
    // The old flat sky swept the video rapidly as the map/compass turned and
    // mirrored at each texture edge. Four mirrored cycles per compass turn
    // keeps the pan responsive without racing ahead of the map gesture.
    private const float SkyPanCyclesPerTurn = 4f;

    [SerializeField, Range(0.01f, 2f)] private float playbackSpeed = 0.1f;

    public static DynamicSkyVideoController Instance { get; private set; }
    public static bool IsActive => Instance != null && Instance.enabled && Instance.layeredSkyMaterial != null;

    public static float SkyTargetFps { get; private set; } = 30f;
    public static bool ExperimentalLayeredSky { get; private set; }
    private static bool nativePanelOpen;

    public static void SetNativePanelOpen(bool open)
    {
        nativePanelOpen = open;
    }

    public static void SetSkyFps(float fps)
    {
        SkyTargetFps = Mathf.Clamp(fps, 1f, 60f);
        PlayerPrefs.SetFloat("k1lo_skyTargetFps", SkyTargetFps);
        PlayerPrefs.Save();
    }

    public static void SetExperimentalLayeredSky(bool enabled)
    {
        // The layered Metal sky is now the production sky. Keep this bridge
        // method for older native settings builds, but never revive video.
        ExperimentalLayeredSky = true;
        PlayerPrefs.SetInt("k1lo_experimentalLayeredSky", 1);
        PlayerPrefs.Save();
        Instance?.ApplySkyRenderer();
    }

    public static void SetExperimentalSkyFloat(string key, float value)
    {
        PlayerPrefs.SetFloat("k1lo_" + key, value);
        PlayerPrefs.Save();
        Instance?.ApplyExperimentalParameters();
    }

    private Material layeredSkyMaterial;
    private Transform skyPlane;
    private MeshRenderer skyPlaneRenderer;

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
        SkyTargetFps = PlayerPrefs.GetFloat("k1lo_skyTargetFps", 30f);
        ExperimentalLayeredSky = true;
        PlayerPrefs.SetInt("k1lo_experimentalLayeredSky", 1);

        /* Legacy weather-video layer retained for easy source archaeology.
           It is no longer allocated or played; the layered sky is the default.
        renderTexture = new RenderTexture(TextureWidth, TextureHeight, 0, RenderTextureFormat.ARGB32)
        {
            name = "K1L0 Weather Sky Video",
            // Mirror is the old ping-pong wrap: adjacent horizontal copies meet
            // edge-to-edge instead of jumping from the right edge back to left.
            // Vertical stays clamped so portrait sky videos do not smear.
            wrapModeU = TextureWrapMode.Mirror,
            wrapModeV = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            useMipMap = false
        };
        renderTexture.Create();

        videoPlayer = gameObject.AddComponent<VideoPlayer>();
        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = false;
        videoPlayer.playbackSpeed = EffectivePlaybackSpeed();
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = renderTexture;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
        videoPlayer.skipOnDrop = true;
        videoPlayer.waitForFirstFrame = false;
        videoPlayer.source = VideoSource.Url; */

        /* Legacy video material disabled with the video player above.
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Texture");
        videoMaterial = new Material(shader) { name = "K1L0 Video Weather Sky Plane" };
        videoMaterial.renderQueue = 1000;
        // Render both sides of the quad and keep it behind world geometry.
        // URP Unlit exposes _Cull as float (0=Off, 1=Front, 2=Back) and
        // _ZWrite (0=Off, 1=On). Falls through silently on unsupported keys.
        if (videoMaterial.HasProperty("_Cull")) videoMaterial.SetFloat("_Cull", 0f);
        if (videoMaterial.HasProperty("_ZWrite")) videoMaterial.SetFloat("_ZWrite", 0f);
        SetVideoTexture(videoMaterial, renderTexture); */
        // A Resources reference keeps the experimental shader in device builds;
        // Shader.Find alone can be stripped when no serialized material uses it.
        var layeredShader = Resources.Load<Shader>("K1L0LayeredSky");
        if (layeredShader == null) layeredShader = Shader.Find("K1L0/Experimental Layered Sky");
        if (layeredShader != null)
            layeredSkyMaterial = new Material(layeredShader) { name = "K1L0 Experimental Layered Sky" };
        else
            Debug.LogError("[DynamicSkyVideo] Experimental layered sky shader missing from player build.");
        if (layeredSkyMaterial != null)
        {
            var cloudDensity = Resources.Load<Texture2D>("K1L0CloudDensityNear");
            if (cloudDensity != null)
            {
                cloudDensity.wrapMode = TextureWrapMode.Repeat;
                cloudDensity.filterMode = FilterMode.Trilinear;
                layeredSkyMaterial.SetTexture("_CloudTex", cloudDensity);
            }
            else Debug.LogError("[DynamicSkyVideo] Photoreal cloud density texture missing.");
        }
        EnsureSkyPlane();
        ApplySkyRenderer();
        ApplyVideoSurface(forceGi: true);
        // SelectAndPlay(force: true); // legacy weather-video path disabled
    }

    private void Update()
    {
    }

    private void LateUpdate()
    {
        ApplyVideoSurface(forceGi: false);
        if (ExperimentalLayeredSky) UpdateCelestialParameters();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (skyPlane != null) Destroy(skyPlane.gameObject);
        if (layeredSkyMaterial != null) Destroy(layeredSkyMaterial);
    }

#if K1L0_LEGACY_VIDEO_SKY
    private void SelectAndPlay(bool force)
    {
        if (ExperimentalLayeredSky) return;
        // If Swift sent us an explicit URL, use it directly and skip ChooseClipName().
        string overrideUrl = pendingOverrideUrl;
        if (!string.IsNullOrEmpty(overrideUrl))
        {
            if (!force && string.Equals(activeOverrideUrl, overrideUrl, StringComparison.Ordinal)) return;
            activeOverrideUrl = overrideUrl;
            activeClipName = Path.GetFileName(overrideUrl);
            playingBackward = false;
            videoPlayer.prepareCompleted -= HandlePrepared;
            videoPlayer.prepareCompleted += HandlePrepared;
            videoPlayer.errorReceived -= HandleVideoError;
            videoPlayer.errorReceived += HandleVideoError;
            videoPlayer.loopPointReached -= HandleLoopPointReached;
            videoPlayer.loopPointReached += HandleLoopPointReached;
            videoPlayer.playbackSpeed = EffectivePlaybackSpeed();
            videoPlayer.url = overrideUrl;
            videoPlayer.Prepare();
            Debug.Log($"[DynamicSkyVideo] Swift override → {activeClipName}");
            return;
        }

        string clipName = ChooseClipName();
        if (!force && string.Equals(activeClipName, clipName, StringComparison.OrdinalIgnoreCase)) return;

        string fullPath = Path.Combine(Application.streamingAssetsPath, "WeatherVideos", clipName);
        if (!File.Exists(fullPath))
        {
            Debug.LogWarning($"[DynamicSkyVideo] Missing sky video: {fullPath}");
            return;
        }

        activeOverrideUrl = null;
        activeClipName = clipName;
        playingBackward = false;
        videoPlayer.prepareCompleted -= HandlePrepared;
        videoPlayer.prepareCompleted += HandlePrepared;
        videoPlayer.errorReceived -= HandleVideoError;
        videoPlayer.errorReceived += HandleVideoError;
        videoPlayer.loopPointReached -= HandleLoopPointReached;
        videoPlayer.loopPointReached += HandleLoopPointReached;
        videoPlayer.playbackSpeed = EffectivePlaybackSpeed();
        videoPlayer.url = ToVideoUrl(fullPath);
        videoPlayer.Prepare();
        Debug.Log($"[DynamicSkyVideo] Preparing {clipName} at {videoPlayer.url}");
    }

    private void HandlePrepared(VideoPlayer player)
    {
        if (ExperimentalLayeredSky)
        {
            player.Stop();
            player.enabled = false;
            return;
        }
        playingBackward = false;
        ApplyEffectivePlaybackSpeed();
        player.time = 0.0;
        player.Play();
        ApplyVideoSurface(forceGi: true);
        Debug.Log($"[DynamicSkyVideo] Playing {activeClipName} ping-pong speed={EffectivePlaybackSpeed():0.00} skyFps={SkyTargetFps:0}");
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
        if (videoPlayer == null || !videoPlayer.isPrepared) return;

        // Some iOS VideoPlayer builds stop on the final frame without reliably
        // delivering loopPointReached. Detect that state too so every clip still
        // reverses rather than visibly snapping back to frame zero.
        if (!playingBackward && videoPlayer.length > 0.0 &&
            (videoPlayer.time >= videoPlayer.length - 0.05 ||
             (!videoPlayer.isPlaying && videoPlayer.time >= videoPlayer.length - 0.10)))
        {
            HandleLoopPointReached(videoPlayer);
        }

        if (!playingBackward) return;

        double nextTime = videoPlayer.time - Time.unscaledDeltaTime * EffectivePlaybackSpeed();
        if (nextTime <= 0.02)
        {
            playingBackward = false;
            videoPlayer.time = 0.0;
            ApplyEffectivePlaybackSpeed();
            videoPlayer.Play();
            return;
        }

        videoPlayer.time = nextTime;
    }

    private void HandleVideoError(VideoPlayer player, string message)
    {
        Debug.LogWarning($"[DynamicSkyVideo] Video error for {activeClipName}: {message}");
    }

    private float EffectivePlaybackSpeed()
    {
        float speed = playbackSpeed * Mathf.Clamp(SkyTargetFps, 1f, 60f) / 30f;
        return Mathf.Clamp(speed, 0.01f, 2f);
    }

    private void ApplyEffectivePlaybackSpeed()
    {
        if (videoPlayer == null || playingBackward) return;
        videoPlayer.playbackSpeed = EffectivePlaybackSpeed();
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
        // Signal hierarchy, best first:
        // 1. Manual hour when testing (override) or desktop (no GPS).
        // 2. Backend isDay — real sunrise/sunset for the player's location.
        // 3. Astronomical sun altitude computed on-device from GPS + UTC.
        // 4. Hardcoded 6/19 local clock, the dumbest last resort. (This was
        //    briefly the ONLY path with GPS on, which put the night sky up at
        //    7pm in July while the sun was still shining.)
        if (GPSLocationController.GPSDisabled || RenderManager.TestSkyOverrideEnabled)
        {
            float hour = Mathf.Repeat(RenderManager.ManualHour, 24f);
            return hour < 6f || hour >= 19f;
        }

        if (RenderManager.WeatherIsDay.HasValue)
            return !RenderManager.WeatherIsDay.Value;

        if (RenderManager.Instance != null)
            return RenderManager.LiveSunAltitudeDeg < -1.5f;

        int localHour = DateTime.Now.ToLocalTime().Hour;
        return localHour < 6 || localHour >= 19;
    }
#endif

    private void ApplyVideoSurface(bool forceGi)
    {
        EnsureSkyPlane();
        if (skyPlane == null) return;

        Camera cam = Camera.main;
        if (cam != null)
        {
            if (cam.clearFlags != CameraClearFlags.SolidColor)
                cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
        }

        if (RenderSettings.skybox != null)
            RenderSettings.skybox = null;

        // Video-sky mode clears the Unity skybox, so Skybox ambient has no
        // source. Apply the profile's flat ambient color and intensity directly.
        var lighting = RenderManager.Instance != null ? RenderManager.Instance.profile?.lighting : null;
        float ambientIntensity = lighting != null && lighting.ambientEnabled
            ? lighting.ambientIntensity
            : 0f;
        Color ambientColor = lighting != null ? lighting.ambientFlatColor : new Color(0.2f, 0.2f, 0.2f);
        if (RenderSettings.ambientMode != UnityEngine.Rendering.AmbientMode.Flat)
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientIntensity = ambientIntensity;
        RenderSettings.ambientLight = ambientColor * ambientIntensity;

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
        if (cam == null || layeredSkyMaterial == null) return;

        if (skyPlane == null)
        {
            var plane = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            plane.name = "K1L0 Layered Sky Plane";
            var collider = plane.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            skyPlane = plane.transform;
            skyPlaneRenderer = plane.GetComponent<MeshRenderer>();
            skyPlaneRenderer.sharedMaterial = layeredSkyMaterial;
            skyPlaneRenderer.shadowCastingMode = ShadowCastingMode.Off;
            skyPlaneRenderer.receiveShadows = false;
            Debug.Log("[DynamicSkyVideo] Created horizon-anchored layered sky plane.");
        }

        float distance = Mathf.Max(30f, Mathf.Min(cam.farClipPlane * 0.82f, 900f));
        skyPlane.SetParent(null, true);
        skyPlane.position = cam.transform.position;
        skyPlane.rotation = Quaternion.identity;
        skyPlane.localScale = Vector3.one * distance * 2f;
        skyPlane.gameObject.SetActive(true);
    }

    private void ApplySkyRenderer()
    {
        EnsureSkyPlane();
        if (skyPlaneRenderer == null) return;
        skyPlaneRenderer.sharedMaterial = layeredSkyMaterial;
        ApplyExperimentalParameters();
        Debug.Log("[K1L0Atmosphere] procedural dome active");
    }

    private void ApplyExperimentalParameters()
    {
        if (layeredSkyMaterial == null) return;
        float topHue = PlayerPrefs.GetFloat("k1lo_layeredSkyTopHue", 0.62f);
        float midHue = PlayerPrefs.GetFloat("k1lo_layeredSkyMidHue", 0.76f);
        float horizonHue = PlayerPrefs.GetFloat("k1lo_layeredSkyHorizonHue", 0.94f);
        layeredSkyMaterial.SetColor("_TopColor", Color.HSVToRGB(Mathf.Repeat(topHue, 1f), .82f, .78f));
        layeredSkyMaterial.SetColor("_MidColor", Color.HSVToRGB(Mathf.Repeat(midHue, 1f), .72f, .82f));
        layeredSkyMaterial.SetColor("_HorizonColor", Color.HSVToRGB(Mathf.Repeat(horizonHue, 1f), .62f, .92f));
        layeredSkyMaterial.SetColor("_CloudColor", new Color(.96f,.93f,.90f,1f));
        layeredSkyMaterial.SetFloat("_CloudOpacity", PlayerPrefs.GetFloat("k1lo_layeredCloudOpacity", .72f));
        layeredSkyMaterial.SetFloat("_CloudSpeed", PlayerPrefs.GetFloat("k1lo_layeredCloudSpeed", .07f));
        layeredSkyMaterial.SetFloat("_CloudScale", PlayerPrefs.GetFloat("k1lo_layeredCloudScale", 1.35f));
        layeredSkyMaterial.SetFloat("_CloudContrast", PlayerPrefs.GetFloat("k1lo_layeredCloudContrast", 1.1f));
        int effect = Mathf.RoundToInt(PlayerPrefs.GetFloat("k1lo_layeredSkyEffect", 0f));
        float rain = PlayerPrefs.GetFloat("k1lo_layeredRain", 0f);
        float aurora = PlayerPrefs.GetFloat("k1lo_layeredAurora", 0f);
        layeredSkyMaterial.SetFloat("_RainStrength", effect == 1 ? Mathf.Max(.7f, rain) : effect == 4 ? Mathf.Max(.9f, rain) : 0f);
        layeredSkyMaterial.SetFloat("_SnowStrength", effect == 2 ? .85f : 0f);
        // Aurora is part of every astronomical night. Sky Lab's aurora mode
        // still provides the stronger manual preview/intensity override.
        layeredSkyMaterial.SetFloat("_AuroraStrength", effect == 3 ? .28f + aurora * .72f : .22f);
        layeredSkyMaterial.SetFloat("_StormStrength", effect == 4 ? 1f : 0f);
        layeredSkyMaterial.SetFloat("_NightBlackness", PlayerPrefs.GetFloat("k1lo_layeredNightBlackness", .72f));
        SkyWeatherVolume.SetEffect(effect, rain);
    }

    private void UpdateCelestialParameters()
    {
        if (layeredSkyMaterial == null || Camera.main == null) return;
        Camera cam = Camera.main;
        bool bypass = PlayerPrefs.GetFloat("k1lo_layeredBypassWeather", 0f) > .5f;
        Vector3 sun = RenderManager.LiveSunDirection.normalized;
        float altitude = RenderManager.LiveSunAltitudeDeg;
        float sunAz = Mathf.Atan2(sun.x, sun.z) * Mathf.Rad2Deg;
        if (bypass)
        {
            float hour = PlayerPrefs.GetFloat("k1lo_manualHour", 13.25f);
            altitude = Mathf.Sin((hour - 6f) / 24f * Mathf.PI * 2f) * 62f;
            sunAz = Mathf.Repeat(hour / 24f * 360f + 90f, 360f);
        }
        else if (PlayerPrefs.HasKey("k1lo_nativeSunAltitude") && PlayerPrefs.HasKey("k1lo_nativeSunAzimuth"))
        {
            altitude = PlayerPrefs.GetFloat("k1lo_nativeSunAltitude");
            sunAz = PlayerPrefs.GetFloat("k1lo_nativeSunAzimuth");
        }
        else if (sun.sqrMagnitude < .5f)
        {
            // Safe startup state until either astronomy or live isDay arrives.
            altitude = 12f;
            sunAz = 180f;
        }
        if (!bypass) ApplyLiveSolarPalette(altitude);
        float horizontalFov = 2f * Mathf.Atan(Mathf.Tan(cam.fieldOfView * .5f * Mathf.Deg2Rad) * cam.aspect) * Mathf.Rad2Deg;
        float sunX = .5f + Mathf.DeltaAngle(cam.transform.eulerAngles.y, sunAz) / Mathf.Max(1f, horizontalFov);
        float sunY = .08f + Mathf.Clamp(altitude, -8f, 90f) / 90f * .78f;
        float sunVisible = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-4f, 2f, altitude));
        layeredSkyMaterial.SetVector("_SunUV", new Vector4(sunX, sunY, 0, 0));
        Vector3 celestialSun = Quaternion.Euler(-altitude, sunAz, 0f) * Vector3.forward;
        layeredSkyMaterial.SetVector("_SunDirection", new Vector4(celestialSun.x, celestialSun.y, celestialSun.z, 0));
        layeredSkyMaterial.SetFloat("_SunVisibility", sunVisible);

        float moonAz = Mathf.Repeat(sunAz + 180f, 360f);
        float moonX = .5f + Mathf.DeltaAngle(cam.transform.eulerAngles.y, moonAz) / Mathf.Max(1f, horizontalFov);
        layeredSkyMaterial.SetVector("_MoonUV", new Vector4(moonX, .38f, 0, 0));
        Vector3 celestialMoon = -celestialSun;
        layeredSkyMaterial.SetVector("_MoonDirection", new Vector4(celestialMoon.x, celestialMoon.y, celestialMoon.z, 0));
        float night = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-2f, -10f, altitude));
        if (bypass && Mathf.RoundToInt(PlayerPrefs.GetFloat("k1lo_layeredSkyEffect", 0f)) == 3)
            night = 1f;
        layeredSkyMaterial.SetFloat("_MoonVisibility", night);
        layeredSkyMaterial.SetFloat("_StarsVisibility", night);
        layeredSkyMaterial.SetFloat("_NightAmount", night);
    }

    private void ApplyLiveSolarPalette(float altitude)
    {
        Color top;
        Color mid;
        Color horizon;
        if (altitude >= 10f)
        {
            top = new Color(.055f, .25f, .66f);
            mid = new Color(.18f, .48f, .84f);
            horizon = new Color(.62f, .76f, .86f);
        }
        else if (altitude >= 0f)
        {
            float t = altitude / 10f;
            top = Color.Lerp(new Color(.055f, .12f, .38f), new Color(.055f, .25f, .66f), t);
            mid = Color.Lerp(new Color(.30f, .29f, .48f), new Color(.18f, .48f, .84f), t);
            horizon = Color.Lerp(new Color(.98f, .43f, .18f), new Color(.62f, .76f, .86f), t);
        }
        else if (altitude >= -6f)
        {
            float t = (altitude + 6f) / 6f;
            top = Color.Lerp(new Color(.018f, .035f, .14f), new Color(.055f, .12f, .38f), t);
            mid = Color.Lerp(new Color(.07f, .07f, .22f), new Color(.30f, .29f, .48f), t);
            horizon = Color.Lerp(new Color(.20f, .13f, .25f), new Color(.98f, .43f, .18f), t);
        }
        else if (altitude >= -12f)
        {
            float t = (altitude + 12f) / 6f;
            top = Color.Lerp(new Color(.004f, .008f, .028f), new Color(.018f, .035f, .14f), t);
            mid = Color.Lerp(new Color(.012f, .022f, .065f), new Color(.07f, .07f, .22f), t);
            horizon = Color.Lerp(new Color(.035f, .05f, .11f), new Color(.20f, .13f, .25f), t);
        }
        else
        {
            top = new Color(.002f, .004f, .014f);
            mid = new Color(.006f, .012f, .035f);
            horizon = new Color(.018f, .028f, .065f);
        }
        layeredSkyMaterial.SetColor("_TopColor", top);
        layeredSkyMaterial.SetColor("_MidColor", mid);
        layeredSkyMaterial.SetColor("_HorizonColor", horizon);
    }

#if K1L0_LEGACY_VIDEO_SKY
    private void ApplySkyTextureTransform(Camera cam)
    {
        if (videoMaterial == null || cam == null) return;

        float yawTurns = cam.transform.eulerAngles.y / 360f;
        Vector2 scale = Vector2.one;
        Vector2 offset = new Vector2(yawTurns * SkyPanCyclesPerTurn, 0f);

        SetTextureScaleOffset(videoMaterial, scale, offset);
    }

    private static void SetVideoTexture(Material material, Texture texture)
    {
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
    }

    private static void SetTextureScaleOffset(Material material, Vector2 scale, Vector2 offset)
    {
        if (material.HasProperty("_BaseMap"))
        {
            material.SetTextureScale("_BaseMap", scale);
            material.SetTextureOffset("_BaseMap", offset);
        }
        if (material.HasProperty("_MainTex"))
        {
            material.SetTextureScale("_MainTex", scale);
            material.SetTextureOffset("_MainTex", offset);
        }
    }
#endif
}
