using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Simple fix script to enable post-processing on the Main Camera.
/// Attach to any GameObject and it will automatically fix the camera on Awake.
/// Can be safely removed after running once.
/// </summary>
public class EnableCameraPostProcessing : MonoBehaviour
{
    void Awake()
    {
        FixCameraPostProcessing();
    }

    void FixCameraPostProcessing()
    {
        // Find the main camera
        Camera mainCamera = Camera.main;
        
        if (mainCamera == null)
        {
            Debug.LogError("[EnableCameraPostProcessing] No main camera found!");
            return;
        }

        // Get the URP camera data component
        UniversalAdditionalCameraData cameraData = mainCamera.GetComponent<UniversalAdditionalCameraData>();
        
        if (cameraData == null)
        {
            Debug.LogError("[EnableCameraPostProcessing] Camera is missing UniversalAdditionalCameraData component!");
            return;
        }

        // Enable post-processing
        if (!cameraData.renderPostProcessing)
        {
            cameraData.renderPostProcessing = true;
            Debug.Log("✅ [EnableCameraPostProcessing] Post-processing ENABLED on " + mainCamera.name);
            Debug.Log("✅ Cinematic post-processing effects should now be visible!");
        }
        else
        {
            Debug.Log("[EnableCameraPostProcessing] Post-processing was already enabled on " + mainCamera.name);
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Fix Camera Post-Processing")]
    void ManualFix()
    {
        FixCameraPostProcessing();
    }
#endif
}