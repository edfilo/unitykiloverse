using System.Collections.Generic;
using UnityEngine;

namespace Kiloverse.Mapbox
{
    /// <summary>
    /// Disables/enables MeshRenderers based on camera frustum and distance.
    /// Keeps tile data cached but stops rendering geometry that's off-screen.
    /// Attach to the same GameObject as OvertureMapManager.
    /// </summary>
    public class TileRendererCuller : MonoBehaviour
{
    [Header("Culling Settings")]
    [Tooltip("Legacy manual frustum toggle. Keep disabled; Unity already performs renderer frustum culling.")]
    public bool enableFrustumCulling = false;

    [Tooltip("Enable distance culling (disable renderers beyond this distance)")]
    public bool enableDistanceCulling = true;

    [Tooltip("Max distance to render geometry (meters)")]
    public float maxRenderDistance = 2000f; // Match camera far plane

    [Tooltip("Buildings beyond this distance use simplified rendering")]
    public float buildingLODDistance = 500f;

    [Tooltip("Roads beyond this distance use simplified rendering")]
    public float roadLODDistance = 800f;

    [Header("Performance")]
    [Tooltip("How often to update culling (seconds)")]
    public float updateInterval = 0.1f;

    [Tooltip("Process this many renderers per frame")]
    public int renderersPerFrame = 50;

    [Header("Debug")]
    public bool showStats = false;
    public bool logCullingChanges = false;

    private Camera _camera;
    private float _lastUpdateTime;
    private List<Renderer> _allRenderers = new List<Renderer>();
    private int _currentRendererIndex = 0;
    private Plane[] _frustumPlanes;

    private int _culledByFrustum = 0;
    private int _culledByDistance = 0;
    private int _activeRenderers = 0;

    private void Start()
    {
        // Unity already omits out-of-frustum renderers from draw submission. Manually
        // changing Renderer.enabled in small batches made geometry remain disabled
        // briefly (or indefinitely after a list rebuild) when the camera rotated.
        enableFrustumCulling = false;
        _camera = Camera.main;
        if (_camera == null)
        {
            Debug.LogWarning("[TileRendererCuller] No main camera found. Culling disabled.");
            enabled = false;
        }
    }

    private void Update()
    {
        if (_camera == null) return;

        // Rebuild renderer list periodically
        if (Time.time - _lastUpdateTime > updateInterval)
        {
            _lastUpdateTime = Time.time;
            RebuildRendererList();
            _frustumPlanes = GeometryUtility.CalculateFrustumPlanes(_camera);
            _currentRendererIndex = 0;
            _culledByFrustum = 0;
            _culledByDistance = 0;
            _activeRenderers = 0;
        }

        // Process renderers incrementally
        ProcessRendererBatch();

        // Show debug stats
        if (showStats && Time.frameCount % 60 == 0)
        {
            int total = _allRenderers.Count;
            Debug.Log($"[TileRendererCuller] Active: {_activeRenderers}/{total}, " +
                      $"Frustum Culled: {_culledByFrustum}, Distance Culled: {_culledByDistance}");
        }
    }

    private void RebuildRendererList()
    {
        _allRenderers.Clear();

        // Find all renderers in tile objects
        var allObjects = FindObjectsOfType<GameObject>();
        foreach (var go in allObjects)
        {
            // Only process Overture/tile geometry
            if (go.name.Contains("Overture") ||
                go.name.Contains("building") ||
                go.name.Contains("road") ||
                go.name.Contains("segment"))
            {
                var renderer = go.GetComponent<Renderer>();
                if (renderer != null && renderer.GetComponent<Camera>() == null)
                {
                    _allRenderers.Add(renderer);
                }
            }
        }

        if (showStats)
        {
            Debug.Log($"[TileRendererCuller] Found {_allRenderers.Count} tile renderers");
        }
    }

    private void ProcessRendererBatch()
    {
        if (_allRenderers.Count == 0) return;

        int processed = 0;
        Vector3 camPos = _camera.transform.position;

        while (processed < renderersPerFrame && _currentRendererIndex < _allRenderers.Count)
        {
            var renderer = _allRenderers[_currentRendererIndex];
            _currentRendererIndex++;

            if (renderer == null)
            {
                processed++;
                continue;
            }

            bool shouldRender = true;
            string cullReason = "";

            // Distance culling
            if (enableDistanceCulling)
            {
                float distance = Vector3.Distance(camPos, renderer.bounds.center);
                if (distance > maxRenderDistance)
                {
                    shouldRender = false;
                    cullReason = "distance";
                    _culledByDistance++;
                }
            }

            // Frustum culling (only if not already culled by distance)
            if (shouldRender && enableFrustumCulling)
            {
                if (!GeometryUtility.TestPlanesAABB(_frustumPlanes, renderer.bounds))
                {
                    shouldRender = false;
                    cullReason = "frustum";
                    _culledByFrustum++;
                }
            }

            // Update renderer state
            if (renderer.enabled != shouldRender)
            {
                renderer.enabled = shouldRender;

                if (logCullingChanges)
                {
                    Debug.Log($"[TileRendererCuller] {renderer.gameObject.name}: " +
                              $"{(shouldRender ? "ENABLED" : $"DISABLED ({cullReason})")}");
                }
            }

            if (shouldRender)
            {
                _activeRenderers++;
            }

            processed++;
        }
    }

    private void OnDrawGizmos()
    {
        if (!showStats || _camera == null) return;

        // Draw culling distance sphere
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(_camera.transform.position, maxRenderDistance);

        // Draw LOD distances
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(_camera.transform.position, buildingLODDistance);

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(_camera.transform.position, roadLODDistance);
    }
}
}
