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
        private Light characterFillLight;
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
            CreateCharacterFillLight();

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
            UpdateCharacterFillLight();
        }

        private void CreateCharacterFillLight()
        {
            if (characterFillLight != null || playerTransform == null || mainCamera == null) return;

            var fillObject = new GameObject("K1L0 Character Fill Light");
            fillObject.transform.SetParent(mainCamera.transform, false);
            fillObject.transform.localPosition = Vector3.zero;
            characterFillLight = fillObject.AddComponent<Light>();
            characterFillLight.type = LightType.Spot;
            characterFillLight.color = new Color(0.88f, 0.93f, 1f);
            characterFillLight.intensity = PlayerPrefs.GetFloat("k1lo_characterFillIntensity", 9f);
            characterFillLight.range = 20f;
            characterFillLight.spotAngle = PlayerPrefs.GetFloat("k1lo_characterFillAngle", 38f);
            characterFillLight.innerSpotAngle = characterFillLight.spotAngle * .63f;
            characterFillLight.shadows = LightShadows.None;
            characterFillLight.renderMode = LightRenderMode.ForcePixel;
        }

        private void UpdateCharacterFillLight()
        {
            if (characterFillLight == null)
            {
                CreateCharacterFillLight();
                return;
            }

            // This is a local avatar-readability light, not the global camera
            // spotlight. Keep it available even when a weather preset disables
            // the map-wide spotlight.
            characterFillLight.enabled = profile != null;
            if (!characterFillLight.enabled || mainCamera == null || playerTransform == null) return;

            characterFillLight.intensity = Mathf.Clamp(
                PlayerPrefs.GetFloat("k1lo_characterFillIntensity", 9f), 0f, 25f);
            characterFillLight.spotAngle = Mathf.Clamp(
                PlayerPrefs.GetFloat("k1lo_characterFillAngle", 38f), 12f, 90f);
            characterFillLight.innerSpotAngle = characterFillLight.spotAngle * .63f;

            float targetHeight = Mathf.Clamp(
                PlayerPrefs.GetFloat("k1lo_characterFillTargetHeight", 1.05f), -1f, 4f);
            Vector3 torso = playerTransform.position + Vector3.up * targetHeight;
            Vector3 toTorso = torso - characterFillLight.transform.position;
            if (toTorso.sqrMagnitude < 0.01f) return;

            characterFillLight.transform.rotation = Quaternion.LookRotation(toTorso.normalized, mainCamera.transform.up);
            characterFillLight.range = Mathf.Max(20f, toTorso.magnitude + 5f);
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
