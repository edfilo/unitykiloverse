using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using System;
using Kiloverse.Mapbox;

public class TransmitterScanner : MonoBehaviour
{
    private static TransmitterScanner _instance;
    public static TransmitterScanner Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<TransmitterScanner>();
                if (_instance == null)
                {
                    var go = new GameObject("TransmitterScanner");
                    _instance = go.AddComponent<TransmitterScanner>();
                    Debug.Log("[TransmitterScanner] Auto-created singleton instance");
                }
            }
            return _instance;
        }
    }

    public class TransmitterData
    {
        public string Name;
        public string PrimaryCategory; // Overture primary category (e.g., "bar", "coffee_shop")
        public string MainCategoryGroup; // One of 4 groups: "coffee", "bar", "food", "convenience", "other"
        public string[] TypesRaw;
        public string Lore;
        public string ArtifactMaterial;
        public string ArtifactContainer;
        public string ArtifactLabel;
        public string ArtifactLore;
        public string ArtifactSenderName;

        // Legacy fields (kept for backwards compatibility)
        public string Class;
        public string Type;
        public string Maki;
        public string Category;

        public Vector2d GeoLocation; // Latitude/Longitude
        public float Distance;
        public string Direction;
        public Vector3 WorldPosition; // Cached world position (set by UI code)
        public bool HasWorldPosition; // Whether WorldPosition is valid
    }

    private List<TransmitterData> _allTransmitters = new List<TransmitterData>();
    private List<TransmitterData> _sortedTransmitters = new List<TransmitterData>();

    [Header("Settings")]
    public float scanInterval = 1.0f;
    [Header("Data Source")]
    public bool usePlacesApi = false;
    [Tooltip("Legacy fallback only. Native Swift owns world/nearby and pushes places into Unity.")]
    public bool allowUnityPlacesPolling = false;
    [Tooltip("Legacy fallback only. Native pushed places are canonical.")]
    public bool allowLegacyRegisteredTransmitters = false;
    [Tooltip("Defer Places API start until after boot (avoids lockup during Startup)")]
    public bool deferPlacesApiStart = true;
    public float placesPollInterval = 8f;
    public float placesRadiusMeters = 5000f;
    public float placesMinFetchSeconds = 1200f; // 20 minutes
    public float placesMinFetchMeters = 1609.34f; // 1 mile
    public bool disableFiltering = false;
    public float locationPingCooldown = 15f;
    [Tooltip("Main category groups to include (coffee, bar, food, convenience)")]
    public string[] targetCategories = new string[] {
        "coffee", "bar", "food", "convenience"
    };

    public string[] excludeCategories = new string[] {
        // No blacklist
    };

private void Awake()
    {
        Debug.Log($"[TransmitterScanner] ===== Awake() called ===== GameObject: {gameObject.name}, Instance ID: {GetInstanceID()}");
        
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning($"[TransmitterScanner] Duplicate instance detected! Destroying this: {GetInstanceID()}, keeping existing: {_instance.GetInstanceID()}");
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        // Force settings reset to use 4 main category groups (matches locations.html)
        disableFiltering = false;
        targetCategories = new string[] {
            "coffee", "bar", "food", "convenience" // 4 main groups
        };
        currentFilterLabel = "all";

        Debug.Log($"[TransmitterScanner] Singleton initialized. Filtering: ON (4 main groups). Instance ID: {GetInstanceID()}");
    }

    private void OnDestroy()
    {
        Debug.LogWarning($"[TransmitterScanner] ===== OnDestroy() called ===== Instance ID: {GetInstanceID()}");
    }

    private void OnEnable()
    {
        Debug.Log($"[TransmitterScanner] OnEnable() called. Instance ID: {GetInstanceID()}");
    }

    private void OnDisable()
    {
        Debug.LogWarning($"[TransmitterScanner] OnDisable() called. Instance ID: {GetInstanceID()}");
    }

    private void Start()
    {
        if (usePlacesApi && allowUnityPlacesPolling)
        {
            if (deferPlacesApiStart)
            {
                StartCoroutine(DeferredPlacesApiStart());
            }
            else
            {
                StartCoroutine(PlacesPollRoutine());
            }
        }
    }

    private IEnumerator DeferredPlacesApiStart()
    {
        if (!usePlacesApi || !allowUnityPlacesPolling)
            yield break;

        // Wait for boot to complete so API/TryAutoConnect doesn't block startup
        while (!BootState.AllowPlayer)
        {
            yield return new WaitForSeconds(0.5f);
        }
        yield return new WaitForSeconds(5f); // Extra buffer after AllowPlayer
        Debug.Log("[TransmitterScanner] Starting Places API (deferred after boot)");
        _placesApiRunning = true;
        StartCoroutine(PlacesPollRoutine());
    }

    private bool _placesApiRunning = false;

    /// <summary>
    /// Force-start the Places API polling if it hasn't started yet.
    /// Useful when TransmitterScanner is auto-created as a singleton (Start may not fire).
    /// </summary>
    public void EnsurePlacesApiRunning()
    {
        if (!usePlacesApi || !allowUnityPlacesPolling || _placesApiRunning) return;
        _placesApiRunning = true;
        Debug.Log("[TransmitterScanner] EnsurePlacesApiRunning: force-starting Places API");
        StartCoroutine(PlacesPollRoutine());
    }

    // Called when teleporting to clear old city data
    public void ClearAll()
    {
        _allTransmitters.Clear();
        _sortedTransmitters.Clear();
        Debug.Log("[TransmitterScanner] Cleared all memory.");
    }

    public void ApplyNativeWorldNearby(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;

        try
        {
            var data = JsonUtility.FromJson<PlacesResponse>(json);
            if (data != null && !data.includePlaces)
                return;
            if (data == null || data.places == null)
            {
                Debug.LogWarning("[TransmitterScanner] Native world payload missing places.");
                return;
            }

            ApplyPlaces(data.places, "native");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[TransmitterScanner] Failed to parse native world payload: {ex.Message}");
        }
    }

public void RegisterTransmitter(string name, string primaryCategory, string mainCategoryGroup, Vector2d latLon)
    {
        if (!allowLegacyRegisteredTransmitters || usePlacesApi)
        {
            // Ignore Overture/manual POIs. Native Swift pushes canonical nearby places.
            return;
        }
        // Dedup based on Name + Location (approximate)
        if (_allTransmitters.Any(t => t.Name == name && (t.GeoLocation - latLon).magnitude < 0.0001))
        {
            // Debug.Log($"[TransmitterScanner] Skipping duplicate: {name}");
            return;
        }

        _allTransmitters.Add(new TransmitterData
        {
            Name = name,
            PrimaryCategory = primaryCategory,
            MainCategoryGroup = mainCategoryGroup,
            GeoLocation = latLon,

            // Legacy fields (empty for now, kept for backwards compatibility)
            Class = "",
            Type = "",
            Maki = primaryCategory, // Use primaryCategory as fallback for Maki
            Category = primaryCategory
        });

        // Log first 3 registrations for debugging
        if (_allTransmitters.Count <= 3)
        {
            Debug.Log($"[TransmitterScanner] Registered #{_allTransmitters.Count}: '{name}' category={mainCategoryGroup}");
        }
        else if (_allTransmitters.Count == 100 || _allTransmitters.Count == 500 || _allTransmitters.Count == 1000)
        {
            Debug.Log($"[TransmitterScanner] Registered {_allTransmitters.Count} transmitters");
        }

        if (Time.time >= nextLocationPingTime)
        {
            var presence = FindObjectOfType<UserPresenceManager>();
            if (presence != null)
            {
                presence.TriggerImmediatePing("new-location-loaded");
                nextLocationPingTime = Time.time + locationPingCooldown;
            }
        }

        // No forced update, let Update loop handle it
    }

    public string CurrentFilterLabel => currentFilterLabel;

    private string currentFilterLabel = "all";
    private float nextLocationPingTime = 0f;

    public void SetCategoryFilter(string label)
    {
        currentFilterLabel = string.IsNullOrEmpty(label) ? "all" : label.ToLower();

        // Map to 4 main category groups (matches locations.html)
        switch (currentFilterLabel)
        {
            case "bar":
            case "drinks":
                targetCategories = new string[] { "bar" }; // MainCategoryGroup
                break;
            case "food":
            case "restaurant":
                targetCategories = new string[] { "food" }; // MainCategoryGroup
                break;
            case "coffee":
            case "cafe":
                targetCategories = new string[] { "coffee" }; // MainCategoryGroup
                break;
            case "convenience":
                targetCategories = new string[] { "convenience" }; // MainCategoryGroup
                break;
            case "all":
            default:
                targetCategories = new string[] { "coffee", "bar", "food", "convenience" }; // Strictly only the 4 targeted groups
                currentFilterLabel = "all";
                break;
        }

        Debug.Log($"[TransmitterScanner] Category filter set to '{currentFilterLabel}' (groups: {string.Join(", ", targetCategories)})");
    }

    // Deprecated/Unused now
    public void UnregisterTransmitter(Transform anchor)
    {
        // No-op: We persist data now even if anchor destroys
    }

    private float _lastScanTime;
    private OvertureMapManager _cachedOvertureManager;
    private KiloFirstPersonController _cachedPlayer;
    private bool _placesFetchInFlight = false;
    private float _lastPlacesFetchTime = -9999f;
    private Vector2d _lastPlacesFetchGPS = new Vector2d(0, 0);
    private bool _scanInProgress = false;
    [Tooltip("Max milliseconds to spend per frame while scanning (editor safety)")]
    public float maxScanFrameTimeMs = 6f;

    [Serializable]
    private class PlacesResponse
    {
        public bool ok;
        public bool includePlaces;
        public PlaceEntry[] places;
        public string error;
    }

    [Serializable]
    private class PlaceEntry
    {
        public string name;
        public string type;
        public string[] types;
        public string businessStatus;
        public string lore;
        public string artifactMaterial;
        public string artifactContainer;
        public string artifactLabel;
        public string artifactLore;
        public string artifactSenderName;
        public PlaceCoordinates coordinates;
    }

    [Serializable]
    private class PlaceCoordinates
    {
        public float lat;
        public float lng;
    }

    private void ApplyPlaces(PlaceEntry[] places, string source)
    {
        if (places == null) return;

        _allTransmitters.Clear();
        foreach (var place in places)
        {
            if (place == null || place.coordinates == null || string.IsNullOrEmpty(place.name))
            {
                continue;
            }

            string primary = !string.IsNullOrEmpty(place.type) ? place.type : (place.types != null && place.types.Length > 0 ? place.types[0] : "place");
            string group = MapToMainGroup(primary, place.types);

            var latLon = new Vector2d(place.coordinates.lat, place.coordinates.lng);
            _allTransmitters.Add(new TransmitterData
            {
                Name = place.name,
                PrimaryCategory = primary,
                MainCategoryGroup = group,
                TypesRaw = place.types,
                Lore = place.lore,
                ArtifactMaterial = place.artifactMaterial,
                ArtifactContainer = place.artifactContainer,
                ArtifactLabel = place.artifactLabel,
                ArtifactLore = place.artifactLore,
                ArtifactSenderName = place.artifactSenderName,
                GeoLocation = latLon,
                Class = group,
                Type = primary,
                Maki = primary,
                Category = primary
            });
        }

        if (!_scanInProgress)
        {
            StartCoroutine(UpdateDistancesAndSortCoroutine());
        }

        var categoryBreakdown = _allTransmitters
            .GroupBy(t => t.MainCategoryGroup)
            .OrderByDescending(g => g.Count())
            .Select(g => $"{g.Key}: {g.Count()}")
            .ToList();

        Debug.Log($"[TransmitterScanner] Loaded {places.Length} places from {source}");
        Debug.Log($"[TransmitterScanner]   Total transmitters: {_allTransmitters.Count}");
        Debug.Log($"[TransmitterScanner]   Categories: {string.Join(", ", categoryBreakdown)}");
        if (_allTransmitters.Count > 0)
        {
            var nearest = _sortedTransmitters.FirstOrDefault();
            if (nearest != null)
            {
                Debug.Log($"[TransmitterScanner]   Nearest: {nearest.Name} ({nearest.Distance:F0}m, {nearest.MainCategoryGroup})");
            }
        }
    }

    private void Update()
    {
        if (Time.frameCount % 300 == 0)
        {
             Debug.Log($"[TransmitterScanner] All: {_allTransmitters.Count}, Sorted: {_sortedTransmitters.Count}, Filter: {currentFilterLabel}");
        }

        if (Time.time - _lastScanTime > scanInterval)
        {
            _lastScanTime = Time.time;
            if (!_scanInProgress)
            {
                StartCoroutine(UpdateDistancesAndSortCoroutine());
            }

            if (_sortedTransmitters.Count == 0 && _allTransmitters.Count > 0)
            {
                Debug.LogWarning($"[TransmitterScanner] Update ran. All: {_allTransmitters.Count}, Sorted: 0. Filter Disabled? {disableFiltering}");
            }
        }
    }

    private IEnumerator PlacesPollRoutine()
    {
        while (true)
        {
            if (!usePlacesApi || !allowUnityPlacesPolling)
            {
                _placesApiRunning = false;
                yield break;
            }

            if (_placesFetchInFlight)
            {
                yield return new WaitForSeconds(placesPollInterval);
                continue;
            }

            var playerController = FindFirstObjectByType<KiloFirstPersonController>();
            if (playerController == null)
            {
                yield return new WaitForSeconds(placesPollInterval);
                continue;
            }

            var playerGPS = playerController.playerGPS;
            if (Mathf.Approximately((float)playerGPS.Latitude, 0f) && Mathf.Approximately((float)playerGPS.Longitude, 0f))
            {
                yield return new WaitForSeconds(placesPollInterval);
                continue;
            }

            bool shouldFetch = false;
            float timeSinceFetch = Time.time - _lastPlacesFetchTime;
            if (timeSinceFetch >= placesMinFetchSeconds)
            {
                shouldFetch = true;
            }
            else
            {
                double metersMoved = Conversions.GeoDistance(
                    _lastPlacesFetchGPS.y,
                    _lastPlacesFetchGPS.x,
                    playerGPS.Longitude,
                    playerGPS.Latitude
                ) * 1000.0;
                if (metersMoved >= placesMinFetchMeters)
                {
                    shouldFetch = true;
                }
            }

            if (!shouldFetch)
            {
                yield return new WaitForSeconds(placesPollInterval);
                continue;
            }

            _placesFetchInFlight = true;
            // Re-fetch live coordinates right before sending (player may have moved during yields)
            var liveGPS = playerController.playerGPS;
            // Strict /places format: latitude, longitude, radiusMeters as top-level numbers
            string payload = $"{{\"latitude\":{liveGPS.Latitude.ToString("F6", CultureInfo.InvariantCulture)},\"longitude\":{liveGPS.Longitude.ToString("F6", CultureInfo.InvariantCulture)},\"radiusMeters\":{Mathf.RoundToInt(placesRadiusMeters)}}}";

            // Log the API call details
            string baseURL = APIManager.Instance.GetBaseURL();
            Debug.Log($"[TransmitterScanner] Calling Places API:");
            Debug.Log($"[TransmitterScanner]   URL: {baseURL}/places");
            Debug.Log($"[TransmitterScanner]   Payload: {payload}");
            Debug.Log($"[TransmitterScanner]   Radius: {placesRadiusMeters}m, Location: ({liveGPS.Latitude:F6}, {liveGPS.Longitude:F6})");

            yield return APIManager.Instance.Post("/places", payload, (success, response) =>
            {
                _placesFetchInFlight = false;
                if (!success)
                {
                    Debug.LogWarning($"[TransmitterScanner] Places API call FAILED: {response}");
                    return;
                }

                Debug.Log($"[TransmitterScanner] Places API call SUCCESS: {response?.Length ?? 0} chars received");

                try
                {
                    var data = JsonUtility.FromJson<PlacesResponse>(response);
                    if (data == null || data.places == null)
                    {
                        Debug.LogWarning("[TransmitterScanner] Places response missing data.");
                        return;
                    }

                    ApplyPlaces(data.places, "legacy Places API");
                    _lastPlacesFetchTime = Time.time;
                    _lastPlacesFetchGPS = new Vector2d(liveGPS.Latitude, liveGPS.Longitude);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[TransmitterScanner] Failed to parse places response: {ex.Message}");
                }
            });

            yield return new WaitForSeconds(placesPollInterval);
        }
    }

    private string MapToMainGroup(string primary, string[] types)
    {
        if (IsInGroup(primary, types, CoffeeTypes)) return "coffee";
        if (IsInGroup(primary, types, BarTypes)) return "bar";
        if (IsInGroup(primary, types, FoodTypes)) return "food";
        if (IsInGroup(primary, types, ConvenienceTypes)) return "convenience";
        return "other";
    }

    private bool IsInGroup(string primary, string[] types, HashSet<string> group)
    {
        if (!string.IsNullOrEmpty(primary) && group.Contains(primary)) return true;
        if (types == null) return false;
        for (int i = 0; i < types.Length; i++)
        {
            if (group.Contains(types[i])) return true;
        }
        return false;
    }

    private static readonly HashSet<string> CoffeeTypes = new HashSet<string> {
        "cafe", "coffee_shop", "tea_house", "juice_bar", "smoothie_juice_bar"
    };

    private static readonly HashSet<string> BarTypes = new HashSet<string> {
        "bar", "pub", "brewery", "beer_bar", "cocktail_bar", "wine_bar", "dive_bar",
        "sports_bar", "gastropub", "irish_pub", "tiki_bar", "sake_bar", "night_club"
    };

    private static readonly HashSet<string> FoodTypes = new HashSet<string> {
        "restaurant", "fast_food_restaurant", "pizza_restaurant", "burger_restaurant",
        "chinese_restaurant", "mexican_restaurant", "italian_restaurant", "japanese_restaurant",
        "thai_restaurant", "indian_restaurant", "sushi_restaurant", "american_restaurant",
        "diner", "breakfast_restaurant", "brunch_restaurant", "steakhouse", "sandwich_shop",
        "bakery", "food_stand", "seafood_restaurant", "ethiopian_restaurant",
        "chicken_restaurant", "hot_dog_restaurant", "buffet_restaurant", "deli"
    };

    private static readonly HashSet<string> ConvenienceTypes = new HashSet<string> {
        "convenience_store", "gas_station", "grocery_store"
    };

private IEnumerator UpdateDistancesAndSortCoroutine()
    {
        _scanInProgress = true;
        float frameStart = Time.realtimeSinceStartup;

        // Get player GPS directly from KiloFirstPersonController (source of truth)
        if (_cachedPlayer == null)
        {
            _cachedPlayer = FindFirstObjectByType<KiloFirstPersonController>();
        }
        var playerController = _cachedPlayer;
        if (playerController == null)
        {
            if (Time.frameCount % 300 == 0)
                Debug.LogWarning("[TransmitterScanner] KiloFirstPersonController not found. Cannot update distances.");
            _scanInProgress = false;
            yield break;
        }

        var playerGPS = playerController.playerGPS;
        Vector3 playerPos = playerController.transform.position;

        // Need map reference to convert stored GeoLocation to World Position for direction
        if (_cachedOvertureManager == null)
        {
            _cachedOvertureManager = FindObjectOfType<OvertureMapManager>();
        }

        var hasMapInfo = _cachedOvertureManager != null && _cachedOvertureManager.map != null && _cachedOvertureManager.map.MapInformation != null;
        var mapInfo = hasMapInfo ? _cachedOvertureManager.map.MapInformation : null;

        // Update distances & directions using player's actual GPS (no conversion errors)
        foreach (var t in _allTransmitters)
        {
            // Use Haversine for Distance (Km -> Meters) - GPS coordinates are source of truth
            // t.GeoLocation: x=Lat, y=Lon
            t.Distance = (float)Conversions.GeoDistance(playerGPS.Longitude, playerGPS.Latitude, t.GeoLocation.y, t.GeoLocation.x) * 1000f;

            if (hasMapInfo)
            {
                // Use World Vector for Direction (Relative Bearing)
                // Convert stored Geo -> Relative World (Relative to Map Center)
                // t.GeoLocation: x=Lat, y=Lon
                var centerMercator = new Vector2d(mapInfo.CenterMercator.x, mapInfo.CenterMercator.y);
                Vector3 relativePos = Conversions.LatitudeLongitudeToWorldPosition(t.GeoLocation.x, t.GeoLocation.y, centerMercator, mapInfo.Scale);
                
                // Convert to Absolute World Position (Add Map Root Offset)
                Vector3 targetWorldPos = relativePos + _cachedOvertureManager.map.transform.position;
                targetWorldPos.y = playerPos.y; 
                
                // Use World Vector for Direction (Relative Bearing)
                t.Direction = GetCardinalDirection(playerPos, targetWorldPos);
                t.WorldPosition = targetWorldPos;
                t.HasWorldPosition = true;
            }
            else
            {
                t.Direction = "";
            }

            if (Time.realtimeSinceStartup - frameStart > maxScanFrameTimeMs / 1000f)
            {
                frameStart = Time.realtimeSinceStartup;
                yield return null;
            }
        }

        // Filter and Sort by distance
        _sortedTransmitters = _allTransmitters
            .Where(t => IsTargetCategory(t))
            .OrderBy(t => t.Distance)
            .ToList();

        _scanInProgress = false;
    }

    private bool IsTargetCategory(TransmitterData data)
    {
        // DEBUG MODE: Show all POIs regardless of category
        if (disableFiltering)
        {
            return true;
        }

        // Use MainCategoryGroup for clean filtering (matches 4 main groups from locations.html)
        if (!string.IsNullOrEmpty(data.MainCategoryGroup))
        {
            return targetCategories.Contains(data.MainCategoryGroup);
        }

        // Fallback to legacy Maki/Category matching (for backwards compatibility)
        if (!string.IsNullOrEmpty(data.Maki) && targetCategories.Contains(data.Maki))
        {
            return true;
        }

        if (!string.IsNullOrEmpty(data.Category))
        {
            for (int i = 0; i < targetCategories.Length; i++)
            {
                if (data.Category.Contains(targetCategories[i]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private string GetCardinalDirection(Vector3 start, Vector3 end)
    {
        // Direction from player to target
        Vector3 dir = (end - start).normalized;
        dir.y = 0; // Flatten

        // Calculate angle relative to WORLD NORTH (0,0,1)
        // Atan2(x, z) gives angle in radians from North (clockwise positive)
        // Unity Z is North, X is East.
        // Atan2(dir.x, dir.z) returns:
        // 0 for North (0,0,1)
        // 90 for East (1,0,0)
        // 180 for South (0,0,-1)
        // -90 for West (-1,0,0)
        
        float angle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        
        // Normalize angle to 0-360
        if (angle < 0) angle += 360f;

        // Determine cardinal direction (45 degree sectors)
        if (angle >= 337.5f || angle < 22.5f) return "North";
        if (angle >= 22.5f && angle < 67.5f) return "NE";
        if (angle >= 67.5f && angle < 112.5f) return "East";
        if (angle >= 112.5f && angle < 157.5f) return "SE";
        if (angle >= 157.5f && angle < 202.5f) return "South";
        if (angle >= 202.5f && angle < 247.5f) return "SW";
        if (angle >= 247.5f && angle < 292.5f) return "West";
        if (angle >= 292.5f && angle < 337.5f) return "NW";
        
        return "North";
    }

    public List<TransmitterData> GetNearest(int count)
    {
        return _sortedTransmitters.Take(count).ToList();
    }
    
    public List<TransmitterData> GetAll()
    {
        return _sortedTransmitters;
    }

    public List<TransmitterData> GetNearestUnfiltered(int count)
    {
        return _allTransmitters
            .OrderBy(t => t.Distance)
            .Take(count)
            .ToList();
    }
}
