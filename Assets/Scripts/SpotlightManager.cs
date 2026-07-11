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
        private Camera mainCamera;

        private void Awake()
        {
            spotLight = GetComponent<Light>();
            mainCamera = Camera.main;

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

        }
    }
}
