using System;
using System.Collections.Generic;
using UnityEngine;
using Kiloverse.Mapbox;

/// <summary>
/// Unity-owned spatial authority for building containment and location presence.
///
/// The player lives at a floating Unity origin, so movement is measured from the
/// controller's GPS/virtual GPS position. Containment is evaluated only when the
/// player actually moves, when nearby building geometry changes, or when a new
/// place catalog arrives. There is no timed scene scan.
/// </summary>
public class BuildingFlattener : MonoBehaviour
{
    public static BuildingFlattener Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("BuildingFlattener");
        DontDestroyOnLoad(go);
        go.AddComponent<BuildingFlattener>();
        Debug.Log("[BuildingFlattener] Auto-bootstrapped event-driven containment");
    }

    [Header("Containment")]
    public bool enableFlattening = true;

    [Tooltip("Re-evaluate after this much real or virtual GPS movement. No scan runs while stationary.")]
    public float movementThresholdMeters = 3f;

    [Tooltip("Only active building footprints within this distance are kept in the containment pool.")]
    public float nearbyBuildingRadiusMeters = 150f;

    [Tooltip("The player's ground circle may touch this far outside a footprint and still count as inside.")]
    public float footprintEdgeBufferMeters = 15f;

    [Tooltip("Extra exit-only tolerance that prevents boundary GPS jitter.")]
    public float footprintExitHysteresisMeters = 5f;

    [Header("Place Pairing")]
    [Tooltip("Fallback maximum distance between a Google place pin and an Overture footprint.")]
    public float placePairingRadiusMeters = 40f;

    [Tooltip("Entry radius used only for outdoor places that have no building footprint.")]
    public float outdoorPlaceEntryRadiusMeters = 15f;

    [Tooltip("Exit radius used only for outdoor/unavailable footprints.")]
    public float outdoorPlaceExitRadiusMeters = 50f;

    [Header("Look")]
    public Color roofColor = new Color(0.34f, 0.36f, 0.40f, 1f);
    public float overlayLift = 0.05f;

    [Header("Debug")]
    public bool verbose;
    public int nearbyBuildingCount;
    public int currentlyFlattened;
    public int pairedLocationCount;
    public string activeLocationName = "";

    private KiloFirstPersonController _player;
    private OvertureMapManager _mapManager;
    private bool _needsFullPoolRefresh = true;
    private bool _catalogDirty;
    private bool _hasLastEvaluationGps;
    private LatitudeLongitude _lastEvaluationGps;
    private Material _sharedRoofMaterial;

    private readonly HashSet<BuildingMetadata> _nearbyBuildings = new HashSet<BuildingMetadata>();
    private readonly HashSet<BuildingMetadata> _dirtyBuildings = new HashSet<BuildingMetadata>();
    private readonly Dictionary<BuildingMetadata, FlattenState> _states = new Dictionary<BuildingMetadata, FlattenState>();
    private readonly HashSet<BuildingMetadata> _insideBuildings = new HashSet<BuildingMetadata>();
    private readonly List<BuildingMetadata> _buildingScratch = new List<BuildingMetadata>();
    private readonly Dictionary<string, LocationState> _locations = new Dictionary<string, LocationState>(StringComparer.Ordinal);
    private string _activeLocationId;

    [Serializable]
    private class LocationCatalogPayload
    {
        public LocationEntry[] places;
    }

    [Serializable]
    private class LocationEntry
    {
        public string placeId;
        public string name;
        public string artifactMaterial;
        public string artifactLabel;
        public string artifactTeaser;
        public string teaser;
        public string buildingFeatureId;
        public string buildingTileKey;
        public PlaceCoordinates coordinates;
    }

    [Serializable]
    private class PlaceCoordinates
    {
        public double lat;
        public double lng;
    }

    private sealed class LocationState
    {
        public string id;
        public string name;
        public double latitude;
        public double longitude;
        public bool hasCollectible;
        public bool catalogPresent;
        public string persistedFeatureId;
        public string persistedTileKey;
        public BuildingMetadata building;
        public bool hasFootprintConfirmation;
        public double lastConfirmedPlayerLatitude;
        public double lastConfirmedPlayerLongitude;
    }

    private sealed class FlattenState
    {
        public MeshRenderer renderer;
        public readonly Dictionary<Renderer, bool> suppressedRenderers = new Dictionary<Renderer, bool>();
        public GameObject overlay;
        public Vector3[] footprintWorld;
        public float groundY;
        public Bounds worldBounds;
        public Vector3 lastBoundsCenter;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnEnable()
    {
        BuildingMetadata.ActiveStateChanged += HandleBuildingActiveStateChanged;
        BuildingMetadata.GeometryChanged += HandleBuildingGeometryChanged;
        _needsFullPoolRefresh = true;
    }

    private void OnDisable()
    {
        BuildingMetadata.ActiveStateChanged -= HandleBuildingActiveStateChanged;
        BuildingMetadata.GeometryChanged -= HandleBuildingGeometryChanged;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void HandleBuildingActiveStateChanged(BuildingMetadata building, bool active)
    {
        if (active)
        {
            if (building != null) _dirtyBuildings.Add(building);
            return;
        }
        RemoveBuilding(building);
    }

    private void HandleBuildingGeometryChanged(BuildingMetadata building)
    {
        if (building != null) _dirtyBuildings.Add(building);
    }

    private bool EnsurePlayer()
    {
        if (_player != null) return true;
        var playerObject = GameObject.Find("Player");
        _player = playerObject != null ? playerObject.GetComponent<KiloFirstPersonController>() : null;
        if (_player == null) _player = FindFirstObjectByType<KiloFirstPersonController>();
        if (_player == null) return false;
        _needsFullPoolRefresh = true;
        _hasLastEvaluationGps = false;
        Debug.Log($"[BuildingFlattener] Spatial authority tracking '{_player.name}'");
        return true;
    }

    private void LateUpdate()
    {
        if (!EnsurePlayer()) return;

        bool moved = PlayerMovedEnough();
        bool poolChanged = false;

        if (_needsFullPoolRefresh || moved)
        {
            poolChanged = RebuildNearbyPool();
            _needsFullPoolRefresh = false;
            _dirtyBuildings.Clear();
        }
        else if (_dirtyBuildings.Count > 0)
        {
            poolChanged = ProcessDirtyBuildings();
        }

        if (!moved && !poolChanged && !_catalogDirty) return;

        ResolveLocationPairings();
        EvaluateBuildingContainment();
        EvaluateLocationPresence();
        _catalogDirty = false;
    }

    private bool PlayerMovedEnough()
    {
        LatitudeLongitude gps = _player.playerGPS;
        bool valid = IsValidCoordinate(gps.Latitude, gps.Longitude);
        if (!_hasLastEvaluationGps)
        {
            if (valid)
            {
                _lastEvaluationGps = gps;
                _hasLastEvaluationGps = true;
            }
            return true;
        }
        if (!valid) return false;

        double movedMeters = Conversions.GeoDistance(
            _lastEvaluationGps.Longitude,
            _lastEvaluationGps.Latitude,
            gps.Longitude,
            gps.Latitude) * 1000.0;
        if (movedMeters < Math.Max(0.5, movementThresholdMeters)) return false;

        _lastEvaluationGps = gps;
        return true;
    }

    private bool RebuildNearbyPool()
    {
        bool changed = false;
        _buildingScratch.Clear();
        foreach (BuildingMetadata building in BuildingMetadata.ActiveBuildings)
        {
            if (building == null) continue;
            _buildingScratch.Add(building);
            changed |= AddOrRefreshNearbyBuilding(building, false);
        }

        var activeSet = new HashSet<BuildingMetadata>(_buildingScratch);
        _buildingScratch.Clear();
        foreach (BuildingMetadata building in _nearbyBuildings)
        {
            if (building == null || !activeSet.Contains(building) || !IsBuildingWithinPool(building))
                _buildingScratch.Add(building);
        }
        for (int i = 0; i < _buildingScratch.Count; i++)
        {
            RemoveBuilding(_buildingScratch[i]);
            changed = true;
        }

        nearbyBuildingCount = _nearbyBuildings.Count;
        return changed;
    }

    private bool ProcessDirtyBuildings()
    {
        bool changed = false;
        _buildingScratch.Clear();
        foreach (BuildingMetadata building in _dirtyBuildings)
            _buildingScratch.Add(building);
        _dirtyBuildings.Clear();

        for (int i = 0; i < _buildingScratch.Count; i++)
        {
            BuildingMetadata building = _buildingScratch[i];
            if (building == null || !building.isActiveAndEnabled)
            {
                RemoveBuilding(building);
                changed = true;
                continue;
            }
            changed |= AddOrRefreshNearbyBuilding(building, true);
        }
        nearbyBuildingCount = _nearbyBuildings.Count;
        return changed;
    }

    private bool AddOrRefreshNearbyBuilding(BuildingMetadata building, bool geometryChanged)
    {
        if (building == null || building.mergedLodBatch || !IsBuildingWithinPool(building))
        {
            if (_nearbyBuildings.Contains(building))
            {
                RemoveBuilding(building);
                return true;
            }
            return false;
        }

        bool wasNearby = _nearbyBuildings.Contains(building);
        if (geometryChanged && _states.ContainsKey(building))
        {
            Restore(building);
            _states.Remove(building);
        }

        if (!_states.TryGetValue(building, out FlattenState state))
        {
            state = ExtractFootprint(building);
            if (state == null) return false;
            _states[building] = state;
        }
        else
        {
            RefreshStateWorldGeometry(state);
        }

        _nearbyBuildings.Add(building);
        TryReconnectPersistedLocations(building);
        return !wasNearby || geometryChanged;
    }

    private bool IsBuildingWithinPool(BuildingMetadata building)
    {
        if (building == null || _player == null) return false;
        MeshRenderer renderer = building.GetComponentInChildren<MeshRenderer>(true);
        if (renderer == null) return false;
        return DistanceXZPointToBounds(_player.transform.position, renderer.bounds)
            <= Mathf.Max(25f, nearbyBuildingRadiusMeters);
    }

    private void RemoveBuilding(BuildingMetadata building)
    {
        if (ReferenceEquals(building, null)) return;
        _nearbyBuildings.Remove(building);
        _dirtyBuildings.Remove(building);
        Restore(building);
        _states.Remove(building);

        foreach (LocationState location in _locations.Values)
        {
            if (location.building == building) location.building = null;
        }
        nearbyBuildingCount = _nearbyBuildings.Count;
    }

    private void EvaluateBuildingContainment()
    {
        if (!enableFlattening)
        {
            _buildingScratch.Clear();
            foreach (BuildingMetadata building in _states.Keys) _buildingScratch.Add(building);
            for (int i = 0; i < _buildingScratch.Count; i++) Restore(_buildingScratch[i]);
            currentlyFlattened = 0;
            return;
        }

        Vector3 playerPosition = _player.transform.position;
        _insideBuildings.Clear();
        foreach (BuildingMetadata building in _nearbyBuildings)
        {
            if (building == null || !_states.TryGetValue(building, out FlattenState state)) continue;
            RefreshStateWorldGeometry(state);
            bool wasInside = state.overlay != null;
            float buffer = Mathf.Max(0f, footprintEdgeBufferMeters)
                + (wasInside ? Mathf.Max(0f, footprintExitHysteresisMeters) : 0f);
            if (!IsInsideFootprint(playerPosition, state, buffer))
            {
                if (wasInside) Restore(building);
                continue;
            }

            _insideBuildings.Add(building);
            if (state.overlay == null) Flatten(building, state);
            else EnforceFlattenedRenderers(building, state);
        }
        currentlyFlattened = _insideBuildings.Count;
    }

    public void ApplyLocationCatalog(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;
        LocationCatalogPayload payload;
        try
        {
            payload = JsonUtility.FromJson<LocationCatalogPayload>(json);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[BuildingFlattener] Location catalog parse failed: {ex.Message}");
            return;
        }
        if (payload == null || payload.places == null) return;

        foreach (LocationState existing in _locations.Values) existing.catalogPresent = false;
        for (int i = 0; i < payload.places.Length; i++)
        {
            LocationEntry entry = payload.places[i];
            if (entry == null || entry.coordinates == null || string.IsNullOrWhiteSpace(entry.name)) continue;
            string id = StablePlaceId(entry);
            if (!_locations.TryGetValue(id, out LocationState location))
            {
                location = new LocationState { id = id };
                _locations[id] = location;
            }

            bool coordinateChanged = IsValidCoordinate(location.latitude, location.longitude)
                && Conversions.GeoDistance(location.longitude, location.latitude,
                    entry.coordinates.lng, entry.coordinates.lat) * 1000.0 > 5.0;
            location.name = entry.name.Trim();
            location.latitude = entry.coordinates.lat;
            location.longitude = entry.coordinates.lng;
            location.hasCollectible = HasCollectible(entry);
            location.catalogPresent = true;
            if (!string.IsNullOrWhiteSpace(entry.buildingFeatureId))
                location.persistedFeatureId = entry.buildingFeatureId.Trim();
            if (!string.IsNullOrWhiteSpace(entry.buildingTileKey))
                location.persistedTileKey = entry.buildingTileKey.Trim();
            if (coordinateChanged) location.building = null;
        }

        var staleLocationIds = new List<string>();
        foreach (var pair in _locations)
        {
            if (!pair.Value.catalogPresent && pair.Key != _activeLocationId)
                staleLocationIds.Add(pair.Key);
        }
        for (int i = 0; i < staleLocationIds.Count; i++) _locations.Remove(staleLocationIds[i]);

        _catalogDirty = true;
    }

    private static string StablePlaceId(LocationEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.placeId)) return entry.placeId.Trim();
        return $"{entry.name.Trim().ToLowerInvariant()}:{entry.coordinates.lat:F6}:{entry.coordinates.lng:F6}";
    }

    private static bool HasCollectible(LocationEntry entry)
    {
        return !string.IsNullOrWhiteSpace(entry.artifactMaterial)
            || !string.IsNullOrWhiteSpace(entry.artifactLabel)
            || !string.IsNullOrWhiteSpace(entry.artifactTeaser)
            || !string.IsNullOrWhiteSpace(entry.teaser);
    }

    private void ResolveLocationPairings()
    {
        pairedLocationCount = 0;
        if (_locations.Count == 0 || _nearbyBuildings.Count == 0) return;
        foreach (LocationState location in _locations.Values)
        {
            if (location.building != null && _nearbyBuildings.Contains(location.building))
            {
                pairedLocationCount++;
                continue;
            }
            location.building = null;

            if (TryAttachPersistedPair(location))
            {
                pairedLocationCount++;
                continue;
            }
            if (!location.catalogPresent || !TryPlaceWorldPosition(location, out Vector3 placePosition)) continue;

            BuildingMetadata exact = null;
            FlattenState exactState = null;
            float exactArea = float.MaxValue;
            BuildingMetadata nearest = null;
            float nearestDistance = Mathf.Max(1f, placePairingRadiusMeters);

            foreach (BuildingMetadata building in _nearbyBuildings)
            {
                if (building == null || !_states.TryGetValue(building, out FlattenState state)) continue;
                RefreshStateWorldGeometry(state);
                if (PointInPolygonXZ(placePosition, state.footprintWorld))
                {
                    float area = state.worldBounds.size.x * state.worldBounds.size.z;
                    if (area < exactArea)
                    {
                        exact = building;
                        exactState = state;
                        exactArea = area;
                    }
                    continue;
                }

                float distance = DistanceXZPointToPolygon(placePosition, state.footprintWorld);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = building;
                }
            }

            BuildingMetadata paired = exact != null ? exact : nearest;
            if (paired == null) continue;
            location.building = paired;
            location.persistedFeatureId = paired.featureId ?? "";
            location.persistedTileKey = StableBuildingTileKey(paired);
            pairedLocationCount++;
            // Pairing is session-local. MapKit is authoritative and K1L0 no
            // longer persists a secondary place-to-building location database.
            if (verbose)
            {
                string reason = exactState != null ? "pin inside footprint" : $"nearest edge {nearestDistance:F1}m";
                Debug.Log($"[BuildingFlattener] Paired '{location.name}' -> '{paired.featureId}' ({reason})");
            }
        }
    }

    private bool TryAttachPersistedPair(LocationState location)
    {
        if (string.IsNullOrWhiteSpace(location.persistedFeatureId)
            && string.IsNullOrWhiteSpace(location.persistedTileKey)) return false;

        foreach (BuildingMetadata building in _nearbyBuildings)
        {
            if (building == null) continue;
            bool featureMatches = !string.IsNullOrWhiteSpace(location.persistedFeatureId)
                && string.Equals(location.persistedFeatureId, building.featureId, StringComparison.Ordinal);
            bool tileMatches = !string.IsNullOrWhiteSpace(location.persistedTileKey)
                && string.Equals(location.persistedTileKey, StableBuildingTileKey(building), StringComparison.Ordinal);
            if (!featureMatches && !tileMatches) continue;
            location.building = building;
            return true;
        }
        return false;
    }

    private void TryReconnectPersistedLocations(BuildingMetadata building)
    {
        string tileKey = StableBuildingTileKey(building);
        foreach (LocationState location in _locations.Values)
        {
            if (location.building != null) continue;
            bool featureMatches = !string.IsNullOrWhiteSpace(location.persistedFeatureId)
                && string.Equals(location.persistedFeatureId, building.featureId, StringComparison.Ordinal);
            bool tileMatches = !string.IsNullOrWhiteSpace(location.persistedTileKey)
                && string.Equals(location.persistedTileKey, tileKey, StringComparison.Ordinal);
            if (featureMatches || tileMatches) location.building = building;
        }
    }

    private void EvaluateLocationPresence()
    {
        if (_locations.Count == 0) return;
        Vector3 playerPosition = _player.transform.position;

        if (!string.IsNullOrEmpty(_activeLocationId)
            && _locations.TryGetValue(_activeLocationId, out LocationState active)
            && IsInsideLocation(active, playerPosition, true))
        {
            activeLocationName = active.name;
            // Re-delivery is idempotent and refreshes Swift's persisted dwell
            // confirmation after real movement/geometry changes. There is no
            // timer scan, so this does not create idle bridge traffic.
            K1L0HUD.DeliverNativeLocationPresence(active.id, active.name, true, active.building != null);
            return;
        }

        if (!string.IsNullOrEmpty(_activeLocationId))
        {
            if (_locations.TryGetValue(_activeLocationId, out LocationState exited))
                K1L0HUD.DeliverNativeLocationPresence(exited.id, exited.name, false, exited.building != null);
            _activeLocationId = null;
            activeLocationName = "";
        }

        LocationState best = null;
        double bestDistance = double.MaxValue;
        foreach (LocationState location in _locations.Values)
        {
            if (!location.catalogPresent || !location.hasCollectible || !IsInsideLocation(location, playerPosition, false)) continue;
            double distance = DistanceFromPlayerMeters(location);
            if (distance < bestDistance)
            {
                best = location;
                bestDistance = distance;
            }
        }
        if (best == null) return;

        _activeLocationId = best.id;
        activeLocationName = best.name;
        K1L0HUD.DeliverNativeLocationPresence(best.id, best.name, true, best.building != null);
    }

    private bool IsInsideLocation(LocationState location, Vector3 playerPosition, bool exiting)
    {
        if (location.building != null && _nearbyBuildings.Contains(location.building)
            && _states.TryGetValue(location.building, out FlattenState state))
        {
            RefreshStateWorldGeometry(state);
            float buffer = Mathf.Max(0f, footprintEdgeBufferMeters)
                + (exiting ? Mathf.Max(0f, footprintExitHysteresisMeters) : 0f);
            bool inside = IsInsideFootprint(playerPosition, state, buffer);
            if (inside) RememberFootprintConfirmation(location);
            return inside;
        }

        // A paired renderer can disappear briefly while a tile or LOD is
        // replaced. Distance from the Google pin is not safe here: the pin may
        // sit tens of metres from the part of a large footprint containing the
        // player. Hold presence relative to the last footprint-confirmed GPS
        // coordinate until the geometry reconnects or the player actually
        // moves beyond the explicit outdoor exit boundary.
        if (exiting && location.hasFootprintConfirmation)
        {
            LatitudeLongitude gps = _player.playerGPS;
            if (IsValidCoordinate(gps.Latitude, gps.Longitude))
            {
                double movedFromConfirmed = Conversions.GeoDistance(
                    location.lastConfirmedPlayerLongitude,
                    location.lastConfirmedPlayerLatitude,
                    gps.Longitude,
                    gps.Latitude) * 1000.0;
                return movedFromConfirmed <= Math.Max(outdoorPlaceEntryRadiusMeters, outdoorPlaceExitRadiusMeters);
            }
            return true;
        }

        // Outdoor venues, and a paired footprint temporarily unloading during
        // a tile/LOD replacement, use the old geographic hysteresis fallback.
        double threshold = exiting
            ? Math.Max(outdoorPlaceEntryRadiusMeters, outdoorPlaceExitRadiusMeters)
            : Math.Max(1f, outdoorPlaceEntryRadiusMeters);
        return DistanceFromPlayerMeters(location) <= threshold;
    }

    private void RememberFootprintConfirmation(LocationState location)
    {
        LatitudeLongitude gps = _player.playerGPS;
        if (!IsValidCoordinate(gps.Latitude, gps.Longitude)) return;
        location.hasFootprintConfirmation = true;
        location.lastConfirmedPlayerLatitude = gps.Latitude;
        location.lastConfirmedPlayerLongitude = gps.Longitude;
    }

    public bool IsLocationFootprintNear(string placeId, string placeName, Vector3 playerPosition, float edgeDistanceMeters)
    {
        LocationState location = null;
        if (!string.IsNullOrWhiteSpace(placeId)) _locations.TryGetValue(placeId.Trim(), out location);
        if (location == null && !string.IsNullOrWhiteSpace(placeName))
        {
            foreach (LocationState candidate in _locations.Values)
            {
                if (string.Equals(candidate.name, placeName, StringComparison.OrdinalIgnoreCase))
                {
                    location = candidate;
                    break;
                }
            }
        }
        if (location?.building == null || !_states.TryGetValue(location.building, out FlattenState state)) return false;
        RefreshStateWorldGeometry(state);
        return IsInsideFootprint(playerPosition, state, Mathf.Max(0f, edgeDistanceMeters));
    }

    private double DistanceFromPlayerMeters(LocationState location)
    {
        LatitudeLongitude gps = _player.playerGPS;
        if (!IsValidCoordinate(gps.Latitude, gps.Longitude)
            || !IsValidCoordinate(location.latitude, location.longitude)) return double.MaxValue;
        return Conversions.GeoDistance(gps.Longitude, gps.Latitude, location.longitude, location.latitude) * 1000.0;
    }

    private bool TryPlaceWorldPosition(LocationState location, out Vector3 worldPosition)
    {
        worldPosition = Vector3.zero;
        if (_mapManager == null) _mapManager = FindFirstObjectByType<OvertureMapManager>();
        if (_mapManager == null || _mapManager.map == null || _mapManager.map.MapInformation == null) return false;
        var mapInfo = _mapManager.map.MapInformation;
        var center = new Vector2d(mapInfo.CenterMercator.x, mapInfo.CenterMercator.y);
        worldPosition = Conversions.LatitudeLongitudeToWorldPosition(location.latitude, location.longitude, center, mapInfo.Scale)
            + _mapManager.map.transform.position;
        worldPosition.y = _player.transform.position.y;
        return true;
    }

    private static string StableBuildingTileKey(BuildingMetadata building)
    {
        if (building == null) return "";
        return $"{building.tileKey}:{building.buildingIndex}";
    }

    private static bool IsValidCoordinate(double latitude, double longitude)
    {
        return double.IsFinite(latitude) && double.IsFinite(longitude)
            && Math.Abs(latitude) <= 90.0 && Math.Abs(longitude) <= 180.0
            && (Math.Abs(latitude) > 0.000001 || Math.Abs(longitude) > 0.000001);
    }

    private FlattenState ExtractFootprint(BuildingMetadata building)
    {
        MeshFilter filter = building.GetComponentInChildren<MeshFilter>(true);
        MeshRenderer renderer = building.GetComponentInChildren<MeshRenderer>(true);
        if (filter == null || renderer == null || filter.sharedMesh == null) return null;

        Mesh mesh = filter.sharedMesh;
        Vector3[] vertices = mesh.vertices;
        if (vertices.Length < 3) return null;
        float roofThreshold = mesh.bounds.max.y - 0.001f;
        Matrix4x4 localToWorld = filter.transform.localToWorldMatrix;
        var roofWorld = new List<Vector3>(16);
        for (int i = 0; i < vertices.Length; i++)
        {
            if (vertices[i].y >= roofThreshold) roofWorld.Add(localToWorld.MultiplyPoint3x4(vertices[i]));
            else if (roofWorld.Count > 0) break;
        }

        if (roofWorld.Count < 3)
        {
            Bounds bounds = renderer.bounds;
            roofWorld.Clear();
            roofWorld.Add(new Vector3(bounds.min.x, bounds.min.y, bounds.min.z));
            roofWorld.Add(new Vector3(bounds.max.x, bounds.min.y, bounds.min.z));
            roofWorld.Add(new Vector3(bounds.max.x, bounds.min.y, bounds.max.z));
            roofWorld.Add(new Vector3(bounds.min.x, bounds.min.y, bounds.max.z));
        }

        float groundY = localToWorld.MultiplyPoint3x4(new Vector3(0f, mesh.bounds.min.y, 0f)).y;
        for (int i = 0; i < roofWorld.Count; i++)
            roofWorld[i] = new Vector3(roofWorld[i].x, groundY, roofWorld[i].z);

        return new FlattenState
        {
            renderer = renderer,
            footprintWorld = roofWorld.ToArray(),
            groundY = groundY + overlayLift,
            worldBounds = renderer.bounds,
            lastBoundsCenter = renderer.bounds.center
        };
    }

    private static void RefreshStateWorldGeometry(FlattenState state)
    {
        if (state?.renderer == null) return;
        Bounds nextBounds = state.renderer.bounds;
        Vector3 shift = nextBounds.center - state.lastBoundsCenter;
        if (shift.sqrMagnitude > 0.000001f)
        {
            for (int i = 0; i < state.footprintWorld.Length; i++) state.footprintWorld[i] += shift;
            state.groundY += shift.y;
            if (state.overlay != null) state.overlay.transform.position += shift;
        }
        state.worldBounds = nextBounds;
        state.lastBoundsCenter = nextBounds.center;
    }

    private void Flatten(BuildingMetadata building, FlattenState state)
    {
        if (building == null || state.renderer == null) return;
        building.runtimeFlattened = true;
        EnforceFlattenedRenderers(building, state);
        state.overlay = BuildRoofOverlay(state);
        if (verbose) Debug.Log($"[BuildingFlattener] Flattened '{building.buildingName}' id={building.featureId}");
    }

    private static void EnforceFlattenedRenderers(BuildingMetadata building, FlattenState state)
    {
        if (building == null || state == null) return;
        foreach (Renderer renderer in building.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null) continue;
            if (!state.suppressedRenderers.ContainsKey(renderer))
                state.suppressedRenderers[renderer] = renderer.enabled;
            renderer.forceRenderingOff = true;
            renderer.enabled = false;
        }
    }

    private void Restore(BuildingMetadata building)
    {
        if (ReferenceEquals(building, null) || !_states.TryGetValue(building, out FlattenState state)) return;
        if (building != null) building.runtimeFlattened = false;
        foreach (var pair in state.suppressedRenderers)
        {
            if (pair.Key == null) continue;
            pair.Key.forceRenderingOff = false;
            pair.Key.enabled = pair.Value;
        }
        state.suppressedRenderers.Clear();
        if (state.overlay != null) Destroy(state.overlay);
        state.overlay = null;
    }

    private GameObject BuildRoofOverlay(FlattenState state)
    {
        int count = state.footprintWorld.Length;
        float centerX = 0f;
        float centerZ = 0f;
        for (int i = 0; i < count; i++)
        {
            centerX += state.footprintWorld[i].x;
            centerZ += state.footprintWorld[i].z;
        }
        centerX /= count;
        centerZ /= count;

        var go = new GameObject("K1L0 Building Roof Overlay");
        go.transform.position = new Vector3(centerX, state.groundY, centerZ);
        var vertices = new Vector3[count];
        for (int i = 0; i < count; i++)
            vertices[i] = new Vector3(state.footprintWorld[i].x - centerX, 0f, state.footprintWorld[i].z - centerZ);
        var triangles = new int[(count - 2) * 3];
        for (int i = 0; i < count - 2; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = i + 2;
        }

        var mesh = new Mesh { name = "K1L0 Roof Polygon", vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = EnsureRoofMaterial();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return go;
    }

    private Material EnsureRoofMaterial()
    {
        if (_sharedRoofMaterial != null) return _sharedRoofMaterial;
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        _sharedRoofMaterial = new Material(shader) { name = "K1L0 Roof Overlay" };
        if (_sharedRoofMaterial.HasProperty("_BaseColor")) _sharedRoofMaterial.SetColor("_BaseColor", roofColor);
        if (_sharedRoofMaterial.HasProperty("_Color")) _sharedRoofMaterial.SetColor("_Color", roofColor);
        return _sharedRoofMaterial;
    }

    private static bool IsInsideFootprint(Vector3 point, FlattenState state, float edgeBuffer)
    {
        if (state == null || state.footprintWorld == null || state.footprintWorld.Length < 3) return false;
        if (DistanceXZPointToBounds(point, state.worldBounds) > edgeBuffer) return false;
        if (PointInPolygonXZ(point, state.footprintWorld)) return true;
        if (DistanceXZPointToPolygon(point, state.footprintWorld) <= edgeBuffer) return true;

        // Mesh roof ordering is imperfect in a few source records. A small,
        // individual building's AABB is a safe final fallback; merged batches
        // never enter the nearby pool.
        return state.worldBounds.size.x <= 150f && state.worldBounds.size.z <= 150f
            && ContainsXZ(state.worldBounds, point);
    }

    private static bool PointInPolygonXZ(Vector3 point, Vector3[] polygon)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            float xi = polygon[i].x;
            float zi = polygon[i].z;
            float xj = polygon[j].x;
            float zj = polygon[j].z;
            bool intersects = ((zi > point.z) != (zj > point.z))
                && point.x < (xj - xi) * (point.z - zi) / ((zj - zi) + 1e-9f) + xi;
            if (intersects) inside = !inside;
        }
        return inside;
    }

    private static bool ContainsXZ(Bounds bounds, Vector3 point)
    {
        return point.x >= bounds.min.x && point.x <= bounds.max.x
            && point.z >= bounds.min.z && point.z <= bounds.max.z;
    }

    private static float DistanceXZPointToBounds(Vector3 point, Bounds bounds)
    {
        float xGap = point.x < bounds.min.x ? bounds.min.x - point.x
            : point.x > bounds.max.x ? point.x - bounds.max.x : 0f;
        float zGap = point.z < bounds.min.z ? bounds.min.z - point.z
            : point.z > bounds.max.z ? point.z - bounds.max.z : 0f;
        return Mathf.Sqrt(xGap * xGap + zGap * zGap);
    }

    private static float DistanceXZPointToPolygon(Vector3 point, Vector3[] polygon)
    {
        float bestSquared = float.MaxValue;
        Vector2 p = new Vector2(point.x, point.z);
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            Vector2 a = new Vector2(polygon[j].x, polygon[j].z);
            Vector2 b = new Vector2(polygon[i].x, polygon[i].z);
            Vector2 edge = b - a;
            float denominator = edge.sqrMagnitude;
            float t = denominator > 0.000001f ? Mathf.Clamp01(Vector2.Dot(p - a, edge) / denominator) : 0f;
            bestSquared = Mathf.Min(bestSquared, (p - (a + edge * t)).sqrMagnitude);
        }
        return Mathf.Sqrt(bestSquared);
    }
}
