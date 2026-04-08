using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Kiloverse.Mapbox;

public class POILabelBridge : MonoBehaviour
{
    [Header("Settings")]
    public int maxLabels = 20;
    public float maxDistance = 800f;
    public float refreshInterval = 2f;
    public float defaultLabelHeight = 8f;
    public float heightPadding = 3f;

    [Header("Label Appearance")]
    public float fontSize = 12f;
    public Color labelColor = new Color(1f, 1f, 1f, 0.95f);

    private float _lastRefreshTime = -999f;
    private Camera _mainCamera;
    private Canvas _canvas;

    private TransmitterScanner _scanner;
    private OvertureMapManager _overtureManager;

    private class POIEntry
    {
        public string name;
        public Vector2d geoLocation;
        public GameObject uiLabel;
        public RectTransform rt;
        public TextMeshProUGUI tmp;
        public float buildingHeight;
        public bool heightCached;
    }

    private List<POIEntry> _entries = new List<POIEntry>();
    private bool _firstLog = true;
    private static POILabelBridge _instance;
    private int _tickCount = 0;

    void Start()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        var canvasGO = new GameObject("POILabelCanvas");
        canvasGO.transform.SetParent(transform);
        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 100;

        Debug.Log("[POILabelBridge] Started v9 - Update() tick (works on device + editor)");
    }

    void Update()
    {
        // EditorApplication.update only works in editor; use Update() everywhere
        _tickCount++;
        if (_tickCount <= 3 || _tickCount % 600 == 0)
            Debug.Log($"[POILabelBridge] Tick #{_tickCount}, entries={_entries.Count}");
        DoTick();
    }

    void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    void DoTick()
    {
        // Find camera
        if (_mainCamera == null || !_mainCamera.isActiveAndEnabled)
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null)
            {
                foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None))
                    if (c.isActiveAndEnabled) { _mainCamera = c; break; }
            }
        }
        if (_mainCamera == null) return;

        // Find TransmitterScanner
        if (_scanner == null)
        {
            _scanner = FindFirstObjectByType<TransmitterScanner>();
            if (_scanner != null)
            {
                Debug.Log($"[POILabelBridge] Found TransmitterScanner, usePlacesApi={_scanner.usePlacesApi}");
                _scanner.EnsurePlacesApiRunning();
            }
            else return;
        }

        // Find OvertureMapManager
        if (_overtureManager == null)
        {
            _overtureManager = FindFirstObjectByType<OvertureMapManager>();
            if (_overtureManager != null)
                Debug.Log("[POILabelBridge] Found OvertureMapManager");
        }
        if (_overtureManager == null) return;

        // Refresh POI list periodically
        if (Time.realtimeSinceStartup - _lastRefreshTime >= refreshInterval)
        {
            _lastRefreshTime = Time.realtimeSinceStartup;
            RefreshPOIs();
        }

        UpdateScreenPositions();
    }

    private void RefreshPOIs()
    {
        if (_scanner == null) return;
        var allPOIs = _scanner.GetNearestUnfiltered(maxLabels * 2);
        if (_firstLog)
            Debug.Log($"[POILabelBridge] RefreshPOIs: allPOIs={allPOIs?.Count ?? -1}");
        if (allPOIs == null || allPOIs.Count == 0) return;

        List<TransmitterScanner.TransmitterData> nearby = new List<TransmitterScanner.TransmitterData>();
        foreach (var poi in allPOIs)
        {
            if (poi.Distance <= maxDistance)
                nearby.Add(poi);
            if (nearby.Count >= maxLabels) break;
        }

        HashSet<string> desiredKeys = new HashSet<string>();
        foreach (var poi in nearby)
            desiredKeys.Add(MakeKey(poi));

        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            string key = MakeKey(_entries[i]);
            if (!desiredKeys.Contains(key))
            {
                if (_entries[i].uiLabel != null) Destroy(_entries[i].uiLabel);
                _entries.RemoveAt(i);
            }
        }

        HashSet<string> existingKeys = new HashSet<string>();
        foreach (var e in _entries) existingKeys.Add(MakeKey(e));

        bool isFirst = _firstLog && nearby.Count > 0;
        foreach (var poi in nearby)
        {
            string key = MakeKey(poi);
            if (existingKeys.Contains(key)) continue;

            var entry = new POIEntry
            {
                name = poi.Name,
                geoLocation = poi.GeoLocation,
                heightCached = false
            };
            CreateLabelUI(entry);
            _entries.Add(entry);

            if (isFirst)
                Debug.Log($"[POILabelBridge] + {poi.Name} dist={poi.Distance:F0}m");
        }

        foreach (var entry in _entries)
        {
            if (entry.tmp == null) continue;
            foreach (var poi in nearby)
            {
                if (poi.Name == entry.name && (poi.GeoLocation - entry.geoLocation).magnitude < 0.0001)
                {
                    int steps = Mathf.RoundToInt(poi.Distance / 0.762f);
                    string stepsStr = steps < 1000 ? $"{steps}" : $"{steps / 1000f:F1}k";
                    entry.tmp.text = $"{entry.name}\n<size=70%>{stepsStr} steps</size>";
                    break;
                }
            }
        }

        if (isFirst)
        {
            _firstLog = false;
            Debug.Log($"[POILabelBridge] Created {_entries.Count} labels from {allPOIs.Count} transmitters");
        }
    }

    private void UpdateScreenPositions()
    {
        IMapInformation mapInfo = _overtureManager?.map?.MapInformation;
        Vector3 mapPos = _overtureManager?.map != null ? _overtureManager.map.transform.position : Vector3.zero;

        if (mapInfo == null) return;

        var centerMercator = new Vector2d(mapInfo.CenterMercator.x, mapInfo.CenterMercator.y);

        foreach (var entry in _entries)
        {
            if (entry.uiLabel == null) continue;

            Vector3 worldPos = Conversions.LatitudeLongitudeToWorldPosition(
                entry.geoLocation.x, entry.geoLocation.y,
                centerMercator, mapInfo.Scale);
            worldPos += mapPos;

            if (!entry.heightCached)
            {
                entry.buildingHeight = GetBuildingHeightAt(worldPos);
                entry.heightCached = true;
            }

            worldPos.y = entry.buildingHeight + heightPadding;

            Vector3 screenPos = _mainCamera.WorldToScreenPoint(worldPos);

            bool visible = screenPos.z > 0 &&
                           screenPos.x > 0 && screenPos.x < Screen.width &&
                           screenPos.y > 0 && screenPos.y < Screen.height;

            if (visible != entry.uiLabel.activeSelf)
                entry.uiLabel.SetActive(visible);

            if (visible && entry.rt != null)
                entry.rt.position = new Vector3(screenPos.x, screenPos.y, 0);
        }
    }

    private void CreateLabelUI(POIEntry entry)
    {
        GameObject labelGO = new GameObject($"Label_{entry.name}");
        labelGO.transform.SetParent(_canvas.transform, false);

        RectTransform rt = labelGO.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(300, 50);
        rt.pivot = new Vector2(0.5f, 0f);

        TextMeshProUGUI tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text = entry.name;
        tmp.fontSize = fontSize;
        tmp.color = labelColor;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        tmp.fontStyle = FontStyles.Bold;

        tmp.outlineWidth = 0.35f;
        tmp.outlineColor = new Color32(0, 0, 0, 220);

        entry.uiLabel = labelGO;
        entry.rt = rt;
        entry.tmp = tmp;
    }

    private float GetBuildingHeightAt(Vector3 worldPos)
    {
        Vector3 rayStart = new Vector3(worldPos.x, 200f, worldPos.z);
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 250f))
        {
            var metadata = hit.collider.GetComponent<BuildingMetadata>();
            if (metadata != null)
                return hit.collider.bounds.max.y;

            var renderer = hit.collider.GetComponent<Renderer>();
            if (renderer != null)
                return renderer.bounds.max.y;
        }
        return defaultLabelHeight;
    }

    private string MakeKey(TransmitterScanner.TransmitterData poi)
    {
        return $"{poi.Name}_{poi.GeoLocation.x:F5}_{poi.GeoLocation.y:F5}";
    }

    private string MakeKey(POIEntry entry)
    {
        return $"{entry.name}_{entry.geoLocation.x:F5}_{entry.geoLocation.y:F5}";
    }
}
