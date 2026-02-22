using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Kiloverse.Mapbox
{
    /// <summary>
    /// Logs the tallest 2 buildings currently visible/active every 30 seconds.
    /// Helps debug if tall buildings like BNY Mellon (226m) and USX Tower (251m) are loaded.
    /// Auto-creates itself as a singleton on first access.
    /// </summary>
    public class BuildingHeightLogger : MonoBehaviour
{
    private static BuildingHeightLogger _instance;
    public static BuildingHeightLogger Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("BuildingHeightLogger");
                _instance = go.AddComponent<BuildingHeightLogger>();
                DontDestroyOnLoad(go);
                Debug.Log("[BuildingHeightLogger] Auto-created singleton instance");
            }
            return _instance;
        }
    }

    [Header("Settings")]
    [Tooltip("How often to log tallest buildings (seconds)")]
    public float logInterval = 30f;

    [Tooltip("Enable logging")]
    public bool enableLogging = true;

    private float lastLogTime = 0f;
    private bool isLogging = false;
    [Tooltip("Max milliseconds to spend per frame while scanning (editor safety)")]
    public float maxFrameTimeMs = 6f;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        // Defer heavy logging on boot to avoid editor stalls.
        enableLogging = false;
        StartCoroutine(EnableLoggingAfterDelay());
        lastLogTime = Time.time; // prevent immediate heavy scan on boot
        BootDiagnostics.Mark("BuildingHeightLogger.Awake");
    }

    private IEnumerator EnableLoggingAfterDelay()
    {
        BootDiagnostics.Mark("BuildingHeightLogger.enable delay");
        yield return new WaitForSeconds(5f);
        enableLogging = true;
        BootDiagnostics.Mark("BuildingHeightLogger.enable true");
    }

    void Update()
    {
        if (!enableLogging) return;

        if (!isLogging && Time.time - lastLogTime > logInterval)
        {
            lastLogTime = Time.time;
            StartCoroutine(LogTallestBuildingsCoroutine());
        }
    }

    private IEnumerator LogTallestBuildingsCoroutine()
    {
        isLogging = true;
        BootDiagnostics.Mark("BuildingHeightLogger.Log begin");
        float frameStart = Time.realtimeSinceStartup;

        // Find all active building mesh renderers (not parent containers)
        var allRenderers = FindObjectsOfType<MeshRenderer>();
        var buildingData = new List<BuildingInfo>();

        // Debug: Log first 5 building names from tile T7 (14/4551/6176)
        int t7Count = 0;
        foreach (var renderer in allRenderers)
        {
            if (renderer.gameObject.name.Contains("_T7_") && t7Count < 5)
            {
                Debug.Log($"[BuildingHeightLogger] Sample from tile T7: {renderer.gameObject.name}, Height: {renderer.bounds.size.y:F1}m");
                t7Count++;
            }
            if (Time.realtimeSinceStartup - frameStart > maxFrameTimeMs / 1000f)
            {
                frameStart = Time.realtimeSinceStartup;
                yield return null;
            }
        }

        // Search for specific buildings (USX Tower, BNY Mellon)
        int usxCount = 0;
        int bnyCount = 0;
        int tallCount250 = 0;

        foreach (var renderer in allRenderers)
        {
            // Only process enabled renderers
            if (!renderer.enabled) continue;

            var go = renderer.gameObject;

            // Check if it's a building mesh (not parent container or tile)
            if (!go.name.Contains("building") && !go.name.Contains("Building")) continue;
            if (go.name.Contains("Tile") || go.name.Contains("Container")) continue;

            // Buildings are extruded meshes - height is the Y extent of the bounds
            float height = renderer.bounds.size.y;

            // Skip obvious parent objects (tile size is 2446m)
            if (height > 500f) continue;

            // Skip very small objects (likely not buildings)
            if (height < 3f) continue;

            // Count buildings matching USX/BNY heights
            if (height >= 254f && height <= 258f) { usxCount++; tallCount250++; }
            if (height >= 224f && height <= 228f) bnyCount++;

            buildingData.Add(new BuildingInfo
            {
                Name = go.name,
                Height = height,
                Position = go.transform.position,
                WorldPosition = renderer.bounds.center,
                IsRendererEnabled = renderer.enabled
            });

            if (Time.realtimeSinceStartup - frameStart > maxFrameTimeMs / 1000f)
            {
                frameStart = Time.realtimeSinceStartup;
                yield return null;
            }
        }

        
        // Search for U.S. Steel Tower by checking all building GameObjects for names
        Debug.Log($"[BuildingHeightLogger] Searching for 'U.S. Steel Tower' in building layer objects...");
        var buildingLayerRoot = GameObject.Find("building layer objects");
        if (buildingLayerRoot != null)
        {
            int totalBuildingObjects = buildingLayerRoot.transform.childCount;
            Debug.Log($"[BuildingHeightLogger] Found building layer root with {totalBuildingObjects} children");
            
            // Search for USX Tower in first 100 building objects
            for (int i = 0; i < Mathf.Min(totalBuildingObjects, 100); i++)
            {
                var child = buildingLayerRoot.transform.GetChild(i);
                if (child.name.Contains("Steel") || child.name.Contains("USX") || child.name.Contains("4551"))
                {
                    Debug.Log($"[BuildingHeightLogger] POTENTIAL MATCH: {child.name} active={child.gameObject.activeInHierarchy}");
                }
                if (Time.realtimeSinceStartup - frameStart > maxFrameTimeMs / 1000f)
                {
                    frameStart = Time.realtimeSinceStartup;
                    yield return null;
                }
            }
        }
        else
        {
            Debug.LogWarning("[BuildingHeightLogger] 'building layer objects' not found!");
        }
Debug.Log($"[BuildingHeightLogger] Buildings matching expected heights: USX range (254-258m): {usxCount}, BNY range (224-228m): {bnyCount}");

        if (buildingData.Count == 0)
        {
            Debug.LogWarning("[BuildingHeightLogger] No active buildings found!");
            isLogging = false;
            yield break;
        }

        // Sort by height descending
        var tallest = buildingData.OrderByDescending(b => b.Height).Take(2).ToList();

        Vector3 camPos = Camera.main != null ? Camera.main.transform.position : Vector3.zero;

        // Find all POI labels for cross-referencing
        var allPOIs = FindObjectsOfType<GameObject>()
            .Where(go => go.name.Contains("POI") || go.name.Contains("Label") || go.name.Contains("Anchor"))
            .Where(go => go.activeInHierarchy)
            .ToList();
        if (Time.realtimeSinceStartup - frameStart > maxFrameTimeMs / 1000f)
        {
            frameStart = Time.realtimeSinceStartup;
            yield return null;
        }

        Debug.Log($"[BuildingHeightLogger] ===== TALLEST 2 BUILDINGS =====");
        Debug.Log($"[BuildingHeightLogger] Total building meshes: {buildingData.Count} | Camera: {camPos}");

        for (int i = 0; i < tallest.Count; i++)
        {
            var b = tallest[i];
            float distance = Vector3.Distance(camPos, b.WorldPosition);

            // Find nearest POI to this building
            string nearestPOI = "No POI nearby";
            float nearestPOIDist = float.MaxValue;

            foreach (var poi in allPOIs)
            {
                float poiDist = Vector3.Distance(b.WorldPosition, poi.transform.position);
                if (poiDist < nearestPOIDist && poiDist < 100f) // Within 100m
                {
                    nearestPOIDist = poiDist;
                    nearestPOI = $"{poi.name} ({poiDist:F0}m away)";
                }
                if (Time.realtimeSinceStartup - frameStart > maxFrameTimeMs / 1000f)
                {
                    frameStart = Time.realtimeSinceStartup;
                    yield return null;
                }
            }

            Debug.Log($"[BuildingHeightLogger] #{i + 1}: HEIGHT={b.Height:F1}m | DISTANCE={distance:F1}m | NEAR: {nearestPOI}");
            Debug.Log($"[BuildingHeightLogger]        Name={b.Name}");
        }

        // Search for BNY Mellon (226m) and USX Tower (251m) - should be 1300-1400m from Point State Park
        var expectedTowers = buildingData
            .Where(b => b.Height >= 200f && b.Height <= 260f) // Height range for BNY/USX
            .OrderBy(b => Vector3.Distance(camPos, b.WorldPosition))
            .Take(5)
            .ToList();

        if (expectedTowers.Count > 0)
        {
            Debug.Log($"[BuildingHeightLogger] ----- BUILDINGS 200-260m TALL (BNY/USX range) -----");
            foreach (var b in expectedTowers)
            {
                float distance = Vector3.Distance(camPos, b.WorldPosition);

                // Find nearest POI
                string nearestPOI = "No POI nearby";
                float nearestPOIDist = float.MaxValue;
            foreach (var poi in allPOIs)
            {
                float poiDist = Vector3.Distance(b.WorldPosition, poi.transform.position);
                if (poiDist < nearestPOIDist && poiDist < 100f)
                {
                    nearestPOIDist = poiDist;
                    nearestPOI = $"{poi.name} ({poiDist:F0}m)";
                }
                if (Time.realtimeSinceStartup - frameStart > maxFrameTimeMs / 1000f)
                {
                    frameStart = Time.realtimeSinceStartup;
                    yield return null;
                }
            }

                Debug.Log($"[BuildingHeightLogger]   HEIGHT={b.Height:F1}m | DISTANCE={distance:F1}m | POS=({b.WorldPosition.x:F1},{b.WorldPosition.z:F1}) | NEAR: {nearestPOI}");
            }
        }
        else
        {
            Debug.Log($"[BuildingHeightLogger] ----- NO BUILDINGS IN 200-260m RANGE (BNY/USX missing?) -----");
        }

        // Also check for buildings at exactly 1300-1400m (expected BNY/USX distance from Point)
        var distanceRange = buildingData
            .Where(b => b.Height >= 180f) // Lower threshold to catch more
            .Select(b => new { Building = b, Distance = Vector3.Distance(camPos, b.WorldPosition) })
            .Where(x => x.Distance >= 1300f && x.Distance <= 1450f)
            .OrderByDescending(x => x.Building.Height)
            .Take(3)
            .ToList();

        if (distanceRange.Count > 0)
        {
            Debug.Log($"[BuildingHeightLogger] ----- BUILDINGS AT 1300-1450m DISTANCE (Point to downtown) -----");
            foreach (var item in distanceRange)
            {
                Debug.Log($"[BuildingHeightLogger]   HEIGHT={item.Building.Height:F1}m | DISTANCE={item.Distance:F1}m | POS=({item.Building.WorldPosition.x:F1},{item.Building.WorldPosition.z:F1})");
                if (Time.realtimeSinceStartup - frameStart > maxFrameTimeMs / 1000f)
                {
                    frameStart = Time.realtimeSinceStartup;
                    yield return null;
                }
            }
        }

        // Specifically search for USX Tower height (256.3m from Overture)
        var usxMatches = buildingData
            .Where(b => b.Height >= 254f && b.Height <= 258f)
            .OrderBy(b => Vector3.Distance(camPos, b.WorldPosition))
            .Take(3)
            .ToList();

        if (usxMatches.Count > 0)
        {
            Debug.Log($"[BuildingHeightLogger] ----- USX HEIGHT MATCHES (254-258m) -----");
            foreach (var b in usxMatches)
            {
                float distance = Vector3.Distance(camPos, b.WorldPosition);
                Debug.Log($"[BuildingHeightLogger]   HEIGHT={b.Height:F1}m | DISTANCE={distance:F1}m | POS=({b.WorldPosition.x:F1},{b.WorldPosition.z:F1})");
                if (Time.realtimeSinceStartup - frameStart > maxFrameTimeMs / 1000f)
                {
                    frameStart = Time.realtimeSinceStartup;
                    yield return null;
                }
            }
        }
        else
        {
            Debug.Log($"[BuildingHeightLogger] ----- NO BUILDINGS AT USX HEIGHT (254-258m) - TOWER NOT LOADED -----");
        }

        Debug.Log($"[BuildingHeightLogger] ================================");
        BootDiagnostics.Mark("BuildingHeightLogger.Log done");
        isLogging = false;
    }

    private class BuildingInfo
    {
        public string Name;
        public float Height;
        public Vector3 Position;
        public Vector3 WorldPosition;
        public bool IsRendererEnabled;
    }
}
}
