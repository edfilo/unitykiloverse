using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

namespace KiloWorld.Rendering
{
    [CreateAssetMenu(fileName = "KiloWorldMasterProfile", menuName = "KiloWorld/Master Profile")]
    public class KiloWorldMasterProfile : ScriptableObject
    {
        [Header("Lighting Settings")]
        public LightingSettings lighting = new LightingSettings();

        [Header("Sky Settings")]
        public SkySettings sky = new SkySettings();

        [Header("Renderer Features")]
        public RendererFeatureSettings rendererFeatures = new RendererFeatureSettings();

        [Header("Particle Beams")]
        public OrbSettings orbs = new OrbSettings();

        [Header("Volumetric Fog")]
        public VolumetricFogSettings volumetricFog = new VolumetricFogSettings();

        [Header("Post-Processing Settings")]
        public PostFXSettings postFX = new PostFXSettings();

        [Header("Camera Settings")]
        public CameraSettings camera = new CameraSettings();

        [Header("Startup Location")]
        public StartupLocationSettings startupLocation = new StartupLocationSettings();

        [Header("Teleport Slots")]
        public TeleportSettings teleportSettings = new TeleportSettings();

        [Header("Road Settings")]
        public RoadSettings roads = new RoadSettings();

        [Header("Building Settings")]
        public BuildingSettings buildings = new BuildingSettings();

        [Header("Ground Settings")]
        public GroundSettings ground = new GroundSettings();

        [Header("Player Helmet")]
        public HelmetSettings helmet = new HelmetSettings();

        [Header("API Configuration")]
        public APISettings api = new APISettings();

        [Header("Client Event Triggers")]
        public ClientEventTriggerSettings clientEventTriggers = new ClientEventTriggerSettings();



        // --- Data Structures ---

        [System.Serializable]
        public class TeleportSlot
        {
            public string name = "New Location";
            public double latitude;
            public double longitude;
        }

        [System.Serializable]
        public class TeleportSettings
        {
            [Tooltip("All available teleport locations - add as many as you want!")]
            public System.Collections.Generic.List<TeleportSlot> locations = new System.Collections.Generic.List<TeleportSlot>()
            {
                new TeleportSlot() { name = "St Charles Tavern", latitude = 32.20232095513046, longitude = -110.96586529245728 },
                new TeleportSlot() { name = "Dunkin Donuts", latitude = 40.69491386993338, longitude = -80.10504714110444 },
                new TeleportSlot() { name = "Meeder Green", latitude = 40.70063561266829, longitude = -80.10918961729158 },
                new TeleportSlot() { name = "Hernandez Park", latitude = 40.703155506631546, longitude = -73.9238789473517 },
                new TeleportSlot() { name = "Point State Park", latitude = 40.441794818874946, longitude = -80.0132127760516 }
            };

        }

        [System.Serializable]
        public class ClientEventTriggerSettings
        {
            [Tooltip("Enable client-side enter pings when crossing into a nearby location.")]
            public bool enableLocationEnter = true;

            [Tooltip("Distance in meters to trigger an enter event.")]
            public float enterDistanceMeters = 30f;

            [Tooltip("Minimum hours between enter triggers.")]
            public float enterCooldownHours = 2f;
        }

        [System.Serializable]
        public class RendererFeatureSettings
        {
            public enum SampleCount { Low, Medium, High }
            public enum Quality { Low, Medium, High, Ultra }
            public enum Resolution { Full, Half, Quarter }

            [Header("Screen Space Ambient Occlusion (SSAO)")]
            public bool ssaoEnabled = true;
            [Range(0, 4)] public float ssaoIntensity = 0.6f;
            [Range(0.01f, 2f)] public float ssaoRadius = 0.3f;
            [Range(0, 1)] public float ssaoDirectLightingStrength = 0.05f;
            public SampleCount ssaoSamples = SampleCount.High;

            [Header("Screen Space Reflections (SSR)")]
            public bool ssrEnabled = true;
            public Resolution ssrResolution = Resolution.Full;
            public int ssrMaxRaySteps = 48;
            public float ssrThickness = 0.4f;
            public bool ssrAccumulation = true;
        }

        [System.Serializable]
        public class LightingSettings
        {
            [Header("Moonlight (Directional Light)")]
            public Color moonlightColor = new Color(0.7f, 0.8f, 1.0f, 1.0f); // Cool blue moonlight
            [Range(0, 2)] public float moonlightIntensity = 0.5f;
            public Vector3 moonlightRotation = new Vector3(90, 0, 0); // Straight down (Zenith)

            [Header("Shadows")]
            public bool enableShadows = true;
            [Range(0, 1)] public float shadowStrength = 1.0f;
            public float shadowDistance = 150f; // How far shadows render
            public UnityEngine.Rendering.Universal.ShadowResolution shadowResolution = UnityEngine.Rendering.Universal.ShadowResolution._2048;
            [Range(1, 4)] public int shadowCascades = 2; // More cascades = better quality at distance

            [Header("Ambient Lighting")]
            public UnityEngine.Rendering.AmbientMode ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            [Range(0, 2)] public float ambientIntensity = 1.0f;

            [Header("Ambient Colors (Flat/Trilight)")]
            [Tooltip("Used when Ambient Mode is Flat")]
            [ColorUsage(false, true)] public Color ambientFlatColor = new Color(0.2f, 0.2f, 0.2f);

            [Tooltip("Used when Ambient Mode is Trilight")]
            [ColorUsage(false, true)] public Color ambientSkyColor = new Color(0.2f, 0.2f, 0.2f);
            [Tooltip("Used when Ambient Mode is Trilight")]
            [ColorUsage(false, true)] public Color ambientEquatorColor = new Color(0.1f, 0.1f, 0.1f);
            [Tooltip("Used when Ambient Mode is Trilight")]
            [ColorUsage(false, true)] public Color ambientGroundColor = new Color(0.05f, 0.05f, 0.05f);

            [Header("Environment Reflections")]
            public UnityEngine.Rendering.DefaultReflectionMode reflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Skybox;
            public Cubemap customReflectionCubemap;
            [Range(0, 2)] public float reflectionIntensity = 1.0f;
            public int reflectionBounces = 1;

            [Header("Subtractive Shadows")]
            [ColorUsage(false, true)] public Color subtractiveShadowColor = new Color(0.1f, 0.1f, 0.25f);

            [Header("Player Spotlight")]
            [ColorUsage(false, true)]
            [Tooltip("Spotlight color with HDR support")]
            public Color spotlightColor = new Color(0.9f, 0.95f, 1f, 1f); // Slightly blue-white

            [Range(0, 2)]
            [Tooltip("Light intensity")]
            public float spotlightIntensity = 1f;

            [Tooltip("Enable shadow casting")]
            public bool spotlightCastShadows = true;

            [Range(0, 1)]
            [Tooltip("Shadow strength")]
            public float spotlightShadowStrength = 1f;

            [Range(0f, 2f)]
            [Tooltip("Shadow bias to prevent self-shadowing artifacts (black pixels)")]
            public float spotlightShadowBias = 0.05f;

            [Range(0f, 3f)]
            [Tooltip("Shadow normal bias")]
            public float spotlightShadowNormalBias = 1.0f;

            [Tooltip("Enable/disable spotlight")]
            public bool spotlightEnabled = true;

            [Header("Experimental Point Light")]
            public bool enableExperimentalPointLight = false;
            [ColorUsage(false, true)] public Color experimentalPointColor = new Color(1f, 0f, 0f); // Red
            [Range(0, 100)] public float experimentalPointRange = 50f;
            [Range(0, 10)] public float experimentalPointIntensity = 2f;
            public float experimentalPointOffsetZ = 20f;
            public bool experimentalLightFollowsBuilding = true;
        }

        [System.Serializable]
        public class SkySettings
        {
            [Header("HDRI Skybox")]
            public Cubemap hdriSkybox; // HDRI cubemap for realistic sky
            [Range(0, 8)] public float skyboxExposure = 1.0f; // Brightness multiplier
            [Range(0, 360)] public float skyboxRotation = 0f; // Y-axis rotation in degrees
            public Color skyboxTint = Color.white; // Color tint for skybox
        }

        [System.Serializable]
        public class CameraSettings
        {
            [Header("Transition")]
            [Tooltip("Time in seconds to transition between first person and god view")]
            [Range(1, 20)] public float transitionTime = 2f;

            [Header("First Person View")]
            [Tooltip("First person camera height (Y position)")]
            [FormerlySerializedAs("positionY")]
            public float fpPositionY = 1.6f;

            [Tooltip("First person forward/back offset (Z position)")]
            [FormerlySerializedAs("positionZ")]
            public float fpPositionZ = 0.3f;

            [Tooltip("First person pitch (X-axis rotation, up/down)")]
            [FormerlySerializedAs("rotationX")]
            [Range(-90, 90)] public float fpRotationX = 0f;

            [Header("God View")]
            [Tooltip("God view camera height (Y position)")]
            public float godPositionY = 100f;

            [Tooltip("God view distance (Z position, further back)")]
            public float godPositionZ = 100f;

            [Tooltip("God view pitch (X-axis rotation, looking down)")]
            [Range(-90, 90)] public float godRotationX = 55f;

            [Header("Camera Clipping")]
            [Tooltip("Near clipping plane distance")]
            public float nearClipPlane = 0.3f;

            [Tooltip("Far clipping plane distance (match to fog visibility for performance)")]
            public float farClipPlane = 250f;
        }

        [System.Serializable]
        public class VolumetricFogSettings
        {
            [Header("Rendering Quality")]
            [Tooltip("The quality of the raymarching. Higher quality means more samples")]
            [Range(1, 16)] public int raymarchQuality = 6;
            [Range(0f, 50)] public float raymarchNearStepping = 8f;
            [Range(0f, 50)] public float raymarchMinStep = 0.1f;
            [Range(0, 1)] public float jittering = 0.5f;
            [Range(0, 2)] public float dithering = 1f;

            [Header("Density")]
            public bool constantDensity = false;
            [Range(0, 3)] public float noiseStrength = 1.2f;
            public float noiseScale = 8f;
            public float noiseFinalMultiplier = 1.2f;
            public float density = 0.4f;

            [Header("Colors")]
            [ColorUsage(false)] public Color albedo = new Color(0.85f, 0.75f, 0.7f, 1f); // Warm atmospheric tint
            [Range(0, 2)] public float brightness = 1.5f;
            [Range(0, 2)] public float deepObscurance = 0.6f;
            public Color specularColor = new Color(1, 0.85f, 0.7f, 1); // Warm specular highlights
            [Range(0, 1f)] public float specularThreshold = 0.5f;
            [Range(0, 1f)] public float specularIntensity = 0.8f;

            [Header("Animation")]
            public float turbulence = 0.73f;
            public Vector3 windDirection = new Vector3(0.02f, 0, 0);

            [Header("Directional Light")]
            [Range(0, 256)] public float lightDiffusionPower = 120;
            [Range(0, 2)] public float lightDiffusionIntensity = 1.2f;
            public bool receiveShadows = false;
            [Range(0, 1)] public float shadowIntensity = 0.5f;

            [Header("Light Interaction (Point/Spot)")]
            public bool enableNativeLights = true;
            [Range(0, 10f)] public float nativeLightsMultiplier = 2f;
            public bool enablePointLights = true;
            public bool enableVoids = false;
            public bool enableAPV = false;
            [Range(0, 10f)] public float apvIntensityMultiplier = 1f;

            [Header("Scattering (Fog Manager)")]
            [Range(0, 1)] public float scattering = 0.6f;
            public float scatteringThreshold = 0.05f;
            public float scatteringIntensity = 1.2f;
            [Range(0, 1)] public float scatteringAbsorption = 0.35f;
            public Color scatteringTint = Color.white;
            public bool scatteringHighQuality = true;

            [Header("Geometry")]
            [Range(0, 1)] public float border = 0.05f;
            [Tooltip("Use custom height instead of Transform Scale Y")]
            public bool customHeight = false;
            [Tooltip("Fog volume height in meters (when customHeight enabled)")]
            public float height = 200f;
            public float verticalOffset = 0f;
            public float distance = 0f;
            [Range(0, 1)] public float distanceFallOff = 0.93f;
            public float maxDistance = 10000f;
            [Range(0, 1)] public float maxDistanceFallOff = 0f;

            [Header("Distant Fog")]
            [Tooltip("Enables exponential distant fog for horizon/sky coverage")]
            public bool distantFog = true;
            public float distantFogStartDistance = 200f;
            [Range(0, 2)] public float distantFogDistanceDensity = 1.2f;
            public float distantFogMaxHeight = 200f;
            [Range(0, 2)] public float distantFogHeightDensity = 1.5f;
            [ColorUsage(false)] public Color distantFogColor = new Color(0.9f, 0.6f, 0.5f); // Warm atmospheric glow
            [Range(0, 2)] public float distantFogDiffusionIntensity = 1.5f;
            public float distantFogBaseAltitude = 0f;
            public bool distantFogSymmetrical = false;
        }

        [System.Serializable]
        public class PostFXSettings
        {
            [Header("Bloom")]
            public bool bloomEnabled = true;
            public float bloomIntensity = 2.5f; // Lowered from 5.0
            public float bloomThreshold = 0.5f; // Lowered threshold to catch reflections
            [Range(0, 1)] public float bloomScatter = 0.95f; // Increased for wider glow
            public Texture2D lensDirtTexture; // Subtle lens dirt overlay
            [Range(0, 10)] public float lensDirtIntensity = 2.0f; // Subtle dirt effect
            public Color bloomTint = Color.white;

            [Header("Tonemapping")]
            public bool tonemappingEnabled = true;
            public TonemappingMode tonemappingMode = TonemappingMode.Neutral;

            [Header("Vignette")]
            public bool vignetteEnabled = true;
            public Color vignetteColor = Color.black;
            [Range(0, 1)] public float vignetteIntensity = 0.3f;
            [Range(0.01f, 1)] public float vignetteSmoothness = 0.2f;
            public bool vignetteRounded = false;

            [Header("Chromatic Aberration")]
            public bool chromaticAberrationEnabled = true;
            [Range(0, 1)] public float chromaticAberrationIntensity = 0.1f;

            [Header("Lens Distortion (Camera Feed)")]
            public bool lensDistortionEnabled = true;
            [Range(-1, 1)] public float lensDistortionIntensity = -0.15f; // Negative = Barrel (GoPro style)
            [Range(0.01f, 5)] public float lensDistortionXMultiplier = 1.0f;
            [Range(0.01f, 5)] public float lensDistortionYMultiplier = 1.0f;
            [Range(0.01f, 5)] public float lensDistortionScale = 1.0f;

            [Header("Exposure")]
            public bool exposureEnabled = true;
            public float exposureFixedValue = 0f; // Fixed exposure value

            [Header("Depth of Field")]
            public bool depthOfFieldEnabled = false;
            [Range(0.1f, 300f)] public float focusDistance = 10f; // Distance to focus point
            [Range(0.05f, 32f)] public float aperture = 5.6f; // f-stop (lower = more blur)
            [Range(1f, 300f)] public float focalLength = 50f; // Lens focal length in mm

            [Header("Color Grading")]
            public bool colorGradingEnabled = true;
            [HideInInspector] public bool whiteBalanceActive = true;
            [HideInInspector] public bool temperatureOverride = true;
            [HideInInspector] public bool tintOverride = true;
            [HideInInspector] [Range(-100, 100)] public float temperature = 0f; // Cool (-) to Warm (+)
            [HideInInspector] [Range(-100, 100)] public float tint = 0f; // Green (-) to Magenta (+)
            [Range(-100, 100)] public float hueShift = 0f;
            [Range(-100, 100)] public float saturation = 0f;
            [Range(-100, 100)] public float contrast = 0f;
            
            [Header("Color Grading - Shadows/Midtones/Highlights")]
            [HideInInspector] public Vector4 shadows = new Vector4(1f, 1f, 1f, 0f);
            [HideInInspector] public Vector4 midtones = new Vector4(1f, 1f, 1f, 0f);
            [HideInInspector] public Vector4 highlights = new Vector4(1f, 1f, 1f, 0f);
            [HideInInspector] public bool shadowsOverride = true;
            [HideInInspector] public bool midtonesOverride = true;
            [HideInInspector] public bool highlightsOverride = true;
            [HideInInspector] [Range(0f, 1f)] public float shadowsStart = 0f;
            [HideInInspector] [Range(0f, 1f)] public float shadowsEnd = 0.3f;
            [HideInInspector] [Range(0f, 1f)] public float highlightsStart = 0.55f;
            [HideInInspector] [Range(0f, 1f)] public float highlightsEnd = 1f;
            [HideInInspector] public bool shadowsStartOverride = true;
            [HideInInspector] public bool shadowsEndOverride = true;
            [HideInInspector] public bool highlightsStartOverride = true;
            [HideInInspector] public bool highlightsEndOverride = true;

            [Header("Motion Blur")]
            public bool motionBlurEnabled = false;
            public UnityEngine.Rendering.Universal.MotionBlurQuality motionBlurQuality = UnityEngine.Rendering.Universal.MotionBlurQuality.Medium;
            [Range(0, 1)] public float motionBlurIntensity = 0.5f;
            [Header("Ambient Occlusion (SSAO)")]
            public bool ambientOcclusionEnabled = true;
            [Range(0, 4)] public float aoIntensity = 0.5f;
            [Range(0.25f, 5f)] public float aoRadius = 0.5f; // Sample radius

            [Header("Film Grain")]
            public bool filmGrainEnabled = false;
            public UnityEngine.Rendering.Universal.FilmGrainLookup filmGrainType = UnityEngine.Rendering.Universal.FilmGrainLookup.Medium1;
            [Range(0, 1)] public float filmGrainIntensity = 0.2f;
            [Range(0, 1)] public float filmGrainResponse = 0.8f; // How grain responds to luminance
        }

        [System.Serializable]
        public class OrbSettings
        {
            [Header("Light Beam")]
            public bool showBeam = true;

            [Tooltip("Particle Count")]
            [Range(0, 5000)] public int particleCount = 2800;

            [Tooltip("Beam Core Color (Emission will be applied on top)")]
            [ColorUsage(false, true)] public Color particleEmissionColor = new Color(0.5f, 0.8f, 1f); // Bright cyan-white

            [Tooltip("Upward Speed")]
            [Range(0.1f, 20f)] public float particleSpeed = 10f;

            [Tooltip("Chaos / Wobble")]
            [Range(0f, 2f)] public float particleChaos = 0.4f;

            [Tooltip("Beam Width - Controls emitter spacing/diameter of particle beam in meters")]
            [Range(0.5f, 5f)] public float beamWidth = 1.3f;

            [Tooltip("Base Particle Size - Individual particle radius in meters (auto = beamWidth * 0.5)")]
            [Range(0.1f, 3f)] public float particleBaseSize = 0.65f;

            // Legacy properties (hidden, kept for backwards compatibility)
            [HideInInspector] public Color glowColor = new Color(1f, 0.8f, 0.3f);
            [HideInInspector] public float emissionIntensity = 2f;
            [HideInInspector] public float orbSize = 2f;
            [HideInInspector] public Color beamColor = Color.white;
            [HideInInspector] public float particleRadius = 0.65f; // Replaced by beamWidth

            [Header("Particle Detail Settings")]
            [Tooltip("Particle size variation (min to max multiplier)")]
            [Range(0.1f, 3f)] public float particleSizeVariation = 2.5f;

            [Tooltip("Size multiplier over particle lifetime (0=shrink to nothing, 1=stay same, 2=grow)")]
            [Range(0f, 2f)] public float particleSizeOverLifetime = 0.8f;

            [Tooltip("Particle fade-in time (0-1, portion of lifetime)")]
            [Range(0f, 0.5f)] public float particleFadeIn = 0.05f;

            [Tooltip("Particle fade-out time (0-1, portion of lifetime)")]
            [Range(0f, 0.5f)] public float particleFadeOut = 0.15f;

            [Tooltip("Emission rate multiplier (higher = denser beam)")]
            [Range(0.1f, 3f)] public float particleDensity = 1.0f;

            [Tooltip("Particle rotation speed (degrees/second)")]
            [Range(0f, 360f)] public float particleRotationSpeed = 45f;

            // Legacy / Internal
            [HideInInspector] [Range(0.1f, 10000f)] public float beamEmission = 750f;
            [HideInInspector] public float beamHeight = 4828f; // 3 miles

            [Header("Proximity Detection")]
            [Tooltip("Trigger alert when player is within this distance of a beam (meters)")]
            [Range(1f, 50f)] public float proximityTriggerDistance = 5.0f;
            [Tooltip("Cooldown between proximity alerts (seconds)")]
            [Range(5f, 300f)] public float proximityAlertCooldown = 30f;

            [Header("Respawn Timing")]
            [Tooltip("How often beams respawn at new locations (seconds). Each beam gets a deterministic offset so they don't all change simultaneously.")]
            [Range(60f, 7200f)] public float respawnIntervalSeconds = 1800f; // 30 minutes default
        }

        [System.Serializable]
        public class RoadSettings
        {
            [Header("Roads")]
            public Material roadMaterial;
            public Color roadColor = Color.white;
            public Texture2D roadAlbedo;
            public Texture2D roadNormal;
            public Vector2 roadTiling = new Vector2(1, 1);
            [Range(0, 1)] public float roadMetallic = 0f;
            [Range(0, 1)] public float roadSmoothness = 0.85f; // Wet look
            [Range(0, 1)] public float roadNormalStrength = 1.0f;
            [ColorUsage(false, true)] public Color roadEmission = Color.black;
            public Texture2D roadEmissionMap;
            [Range(0, 50)] public float roadEmissionIntensity = 0f;

            [Header("Road Positioning")]
            [Tooltip("Road system Y position (height offset in meters)")]
            [Range(-1f, 2f)] public float roadYPosition = 0.3f;

            [Header("Road Advanced")]
            public Texture2D roadOcclusionMap;
            public Texture2D roadHeightMap; // Height/displacement map
            [Range(0, 1)] public float roadOcclusionStrength = 1.0f;
            [Range(0, 0.1f)] public float roadHeightScale = 0.02f; // Parallax height
            public bool roadEnvironmentReflections = true;
            public bool roadSpecularHighlights = true;

            [Header("Road Reflections & Puddles")]
            [Tooltip("Overall reflection strength")]
            [Range(0f, 2f)] public float reflectionStrength = 1.75f;

            [Tooltip("Height offset for reflection sampling (meters above road)")]
            [Range(0f, 100f)] public float reflectionYOffset = 2.0f;

            [Tooltip("Normal-based distortion amount (light scattering in puddles)")]
            [Range(0f, 5f)] public float reflectionDistortion = 0.10f;

            [Tooltip("Horizontal wave/warble amount")]
            [Range(0f, 0.1f)] public float reflectionWarble = 0.05f;

            [Tooltip("Frequency of horizontal warble")]
            [Range(1f, 20f)] public float reflectionWarbleScale = 10.0f;

            [Tooltip("Base color of puddles (usually black)")]
            [ColorUsage(false, true)] public Color puddleColor = Color.black;

            [Tooltip("Puddle size/frequency (higher = smaller, more frequent puddles)")]
            [Range(0.1f, 10f)] public float puddleFrequency = 2.5f;

            [Tooltip("Puddle coverage threshold (0 = dry, 1 = flooded)")]
            [Range(0f, 1f)] public float puddleAmount = 0.5f;

            [Tooltip("Puddle edge sharpness (1 = soft gradient, 10 = hard edges)")]
            [Range(1f, 10f)] public float puddleSharpness = 3.0f;
        }

        [System.Serializable]
        public class BuildingSettings
        {
            [Header("Building Walls (Zoss)")]
            public Material zossWallMaterial;
            public Color zossWallColor = new Color(0.3f, 0.3f, 0.35f);
            public Texture2D zossWallAlbedo;
            public Texture2D zossWallNormal;
            public Texture2D zossWallOcclusionMap; // AO map
            public Texture2D zossWallRoughnessMap; // Roughness (inverted smoothness)
            public Vector2 zossWallTiling = new Vector2(1, 1);
            [Range(0, 1)] public float zossWallMetallic = 0f;
            [Range(0, 1)] public float zossWallSmoothness = 0.401f;
            [Range(0, 1)] public float zossWallNormalStrength = 1.0f;
            [Range(0, 1)] public float zossWallOcclusionStrength = 1.0f;
            [ColorUsage(false, true)] public Color zossWallEmission = Color.black;
            public Texture2D zossWallEmissionMap;
            [Range(0, 50)] public float zossWallEmissionIntensity = 0f;
            public bool zossWallEnvironmentReflections = false; // Disable reflections for brick walls
            public bool zossWallSpecularHighlights = true;

            [Header("Window Lights (Zoss)")]
            public Material zossEmissiveMaterial;
            public Color zossEmissiveColor = new Color(1f, 0.65f, 0.35f); // Warm orange
            public Texture2D zossEmissiveAlbedo;
            public Vector2 zossEmissiveTiling = new Vector2(1, 1);
            [Range(0, 1)] public float zossEmissiveMetallic = 0.05f; // Glass has slight metallic
            [Range(0, 1)] public float zossEmissiveSmoothness = 0.9f; // Glass is very smooth
            [ColorUsage(false, true)] public Color zossEmissiveEmission = new Color(1f, 0.65f, 0.35f);
            public Texture2D zossEmissiveEmissionMap;
            [Range(0, 50)] public float zossEmissiveIntensity = 4.5f; // Increased for brighter glow
            
            [Header("Window Glass Properties")]
            public bool windowEnvironmentReflections = true; // Enable reflections for glass
            public bool windowSpecularHighlights = true; // Enable specular for glass

        }

        [System.Serializable]
        public class GroundSettings
        {
            [Header("Ground/Terrain")]
            [HideInInspector] public Material groundMaterial; // Runtime reference only
            public Color groundColor = Color.white;
            public Texture2D groundTexture;
            public Texture2D groundNormal;
            public Vector2 groundTiling = new Vector2(100, 100);
            [Tooltip("Ground plane Y position (height offset in meters)")]
            [Range(-1f, 1f)] public float groundYPosition = 0.05f;

            [Header("Water Positioning")]
            [Tooltip("Water layer Y position (should be above ground, below roads)")]
            [Range(-1f, 1f)] public float waterYPosition = 0.15f;
            [Range(0, 1)] public float groundMetallic = 0f;
            [Range(0, 1)] public float groundSmoothness = 0.1f; // Reduced from 0.3
            [Range(0, 1)] public float groundNormalStrength = 1.0f;
            [ColorUsage(false, true)] public Color groundEmission = Color.black;
            public Texture2D groundEmissionMap;
            [Range(0, 50)] public float groundEmissionIntensity = 0f;
            [Range(0, 1)] public float groundBrightness = 1.0f;
        }

        [System.Serializable]
        public class StartupLocationSettings
        {
            public enum Location
            {
                StCharlesTavern,
                DunkinDonuts,
                HernandezPark,
                MeederGreen,
                PointStatePark,
                TucsonRandom,
                Hotel,
                ElPasoApt,
                Custom
            }

            [Tooltip("Select startup location")]
            public Location startupLocation = Location.Hotel;

            [Tooltip("Randomize Tucson location within 3 miles every time")]
            public bool randomizeTucson = true;

            [Header("Location Coordinates")]
            [Tooltip("St Charles Tavern - South Tucson")]
            public double stCharlesLatitude = 32.20232095513046;
            public double stCharlesLongitude = -110.96586529245728;

            [Tooltip("Dunkin Donuts - Pittsburgh")]
            public double dunkinLatitude = 40.69491386993338;
            public double dunkinLongitude = -80.10504714110444;

            [Tooltip("Hernandez Park - NYC")]
            public double hernandezLatitude = 40.703155506631546;
            public double hernandezLongitude = -73.9238789473517;

            [Tooltip("Meeder Green Park - Pittsburgh")]
            public double meederLatitude = 40.70063561266829;
            public double meederLongitude = -80.10918961729158;

            [Tooltip("Point State Park - Pittsburgh")]
            public double pointStateParkLatitude = 40.441794818874946;
            public double pointStateParkLongitude = -80.0132127760516;

            [Tooltip("Hotel - Debug")]
            public double hotelLatitude = 32.20763210335854;
            public double hotelLongitude = -110.98073546588911;

            [Tooltip("El Paso Apt")]
            public double elPasoAptLatitude = 31.75623028924599;
            public double elPasoAptLongitude = -106.49324445120884;

            [Header("Custom Location")]
            [Tooltip("Custom coordinates (only used when Location = Custom)")]
            public double customLatitude = 40.69491386993338;
            public double customLongitude = -80.10504714110444;

            public void GetStartupCoordinates(out double latitude, out double longitude)
            {
                switch (startupLocation)
                {
                    case Location.StCharlesTavern:
                        latitude = stCharlesLatitude;
                        longitude = stCharlesLongitude;
                        break;
                    case Location.DunkinDonuts:
                        latitude = dunkinLatitude;
                        longitude = dunkinLongitude;
                        break;
                    case Location.HernandezPark:
                        latitude = hernandezLatitude;
                        longitude = hernandezLongitude;
                        break;
                    case Location.MeederGreen:
                        latitude = meederLatitude;
                        longitude = meederLongitude;
                        break;
                    case Location.PointStatePark:
                        latitude = pointStateParkLatitude;
                        longitude = pointStateParkLongitude;
                        break;
                    case Location.Hotel:
                        latitude = hotelLatitude;
                        longitude = hotelLongitude;
                        break;
                    case Location.ElPasoApt:
                        latitude = elPasoAptLatitude;
                        longitude = elPasoAptLongitude;
                        break;
                    case Location.TucsonRandom:
                        // Tucson Center: 32.2226, -110.9747
                        if (randomizeTucson)
                        {
                            // 3 miles ~ 4828 meters
                            // Random point in circle
                            Vector2 randomCircle = UnityEngine.Random.insideUnitCircle * 4828f;
                            // Convert meters to lat/lon degrees (approx)
                            double latOffset = randomCircle.y / 111320.0;
                            double lonOffset = randomCircle.x / (111320.0 * Mathf.Cos(32.2226f * Mathf.Deg2Rad));
                            
                            latitude = 32.2226 + latOffset;
                            longitude = -110.9747 + lonOffset;
                        }
                        else
                        {
                            latitude = 32.2226;
                            longitude = -110.9747;
                        }
                        break;
                    case Location.Custom:
                        latitude = customLatitude;
                        longitude = customLongitude;
                        break;
                    default:
                        latitude = dunkinLatitude;
                        longitude = dunkinLongitude;
                        break;
                }
            }

            /// <summary>Display name for UI when GPS unavailable (e.g. editor).</summary>
            public string GetStartupLocationDisplayName()
            {
                switch (startupLocation)
                {
                    case Location.StCharlesTavern: return "Tucson";
                    case Location.DunkinDonuts: return "Pittsburgh";
                    case Location.HernandezPark: return "Brooklyn";
                    case Location.MeederGreen: return "Pittsburgh";
                    case Location.PointStatePark: return "Pittsburgh";
                    case Location.Hotel: return "Tucson";
                    case Location.TucsonRandom: return "Tucson";
                    case Location.ElPasoApt: return "El Paso Apt";
                    case Location.Custom: return "Custom";
                    default: return "Pittsburgh";
                }
            }
        }

        [System.Serializable]
        public class HelmetSettings
        {
            [Header("Helmet Transform")]
            [Tooltip("Helmet scale multiplier")]
            [Range(0.1f, 10f)] public float scale = 3f;

            [Tooltip("Helmet position offset (X, Y, Z)")]
            public Vector3 positionOffset = new Vector3(0, 1.6f, 0);

            [Tooltip("Helmet rotation (Euler angles X, Y, Z)")]
            public Vector3 rotation = new Vector3(90f, 270f, 0f);

            [Header("Visibility")]
            [Tooltip("Show/hide helmet")]
            public bool showHelmet = true;
        }

        [System.Serializable]
        public class APISettings
        {
            [Header("Backend Environment")]
            [Tooltip("API environment selection:\n• Auto: Try Localhost → Tethered → Ngrok → Production until one works\n• Localhost: http://localhost:3000\n• Tethered: http://172.20.10.5:3000 (iPhone hotspot)\n• Ngrok: Dynamic tunnel URL\n• Production: https://api.kilomeme.com")]
            public APIManager.APIEnvironment environment = APIManager.APIEnvironment.Auto;

            [Header("Dynamic Ngrok URL")]
            [Tooltip("Fetched from tethered API at startup. Leave empty to use hardcoded fallback.")]
            public string dynamicNgrokURL = "";

            [Header("Manual Overrides")]
            [Tooltip("Override localhost URL (default: http://localhost:3000)")]
            public string customLocalhostURL = "";

            [Tooltip("Override tethered URL (default: http://172.20.10.5:3000)")]
            public string customTetheredURL = "";

        }
    }
}
// Updated
