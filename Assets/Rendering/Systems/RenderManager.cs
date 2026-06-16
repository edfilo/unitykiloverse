using UnityEngine;
using System.Collections;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using KiloWorld.Rendering;
using VolumetricFogAndMist2;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace KiloWorld.Rendering.Systems
{
    [ExecuteAlways]
    public class RenderManager : MonoBehaviour
    {
        [Header("Profile Reference")]
        public KiloWorldMasterProfile profile;

        [Header("Scene References")]
        public Light directionalLight;
        public Volume globalVolume;
        
        // Runtime Cache
        private SimpleGroundPlane _cachedGroundPlane;
        private VolumetricFog _cachedVolumetricFog;
        private VolumetricFogManager _cachedVolumetricFogManager;
        private MeshRenderer[] _cachedRoadRenderers;
        private float _lastRoadCacheTime;

        // Cached Overrides for PostFX
        private Bloom _bloom;
        private Vignette _vignette;
        private ChromaticAberration _chromaticAberration;
        private Tonemapping _tonemapping;
        private ColorAdjustments _colorAdjustments;
        private DepthOfField _depthOfField;
        private WhiteBalance _whiteBalance;
        private ColorCurves _colorCurves;
        private ShadowsMidtonesHighlights _splitToning;
        private MotionBlur _motionBlur;
        private FilmGrain _filmGrain;
        private LensDistortion _lensDistortion;

        public static RenderManager Instance;

        private void OnEnable()
        {
            Instance = this;
            LoadPostFXPrefs();
        }

        private void LoadPostFXPrefs()
        {
            if (profile == null) return;
            var pfx = profile.postFX;
            if (PlayerPrefs.GetInt("k1lo_brightMapDefaults_v1", 0) != 1)
            {
                PlayerPrefs.SetFloat("k1lo_saturation", 35f);
                PlayerPrefs.SetFloat("k1lo_contrast", 18f);
                PlayerPrefs.SetFloat("k1lo_exposureFixedValue", 0.35f);
                PlayerPrefs.SetInt("k1lo_brightMapDefaults_v1", 1);
                PlayerPrefs.Save();
            }

            pfx.saturation = PlayerPrefs.GetFloat("k1lo_saturation", 35f);
            pfx.contrast = PlayerPrefs.GetFloat("k1lo_contrast", 18f);
            pfx.exposureFixedValue = PlayerPrefs.GetFloat("k1lo_exposureFixedValue", 0.35f);
            LoadPref("contrast", ref pfx.contrast);
            LoadPref("exposureFixedValue", ref pfx.exposureFixedValue);
            LoadPref("hueShift", ref pfx.hueShift);
            LoadPref("temperature", ref pfx.temperature);
            LoadPref("tint", ref pfx.tint);
            LoadPref("bloomIntensity", ref pfx.bloomIntensity);
            LoadPref("bloomThreshold", ref pfx.bloomThreshold);
            LoadPref("bloomScatter", ref pfx.bloomScatter);
            LoadPrefBool("bloomEnabled", ref pfx.bloomEnabled);
            LoadPref("vignetteIntensity", ref pfx.vignetteIntensity);
            LoadPref("vignetteSmoothness", ref pfx.vignetteSmoothness);
            LoadPrefBool("vignetteEnabled", ref pfx.vignetteEnabled);
            LoadPref("chromaticIntensity", ref pfx.chromaticAberrationIntensity);
            LoadPrefBool("chromaticEnabled", ref pfx.chromaticAberrationEnabled);
            LoadPref("lensDistIntensity", ref pfx.lensDistortionIntensity);
            LoadPrefBool("lensDistEnabled", ref pfx.lensDistortionEnabled);
            LoadPrefBool("dofEnabled", ref pfx.depthOfFieldEnabled);
            LoadPref("focusDistance", ref pfx.focusDistance);
            LoadPref("aperture", ref pfx.aperture);
            LoadPref("focalLength", ref pfx.focalLength);
            LoadPref("motionBlurIntensity", ref pfx.motionBlurIntensity);
            LoadPrefBool("motionBlurEnabled", ref pfx.motionBlurEnabled);
            LoadPref("filmGrainIntensity", ref pfx.filmGrainIntensity);
            LoadPrefBool("filmGrainEnabled", ref pfx.filmGrainEnabled);
            if (PlayerPrefs.GetInt("k1lo_skyBandingDither_v1", 0) != 1)
            {
                pfx.filmGrainEnabled = true;
                pfx.filmGrainIntensity = Mathf.Max(pfx.filmGrainIntensity, 0.085f);
                PlayerPrefs.SetInt("k1lo_filmGrainEnabled", 1);
                PlayerPrefs.SetFloat("k1lo_filmGrainIntensity", pfx.filmGrainIntensity);
                PlayerPrefs.SetInt("k1lo_skyBandingDither_v1", 1);
                PlayerPrefs.Save();
            }

            // Camera
            var cam = profile.camera;
            LoadPref("godPositionY", ref cam.godPositionY);
            LoadPref("godPositionZ", ref cam.godPositionZ);
            LoadPref("godRotationX", ref cam.godRotationX);
            LoadPref("farClipPlane", ref cam.farClipPlane);

            var sky = profile.sky;
            LoadPrefBool("auroraEnabled", ref sky.auroraEnabled);
            LoadPref("auroraIntensity", ref sky.auroraIntensity);
            LoadPref("auroraHeight", ref sky.auroraHeight);
            LoadPref("auroraDistance", ref sky.auroraDistance);
            LoadPref("auroraWidth", ref sky.auroraWidth);
            LoadPref("auroraVerticalSize", ref sky.auroraVerticalSize);
            LoadPref("auroraDriftSpeed", ref sky.auroraDriftSpeed);

            LoadPref("manualHour", ref ManualHour);
            if (PlayerPrefs.HasKey("k1lo_manualWeatherGlyph"))
                ManualWeatherGlyph = PlayerPrefs.GetString("k1lo_manualWeatherGlyph");
        }

        private static void LoadPref(string key, ref float field)
        {
            string k = "k1lo_" + key;
            if (PlayerPrefs.HasKey(k)) field = PlayerPrefs.GetFloat(k);
        }

        private static void LoadPrefBool(string key, ref bool field)
        {
            string k = "k1lo_" + key;
            if (PlayerPrefs.HasKey(k)) field = PlayerPrefs.GetFloat(k) >= 0.5f;
        }

        private void Start()
        {
            StartCoroutine(DeferredStart());
        }

        private IEnumerator DeferredStart()
        {
            while (!BootState.AllowRender)
            {
                yield return null;
            }

            BootDiagnostics.Mark("RenderManager.Start");
            // Verify URP has Opaque Texture enabled (required for screen-space reflections)
            CheckOpaqueTextureEnabled();

            // Apply on Start to ensure all components (like SimpleGroundPlane) are initialized
            Apply();
            BootDiagnostics.Mark("RenderManager.Start complete");
        }

        private void CheckOpaqueTextureEnabled()
        {
            var urpAsset = UniversalRenderPipeline.asset;
            if (urpAsset != null)
            {
                // Use reflection to check if Opaque Texture is enabled
                var property = typeof(UniversalRenderPipelineAsset).GetProperty("supportsCameraOpaqueTexture",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                if (property != null)
                {
                    bool opaqueTextureEnabled = (bool)property.GetValue(urpAsset);
                    if (!opaqueTextureEnabled)
                    {
                        Debug.LogError("[RenderManager] ❌ OPAQUE TEXTURE IS DISABLED! Screen-space reflections will NOT work!\n" +
                                     "To fix: Select URP Asset → Enable 'Opaque Texture' in Rendering settings");
                    }
                    else
                    {
                        Debug.Log("[RenderManager] ✅ Opaque Texture is enabled - screen-space reflections ready");
                    }
                }
            }
        }

        private void OnValidate()
        {
            // Auto-apply in Editor when this component changes
            // Skip during builds to avoid "cannot call during OnValidate" errors
            #if UNITY_EDITOR
            if (!UnityEditor.BuildPipeline.isBuildingPlayer)
            {
                Apply();
            }
            #endif
        }

        private void Update()
        {
            // In Edit Mode: Full Apply for robustness
            // In Play Mode: Lightweight Apply for performance + responsiveness
            
            #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Apply();
                return;
            }
            #endif

            if (Application.isPlaying)
            {
                ApplyRuntime();
            }
        }

        // Full application (safe for Edit Mode, or initialization)
        public void Apply()
        {
            if (profile == null)
            {
                Debug.LogWarning("[RenderManager] Apply() called but profile is null!");
                return;
            }

            // Ensure legacy fog is disabled
            RenderSettings.fog = false;

            ApplyLighting();
            ApplySky();
            ApplyPostFX();
            ApplyVolumetricFog();
            ApplyMaterials(); // Heavy material property block updates
            ApplyGroundMaterial(); // Ground plane updates
            ApplyMapSettings(); // Road/Map height updates
            ApplyRendererFeatures(); // URP Feature toggles
        }

        // Lightweight application (safe for every frame in Play Mode)
        // Updates only things that don't cause massive re-allocations or searches
        private void ApplyRuntime()
        {
            if (profile == null) return;

            // Update Lighting (Sun rotation, color) - critical for real-time god rays
            ApplyLighting();
            
            // Update PostFX (Bloom, grading, etc) - critical for visual tuning
            // We use the cached volume components, so this is fast
            ApplyPostFX();

            // Update Volumetric Fog - critical for atmospheric tuning
            ApplyVolumetricFog();

            // Update Materials (Roads, Puddles) - Cached and optimized
            ApplyMaterials();

            // Update Skybox (Exposure, Rotation)
            // Material property setting is relatively cheap
            ApplySky();
            
            // Update Ground (Color, Texture, etc)
            ApplyGroundMaterial();

            // Update Map/Road height
            ApplyMapSettings();

            // TEMP: Disable BuildingColliderManager creation in editor (we disabled it to prevent freeze)
#if !UNITY_EDITOR
            // Ensure Building Collider Manager is running (for POI occlusion)
            if (BuildingColliderManager.Instance == null)
            {
                GameObject go = new GameObject("BuildingColliderSystem");
                go.AddComponent<BuildingColliderManager>();
            }
#endif

            // TEMP: Disable expensive FindObjectsOfType every frame in editor (causes freeze)
#if !UNITY_EDITOR
            // Debug: Check for rogue reflection probes causing streaks
            LogReflectionProbes();
#endif
        }

        private void LogReflectionProbes()
        {
            var probes = FindObjectsOfType<ReflectionProbe>();
            if (probes.Length > 0)
            {
                foreach (var p in probes)
                {
                    if (p.enabled)
                    {
                        Debug.Log($"[RenderManager] Found Active Reflection Probe: '{p.name}' | Type: {p.mode} | BoxProjection: {p.boxProjection} | Size: {p.size}");
                    }
                }
            }
        }

        private void ApplyRendererFeatures()
        {
            var urpAsset = UniversalRenderPipeline.asset;
            if (urpAsset == null)
            {
                Debug.LogWarning("[RenderManager] URP Asset is null - cannot apply renderer features");
                return;
            }

            // Access the active renderer
            var renderer = urpAsset.scriptableRenderer;
            if (renderer == null)
            {
                Debug.LogWarning("[RenderManager] Active renderer is null");
                return;
            }

            // Use Reflection to get 'm_RendererFeatures' (List<ScriptableRendererFeature>)
            var type = typeof(ScriptableRenderer);
            var fieldInfo = type.GetField("m_RendererFeatures", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (fieldInfo == null)
            {
                Debug.LogWarning("[RenderManager] Could not find m_RendererFeatures field via reflection");
                return;
            }

            var features = fieldInfo.GetValue(renderer) as System.Collections.Generic.List<ScriptableRendererFeature>;
            if (features == null)
            {
                Debug.LogWarning("[RenderManager] Renderer features list is null");
                return;
            }

            bool foundSSAO = false;
            bool foundSSR = false;

            foreach (var feature in features)
            {
                if (feature == null) continue;

                string featureName = feature.name;
                string typeName = feature.GetType().Name;

                // --- SSAO ---
                // Match by exact type name or substring for robustness
                if (typeName.Contains("ScreenSpaceAmbientOcclusion") || featureName.Contains("SSAO"))
                {
                    foundSSAO = true;
                    feature.SetActive(profile.rendererFeatures.ssaoEnabled);

                    if (profile.rendererFeatures.ssaoEnabled)
                    {
                        SetFeatureField(feature, "Intensity", profile.rendererFeatures.ssaoIntensity);
                        SetFeatureField(feature, "Radius", profile.rendererFeatures.ssaoRadius);
                        SetFeatureField(feature, "DirectLightingStrength", profile.rendererFeatures.ssaoDirectLightingStrength);

                        // Unity URP SSAO uses "Samples" for the Quality enum (Low/Med/High)
                        // But some versions might use "SampleCount"
                        SetFeatureField(feature, "Samples", (int)profile.rendererFeatures.ssaoSamples);
                        SetFeatureField(feature, "SampleCount", (int)profile.rendererFeatures.ssaoSamples);

                        // CRITICAL: Force feature to recreate with new settings
                        #if UNITY_EDITOR
                        UnityEditor.EditorUtility.SetDirty(feature);
                        #endif
                        feature.Create(); // Recreate the feature's render passes with new settings
                    }
                }

                // --- SSR ---
                else if (featureName.Contains("ScreenSpaceReflection") || featureName.Contains("SSR"))
                {
                    foundSSR = true;
                    feature.SetActive(profile.rendererFeatures.ssrEnabled);
                    if (profile.rendererFeatures.ssrEnabled)
                    {
                        SetFeatureField(feature, "Resolution", (int)profile.rendererFeatures.ssrResolution);
                        SetFeatureField(feature, "MaxRaySteps", profile.rendererFeatures.ssrMaxRaySteps);
                        SetFeatureField(feature, "Thickness", profile.rendererFeatures.ssrThickness);
                        SetFeatureField(feature, "Accumulation", profile.rendererFeatures.ssrAccumulation);
                    }
                }
            }

            if (!foundSSAO && profile.rendererFeatures.ssaoEnabled)
            {
                Debug.LogWarning("[RenderManager] SSAO is enabled in profile but no SSAO feature found in renderer! Add 'Screen Space Ambient Occlusion' feature to your Renderer Data asset.");
            }

            if (!foundSSR && profile.rendererFeatures.ssrEnabled)
            {
                Debug.LogWarning("[RenderManager] SSR is enabled in profile but no SSR feature found in renderer!");
            }
        }

        private void SetFeatureField(object target, string fieldName, object value)
        {
            if (target == null) return;
            var type = target.GetType();

            // 1. Try to find the field directly
            var field = type.GetField(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);

            // 2. If not found, look for a "settings" or "m_Settings" object
            if (field == null)
            {
                var settingsField = type.GetField("settings", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                if (settingsField == null) settingsField = type.GetField("m_Settings", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);

                if (settingsField != null)
                {
                    object settingsObj = settingsField.GetValue(target);
                    if (settingsObj != null)
                    {
                        var subField = settingsObj.GetType().GetField(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                        if (subField != null)
                        {
                            try
                            {
                                if (subField.FieldType.IsEnum && value is int intVal)
                                    subField.SetValue(settingsObj, System.Enum.ToObject(subField.FieldType, intVal));
                                else
                                    subField.SetValue(settingsObj, value);

                                // IMPORTANT: Value types (structs) must be set back to the parent object
                                settingsField.SetValue(target, settingsObj);
                            }
                            catch (System.Exception e)
                            {
                                Debug.LogWarning($"[RenderManager] Failed to set field {fieldName} = {value}: {e.Message}");
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"[RenderManager] Field '{fieldName}' not found in settings object of type {settingsObj.GetType().Name}");
                        }
                    }
                }
                else
                {
                    Debug.LogWarning($"[RenderManager] Could not find settings field in {type.Name} to set {fieldName}");
                }
            }
            else
            {
                try
                {
                     if (field.FieldType.IsEnum && value is int intVal)
                         field.SetValue(target, System.Enum.ToObject(field.FieldType, intVal));
                     else
                         field.SetValue(target, value);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[RenderManager] Failed to set field {fieldName} = {value}: {e.Message}");
                }
            }
        }

        private void ApplyLighting()
        {
            // Use cached or find light (cached is safer for runtime)
            if (directionalLight == null)
            {
                // TEMP: Disable expensive FindObjectsOfType in editor (causes freeze)
#if !UNITY_EDITOR
                var lights = FindObjectsOfType<Light>();
                foreach (var l in lights)
                {
                    // Look for the main sun/moon, ignore player spotlights
                    if (l.type == LightType.Directional && !l.name.Contains("Spotlight"))
                    {
                        directionalLight = l;
                        break;
                    }
                }
#endif
            }

            if (directionalLight != null)
            {
                // Ensure Volumetric Fog uses this light as the Sun
                if (VolumetricFogManager.instance != null && VolumetricFogManager.instance.sun != directionalLight)
                {
                    VolumetricFogManager.instance.sun = directionalLight;
                }

                if (profile.sky != null && profile.sky.dynamicSky)
                {
                    // Drive the sun/moon from the real solar position + weather.
                    DriveSunLight(directionalLight);
                }
                else
                {
                    if (directionalLight.color != profile.lighting.moonlightColor)
                        directionalLight.color = profile.lighting.moonlightColor;

                    if (directionalLight.intensity != profile.lighting.moonlightIntensity)
                        directionalLight.intensity = profile.lighting.moonlightIntensity;

                    // Always apply full rotation from profile
                    // We use localRotation or rotation? Usually global rotation for sun.
                    Quaternion targetRot = Quaternion.Euler(profile.lighting.moonlightRotation);
                    if (directionalLight.transform.rotation != targetRot)
                        directionalLight.transform.rotation = targetRot;
                }

                var shadowType = profile.lighting.enableShadows ? LightShadows.Soft : LightShadows.None;
                if (directionalLight.shadows != shadowType)
                    directionalLight.shadows = shadowType;
                
                if (directionalLight.shadowStrength != profile.lighting.shadowStrength)
                    directionalLight.shadowStrength = profile.lighting.shadowStrength;
            }

            // Sync Global URP Shadow Settings
            var urpAsset = UniversalRenderPipeline.asset;
            if (urpAsset != null)
            {
                if (urpAsset.shadowDistance != profile.lighting.shadowDistance)
                    urpAsset.shadowDistance = profile.lighting.shadowDistance;
                
                if (urpAsset.shadowCascadeCount != profile.lighting.shadowCascades)
                    urpAsset.shadowCascadeCount = profile.lighting.shadowCascades;
            }

            RenderSettings.sun = directionalLight;

            // Apply Ambient Mode and Colors
            if (RenderSettings.ambientMode != profile.lighting.ambientMode)
                RenderSettings.ambientMode = profile.lighting.ambientMode;
                
            if (RenderSettings.ambientIntensity != profile.lighting.ambientIntensity)
                RenderSettings.ambientIntensity = profile.lighting.ambientIntensity;

            // Apply Colors based on mode
            // Note: RenderSettings.ambientIntensity usually only affects Skybox mode.
            // For Flat and Trilight, we manually multiply the colors by the intensity for consistency.
            float intensity = profile.lighting.ambientIntensity;

            if (profile.lighting.ambientMode == AmbientMode.Flat)
            {
                RenderSettings.ambientLight = profile.lighting.ambientFlatColor * intensity;
            }
            else if (profile.lighting.ambientMode == AmbientMode.Trilight)
            {
                RenderSettings.ambientSkyColor = profile.lighting.ambientSkyColor * intensity;
                RenderSettings.ambientEquatorColor = profile.lighting.ambientEquatorColor * intensity;
                RenderSettings.ambientGroundColor = profile.lighting.ambientGroundColor * intensity;
            }

            // Apply Reflection Settings
            RenderSettings.defaultReflectionMode = profile.lighting.reflectionMode;
            if (profile.lighting.reflectionMode == DefaultReflectionMode.Custom)
            {
                RenderSettings.customReflection = profile.lighting.customReflectionCubemap;
            }

            if (RenderSettings.reflectionIntensity != profile.lighting.reflectionIntensity)
                RenderSettings.reflectionIntensity = profile.lighting.reflectionIntensity;
                
            if (RenderSettings.reflectionBounces != profile.lighting.reflectionBounces)
                RenderSettings.reflectionBounces = profile.lighting.reflectionBounces;

            // Subtractive Shadows
            RenderSettings.subtractiveShadowColor = profile.lighting.subtractiveShadowColor;
        }

        // Cache for Skybox updates
        private Cubemap _lastSkybox;
        private float _lastSkyboxRotation = -1f;
        private float _lastSkyboxExposure = -1f;
        private Color _lastSkyboxTint = Color.clear;
        private GameObject _auroraRoot;
        private MeshFilter _auroraFilter;
        private Material _auroraMaterial;
        private Texture2D _auroraTexture;
        private float _auroraSpan = -1f, _auroraElLow = -1f, _auroraElHigh = -1f;

        // Live state the aurora sky reflects. Set by the HUD surveillance toggle and the
        // presence/weather feed. SurveillanceActive: world (map) camera on → vivid green
        // aurora; off → dim red "offline" sky. WeatherTempF biases the hue warm/cool.
        public static bool SurveillanceActive = true;
        public static float WeatherTempF = float.NaN;
        public static string WeatherGlyph = null;   // condition word/emoji from the presence feed

        // Manual sky overrides, used only when GPS mode is off (no live time/location).
        public static float ManualHour = 13f;            // 0..24 local hour
        public static string ManualWeatherGlyph = "clear";

        // Dynamic sky state.
        private Material _proceduralSky;
        private Material _nightCubeSky;
        private float _lastGiSunAlt = -999f;
        private float _lastGiTime = -999f;

        private void ApplySky()
        {
            Camera mainCam = Camera.main;
            if (mainCam == null) return;

            // Apply camera clipping planes from profile
            mainCam.farClipPlane = profile.camera.farClipPlane;
            mainCam.nearClipPlane = profile.camera.nearClipPlane;
            
            if (mainCam.clearFlags != CameraClearFlags.Skybox)
                mainCam.clearFlags = CameraClearFlags.Skybox;

            if (profile.sky != null && profile.sky.dynamicSky)
            {
                ApplyProceduralSky();
                ApplyAurora(mainCam);
                ApplyPrecip(mainCam);
                return;
            }

            if (profile.sky.hdriSkybox != null)
            {
                Material skyboxMat = RenderSettings.skybox;
                if (skyboxMat == null || skyboxMat.shader.name != "Skybox/Cubemap")
                {
                    skyboxMat = new Material(Shader.Find("Skybox/Cubemap"));
                    RenderSettings.skybox = skyboxMat;
                }

                // Check for changes before applying to avoid expensive GI updates
                bool skyChanged = false;
                if (_lastSkybox != profile.sky.hdriSkybox) skyChanged = true;
                if (Mathf.Abs(_lastSkyboxRotation - profile.sky.skyboxRotation) > 0.01f) skyChanged = true;
                if (Mathf.Abs(_lastSkyboxExposure - profile.sky.skyboxExposure) > 0.01f) skyChanged = true;
                if (_lastSkyboxTint != profile.sky.skyboxTint) skyChanged = true;

                if (skyChanged)
                {
                    skyboxMat.SetTexture("_Tex", profile.sky.hdriSkybox);
                    skyboxMat.SetFloat("_Exposure", profile.sky.skyboxExposure);
                    skyboxMat.SetFloat("_Rotation", profile.sky.skyboxRotation);
                    skyboxMat.SetColor("_Tint", profile.sky.skyboxTint);
                    
                    // Force Unity to re-bake the ambient probe ONLY when sky changes
                    DynamicGI.UpdateEnvironment();

                    // Update cache
                    _lastSkybox = profile.sky.hdriSkybox;
                    _lastSkyboxRotation = profile.sky.skyboxRotation;
                    _lastSkyboxExposure = profile.sky.skyboxExposure;
                    _lastSkyboxTint = profile.sky.skyboxTint;
                }
            }
            else
            {
                mainCam.clearFlags = CameraClearFlags.SolidColor;
                mainCam.backgroundColor = Color.black;
                RenderSettings.skybox = null;
            }

            ApplyAurora(mainCam);
            ApplyPrecip(mainCam);
        }

        private void ApplyAurora(Camera mainCam)
        {
            if (profile == null || profile.sky == null || !profile.sky.auroraEnabled || profile.sky.auroraIntensity <= 0.001f)
            {
                if (_auroraRoot != null) _auroraRoot.SetActive(false);
                return;
            }

            EnsureAurora();
            if (_auroraRoot == null || _auroraMaterial == null) return;

            var sky = profile.sky;
            _auroraRoot.SetActive(true);

            // HEIGHT → how high the band sits, WIDTH → horizontal sweep, VERTICAL SIZE →
            // band thickness. Rebuild the arc mesh only when one of these changes.
            float span = Mathf.Lerp(50f, 220f, Mathf.InverseLerp(80f, 900f, sky.auroraWidth));
            float elLow = Mathf.Lerp(2f, 45f, Mathf.InverseLerp(20f, 300f, sky.auroraHeight));
            float elHigh = elLow + Mathf.Lerp(15f, 70f, Mathf.InverseLerp(20f, 320f, sky.auroraVerticalSize));
            if (_auroraFilter != null &&
                (Mathf.Abs(span - _auroraSpan) > 0.5f || Mathf.Abs(elLow - _auroraElLow) > 0.5f || Mathf.Abs(elHigh - _auroraElHigh) > 0.5f))
            {
                _auroraSpan = span; _auroraElLow = elLow; _auroraElHigh = elHigh;
                _auroraFilter.sharedMesh = CreateAuroraMesh(span, elLow, elHigh);
            }

            Vector3 planarForward = mainCam.transform.forward;
            planarForward.y = 0f;
            if (planarForward.sqrMagnitude < 0.0001f)
                planarForward = mainCam.transform.rotation * Vector3.forward;
            planarForward.Normalize();

            // The band is a unit-radius arc centred on the camera. Keep its radius INSIDE
            // the far clip plane — the old code parked the quad at auroraDistance (420m)
            // beyond a 250m far plane, so it was clipped away and never drew regardless of
            // how high intensity was cranked.
            float radius = Mathf.Clamp(sky.auroraDistance, 60f, mainCam.farClipPlane * 0.82f);
            _auroraRoot.transform.position = mainCam.transform.position;
            _auroraRoot.transform.rotation = Quaternion.LookRotation(planarForward, Vector3.up);
            _auroraRoot.transform.localScale = Vector3.one * radius;

            // Hue reflects live state: surveillance on → green/teal (warm-biased by weather),
            // off → dim red. Intensity drives brightness; a slow pulse keeps it alive.
            float intensity = Mathf.Clamp(sky.auroraIntensity, 0f, 2f);
            // Aurora belongs to the night — fade it as the sun climbs above the horizon.
            if (sky.dynamicSky)
            {
                float night = Mathf.Clamp01((2f - _sunAlt) / 8f);
                intensity *= night;
                if (intensity <= 0.001f) { _auroraRoot.SetActive(false); return; }
            }
            float warm = float.IsNaN(WeatherTempF) ? 0.5f : Mathf.Clamp01((WeatherTempF - 35f) / 55f);
            Color onHue = Color.Lerp(new Color(0.30f, 0.95f, 0.88f), new Color(0.58f, 1f, 0.55f), warm);
            Color hue = SurveillanceActive ? onHue : new Color(1f, 0.34f, 0.30f);
            float stateMul = SurveillanceActive ? 1f : 0.45f;
            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 0.7f);

            Color baseCol = hue * ((0.55f + 0.45f * intensity) * stateMul);
            baseCol.a = 1f;
            _auroraMaterial.SetColor("_BaseColor", baseCol);
            _auroraMaterial.SetColor("_Color", baseCol);
            _auroraMaterial.SetColor("_EmissionColor",
                hue * (Mathf.Lerp(0.8f, 3.2f, Mathf.Clamp01(intensity * 0.5f)) * pulse * stateMul));

            // Two layers drift at slightly different rates for a shimmering curtain.
            float t = Time.time * sky.auroraDriftSpeed;
            Vector2 offset = new Vector2(t * 0.03f, Mathf.Sin(t * 0.05f) * 0.015f);
            _auroraMaterial.mainTextureOffset = offset;
            _auroraMaterial.SetTextureOffset("_BaseMap", offset);
        }

        private void EnsureAurora()
        {
            if (_auroraRoot != null) return;

            _auroraRoot = new GameObject("K1L0_AuroraSky");
            _auroraRoot.hideFlags = HideFlags.DontSave;

            _auroraFilter = _auroraRoot.AddComponent<MeshFilter>();
            _auroraFilter.sharedMesh = CreateAuroraMesh(130f, 9f, 54f);

            var meshRenderer = _auroraRoot.AddComponent<MeshRenderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            _auroraMaterial = new Material(shader);
            _auroraMaterial.name = "K1L0_AuroraSky_Runtime";
            _auroraTexture = CreateAuroraTexture();
            _auroraMaterial.mainTexture = _auroraTexture;
            _auroraMaterial.SetTexture("_BaseMap", _auroraTexture);
            ConfigureTransparentMaterial(_auroraMaterial);
            meshRenderer.material = _auroraMaterial;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
        }

        // A curved ribbon spanning a wide arc of the upper sky (unit radius — the runtime
        // scales it to fit inside the far clip). Reads as a sky feature you can pan across,
        // not a flat billboard parked in front of the camera.
        private static Mesh CreateAuroraMesh(float spanDeg, float elLowDeg, float elHighDeg)
        {
            const int seg = 32;             // horizontal segments
            float elLow = elLowDeg * Mathf.Deg2Rad;
            float elHigh = elHighDeg * Mathf.Deg2Rad;

            var verts = new Vector3[(seg + 1) * 2];
            var uvs = new Vector2[(seg + 1) * 2];
            var tris = new int[seg * 6];

            for (int i = 0; i <= seg; i++)
            {
                float tu = i / (float)seg;
                float az = Mathf.Deg2Rad * Mathf.Lerp(-spanDeg * 0.5f, spanDeg * 0.5f, tu);
                Vector3 hdir = new Vector3(Mathf.Sin(az), 0f, Mathf.Cos(az));
                Vector3 bottom = Mathf.Cos(elLow) * hdir + Mathf.Sin(elLow) * Vector3.up;
                Vector3 top = Mathf.Cos(elHigh) * hdir + Mathf.Sin(elHigh) * Vector3.up;
                verts[i * 2 + 0] = bottom.normalized;
                verts[i * 2 + 1] = top.normalized;
                uvs[i * 2 + 0] = new Vector2(tu * 3f, 0f); // repeat curtains horizontally
                uvs[i * 2 + 1] = new Vector2(tu * 3f, 1f);
            }
            for (int i = 0; i < seg; i++)
            {
                int b = i * 2;
                int t = i * 6;
                tris[t + 0] = b; tris[t + 1] = b + 1; tris[t + 2] = b + 2;
                tris[t + 3] = b + 1; tris[t + 4] = b + 3; tris[t + 5] = b + 2;
            }

            Mesh mesh = new Mesh { name = "K1L0_AuroraSkyMesh" };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Texture2D CreateAuroraTexture()
        {
            const int w = 512;
            const int h = 256;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < h; y++)
            {
                float v = y / (float)(h - 1);
                // Soft vertical envelope: fades in from the bottom, tapers out near the top.
                float vertical = Mathf.SmoothStep(0f, 0.35f, v) * (1f - Mathf.SmoothStep(0.6f, 1f, v));
                // Gentle green→teal→violet gradient up the curtain.
                Color low = new Color(0.45f, 1f, 0.62f);
                Color mid = new Color(0.40f, 0.95f, 0.95f);
                Color high = new Color(0.62f, 0.45f, 1f);
                Color grad = v < 0.5f ? Color.Lerp(low, mid, v * 2f) : Color.Lerp(mid, high, (v - 0.5f) * 2f);
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)(w - 1);
                    // Multi-octave vertical curtains for a filamented look.
                    float c1 = Mathf.Sin(u * 26f + Mathf.Sin(u * 6f) * 2.0f);
                    float c2 = Mathf.Sin(u * 61f + Mathf.Sin(u * 13f) * 1.3f);
                    float curtain = Mathf.Pow(Mathf.Clamp01(c1 * 0.5f + 0.5f), 2.4f);
                    curtain *= 0.7f + 0.3f * Mathf.Clamp01(c2 * 0.5f + 0.5f);
                    float a = curtain * vertical;
                    tex.SetPixel(x, y, new Color(grad.r, grad.g, grad.b, a));
                }
            }
            tex.Apply(false, true);
            return tex;
        }

        private static void ConfigureTransparentMaterial(Material mat)
        {
            if (mat == null) return;
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_ZWrite", 0);
            mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.EnableKeyword("_EMISSION");
            mat.renderQueue = 3000;
        }

        // ---------------------------------------------------------------------
        // Dynamic sky: real sun position (time + GPS) + weather, weather-app style.
        // ---------------------------------------------------------------------

        private const float Deg2RadD = 0.01745329251994f;
        private const float Rad2DegD = 57.2957795130823f;

        // Maps the current weather glyph to sky character: cloudiness 0..1, wetness 0..1.
        private static void WeatherFactors(out float cloud, out float wet, out bool snow)
        {
            cloud = 0f; wet = 0f; snow = false;
            string g = GPSLocationController.GPSDisabled ? ManualWeatherGlyph : WeatherGlyph;
            if (string.IsNullOrEmpty(g)) return;
            g = g.ToLowerInvariant();
            if (g.Contains("storm") || g.Contains("thunder")) { cloud = 1f; wet = 1f; }
            else if (g.Contains("rain") || g.Contains("drizzle") || g.Contains("🌧")) { cloud = 0.9f; wet = 0.85f; }
            else if (g.Contains("snow") || g.Contains("❄")) { cloud = 0.85f; wet = 0.5f; snow = true; }
            else if (g.Contains("fog") || g.Contains("mist") || g.Contains("🌫")) { cloud = 0.8f; wet = 0.3f; }
            else if (g.Contains("overcast")) { cloud = 0.95f; }
            else if (g.Contains("partly")) { cloud = 0.45f; }
            else if (g.Contains("cloud") || g.Contains("☁") || g.Contains("⛅")) { cloud = 0.7f; }
            // sun/clear → 0
        }

        private bool GetLatLon(out double lat, out double lon)
        {
            if (Input.location.status == LocationServiceStatus.Running)
            {
                lat = Input.location.lastData.latitude;
                lon = Input.location.lastData.longitude;
                if (lat != 0.0 || lon != 0.0) return true;
            }
            if (profile != null && profile.startupLocation != null)
            {
                profile.startupLocation.GetStartupCoordinates(out lat, out lon);
                return true;
            }
            lat = 40.7; lon = -74.0;
            return true;
        }

        // Sun altitude/azimuth (degrees) for a lat/lon at a UTC instant. Compact NOAA-style
        // approximation — accurate to a small fraction of a degree, ample for sky colour.
        private static void SunAltAz(double latDeg, double lonDeg, System.DateTime utc, out double altDeg, out double azDeg)
        {
            double dayFraction = (utc.Hour + utc.Minute / 60.0 + utc.Second / 3600.0) / 24.0;
            double Y = utc.Year, M = utc.Month, D = utc.Day + dayFraction;
            if (M <= 2) { Y -= 1; M += 12; }
            double A = System.Math.Floor(Y / 100.0);
            double B = 2 - A + System.Math.Floor(A / 4.0);
            double JD = System.Math.Floor(365.25 * (Y + 4716)) + System.Math.Floor(30.6001 * (M + 1)) + D + B - 1524.5;
            double n = JD - 2451545.0;

            double L = (280.460 + 0.9856474 * n) % 360.0; if (L < 0) L += 360;
            double g = (357.528 + 0.9856003 * n) % 360.0; if (g < 0) g += 360;
            double gR = g * Deg2RadD;
            double lambda = (L + 1.915 * System.Math.Sin(gR) + 0.020 * System.Math.Sin(2 * gR)) * Deg2RadD;
            double eps = (23.439 - 0.0000004 * n) * Deg2RadD;
            double delta = System.Math.Asin(System.Math.Sin(eps) * System.Math.Sin(lambda));
            double alpha = System.Math.Atan2(System.Math.Cos(eps) * System.Math.Sin(lambda), System.Math.Cos(lambda));
            double gmst = (280.46061837 + 360.98564736629 * n) % 360.0; if (gmst < 0) gmst += 360;
            double H = ((gmst + lonDeg) - alpha * Rad2DegD) % 360.0;
            double Hr = H * Deg2RadD;
            double latR = latDeg * Deg2RadD;
            double alt = System.Math.Asin(System.Math.Sin(latR) * System.Math.Sin(delta) +
                                          System.Math.Cos(latR) * System.Math.Cos(delta) * System.Math.Cos(Hr));
            double az = System.Math.Atan2(-System.Math.Sin(Hr),
                                          System.Math.Tan(delta) * System.Math.Cos(latR) - System.Math.Sin(latR) * System.Math.Cos(Hr));
            altDeg = alt * Rad2DegD;
            azDeg = (az * Rad2DegD + 360.0) % 360.0;
        }

        private float _sunAlt = 30f;

        private Vector3 CurrentSunDirection(out float altDeg)
        {
            // GPS off → no live location/time, so the sun follows the manual hour slider:
            // a simple arc (0° at 6h/18h, +60° at noon, below the horizon at night).
            if (GPSLocationController.GPSDisabled)
            {
                float hr = Mathf.Repeat(ManualHour, 24f);
                float dayAngle = (hr - 6f) / 12f * Mathf.PI;
                altDeg = 60f * Mathf.Sin(dayAngle);
                float azM = Mathf.Lerp(70f, 290f, Mathf.Clamp01(hr / 24f));
                float aR = altDeg * Deg2RadD, zR = azM * Deg2RadD;
                return new Vector3(Mathf.Sin(zR) * Mathf.Cos(aR), Mathf.Sin(aR), Mathf.Cos(zR) * Mathf.Cos(aR));
            }

            GetLatLon(out double lat, out double lon);
            SunAltAz(lat, lon, System.DateTime.UtcNow, out double alt, out double az);
            altDeg = (float)alt;
            float altR = (float)(alt * Deg2RadD), azR = (float)(az * Deg2RadD);
            // az from north, clockwise. World +Z≈north, +X≈east, +Y up.
            return new Vector3(Mathf.Sin(azR) * Mathf.Cos(altR), Mathf.Sin(altR), Mathf.Cos(azR) * Mathf.Cos(altR));
        }

        private void DriveSunLight(Light light)
        {
            Vector3 toSun = CurrentSunDirection(out float alt);
            _sunAlt = alt;
            // Keep the disc from sitting exactly on the horizon (avoids a hard flicker).
            light.transform.rotation = Quaternion.LookRotation(-toSun.normalized, Vector3.up);

            WeatherFactors(out float cloud, out float wet, out bool snow);

            // Colour: warm at low sun, white high, cool moonlight below the horizon.
            Color horizonWarm = new Color(1f, 0.62f, 0.36f);
            Color midday = new Color(1f, 0.97f, 0.92f);
            Color day = Color.Lerp(horizonWarm, midday, Mathf.Clamp01(alt / 22f));
            Color moon = profile.lighting != null ? profile.lighting.moonlightColor : new Color(0.7f, 0.8f, 1f);
            Color col = Color.Lerp(moon, day, Mathf.Clamp01((alt + 4f) / 9f));
            // Overcast desaturates and greys the key light.
            float grey = (col.r + col.g + col.b) / 3f;
            col = Color.Lerp(col, new Color(grey, grey, grey), cloud * 0.6f);
            light.color = col;

            float dayInt = Mathf.Lerp(0.35f, 1.2f, Mathf.Clamp01((alt + 6f) / 30f));
            light.intensity = dayInt * (1f - 0.5f * cloud);
        }

        private void ApplyProceduralSky()
        {
            float alt = _sunAlt;
            WeatherFactors(out float cloud, out float wet, out bool snow);

            // At night, use the authored star/moon cubemap (the profile's hdriSkybox) — the
            // real night sky. Day/dawn/dusk use the sun-driven procedural scattering sky.
            // Switch once the sun is below civil twilight, where the procedural sky is
            // already near-dark, so the handoff is soft.
            if (alt < -4f && profile.sky != null && profile.sky.hdriSkybox != null)
            {
                if (_nightCubeSky == null || _nightCubeSky.shader == null)
                {
                    Shader cs = Shader.Find("Skybox/Cubemap");
                    if (cs != null) _nightCubeSky = new Material(cs) { name = "K1L0_NightSky" };
                }
                if (_nightCubeSky != null)
                {
                    if (_nightCubeSky.GetTexture("_Tex") != profile.sky.hdriSkybox)
                        _nightCubeSky.SetTexture("_Tex", profile.sky.hdriSkybox);
                    if (RenderSettings.skybox != _nightCubeSky)
                        RenderSettings.skybox = _nightCubeSky;

                    float deepNight = Mathf.Clamp01((-4f - alt) / 6f);       // 0 at -4°, 1 by -10°
                    // Skybox/Cubemap tint of 0.5 grey is neutral; overcast greys/dims it.
                    float t = Mathf.Lerp(0.5f, 0.34f, cloud);
                    _nightCubeSky.SetColor("_Tint", new Color(t, t, t, 1f));
                    _nightCubeSky.SetFloat("_Exposure", Mathf.Lerp(0.7f, 1.05f, deepNight) * (1f - 0.35f * cloud));
                    _nightCubeSky.SetFloat("_Rotation", profile.sky.skyboxRotation);

                    if (RenderSettings.ambientMode != AmbientMode.Skybox)
                        RenderSettings.ambientMode = AmbientMode.Skybox;
                    RenderSettings.ambientIntensity = 0.22f;
                    if (Mathf.Abs(alt - _lastGiSunAlt) > 1.5f || Time.time - _lastGiTime > 20f)
                    {
                        _lastGiSunAlt = alt; _lastGiTime = Time.time;
                        DynamicGI.UpdateEnvironment();
                    }
                    return;
                }
            }

            // Built-in procedural skybox does real atmospheric scattering driven by the sun
            // (RenderSettings.sun), so sunrise/sunset/day come for free. We modulate
            // its tint/exposure/thickness by weather.
            if (_proceduralSky == null || _proceduralSky.shader == null)
            {
                Shader s = Shader.Find("Skybox/Procedural");
                if (s == null) return;
                _proceduralSky = new Material(s) { name = "K1L0_DynamicSky" };
            }
            if (RenderSettings.skybox != _proceduralSky)
                RenderSettings.skybox = _proceduralSky;

            // Night darkens everything; twilight keeps a glow near the horizon.
            float dayness = Mathf.Clamp01((alt + 6f) / 14f);   // 0 deep night .. 1 day
            float baseExposure = profile.sky != null ? profile.sky.dynamicSkyExposure : 1.15f;

            // Clear sky tint shifts from a warm twilight to daytime blue; weather greys it.
            Color clearTint = Color.Lerp(new Color(0.55f, 0.45f, 0.6f), new Color(0.5f, 0.62f, 0.95f), dayness);
            Color overcastTint = snow ? new Color(0.78f, 0.80f, 0.86f) : new Color(0.55f, 0.57f, 0.6f);
            Color skyTint = Color.Lerp(clearTint, overcastTint, cloud);

            float exposure = baseExposure * Mathf.Lerp(0.18f, 1f, dayness) * Mathf.Lerp(1f, 0.55f, cloud * (1f - 0.3f * (snow ? 1f : 0f)));
            float atmThickness = Mathf.Lerp(1.0f, 0.4f, cloud) * Mathf.Lerp(1.3f, 1.0f, dayness); // thicker → redder near horizon
            float sunSize = Mathf.Lerp(0.04f, 0.005f, cloud); // hide the disc under cloud

            _proceduralSky.SetColor("_SkyTint", skyTint);
            _proceduralSky.SetColor("_GroundColor", new Color(0.06f, 0.07f, 0.08f));
            _proceduralSky.SetFloat("_AtmosphereThickness", atmThickness);
            _proceduralSky.SetFloat("_Exposure", Mathf.Max(0.05f, exposure));
            _proceduralSky.SetFloat("_SunSize", sunSize);

            // Ambient follows the sky, but baking GI every frame is expensive — refresh only
            // when the sun has moved a little or enough time passed.
            if (RenderSettings.ambientMode != AmbientMode.Skybox)
                RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = Mathf.Lerp(0.25f, 1f, dayness);
            if (Mathf.Abs(alt - _lastGiSunAlt) > 0.75f || Time.time - _lastGiTime > 12f)
            {
                _lastGiSunAlt = alt;
                _lastGiTime = Time.time;
                DynamicGI.UpdateEnvironment();
            }
        }

        // ---------------------------------------------------------------------
        // Precipitation: rain / snow particles, driven by the active weather.
        // ---------------------------------------------------------------------
        private GameObject _precipRoot;
        private ParticleSystem _precip;
        private int _precipMode = -1;   // -1 unset, 0 none, 1 rain, 2 snow

        private void ApplyPrecip(Camera cam)
        {
            WeatherFactors(out float cloud, out float wet, out bool snow);
            int mode = snow ? 2 : (wet > 0.45f ? 1 : 0);

            EnsurePrecip(cam);
            if (_precip == null) return;

            if (mode != _precipMode)
            {
                _precipMode = mode;
                ConfigurePrecip(mode);
            }

            var em = _precip.emission;
            if (mode == 0)
            {
                em.rateOverTime = 0f;
                if (_precip.isEmitting) _precip.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                return;
            }
            em.rateOverTime = mode == 1 ? Mathf.Lerp(280f, 1100f, wet) : Mathf.Lerp(120f, 460f, Mathf.Max(cloud, wet));
            if (!_precip.isPlaying) _precip.Play();
        }

        private void EnsurePrecip(Camera cam)
        {
            if (_precipRoot != null) return;
            if (cam == null) return;

            _precipRoot = new GameObject("K1L0_Precip");
            _precipRoot.hideFlags = HideFlags.DontSave;
            _precipRoot.transform.SetParent(cam.transform, false);
            _precipRoot.transform.localPosition = new Vector3(0f, 16f, 2f);
            _precipRoot.transform.localRotation = Quaternion.identity;

            _precip = _precipRoot.AddComponent<ParticleSystem>();
            var main = _precip.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World; // fall through the world, not with the camera
            main.maxParticles = 2500;
            main.playOnAwake = false;

            var shape = _precip.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(46f, 1f, 46f);

            var renderer = _precip.GetComponent<ParticleSystemRenderer>();
            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            var mat = new Material(sh) { name = "K1L0_PrecipMat" };
            var tex = MakePrecipDot(32);
            mat.SetTexture("_BaseMap", tex);
            mat.SetTexture("_MainTex", tex);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = 3100;
            renderer.material = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            _precip.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private void ConfigurePrecip(int mode)
        {
            if (_precip == null) return;
            var main = _precip.main;
            var renderer = _precip.GetComponent<ParticleSystemRenderer>();
            var noise = _precip.noise;
            var col = _precip.colorOverLifetime; col.enabled = false;

            if (mode == 1) // rain
            {
                main.startLifetime = 1.1f;
                main.startSpeed = 0f;
                main.gravityModifier = 4.5f;
                main.startSize = new ParticleSystem.MinMaxCurve(0.045f, 0.09f);
                main.startColor = new Color(0.62f, 0.72f, 0.9f, 0.55f);
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.lengthScale = 0.22f;
                renderer.velocityScale = 0.13f;
                noise.enabled = false;
            }
            else if (mode == 2) // snow
            {
                main.startLifetime = 7f;
                main.startSpeed = 0f;
                main.gravityModifier = 0.22f;
                main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.13f);
                main.startColor = new Color(1f, 1f, 1f, 0.9f);
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.lengthScale = 1f;
                renderer.velocityScale = 0f;
                noise.enabled = true;
                noise.strength = 0.7f;
                noise.frequency = 0.25f;
                noise.scrollSpeed = 0.35f;
            }
        }

        private static Texture2D MakePrecipDot(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            Vector2 c = new Vector2(size * 0.5f, size * 0.5f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c) / (size * 0.5f);
                    float a = Mathf.Clamp01(1f - d);
                    a = Mathf.Pow(a, 1.6f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            return tex;
        }

        private void ApplyPostFX()
        {
            if (globalVolume == null)
            {
                globalVolume = GetComponent<Volume>();
                if (globalVolume == null)
                {
                    // TEMP: Disable expensive FindObjectsOfType in editor (causes freeze)
#if !UNITY_EDITOR
                    // Try find global volume (once)
                    var volumes = FindObjectsOfType<Volume>();
                    foreach(var v in volumes) { if(v.isGlobal) { globalVolume = v; break; } }
#endif
                }
            }

            if (globalVolume == null || globalVolume.profile == null) return;
            EnsurePostFXVolume();
            EnsureCameraPostProcessing();

            // --- Bloom ---
            if (_bloom == null) globalVolume.profile.TryGet(out _bloom);
            if (_bloom == null) _bloom = globalVolume.profile.Add<Bloom>(true);

            _bloom.active = profile.postFX.bloomEnabled;
            _bloom.intensity.overrideState = true;
            _bloom.intensity.value = Mathf.Max(0f, profile.postFX.bloomIntensity);
            _bloom.threshold.overrideState = true;
            _bloom.threshold.value = Mathf.Max(0f, profile.postFX.bloomThreshold);
            _bloom.scatter.overrideState = true;
            _bloom.scatter.value = Mathf.Clamp01(profile.postFX.bloomScatter);
            _bloom.tint.overrideState = true;
            _bloom.tint.value = profile.postFX.bloomTint;

            // Lens Dirt (subtle overlay)
            if (profile.postFX.lensDirtTexture != null)
            {
                _bloom.dirtTexture.overrideState = true;
                _bloom.dirtTexture.value = profile.postFX.lensDirtTexture;
                _bloom.dirtIntensity.overrideState = true;
                _bloom.dirtIntensity.value = profile.postFX.lensDirtIntensity;
            }
            else
            {
                _bloom.dirtTexture.overrideState = false;
                _bloom.dirtIntensity.overrideState = false;
            }

            // High quality filtering for better bloom
            _bloom.highQualityFiltering.overrideState = true;
            _bloom.highQualityFiltering.value = true;

            // Note: URP Bloom uses threshold to clamp emissive (bloomClampBeforeBloom)
            // For stronger clamp control, adjust threshold parameter
            // Note: Anamorphic bloom requires custom shader (not natively supported in URP)

            // --- Vignette ---
            if (_vignette == null) globalVolume.profile.TryGet(out _vignette);
            if (_vignette == null) _vignette = globalVolume.profile.Add<Vignette>(true);

            _vignette.active = profile.postFX.vignetteEnabled;
            _vignette.color.overrideState = true; _vignette.color.value = profile.postFX.vignetteColor;
            _vignette.intensity.overrideState = true; _vignette.intensity.value = profile.postFX.vignetteIntensity;
            _vignette.smoothness.overrideState = true; _vignette.smoothness.value = profile.postFX.vignetteSmoothness;
            _vignette.rounded.overrideState = true; _vignette.rounded.value = profile.postFX.vignetteRounded;

            // --- Chromatic Aberration ---
            if (_chromaticAberration == null) globalVolume.profile.TryGet(out _chromaticAberration);
            if (_chromaticAberration == null) _chromaticAberration = globalVolume.profile.Add<ChromaticAberration>(true);

            _chromaticAberration.active = profile.postFX.chromaticAberrationEnabled;
            _chromaticAberration.intensity.overrideState = true;
            _chromaticAberration.intensity.value = profile.postFX.chromaticAberrationIntensity;

            // --- Tonemapping ---
            if (_tonemapping == null) globalVolume.profile.TryGet(out _tonemapping);
            if (_tonemapping == null) _tonemapping = globalVolume.profile.Add<Tonemapping>(true);

            _tonemapping.active = profile.postFX.tonemappingEnabled;
            _tonemapping.mode.overrideState = true;
            _tonemapping.mode.value = profile.postFX.tonemappingMode;
            
            // --- Lens Distortion ---
            if (_lensDistortion == null) globalVolume.profile.TryGet(out _lensDistortion);
            if (_lensDistortion == null) _lensDistortion = globalVolume.profile.Add<LensDistortion>(true);
            
            _lensDistortion.active = profile.postFX.lensDistortionEnabled;
            _lensDistortion.intensity.overrideState = true; _lensDistortion.intensity.value = profile.postFX.lensDistortionIntensity;
            _lensDistortion.xMultiplier.overrideState = true; _lensDistortion.xMultiplier.value = profile.postFX.lensDistortionXMultiplier;
            _lensDistortion.yMultiplier.overrideState = true; _lensDistortion.yMultiplier.value = profile.postFX.lensDistortionYMultiplier;
            _lensDistortion.scale.overrideState = true; _lensDistortion.scale.value = profile.postFX.lensDistortionScale;

            // --- Color Grading (ColorAdjustments) ---
            if (_colorAdjustments == null) globalVolume.profile.TryGet(out _colorAdjustments);
            if (_colorAdjustments == null) _colorAdjustments = globalVolume.profile.Add<ColorAdjustments>(true);

            bool colorGradingActive = profile.postFX.colorGradingEnabled ||
                                      Mathf.Abs(profile.postFX.hueShift) > 0.001f ||
                                      Mathf.Abs(profile.postFX.saturation) > 0.001f ||
                                      Mathf.Abs(profile.postFX.contrast) > 0.001f;
            _colorAdjustments.active = colorGradingActive;
            _colorAdjustments.postExposure.overrideState = true;
            _colorAdjustments.postExposure.value = profile.postFX.exposureEnabled ? profile.postFX.exposureFixedValue : 0f;

            // --- Depth of Field ---
            if (_depthOfField == null) globalVolume.profile.TryGet(out _depthOfField);
            if (_depthOfField == null) _depthOfField = globalVolume.profile.Add<DepthOfField>(true);
            
            _depthOfField.active = profile.postFX.depthOfFieldEnabled;
            _depthOfField.mode.overrideState = true; 
            _depthOfField.mode.value = DepthOfFieldMode.Bokeh;
            
            _depthOfField.focusDistance.overrideState = true; 
            _depthOfField.focusDistance.value = profile.postFX.focusDistance;
            
            _depthOfField.focalLength.overrideState = true; 
            _depthOfField.focalLength.value = profile.postFX.focalLength;
            
            _depthOfField.aperture.overrideState = true; 
            _depthOfField.aperture.value = profile.postFX.aperture;

            // Bokeh aesthetics
            _depthOfField.bladeCount.overrideState = true; _depthOfField.bladeCount.value = 5;
            _depthOfField.bladeCurvature.overrideState = true; _depthOfField.bladeCurvature.value = 1f;
            _depthOfField.bladeRotation.overrideState = true; _depthOfField.bladeRotation.value = 0f;
            
            // --- Color Grading (White Balance) ---
            if (_whiteBalance == null) globalVolume.profile.TryGet(out _whiteBalance);
            if (_whiteBalance == null) _whiteBalance = globalVolume.profile.Add<WhiteBalance>(true);
            
            _whiteBalance.active = profile.postFX.whiteBalanceActive;
            _whiteBalance.temperature.overrideState = profile.postFX.temperatureOverride;
            _whiteBalance.temperature.value = profile.postFX.temperature;
            _whiteBalance.tint.overrideState = profile.postFX.tintOverride;
            _whiteBalance.tint.value = profile.postFX.tint;
            
            _colorAdjustments.hueShift.overrideState = true; _colorAdjustments.hueShift.value = profile.postFX.hueShift;
            _colorAdjustments.saturation.overrideState = true; _colorAdjustments.saturation.value = profile.postFX.saturation;
            _colorAdjustments.contrast.overrideState = true; _colorAdjustments.contrast.value = profile.postFX.contrast;
            
            // --- Color Grading (Shadows/Midtones/Highlights) ---
            if (_splitToning == null) globalVolume.profile.TryGet(out _splitToning);
            if (_splitToning == null) _splitToning = globalVolume.profile.Add<ShadowsMidtonesHighlights>(true);
            
            _splitToning.active = profile.postFX.colorGradingEnabled;
            _splitToning.shadows.overrideState = profile.postFX.shadowsOverride; _splitToning.shadows.value = profile.postFX.shadows;
            _splitToning.midtones.overrideState = profile.postFX.midtonesOverride; _splitToning.midtones.value = profile.postFX.midtones;
            _splitToning.highlights.overrideState = profile.postFX.highlightsOverride; _splitToning.highlights.value = profile.postFX.highlights;
            _splitToning.shadowsStart.overrideState = profile.postFX.shadowsStartOverride; _splitToning.shadowsStart.value = profile.postFX.shadowsStart;
            _splitToning.shadowsEnd.overrideState = profile.postFX.shadowsEndOverride; _splitToning.shadowsEnd.value = profile.postFX.shadowsEnd;
            _splitToning.highlightsStart.overrideState = profile.postFX.highlightsStartOverride; _splitToning.highlightsStart.value = profile.postFX.highlightsStart;
            _splitToning.highlightsEnd.overrideState = profile.postFX.highlightsEndOverride; _splitToning.highlightsEnd.value = profile.postFX.highlightsEnd;
            
            // --- Motion Blur ---
            if (_motionBlur == null) globalVolume.profile.TryGet(out _motionBlur);
            if (_motionBlur == null) _motionBlur = globalVolume.profile.Add<MotionBlur>(true);
            
            _motionBlur.active = profile.postFX.motionBlurEnabled;
            _motionBlur.quality.overrideState = true; _motionBlur.quality.value = profile.postFX.motionBlurQuality;
            _motionBlur.intensity.overrideState = true; _motionBlur.intensity.value = profile.postFX.motionBlurIntensity;
            _motionBlur.clamp.overrideState = true; _motionBlur.clamp.value = 0.05f;

            // --- Film Grain ---
            if (_filmGrain == null) globalVolume.profile.TryGet(out _filmGrain);
            if (_filmGrain == null) _filmGrain = globalVolume.profile.Add<FilmGrain>(true);
            
            _filmGrain.active = profile.postFX.filmGrainEnabled;
            _filmGrain.type.overrideState = true; _filmGrain.type.value = profile.postFX.filmGrainType;
            _filmGrain.intensity.overrideState = true; _filmGrain.intensity.value = profile.postFX.filmGrainIntensity;
            _filmGrain.response.overrideState = true; _filmGrain.response.value = profile.postFX.filmGrainResponse;
        }

        private void EnsurePostFXVolume()
        {
            if (globalVolume == null) return;

            globalVolume.enabled = true;
            globalVolume.isGlobal = true;
            globalVolume.weight = 1f;
            if (globalVolume.priority < 1000f)
                globalVolume.priority = 1000f;
        }

        private void EnsureCameraPostProcessing()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null) return;
            mainCamera.allowHDR = true;

            UniversalAdditionalCameraData cameraData = mainCamera.GetUniversalAdditionalCameraData();
            if (cameraData == null) return;

            cameraData.renderPostProcessing = true;
            if (globalVolume != null)
                cameraData.volumeLayerMask |= 1 << globalVolume.gameObject.layer;
        }

        private void ApplyVolumetricFog()
        {
            var fog = profile.volumetricFog;

            if (_cachedVolumetricFogManager == null)
            {
                _cachedVolumetricFogManager = FindObjectOfType<VolumetricFogManager>();
            }

            if (_cachedVolumetricFogManager != null)
            {
                float specularBoost = Mathf.Max(0f, fog.specularIntensity);
                float thresholdBias = Mathf.Clamp01(fog.specularThreshold);
                Color scatteringTint = new Color(
                    fog.scatteringTint.r * fog.specularColor.r,
                    fog.scatteringTint.g * fog.specularColor.g,
                    fog.scatteringTint.b * fog.specularColor.b,
                    1f
                );

                _cachedVolumetricFogManager.scattering = Mathf.Clamp01(Mathf.Max(fog.scattering, specularBoost * 0.15f));
                _cachedVolumetricFogManager.scatteringThreshold = Mathf.Max(0f, fog.scatteringThreshold - thresholdBias * 0.6f);
                _cachedVolumetricFogManager.scatteringIntensity = Mathf.Max(0f, fog.scatteringIntensity + specularBoost * 2.5f);
                _cachedVolumetricFogManager.scatteringAbsorption = fog.scatteringAbsorption;
                _cachedVolumetricFogManager.scatteringTint = scatteringTint;
                _cachedVolumetricFogManager.scatteringHighQuality = fog.scatteringHighQuality;
            }

            // Find the VolumetricFog component if not cached
            if (_cachedVolumetricFog == null)
            {
                _cachedVolumetricFog = FindObjectOfType<VolumetricFog>();
                if (_cachedVolumetricFog == null)
                {
                    // No volumetric fog in the scene, skip
                    return;
                }
            }

            // Ensure fog has a profile to update
            if (_cachedVolumetricFog.profile == null)
            {
                Debug.LogWarning("[RenderManager] VolumetricFog found but has no profile assigned!");
                return;
            }

            // Apply settings from our profile to the fog profile
            var fogProfile = _cachedVolumetricFog.profile;

            // Rendering Quality
            fogProfile.raymarchQuality = fog.raymarchQuality;
            fogProfile.raymarchNearStepping = fog.raymarchNearStepping;
            fogProfile.raymarchMinStep = fog.raymarchMinStep;
            fogProfile.jittering = fog.jittering;
            fogProfile.dithering = fog.dithering;

            // Density & Appearance
            fogProfile.constantDensity = fog.constantDensity;
            fogProfile.noiseStrength = fog.noiseStrength;
            fogProfile.noiseScale = fog.noiseScale;
            fogProfile.noiseFinalMultiplier = fog.noiseFinalMultiplier;
            fogProfile.density = fog.density;

            // Colors
            fogProfile.albedo = fog.albedo;
            fogProfile.brightness = fog.brightness;
            fogProfile.deepObscurance = fog.deepObscurance;
            fogProfile.specularColor = fog.specularColor;
            fogProfile.specularThreshold = fog.specularThreshold;
            fogProfile.specularIntensity = fog.specularIntensity;

            // Animation
            fogProfile.turbulence = fog.turbulence;
            fogProfile.windDirection = fog.windDirection;

            // Directional Light
            fogProfile.lightDiffusionPower = fog.lightDiffusionPower;
            fogProfile.lightDiffusionIntensity = fog.lightDiffusionIntensity;
            fogProfile.receiveShadows = fog.receiveShadows;
            fogProfile.shadowIntensity = fog.shadowIntensity;

            // Light Interaction (Point/Spot)
            _cachedVolumetricFog.enableNativeLights = fog.enableNativeLights;
            _cachedVolumetricFog.nativeLightsMultiplier = fog.nativeLightsMultiplier;
            _cachedVolumetricFog.enablePointLights = fog.enablePointLights;
            _cachedVolumetricFog.enableVoids = fog.enableVoids;
            _cachedVolumetricFog.enableAPV = fog.enableAPV;
            _cachedVolumetricFog.apvIntensityMultiplier = fog.apvIntensityMultiplier;

            // Geometry
            fogProfile.border = fog.border;
            fogProfile.customHeight = fog.customHeight;
            fogProfile.height = fog.height;
            fogProfile.verticalOffset = fog.verticalOffset;
            fogProfile.distance = fog.distance;
            fogProfile.distanceFallOff = fog.distanceFallOff;
            fogProfile.maxDistance = fog.maxDistance;
            fogProfile.maxDistanceFallOff = fog.maxDistanceFallOff;

            // Distant Fog
            fogProfile.distantFog = fog.distantFog;
            fogProfile.distantFogStartDistance = fog.distantFogStartDistance;
            fogProfile.distantFogDistanceDensity = fog.distantFogDistanceDensity;
            fogProfile.distantFogMaxHeight = fog.distantFogMaxHeight;
            fogProfile.distantFogHeightDensity = fog.distantFogHeightDensity;
            fogProfile.distantFogColor = fog.distantFogColor;
            fogProfile.distantFogDiffusionIntensity = fog.distantFogDiffusionIntensity;
            fogProfile.distantFogBaseAltitude = fog.distantFogBaseAltitude;
            fogProfile.distantFogSymmetrical = fog.distantFogSymmetrical;

            // Trigger the fog to update with new settings
            _cachedVolumetricFog.UpdateMaterialPropertiesNow();
        }

        private void ApplyMaterials()
        {
            // Road Material - Apply to BOTH the shared material AND all runtime instances
            if (profile.roads.roadMaterial != null)
            {
                // Apply to shared material first
                ApplyRoadMaterialSettings(profile.roads.roadMaterial);

                // Update cache if needed (every 2 seconds) or if cache is empty
                if (_cachedRoadRenderers == null || Time.time - _lastRoadCacheTime > 2f)
                {
                    GameObject runtimeRoot = GameObject.Find("RuntimeObjectsRoot");
                    if (runtimeRoot != null)
                    {
                        Transform roadLayer = runtimeRoot.transform.Find("road layer objects");
                        if (roadLayer != null)
                        {
                            // TEMP: Disable expensive GetComponentsInChildren in editor (causes freeze)
                            // This searches ALL MeshRenderers in the scene tree - very expensive!
#if !UNITY_EDITOR
                            _cachedRoadRenderers = roadLayer.GetComponentsInChildren<MeshRenderer>(true);
                            _lastRoadCacheTime = Time.time;
#else
                            // In editor, use empty array to prevent freeze
                            _cachedRoadRenderers = new MeshRenderer[0];
                            _lastRoadCacheTime = Time.time;
#endif
                        }
                    }
                }

                // Apply to cached instances
                if (_cachedRoadRenderers != null)
                {
                    foreach (var renderer in _cachedRoadRenderers)
                    {
                        if (renderer == null) continue;
                        
                        // Check shared material to avoid creating instances unnecessarily if not a road
                        if (renderer.sharedMaterial != null && renderer.sharedMaterial.name.Contains("KiloverseRoads"))
                        {
                            ApplyRoadMaterialSettings(renderer.material); // Use .material to get/create instance
                        }
                    }
                }
            }

            // Zoss Wall Material (building walls)
            if (profile.buildings.zossWallMaterial != null)
            {
                // FORCE URP LIT SHADER to guarantee depth writing
                if (profile.buildings.zossWallMaterial.shader.name != "Universal Render Pipeline/Lit")
                {
                    Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
                    if (urpLit != null)
                    {
                        profile.buildings.zossWallMaterial.shader = urpLit;
                    }
                }

                profile.buildings.zossWallMaterial.SetColor("_BaseColor", profile.buildings.zossWallColor);
                profile.buildings.zossWallMaterial.SetFloat("_Metallic", profile.buildings.zossWallMetallic);
                profile.buildings.zossWallMaterial.SetFloat("_Smoothness", profile.buildings.zossWallSmoothness);

                // Albedo
                profile.buildings.zossWallMaterial.SetTexture("_BaseMap", profile.buildings.zossWallAlbedo);

                // Normal Map
                if (profile.buildings.zossWallNormal != null)
                {
                    profile.buildings.zossWallMaterial.SetTexture("_BumpMap", profile.buildings.zossWallNormal);
                    profile.buildings.zossWallMaterial.EnableKeyword("_NORMALMAP");
                    profile.buildings.zossWallMaterial.SetFloat("_BumpScale", profile.buildings.zossWallNormalStrength);
                }
                else
                {
                    profile.buildings.zossWallMaterial.SetTexture("_BumpMap", null);
                    profile.buildings.zossWallMaterial.DisableKeyword("_NORMALMAP");
                }

                // Emission (HDR)
                Color hdrEmission = profile.buildings.zossWallEmission * profile.buildings.zossWallEmissionIntensity;
                profile.buildings.zossWallMaterial.SetColor("_EmissionColor", hdrEmission);
                profile.buildings.zossWallMaterial.SetTexture("_EmissionMap", profile.buildings.zossWallEmissionMap);

                if (profile.buildings.zossWallEmissionIntensity > 0 || profile.buildings.zossWallEmissionMap != null)
                {
                    profile.buildings.zossWallMaterial.EnableKeyword("_EMISSION");
                    profile.buildings.zossWallMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                else
                {
                    profile.buildings.zossWallMaterial.DisableKeyword("_EMISSION");
                }

                // Occlusion Map
                if (profile.buildings.zossWallOcclusionMap != null)
                {
                    profile.buildings.zossWallMaterial.SetTexture("_OcclusionMap", profile.buildings.zossWallOcclusionMap);
                    profile.buildings.zossWallMaterial.SetFloat("_OcclusionStrength", profile.buildings.zossWallOcclusionStrength);
                }
                else
                {
                    profile.buildings.zossWallMaterial.SetTexture("_OcclusionMap", null);
                }

                // Tiling (set main texture tiling - URP uses this for all textures sharing UV0)
                profile.buildings.zossWallMaterial.mainTextureScale = profile.buildings.zossWallTiling;
                profile.buildings.zossWallMaterial.mainTextureOffset = Vector2.zero;

                profile.buildings.zossWallMaterial.SetTextureScale("_BaseMap", profile.buildings.zossWallTiling);
                profile.buildings.zossWallMaterial.SetTextureOffset("_BaseMap", Vector2.zero);

                profile.buildings.zossWallMaterial.SetTextureScale("_BumpMap", profile.buildings.zossWallTiling);
                profile.buildings.zossWallMaterial.SetTextureOffset("_BumpMap", Vector2.zero);

                profile.buildings.zossWallMaterial.SetTextureScale("_EmissionMap", profile.buildings.zossWallTiling);
                profile.buildings.zossWallMaterial.SetTextureOffset("_EmissionMap", Vector2.zero);

                profile.buildings.zossWallMaterial.SetTextureScale("_OcclusionMap", profile.buildings.zossWallTiling);
                profile.buildings.zossWallMaterial.SetTextureOffset("_OcclusionMap", Vector2.zero);


                // Environment Reflections
                if (profile.buildings.zossWallEnvironmentReflections)
                {
                    profile.buildings.zossWallMaterial.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                    profile.buildings.zossWallMaterial.SetFloat("_EnvironmentReflections", 1.0f);
                }
                else
                {
                    profile.buildings.zossWallMaterial.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                    profile.buildings.zossWallMaterial.SetFloat("_EnvironmentReflections", 0.0f);
                }

                // Specular Highlights
                if (profile.buildings.zossWallSpecularHighlights)
                {
                    profile.buildings.zossWallMaterial.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
                    profile.buildings.zossWallMaterial.SetFloat("_SpecularHighlights", 1.0f);
                }
                else
                {
                    profile.buildings.zossWallMaterial.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                    profile.buildings.zossWallMaterial.SetFloat("_SpecularHighlights", 0.0f);
                }

                // FORCE OPAQUE settings
                profile.buildings.zossWallMaterial.SetFloat("_Surface", 0.0f);
                profile.buildings.zossWallMaterial.SetFloat("_Blend", 0.0f);
                profile.buildings.zossWallMaterial.SetFloat("_ZWrite", 1.0f);
                profile.buildings.zossWallMaterial.renderQueue = 2000;
                profile.buildings.zossWallMaterial.SetOverrideTag("RenderType", "Opaque");
            }

            // Zoss Emissive Material (window lights)
            if (profile.buildings.zossEmissiveMaterial != null)
            {
                // FORCE URP LIT SHADER
                if (profile.buildings.zossEmissiveMaterial.shader.name != "Universal Render Pipeline/Lit")
                {
                    Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
                    if (urpLit != null) profile.buildings.zossEmissiveMaterial.shader = urpLit;
                }

                profile.buildings.zossEmissiveMaterial.SetColor("_BaseColor", profile.buildings.zossEmissiveColor);
                profile.buildings.zossEmissiveMaterial.SetFloat("_Metallic", profile.buildings.zossEmissiveMetallic);
                profile.buildings.zossEmissiveMaterial.SetFloat("_Smoothness", profile.buildings.zossEmissiveSmoothness);

                // Albedo
                profile.buildings.zossEmissiveMaterial.SetTexture("_BaseMap", profile.buildings.zossEmissiveAlbedo);

                // Emission (HDR)
                Color hdrEmission = profile.buildings.zossEmissiveEmission * profile.buildings.zossEmissiveIntensity;
                profile.buildings.zossEmissiveMaterial.SetColor("_EmissionColor", hdrEmission);
                profile.buildings.zossEmissiveMaterial.SetTexture("_EmissionMap", profile.buildings.zossEmissiveEmissionMap);

                profile.buildings.zossEmissiveMaterial.EnableKeyword("_EMISSION");
                profile.buildings.zossEmissiveMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

                // Tiling
                profile.buildings.zossEmissiveMaterial.SetTextureScale("_BaseMap", profile.buildings.zossEmissiveTiling);
                profile.buildings.zossEmissiveMaterial.SetTextureScale("_EmissionMap", profile.buildings.zossEmissiveTiling);

                // Glass Properties - Environment Reflections
                if (profile.buildings.windowEnvironmentReflections)
                {
                    profile.buildings.zossEmissiveMaterial.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                    profile.buildings.zossEmissiveMaterial.SetFloat("_EnvironmentReflections", 1.0f);
                }
                else
                {
                    profile.buildings.zossEmissiveMaterial.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                    profile.buildings.zossEmissiveMaterial.SetFloat("_EnvironmentReflections", 0.0f);
                }

                // Glass Properties - Specular Highlights
                if (profile.buildings.windowSpecularHighlights)
                {
                    profile.buildings.zossEmissiveMaterial.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
                    profile.buildings.zossEmissiveMaterial.SetFloat("_SpecularHighlights", 1.0f);
                }
                else
                {
                    profile.buildings.zossEmissiveMaterial.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                    profile.buildings.zossEmissiveMaterial.SetFloat("_SpecularHighlights", 0.0f);
                }
            }
        }

        [ContextMenu("Debug: Print Puddle Settings")]
        private void DebugPrintPuddleSettings()
        {
            Debug.Log($"[RenderManager] Puddle Settings:\n" +
                     $"  reflectionStrength: {profile.roads.reflectionStrength}\n" +
                     $"  reflectionYOffset: {profile.roads.reflectionYOffset}\n" +
                     $"  reflectionDistortion: {profile.roads.reflectionDistortion}\n" +
                     $"  reflectionWarble: {profile.roads.reflectionWarble}\n" +
                     $"  reflectionWarbleScale: {profile.roads.reflectionWarbleScale}\n" +
                     $"  puddleColor: {profile.roads.puddleColor}\n" +
                     $"  puddleFrequency: {profile.roads.puddleFrequency}\n" +
                     $"  puddleAmount: {profile.roads.puddleAmount}");
        }

        [ContextMenu("Debug: Check Road Material")]
        private void DebugCheckRoadMaterial()
        {
            if (profile.roads.roadMaterial == null)
            {
                Debug.LogError("[RenderManager] Road material is NULL!");
                return;
            }

            Material mat = profile.roads.roadMaterial;
            Debug.Log($"[RenderManager] Road Material Check:\n" +
                     $"  Name: {mat.name}\n" +
                     $"  Shader: {mat.shader.name}\n" +
                     $"  Render Queue: {mat.renderQueue} (should be 3000 for Transparent)\n" +
                     $"  Has _ReflectionStrength: {mat.HasProperty("_ReflectionStrength")}\n" +
                     $"  Has _PuddleScale: {mat.HasProperty("_PuddleScale")}\n" +
                     $"  Current _ReflectionStrength: {(mat.HasProperty("_ReflectionStrength") ? mat.GetFloat("_ReflectionStrength").ToString("F2") : "N/A")}\n" +
                     $"  Current _PuddleSpread: {(mat.HasProperty("_PuddleSpread") ? mat.GetFloat("_PuddleSpread").ToString("F2") : "N/A")}");
        }

        [ContextMenu("Fix: Force Render Queue to Transparent")]
        private void ForceRenderQueueTransparent()
        {
            if (profile.roads.roadMaterial != null)
            {
                profile.roads.roadMaterial.renderQueue = 3000;
                Debug.Log($"[RenderManager] Forced '{profile.roads.roadMaterial.name}' renderQueue to 3000 (Transparent)");
                Apply();
            }
        }

        private void ApplyRoadMaterialSettings(Material mat)
        {
            if (mat == null) return;

            mat.SetColor("_BaseColor", profile.roads.roadColor);
            mat.SetFloat("_Metallic", profile.roads.roadMetallic);
            mat.SetFloat("_Smoothness", profile.roads.roadSmoothness);

            // Albedo
            mat.SetTexture("_BaseMap", profile.roads.roadAlbedo);

            // Normal Map
            if (profile.roads.roadNormal != null)
            {
                mat.SetTexture("_BumpMap", profile.roads.roadNormal);
                mat.EnableKeyword("_NORMALMAP");
                mat.SetFloat("_BumpScale", profile.roads.roadNormalStrength);
            }
            else
            {
                mat.SetTexture("_BumpMap", null);
                mat.DisableKeyword("_NORMALMAP");
            }

            // Emission
            Color hdrEmission = profile.roads.roadEmission * profile.roads.roadEmissionIntensity;
            mat.SetColor("_EmissionColor", hdrEmission);
            mat.SetTexture("_EmissionMap", profile.roads.roadEmissionMap);

            if (profile.roads.roadEmissionIntensity > 0 || profile.roads.roadEmissionMap != null)
            {
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                mat.DisableKeyword("_EMISSION");
            }

            // Occlusion
            mat.SetTexture("_OcclusionMap", profile.roads.roadOcclusionMap);
            mat.SetFloat("_OcclusionStrength", profile.roads.roadOcclusionStrength);

            // Environment Reflections
            if (profile.roads.roadEnvironmentReflections)
            {
                mat.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                mat.SetFloat("_EnvironmentReflections", 1.0f);
            }
            else
            {
                mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                mat.SetFloat("_EnvironmentReflections", 0.0f);
            }

            // Specular Highlights
            if (profile.roads.roadSpecularHighlights)
            {
                mat.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
                mat.SetFloat("_SpecularHighlights", 1.0f);
            }
            else
            {
                mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                mat.SetFloat("_SpecularHighlights", 0.0f);
            }

            // Tiling (world-space: set both mainTextureScale and per-texture scale)
            mat.mainTextureScale = profile.roads.roadTiling;
            mat.mainTextureOffset = Vector2.zero;

            mat.SetTextureScale("_BaseMap", profile.roads.roadTiling);
            mat.SetTextureOffset("_BaseMap", Vector2.zero);

            mat.SetTextureScale("_BumpMap", profile.roads.roadTiling);
            mat.SetTextureOffset("_BumpMap", Vector2.zero);

            mat.SetTextureScale("_EmissionMap", profile.roads.roadTiling);
            mat.SetTextureOffset("_EmissionMap", Vector2.zero);

            mat.SetTextureScale("_OcclusionMap", profile.roads.roadTiling);
            mat.SetTextureOffset("_OcclusionMap", Vector2.zero);

            // Height Map (Parallax)
            if (profile.roads.roadHeightMap != null)
            {
                mat.SetTexture("_ParallaxMap", profile.roads.roadHeightMap);
                mat.SetTextureScale("_ParallaxMap", profile.roads.roadTiling);
                mat.SetTextureOffset("_ParallaxMap", Vector2.zero);
                mat.SetFloat("_Parallax", profile.roads.roadHeightScale);
                mat.EnableKeyword("_PARALLAXMAP");
            }
            else
            {
                mat.SetTexture("_ParallaxMap", null);
                mat.DisableKeyword("_PARALLAXMAP");
            }

            // Anti-flickering: Disable motion vectors (causes flickering with procedural roads)
            mat.SetFloat("_AddPrecomputedVelocity", 0.0f);
            mat.DisableKeyword("_ADD_PRECOMPUTED_VELOCITY");

            // Reflections & Puddles (SeamlessRoad shader properties)
            // Check if material has these properties before setting
            if (mat.HasProperty("_ReflectionStrength"))
            {
                mat.SetFloat("_ReflectionStrength", profile.roads.reflectionStrength);
                mat.SetFloat("_ReflectionYOffset", profile.roads.reflectionYOffset);
                mat.SetFloat("_ReflectionDistortion", profile.roads.reflectionDistortion);
                mat.SetFloat("_ReflectionWarble", profile.roads.reflectionWarble);
                mat.SetFloat("_ReflectionWarbleScale", profile.roads.reflectionWarbleScale);
                mat.SetColor("_PuddleColor", profile.roads.puddleColor);
                mat.SetFloat("_PuddleScale", profile.roads.puddleFrequency);
                mat.SetFloat("_PuddleSpread", profile.roads.puddleAmount);
                mat.SetFloat("_PuddleSharpness", profile.roads.puddleSharpness);

                // CRITICAL: Ensure material is in Transparent queue to access CameraOpaqueTexture
                if (mat.renderQueue != 3000) // 3000 = Transparent queue
                {
                    mat.renderQueue = 3000;
                }
            }
            else if (Time.frameCount % 300 == 0)
            {
                Debug.LogWarning($"[RenderManager] Material '{mat.name}' doesn't have reflection/puddle properties! Shader: {mat.shader.name}");
            }

            // Note: Roughness maps not used - URP Lit shader uses smoothness value only
        }

        private void ApplyGroundMaterial()
        {
            // Update cache if needed
            if (_cachedGroundPlane == null)
            {
                _cachedGroundPlane = FindFirstObjectByType<SimpleGroundPlane>();
                if (_cachedGroundPlane != null)
                {
                    Debug.Log("[RenderManager] Found SimpleGroundPlane for ground material updates");
                }
            }

            if (_cachedGroundPlane != null)
            {
                // Set ground Y position from ground settings
                Vector3 pos = _cachedGroundPlane.transform.position;
                pos.y = profile.ground.groundYPosition;
                _cachedGroundPlane.transform.position = pos;

                _cachedGroundPlane.UpdateMaterial(
                    color: profile.ground.groundColor,
                    smoothness: profile.ground.groundSmoothness,
                    brightness: profile.ground.groundBrightness,
                    metallic: profile.ground.groundMetallic,
                    albedo: profile.ground.groundTexture,
                    normal: profile.ground.groundNormal,
                    normalStrength: profile.ground.groundNormalStrength,
                    emission: profile.ground.groundEmission,
                    emissionMap: profile.ground.groundEmissionMap,
                    emissionIntensity: profile.ground.groundEmissionIntensity,
                    tiling: profile.ground.groundTiling
                );

                if (profile.ground.groundMaterial == null)
                {
                    profile.ground.groundMaterial = _cachedGroundPlane.GetGroundMaterial();
                }
            }
            else if (Time.frameCount % 300 == 0)
            {
                Debug.LogWarning("[RenderManager] SimpleGroundPlane not found in scene!");
            }
        }

        private void ApplyMapSettings()
        {
            // Set per-layer Y positions for proper rendering order:
            // Ground plane < Water < Roads (buildings at Y=0, they have their own height)
            SetLayerRootY("water layer objects", profile.ground.waterYPosition);
            SetLayerRootY("road layer objects", profile.roads.roadYPosition);

            // Legacy: also set KiloMap Y if it exists
            GameObject map = GameObject.Find("KiloMap");
            if (map != null)
            {
                Vector3 pos = map.transform.position;
                if (Mathf.Abs(pos.y - profile.roads.roadYPosition) > 0.001f)
                {
                    pos.y = profile.roads.roadYPosition;
                    map.transform.position = pos;
                }
            }
        }

        private static void SetLayerRootY(string layerRootName, float yPosition)
        {
            GameObject layerRoot = GameObject.Find(layerRootName);
            if (layerRoot != null)
            {
                Vector3 pos = layerRoot.transform.position;
                if (Mathf.Abs(pos.y - yPosition) > 0.001f)
                {
                    pos.y = yPosition;
                    layerRoot.transform.position = pos;
                }
            }
        }
    }
}
