using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Linq;

using Mapbox.BaseModule.Data.DataFetchers;
using Kiloverse.Mapbox;
using Mapbox.BaseModule.Data.Tiles;
using Kiloverse.Mapbox;
using Mapbox.UnityMapService;
using Kiloverse.Mapbox;
using Mapbox.VectorTile;
using Kiloverse.Mapbox;
using Mapbox.GeocodingApi; // Correct namespace
using Kiloverse.Mapbox;
using KiloWorld.Rendering.Systems;

/// <summary>
/// Displays current city/town/village name in top-left UI
/// Uses Reverse Geocoding API for reliable city names
/// </summary>
public class LocationLabelUI : MonoBehaviour
{
    [Header("UI")]
    public Text locationText;
    
    [Header("Settings")]
    public float updateDistanceThreshold = 1000f; // Update city if moved > 1km
    
    private KiloverseMapInfo _map;
    private Mapbox.BaseModule.Data.Vector2d.LatitudeLongitude _lastLocation; // Fixed type
    private ReverseGeocodeResource _resource;
    private MapboxGeocodingApi _geocoder;

    void Start()
    {
        Debug.Log("[LocationLabelUI] Start() called");
        // Defer Find and init to next frame so we don't block boot (FindFirstObjectByType can lock editor)
        StartCoroutine(InitNextFrame());
    }

    IEnumerator InitNextFrame()
    {
        yield return null;

        // Editor / GPS unavailable: show profile default location (e.g. Pittsburgh) instead of "Finding location..."
        if (Application.isEditor || !GPSLocationController.GPSReady)
        {
            var renderManager = FindFirstObjectByType<RenderManager>();
            if (renderManager != null && renderManager.profile != null && locationText != null)
            {
                string defaultName = renderManager.profile.startupLocation.GetStartupLocationDisplayName();
                locationText.text = defaultName;
                Debug.Log($"[LocationLabelUI] Using profile default location (GPS unavailable): {defaultName}");
            }
            // In editor with Kiloverse tiles, map may not have MapboxMap - skip geocoding entirely
            if (Application.isEditor)
            {
                yield break;
            }
        }

        _map = FindFirstObjectByType<KiloverseMapInfo>();
        if (_map == null)
        {
            Debug.LogWarning("[LocationLabelUI] KiloverseMapInfo not found!");
            yield break;
        }

        Debug.Log($"[LocationLabelUI] Map found. MapboxMap null? {_map.MapboxMap == null}");

        StartCoroutine(WaitForMapInitialization());
    }

    System.Collections.IEnumerator WaitForMapInitialization()
    {
        // Wait for map to be ready
        while (_map.MapboxMap == null || _map.MapboxMap.MapService == null)
        {
            yield return new UnityEngine.WaitForSeconds(0.5f);
        }

        Debug.Log("[LocationLabelUI] Map initialized, setting up geocoder");

        // Skip geocoding for Kiloverse tiles (no Mapbox API needed)
        if (_map.MapboxMap.MapService.FileSource != null)
        {
            _geocoder = new MapboxGeocodingApi(_map.MapboxMap.MapService.FileSource as Mapbox.BaseModule.Data.Platform.IFileSource);
            _resource = new ReverseGeocodeResource(_map.MapInformation.LatitudeLongitude);
            DoReverseGeocoding();
        }
        else
        {
            Debug.Log("[LocationLabelUI] Skipping Mapbox geocoding (using Kiloverse tiles)");
        }
    }
    
    private static bool _loggedFirstUpdateAfterBoot;
    void Update()
    {
        if (BootState.AllowPlayer && !_loggedFirstUpdateAfterBoot)
        {
            _loggedFirstUpdateAfterBoot = true;
            BootDiagnostics.Mark("LocationLabelUI first Update after AllowPlayer");
        }
        // Editor: skip Update work for 2s after AllowPlayer (lockup started when we broke up boot - pile-up of Finds/geocoder)
        if (Application.isEditor && BootState.AllowPlayer && (Time.realtimeSinceStartup - BootState.AllowPlayerTime) < 2f)
            return;
        if (_map == null || _map.MapInformation == null) return;
        if (_geocoder == null) return; // Wait for geocoder to initialize

        // Use LatitudeLongitude property
        var currentLoc = _map.MapInformation.LatitudeLongitude;

        // Calculate distance (approximate math for speed)
        // 1 degree lat ~ 111km. 0.01 ~ 1.1km.
        // Or use accurate GeoDistance (returns KM)
        double distKm = Mapbox.BaseModule.Utilities.Conversions.GeoDistance(
            _lastLocation.Longitude, _lastLocation.Latitude,
            currentLoc.Longitude, currentLoc.Latitude);

        if (distKm > 1.0) // 1 km threshold
        {
            DoReverseGeocoding();
        }
    }

    void DoReverseGeocoding()
    {
        if (_map == null || _geocoder == null)
        {
            Debug.LogWarning($"[LocationLabelUI] Cannot geocode - map null? {_map == null}, geocoder null? {_geocoder == null}");
            return;
        }

        // Update last checked location
        _lastLocation = _map.MapInformation.LatitudeLongitude;

        // Get current map center (player location)
        var center = _map.MapInformation.LatitudeLongitude;
        _resource.Query = center;
        // Prefer larger cities: "place" (cities/towns), "region" (states/provinces), exclude "neighborhood"
        _resource.Types = new string[] { "place", "locality" };

        Debug.Log($"[LocationLabelUI] Requesting reverse geocode for: {center.Latitude}, {center.Longitude}");

        // Let compiler infer T based on callback type
        _geocoder.Geocode(_resource, (ReverseGeocodeResponse response) => {
            Debug.Log($"[LocationLabelUI] Geocode response received. Features count: {response.Features?.Count ?? 0}");

            if (response.Features != null && response.Features.Count > 0)
            {
                // Log all features for debugging
                for (int i = 0; i < response.Features.Count; i++)
                {
                    var f = response.Features[i];
                    Debug.Log($"[LocationLabelUI] Feature {i}: {f.Text} (place_name: {f.PlaceName})");
                }

                // Get the most relevant feature (largest administrative area)
                var feature = response.Features[0];
                string placeName = feature.Text; // e.g. "Pittsburgh" instead of "Shadyside"

                Debug.Log($"[LocationLabelUI] ✓ Geocoded location: {placeName}");

                // Update UI
                if (locationText != null)
                {
                    locationText.text = placeName;
                }
                else
                {
                    Debug.LogWarning("[LocationLabelUI] locationText is null!");
                }
            }
            else
            {
                Debug.LogWarning("[LocationLabelUI] No features returned from geocoding");
            }
        });
    }
}
