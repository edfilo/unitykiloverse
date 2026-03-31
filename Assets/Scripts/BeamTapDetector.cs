using UnityEngine;
using System.Collections;
using System.Linq;
using Kiloverse.Mapbox;

/// <summary>
/// Detects tap/click on beam base (10m from ground) and sends tapBeam ping
/// </summary>
[RequireComponent(typeof(Collider))]
public class BeamTapDetector : MonoBehaviour
{
    [Header("References")]
    public int beamIndex = -1; // Set by VirtualGridSpawner

    [Header("Settings")]
    public float detectionHeight = 10f; // Height of tappable zone from ground
    public LayerMask beamLayer; // Layer for beams to prevent accidental hits

    private VirtualGridSpawner spawner;
    private Camera mainCamera;
    private bool isProcessingTap = false;

    void Start()
    {
        mainCamera = Camera.main;
        spawner = FindObjectOfType<VirtualGridSpawner>();

        // Ensure collider exists and is trigger
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            // Add a capsule collider for the beam base
            CapsuleCollider capsule = gameObject.AddComponent<CapsuleCollider>();
            capsule.radius = 2f; // 2m radius for easy tapping
            capsule.height = detectionHeight;
            capsule.center = new Vector3(0, detectionHeight / 2f, 0);
            capsule.isTrigger = true;
        }
        else
        {
            col.isTrigger = true;
        }

        Debug.Log($"[BeamTapDetector] Initialized for beam {beamIndex}");
    }

    void Update()
    {
        // Handle mouse click (desktop) or touch (mobile)
        bool tapDetected = false;
        Vector3 inputPosition = Vector3.zero;

        #if UNITY_EDITOR || UNITY_STANDALONE
        // Desktop: Mouse click
        if (Input.GetMouseButtonDown(0))
        {
            tapDetected = true;
            inputPosition = Input.mousePosition;
        }
        #else
        // Mobile: Touch
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            tapDetected = true;
            inputPosition = Input.GetTouch(0).position;
        }
        #endif

        if (tapDetected && !isProcessingTap)
        {
            CheckTapOnBeam(inputPosition);
        }
    }

    void CheckTapOnBeam(Vector3 screenPosition)
    {
        if (mainCamera == null) return;

        // Raycast from screen position
        Ray ray = mainCamera.ScreenPointToRay(screenPosition);
        RaycastHit hit;

        // Check if we hit this beam's collider
        if (Physics.Raycast(ray, out hit, 1000f))
        {
            if (hit.collider.gameObject == gameObject)
            {
                Debug.Log($"[BeamTapDetector] Beam {beamIndex} tapped at {hit.point}");
                OnBeamTapped();
            }
        }
    }

    void OnBeamTapped()
    {
        if (isProcessingTap) return;

        isProcessingTap = true;

        // Get seed from VirtualGridSpawner
        int seed = 0;
        if (spawner != null)
        {
            seed = spawner.GetBeamSeed(beamIndex);
        }

        // Calculate distance from player to beam
        float distance = 0f;
        var player = FindObjectOfType<KiloFirstPersonController>();
        if (player != null)
        {
            distance = Vector3.Distance(player.transform.position, transform.position);
        }

        Debug.Log($"[BeamTapDetector] Beam {beamIndex} collected! Seed: {seed}, Distance: {distance:F1}m");

        // Send tapBeam ping
        StartCoroutine(SendTapBeamPing(beamIndex, seed, distance));

        // Hide beam (same as proximity collection)
        gameObject.SetActive(false);

        // Mark as collected in spawner
        if (spawner != null)
        {
            spawner.MarkBeamAsCollected(beamIndex);
        }

        isProcessingTap = false;
    }

    IEnumerator SendTapBeamPing(int index, int seed, float tapDistance)
    {
        // Get User ID
        string userId;
        if (DeviceIDManager.Instance != null)
            userId = DeviceIDManager.Instance.DeviceID;
        else
            userId = SystemInfo.deviceUniqueIdentifier;

        // Get location
        double lat = 0, lon = 0;
        var player = FindObjectOfType<KiloFirstPersonController>();
        var mapBehaviour = FindObjectOfType<KiloverseMapInfo>();

        if (player != null && mapBehaviour != null && mapBehaviour.MapboxMap != null)
        {
            var mapInfo = mapBehaviour.MapInformation;
            var latLon = player.transform.position.GetGeoPosition(mapInfo.CenterMercator, mapInfo.Scale);
            lat = latLon.Latitude;
            lon = latLon.Longitude;
        }
        else if (Input.location.status == LocationServiceStatus.Running)
        {
            lat = Input.location.lastData.latitude;
            lon = Input.location.lastData.longitude;
        }

        // Get steps
        int dailySteps = 0;
        int weeklySteps = 0;
        var pedometer = FindObjectOfType<PedometerService>();
        if (pedometer != null)
        {
            dailySteps = pedometer.stepsLast24Hours;
            weeklySteps = pedometer.stepsLast7Days;
        }

        // Get nearby POIs (closest 3)
        string nearbyJson = "[]";
        if (TransmitterScanner.Instance != null)
        {
            var allPOIs = TransmitterScanner.Instance.GetAll();
            var closest3 = allPOIs.Take(3).ToList();
            var nearbyList = closest3.Select(poi => {
                int steps = Mathf.RoundToInt(poi.Distance / 0.762f);
                return $"\"{poi.Name} ({steps} steps)\"";
            });
            nearbyJson = string.Join(",", nearbyList);
        }

        // Build JSON payload
        long timestamp = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string tapDistStr = tapDistance.ToString("F1");

        string json = $@"{{
            ""userId"": ""{userId}"",
            ""lastActive"": {timestamp},
            ""coordinates"": {{ ""latitude"": {lat}, ""longitude"": {lon} }},
            ""steps"": {{
                ""daily"": {dailySteps},
                ""weekly"": {weeklySteps}
            }},
            ""nearby"": [{nearbyJson}],
            ""command"": {{
                ""name"": ""tapBeam"",
                ""distance"": {tapDistStr}
            }},
            ""beamIndex"": {index},
            ""beamSeed"": {seed},
            ""platform"": ""{Application.platform}""
        }}";

        Debug.Log($"[BeamTapDetector] Sending tapBeam ping: {json}");

        // Send via APIManager
        yield return APIManager.Instance.Post("/ping", json, (success, response) => {
            if (success)
            {
                Debug.Log($"[BeamTapDetector] tapBeam ping success: {response}");
            }
            else
            {
                Debug.LogError($"[BeamTapDetector] tapBeam ping failed: {response}");
            }
        });
    }
}
