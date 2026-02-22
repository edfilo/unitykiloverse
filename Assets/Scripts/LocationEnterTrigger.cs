using System;
using System.Collections;
using Mapbox.BaseModule.Utilities;
using Kiloverse.Mapbox;
using UnityEngine;

public class LocationEnterTrigger : MonoBehaviour
{
    public static LocationEnterTrigger Instance { get; private set; }

    [Header("Debug")]
    [SerializeField] private string lastTriggeredLocation;
    [SerializeField] private long lastTriggeredUnix;
    [SerializeField] private bool lastWithinThreshold;

    private const string PrefLastTriggerTime = "KiloLastEnterTriggerTime";
    private const string PrefLastTriggerLocation = "KiloLastEnterTriggerLocation";

    private TransmitterScanner scanner;
    private PedometerService pedometer;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadState();
    }

    private void Start()
    {
        scanner = TransmitterScanner.Instance;
        pedometer = FindObjectOfType<PedometerService>();
    }

    public void CheckForTrigger()
    {
        var settings = GetTriggerSettings();
        if (settings == null || !settings.enableLocationEnter) return;

        if (scanner == null)
        {
            scanner = TransmitterScanner.Instance;
        }

        if (scanner == null) return;

        var all = scanner.GetAll();
        if (all == null || all.Count == 0)
        {
            lastWithinThreshold = false;
            return;
        }

        var closest = all[0];
        bool within = closest.Distance <= settings.enterDistanceMeters;
        bool crossedThreshold = within && !lastWithinThreshold;

        if (crossedThreshold && CooldownComplete(settings.enterCooldownHours))
        {
            StartCoroutine(SendEnterPing(closest));
            lastTriggeredLocation = closest.Name;
            lastTriggeredUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            SaveState();
        }

        lastWithinThreshold = within;
    }

    private KiloWorld.Rendering.KiloWorldMasterProfile.ClientEventTriggerSettings GetTriggerSettings()
    {
        var renderManager = KiloWorld.Rendering.Systems.RenderManager.Instance
            ?? FindObjectOfType<KiloWorld.Rendering.Systems.RenderManager>();
        return renderManager != null ? renderManager.profile?.clientEventTriggers : null;
    }

    private bool CooldownComplete(float hours)
    {
        if (hours <= 0f) return true;

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long cooldownSeconds = (long)Mathf.Max(0f, hours) * 3600;
        return now - lastTriggeredUnix >= cooldownSeconds;
    }

    private IEnumerator SendEnterPing(TransmitterScanner.TransmitterData location)
    {
        if (location == null) yield break;

        string userId = DeviceIDManager.Instance != null
            ? DeviceIDManager.Instance.GetCurrentUserId()
            : SystemInfo.deviceUniqueIdentifier;

        double lat = 0;
        double lon = 0;
        GetPlayerCoordinates(ref lat, ref lon);

        int steps10m = -1;
        int steps20m = -1;
        int steps30m = -1;
        int steps40m = -1;
        int steps50m = -1;
        int steps60m = -1;
        int dailySteps = 0;
        int weeklySteps = 0;

        if (pedometer == null)
        {
            pedometer = FindObjectOfType<PedometerService>();
        }

        if (pedometer != null)
        {
            steps10m = pedometer.GetCachedStepsForInterval(10);
            steps20m = pedometer.GetCachedStepsForInterval(20);
            steps30m = pedometer.GetCachedStepsForInterval(30);
            steps40m = pedometer.GetCachedStepsForInterval(40);
            steps50m = pedometer.GetCachedStepsForInterval(50);
            steps60m = pedometer.GetCachedStepsForInterval(60);
            dailySteps = pedometer.stepsLast24Hours;
            weeklySteps = pedometer.stepsLast7Days;
        }

        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

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
            ""command"": {{
                ""name"": ""enter"",
                ""locationName"": ""{EscapeJson(location.Name)}"",
                ""locationClass"": ""{EscapeJson(location.Class)}"",
                ""locationType"": ""{EscapeJson(location.Type)}"",
                ""locationMaki"": ""{EscapeJson(location.Maki)}"",
                ""locationCategory"": ""{EscapeJson(location.Category)}"",
                ""locationLatitude"": {location.GeoLocation.x},
                ""locationLongitude"": {location.GeoLocation.y},
                ""distance"": {location.Distance:F1},
                ""direction"": ""{EscapeJson(location.Direction)}""
            }},
            ""platform"": ""{Application.platform}""
        }}";

        Debug.Log($"[LocationEnterTrigger] Sending enter ping: {json}");

        yield return APIManager.Instance.Post("/ping", json, (success, response) => {
            if (success)
            {
                Debug.Log($"[LocationEnterTrigger] enter ping success: {response}");
            }
            else
            {
                Debug.LogError($"[LocationEnterTrigger] enter ping failed: {response}");
            }
        });
    }

    private void GetPlayerCoordinates(ref double lat, ref double lon)
    {
        var player = FindObjectOfType<KiloFirstPersonController>();
        var mapBehaviour = FindObjectOfType<Kiloverse.Mapbox.KiloverseMapInfo>();

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
    }

    private void LoadState()
    {
        lastTriggeredUnix = (long)PlayerPrefs.GetInt(PrefLastTriggerTime, 0);
        lastTriggeredLocation = PlayerPrefs.GetString(PrefLastTriggerLocation, string.Empty);
    }

    private void SaveState()
    {
        PlayerPrefs.SetInt(PrefLastTriggerTime, (int)Mathf.Clamp(lastTriggeredUnix, 0, int.MaxValue));
        PlayerPrefs.SetString(PrefLastTriggerLocation, lastTriggeredLocation ?? string.Empty);
        PlayerPrefs.Save();
    }

    private string EscapeJson(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
