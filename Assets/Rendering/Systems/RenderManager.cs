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
        private MeshRenderer[] _cachedLandRenderers;
        private MeshRenderer[] _cachedBuildingRenderers;
        private float _lastRoadCacheTime;
        private float _lastBuildingCacheTime;
        private readonly MaterialPropertyBlock _wallPropertyBlock = new MaterialPropertyBlock();

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

        // Day vs Night Fog and Ground settings.
        // Day: radioactive-blooming look — thicker (visible), high scatter so
        // sun rims + windows glow into it, height clipped so it hugs the
        // walkable slab instead of the whole sky.
        [HideInInspector] public float dayFogDensity = 0.045f;
        [HideInInspector] public float dayFogNoiseStrength = 2.1f;
        [HideInInspector] public float dayFogNoiseScale = 24.0f;
        [HideInInspector] public float dayFogBrightness = 1.05f;
        [HideInInspector] public float dayFogScatteringIntensity = 2.15f;
        [HideInInspector] public float dayFogHeight = 62.0f;
        [HideInInspector] public float dayFogDistantDensity = 0.006f;
        [HideInInspector] public float dayFogDistantStart = 320.0f;

        [HideInInspector] public float nightFogDensity = 0.025f;
        [HideInInspector] public float nightFogNoiseStrength = 0.22f;
        [HideInInspector] public float nightFogNoiseScale = 17.4f;
        [HideInInspector] public float nightFogBrightness = 0.24f;
        [HideInInspector] public float nightFogScatteringIntensity = 0.55f;
        [HideInInspector] public float nightFogHeight = 48.0f;
        [HideInInspector] public float nightFogDistantDensity = 0.0f;
        [HideInInspector] public float nightFogDistantStart = 0.0f;

        [HideInInspector] public float dayGroundHue = 0.33f;
        [HideInInspector] public float dayGroundSaturation = 0.42f;
        [HideInInspector] public float nightGroundHue = 0.3f;
        [HideInInspector] public float nightGroundSaturation = 0f;

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
            if (PlayerPrefs.GetInt("k1lo_colorGradeZero_v1", 0) != 1)
            {
                // Snap all color-grade sliders to 0 for new installs AND to
                // clear the older "brightMap" / "dystopian" grades that were
                // baked in on existing installs.
                PlayerPrefs.SetFloat("k1lo_saturation", 0f);
                PlayerPrefs.SetFloat("k1lo_contrast", 0f);
                PlayerPrefs.SetFloat("k1lo_mapBrightness", 0f);
                PlayerPrefs.SetFloat("k1lo_hueShift", 0f);
                PlayerPrefs.SetFloat("k1lo_temperature", 0f);
                PlayerPrefs.SetFloat("k1lo_tint", 0f);
                PlayerPrefs.SetFloat("k1lo_exposureFixedValue", 0.35f);
                PlayerPrefs.SetInt("k1lo_colorGradeZero_v1", 1);
                PlayerPrefs.Save();
            }

            pfx.saturation = PlayerPrefs.GetFloat("k1lo_saturation", 0f);
            pfx.contrast = PlayerPrefs.GetFloat("k1lo_contrast", 0f);
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

            // Camera
            var cam = profile.camera;
            LoadPref("godPositionY", ref cam.godPositionY);
            LoadPref("godPositionZ", ref cam.godPositionZ);
            LoadPref("godRotationX", ref cam.godRotationX);
            LoadPref("farClipPlane", ref cam.farClipPlane);

            var lighting = profile.lighting;
            LoadPrefBool("moonlightEnabled", ref lighting.moonlightEnabled);
            LoadPrefBool("ambientEnabled", ref lighting.ambientEnabled);
            LoadPref("moonlightIntensity", ref lighting.moonlightIntensity);
            LoadPref("moonlightRed", ref lighting.moonlightColor.r);
            LoadPref("moonlightGreen", ref lighting.moonlightColor.g);
            LoadPref("moonlightBlue", ref lighting.moonlightColor.b);
            LoadPref("moonlightPitch", ref lighting.moonlightRotation.x);
            LoadPref("moonlightYaw", ref lighting.moonlightRotation.y);
            LoadPref("moonlightRoll", ref lighting.moonlightRotation.z);
            LoadPref("ambientIntensity", ref lighting.ambientIntensity);
            LoadPrefBool("spotlightEnabled", ref lighting.spotlightEnabled);
            LoadPref("spotlightIntensity", ref lighting.spotlightIntensity);

            var fog = profile.volumetricFog;
            ClearBadFogDefaultsOnce();
            LoadPrefBool("fogConstantDensity", ref fog.constantDensity);
            fog.raymarchQuality = Mathf.Clamp(
                Mathf.RoundToInt(PlayerPrefs.GetFloat("k1lo_fogRaymarchQuality", fog.raymarchQuality)), 1, 16);
            
            // Load day fog settings into profile.volumetricFog AND our dayFog fields
            LoadPref("fogDensity", ref fog.density);
            // Daytime fog must stay atmospheric rather than obscuring the map.
            // Ignore older persisted heavy values and use the approved light density.
            dayFogDensity = 0.01f;
            fog.density = dayFogDensity;
            LoadPref("fogNoiseStrength", ref fog.noiseStrength); dayFogNoiseStrength = fog.noiseStrength;
            LoadPref("fogNoiseScale", ref fog.noiseScale); dayFogNoiseScale = fog.noiseScale;
            LoadPref("fogBrightness", ref fog.brightness); dayFogBrightness = fog.brightness;
            LoadPref("fogScatteringIntensity", ref fog.scatteringIntensity); dayFogScatteringIntensity = fog.scatteringIntensity;
            LoadPrefBool("fogCustomHeight", ref fog.customHeight);
            LoadPref("fogHeight", ref fog.height); dayFogHeight = fog.height;
            LoadPref("fogVerticalOffset", ref fog.verticalOffset);
            LoadPref("fogDistance", ref fog.distance);
            LoadPref("fogDistanceFallOff", ref fog.distanceFallOff);
            LoadPref("fogMaxDistance", ref fog.maxDistance);
            LoadPref("fogMaxDistanceFallOff", ref fog.maxDistanceFallOff);
            LoadPrefBool("fogDistantFog", ref fog.distantFog);
            LoadPref("fogDistantDensity", ref fog.distantFogDistanceDensity); dayFogDistantDensity = fog.distantFogDistanceDensity;
            LoadPref("fogDistantStart", ref fog.distantFogStartDistance); dayFogDistantStart = fog.distantFogStartDistance;
            LoadPrefBool("fogNativeLights", ref fog.enableNativeLights);
            LoadPref("fogNativeLightsMultiplier", ref fog.nativeLightsMultiplier);

            // Load night fog settings
            nightFogDensity = PlayerPrefs.GetFloat("k1lo_fogDensity_night", dayFogDensity);
            nightFogNoiseStrength = PlayerPrefs.GetFloat("k1lo_fogNoiseStrength_night", dayFogNoiseStrength);
            nightFogNoiseScale = PlayerPrefs.GetFloat("k1lo_fogNoiseScale_night", dayFogNoiseScale);
            nightFogBrightness = PlayerPrefs.GetFloat("k1lo_fogBrightness_night", dayFogBrightness);
            nightFogScatteringIntensity = PlayerPrefs.GetFloat("k1lo_fogScatteringIntensity_night", dayFogScatteringIntensity);
            nightFogHeight = PlayerPrefs.GetFloat("k1lo_fogHeight_night", dayFogHeight);
            nightFogDistantDensity = PlayerPrefs.GetFloat("k1lo_fogDistantDensity_night", dayFogDistantDensity);
            nightFogDistantStart = PlayerPrefs.GetFloat("k1lo_fogDistantStart_night", dayFogDistantStart);

            var buildings = profile.buildings;
            LoadPref("zossWallSmoothness", ref buildings.zossWallSmoothness);
            LoadPref("zossWallMetallic", ref buildings.zossWallMetallic);
            LoadPref("zossEmissiveIntensity", ref buildings.zossEmissiveIntensity);
            LoadPref("zossEmissiveSmoothness", ref buildings.zossEmissiveSmoothness);
            LoadPref("zossEmissiveMetallic", ref buildings.zossEmissiveMetallic);

            // Persisted window-glow + ground hue/sat (Swift settings sliders).
            if (PlayerPrefs.HasKey("k1lo_zossEmissiveHue") || PlayerPrefs.HasKey("k1lo_zossEmissiveSaturation"))
            {
                Color.RGBToHSV(buildings.zossEmissiveColor, out float wh, out float ws, out float wv);
                if (PlayerPrefs.HasKey("k1lo_zossEmissiveHue")) wh = PlayerPrefs.GetFloat("k1lo_zossEmissiveHue");
                if (PlayerPrefs.HasKey("k1lo_zossEmissiveSaturation")) ws = PlayerPrefs.GetFloat("k1lo_zossEmissiveSaturation");
                var wc = Color.HSVToRGB(Mathf.Clamp01(wh), Mathf.Clamp01(ws), Mathf.Max(0.01f, wv));
                buildings.zossEmissiveColor = wc;
                buildings.zossEmissiveEmission = wc;
            }

            // Vaporwave building look (Swift settings sliders): dark wall
            // bodies + per-window palette variety on the emissive material.
            if (PlayerPrefs.HasKey("k1lo_zossWallValue") || PlayerPrefs.HasKey("k1lo_zossWallHue") || PlayerPrefs.HasKey("k1lo_zossWallSaturation"))
            {
                Color.RGBToHSV(buildings.zossWallColor, out float bh, out float bs, out float bv);
                bh = PlayerPrefs.GetFloat("k1lo_zossWallHue", bh);
                bs = PlayerPrefs.GetFloat("k1lo_zossWallSaturation", bs);
                bv = PlayerPrefs.GetFloat("k1lo_zossWallValue", bv);
                buildings.zossWallColor = Color.HSVToRGB(Mathf.Clamp01(bh), Mathf.Clamp01(bs), Mathf.Clamp01(bv));
            }
            // K1L0's city identity depends on luminous windows in every visual
            // mode. Never let stale settings, resets or astronomy turn them off.
            WindowLitFraction = 1f;
            WindowPaletteMix = PlayerPrefs.GetFloat("k1lo_zossPaletteMix", 1f);
            WindowPaletteSaturation = PlayerPrefs.GetFloat("k1lo_zossPaletteSaturation", 1.35f);
            WindowPaletteSaturationNight = PlayerPrefs.GetFloat("k1lo_zossPaletteSaturation_night", 1.22f);
            WindowWarmth = PlayerPrefs.GetFloat("k1lo_zossWarmth", 1f);
            WindowAccentFraction = PlayerPrefs.GetFloat("k1lo_zossAccentFraction", 0.08f);
            WindowBrightness = Mathf.Max(0.75f, PlayerPrefs.GetFloat("k1lo_zossWindowBrightness", 1f));
            
            ApplyGreenGroundOnce();
            ApplyRadioactiveFogOnce();
            Color.RGBToHSV(profile.ground.groundColor, out float initialGh, out float initialGs, out float initialGv);
            // 0.18 saturation cap removed — was crushing green to washed-out
            // gray so ambient tungsten + warm sun blew it out to orange/brown.
            dayGroundHue = PlayerPrefs.GetFloat("k1lo_groundHue", initialGh);
            dayGroundSaturation = PlayerPrefs.GetFloat("k1lo_groundSaturation", initialGs);
            nightGroundHue = PlayerPrefs.GetFloat("k1lo_groundHue_night", dayGroundHue);
            nightGroundSaturation = PlayerPrefs.GetFloat("k1lo_groundSaturation_night", dayGroundSaturation);

            if (PlayerPrefs.HasKey("k1lo_groundHue") || PlayerPrefs.HasKey("k1lo_groundSaturation"))
            {
                if (initialGv < 0.05f) initialGv = 0.5f;
                profile.ground.groundColor = Color.HSVToRGB(Mathf.Clamp01(dayGroundHue), Mathf.Clamp01(dayGroundSaturation), initialGv);
            }

            LoadPref("manualHour", ref ManualHour);
            ClearOvercastManualWeatherOnce();
            TestSkyOverrideEnabled = PlayerPrefs.GetInt("k1lo_testSkyOverride", 0) == 1;
            ManualWeatherOverrideEnabled = PlayerPrefs.GetInt("k1lo_manualWeatherOverrideEnabled", 0) == 1;
            if (PlayerPrefs.HasKey("k1lo_manualWeatherGlyph"))
                ManualWeatherGlyph = PlayerPrefs.GetString("k1lo_manualWeatherGlyph");
        }

        // One-shot: force existing installs to the new radioactive-daytime fog
        // preset (thicker density + high scattering so sun/window rims bloom
        // through the haze). Prior tuning sessions had left the day fog near
        // transparent — this bumps every existing install to the new default
        // once, and users can still slider-tune from there.
        private static void ApplyRadioactiveFogOnce()
        {
            const string migrationKey = "k1lo_radioactiveFog_v1";
            if (PlayerPrefs.GetInt(migrationKey, 0) == 1) return;

            PlayerPrefs.SetFloat("k1lo_fogDensity", 0.045f);
            PlayerPrefs.SetFloat("k1lo_fogNoiseStrength", 2.1f);
            PlayerPrefs.SetFloat("k1lo_fogNoiseScale", 24.0f);
            PlayerPrefs.SetFloat("k1lo_fogBrightness", 1.05f);
            PlayerPrefs.SetFloat("k1lo_fogScatteringIntensity", 2.15f);
            PlayerPrefs.SetFloat("k1lo_fogHeight", 62.0f);
            PlayerPrefs.SetFloat("k1lo_fogDistantDensity", 0.006f);
            PlayerPrefs.SetFloat("k1lo_fogDistantStart", 320.0f);
            PlayerPrefs.SetInt(migrationKey, 1);
            PlayerPrefs.Save();
        }

        // One-shot: force the ground back to a legible green. Prior tuning
        // sessions and migrations had crushed saved ground-saturation prefs
        // down to 0.18 (or less) which reads as gray, and once the ambient
        // tungsten + warm sun hit that gray, the whole ground took an
        // orange/brown cast. This bump gives every install a hue-0.33 /
        // saturation-0.42 / value-0.55 starting point. Users can still
        // re-tune via the settings sliders after; this only fires once.
        private static void ApplyGreenGroundOnce()
        {
            const string migrationKey = "k1lo_greenGround_v1";
            if (PlayerPrefs.GetInt(migrationKey, 0) == 1) return;

            if (!PlayerPrefs.HasKey("k1lo_groundHue"))
                PlayerPrefs.SetFloat("k1lo_groundHue", 0.33f);
            if (!PlayerPrefs.HasKey("k1lo_groundSaturation"))
                PlayerPrefs.SetFloat("k1lo_groundSaturation", 0.42f);
            if (!PlayerPrefs.HasKey("k1lo_groundHue_night"))
                PlayerPrefs.SetFloat("k1lo_groundHue_night", 0.33f);
            if (!PlayerPrefs.HasKey("k1lo_groundSaturation_night"))
                PlayerPrefs.SetFloat("k1lo_groundSaturation_night", 0.42f);
            PlayerPrefs.SetInt(migrationKey, 1);
            PlayerPrefs.Save();
        }

        // The dystopian grade briefly defaulted manual weather to Overcast,
        // which hijacked the sky video whenever live weather was unavailable.
        // One-shot: drop a stored Overcast override so the fallback is Clear
        // again; deliberate non-overcast picks are left alone.
        private static void ClearOvercastManualWeatherOnce()
        {
            const string migrationKey = "k1lo_clearOvercastManualWeather_v1";
            if (PlayerPrefs.GetInt(migrationKey, 0) == 1) return;

            if (PlayerPrefs.GetString("k1lo_manualWeatherGlyph", "") == "overcast")
            {
                PlayerPrefs.DeleteKey("k1lo_manualWeather");
                PlayerPrefs.DeleteKey("k1lo_manualWeatherGlyph");
                PlayerPrefs.DeleteKey("k1lo_manualWeatherOverrideEnabled");
            }

            PlayerPrefs.SetInt(migrationKey, 1);
            PlayerPrefs.Save();
        }

        private void ClearBadFogDefaultsOnce()
        {
            const string migrationKey = "k1lo_clearBadFogDefaults_v3";
            if (PlayerPrefs.GetInt(migrationKey, 0) == 1) return;

            string[] fogKeys =
            {
                "k1lo_fogConstantDensity",
                "k1lo_fogDensity",
                "k1lo_fogNoiseStrength",
                "k1lo_fogNoiseScale",
                "k1lo_fogBrightness",
                "k1lo_fogScatteringIntensity",
                "k1lo_fogCustomHeight",
                "k1lo_fogHeight",
                "k1lo_fogDistantFog",
                "k1lo_fogDistantDensity",
                "k1lo_fogDistantStart",
                "k1lo_fogNativeLights",
                "k1lo_fogNativeLightsMultiplier"
            };

            foreach (var key in fogKeys)
            {
                PlayerPrefs.DeleteKey(key);
            }

            // Previous clamp to 0.18 killed the green — leaving the sat pref
            // alone here now; the green-ground migration (ApplyGreenGroundOnce)
            // takes over on the ground channel.

            // Wall value pulled down so daytime walls don't wash out.
            if (PlayerPrefs.HasKey("k1lo_zossWallValue"))
                PlayerPrefs.SetFloat("k1lo_zossWallValue",
                    Mathf.Min(PlayerPrefs.GetFloat("k1lo_zossWallValue"), 0.35f));

            PlayerPrefs.SetInt(migrationKey, 1);
            PlayerPrefs.Save();
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
            // (SSR opaque-texture check removed with the reflection system.)

            // Apply on Start to ensure all components (like SimpleGroundPlane) are initialized
            Apply();
            BootDiagnostics.Mark("RenderManager.Start complete");
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
            // (Reflection-probe debug logging removed with the reflection system.)
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

                // (SSR renderer-feature branch removed with the reflection system.)
            }

            if (!foundSSAO && profile.rendererFeatures.ssaoEnabled)
            {
                Debug.LogWarning("[RenderManager] SSAO is enabled in profile but no SSAO feature found in renderer! Add 'Screen Space Ambient Occlusion' feature to your Renderer Data asset.");
            }
        }

        /// Truly toggles the Volumetric Fog renderer feature. Density alone
        /// only changes opacity; the URP fog and blur passes otherwise remain
        /// enqueued and still consume GPU time even at zero density.
        public void SetVolumetricFogRuntimeEnabled(bool enabled)
        {
            var urpAsset = UniversalRenderPipeline.asset;
            var renderer = urpAsset != null ? urpAsset.scriptableRenderer : null;
            if (renderer != null)
            {
                var field = typeof(ScriptableRenderer).GetField(
                    "m_RendererFeatures",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var features = field?.GetValue(renderer) as System.Collections.Generic.List<ScriptableRendererFeature>;
                if (features != null)
                {
                    foreach (var feature in features)
                    {
                        if (feature == null || !feature.GetType().Name.Contains("VolumetricFogRenderFeature")) continue;
                        feature.SetActive(enabled);
                        if (enabled) feature.Create();
                    }
                }
            }

            if (_cachedVolumetricFog == null) _cachedVolumetricFog = FindObjectOfType<VolumetricFog>(true);
            if (_cachedVolumetricFogManager == null) _cachedVolumetricFogManager = FindObjectOfType<VolumetricFogManager>(true);
            if (_cachedVolumetricFog != null) _cachedVolumetricFog.enabled = enabled;
            if (_cachedVolumetricFogManager != null) _cachedVolumetricFogManager.enabled = enabled;
            PlayerPrefs.SetInt("k1lo_volumetricFogEnabled", enabled ? 1 : 0);
            PlayerPrefs.Save();
            Debug.Log($"[RenderManager] Volumetric fog renderer feature enabled={enabled}");
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
            EnsureDirectionalSun();

            bool dynamicSkySun = profile.sky != null && profile.sky.dynamicSky;
            bool directionalEnabled = profile.lighting.moonlightEnabled || dynamicSkySun;

            if (directionalLight != null)
            {
                if (!directionalEnabled)
                {
                    if (directionalLight.enabled)
                        directionalLight.enabled = false;
                    if (directionalLight.intensity != 0f)
                        directionalLight.intensity = 0f;
                    if (VolumetricFogManager.instance != null && VolumetricFogManager.instance.sun == directionalLight)
                        VolumetricFogManager.instance.sun = null;
                }
                else
                {
                    if (!directionalLight.enabled)
                        directionalLight.enabled = true;

                    // Ensure Volumetric Fog uses this light as the Sun only when enabled.
                    if (VolumetricFogManager.instance != null && VolumetricFogManager.instance.sun != directionalLight)
                    {
                        VolumetricFogManager.instance.sun = directionalLight;
                    }

                    bool manualMoonlight = PlayerPrefs.GetFloat("k1lo_moonlightManualOverride", 0f) > 0.5f;
                    if (dynamicSkySun && !manualMoonlight)
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

                    // Shadows removed — force the directional (sun/moon) light
                    // to cast no shadows every frame.
                    if (directionalLight.shadows != LightShadows.None)
                        directionalLight.shadows = LightShadows.None;
                }
            }

            RenderSettings.sun = directionalEnabled ? directionalLight : null;

            // Apply Ambient Mode and Colors
            if (RenderSettings.ambientMode != profile.lighting.ambientMode)
                RenderSettings.ambientMode = profile.lighting.ambientMode;

            float effectiveAmbientIntensity = profile.lighting.ambientEnabled ? profile.lighting.ambientIntensity : 0f;
            if (RenderSettings.ambientIntensity != effectiveAmbientIntensity)
                RenderSettings.ambientIntensity = effectiveAmbientIntensity;

            // Apply Colors based on mode
            // Note: RenderSettings.ambientIntensity usually only affects Skybox mode.
            // For Flat and Trilight, we manually multiply the colors by the intensity for consistency.
            float intensity = effectiveAmbientIntensity;

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

            // Reflections removed — force fully off every frame so nothing visual
            // remains from serialized RenderSettings defaults.
            if (RenderSettings.reflectionIntensity != 0f)
                RenderSettings.reflectionIntensity = 0f;
            if (RenderSettings.defaultReflectionMode != DefaultReflectionMode.Skybox)
                RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
        }

        private void EnsureDirectionalSun()
        {
            if (directionalLight != null)
            {
                directionalLight.type = LightType.Directional;
                directionalLight.renderMode = LightRenderMode.ForcePixel;
                return;
            }

            var lights = FindObjectsOfType<Light>();
            foreach (var l in lights)
            {
                // Look for the main sun/moon, ignore player spotlights even if the
                // runtime spotlight manager has converted its light to Directional.
                if (l.type == LightType.Directional &&
                    !l.name.Contains("Spotlight") &&
                    l.GetComponent<global::KiloWorld.SpotlightManager>() == null)
                {
                    directionalLight = l;
                    directionalLight.renderMode = LightRenderMode.ForcePixel;
                    return;
                }
            }

            if (!Application.isPlaying)
                return;

            var sunObject = GameObject.Find("K1L0_Sun") ?? new GameObject("K1L0_Sun");
            sunObject.hideFlags = HideFlags.DontSave;
            directionalLight = sunObject.GetComponent<Light>() ?? sunObject.AddComponent<Light>();
            directionalLight.type = LightType.Directional;
            directionalLight.renderMode = LightRenderMode.ForcePixel;
            directionalLight.cullingMask = ~0;
            directionalLight.enabled = true;
            Debug.Log("[RenderManager] Created runtime K1L0_Sun directional light.");
        }

        // Cache for Skybox updates
        private Cubemap _lastSkybox;
        private float _lastSkyboxRotation = -1f;
        private float _lastSkyboxExposure = -1f;
        private Color _lastSkyboxTint = Color.clear;
        public static string WeatherGlyph = null;   // condition word/emoji from the presence feed
        public static bool? WeatherIsDay = null;    // daylight state from backend weather/sunrise-sunset

        // Manual sky overrides, used only when GPS mode is off (no live time/location).
        public static float ManualHour = 13f;            // 0..24 local hour
        public static string ManualWeatherGlyph = "clear";
        public static bool ManualWeatherOverrideEnabled = false;
        public static int ManualSkyRevision { get; private set; }

        public static void NotifyManualSkyChanged()
        {
            ManualSkyRevision++;
        }

        // Test override: when on, the manual weather picker + sky-hour slider
        // beat the live server weather and real clock even with GPS enabled.
        // Toggled from the Swift settings panel for QA'ing every sky state.
        public static bool TestSkyOverrideEnabled = false;

        // Vaporwave building knobs (Swift sliders → PlayerPrefs → window shader).
        public static float WindowLitFraction = 0.72f;
        public static float WindowPaletteMix = 1f;
        // Saturation blends day→night on the sun-altitude ramp: juiced neon
        // palette in daylight, monochrome glow after dark.
        public static float WindowPaletteSaturation = 1.35f;
        public static float WindowPaletteSaturationNight = 0f;
        public static float WindowWarmth = 1f;
        public static float WindowAccentFraction = 0.08f;
        public static float WindowBrightness = 1f;

        public static string EffectiveWeatherGlyph()
        {
            if (TestSkyOverrideEnabled)
                return ManualWeatherGlyph;
            if (GPSLocationController.GPSDisabled && ManualWeatherOverrideEnabled)
                return ManualWeatherGlyph;
            return !string.IsNullOrWhiteSpace(WeatherGlyph) ? WeatherGlyph : ManualWeatherGlyph;
        }

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

            if (global::DynamicSkyVideoController.IsActive)
            {
                global::DynamicSkyVideoController.ForceApply();
                ApplyPrecip(mainCam);
                return;
            }
            
            if (mainCam.clearFlags != CameraClearFlags.Skybox)
                mainCam.clearFlags = CameraClearFlags.Skybox;

            if (profile.sky != null && profile.sky.dynamicSky)
            {
                ApplyProceduralSky();
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

            ApplyPrecip(mainCam);
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
            string g = EffectiveWeatherGlyph();
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
            // GPS off (or test override) → the sun follows the manual hour slider:
            // a simple arc (0° at 6h/18h, +60° at noon, below the horizon at night).
            if (GPSLocationController.GPSDisabled || TestSkyOverrideEnabled)
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
            // Backend sunrise/sunset is authoritative for day vs night: if the weather
            // feed says daylight but the astro sun sits at/below the horizon (stale GPS
            // fix, clock skew, 0/0 coords), floor the sun so the world never renders
            // night lighting under a daytime sky.
            if (WeatherIsDay == true && altDeg < 8f) altDeg = 8f;
            float altR = (float)(altDeg * Deg2RadD), azR = (float)(az * Deg2RadD);
            // az from north, clockwise. World +Z≈north, +X≈east, +Y up.
            return new Vector3(Mathf.Sin(azR) * Mathf.Cos(altR), Mathf.Sin(altR), Mathf.Cos(azR) * Mathf.Cos(altR));
        }

        // Latest computed sun altitude (degrees above horizon) — real astronomy
        // from GPS + UTC time, floored by the backend isDay clamp. Exposed so
        // the sky-video picker can fall back to it when the API is silent.
        public static float LiveSunAltitudeDeg = 30f;
        public static Vector3 LiveSunDirection = new Vector3(0f, .5f, .866f);

        private void DriveSunLight(Light light)
        {
            Vector3 toSun = CurrentSunDirection(out float alt);
            _sunAlt = alt;
            LiveSunAltitudeDeg = alt;
            LiveSunDirection = toSun;

            WeatherFactors(out float cloud, out float wet, out bool snow);

            Vector3 lightForward;
            float intensity;
            Color col;

            Color horizonWarm = new Color(1f, 0.62f, 0.36f);
            Color midday     = new Color(1f, 0.97f, 0.92f);
            Color moonColor  = profile.lighting != null ? profile.lighting.moonlightColor : new Color(0.7f, 0.8f, 1f);

            if (alt < 0f)
            {
                // Sun underground: use moon approximation — opposite azimuth, ~30° above horizon.
                // This ensures moonlight shines downward onto rooftops and streets rather than
                // upward (which was the -toSun direction when toSun.Y < 0).
                Vector3 antiHoriz = new Vector3(-toSun.x, 0f, -toSun.z);
                if (antiHoriz.sqrMagnitude < 0.0001f) antiHoriz = Vector3.forward;
                antiHoriz.Normalize();
                float moonElevSin = 0.5f; // ~30° elevation — lights roofs and walls
                Vector3 toMoon = antiHoriz * Mathf.Sqrt(1f - moonElevSin * moonElevSin) + Vector3.up * moonElevSin;
                lightForward = -toMoon.normalized; // rays travel from moon downward into scene

                float moonInt = profile.lighting != null ? profile.lighting.moonlightIntensity : 0.5f;
                float nightT   = Mathf.Clamp01((-alt) / 10f); // full moon brightness by 10° below horizon
                float duskInt  = Mathf.Lerp(0f, moonInt, nightT);
                intensity = duskInt * (1f - 0.4f * cloud);

                col = moonColor;
            }
            else
            {
                // Sun above horizon: normal day path.
                lightForward = -toSun.normalized;

                Color day = Color.Lerp(horizonWarm, midday, Mathf.Clamp01(alt / 22f));
                col = Color.Lerp(moonColor, day, Mathf.Clamp01((alt + 4f) / 9f));

                // Sun and moon are separate light regimes. Reusing the moonlight
                // slider here made the intended soft .55 night default cut the
                // daytime sun nearly in half as well, leaving summer scenes black
                // beneath a bright blue sky.
                float lightScale = Mathf.Clamp(
                    PlayerPrefs.GetFloat("k1lo_daySunIntensity", 1.35f), 0f, 8f);
                float dayInt = Mathf.Lerp(0f, 1.2f, Mathf.Clamp01((alt + 6f) / 30f));
                intensity = dayInt * lightScale * (1f - 0.5f * cloud);
            }

            // Overcast desaturates the key light.
            float grey = (col.r + col.g + col.b) / 3f;
            col = Color.Lerp(col, new Color(grey, grey, grey), cloud * 0.6f);
            light.color = col;
            light.intensity = intensity;

            // Guard against degenerate forward (e.g. exactly at zenith).
            if (lightForward.sqrMagnitude > 0.001f)
                light.transform.rotation = Quaternion.LookRotation(lightForward, Vector3.up);
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
                    float ambientMultiplier = profile.lighting != null && profile.lighting.ambientEnabled
                        ? profile.lighting.ambientIntensity
                        : 0f;
                    RenderSettings.ambientIntensity = 0.22f * ambientMultiplier;
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
            // Below-horizon hemisphere doubles as the skybox-ambient ground bounce:
            // pale ash in daylight so terrain reads hazy dust, near-black at night.
            Color groundHaze = Color.Lerp(new Color(0.06f, 0.07f, 0.08f), new Color(0.45f, 0.45f, 0.40f), dayness);
            _proceduralSky.SetColor("_GroundColor", groundHaze);
            _proceduralSky.SetFloat("_AtmosphereThickness", atmThickness);
            _proceduralSky.SetFloat("_Exposure", Mathf.Max(0.05f, exposure));
            _proceduralSky.SetFloat("_SunSize", sunSize);

            // Ambient follows the sky, but baking GI every frame is expensive — refresh only
            // when the sun has moved a little or enough time passed.
            if (RenderSettings.ambientMode != AmbientMode.Skybox)
                RenderSettings.ambientMode = AmbientMode.Skybox;
            float skyAmbientMultiplier = profile.lighting != null && profile.lighting.ambientEnabled
                ? profile.lighting.ambientIntensity
                : 0f;
            // Daylight must remain readable even if a stale authored profile
            // carried a near-zero ambient multiplier.
            float liveAmbientMultiplier = Mathf.Lerp(skyAmbientMultiplier, Mathf.Max(1.85f, skyAmbientMultiplier), dayness);
            RenderSettings.ambientIntensity = Mathf.Lerp(0.25f, 1f, dayness) * liveAmbientMultiplier;
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
            if (_precip == null) return;
            var em = _precip.emission;
            em.rateOverTime = 0f;
            if (_precip.isEmitting) _precip.Stop(true, ParticleSystemStopBehavior.StopEmitting);
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

            bool bloomEnabled = profile.postFX.bloomEnabled;
            // Keep the Bloom override active even when "disabled". If we make
            // the component inactive, URP can fall back to the default volume
            // profile's Bloom, which makes the scene look like bloom increased.
            _bloom.active = true;
            _bloom.intensity.overrideState = true;
            float solarDayness = PlayerPrefs.GetInt("k1lo_visualNightOverride", 0) == 1
                ? 0f : Mathf.Clamp01((_sunAlt + 4f) / 14f);
            bool solarWorldOverride = PlayerPrefs.GetInt("k1lo_solarWorldOverride", 0) == 1;
            float dayBloomIntensity = Mathf.Clamp(
                PlayerPrefs.GetFloat("k1lo_dayBloomIntensity", 2.0f), 0f, 8f);
            float liveBloomIntensity = Mathf.Lerp(profile.postFX.bloomIntensity, dayBloomIntensity, solarDayness);
            _bloom.intensity.value = bloomEnabled
                ? Mathf.Max(0f, solarWorldOverride ? profile.postFX.bloomIntensity : liveBloomIntensity)
                : 0f;
            _bloom.threshold.overrideState = true;
            _bloom.threshold.value = bloomEnabled ? Mathf.Max(0f, profile.postFX.bloomThreshold) : 999f;
            _bloom.scatter.overrideState = true;
            _bloom.scatter.value = bloomEnabled ? Mathf.Clamp01(profile.postFX.bloomScatter) : 0f;
            _bloom.tint.overrideState = true;
            _bloom.tint.value = profile.postFX.bloomTint;

            // Lens Dirt (subtle overlay)
            if (bloomEnabled && profile.postFX.lensDirtTexture != null)
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
            
            // K1L0's procedural sky and emissive facades already carry fine
            // visual structure. Film grain only muddies edges and compression,
            // so keep the post-process removed even if a stale preference says on.
            profile.postFX.filmGrainEnabled = false;
            _filmGrain.active = false;
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
            float fogDayness = PlayerPrefs.GetInt("k1lo_visualNightOverride", 0) == 1
                ? 0f : Mathf.Clamp01((_sunAlt + 6f) / 14f);

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
                
                float activeScatteringIntensity = Mathf.Lerp(nightFogScatteringIntensity, dayFogScatteringIntensity, fogDayness);
                _cachedVolumetricFogManager.scatteringIntensity = Mathf.Max(0f, activeScatteringIntensity + specularBoost * 2.5f);
                
                _cachedVolumetricFogManager.scatteringAbsorption = fog.scatteringAbsorption;
                _cachedVolumetricFogManager.scatteringTint = scatteringTint;
                _cachedVolumetricFogManager.scatteringHighQuality = PlayerPrefs.GetInt("k1lo_fogV2ScatteringHighQuality", fog.scatteringHighQuality ? 1 : 0) == 1;
                _cachedVolumetricFogManager.downscaling = Mathf.Clamp(PlayerPrefs.GetFloat("k1lo_fogV2Downscaling", _cachedVolumetricFogManager.downscaling), 1f, 8f);
                _cachedVolumetricFogManager.downscalingEdgeDepthThreshold = Mathf.Max(0.0001f, PlayerPrefs.GetFloat("k1lo_fogV2DownscalingEdgeThreshold", _cachedVolumetricFogManager.downscalingEdgeDepthThreshold));
                _cachedVolumetricFogManager.blurPasses = Mathf.Clamp(Mathf.RoundToInt(PlayerPrefs.GetFloat("k1lo_fogV2BlurPasses", _cachedVolumetricFogManager.blurPasses)), 0, 6);
                _cachedVolumetricFogManager.blurDownscaling = Mathf.Clamp(PlayerPrefs.GetFloat("k1lo_fogV2BlurDownscaling", _cachedVolumetricFogManager.blurDownscaling), 1f, 8f);
                _cachedVolumetricFogManager.blurSpread = Mathf.Clamp(PlayerPrefs.GetFloat("k1lo_fogV2BlurSpread", _cachedVolumetricFogManager.blurSpread), 0.1f, 4f);
                _cachedVolumetricFogManager.blurHDR = PlayerPrefs.GetInt("k1lo_fogV2BlurHDR", _cachedVolumetricFogManager.blurHDR ? 1 : 0) == 1;
                _cachedVolumetricFogManager.blurEdgePreserve = PlayerPrefs.GetInt("k1lo_fogV2BlurEdgePreserve", _cachedVolumetricFogManager.blurEdgePreserve ? 1 : 0) == 1;
                _cachedVolumetricFogManager.blurEdgeDepthThreshold = Mathf.Max(0f, PlayerPrefs.GetFloat("k1lo_fogV2BlurEdgeThreshold", _cachedVolumetricFogManager.blurEdgeDepthThreshold));
                _cachedVolumetricFogManager.ditherStrength = Mathf.Clamp(PlayerPrefs.GetFloat("k1lo_fogV2ManagerDither", _cachedVolumetricFogManager.ditherStrength), 0f, 0.2f);
            }

            // Find the VolumetricFog component if not cached
            if (_cachedVolumetricFog == null)
            {
                _cachedVolumetricFog = FindObjectOfType<VolumetricFog>();
                if (_cachedVolumetricFog == null)
                {
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
            fogProfile.raymarchQuality = Mathf.Clamp(Mathf.RoundToInt(PlayerPrefs.GetFloat("k1lo_fogRaymarchQuality", fog.raymarchQuality)), 1, 16);
            fogProfile.raymarchNearStepping = Mathf.Clamp(PlayerPrefs.GetFloat("k1lo_fogV2NearStepping", fog.raymarchNearStepping), 0f, 50f);
            fogProfile.raymarchMinStep = Mathf.Max(0f, PlayerPrefs.GetFloat("k1lo_fogV2MinStep", fog.raymarchMinStep));
            fogProfile.jittering = Mathf.Max(0f, PlayerPrefs.GetFloat("k1lo_fogV2Jittering", fog.jittering));
            fogProfile.dithering = Mathf.Clamp(PlayerPrefs.GetFloat("k1lo_fogV2Dithering", fog.dithering), 0f, 2f);

            // Density & Appearance
            fogProfile.constantDensity = fog.constantDensity;
            fogProfile.noiseStrength = Mathf.Lerp(nightFogNoiseStrength, dayFogNoiseStrength, fogDayness);
            fogProfile.noiseScale = Mathf.Lerp(nightFogNoiseScale, dayFogNoiseScale, fogDayness);
            fogProfile.noiseFinalMultiplier = fog.noiseFinalMultiplier;
            fogProfile.noiseFinalMultiplier = Mathf.Max(0f, PlayerPrefs.GetFloat("k1lo_fogV2NoiseMultiplier", fogProfile.noiseFinalMultiplier));
            fogProfile.useDetailNoise = PlayerPrefs.GetInt("k1lo_fogV2DetailNoise", fogProfile.useDetailNoise ? 1 : 0) == 1;
            fogProfile.detailScale = Mathf.Max(0.001f, PlayerPrefs.GetFloat("k1lo_fogV2DetailScale", fogProfile.detailScale));
            fogProfile.detailStrength = Mathf.Clamp01(PlayerPrefs.GetFloat("k1lo_fogV2DetailStrength", fogProfile.detailStrength));
            fogProfile.detailOffset = PlayerPrefs.GetFloat("k1lo_fogV2DetailOffset", fogProfile.detailOffset);
            fogProfile.density = Mathf.Lerp(nightFogDensity, dayFogDensity, fogDayness);

            // Colors
            float fogOrangeAmount = Mathf.Clamp01(PlayerPrefs.GetFloat("k1lo_fogOrangeAmount", 0f));
            Color orangeFog = new Color(1f, .42f, .16f, 1f);
            Color authoredFog = Color.Lerp(fog.albedo, orangeFog, fogOrangeAmount);
            // Optional live RGB override. Keeping the authored/blended color as
            // each key's default preserves every existing preset while allowing
            // Haze Lab to tune genuinely pink/blue fog without grading the world.
            bool customFogColor = PlayerPrefs.HasKey("k1lo_fogColorRed") ||
                                  PlayerPrefs.HasKey("k1lo_fogColorGreen") ||
                                  PlayerPrefs.HasKey("k1lo_fogColorBlue");
            fogProfile.albedo = new Color(
                Mathf.Clamp01(PlayerPrefs.GetFloat("k1lo_fogColorRed", authoredFog.r)),
                Mathf.Clamp01(PlayerPrefs.GetFloat("k1lo_fogColorGreen", authoredFog.g)),
                Mathf.Clamp01(PlayerPrefs.GetFloat("k1lo_fogColorBlue", authoredFog.b)),
                1f);
            fogProfile.brightness = Mathf.Lerp(nightFogBrightness, dayFogBrightness, fogDayness);
            fogProfile.deepObscurance = fog.deepObscurance;
            fogProfile.specularColor = fog.specularColor;
            fogProfile.specularThreshold = fog.specularThreshold;
            fogProfile.specularIntensity = fog.specularIntensity;
            fogProfile.deepObscurance = Mathf.Clamp(PlayerPrefs.GetFloat("k1lo_fogV2DeepObscurance", fogProfile.deepObscurance), 0f, 2f);
            fogProfile.specularThreshold = Mathf.Clamp01(PlayerPrefs.GetFloat("k1lo_fogV2SpecularThreshold", fogProfile.specularThreshold));
            fogProfile.specularIntensity = Mathf.Clamp01(PlayerPrefs.GetFloat("k1lo_fogV2SpecularIntensity", fogProfile.specularIntensity));

            // Animation
            fogProfile.turbulence = Mathf.Clamp(
                PlayerPrefs.GetFloat("k1lo_fogTurbulence", fog.turbulence), 0f, 10f);
            fogProfile.windDirection = new Vector3(
                PlayerPrefs.GetFloat("k1lo_fogWindX", fog.windDirection.x),
                PlayerPrefs.GetFloat("k1lo_fogWindY", fog.windDirection.y),
                PlayerPrefs.GetFloat("k1lo_fogWindZ", fog.windDirection.z));

            // Directional Light
            fogProfile.lightDiffusionPower = fog.lightDiffusionPower;
            fogProfile.lightDiffusionIntensity = fog.lightDiffusionIntensity;
            fogProfile.lightDiffusionPower = Mathf.Clamp(PlayerPrefs.GetFloat("k1lo_fogV2DiffusionPower", fogProfile.lightDiffusionPower), 1f, 256f);
            fogProfile.lightDiffusionIntensity = Mathf.Max(0f, PlayerPrefs.GetFloat("k1lo_fogV2DiffusionIntensity", fogProfile.lightDiffusionIntensity));
            fogProfile.lightDiffusionBackScatter = fog.lightDiffusionBackScatter;
            fogProfile.diffusionFloor = fog.diffusionFloor;
            fogProfile.lightDiffusionNearDepthAtten = Mathf.Max(0f, PlayerPrefs.GetFloat("k1lo_fogV2DiffusionNearAtten", fogProfile.lightDiffusionNearDepthAtten));
            fogProfile.lightDiffusionBackScatter = Mathf.Clamp01(PlayerPrefs.GetFloat("k1lo_fogV2BackScatter", fogProfile.lightDiffusionBackScatter));
            fogProfile.diffusionFloor = Mathf.Clamp01(PlayerPrefs.GetFloat("k1lo_fogV2DiffusionFloor", fogProfile.diffusionFloor));
            fogProfile.receiveShadows = PlayerPrefs.GetInt("k1lo_fogV2ReceiveShadows", fog.receiveShadows ? 1 : 0) == 1;
            fogProfile.shadowIntensity = Mathf.Clamp01(PlayerPrefs.GetFloat("k1lo_fogV2ShadowIntensity", fog.shadowIntensity));
            fogProfile.shadowCancellation = Mathf.Clamp01(PlayerPrefs.GetFloat("k1lo_fogV2ShadowCancellation", fogProfile.shadowCancellation));
            fogProfile.shadowMaxDistance = Mathf.Max(0f, PlayerPrefs.GetFloat("k1lo_fogV2ShadowMaxDistance", fogProfile.shadowMaxDistance));

            // Light Interaction (Point/Spot)
            _cachedVolumetricFog.enableNativeLights = fog.enableNativeLights;
            _cachedVolumetricFog.nativeLightsMultiplier = fog.nativeLightsMultiplier;
            _cachedVolumetricFog.enablePointLights = fog.enablePointLights;
            _cachedVolumetricFog.enableVoids = fog.enableVoids;
            _cachedVolumetricFog.enableAPV = fog.enableAPV;
            _cachedVolumetricFog.apvIntensityMultiplier = fog.apvIntensityMultiplier;

            // Geometry
            fogProfile.border = fog.border;
            fogProfile.border = Mathf.Clamp(PlayerPrefs.GetFloat("k1lo_fogV2Border", fogProfile.border), 0f, 2f);
            fogProfile.scaleNoiseWithHeight = Mathf.Clamp01(PlayerPrefs.GetFloat("k1lo_fogV2ScaleNoiseWithHeight", fogProfile.scaleNoiseWithHeight));
            fogProfile.customHeight = fog.customHeight;
            fogProfile.height = Mathf.Lerp(nightFogHeight, dayFogHeight, fogDayness);
            fogProfile.verticalOffset = PlayerPrefs.GetFloat("k1lo_fogVerticalOffset", fog.verticalOffset);
            fogProfile.distance = Mathf.Max(0f,
                PlayerPrefs.GetFloat("k1lo_fogDistance", fog.distance));
            fogProfile.distanceFallOff = Mathf.Clamp01(
                PlayerPrefs.GetFloat("k1lo_fogDistanceFallOff", fog.distanceFallOff));
            fogProfile.maxDistance = Mathf.Max(1f,
                PlayerPrefs.GetFloat("k1lo_fogMaxDistance", fog.maxDistance));
            fogProfile.maxDistanceFallOff = Mathf.Clamp01(
                PlayerPrefs.GetFloat("k1lo_fogMaxDistanceFallOff", fog.maxDistanceFallOff));

            // Distant Fog
            fogProfile.distantFog = fog.distantFog;
            fogProfile.distantFogStartDistance = Mathf.Lerp(nightFogDistantStart, dayFogDistantStart, fogDayness);
            fogProfile.distantFogDistanceDensity = Mathf.Lerp(nightFogDistantDensity, dayFogDistantDensity, fogDayness);
            fogProfile.distantFogMaxHeight = fog.distantFogMaxHeight;
            fogProfile.distantFogHeightDensity = fog.distantFogHeightDensity;
            // Daylight horizon fogs to pale radioactive dust; night keeps the authored color.
            Color daylightFog = Color.Lerp(fog.distantFogColor, new Color(0.58f, 0.60f, 0.54f), fogDayness);
            Color authoredDistantFog = Color.Lerp(daylightFog, orangeFog, fogOrangeAmount);
            fogProfile.distantFogColor = customFogColor
                ? Color.Lerp(authoredDistantFog, fogProfile.albedo, fogDayness)
                : authoredDistantFog;
            fogProfile.distantFogDiffusionIntensity = fog.distantFogDiffusionIntensity;
            fogProfile.distantFogBaseAltitude = fog.distantFogBaseAltitude;
            fogProfile.distantFogSymmetrical = fog.distantFogSymmetrical;
            fogProfile.distantFogMaxHeight = Mathf.Max(0f, PlayerPrefs.GetFloat("k1lo_fogV2DistantMaxHeight", fogProfile.distantFogMaxHeight));
            fogProfile.distantFogHeightDensity = Mathf.Max(0f, PlayerPrefs.GetFloat("k1lo_fogV2DistantHeightDensity", fogProfile.distantFogHeightDensity));
            fogProfile.distantFogDiffusionIntensity = Mathf.Max(0f, PlayerPrefs.GetFloat("k1lo_fogV2DistantDiffusion", fogProfile.distantFogDiffusionIntensity));
            fogProfile.distantFogBaseAltitude = PlayerPrefs.GetFloat("k1lo_fogV2DistantBaseAltitude", fogProfile.distantFogBaseAltitude);
            fogProfile.distantFogSymmetrical = PlayerPrefs.GetInt("k1lo_fogV2DistantSymmetrical", fogProfile.distantFogSymmetrical ? 1 : 0) == 1;
            fogProfile.distantFogTransparencySupport = PlayerPrefs.GetInt("k1lo_fogV2DistantTransparency", fogProfile.distantFogTransparencySupport ? 1 : 0) == 1;
            fogProfile.distantFogNoise = PlayerPrefs.GetInt("k1lo_fogV2DistantNoise", fog.distantFogNoise ? 1 : 0) == 1;
            fogProfile.distantFogDistanceNoiseScale = Mathf.Max(0.01f, PlayerPrefs.GetFloat("k1lo_fogV2DistantNoiseScale", fog.distantFogDistanceNoiseScale));
            fogProfile.distantFogDistanceNoiseStrength = Mathf.Clamp01(PlayerPrefs.GetFloat("k1lo_fogV2DistantNoiseStrength", fog.distantFogDistanceNoiseStrength));
            fogProfile.distantFogDistanceNoiseMaxDistance = Mathf.Max(0f, PlayerPrefs.GetFloat("k1lo_fogV2DistantNoiseMaxDistance", fog.distantFogDistanceNoiseMaxDistance));
            fogProfile.distantFogNoiseWindDirection = new Vector3(
                PlayerPrefs.GetFloat("k1lo_fogV2DistantWindX", fog.distantFogNoiseWindDirection.x),
                PlayerPrefs.GetFloat("k1lo_fogV2DistantWindY", fog.distantFogNoiseWindDirection.y),
                PlayerPrefs.GetFloat("k1lo_fogV2DistantWindZ", fog.distantFogNoiseWindDirection.z));

            // Trigger the fog to update with new settings
            _cachedVolumetricFog.UpdateMaterialPropertiesNow();
        }

        private void ApplyMaterials()
        {
            ApplyRuntimeLandDaylight();
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

                float wallDayness = Mathf.Clamp01((_sunAlt + 4f) / 14f);
                float wallDaylightLift = PlayerPrefs.GetFloat("k1lo_zossWallDaylightLift", 0.55f);
                Color daylightWall = new Color(.18f, .22f, .17f, 1f);
                if (PlayerPrefs.HasKey("k1lo_zossWallValue"))
                {
                    // Player-darkened walls stay crushed at noon: brighten the
                    // chosen color slightly instead of lerping to stock green-gray.
                    // Lift slider (0..1) pulls the target back toward the chosen
                    // wall color so daytime walls don't all read as black.
                    float crushAmount = 0.35f * (1f - wallDaylightLift);
                    daylightWall = Color.Lerp(profile.buildings.zossWallColor, Color.black, crushAmount);
                }
                float wallDaynessBlend = wallDayness * Mathf.Lerp(0.72f, 0.30f, wallDaylightLift);
                Color wallBaseColor = Color.Lerp(profile.buildings.zossWallColor, daylightWall, wallDaynessBlend);
                profile.buildings.zossWallMaterial.SetColor("_BaseColor", wallBaseColor);
                profile.buildings.zossWallMaterial.SetFloat("_Metallic", profile.buildings.zossWallMetallic);
                profile.buildings.zossWallMaterial.SetFloat("_Smoothness", profile.buildings.zossWallSmoothness);

                // Give individual building walls restrained, deterministic dark
                // variation. This only touches renderers using ZossWallMaterial;
                // road renderers and the road material remain completely separate.
                ApplyBuildingWallVariation(wallBaseColor);

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


                // Environment Reflections (disabled in day)
                bool isDayTime = _sunAlt >= -4f;
                if (profile.buildings.zossWallEnvironmentReflections && !isDayTime)
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
                bool buildingLightsEnabled = PlayerPrefs.GetInt("k1lo_buildingLightsEnabled", 1) == 1;
                // Per-window vaporwave palette shader; falls back to URP Lit
                // (old uniform-glow behavior) if it's missing from the build.
                Shader windowShader = Shader.Find("K1L0/ZossWindows");
                if (windowShader != null)
                {
                    if (profile.buildings.zossEmissiveMaterial.shader != windowShader)
                        profile.buildings.zossEmissiveMaterial.shader = windowShader;
                    profile.buildings.zossEmissiveMaterial.SetFloat("_LitFraction", buildingLightsEnabled ? 1f : 0f);
                    profile.buildings.zossEmissiveMaterial.SetFloat("_PaletteMix", WindowPaletteMix);
                    float saturationDayness = Mathf.Clamp01((_sunAlt + 6f) / 14f);
                    profile.buildings.zossEmissiveMaterial.SetFloat("_PaletteSaturation",
                        Mathf.Lerp(WindowPaletteSaturationNight, WindowPaletteSaturation, saturationDayness));
                    profile.buildings.zossEmissiveMaterial.SetFloat("_Warmth", WindowWarmth);
                    profile.buildings.zossEmissiveMaterial.SetFloat("_AccentFraction", WindowAccentFraction);
                    profile.buildings.zossEmissiveMaterial.SetFloat("_WindowBrightness", WindowBrightness);
                    profile.buildings.zossEmissiveMaterial.SetFloat("_BrightnessJitter",
                        PlayerPrefs.GetFloat("k1lo_zossBrightnessJitter", 0.5f));
                    profile.buildings.zossEmissiveMaterial.SetFloat("_BrightnessJitterRate",
                        PlayerPrefs.GetFloat("k1lo_zossBrightnessJitterRate", 0.6f));
                }
                else if (profile.buildings.zossEmissiveMaterial.shader.name != "Universal Render Pipeline/Lit")
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
                float windowDayness = PlayerPrefs.GetInt("k1lo_visualNightOverride", 0) == 1
                    ? 0f : Mathf.Clamp01((_sunAlt + 6f) / 14f);
                float dayWindowIntensity = Mathf.Clamp(
                    PlayerPrefs.GetFloat("k1lo_zossDayWindowIntensity", 6.5f), 0f, 30f);
                float liveWindowIntensity = Mathf.Lerp(profile.buildings.zossEmissiveIntensity, dayWindowIntensity, windowDayness);
                bool solarWorldOverride = PlayerPrefs.GetInt("k1lo_solarWorldOverride", 0) == 1;
                if (!solarWorldOverride)
                    liveWindowIntensity = Mathf.Max(liveWindowIntensity, Mathf.Lerp(12f, 0f, windowDayness));
                // Permanent emissive floor: day/night/auto and saved overrides
                // may alter the palette, but windows must always visibly glow.
                liveWindowIntensity = buildingLightsEnabled ? Mathf.Max(liveWindowIntensity, 8f) : 0f;
                Color hdrEmission = profile.buildings.zossEmissiveEmission * liveWindowIntensity;
                profile.buildings.zossEmissiveMaterial.SetColor("_EmissionColor", hdrEmission);
                profile.buildings.zossEmissiveMaterial.SetTexture("_EmissionMap", profile.buildings.zossEmissiveEmissionMap);

                if (buildingLightsEnabled)
                {
                    profile.buildings.zossEmissiveMaterial.EnableKeyword("_EMISSION");
                    profile.buildings.zossEmissiveMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                else
                {
                    profile.buildings.zossEmissiveMaterial.DisableKeyword("_EMISSION");
                    profile.buildings.zossEmissiveMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                }

                // Tiling
                profile.buildings.zossEmissiveMaterial.SetTextureScale("_BaseMap", profile.buildings.zossEmissiveTiling);
                profile.buildings.zossEmissiveMaterial.SetTextureScale("_EmissionMap", profile.buildings.zossEmissiveTiling);

                // Glass Properties - Environment Reflections (disabled in day)
                bool isDayTime = _sunAlt >= -4f;
                if (profile.buildings.windowEnvironmentReflections && !isDayTime)
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

        private void ApplyRuntimeLandDaylight()
        {
            if (_cachedLandRenderers == null || _cachedLandRenderers.Length == 0)
            {
                var root = GameObject.Find("land layer objects");
                if (root == null)
                {
                    var runtimeRoot = GameObject.Find("RuntimeObjectsRoot");
                    root = runtimeRoot != null ? FindChildByName(runtimeRoot.transform, "land layer objects")?.gameObject : null;
                }
                if (root != null) _cachedLandRenderers = root.GetComponentsInChildren<MeshRenderer>(true);
            }
            if (_cachedLandRenderers == null) return;
            float dayness = Mathf.Clamp01((_sunAlt + 4f) / 14f);
            if (dayness <= .001f) return;
            Color daylightLand = new Color(.24f, .30f, .20f, 1f);
            foreach (var renderer in _cachedLandRenderers)
            {
                if (renderer == null) continue;
                foreach (var mat in renderer.materials)
                {
                    if (mat == null) continue;
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", daylightLand);
                    if (mat.HasProperty("_Color")) mat.SetColor("_Color", daylightLand);
                    if (mat.HasProperty("_EmissionColor"))
                    {
                        mat.EnableKeyword("_EMISSION");
                        mat.SetColor("_EmissionColor", daylightLand * (.18f * dayness));
                    }
                }
            }
        }

        private static Transform FindChildByName(Transform root, string childName)
        {
            foreach (Transform child in root)
            {
                if (child.name == childName) return child;
                var nested = FindChildByName(child, childName);
                if (nested != null) return nested;
            }
            return null;
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

            float roadDayness = PlayerPrefs.GetInt("k1lo_visualNightOverride", 0) == 1
                ? 0f : Mathf.Clamp01((_sunAlt + 4f) / 14f);
            // Road value slider (0..1) lifts the daytime road color from a
            // near-black asphalt toward warm gray so streets aren't black.
            float nightRoadValue = PlayerPrefs.GetFloat("k1lo_roadValue", 0.88f);
            float dayRoadValue = PlayerPrefs.GetFloat("k1lo_dayRoadValue", 0.32f);
            float roadValue = Mathf.Lerp(nightRoadValue, dayRoadValue, roadDayness);
            Color roadDark = new Color(.20f, .23f, .18f, 1f);
            Color roadLight = new Color(.68f, .66f, .62f, 1f);
            Color daylightRoad = Color.Lerp(roadDark, roadLight, roadValue);
            float roadHue = PlayerPrefs.GetFloat("k1lo_roadHue", .62f);
            float roadSaturation = PlayerPrefs.GetFloat("k1lo_roadSaturation", .08f);
            Color coolNightRoad = Color.HSVToRGB(Mathf.Repeat(roadHue, 1f), Mathf.Clamp01(roadSaturation), roadValue);
            // Keep the road network legible at night. Runtime tile property blocks
            // use the same palette, but this central pass runs repeatedly and used
            // to restore the authored near-black material underneath them.
            Color nightRoad = Color.Lerp(profile.roads.roadColor, coolNightRoad, .72f);
            mat.SetColor("_BaseColor", Color.Lerp(nightRoad, daylightRoad, roadDayness * .78f));
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
            float roadGlow = Mathf.Clamp01(PlayerPrefs.GetFloat("k1lo_roadGlow", .34f));
            Color nightEmission = coolNightRoad * (roadGlow * 1.8f * (1f - roadDayness));
            Color hdrEmission = profile.roads.roadEmission * profile.roads.roadEmissionIntensity + nightEmission;
            mat.SetColor("_EmissionColor", hdrEmission);
            mat.SetTexture("_EmissionMap", profile.roads.roadEmissionMap);

            if (profile.roads.roadEmissionIntensity > 0 || profile.roads.roadEmissionMap != null || roadGlow > .001f)
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

            // Environment Reflections (disabled in day)
            bool isDayTime = _sunAlt >= -4f;
            if (profile.roads.roadEnvironmentReflections && !isDayTime)
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
                mat.SetFloat("_ReflectionStrength", isDayTime ? 0f : profile.roads.reflectionStrength);
                mat.SetFloat("_ReflectionYOffset", profile.roads.reflectionYOffset);
                mat.SetFloat("_ReflectionDistortion", profile.roads.reflectionDistortion);
                mat.SetFloat("_ReflectionWarble", profile.roads.reflectionWarble);
                mat.SetFloat("_ReflectionWarbleScale", profile.roads.reflectionWarbleScale);
                mat.SetColor("_PuddleColor", profile.roads.puddleColor);
                mat.SetFloat("_PuddleScale", profile.roads.puddleFrequency);
                mat.SetFloat("_PuddleSpread", profile.roads.puddleAmount);
                mat.SetFloat("_PuddleSharpness", profile.roads.puddleSharpness);

                // The road no longer samples the opaque camera texture for
                // fake reflections, so keep it in the normal opaque queue.
                if (mat.renderQueue != 2000)
                {
                    mat.renderQueue = 2000;
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

                // Daylight adds a little atmospheric haze, but keeps the authored
                // ground hue strong enough for grass/terrain color tuning to matter.
                // Manual sky preview must own the entire world-lighting phase,
                // not only the sky material. Previously `_sunAlt` continued to
                // carry the real nighttime altitude, leaving the ground dark/
                // olive underneath a forced sunrise. Match the same synthetic
                // solar curve used by DynamicSkyVideoController while previewing.
                float effectiveGroundSunAltitude = _sunAlt;
                bool manualSkyPreview = TestSkyOverrideEnabled
                    || PlayerPrefs.GetFloat("k1lo_layeredBypassWeather", 0f) > .5f;
                if (manualSkyPreview)
                {
                    float previewHour = PlayerPrefs.GetFloat("k1lo_manualHour", ManualHour);
                    effectiveGroundSunAltitude = Mathf.Sin((previewHour - 6f) / 24f * Mathf.PI * 2f) * 62f;
                }
                float groundDayness = PlayerPrefs.GetInt("k1lo_visualNightOverride", 0) == 1
                    ? 0f : Mathf.Clamp01((effectiveGroundSunAltitude + 6f) / 14f);
                
                // Day vs Night ground color interpolation
                Color.RGBToHSV(profile.ground.groundColor, out _, out _, out float gv);
                gv = Mathf.Clamp01(PlayerPrefs.GetFloat("k1lo_groundValue", gv));
                if (gv < 0.05f) gv = 0.5f;
                float gh = Mathf.Lerp(nightGroundHue, dayGroundHue, groundDayness);
                float gs = Mathf.Lerp(nightGroundSaturation, dayGroundSaturation, groundDayness);
                float fogOrangeAmount = Mathf.Clamp01(PlayerPrefs.GetFloat("k1lo_fogOrangeAmount", 0f));
                bool radioactiveDay = groundDayness > .5f && fogOrangeAmount > .05f;
                float daylightValue = radioactiveDay ? Mathf.Max(gv, 0.08f) : Mathf.Max(gv, 0.38f);
                Color activeBaseColor = Color.HSVToRGB(gh, gs, Mathf.Lerp(gv, daylightValue, groundDayness));

                // Cool neutral slate keeps sunrise/sunset ground from turning
                // muddy olive beneath a saturated red-orange horizon.
                Color dayAsh = new Color(0.46f, 0.48f, 0.52f);
                float daylightHaze = radioactiveDay ? 0.025f : Mathf.Lerp(0.24f, 0.10f, Mathf.Clamp01(gs));
                Color groundColor = Color.Lerp(activeBaseColor, dayAsh, groundDayness * daylightHaze);
                float authoredGroundBrightness = Mathf.Clamp(
                    PlayerPrefs.GetFloat("k1lo_groundBrightness", profile.ground.groundBrightness), 0f, 3f);
                float daylightBrightness = radioactiveDay
                    ? authoredGroundBrightness
                    : Mathf.Max(authoredGroundBrightness, 1.18f);
                float groundBrightness = Mathf.Lerp(authoredGroundBrightness, daylightBrightness, groundDayness);

                _cachedGroundPlane.UpdateMaterial(
                    color: groundColor,
                    smoothness: profile.ground.groundSmoothness,
                    brightness: groundBrightness,
                    metallic: profile.ground.groundMetallic,
                    albedo: profile.ground.groundTexture,
                    normal: profile.ground.groundNormal,
                    normalStrength: profile.ground.groundNormalStrength,
                    emission: radioactiveDay
                        ? Color.Lerp(profile.ground.groundEmission, new Color(.045f, .18f, .055f), groundDayness)
                        : Color.Lerp(profile.ground.groundEmission, new Color(.13f, .17f, .23f), groundDayness),
                    emissionMap: profile.ground.groundEmissionMap,
                    emissionIntensity: radioactiveDay
                        ? Mathf.Lerp(profile.ground.groundEmissionIntensity, .38f, groundDayness)
                        : Mathf.Lerp(profile.ground.groundEmissionIntensity, .42f, groundDayness),
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

        private void ApplyBuildingWallVariation(Color baseColor)
        {
#if UNITY_EDITOR
            return;
#else
            // Apply in batches as map tiles stream; never touch thousands of
            // renderer property blocks every frame.
            if (_cachedBuildingRenderers != null && Time.time - _lastBuildingCacheTime < 2f)
                return;

            if (_cachedBuildingRenderers == null || Time.time - _lastBuildingCacheTime >= 2f)
            {
                var buildingRendererSet = new System.Collections.Generic.HashSet<MeshRenderer>();
                foreach (var candidate in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (candidate == null ||
                        candidate.name.IndexOf("building", System.StringComparison.OrdinalIgnoreCase) < 0 ||
                        candidate.name.IndexOf("layer objects", System.StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    foreach (var renderer in candidate.GetComponentsInChildren<MeshRenderer>(true))
                        if (renderer != null) buildingRendererSet.Add(renderer);
                }
                foreach (var metadata in FindObjectsByType<Kiloverse.Mapbox.BuildingMetadata>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (metadata == null) continue;
                    foreach (var renderer in metadata.GetComponentsInChildren<MeshRenderer>(true))
                        if (renderer != null) buildingRendererSet.Add(renderer);
                }
                // Merged distant LOD batches deliberately omit per-building
                // metadata and can sit outside the standard layer root. Their
                // GameObject names still identify them as building batches.
                foreach (var renderer in FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (renderer != null &&
                        renderer.gameObject.name.IndexOf("building", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        buildingRendererSet.Add(renderer);
                }
                _cachedBuildingRenderers = buildingRendererSet.Count > 0
                    ? new System.Collections.Generic.List<MeshRenderer>(buildingRendererSet).ToArray()
                    : System.Array.Empty<MeshRenderer>();
                _lastBuildingCacheTime = Time.time;
            }

            bool buildingsVisible = PlayerPrefs.GetFloat("k1lo_buildingsVisible", 1f) >= 0.5f;
            foreach (var renderer in _cachedBuildingRenderers)
            {
                if (renderer == null || renderer.sharedMaterial == null) continue;
                renderer.forceRenderingOff = !buildingsVisible;
                renderer.enabled = buildingsVisible;
                if (!buildingsVisible) continue;
                string materialName = renderer.sharedMaterial.name;
                if (renderer.sharedMaterial != profile.buildings.zossWallMaterial &&
                    !materialName.Contains("ZossWall")) continue;

                Vector3 center = renderer.bounds.center;
                float hash = Mathf.Abs(Mathf.Sin(center.x * 0.1271f + center.z * 0.3117f));
                float variance = PlayerPrefs.GetFloat("k1lo_zossWallVariance", 0.6f);
                // variance 0 → all same (1.0x); 1 → per-building 0.5x..1.5x
                float lo = 1f - 0.5f * variance;
                float hi = 1f + 0.5f * variance;
                float mult = Mathf.Lerp(lo, hi, hash);
                renderer.GetPropertyBlock(_wallPropertyBlock);
                _wallPropertyBlock.SetColor("_BaseColor", baseColor * mult);
                renderer.SetPropertyBlock(_wallPropertyBlock);
            }
#endif
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
