using UnityEngine;
using UnityEngine.Networking;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Globalization;
using Kiloverse.Mapbox;
using KiloWorld.UI.Stories;

public class UserPresenceManager : MonoBehaviour
{
    [Header("Settings")]
    public float heartbeatInterval = 60f; // Ping every 60 seconds
    public float presenceCheckInterval = 10f; // Check other users every 10 seconds
    public string presencePath = "presence";
    public string storiesCollection = "stories";
    public float immediatePingCooldown = 15f;

    [Header("Debug")]
    public int lastSteps60m = 0;
    public string lastStoryLog = "";
    public bool editorLightMode = true;

    private string userId;
    private PedometerService pedometer;
    private GPSLocationController gps;
    public float activeBeamRadius = 20f;
    private float lastImmediatePingTime = -999f;
    
    void Start()
    {
        BootDiagnostics.Mark("UserPresenceManager.Start");
        StartCoroutine(DeferredStart());
    }

    private IEnumerator DeferredStart()
    {
        BootDiagnostics.Mark("UserPresenceManager.DeferredStart");
        yield return null;

        // Get User ID
        if (DeviceIDManager.Instance != null)
            userId = DeviceIDManager.Instance.DeviceID;
        else
            userId = SystemInfo.deviceUniqueIdentifier;
        BootDiagnostics.Mark("UserPresenceManager.userId");
        yield return null;

        pedometer = GetComponent<PedometerService>();
        if (pedometer == null) pedometer = FindObjectOfType<PedometerService>();
        BootDiagnostics.Mark("UserPresenceManager.pedometer");
        yield return null;

        gps = GetComponent<GPSLocationController>();
        if (gps == null) gps = FindObjectOfType<GPSLocationController>();
        BootDiagnostics.Mark("UserPresenceManager.gps");
        yield return null;

        // Start Loops
        StartCoroutine(StartCoroutinesDelayed());
        
        // Initial Fetch
        if (!Application.isEditor)
        {
            FetchRecentStories();
            BootDiagnostics.Mark("UserPresenceManager.FetchStories");
        }
        else
        {
            Debug.Log("[Presence] Editor: skipping FetchRecentStories");
            BootDiagnostics.Mark("UserPresenceManager.FetchStories skipped");
        }
    }

    private IEnumerator StartCoroutinesDelayed()
    {
        BootDiagnostics.Mark("UserPresenceManager.coroutines queued");
        yield return null;
        StartCoroutine(HeartbeatRoutine());
        yield return null;
        StartCoroutine(PresenceMonitorRoutine());
        BootDiagnostics.Mark("UserPresenceManager.coroutines started");
    }

    IEnumerator HeartbeatRoutine()
    {
        // In editor: skip immediate heartbeat during boot to avoid API connection delays
        // Wait for boot to complete first
        if (Application.isEditor)
        {
            // Wait for boot sequence to complete
            while (!BootState.AllowPlayer)
            {
                yield return new WaitForSeconds(0.5f);
            }
            // Additional delay in editor to let everything settle (avoid lockup on "boot complete")
            yield return new WaitForSeconds(5f);
        }
        else
        {
            // Mobile: send immediate heartbeat after short yield
            yield return null;
        }
        
        BootDiagnostics.Mark("UserPresenceManager.Heartbeat start");
        Debug.Log($"[Presence] Ping API call (heartbeat, first after boot)");
        yield return StartCoroutine(SendHeartbeatCoroutine());
        
        while (true)
        {
            yield return new WaitForSeconds(heartbeatInterval);
            BootDiagnostics.Mark("UserPresenceManager.Heartbeat tick");
            Debug.Log($"[Presence] Ping API call (heartbeat, every {heartbeatInterval}s)");
            yield return StartCoroutine(SendHeartbeatCoroutine());
        }
    }

    IEnumerator PresenceMonitorRoutine()
    {
        while (true)
        {
            BootDiagnostics.Mark("UserPresenceManager.Presence tick");
            CheckPresence();
            yield return new WaitForSeconds(presenceCheckInterval);
        }
    }

    void SendHeartbeat()
    {
        // Legacy synchronous method - use SendHeartbeatCoroutine instead
        StartCoroutine(SendHeartbeatCoroutine());
    }

    IEnumerator SendHeartbeatCoroutine()
    {
        BootDiagnostics.Mark("Presence.SendHeartbeat begin");

        // Yield first to prevent blocking boot sequence
        yield return null;

        // Prefer Firebase Auth UID — it may not be ready at Start() time, so
        // re-resolve before every ping. Falls back to deviceId if not signed in.
        var auth = FirebaseAuthManager.Instance;
        if (auth != null)
        {
            string resolved = auth.GetUserId();
            if (!string.IsNullOrEmpty(resolved)) userId = resolved;
        }

        // Gather Data
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        
        double lat = 0, lon = 0;
        
        // 1. Try to get virtual coordinates from Mapbox (accurate to player position)
        // Yield after each FindObjectOfType to prevent blocking
        var player = FindObjectOfType<KiloFirstPersonController>();
        yield return null;
        var mapBehaviour = FindObjectOfType<KiloverseMapInfo>();
        yield return null;
        
        if (player != null && mapBehaviour != null && mapBehaviour.MapboxMap != null)
        {
            var mapInfo = mapBehaviour.MapInformation;
            var latLon = player.transform.position.GetGeoPosition(mapInfo.CenterMercator, mapInfo.Scale);
            lat = latLon.Latitude;
            lon = latLon.Longitude;
        }
        // 2. Fallback to GPS if map isn't ready but GPS is
        else if (Input.location.status == LocationServiceStatus.Running)
        {
            lat = Input.location.lastData.latitude;
            lon = Input.location.lastData.longitude;
            Debug.Log($"[Presence] Using GPS fallback coordinates: ({lat}, {lon})");
        }
        else
        {
            yield return null;
            var renderManager = KiloWorld.Rendering.Systems.RenderManager.Instance
                ?? FindObjectOfType<KiloWorld.Rendering.Systems.RenderManager>();
            yield return null;
            if (renderManager != null && renderManager.profile != null)
            {
                var profile = renderManager.profile;
                profile.startupLocation.GetStartupCoordinates(out double fallbackLat, out double fallbackLon);
                lat = fallbackLat;
                lon = fallbackLon;
                Debug.Log($"[Presence] Using profile startup fallback coordinates: ({lat}, {lon}) | {profile.startupLocation.startupLocation}");
            }
            else
            {
                Debug.LogWarning($"[Presence] No coordinates available! GPS status: {Input.location.status}, Map ready: {(mapBehaviour != null && mapBehaviour.MapboxMap != null)}");
            }
        }

        Debug.Log($"[Presence] Final coordinates for heartbeat: lat={lat}, lon={lon}");
        BootDiagnostics.Mark("Presence.coords ready");
        yield return null;

        int steps60 = 0;
        int totalSteps = 0;
        int dailySteps = 0;
        int weeklySteps = 0;
        int steps10m = -1;
        int steps20m = -1;
        int steps30m = -1;
        int steps40m = -1;
        int steps50m = -1;
        int steps60m = -1;

        if (pedometer != null)
        {
            steps60 = pedometer.stepsLastHour;
            totalSteps = pedometer.stepCount; 
            if (DeviceIDManager.Instance != null) totalSteps = DeviceIDManager.Instance.AllTimeSteps;
            
            dailySteps = pedometer.stepsLast24Hours;
            weeklySteps = pedometer.stepsLast7Days;

            // Get cached step intervals (returns -1 if not cached yet, which is fine)
            steps10m = pedometer.GetCachedStepsForInterval(10);
            steps20m = pedometer.GetCachedStepsForInterval(20);
            steps30m = pedometer.GetCachedStepsForInterval(30);
            steps40m = pedometer.GetCachedStepsForInterval(40);
            steps50m = pedometer.GetCachedStepsForInterval(50);
            steps60m = pedometer.GetCachedStepsForInterval(60);
        }
        else
        {
            // Editor: pedometer doesn't exist, use defaults
            if (Application.isEditor)
            {
                Debug.Log("[Presence] Editor mode: PedometerService not available, using default step values");
            }
        }

        lastSteps60m = steps60;
        yield return null;

        // Nearby POIs (closest 3)
        List<string> nearbyPOIs = new List<string>();
        if (!(Application.isEditor && editorLightMode) && TransmitterScanner.Instance != null)
        {
            var allPOIs = TransmitterScanner.Instance.GetAll();
            var closest3 = allPOIs.Take(3).ToList();

        float stride = 0.762f;
            if (pedometer != null) stride = pedometer.EstimatedStrideLength;

            foreach (var poi in closest3)
            {
                int steps = Mathf.RoundToInt(poi.Distance / stride);
                float miles = poi.Distance * 0.000621371f;
                string distStr = miles < 0.1f
                    ? $"{Mathf.RoundToInt(poi.Distance * 3.28084f)}ft"
                    : $"{miles:F1}mi";
                nearbyPOIs.Add($"{poi.Name} ({steps} steps [{distStr} {poi.Direction}])");
            }
        }
        BootDiagnostics.Mark("Presence.POIs done");
        yield return null;

        // Nearby beams (all 9)
        float closestBeamDistance = float.MaxValue;
        string closestBeamNoun = null;
        string closestBeamName = null;
        int closestBeamSeed = 0;
        List<string> beamDistances = new List<string>();
        var spawner = (Application.isEditor && editorLightMode) ? null : FindFirstObjectByType<VirtualGridSpawner>();
        yield return null;
        if (spawner != null && player != null)
        {
            var beams = spawner.GetBeams();
            for (int i = 0; i < beams.Length; i++)
            {
                int seed = spawner.GetBeamSeed(i);
                string noun = NounGenerator.GetFromSeed(seed);
                string beamName = BeamNameGenerator.GetNameFromSeed(seed);
                if (beams[i] != null && beams[i].activeSelf)
                {
                    float distance = Vector3.Distance(player.transform.position, beams[i].transform.position);
                    string distanceStr = distance.ToString("F1", CultureInfo.InvariantCulture);
                    beamDistances.Add($"{{\"id\":{i},\"distance\":{distanceStr},\"seed\":{seed},\"noun\":\"{EscapeJson(noun)}\",\"name\":\"{EscapeJson(beamName)}\"}}");
                    if (distance < closestBeamDistance)
                    {
                        closestBeamDistance = distance;
                        closestBeamNoun = noun;
                        closestBeamName = beamName;
                        closestBeamSeed = seed;
                    }
                }
            }
        }
        BootDiagnostics.Mark("Presence.Beams done");
        yield return null;

        // Current location (within enter distance)
        string currentLocationJson = "null";
        if (!(Application.isEditor && editorLightMode) && TransmitterScanner.Instance != null)
        {
            var allPOIs = TransmitterScanner.Instance.GetAll();
            if (allPOIs.Count > 0)
            {
                var closest = allPOIs[0];
                float enterDistance = 30f;
                yield return null;
                var renderManager = KiloWorld.Rendering.Systems.RenderManager.Instance
                    ?? FindObjectOfType<KiloWorld.Rendering.Systems.RenderManager>();
                yield return null;
                var triggerSettings = renderManager != null ? renderManager.profile?.clientEventTriggers : null;
                if (triggerSettings != null && triggerSettings.enterDistanceMeters > 0f)
                {
                    enterDistance = triggerSettings.enterDistanceMeters;
                }

                if (closest.Distance <= enterDistance)
                {
                    string locNoun = NounGenerator.GetFromKey($"{closest.Name}|{closest.Class}|{closest.Type}|{closest.Category}");
                    currentLocationJson = $@"{{
                        ""locationName"": ""{EscapeJson(closest.Name)}"",
                        ""locationClass"": ""{EscapeJson(closest.Class)}"",
                        ""locationType"": ""{EscapeJson(closest.Type)}"",
                        ""locationMaki"": ""{EscapeJson(closest.Maki)}"",
                        ""locationCategory"": ""{EscapeJson(closest.Category)}"",
                        ""locationLatitude"": {closest.GeoLocation.x},
                        ""locationLongitude"": {closest.GeoLocation.y},
                        ""distance"": {closest.Distance:F1},
                        ""direction"": ""{EscapeJson(closest.Direction)}"",
                        ""noun"": ""{EscapeJson(locNoun)}""
                    }}";
                }
            }
        }
        BootDiagnostics.Mark("Presence.Location done");
        yield return null;

        steps10m = EnsureStepValue(steps10m, 10);
        steps20m = EnsureStepValue(steps20m, 20);
        steps30m = EnsureStepValue(steps30m, 30);
        steps40m = EnsureStepValue(steps40m, 40);
        steps50m = EnsureStepValue(steps50m, 50);
        steps60m = EnsureStepValue(steps60m, 60);

        // Construct JSON manually
        string nearbyJson = string.Join(",", nearbyPOIs.Select(s => $"\"{s}\""));
        string beamsJson = string.Join(",", beamDistances);
        string activeBeamJson = "null";
        if (closestBeamDistance <= activeBeamRadius && closestBeamDistance < float.MaxValue)
        {
            string noun = closestBeamNoun ?? NounGenerator.GetFromSeed(closestBeamSeed);
            string name = closestBeamName ?? BeamNameGenerator.GetNameFromSeed(closestBeamSeed);
            string distanceStr = closestBeamDistance.ToString("F1", CultureInfo.InvariantCulture);
            activeBeamJson = $@"{{""seed"":{closestBeamSeed},""distance"":{distanceStr},""noun"":""{EscapeJson(noun)}"",""name"":""{EscapeJson(name)}""}}";
        }
        string json = $@"{{
            ""userId"": ""{userId}"",
            ""lastActive"": {timestamp},
            ""lat"": {lat},
            ""lon"": {lon},
            ""coordinates"": {{ ""latitude"": {lat}, ""longitude"": {lon} }},
            ""steps"": {{
                ""10m"": {steps10m},
                ""20m"": {steps20m},
                ""30m"": {steps30m},
                ""40m"": {steps40m},
                ""50m"": {steps50m},
                ""60m"": {steps60m},
                ""daily"": {dailySteps},
                ""weekly"": {weeklySteps}
            }},
            ""steps60m"": {steps60},
            ""totalSteps"": {totalSteps},
            ""dailySteps"": {dailySteps},
            ""weeklySteps"": {weeklySteps},
            ""nearby"": [{nearbyJson}],
            ""beams"": [{beamsJson}],
            ""currentLocation"": {currentLocationJson},
            ""activeBeam"": {activeBeamJson},
            ""platform"": ""{Application.platform}""
        }}";

        BootDiagnostics.Mark("Presence.Ping begin");
        yield return StartCoroutine(PingAPI(json));
    }

    public void TriggerImmediatePing(string reason)
    {
        if (Time.time - lastImmediatePingTime < immediatePingCooldown)
        {
            return;
        }

        lastImmediatePingTime = Time.time;
        Debug.Log($"[Presence] Immediate ping requested: {reason}");
        StartCoroutine(SendHeartbeatCoroutine());
    }

    private int EnsureStepValue(int reported, int minutes)
    {
        if (reported >= 0) return reported;
        string deviceId = DeviceIDManager.Instance != null
            ? DeviceIDManager.Instance.DeviceID
            : SystemInfo.deviceUniqueIdentifier;
        if (string.IsNullOrEmpty(deviceId)) deviceId = "kilo";
        int seed = deviceId.GetHashCode() ^ minutes;
        System.Random rng = new System.Random(seed);
        int min = Mathf.Max(5, minutes * 3);
        int max = minutes * 6 + 40;
        return rng.Next(min, max + 1);
    }

    private string EscapeJson(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    IEnumerator PingAPI(string json)
    {
        BootDiagnostics.Mark("Presence.Ping coroutine");
        Debug.Log($"[Presence] Sending heartbeat via APIManager\nPayload: {json}");

        yield return APIManager.Instance.Post("/ping", json, (success, response) => {
            if (success)
            {
                Debug.Log($"[Presence] Ping Success: {response}");
                HandlePingResponse(response);
            }
            else
            {
                Debug.LogError($"[Presence] Ping Error: {response}");
            }
        });
    }

    [System.Serializable]
    private class PingResponse
    {
        public WeatherData weather;
        public string message;
        public string city;
        public BeamInfo[] beams;
    }

    [System.Serializable]
    public class WeatherData
    {
        public string icon;
        public string glyph;
        public float temperatureF;
    }

    [System.Serializable]
    public class BeamInfo
    {
        public int id;
        public float distance;
        public string noun;
        public string name;
        public string avatarUrl;
    }

    private static string WeatherGlyphForOverlay(string glyphOrIcon)
    {
        if (string.IsNullOrWhiteSpace(glyphOrIcon)) return "";

        switch (glyphOrIcon.Trim().ToLowerInvariant())
        {
            case "sun":
            case "sunny":
            case "clear":
                return "☀";
            case "cloud":
            case "cloudy":
            case "overcast":
                return "☁";
            case "partly cloudy":
            case "partlycloudy":
                return "⛅";
            case "rain":
            case "rainy":
            case "drizzle":
                return "🌧";
            case "snow":
            case "snowy":
                return "❄";
            case "storm":
            case "thunder":
            case "thunderstorm":
                return "⛈";
            case "fog":
            case "foggy":
            case "mist":
                return "🌫";
            case "wind":
            case "windy":
                return "🌬";
            default:
                return "";
        }
    }

    private void HandlePingResponse(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        
        try
        {
            // The response from Update (PATCH) is usually the data that was sent,
            // UNLESS the server returns specific data. 
            // If the Firebase rule/cloud function modifies the return, we get it here.
            // Assuming the server returns the requested format.
            
            var data = JsonUtility.FromJson<PingResponse>(json);
            if (data != null && data.weather != null)
            {
                if (!string.IsNullOrEmpty(data.weather.icon))
                {
                    KiloWorld.Rendering.Systems.RenderManager.WeatherTempF = data.weather.temperatureF;   // biases aurora hue warm/cool
                    KiloWorld.Rendering.Systems.RenderManager.WeatherGlyph =
                        !string.IsNullOrEmpty(data.weather.glyph) ? data.weather.glyph : data.weather.icon; // drives dynamic sky condition
                }
                var weatherView = FindObjectOfType<WeatherView>();
                if (weatherView != null)
                {
                    weatherView.UpdateWeather(data.weather.icon, data.weather.glyph, data.weather.temperatureF);
                }
                else
                {
                    var ui = FindObjectOfType<PedometerUI>();
                    if (ui != null)
                    {
                        ui.UpdateWeather(data.weather.icon, data.weather.glyph, data.weather.temperatureF);
                    }
                }
            }

            // Set city/weather on standalone persistent overlay (K1L0HUD canvas unreliable)
            Debug.Log($"[Presence] Parsed city='{data?.city}', weather={(data?.weather != null)}");
            if (data != null)
            {
                string weatherStr = null;
                // JsonUtility deserializes "weather":null as empty object, check icon to confirm real data
                if (data.weather != null && !string.IsNullOrEmpty(data.weather.icon))
                {
                    weatherStr = $"{Mathf.RoundToInt(data.weather.temperatureF)}°F";
                }
                if (!string.IsNullOrEmpty(data.city) || weatherStr != null)
                    CityWeatherOverlay.Show(data.city, weatherStr, !string.IsNullOrEmpty(data.weather?.glyph) ? data.weather.glyph : data.weather?.icon);
            }

            if (data != null && !string.IsNullOrEmpty(data.message))
            {
                if (MessageView.Instance != null)
                {
                    MessageView.Instance.SetMessage(data.message, Color.white);
                }
            }

            if (data != null && data.beams != null && data.beams.Length > 0)
            {
                var spawner = FindFirstObjectByType<VirtualGridSpawner>();
                if (spawner != null)
                {
                    foreach (var beam in data.beams)
                    {
                        spawner.SetBeamAvatar(beam.id, beam.avatarUrl);
                    }
                }
            }

            StoryFeedService.ApplyFromPingJson(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Presence] Failed to parse ping response: {e.Message}");
        }
    }

    void CheckPresence()
    {
        if (FirebaseRestClient.Instance == null) return;

        FirebaseRestClient.Instance.GetData(presencePath, (json) => {
            // Parse JSON (Dictionary of users)
            // For now, just log the raw data size or count
            // Debug.Log($"[Presence] Received presence data: {json.Length} bytes");
            // TODO: Parse and visualize other users on map
        }, (err) => {
            // Debug.LogError($"[Presence] Check failed: {err}");
        });
    }

    public void FetchRecentStories()
    {
        if (FirebaseRestClient.Instance == null) return;

        // Firestore Query
        // We want: collection "stories", where senderId == userId OR receiverId == userId
        // Note: Firestore REST API "runQuery" is powerful but verbose.
        // For simplicity, let's just query where senderId == userId for now (Sent)
        // Implementing OR in Firestore REST requires a composite filter which is verbose to write in raw string JSON.
        
        string queryJson = $@"{{
            ""structuredQuery"": {{
                ""from"": [{{ ""collectionId"": ""{storiesCollection}"" }}],
                ""where"": {{
                    ""fieldFilter"": {{ 
                        ""field"": {{ ""fieldPath"": ""senderId"" }}, 
                        ""op"": ""EQUAL"", 
                        ""value"": {{ ""stringValue"": ""{userId}"" }} 
                    }}
                }},
                ""orderBy"": [{{ ""field"": {{ ""fieldPath"": ""timestamp"" }}, ""direction"": ""DESCENDING"" }}],
                ""limit"": 10
            }}
        }}";

        FirebaseRestClient.Instance.RunFirestoreQuery(storiesCollection, queryJson, (response) => {
            lastStoryLog = response;
            Debug.Log($"[Presence] Stories fetched: {response.Length} bytes");
            // Parse response to extract messages
        }, (err) => {
            Debug.LogError($"[Presence] Fetch stories failed: {err}");
        });
    }
}
