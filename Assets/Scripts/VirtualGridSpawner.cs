using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Reflection;
using Kiloverse.Mapbox;

/// <summary>
/// Spawns 9 light beams (one per virtual grid cell) by pooling all road geometry points.
/// Uses MERCATOR coordinates for global positioning, not local Unity space.
/// </summary>
public class VirtualGridSpawner : MonoBehaviour
{
    [Header("Grid Settings")]
    [Tooltip("Size of each grid cell in meters")]
    public float cellSize = 150f;

    [Tooltip("How often to update beam positions (seconds)")]
    public float updateInterval = 10f;

    [Header("Spawn Settings")]
    [Tooltip("Light beam prefab to spawn (BeamAvatar with particle beam)")]
    public GameObject beamPrefab;

    [Tooltip("Material for particle beams (prevents shader stripping on mobile)")]
    public Material particleBeamMaterial;

    [Tooltip("Offset to side of road (meters)")]
    public float sideOffset = 1.5f;

    [Tooltip("Height offset above road")]
    public float spawnHeight = 0.02f;

    [Header("Proximity Detection")]
    [Tooltip("Trigger alert when player is within this distance of a beam (meters)")]
    public float proximityTriggerDistance = 5.0f;

    [Tooltip("Cooldown between proximity alerts (seconds)")]
    public float proximityAlertCooldown = 30f;

    [Header("Tap Detection")]
    public float tapDetectionHeight = 10f;
    public float tapColliderRadius = 2f;
    public LayerMask tapRaycastMask = ~0;

    // 9 permanent light beams (reused each update)
    private GameObject[] beams = new GameObject[9];
    public GameObject[] GetBeams() => beams;
    public int GetBeamSeed(int index) => (index >= 0 && index < 9) ? currentBeamSeeds[index] : 0;
    private readonly string[] beamAvatarUrls = new string[9];

    public void SetBeamAvatar(int index, string avatarUrl)
    {
        if (index < 0 || index >= beams.Length) return;
        if (beamAvatarUrls[index] == avatarUrl) return;
        beamAvatarUrls[index] = avatarUrl;
        ApplyBeamAvatar(index);
    }

    private GameObject playerObj;
    private KiloverseMapInfo map;
    private float lastUpdateTime = -999f;
    private float lastCheckTime = -999f;
    private Transform beamContainer;
    private GridCell currentPlayerCell;
    
    // Performance optimization: Cache references and geometry
    private Transform roadLayerFolder;
    // Performance optimization: Cache references and geometry (Mercator for stability)
    private Dictionary<string, List<Vector2d>> cachedCellGeometry;
    private int lastKnownRoadMeshCount = -1;
    private BeamAvatar[] cachedOrbComponents = new BeamAvatar[9];
    
    // Member reference for live updates
    private KiloWorld.Rendering.KiloWorldMasterProfile profile;

    // Proximity detection tracking
    private int lastProximityBeamIndex = -1;
    private float lastProximityAlertTime = -999f;
    public System.Action<int, float, string, string> OnBeamProximityEntered; // Event: (beamIndex, distance, beamName, beamNoun)
    private bool isProcessingTap = false;

    // Respawn timing
    private float respawnIntervalSeconds = 1800f; // Loaded from profile

    void Start()
    {
        StartCoroutine(WaitForMapAndInitialize());
    }

    private System.Collections.IEnumerator WaitForMapAndInitialize()
    {
        while (map == null)
        {
            map = FindFirstObjectByType<KiloverseMapInfo>();
            if (map == null) yield return new WaitForSeconds(1f);
        }
        Debug.Log("[VirtualGridSpawner] KiloverseMapInfo found, initializing beams...");
        InitializeBeams();
    }

    private void InitializeBeams()
    {
        // Create container
        GameObject runtimeRoot = GameObject.Find("RuntimeObjectsRoot");
        beamContainer = new GameObject("VirtualGridBeams").transform;

        if (runtimeRoot != null)
        {
            beamContainer.SetParent(runtimeRoot.transform);
        }

        // Find player
        playerObj = GameObject.Find("Player");
        if (playerObj == null)
        {
            Debug.LogError("[VirtualGridSpawner] Player not found!");
            enabled = false;
            return;
        }

        if (beamPrefab == null)
        {
            Debug.LogError("[VirtualGridSpawner] Beam prefab not assigned!");
            enabled = false;
            return;
        }

        Debug.Log($"[VirtualGridSpawner] Beam prefab assigned: {beamPrefab.name}");

        // Get profile once at the start
        profile = KiloWorld.Rendering.Systems.RenderManager.Instance?.profile;
        if (profile == null)
        {
            Debug.LogWarning($"[VirtualGridSpawner] Profile is null! RenderManager.Instance={(KiloWorld.Rendering.Systems.RenderManager.Instance != null ? "exists" : "null")}");
        }

        // Auto-load particle beam material if not assigned
        if (particleBeamMaterial == null)
        {
            particleBeamMaterial = Resources.Load<Material>("Materials/ParticleBeam");
            if (particleBeamMaterial != null)
            {
                Debug.Log("[VirtualGridSpawner] Auto-loaded ParticleBeam material");
            }
        }

        // Create 9 permanent light beams
        Debug.Log($"[VirtualGridSpawner] beamContainer null? {beamContainer == null}, beamPrefab null? {beamPrefab == null}, about to instantiate 9 beams");
        
        for (int i = 0; i < 9; i++)
        {
            try
            {
                beams[i] = Instantiate(beamPrefab, Vector3.zero, Quaternion.identity, beamContainer);
                beams[i].name = $"LightBeam_{i}";
                beams[i].SetActive(false); // Start hidden
                Debug.Log($"[VirtualGridSpawner] Successfully created beam {i}: {beams[i].name}, active={beams[i].activeSelf}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[VirtualGridSpawner] Failed to instantiate beam {i}: {ex.Message}\n{ex.StackTrace}");
                continue;
            }

            // Cache BeamAvatar component and apply profile settings
            BeamAvatar orb = beams[i].GetComponent<BeamAvatar>();
            cachedOrbComponents[i] = orb;

            // Assign particle beam material to prevent shader stripping
            if (orb != null && particleBeamMaterial != null)
            {
                orb.particleBeamMaterial = particleBeamMaterial;
            }

            // Apply orb settings from profile
            if (profile != null && orb != null)
            {
                orb.glowColor = profile.orbs.glowColor;
                orb.emissionIntensity = profile.orbs.emissionIntensity;
                orb.orbSize = profile.orbs.orbSize;
                
                // Beam settings
                orb.showBeam = profile.orbs.showBeam;
                // Legacy
                orb.beamColor = profile.orbs.beamColor;
                orb.beamEmission = profile.orbs.beamEmission;
                orb.beamWidth = profile.orbs.beamWidth;
                orb.beamHeight = profile.orbs.beamHeight;
                
                // NEW Particle Beam Settings
                orb.particleCount = profile.orbs.particleCount;
                orb.particleEmissionColor = profile.orbs.particleEmissionColor;
                orb.particleSpeed = profile.orbs.particleSpeed;
                orb.particleChaos = profile.orbs.particleChaos;
                orb.beamWidth = profile.orbs.beamWidth;
                orb.particleBaseSize = profile.orbs.particleBaseSize;

                // FORCE REBUILD: Ensure the new particle system is created for this recycled orb
                orb.RebuildBeamSystem();
            }

            // If an avatar was already assigned before the orb was created, apply it now.
            ApplyBeamAvatar(i);

            EnsureTapCollider(beams[i]);
        }

        // Cache road layer folder reference (reuse runtimeRoot from above)
        if (runtimeRoot != null)
        {
            roadLayerFolder = runtimeRoot.transform.Find("road layer objects");
            Debug.Log($"[VirtualGridSpawner] roadLayerFolder found: {roadLayerFolder != null}");
        }

        // Load proximity settings and respawn timing from profile
        if (profile != null)
        {
            proximityTriggerDistance = profile.orbs.proximityTriggerDistance;
            proximityAlertCooldown = profile.orbs.proximityAlertCooldown;
            respawnIntervalSeconds = profile.orbs.respawnIntervalSeconds;
        }

        // Initialize player cell
        Vector2d playerMercator = GetPlayerMercatorPosition();
        currentPlayerCell = GridCell.FromMercatorPosition(playerMercator, cellSize);
    }
    private float lastDiagnosticTime = -999f;

    void Update()
    {
        // Live Update in Editor for rapid iteration (DISABLED for performance)
        /*
        #if UNITY_EDITOR
        if (profile != null)
        {
            UpdateAllBeamsSettings();
        }
        #endif
        */

        // CRITICAL: Wait for GPS before spawning beams (prevents (0,0) coordinate spam)
        if (!GPSLocationController.GPSReady)
        {
            if (Time.frameCount % 300 == 0) // Log every 5 seconds
            {
                Debug.Log("[VirtualGridSpawner] Waiting for GPS to be ready...");
            }
            return;
        }

        if (Time.frameCount % 300 == 0) // Every 5 seconds
        {
            Debug.Log($"[VirtualGridSpawner] Update() ENTRY: playerObj={playerObj != null}, map={map != null}");
        }

        if (playerObj == null || map == null) return;

        HandleTapInput();

        // Check proximity every frame for responsiveness
        // DISABLED: Using tap detection instead
        // CheckBeamProximity();

        // LOGGING DIAGNOSTICS - DISABLED SPAM
        /*
        if (Time.time - lastDiagnosticTime >= 10f)
        {
            lastDiagnosticTime = Time.time;
            LogBeamDiagnostics();
        }
        */

        // Grid updates can be slower (every 1s)
        if (Time.time - lastCheckTime < 1f) return;
        lastCheckTime = Time.time;

        if (Time.frameCount % 300 == 0) // Log every 5 seconds
        {
            Debug.Log($"[VirtualGridSpawner] Update running: beams={beams.Length}, roadLayerFolder={(roadLayerFolder != null ? "OK" : "NULL")}");
        }

        // Get player's current mercator position
        Vector2d playerMercator = GetPlayerMercatorPosition();
        GridCell playerCell = GridCell.FromMercatorPosition(playerMercator, cellSize);

        // Check if player changed cells OR 10 seconds passed
        bool cellChanged = !playerCell.Equals(currentPlayerCell);
        bool timeExpired = Time.time - lastUpdateTime >= updateInterval;

        if (cellChanged || timeExpired)
        {
            currentPlayerCell = playerCell;
            lastUpdateTime = Time.time;
            UpdateAllBeams();
        }
    }

    private void EnsureTapCollider(GameObject beam)
    {
        if (beam == null) return;

        Collider col = beam.GetComponent<Collider>();
        if (col == null)
        {
            CapsuleCollider capsule = beam.AddComponent<CapsuleCollider>();
            capsule.radius = tapColliderRadius;
            capsule.height = tapDetectionHeight;
            capsule.center = new Vector3(0, tapDetectionHeight / 2f, 0);
            capsule.isTrigger = true;
        }
    }

    private void HandleTapInput()
    {
        if (isProcessingTap) return;

        bool tapDetected = false;
        Vector3 inputPosition = Vector3.zero;

        #if UNITY_EDITOR || UNITY_STANDALONE
        if (Input.GetMouseButtonDown(0))
        {
            tapDetected = true;
            inputPosition = Input.mousePosition;
        }
        #else
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            tapDetected = true;
            inputPosition = Input.GetTouch(0).position;
        }
        #endif

        if (!tapDetected) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(inputPosition);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, 1000f, tapRaycastMask, QueryTriggerInteraction.Collide))
        {
            int beamIndex = GetBeamIndexFromHit(hit.collider);
            if (beamIndex >= 0)
            {
                HandleBeamTapped(beamIndex);
            }
        }
    }

    private int GetBeamIndexFromHit(Collider hitCollider)
    {
        if (hitCollider == null) return -1;

        for (int i = 0; i < beams.Length; i++)
        {
            if (beams[i] == null) continue;
            if (hitCollider.transform.IsChildOf(beams[i].transform))
            {
                return i;
            }
        }

        return -1;
    }

    private void HandleBeamTapped(int beamIndex)
    {
        if (beamIndex < 0 || beamIndex >= beams.Length) return;
        if (beams[beamIndex] == null || !beams[beamIndex].activeSelf) return;

        isProcessingTap = true;

        int seed = GetBeamSeed(beamIndex);
        float distance = 0f;
        var player = FindFirstObjectByType<KiloFirstPersonController>();
        if (player != null)
        {
            distance = Vector3.Distance(player.transform.position, beams[beamIndex].transform.position);
        }

        string beamName = BeamNameGenerator.GetNameFromSeed(seed);
        string beamNoun = NounGenerator.GetFromSeed(seed);
        Debug.Log($"[VirtualGridSpawner] Beam {beamIndex} tapped. Seed: {seed}, Name: {beamName}, Noun: {beamNoun}, Distance: {distance:F1}m");

        StartCoroutine(SendTapBeamPing(beamIndex, seed, beamName, beamNoun, distance));

        beams[beamIndex].SetActive(false);
        MarkBeamAsCollected(beamIndex);

        isProcessingTap = false;
    }

    private IEnumerator SendTapBeamPing(int index, int seed, string beamName, string beamNoun, float tapDistance)
    {
        string userId;
        if (DeviceIDManager.Instance != null)
            userId = DeviceIDManager.Instance.DeviceID;
        else
            userId = SystemInfo.deviceUniqueIdentifier;

        double lat = 0, lon = 0;
        var player = FindFirstObjectByType<KiloFirstPersonController>();
        var mapBehaviour = FindFirstObjectByType<KiloverseMapInfo>();

        if (player != null && mapBehaviour != null)
        {
            var mapInfo = mapBehaviour.MapInformation;
            var latLon = player.transform.position.GetGeoPosition(mapInfo.CenterMercator, mapInfo.WorldRelativeScale);
            lat = latLon.Latitude;
            lon = latLon.Longitude;
        }
        else if (Input.location.status == LocationServiceStatus.Running)
        {
            lat = Input.location.lastData.latitude;
            lon = Input.location.lastData.longitude;
        }

        int dailySteps = 0;
        int weeklySteps = 0;
        var pedometer = FindFirstObjectByType<PedometerService>();
        if (pedometer != null)
        {
            dailySteps = pedometer.stepsLast24Hours;
            weeklySteps = pedometer.stepsLast7Days;
        }

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
            ""activeBeam"": {{
                ""distance"": {tapDistStr},
                ""name"": ""{EscapeJson(beamName)}"",
                ""noun"": ""{EscapeJson(beamNoun)}""
            }},
            ""command"": {{
                ""name"": ""tapBeam"",
                ""distance"": {tapDistStr},
                ""beamName"": ""{EscapeJson(beamName)}"",
                ""beamNoun"": ""{EscapeJson(beamNoun)}""
            }},
            ""beamIndex"": {index},
            ""beamSeed"": {seed},
            ""platform"": ""{Application.platform}""
        }}";

        Debug.Log($"[VirtualGridSpawner] Sending tapBeam ping: {json}");

        yield return APIManager.Instance.Post("/ping", json, (success, response) => {
            if (success)
            {
                Debug.Log($"[VirtualGridSpawner] tapBeam ping success: {response}");
            }
            else
            {
                Debug.LogError($"[VirtualGridSpawner] tapBeam ping failed: {response}");
            }
        });
    }

    private string EscapeJson(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private void LogBeamDiagnostics()
    {
        /*
        if (playerObj == null) return;
        Vector3 playerPos = playerObj.transform.position;
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"[VirtualGridSpawner] 📊 DIAGNOSTIC REPORT (T={Time.time:F1})");
        sb.AppendLine($"Player Pos: {playerPos} | Current Cell: {currentPlayerCell.GetCellId()}");
        sb.AppendLine("---------------------------------------------------------------");
        sb.AppendLine("| IDX | ACTIVE | DISTANCE | CELL ID      | WORLD POS            |");
        sb.AppendLine("---------------------------------------------------------------");

        for (int i = 0; i < beams.Length; i++)
        {
            if (beams[i] == null) continue;
            
            bool active = beams[i].activeSelf;
            float dist = Vector3.Distance(playerPos, beams[i].transform.position);
            
            // Re-calculate cell ID for this beam index to verify
            // Note: This assumes the loop order in UpdateAllBeams matches this 0-8 logic
            // 0=NW, 1=N, 2=NE, etc.
            int dx = (i / 3) - 1; 
            int dz = (i % 3) - 1;
            GridCell beamCell = new GridCell(currentPlayerCell.x + dx, currentPlayerCell.z + dz);
            
            sb.AppendLine($"| {i,3} | {active,6} | {dist,8:F1}m | {beamCell.GetCellId(),-12} | {beams[i].transform.position,-20} |");
        }
        sb.AppendLine("---------------------------------------------------------------");
        Debug.Log(sb.ToString());
        */
    }
    
    // Efficiently update visual settings on ALL beams without rebuilding them
    private void UpdateAllBeamsSettings()
    {
        if (profile == null) return;
        
        for (int i = 0; i < beams.Length; i++)
        {
            if (beams[i] != null && beams[i].activeSelf)
            {
                BeamAvatar orb = cachedOrbComponents[i];
                if (orb != null)
                {
                    // Copy settings
                    orb.glowColor = profile.orbs.glowColor;
                    orb.emissionIntensity = profile.orbs.emissionIntensity;
                    orb.orbSize = profile.orbs.orbSize;
                    
                    // Particle Settings
                    orb.particleCount = profile.orbs.particleCount;
                    orb.particleEmissionColor = profile.orbs.particleEmissionColor;
                    orb.particleSpeed = profile.orbs.particleSpeed;
                    orb.particleChaos = profile.orbs.particleChaos;
                    orb.beamWidth = profile.orbs.beamWidth;
                    orb.particleBaseSize = profile.orbs.particleBaseSize;

                    // APPLY (Lightweight)
                    orb.ApplyParticleSettings();
                }
            }
        }
    }

    private void CheckBeamProximity()
    {
        // Respect cooldown between proximity alerts
        if (Time.time - lastProximityAlertTime < proximityAlertCooldown)
        {
            return;
        }

        Vector3 playerPos = playerObj.transform.position;
        int closestBeamIndex = -1;
        float closestDistance = float.MaxValue;
        int activeBeamCount = 0;

        // Check distance to each active beam
        for (int i = 0; i < beams.Length; i++)
        {
            if (beams[i] != null && beams[i].activeSelf)
            {
                activeBeamCount++;
                Vector3 beamPos = beams[i].transform.position;
                float distance = Vector3.Distance(playerPos, beamPos);

                // Track closest beam within trigger distance
                if (distance <= proximityTriggerDistance && distance < closestDistance)
                {
                    closestDistance = distance;
                    closestBeamIndex = i;
                }
            }
        }

        // Fire proximity event if we found a close beam
        if (closestBeamIndex != -1)
        {
            lastProximityBeamIndex = closestBeamIndex;
            lastProximityAlertTime = Time.time;
            
            // Get beam name and noun from seed
            int seed = GetBeamSeed(closestBeamIndex);
            string beamName = BeamNameGenerator.GetNameFromSeed(seed);
            string beamNoun = NounGenerator.GetFromSeed(seed);
            
            OnBeamProximityEntered?.Invoke(closestBeamIndex, closestDistance, beamName, beamNoun);
            Debug.Log($"[VirtualGridSpawner] 🎯 Proximity Alert! Beam {closestBeamIndex} '{beamName}' the {beamNoun} at {closestDistance:F1}m (threshold: {proximityTriggerDistance}m)");
            
            // Mark beam as collected so it doesn't respawn immediately in UpdateAllBeams
            MarkBeamAsCollected(closestBeamIndex);
        }
    }

    // Track collected beams to prevent immediate respawn
    private Dictionary<int, float> collectedBeamTimestamps = new Dictionary<int, float>();
    
    public void MarkBeamAsCollected(int index)
    {
        collectedBeamTimestamps[index] = Time.time;
    }

    // Convert Unity world position to Mercator coordinates
    private Vector2d UnityToMercator(Vector3 unityPosition)
    {
        var centerLatLng = map.MapInformation.Position;
        var centerMercator = Conversions.LatitudeLongitudeToWebMercator(centerLatLng);
        return new Vector2d(
            centerMercator.x + unityPosition.x,
            centerMercator.y + unityPosition.z
        );
    }

    // Convert Mercator coordinates to Unity world position
    private Vector3 MercatorToUnity(Vector2d mercatorPosition)
    {
        var centerLatLng = map.MapInformation.Position;
        var centerMercator = Conversions.LatitudeLongitudeToWebMercator(centerLatLng);
        return new Vector3(
            (float)(mercatorPosition.x - centerMercator.x),
            0,
            (float)(mercatorPosition.y - centerMercator.y)
        );
    }

// Get player's mercator position (works in both Editor and Mobile modes)
    private Vector2d GetPlayerMercatorPosition()
    {
        if (map == null || map.MapInformation == null)
        {
            return new Vector2d(0, 0);
        }
        // In Editor mode: GPS is static, player moves in Unity space
        // On Mobile: GPS updates, player is relatively static in Unity space
        // Solution: Always combine both - use player's Unity offset from origin + map center
        return UnityToMercator(playerObj.transform.position);
    }


    [Tooltip("Max distance to snap a beam to a road. If closest road is further, beam is hidden.")]
    public float maxSnapDistance = 40f;

    // Track stable positions to prevent jitter when map data updates
    private string[] currentBeamCellIds = new string[9];
    private Vector2d[] currentBeamMercatorTargets = new Vector2d[9];
    private int[] currentBeamTimeSlots = new int[9];
    private int[] currentBeamSeeds = new int[9]; // Store deterministic seed for each beam

    private void ApplyBeamAvatar(int index)
    {
        if (index < 0 || index >= beamAvatarUrls.Length) return;
        if (cachedOrbComponents[index] == null) return;
        string avatarUrl = beamAvatarUrls[index];
        if (string.IsNullOrEmpty(avatarUrl)) return;
        cachedOrbComponents[index].SetAvatarUrl(avatarUrl);
    }

    private void UpdateAllBeams()
    {
        // Debug.Log("[VirtualGridSpawner] UpdateAllBeams() CALLED");

        Vector2d playerMercator = GetPlayerMercatorPosition();
        GridCell playerCell = GridCell.FromMercatorPosition(playerMercator, cellSize);

        // Log player world position and map center for debugging
        Vector3 playerWorldPos = playerObj.transform.position;
        var mapLatLng = map.MapInformation.LatitudeLongitude;
        var centerLatLng = new Kiloverse.Mapbox.LatitudeLongitude(mapLatLng.Latitude, mapLatLng.Longitude);
        var mapCenterMercator = Kiloverse.Mapbox.Conversions.LatitudeLongitudeToWebMercator(centerLatLng);
        Vector3 mapWorldPos = map.transform.position;
        // Debug.Log("[VirtualGridSpawner] Player WorldPos=" + playerWorldPos + " | Map WorldPos=" + mapWorldPos + " | Map Center Mercator=" + mapCenterMercator.x.ToString("F1") + "," + mapCenterMercator.y.ToString("F1"));

        // Get geometry (Mercator)
        Dictionary<string, List<Vector2d>> cellGeometry = GetCachedRoadGeometry();
        // Debug.Log($"[VirtualGridSpawner] UpdateAllBeams: cellGeometry has {cellGeometry.Count} cells, {cellGeometry.Values.Sum(v => v.Count)} total points");

        // ALWAYS log player cell and sample cells
        var sampleCells = cellGeometry.Keys.Take(5).ToList();
        string samplesStr = string.Join(", ", sampleCells);
        // Debug.Log("[VirtualGridSpawner] Player Mercator: " + playerMercator.x.ToString("F1") + "," + playerMercator.y.ToString("F1") + " -> cell: " + playerCell.GetCellId() + ", Sample cells: " + samplesStr);

        string date = DateTime.UtcNow.ToString("yyyyMMdd");

        int beamIndex = 0;
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dz = -1; dz <= 1; dz++)
            {
                GridCell cell = new GridCell(playerCell.x + dx, playerCell.z + dz);
                string cellId = cell.GetCellId();
                GameObject beam = beams[beamIndex];

                if (beam == null)
                {
                    beamIndex++;
                    continue;
                }

                // DEBUG: Log cell lookup (ALWAYS for beam 0)
                if (beamIndex == 0)
                {
                    bool hasCell = cellGeometry.ContainsKey(cellId);
                    int pointCount = hasCell ? cellGeometry[cellId].Count : 0;
                    // Debug.Log("[VirtualGridSpawner] Beam 0 cellId: " + cellId + " found=" + hasCell + " points=" + pointCount);
                }

                // Calculate time slot for this cell (rotates every ~30 mins)
                int cellTimeSlot = GetCellTimeSlot(cellId);

                // CHECK: Can we keep the current stable position?
                // Conditions: Same Cell ID + Same Time Slot + Beam is currently active (found road previously)
                bool keepCurrentPosition = (cellId == currentBeamCellIds[beamIndex]) && 
                                         (cellTimeSlot == currentBeamTimeSlots[beamIndex]) &&
                                         beam.activeSelf;

                if (keepCurrentPosition)
                {
                    // FAST PATH: Just update Unity world position from cached Mercator (handles floating origin)
                    Vector3 finalPos = MercatorToUnity(currentBeamMercatorTargets[beamIndex]);
                    finalPos.y += spawnHeight;

                    if (cachedOrbComponents[beamIndex] != null)
                        cachedOrbComponents[beamIndex].SetPosition(finalPos);
                    else
                        beam.transform.position = finalPos;
                }
                else if (cellGeometry.ContainsKey(cellId) && cellGeometry[cellId].Count > 0)
                {
                    // SLOW PATH: Find new position
                    
                    // 1. Generate a DETERMINISTIC Random Target in Mercator Space
                    int seed = (cellId.GetHashCode() + cellTimeSlot * 7919 + date.GetHashCode() * 31).GetHashCode();
                    System.Random rng = new System.Random(seed);
                    
                    // Random offset within the cell (0.0 to 1.0 of cell size)
                    double offsetX = (rng.NextDouble() - 0.5) * cellSize;
                    double offsetY = (rng.NextDouble() - 0.5) * cellSize;
                    
                    Vector2d cellCenterMerc = new Vector2d(
                        (cell.x * cellSize) + (cellSize * 0.5), 
                        (cell.z * cellSize) + (cellSize * 0.5)
                    );
                    Vector2d randomTarget = new Vector2d(cellCenterMerc.x + offsetX, cellCenterMerc.y + offsetY);

                    // 2. Find closest road vertex to this random target
                    List<Vector2d> points = cellGeometry[cellId];
                    Vector2d selectedMercator = points[0];
                    double minDstSq = double.MaxValue;
                    
                    // Strided search for performance (check every 5th point)
                    int stride = 5;
                    for(int p=0; p<points.Count; p+=stride) 
                    {
                        double dX = points[p].x - randomTarget.x;
                        double dY = points[p].y - randomTarget.y;
                        double dSq = dX*dX + dY*dY;
                        
                        if (dSq < minDstSq)
                        {
                            minDstSq = dSq;
                            selectedMercator = points[p];
                        }
                    }

                    // GUARD: Check if the closest road is actually close enough
                    // If the map isn't loaded or there are no roads nearby, this distance will be large.
                    // sqrt(minDstSq) is roughly meters because UnityToMercator adds Unity meters directly.
                    double snapDist = Math.Sqrt(minDstSq);
                    bool validSnap = snapDist <= maxSnapDistance;

                    // ALWAYS log for beam 0 to debug
                    if (beamIndex == 0)
                    {
                        Debug.Log("[VirtualGridSpawner] Beam 0: roadPoints=" + points.Count + " minDist=" + snapDist.ToString("F1") + "m maxSnap=" + maxSnapDistance + "m validSnap=" + validSnap);
                    }

                    if (validSnap)
                    {
                        // CACHE THE RESULT
                        currentBeamCellIds[beamIndex] = cellId;
                        currentBeamTimeSlots[beamIndex] = cellTimeSlot;
                        currentBeamMercatorTargets[beamIndex] = selectedMercator;
                        currentBeamSeeds[beamIndex] = seed; // Store deterministic seed

                        // 3. Convert STABLE Mercator Point to CURRENT Unity Position
                        Vector3 finalPos = MercatorToUnity(selectedMercator);
                        finalPos.y += spawnHeight;

                        // Apply
                        if (cachedOrbComponents[beamIndex] != null)
                            cachedOrbComponents[beamIndex].SetPosition(finalPos);
                        else
                            beam.transform.position = finalPos;

                        // Activation logic...
                        bool isRecentlyCollected = collectedBeamTimestamps.ContainsKey(beamIndex) &&
                                                 (Time.time - collectedBeamTimestamps[beamIndex] < 60f);

                        if (!isRecentlyCollected)
                        {
                            beam.SetActive(true);

                            // Ensure particle system plays when beam is activated
                            if (cachedOrbComponents[beamIndex] != null)
                            {
                                cachedOrbComponents[beamIndex].PlayParticles();
                            }

                            // Log detailed position info for ALL beams
                            Vector3 playerPos = playerObj.transform.position;
                            Vector3 offset = finalPos - playerPos;
                            float dist = offset.magnitude;
                            Debug.Log("[VirtualGridSpawner] ACTIVATED beam " + beamIndex + " | WorldPos=" + finalPos + " | OffsetFromPlayer=" + offset + " | Distance=" + dist.ToString("F1") + "m | Active=" + beam.activeSelf + " | HasBeamAvatar=" + (cachedOrbComponents[beamIndex] != null));
                        }
                        else if (beamIndex == 0)
                        {
                            Debug.Log("[VirtualGridSpawner] Beam 0 recently collected, staying hidden");
                        }
                    }
                    else
                    {
                        // Too far from target -> Map likely not loaded or no roads here.
                        // Hide beam and DO NOT cache (so we retry next frame)
                        beam.SetActive(false);
                        currentBeamCellIds[beamIndex] = "";
                        if (beamIndex < 3) // Log first 3 beams
                        {
                            Debug.Log("[VirtualGridSpawner] Beam " + beamIndex + " HIDDEN: snapDist too far (" + snapDist.ToString("F1") + "m > " + maxSnapDistance + "m)");
                        }
                    }
                }
                else
                {
                    beam.SetActive(false);
                    // Reset cache for this index so we retry next time
                    currentBeamCellIds[beamIndex] = "";
                    if (beamIndex < 3) // Log first 3 beams
                    {
                        // Debug.Log("[VirtualGridSpawner] Beam " + beamIndex + " HIDDEN: no roads in cell " + cellId);
                    }
                }
                beamIndex++;
            }
        }
    }
// Get cached geometry or rebuild if map changed
    private Dictionary<string, List<Vector2d>> GetCachedRoadGeometry()
    {
        if (roadLayerFolder == null)
        {
            if (Time.frameCount % 300 == 0)
            {
                Debug.LogWarning("[VirtualGridSpawner] roadLayerFolder is NULL - cannot cache roads");
            }
            return new Dictionary<string, List<Vector2d>>();
        }

        // Check if road mesh count changed
        MeshFilter[] allRoadMeshes = roadLayerFolder.GetComponentsInChildren<MeshFilter>();

        if (cachedCellGeometry == null || allRoadMeshes.Length != lastKnownRoadMeshCount)
        {
            // Rebuild cache
            if (Time.frameCount % 300 == 0)
            {
                Debug.Log($"[VirtualGridSpawner] Rebuilding road cache: {allRoadMeshes.Length} road meshes found");
            }
            cachedCellGeometry = GatherRoadGeometry();
            lastKnownRoadMeshCount = allRoadMeshes.Length;
        }

        return cachedCellGeometry;
    }


    private Dictionary<string, List<Vector2d>> GatherRoadGeometry()
    {
        Dictionary<string, List<Vector2d>> cellGeometry = new Dictionary<string, List<Vector2d>>();

        // Find RuntimeObjectsRoot
        GameObject runtimeRoot = GameObject.Find("RuntimeObjectsRoot");
        if (runtimeRoot == null)
        {
            Debug.LogWarning("[VirtualGridSpawner] RuntimeObjectsRoot not found!");
            return cellGeometry;
        }

        Transform roadLayerFolder = runtimeRoot.transform.Find("road layer objects");
        if (roadLayerFolder == null)
        {
            Debug.LogWarning("[VirtualGridSpawner] 'road layer objects' not found!");
            return cellGeometry;
        }

        MeshFilter[] allRoadMeshes = roadLayerFolder.GetComponentsInChildren<MeshFilter>(true);
        int processedRoads = 0;

        foreach (MeshFilter mf in allRoadMeshes)
        {
            if (mf.sharedMesh != null)
            {
                ProcessRoadMeshGeometry(mf, cellGeometry);
                processedRoads++;
            }
        }

        int totalPoints = 0;
        foreach (var kvp in cellGeometry)
        {
            totalPoints += kvp.Value.Count;
        }

        Debug.Log($"[VirtualGridSpawner] GatherRoadGeometry: {allRoadMeshes.Length} meshes found, {processedRoads} processed, {totalPoints} total road points, {cellGeometry.Count} cells");

        return cellGeometry;
    }

    // Allowed road classes (only filter out motorways/highways/trunks)
    private static readonly HashSet<string> WalkableRoadClasses = new HashSet<string>
    {
        "residential", "living_street", "pedestrian", "footway", "path",
        "tertiary", "secondary", "primary", "unclassified", "service", "steps",
        "cycleway", "bridleway", "track", "primary_link", "secondary_link", "tertiary_link"
    };

    private static HashSet<string> _loggedRoadClasses = new HashSet<string>();

    private void ProcessRoadMeshGeometry(MeshFilter meshFilter, Dictionary<string, List<Vector2d>> cellGeometry)
    {
        // Check if this road is walkable (exclude highways/motorways)
        GameObject roadObj = meshFilter.gameObject;

        // Try to get metadata component using reflection (avoids compile-time dependency)
        var metadataComponent = roadObj.GetComponent("RoadSegmentMetadata");
        if (metadataComponent != null)
        {
            var roadClassField = metadataComponent.GetType().GetField("roadClass");
            if (roadClassField != null)
            {
                string roadClass = roadClassField.GetValue(metadataComponent) as string;

                // Log each unique road class we encounter (for debugging)
                if (!string.IsNullOrEmpty(roadClass) && !_loggedRoadClasses.Contains(roadClass))
                {
                    _loggedRoadClasses.Add(roadClass);
                    bool allowed = WalkableRoadClasses.Contains(roadClass);
                    Debug.Log($"[VirtualGridSpawner] Road class '{roadClass}': {(allowed ? "ALLOWED" : "FILTERED OUT")}");
                }

                if (!string.IsNullOrEmpty(roadClass) && !WalkableRoadClasses.Contains(roadClass))
                {
                    // Skip highways, motorways, trunks, and primary roads
                    return;
                }
            }
        }
        // If no metadata, allow it (backwards compatibility with old road data)

        Mesh mesh = meshFilter.sharedMesh;
        Vector3[] vertices = mesh.vertices;
        Transform tr = meshFilter.transform;

        // Convert and pool
        foreach (Vector3 localVertex in vertices)
        {
            Vector3 unityWorldPos = tr.TransformPoint(localVertex);

            // KEY CHANGE: Store Mercator (Global Lat/Lon) Coords
            // These are stable and do not change when Unity World Origin shifts
            Vector2d mercatorPos = UnityToMercator(unityWorldPos);

            GridCell cell = GridCell.FromMercatorPosition(mercatorPos, cellSize);
            string cellId = cell.GetCellId();

            if (!cellGeometry.ContainsKey(cellId))
            {
                cellGeometry[cellId] = new List<Vector2d>();
            }

            // Optimization: Filter too-close points?
            // For now, raw dump is fine, the search is fast enough for <10k points
            cellGeometry[cellId].Add(mercatorPos);
        }
    }

    private void LogAllBeamPositions(Vector3 playerPos, GridCell playerCell)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"\n[VirtualGridSpawner] Player in cell {playerCell.GetCellId()} at {playerPos}");
        sb.AppendLine("9 Light Beams:");

        int beamIndex = 0;
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dz = -1; dz <= 1; dz++)
            {
                GridCell cell = new GridCell(playerCell.x + dx, playerCell.z + dz);
                GameObject beam = beams[beamIndex];

                if (beam.activeSelf)
                {
                    Vector3 beamPos = beam.transform.position;
                    Vector3 offset = beamPos - playerPos;
                    float distance = offset.magnitude;
                    sb.AppendLine($"  {beamIndex}: Cell {cell.GetCellId()} | Pos {beamPos} | Offset ({offset.x:F1}, {offset.z:F1}) | {distance:F1}m");
                }
                else
                {
                    sb.AppendLine($"  {beamIndex}: Cell {cell.GetCellId()} | HIDDEN (no roads)");
                }

                beamIndex++;
            }
        }

        Debug.Log(sb.ToString());
    }

    // Get deterministic offset (0-1) for a cell based on its ID
    private float GetCellOffset(string cellId)
    {
        // Use hash to generate deterministic 0-1 value
        int hash = cellId.GetHashCode();
        // Convert to 0-1 range (using modulo to keep positive)
        return (Mathf.Abs(hash % 10000) / 10000f);
    }

    // Get time slot for a specific cell (with deterministic offset)
    private int GetCellTimeSlot(string cellId)
    {
        // Get current Unix time in seconds
        double currentTimeSeconds = (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;

        // Get deterministic offset for this cell (0-1)
        float cellOffset = GetCellOffset(cellId);

        // Apply offset: shift time forward by (offset * respawnInterval)
        // This makes each cell's time slot change at a different time
        double effectiveTime = currentTimeSeconds + (cellOffset * respawnIntervalSeconds);

        // Calculate time slot
        int timeSlot = (int)(effectiveTime / respawnIntervalSeconds);

        return timeSlot;
    }

    // Grid cell structure
    public struct GridCell
    {
        public int x;
        public int z;

        public GridCell(int x, int z)
        {
            this.x = x;
            this.z = z;
        }

        public string GetCellId() => $"{x}_{z}";

        public static GridCell FromMercatorPosition(Vector2d mercatorPos, float cellSize)
        {
            return new GridCell(
                Mathf.FloorToInt((float)(mercatorPos.x / cellSize)),
                Mathf.FloorToInt((float)(mercatorPos.y / cellSize))
            );
        }

        public static GridCell FromWorldPosition(Vector3 pos, float cellSize)
        {
            return new GridCell(
                Mathf.FloorToInt(pos.x / cellSize),
                Mathf.FloorToInt(pos.z / cellSize)
            );
        }

        public override bool Equals(object obj)
        {
            if (!(obj is GridCell)) return false;
            GridCell other = (GridCell)obj;
            return x == other.x && z == other.z;
        }

        public override int GetHashCode()
        {
            return (x * 397) ^ z;
        }

        public override string ToString() => GetCellId();
    }
}
