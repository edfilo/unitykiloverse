// ============================================================================
// REMOVED REFLECTIONS & SHADOWS — recoverable stash
// ----------------------------------------------------------------------------
// Everything below was ripped out of the live Swift HUD + Unity render pipeline
// per the user's request to remove reflections and shadows. Fog and the player
// spotlight (incl. its shadows) were intentionally KEPT. Paste blocks back into
// their original files to restore. Original locations noted per block.
// ============================================================================

// ---------------------------------------------------------------------------
// KiloWorldMasterProfile.cs — SSAOSettings (was at [Header("Screen Space Reflections (SSR)")])
// ---------------------------------------------------------------------------
//             [Header("Screen Space Reflections (SSR)")]
//             public bool ssrEnabled = true;
//             public Resolution ssrResolution = Resolution.Full;
//             public int ssrMaxRaySteps = 48;
//             public float ssrThickness = 0.4f;
//             public bool ssrAccumulation = true;

// ---------------------------------------------------------------------------
// KiloWorldMasterProfile.cs — LightingSettings
// ---------------------------------------------------------------------------
//             [Header("Shadows")]
//             public bool enableShadows = true;
//             [Range(0, 1)] public float shadowStrength = 1.0f;
//             public float shadowDistance = 150f;
//             public UnityEngine.Rendering.Universal.ShadowResolution shadowResolution = UnityEngine.Rendering.Universal.ShadowResolution._2048;
//             [Range(1, 4)] public int shadowCascades = 2;
//
//             [Header("Environment Reflections")]
//             public bool reflectionsEnabled = true;
//             public UnityEngine.Rendering.DefaultReflectionMode reflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Skybox;
//             public Cubemap customReflectionCubemap;
//             [Range(0, 2)] public float reflectionIntensity = 1.0f;
//             public int reflectionBounces = 1;
//
//             [Header("Subtractive Shadows")]
//             [ColorUsage(false, true)] public Color subtractiveShadowColor = new Color(0.1f, 0.1f, 0.25f);

// ---------------------------------------------------------------------------
// RenderManager.cs — LoadPrefs (was ~lines 144-150)
// ---------------------------------------------------------------------------
//             LoadPrefBool("enableShadows", ref lighting.enableShadows);
//             LoadPref("shadowStrength", ref lighting.shadowStrength);
//             LoadPref("shadowDistance", ref lighting.shadowDistance);
//             LoadPrefBool("reflectionsEnabled", ref lighting.reflectionsEnabled);
//             LoadPref("reflectionIntensity", ref lighting.reflectionIntensity);

// ---------------------------------------------------------------------------
// RenderManager.cs — Start() SSR opaque-texture check call + CheckOpaqueTextureEnabled()
// ---------------------------------------------------------------------------
//             // Verify URP has Opaque texture enabled (required for screen-space reflections)
//             CheckOpaqueTextureEnabled();
//
//         private void CheckOpaqueTextureEnabled()
//         {
//             var urpAsset = UniversalRenderPipeline.asset;
//             if (urpAsset != null)
//             {
//                 var property = typeof(UniversalRenderPipelineAsset).GetProperty("supportsCameraOpaqueTexture",
//                     System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
//                 if (property != null)
//                 {
//                     bool opaqueTextureEnabled = (bool)property.GetValue(urpAsset);
//                     if (!opaqueTextureEnabled)
//                         Debug.LogError("[RenderManager] OPAQUE TEXTURE IS DISABLED! ...");
//                     else
//                         Debug.Log("[RenderManager] Opaque Texture is enabled - screen-space reflections ready");
//                 }
//             }
//         }

// ---------------------------------------------------------------------------
// RenderManager.cs — LogReflectionProbes() call + method
// ---------------------------------------------------------------------------
// #if !UNITY_EDITOR
//             LogReflectionProbes();
// #endif
//
//         private void LogReflectionProbes()
//         {
//             var probes = FindObjectsOfType<ReflectionProbe>();
//             if (probes.Length > 0)
//                 foreach (var p in probes)
//                     if (p.enabled)
//                         Debug.Log($"[RenderManager] Found Active Reflection Probe: '{p.name}' ...");
//         }

// ---------------------------------------------------------------------------
// RenderManager.cs — ApplyRendererFeatures() SSR branch + warning
// ---------------------------------------------------------------------------
//             bool foundSSR = false;
//             ...
//                 // --- SSR ---
//                 else if (featureName.Contains("ScreenSpaceReflection") || featureName.Contains("SSR"))
//                 {
//                     foundSSR = true;
//                     feature.SetActive(profile.rendererFeatures.ssrEnabled);
//                     if (profile.rendererFeatures.ssrEnabled)
//                     {
//                         SetFeatureField(feature, "Resolution", (int)profile.rendererFeatures.ssrResolution);
//                         SetFeatureField(feature, "MaxRaySteps", profile.rendererFeatures.ssrMaxRaySteps);
//                         SetFeatureField(feature, "Thickness", profile.rendererFeatures.ssrThickness);
//                         SetFeatureField(feature, "Accumulation", profile.rendererFeatures.ssrAccumulation);
//                     }
//                 }
//             ...
//             if (!foundSSR && profile.rendererFeatures.ssrEnabled)
//                 Debug.LogWarning("[RenderManager] SSR is enabled in profile but no SSR feature found in renderer!");

// ---------------------------------------------------------------------------
// RenderManager.cs — directional-light shadow apply (was ~lines 651-669)
// ---------------------------------------------------------------------------
//                     var shadowType = profile.lighting.enableShadows ? LightShadows.Soft : LightShadows.None;
//                     if (directionalLight.shadows != shadowType)
//                         directionalLight.shadows = shadowType;
//                     if (directionalLight.shadowStrength != profile.lighting.shadowStrength)
//                         directionalLight.shadowStrength = profile.lighting.shadowStrength;
//                 }
//             }
//
//             // Sync Global URP Shadow Settings
//             var urpAsset = UniversalRenderPipeline.asset;
//             if (urpAsset != null)
//             {
//                 if (urpAsset.shadowDistance != profile.lighting.shadowDistance)
//                     urpAsset.shadowDistance = profile.lighting.shadowDistance;
//                 if (urpAsset.shadowCascadeCount != profile.lighting.shadowCascades)
//                     urpAsset.shadowCascadeCount = profile.lighting.shadowCascades;
//             }

// ---------------------------------------------------------------------------
// RenderManager.cs — reflection apply + subtractive shadow color (was ~lines 697-718)
// ---------------------------------------------------------------------------
//             // Apply Reflection Settings
//             bool isDayTime = _sunAlt >= -4f;
//             bool reflectionsActuallyEnabled = profile.lighting.reflectionsEnabled && !isDayTime;
//             RenderSettings.defaultReflectionMode = reflectionsActuallyEnabled
//                 ? profile.lighting.reflectionMode
//                 : DefaultReflectionMode.Skybox;
//             if (profile.lighting.reflectionMode == DefaultReflectionMode.Custom)
//                 RenderSettings.customReflection = profile.lighting.customReflectionCubemap;
//             float effectiveReflectionIntensity = reflectionsActuallyEnabled ? profile.lighting.reflectionIntensity : 0f;
//             if (RenderSettings.reflectionIntensity != effectiveReflectionIntensity)
//                 RenderSettings.reflectionIntensity = effectiveReflectionIntensity;
//             if (RenderSettings.reflectionBounces != profile.lighting.reflectionBounces)
//                 RenderSettings.reflectionBounces = profile.lighting.reflectionBounces;
//             // Subtractive Shadows
//             RenderSettings.subtractiveShadowColor = profile.lighting.subtractiveShadowColor;

// ---------------------------------------------------------------------------
// K1L0ProfileMode.cs — settings rows (were lines 217-222)
// ---------------------------------------------------------------------------
//             y = AddToggle(scrollContent, y, "SHADOWS", "enableShadows", lighting.enableShadows, v => lighting.enableShadows = v);
//             y = AddSlider(scrollContent, y, "SHADOW STR", "shadowStrength", 0f, 1f, lighting.shadowStrength, v => lighting.shadowStrength = Mathf.Clamp01(v));
//             y = AddToggle(scrollContent, y, "REFLECTIONS", "reflectionsEnabled", lighting.reflectionsEnabled, v => lighting.reflectionsEnabled = v);
//             y = AddSlider(scrollContent, y, "REFLECTION", "reflectionIntensity", 0f, 2f, lighting.reflectionIntensity, v => lighting.reflectionIntensity = Mathf.Clamp(v, 0f, 2f));

// ---------------------------------------------------------------------------
// K1L0HUD.cs — setting handlers (were lines 1574-1603)
// ---------------------------------------------------------------------------
//                 case "enableShadows":      profile.lighting.enableShadows = boolValue; SaveBool("enableShadows", boolValue); break;
//                 case "shadowStrength":     profile.lighting.shadowStrength = Mathf.Clamp01(floatValue); SaveFloat("shadowStrength", ...); break;
//                 case "shadowDistance":     profile.lighting.shadowDistance = Mathf.Clamp(floatValue, 0f, 500f); SaveFloat("shadowDistance", ...); break;
//                 case "reflectionsEnabled": profile.lighting.reflectionsEnabled = boolValue; SaveBool("reflectionsEnabled", boolValue); break;
//                 case "reflectionIntensity":profile.lighting.reflectionIntensity = Mathf.Clamp(floatValue, 0f, 2f); SaveFloat("reflectionIntensity", ...); break;

// ---------------------------------------------------------------------------
// K1L0SettingsUITK.cs — settings rows (were lines 279-285)
// ---------------------------------------------------------------------------
//             Toggle(lightingSec, "SHADOWS", "enableShadows", lighting.enableShadows, v => { lighting.enableShadows = v; RenderManager.Instance?.Apply(); });
//             Sliders(lightingSec, "SHADOW STR", "shadowStrength", 0f, 1f, lighting.shadowStrength, v => { ... });
//             Sliders(lightingSec, "SHADOW DIST", "shadowDistance", 0f, 500f, lighting.shadowDistance, v => { ... });
//             Toggle(lightingSec, "REFLECTIONS", "reflectionsEnabled", lighting.reflectionsEnabled, v => { ... });
//             Sliders(lightingSec, "REFLECTION", "reflectionIntensity", 0f, 2f, lighting.reflectionIntensity, v => { ... });

// ---------------------------------------------------------------------------
// K1L0WeatherOverlay.swift — 16 .shadow(...) modifiers + Reflections/Shadows
// @AppStorage, settings rows, defaults, and setUnitySetting sync calls.
// (Individual .shadow() lines are simply deleted inline; grep K1L0WeatherOverlay.swift
//  history at commit 3382d0d to recover exact lines.)
// ---------------------------------------------------------------------------
