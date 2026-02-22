using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using KiloWorld.Rendering;
using KiloWorld.Rendering.Systems;
using System.Linq;
using BeamAvatarNamespace = global::BeamAvatar;

namespace KiloWorld.Rendering.Editor
{
    public class KiloSettingsPanel : EditorWindow
    {
        [MenuItem("KiloWorld/Unified Settings Panel")]
        public static void ShowWindow()
        {
            GetWindow<KiloSettingsPanel>("Kilo Settings");
        }

        private Vector2 scrollPosition;
        private UnityEditor.Editor cachedEditor;
        private RenderManager cachedManager;
        private bool showOrbTools = true;
        private bool showReflectionTools = true;
        private bool showGroundTools = false;

        private void OnEnable()
        {
            // Try to find manager on open
            FindManager();
            RefreshOrbDefaults();
        }

        private void FindManager()
        {
            if (cachedManager == null)
            {
                cachedManager = FindObjectOfType<RenderManager>();
            }
        }

        private void OnGUI()
        {
            // 1. Ensure Manager Exists
            FindManager();

            if (cachedManager == null)
            {
                EditorGUILayout.HelpBox("RenderManager not found in the active scene.", MessageType.Error);
                if (GUILayout.Button("Create RenderManager GameObject"))
                {
                    GameObject go = new GameObject("RenderManager");
                    cachedManager = go.AddComponent<RenderManager>();
                    Undo.RegisterCreatedObjectUndo(go, "Create RenderManager");
                    
                    // Try to auto-find a profile to assign
                    string[] guids = AssetDatabase.FindAssets("t:KiloWorldMasterProfile");
                    if (guids.Length > 0)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                        cachedManager.profile = AssetDatabase.LoadAssetAtPath<KiloWorldMasterProfile>(path);
                        Debug.Log("Auto-assigned existing profile: " + cachedManager.profile.name);
                    }
                }
                return;
            }

            // 2. Profile Selection Header
            EditorGUILayout.BeginHorizontal();
            
            // Always allow assigning/changing profile
            EditorGUI.BeginChangeCheck();
            KiloWorldMasterProfile assignedProfile = (KiloWorldMasterProfile)EditorGUILayout.ObjectField("Active Profile", cachedManager.profile, typeof(KiloWorldMasterProfile), false);
            if (EditorGUI.EndChangeCheck())
            {
                cachedManager.profile = assignedProfile;
                EditorUtility.SetDirty(cachedManager);
            }

            if (GUILayout.Button("New", GUILayout.Width(45)))
            {
                CreateNewProfile();
            }

            if (cachedManager.profile != null && GUILayout.Button("Clone", GUILayout.Width(50)))
            {
                CloneCurrentProfile();
            }
            
            EditorGUILayout.EndHorizontal();

            // 3. If no profile, stop here
            if (cachedManager.profile == null)
            {
                EditorGUILayout.HelpBox("Please assign or create a Profile to start editing.", MessageType.Warning);
                
                // Try to auto-find one last time if it's null
                if (GUILayout.Button("Auto-Find Existing Profile"))
                {
                    string[] guids = AssetDatabase.FindAssets("t:KiloWorldMasterProfile");
                    if (guids.Length > 0)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                        cachedManager.profile = AssetDatabase.LoadAssetAtPath<KiloWorldMasterProfile>(path);
                    }
                }
                return;
            }

            // 4. Draw the Profile Inspector
            if (cachedEditor == null || cachedEditor.target != cachedManager.profile)
            {
                UnityEditor.Editor.CreateCachedEditor(cachedManager.profile, null, ref cachedEditor);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("KiloWorld Unified Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            EditorGUI.BeginChangeCheck();
            
            // Draw the default inspector for the ScriptableObject which handles foldouts/headers nicely
            cachedEditor.OnInspectorGUI();

            if (EditorGUI.EndChangeCheck())
            {
                // Apply changes immediately to the scene
                cachedManager.Apply();
                
                // Ensure the SO is marked dirty so changes save to disk
                // (OnInspectorGUI usually handles this for the object, but good to be sure)
            }

            DrawAPISettings();
            DrawBeamAvatarTools();
            DrawReflectionTools();
            DrawGroundTools();
            DrawRendererFeatureStatus();

            EditorGUILayout.EndScrollView();
        }

        private void DrawAPISettings()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Backend API Configuration", EditorStyles.boldLabel);

            if (cachedManager == null || cachedManager.profile == null)
            {
                EditorGUILayout.HelpBox("Profile not found. Please assign a profile to RenderManager.", MessageType.Warning);
                return;
            }

            EditorGUI.BeginChangeCheck();

            // Environment dropdown
            cachedManager.profile.api.environment = (APIManager.APIEnvironment)EditorGUILayout.EnumPopup(
                new GUIContent("Environment", "Select backend environment:\n• Auto: Try all until one works\n• Localhost: http://localhost:3000\n• Tethered: http://172.20.10.5:3000\n• Ngrok: Dynamic tunnel\n• Production: https://api.kilomeme.com"),
                cachedManager.profile.api.environment
            );

            // Show info about current selection
            EditorGUILayout.HelpBox(GetEnvironmentDescription(cachedManager.profile.api.environment), MessageType.Info);

            // Dynamic ngrok URL (read-only display)
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("Dynamic Ngrok URL", string.IsNullOrEmpty(cachedManager.profile.api.dynamicNgrokURL) ? "(not fetched yet)" : cachedManager.profile.api.dynamicNgrokURL);
            EditorGUI.EndDisabledGroup();

            // Refresh ngrok button
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh Ngrok URL from Tethered API"))
            {
                APIManager.Instance.StartCoroutine(APIManager.Instance.RefreshNgrokURL((success, url) => {
                    if (success)
                    {
                        cachedManager.profile.api.dynamicNgrokURL = url;
                        EditorUtility.SetDirty(cachedManager.profile);
                        Debug.Log($"[KiloSettings] ✓ Refreshed ngrok URL: {url}");
                    }
                    else
                    {
                        Debug.LogWarning("[KiloSettings] Failed to refresh ngrok URL");
                    }
                }));
            }
            EditorGUILayout.EndHorizontal();

            // Test connection button
            EditorGUILayout.Space();
            if (GUILayout.Button("Test Connection"))
            {
                TestAPIConnection();
            }

            bool changed = EditorGUI.EndChangeCheck();

            if (changed)
            {
                EditorUtility.SetDirty(cachedManager.profile);
                // Apply environment to APIManager
                APIManager.Instance.SetEnvironment(cachedManager.profile.api.environment);
            }
        }

        private string GetEnvironmentDescription(APIManager.APIEnvironment env)
        {
            switch (env)
            {
                case APIManager.APIEnvironment.Auto:
                    return "Auto: Tries Localhost → Tethered → Ngrok → Production until one responds.";
                case APIManager.APIEnvironment.Localhost:
                    return "Localhost: http://localhost:3000 (local development server)";
                case APIManager.APIEnvironment.Tethered:
                    return "Tethered: http://172.20.10.5:3000 (iPhone hotspot connection)";
                case APIManager.APIEnvironment.Ngrok:
                    return "Ngrok: Dynamic tunnel URL (fetched from tethered API or hardcoded fallback)";
                case APIManager.APIEnvironment.Production:
                    return "Production: https://api.kilomeme.com (live production API)";
                default:
                    return "";
            }
        }

        private void TestAPIConnection()
        {
            Debug.Log("[KiloSettings] Testing API connection...");
            APIManager.Instance.SetEnvironment(cachedManager.profile.api.environment);

            APIManager.Instance.StartCoroutine(APIManager.Instance.Get("/ping", (success, response) => {
                if (success)
                {
                    Debug.Log($"[KiloSettings] ✓ API connection successful! Response: {response}");
                    EditorUtility.DisplayDialog("API Test", $"✓ Connection successful!\n\nURL: {APIManager.Instance.GetBaseURL()}\n\nResponse: {response}", "OK");
                }
                else
                {
                    Debug.LogError($"[KiloSettings] ✗ API connection failed: {response}");
                    EditorUtility.DisplayDialog("API Test", $"✗ Connection failed!\n\nURL: {APIManager.Instance.GetBaseURL()}\n\nError: {response}", "OK");
                }
            }));
        }

        private void DrawRendererFeatureStatus()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Renderer Feature Status", EditorStyles.boldLabel);

            var urpAsset = UniversalRenderPipeline.asset;
            if (urpAsset == null)
            {
                EditorGUILayout.HelpBox("No Active URP Asset found in Graphics Settings!", MessageType.Error);
                return;
            }

            // Get the default renderer data (ScriptableRendererData)
            // Note: URP Asset stores a list of renderer data. We assume index 0 is default or active.
            var scriptableRendererData = GetDefaultRendererData(urpAsset);
            
            if (scriptableRendererData == null)
            {
                EditorGUILayout.HelpBox("Could not access Default Renderer Data.", MessageType.Warning);
                return;
            }

            bool ssaoFound = false;
            bool ssgiFound = false;
            bool ssrFound = false;

            foreach (var feature in scriptableRendererData.rendererFeatures)
            {
                if (feature == null) continue;
                string name = feature.GetType().Name;
                if (name.Contains("ScreenSpaceAmbientOcclusion") || name.Contains("SSAO")) ssaoFound = true;
                if (name.Contains("GlobalIllumination") || name.Contains("SSGI")) ssgiFound = true;
                if (name.Contains("ScreenSpaceReflection") || name.Contains("SSR")) ssrFound = true;
            }

            DrawStatusLabel("SSAO", ssaoFound);
            DrawStatusLabel("SSGI (Fake/Real)", ssgiFound);
            DrawStatusLabel("SSR", ssrFound);

            EditorGUILayout.Space();
            if (GUILayout.Button("Attempt to Add Missing Features"))
            {
                AddMissingFeatures(scriptableRendererData);
            }
        }

        private void DrawStatusLabel(string label, bool found)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(100));
            GUI.color = found ? Color.green : Color.red;
            EditorGUILayout.LabelField(found ? "INSTALLED" : "MISSING", EditorStyles.boldLabel);
            GUI.color = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        private ScriptableRendererData GetDefaultRendererData(UniversalRenderPipelineAsset asset)
        {
            if (asset == null) return null;
            // Use reflection to get 'm_RendererDataList' because it might be internal/protected
            var field = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                var list = field.GetValue(asset) as ScriptableRendererData[];
                if (list != null && list.Length > 0) return list[0];
            }
            return null; // Fallback or fail
        }

        private void AddMissingFeatures(ScriptableRendererData data)
        {
            if (data == null) return;
            
            Undo.RecordObject(data, "Add Renderer Features");

            // Define target feature names (short names) to find
            string[] targetFeatures = new string[] 
            { 
                "ScreenSpaceAmbientOcclusion", 
                "ScreenSpaceReflections", // Standard SSR (Unity 6 / Newer URP)
                "GlobalIllumination" // Attempt to find standard SSGI
            };

            var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
            bool addedAny = false;

            foreach (var targetName in targetFeatures)
            {
                // 1. Check if already exists in data
                bool exists = data.rendererFeatures.Any(f => f != null && f.GetType().Name.Contains(targetName));
                if (exists) continue;

                // 2. Find the Type across all assemblies
                System.Type foundType = null;
                foreach (var assembly in assemblies)
                {
                    try 
                    {
                        var types = assembly.GetTypes();
                        // Strict name match first
                        foundType = types.FirstOrDefault(t => t.Name == targetName && t.IsSubclassOf(typeof(ScriptableRendererFeature)));
                        if (foundType != null) break;
                        
                        // Loose name match
                        foundType = types.FirstOrDefault(t => t.Name.Contains(targetName) && t.IsSubclassOf(typeof(ScriptableRendererFeature)));
                        if (foundType != null) break;
                    }
                    catch (System.Reflection.ReflectionTypeLoadException)
                    {
                        // Skip assemblies that fail to load some types
                        continue;
                    }
                }

                if (foundType != null)
                {
                    try
                    {
                        var feature = (ScriptableRendererFeature)ScriptableObject.CreateInstance(foundType);
                        feature.name = targetName;
                        feature.Create(); 
                        
                        data.rendererFeatures.Add(feature);
                        AssetDatabase.AddObjectToAsset(feature, data);
                        
                        Debug.Log($"[KiloSettings] SUCCESSFULLY ADDED: {foundType.FullName}");
                        addedAny = true;
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"[KiloSettings] Failed to instantiate {targetName}: {e.Message}");
                    }
                }
                else
                {
                    Debug.LogWarning($"[KiloSettings] Could not find script type for: {targetName}. (If SSGI, check if SURFACE_CACHE define is enabled)");
                }
            }

            if (addedAny)
            {
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssets();
                cachedManager.Apply(); 
            }
        }

        private void CreateNewProfile()
        {
            string path = EditorUtility.SaveFilePanelInProject("Create New Profile", "NewKiloProfile", "asset", "Save Kilo Profile");
            if (!string.IsNullOrEmpty(path))
            {
                KiloWorldMasterProfile asset = ScriptableObject.CreateInstance<KiloWorldMasterProfile>();
                AssetDatabase.CreateAsset(asset, path);
                AssetDatabase.SaveAssets();
                cachedManager.profile = asset;
                EditorUtility.SetDirty(cachedManager);
            }
        }

        private void CloneCurrentProfile()
        {
            string path = EditorUtility.SaveFilePanelInProject("Clone Profile", cachedManager.profile.name + "_Clone", "asset", "Save Clone");
            if (!string.IsNullOrEmpty(path))
            {
                KiloWorldMasterProfile clone = Instantiate(cachedManager.profile);
                AssetDatabase.CreateAsset(clone, path);
                AssetDatabase.SaveAssets();
                cachedManager.profile = clone;
                EditorUtility.SetDirty(cachedManager);
            }
        }

        private void DrawBeamAvatarTools()
        {
            EditorGUILayout.Space();
            showOrbTools = EditorGUILayout.Foldout(showOrbTools, "Particle Beam", true);
            if (!showOrbTools) return;

            var orbs = FindObjectsOfType<BeamAvatarNamespace>(true);
            if (orbs.Length == 0)
            {
                EditorGUILayout.HelpBox("No BeamAvatar components found in the scene.", MessageType.Info);
                if (GUILayout.Button("Refresh"))
                {
                    RefreshOrbDefaults();
                }
                return;
            }

            EditorGUILayout.LabelField($"Editing {orbs.Length} BeamAvatar{(orbs.Length > 1 ? "s" : "")}", EditorStyles.miniLabel);

            if (cachedManager == null || cachedManager.profile == null)
            {
                EditorGUILayout.HelpBox("Profile not found. Please assign a profile to RenderManager.", MessageType.Warning);
                return;
            }

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.LabelField("Light Beam", EditorStyles.boldLabel);
            cachedManager.profile.orbs.particleCount = EditorGUILayout.IntSlider("Particle Count", cachedManager.profile.orbs.particleCount, 0, 5000);
            cachedManager.profile.orbs.particleEmissionColor = EditorGUILayout.ColorField("Beam Color", cachedManager.profile.orbs.particleEmissionColor);
            cachedManager.profile.orbs.particleSpeed = EditorGUILayout.Slider("Upward Speed", cachedManager.profile.orbs.particleSpeed, 0.1f, 20f);
            cachedManager.profile.orbs.particleChaos = EditorGUILayout.Slider("Chaos / Wobble", cachedManager.profile.orbs.particleChaos, 0f, 2f);
            cachedManager.profile.orbs.beamWidth = EditorGUILayout.Slider("Beam Width (meters)", cachedManager.profile.orbs.beamWidth, 0.5f, 5f);
            cachedManager.profile.orbs.particleBaseSize = EditorGUILayout.Slider("Particle Size (meters)", cachedManager.profile.orbs.particleBaseSize, 0.1f, 3f);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Particle Detail Settings", EditorStyles.boldLabel);
            cachedManager.profile.orbs.particleSizeVariation = EditorGUILayout.Slider("Size Variation", cachedManager.profile.orbs.particleSizeVariation, 0.1f, 3f);
            EditorGUILayout.BeginHorizontal();
            cachedManager.profile.orbs.particleSizeOverLifetime = EditorGUILayout.Slider("Size Over Lifetime", cachedManager.profile.orbs.particleSizeOverLifetime, 0f, 2f);
            GUILayout.Label("(1=same, <1=shrink, >1=grow)", EditorStyles.miniLabel, GUILayout.Width(150));
            EditorGUILayout.EndHorizontal();
            cachedManager.profile.orbs.particleFadeIn = EditorGUILayout.Slider("Fade In", cachedManager.profile.orbs.particleFadeIn, 0f, 0.5f);
            cachedManager.profile.orbs.particleFadeOut = EditorGUILayout.Slider("Fade Out", cachedManager.profile.orbs.particleFadeOut, 0f, 0.5f);
            cachedManager.profile.orbs.particleDensity = EditorGUILayout.Slider("Particle Density", cachedManager.profile.orbs.particleDensity, 0.1f, 3f);
            cachedManager.profile.orbs.particleRotationSpeed = EditorGUILayout.Slider("Rotation Speed", cachedManager.profile.orbs.particleRotationSpeed, 0f, 360f);

            bool changed = EditorGUI.EndChangeCheck();

            if (changed)
            {
                EditorUtility.SetDirty(cachedManager.profile);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh From First Beam"))
            {
                RefreshOrbDefaults();
            }
            if (GUILayout.Button("Apply To All BeamAvatars"))
            {
                ApplyOrbSettings(orbs);
            }
            EditorGUILayout.EndHorizontal();

            if (changed)
            {
                ApplyOrbSettings(orbs);
            }
        }

        private void RefreshOrbDefaults()
        {
            // Orb settings now come from profile, no need to refresh from scene
        }

        private void DrawReflectionTools()
        {
            EditorGUILayout.Space();
            showReflectionTools = EditorGUILayout.Foldout(showReflectionTools, "Road Reflections & Puddles", true);
            if (!showReflectionTools) return;

            if (cachedManager == null || cachedManager.profile == null)
            {
                EditorGUILayout.HelpBox("Profile not found. Please assign a profile to RenderManager.", MessageType.Warning);
                return;
            }

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.LabelField("Road Material", EditorStyles.boldLabel);
            cachedManager.profile.roads.roadMaterial = (Material)EditorGUILayout.ObjectField("Road Material", cachedManager.profile.roads.roadMaterial, typeof(Material), false);
            cachedManager.profile.roads.roadColor = EditorGUILayout.ColorField("Road Color", cachedManager.profile.roads.roadColor);
            cachedManager.profile.roads.roadSmoothness = EditorGUILayout.Slider("Smoothness", cachedManager.profile.roads.roadSmoothness, 0f, 1f);
            cachedManager.profile.roads.roadMetallic = EditorGUILayout.Slider("Metallic", cachedManager.profile.roads.roadMetallic, 0f, 1f);
            cachedManager.profile.roads.roadYPosition = EditorGUILayout.Slider("Road Y Position", cachedManager.profile.roads.roadYPosition, -1f, 1f);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Reflection Settings", EditorStyles.boldLabel);
            cachedManager.profile.roads.reflectionStrength = EditorGUILayout.Slider("Reflection Strength", cachedManager.profile.roads.reflectionStrength, 0f, 2f);
            cachedManager.profile.roads.reflectionYOffset = EditorGUILayout.Slider("Y Offset (meters)", cachedManager.profile.roads.reflectionYOffset, 0f, 100f);
            cachedManager.profile.roads.reflectionDistortion = EditorGUILayout.Slider("Distortion (Light Scatter)", cachedManager.profile.roads.reflectionDistortion, 0f, 5f);
            cachedManager.profile.roads.reflectionWarble = EditorGUILayout.Slider("Warble Amount", cachedManager.profile.roads.reflectionWarble, 0f, 0.1f);
            cachedManager.profile.roads.reflectionWarbleScale = EditorGUILayout.Slider("Warble Scale", cachedManager.profile.roads.reflectionWarbleScale, 1f, 20f);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Puddle Settings", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Frequency = noise scale (higher = smaller features). Amount = coverage threshold. Sharpness = edge definition.", MessageType.Info);
            cachedManager.profile.roads.puddleColor = EditorGUILayout.ColorField("Puddle Color", cachedManager.profile.roads.puddleColor);
            cachedManager.profile.roads.puddleFrequency = EditorGUILayout.Slider("Frequency (Noise Scale)", cachedManager.profile.roads.puddleFrequency, 0.1f, 10f);
            cachedManager.profile.roads.puddleAmount = EditorGUILayout.Slider("Coverage Threshold", cachedManager.profile.roads.puddleAmount, 0f, 1f);
            cachedManager.profile.roads.puddleSharpness = EditorGUILayout.Slider("Edge Sharpness", cachedManager.profile.roads.puddleSharpness, 1f, 10f);

            bool changed = EditorGUI.EndChangeCheck();

            if (changed)
            {
                EditorUtility.SetDirty(cachedManager.profile);
                cachedManager.Apply(); // Apply immediately to see changes
                Debug.Log($"[KiloSettings] Puddle settings updated: Frequency={cachedManager.profile.roads.puddleFrequency:F2}, Amount={cachedManager.profile.roads.puddleAmount:F2}");
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Force Update Puddles Now"))
            {
                EditorUtility.SetDirty(cachedManager.profile);
                cachedManager.Apply();
                Debug.Log("[KiloSettings] Forced puddle update!");
            }
        }

        private void DrawGroundTools()
        {
            EditorGUILayout.Space();
            showGroundTools = EditorGUILayout.Foldout(showGroundTools, "Ground/Terrain Material", true);
            if (!showGroundTools) return;

            if (cachedManager == null || cachedManager.profile == null)
            {
                EditorGUILayout.HelpBox("Profile not found. Please assign a profile to RenderManager.", MessageType.Warning);
                return;
            }

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.LabelField("Ground Material", EditorStyles.boldLabel);
            cachedManager.profile.ground.groundColor = EditorGUILayout.ColorField("Ground Color", cachedManager.profile.ground.groundColor);
            cachedManager.profile.ground.groundTexture = (Texture2D)EditorGUILayout.ObjectField("Albedo Texture", cachedManager.profile.ground.groundTexture, typeof(Texture2D), false);
            cachedManager.profile.ground.groundNormal = (Texture2D)EditorGUILayout.ObjectField("Normal Map", cachedManager.profile.ground.groundNormal, typeof(Texture2D), false);
            cachedManager.profile.ground.groundTiling = EditorGUILayout.Vector2Field("Tiling", cachedManager.profile.ground.groundTiling);
            cachedManager.profile.ground.groundYPosition = EditorGUILayout.Slider("Ground Y Position", cachedManager.profile.ground.groundYPosition, -1f, 1f);
            cachedManager.profile.ground.waterYPosition = EditorGUILayout.Slider("Water Y Position", cachedManager.profile.ground.waterYPosition, -1f, 1f);
            cachedManager.profile.ground.groundSmoothness = EditorGUILayout.Slider("Smoothness", cachedManager.profile.ground.groundSmoothness, 0f, 1f);
            cachedManager.profile.ground.groundMetallic = EditorGUILayout.Slider("Metallic", cachedManager.profile.ground.groundMetallic, 0f, 1f);
            cachedManager.profile.ground.groundNormalStrength = EditorGUILayout.Slider("Normal Strength", cachedManager.profile.ground.groundNormalStrength, 0f, 1f);

            bool changed = EditorGUI.EndChangeCheck();

            if (changed)
            {
                EditorUtility.SetDirty(cachedManager.profile);
                cachedManager.Apply(); // Apply immediately to see changes
            }
        }

        private void ApplyOrbSettings(BeamAvatarNamespace[] orbs)
        {
            if (orbs == null || orbs.Length == 0) return;
            if (cachedManager == null || cachedManager.profile == null) return;

            Undo.RecordObjects(orbs, "Update Glowing Orbs");

            foreach (var orb in orbs)
            {
                if (orb == null) continue;

                // Copy particle beam settings
                orb.particleCount = cachedManager.profile.orbs.particleCount;
                orb.particleEmissionColor = cachedManager.profile.orbs.particleEmissionColor;
                orb.particleSpeed = cachedManager.profile.orbs.particleSpeed;
                orb.particleChaos = cachedManager.profile.orbs.particleChaos;
                orb.beamWidth = cachedManager.profile.orbs.beamWidth;
                orb.particleBaseSize = cachedManager.profile.orbs.particleBaseSize;

                // Copy particle detail settings
                orb.particleSizeVariation = cachedManager.profile.orbs.particleSizeVariation;
                orb.particleSizeOverLifetime = cachedManager.profile.orbs.particleSizeOverLifetime;
                orb.particleFadeIn = cachedManager.profile.orbs.particleFadeIn;
                orb.particleFadeOut = cachedManager.profile.orbs.particleFadeOut;
                orb.particleDensity = cachedManager.profile.orbs.particleDensity;
                orb.particleRotationSpeed = cachedManager.profile.orbs.particleRotationSpeed;

                // Apply particle settings to update visual appearance
                orb.ApplyParticleSettings();

                EditorUtility.SetDirty(orb);
            }
        }
    }
}
