using UnityEngine;
using KiloWorld.Rendering;

namespace KiloWorld
{
    [RequireComponent(typeof(Light))]
    public class SpotlightManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private KiloWorldMasterProfile profile;
        [SerializeField] private Transform playerTransform;

        private Light spotLight;
        private Light experimentalPointLight;
        private Camera mainCamera;

        private void Awake()
        {
            spotLight = GetComponent<Light>();
            mainCamera = Camera.main;

            // Create experimental point light object
            GameObject pointObj = new GameObject("ExperimentalPointLight");
            pointObj.transform.SetParent(transform);
            experimentalPointLight = pointObj.AddComponent<Light>();
            experimentalPointLight.type = LightType.Point;

            // Find player (camera's parent) if not assigned
            if (playerTransform == null)
            {
                if (mainCamera != null && mainCamera.transform.parent != null)
                {
                    // Camera is child of Player, use Player's transform for rotation
                    playerTransform = mainCamera.transform.parent;
                }
                else if (mainCamera != null)
                {
                    // Fallback to camera if no parent
                    playerTransform = mainCamera.transform;
                }
            }
        }

        private void Start()
        {
            // Load profile from RenderManager if not assigned
            if (profile == null)
            {
                profile = KiloWorld.Rendering.Systems.RenderManager.Instance?.profile;
            }

            ApplySpotlightSettings();

            // Debug log
            Debug.Log($"[SpotlightManager] Initialized - Enabled: {spotLight.enabled}, Intensity: {spotLight.intensity}, Range: {spotLight.range}, Type: {spotLight.type}");
            Debug.Log($"[SpotlightManager] Player Transform: {(playerTransform != null ? playerTransform.name : "NULL")}");
            Debug.Log($"[SpotlightManager] Spotlight Color: {spotLight.color}, RenderMode: {spotLight.renderMode}");
        }

        private Transform runtimeRoot;

        private void Update()
        {
            if (profile == null)
            {
                Debug.LogWarning("[SpotlightManager] Profile is null in Update");
                return;
            }

            if (playerTransform == null)
            {
                Debug.LogWarning("[SpotlightManager] PlayerTransform is null in Update");
                return;
            }

            if (mainCamera == null || !mainCamera.isActiveAndEnabled)
            {
                mainCamera = Camera.main;
            }

            Transform aimTransform = mainCamera != null ? mainCamera.transform : playerTransform;

            // Update position relative to player
            if (profile.lighting.spotlightEnabled)
            {
                if (transform.parent != aimTransform)
                {
                    transform.SetParent(aimTransform, false);
                }
                
                // Reset local transform to zero (follow parent exactly)
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
            }

            // Update experimental point light position
            if (experimentalPointLight != null && profile.lighting.enableExperimentalPointLight)
            {
                if (profile.lighting.experimentalLightFollowsBuilding)
                {
                    // Find nearest building
                    if (runtimeRoot == null)
                    {
                        GameObject go = GameObject.Find("RuntimeObjectsRoot");
                        if (go != null) runtimeRoot = go.transform;
                    }

                    Vector3 targetPos = aimTransform.position + aimTransform.forward * profile.lighting.experimentalPointOffsetZ; // Default fallback

                    if (runtimeRoot != null)
                    {
                        float minDst = float.MaxValue;
                        Transform bestBuilding = null;
                        
                        foreach (Transform child in runtimeRoot)
                        {
                            if (child.name.Contains("road")) continue; // Skip roads
                            
                            float dst = Vector3.SqrMagnitude(child.position - aimTransform.position);
                            if (dst < minDst)
                            {
                                minDst = dst;
                                bestBuilding = child;
                            }
                        }

                        if (bestBuilding != null)
                        {
                            // "Middle of first floor window" approximation
                            // Assuming pivot is at bottom or center. 
                            // If bottom, Y+1.5m. If center, Y is already center?
                            // Usually buildings grow up from 0.
                            targetPos = bestBuilding.position;
                            targetPos.y += 1.5f; // First floor window height
                            
                            // Move slightly towards player to be "outside" the wall?
                            // Or leave it inside to light up the window from inside?
                            // "in the middle of the first floor window"
                            // I'll leave it at the center XZ.
                        }
                    }
                    
                    // Detach from camera if following building
                    if (experimentalPointLight.transform.parent != null)
                    {
                        experimentalPointLight.transform.SetParent(null);
                    }
                    experimentalPointLight.transform.position = Vector3.Lerp(experimentalPointLight.transform.position, targetPos, Time.deltaTime * 5f);
                }
                else
                {
                    // Attach back to camera
                    if (experimentalPointLight.transform.parent != transform)
                    {
                        experimentalPointLight.transform.SetParent(transform);
                    }
                    // It's a child of this object, which is at camera position.
                    experimentalPointLight.transform.localPosition = new Vector3(0, 0, profile.lighting.experimentalPointOffsetZ);
                }
            }

            // Apply settings every frame to allow live tuning
            ApplySpotlightSettings();
        }

private void ApplySpotlightSettings()
        {
            if (profile == null || spotLight == null) return;

            // Apply light properties
            spotLight.enabled = profile.lighting.spotlightEnabled;
            spotLight.type = LightType.Directional;
            spotLight.color = profile.lighting.spotlightColor;
            spotLight.intensity = profile.lighting.spotlightIntensity;
            
            // Ensure light affects all layers
            spotLight.cullingMask = -1; // All layers

            // Shadow settings
            spotLight.shadows = profile.lighting.spotlightCastShadows ? LightShadows.Soft : LightShadows.None;
            spotLight.shadowStrength = profile.lighting.spotlightShadowStrength;
            spotLight.shadowBias = profile.lighting.spotlightShadowBias;
            spotLight.shadowNormalBias = profile.lighting.spotlightShadowNormalBias;

            // Apply Experimental Point Light
            if (experimentalPointLight != null)
            {
                experimentalPointLight.enabled = profile.lighting.enableExperimentalPointLight;
                if (experimentalPointLight.enabled)
                {
                    experimentalPointLight.color = profile.lighting.experimentalPointColor;
                    experimentalPointLight.range = profile.lighting.experimentalPointRange;
                    experimentalPointLight.intensity = profile.lighting.experimentalPointIntensity;
                    experimentalPointLight.shadows = LightShadows.None; // Performance
                }
            }
        }
    }
}
