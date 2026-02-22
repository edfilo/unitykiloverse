using UnityEngine;
using UnityEngine.Serialization;

public class Billboard : MonoBehaviour
{
    public bool reverseFace = false;
    public bool maintainConstantSize = false;
    [FormerlySerializedAs("fixedSize")]
    [Tooltip("Reference distance in world units where scale is ~1 before clamping.")]
    public float referenceDistance = 50f;
    public float minScale = 0.25f;
    public float maxScale = 4f;
    [Tooltip("Hide renderer when farther than this distance from the camera. 0 = no culling.")]
    public float cullDistanceMeters = 0f;

    private Camera mainCamera;
    private Renderer cachedRenderer;
    private Vector3 initialScale = Vector3.one;

    void Start()
    {
        mainCamera = Camera.main;
        initialScale = transform.localScale;
        CacheRenderer();
    }

    void LateUpdate()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;
        if (initialScale == Vector3.zero) initialScale = transform.localScale;
        if (cachedRenderer == null) CacheRenderer();

        float distance = Vector3.Distance(transform.position, mainCamera.transform.position);

        // Distance cull: disable renderer when out of range, keep object active so it can re-enable.
        if (cullDistanceMeters > 0f)
        {
            bool inRange = distance <= cullDistanceMeters;
            if (cachedRenderer != null) cachedRenderer.enabled = inRange;
            if (!inRange) return;
        }

        if (reverseFace)
        {
            transform.LookAt(transform.position - (mainCamera.transform.position - transform.position));
        }
        else
        {
            transform.LookAt(mainCamera.transform);
        }

        // Keep label size roughly constant on screen (optional)
        if (maintainConstantSize)
        {
            // Inverse relation to distance, clamped: near = larger up to maxScale, far = smaller down to minScale.
            float scale = Mathf.Clamp(referenceDistance / Mathf.Max(distance, 0.001f), minScale, maxScale);
            transform.localScale = initialScale * scale;
        }
    }

    private void CacheRenderer()
    {
        if (cachedRenderer != null) return;
        cachedRenderer = GetComponent<Renderer>();
        if (cachedRenderer == null)
        {
            cachedRenderer = GetComponentInChildren<Renderer>();
        }
    }
}
