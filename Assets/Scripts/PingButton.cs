using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Kiloverse.Mapbox;

/// <summary>
/// Creates a "P" button that manually pings the API with location, nearby POIs, and steps.
/// Displays the response message in the center of the screen.
/// </summary>
public class PingButton : MonoBehaviour
{
    private Button pingButton;
    private GameObject responseOverlay;
    // responseText removed - now using MessageView.Instance
    private Canvas canvas;

    void Start()
    {
        CreateUI();
    }

    void CreateUI()
    {
        // ALWAYS create a fresh unique canvas for the Ping button
        GameObject canvasGO = new GameObject("PingButtonCanvas_Unique");
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 3005; // Very high
        canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        
        // Ensure it has a scaler
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);

        // Create P button container
        GameObject pButtonContainer = new GameObject("PingButtonContainer");
        pButtonContainer.transform.SetParent(canvas.transform, false);
        RectTransform pRect = pButtonContainer.AddComponent<RectTransform>();
        pRect.anchorMin = new Vector2(0, 0); // Bottom-Left
        pRect.anchorMax = new Vector2(0, 0);
        pRect.pivot = new Vector2(0, 0);
        pRect.anchoredPosition = new Vector2(20, 460); // Moved HIGHER to avoid T (240) and possible overlap
        pRect.sizeDelta = new Vector2(200, 200); 

        // Create button
        GameObject btnGO = new GameObject("PingButton_Actual");
        btnGO.transform.SetParent(pButtonContainer.transform, false);

        RectTransform btnRect = btnGO.AddComponent<RectTransform>();
        btnRect.anchorMin = Vector2.zero;
        btnRect.anchorMax = Vector2.one;
        btnRect.offsetMin = Vector2.zero;
        btnRect.offsetMax = Vector2.zero;

        // Transparent background
        Image img = btnGO.AddComponent<Image>();
        img.color = Color.clear;

        pingButton = btnGO.AddComponent<Button>();
        pingButton.onClick.AddListener(OnPingButtonClick);

        // White "P" text matching other buttons
        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(btnGO.transform, false);
        RectTransform textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;

        Text txt = textGO.AddComponent<Text>();
        txt.text = "P";
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.white;
        txt.fontSize = 100;

        Debug.Log("[PingButton] Created P button on unique canvas");
        // Response text now handled by MessageView in UILayoutManager
    }

    // Removed: Now using MessageView.Instance instead of local responseText

    void OnPingButtonClick()
    {
        Debug.Log("[PingButton] P button clicked - sending manual ping");
        if (MessageView.Instance != null)
        {
            MessageView.Instance.SetMessage("Pinging...", Color.yellow);
        }
        StartCoroutine(SendManualPing());
    }
    IEnumerator SendManualPing()
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

        // Get steps - including granular intervals
        int dailySteps = 0;
        int weeklySteps = 0;
        int steps10m = -1, steps20m = -1, steps30m = -1, steps40m = -1, steps50m = -1, steps60m = -1;

        var pedometer = FindObjectOfType<PedometerService>();
        if (pedometer != null)
        {
            dailySteps = pedometer.stepsLast24Hours;
            weeklySteps = pedometer.stepsLast7Days;

            // Query granular step intervals (10m, 20m, 30m, 40m, 50m, 60m)
            int queriesCompleted = 0;
            int totalQueries = 6;

            pedometer.GetStepsForInterval(10, (steps) => { steps10m = steps; queriesCompleted++; });
            pedometer.GetStepsForInterval(20, (steps) => { steps20m = steps; queriesCompleted++; });
            pedometer.GetStepsForInterval(30, (steps) => { steps30m = steps; queriesCompleted++; });
            pedometer.GetStepsForInterval(40, (steps) => { steps40m = steps; queriesCompleted++; });
            pedometer.GetStepsForInterval(50, (steps) => { steps50m = steps; queriesCompleted++; });
            pedometer.GetStepsForInterval(60, (steps) => { steps60m = steps; queriesCompleted++; });

            // Wait for all queries to complete (max 3 seconds)
            float timeout = 3f;
            float elapsed = 0f;
            while (queriesCompleted < totalQueries && elapsed < timeout)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }

            Debug.Log($"[PingButton] Granular steps loaded: 10m={steps10m}, 20m={steps20m}, 30m={steps30m}, 40m={steps40m}, 50m={steps50m}, 60m={steps60m}");
        }

        // Get beam distances (all 9 beams)
        List<string> beamDistances = new List<string>();
        var spawner = FindObjectOfType<VirtualGridSpawner>();
        if (spawner != null && player != null)
        {
            var beams = spawner.GetBeams();
            for (int i = 0; i < beams.Length; i++)
            {
                if (beams[i] != null && beams[i].activeSelf)
                {
                    float distance = Vector3.Distance(player.transform.position, beams[i].transform.position);
                    string distStr = distance.ToString("F1");
                    beamDistances.Add($"{{\"id\":{i},\"distance\":{distStr}}}");
                }
            }
        }

        // Get nearby POIs (closest 3)
        List<string> nearbyPOIs = new List<string>();
        if (TransmitterScanner.Instance != null)
        {
            var allPOIs = TransmitterScanner.Instance.GetAll();
            var closest3 = allPOIs.Take(3).ToList();
            
            // Get dynamic stride
            float stride = 0.762f;
            if (pedometer != null) stride = pedometer.EstimatedStrideLength;

            foreach (var poi in closest3)
            {
                int steps = Mathf.RoundToInt(poi.Distance / stride);
                
                // Calculate distance string (matching LocationsPanel)
                float miles = poi.Distance * 0.000621371f;
                string distStr;
                if (miles < 0.1f)
                {
                    int feet = Mathf.RoundToInt(poi.Distance * 3.28084f);
                    distStr = $"{feet}ft";
                }
                else
                {
                    distStr = $"{miles:F1}mi";
                }

                nearbyPOIs.Add($"{poi.Name} ({steps} steps [{distStr} {poi.Direction}])");
            }
        }

        // Build JSON payload with nested structure
        long timestamp = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string nearbyJson = string.Join(",", nearbyPOIs.Select(s => $"\"{s}\""));
        string beamsJson = string.Join(",", beamDistances);

        string json = $@"{{
            ""userId"": ""{userId}"",
            ""lastActive"": {timestamp},
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
            ""nearby"": [{nearbyJson}],
            ""beams"": [{beamsJson}],
            ""platform"": ""{Application.platform}"",
            ""manualPing"": true
        }}";

        Debug.Log($"[PingButton] Sending manual ping: {json}");

        // Send via APIManager
        yield return APIManager.Instance.Post("/ping", json, (success, response) => {
            if (success)
            {
                Debug.Log($"[PingButton] Manual ping success: {response}");
                HandlePingResponse(response);
            }
            else
            {
                Debug.LogError($"[PingButton] Manual ping failed: {response}");
                ShowResponse($"Ping failed:\n{response}");
            }
        });
    }

    void HandlePingResponse(string json)
    {
        try
        {
            var data = JsonUtility.FromJson<PingResponse>(json);
            if (data != null && !string.IsNullOrEmpty(data.message))
            {
                ShowResponse(data.message, true);
            }
            else
            {
                ShowResponse("Ping successful!", true);
            }

            if (data != null && data.weather != null)
            {
                var weatherView = FindObjectOfType<WeatherView>();
                if (weatherView != null)
                {
                    weatherView.UpdateWeather(data.weather.icon, data.weather.glyph, data.weather.temperatureF);
                }
            }

            KiloWorld.UI.Stories.StoryFeedService.ApplyFromPingJson(json);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[PingButton] Failed to parse response: {e.Message}");
            ShowResponse($"Ping successful!\nRaw: {json}", true);
            KiloWorld.UI.Stories.StoryFeedService.ApplyFromPingJson(json);
        }
    }

    void ShowResponse(string message, bool success = false)
    {
        if (MessageView.Instance != null)
        {
            Color color = success ? Color.white : Color.red;
            MessageView.Instance.SetMessage(message, color);
        }

        // No auto-hide, stays on screen
    }

    // Removed CreateResponseOverlay and AutoHideResponse
    
    [System.Serializable]
    private class PingResponse
    {
        public bool ok;
        public string message;
        public string city;
        public WeatherData weather;
    }

    [System.Serializable]
    private class WeatherData
    {
        public string icon;
        public string glyph;
        public float temperatureF;
    }
}
