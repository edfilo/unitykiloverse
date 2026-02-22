using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class POICanvasManager : MonoBehaviour
{
    public static POICanvasManager Instance { get; private set; }

    [Header("UI Settings")]
    public GameObject labelPrefab; 
    public Canvas canvas;
    public Color labelColor = new Color(1f, 1f, 1f, 0.8f); // White with 0.8 alpha
    public float verticalOffset = 20f; 
    public float fontSize = 72f; 

    [Header("Visibility Settings")]
    [Tooltip("Maximum distance to show POI labels (meters). Default: 1600m (~1 mile)")]
    public float maxDistance = 1600f;
    
    [Tooltip("POIs within this distance ALWAYS show, ignoring all culling. Default: 30m")]
    public float neverCullDistance = 30f;

    [Header("Culling Options")]
    [Tooltip("Use 'elevator logic' to lift labels above buildings when occluded. Raycasts from camera to POI, if blocked, tries heights up to 100m.")]
    public bool useElevatorLogic = true;
    
    [Tooltip("Layer mask for occlusion raycasts (what blocks labels)")]
    public LayerMask occlusionLayers = ~0;
    
    [Tooltip("Hide labels that overlap closer labels on screen")]
    public bool useOverlapCulling = true;

    private Dictionary<Transform, GameObject> _trackedPOIs = new Dictionary<Transform, GameObject>();
    private Camera _mainCamera;

    // Culling Structures
    private struct LabelCandidate
    {
        public GameObject label;
        public float distance;
        public Rect screenRect;
        public bool isVeryClose; // Never cull if true
    }

    private bool _statsLogged = false;
    private List<LabelCandidate> _candidates = new List<LabelCandidate>();
    private List<Rect> _occupiedRects = new List<Rect>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        EnsureCanvas();
        if (labelPrefab == null) CreateDefaultLabelPrefab();
    }

    private void OnEnable()
    {
        _mainCamera = Camera.main;
    }

private void LateUpdate()
    {
        if (_mainCamera == null || !_mainCamera.isActiveAndEnabled)
        {
            _mainCamera = Camera.main;
        }

        if (_mainCamera == null || canvas == null) return;

        List<Transform> toRemove = new List<Transform>();
        Vector3 camPos = _mainCamera.transform.position;

        // Clear per-frame lists
        _candidates.Clear();
        _occupiedRects.Clear();

        // Tracking variables for logging (every 5 seconds)
        int totalPOIs = _trackedPOIs.Count;
        int tooFar = 0;
        int offScreen = 0;
        int occluded = 0;
        int overlapped = 0;
        int visible = 0;

        foreach (var kvp in _trackedPOIs)
        {
            Transform target = kvp.Key;
            GameObject label = kvp.Value;

            if (target == null)
            {
                toRemove.Add(target);
                if (label != null) Destroy(label);
                continue;
            }

            if (label == null) continue;

            // NOTE: POI anchors are NOT tile-culled (they stay active across all tiles)
            // POICanvasManager handles all culling: distance, frustum, occlusion, overlap

            // 1. Distance Check
            float distance = Vector3.Distance(camPos, target.position);

            // NEVER CULL: POIs within neverCullDistance always show, skip all culling
            bool isVeryClose = distance <= neverCullDistance;

            if (!isVeryClose && distance > maxDistance)
            {
                if (label.activeSelf) label.SetActive(false);
                tooFar++;
                continue;
            }

            // 2. Frustum Check (skip if very close)
            // z > 0 means in front of camera
            // Also check if beyond far clip plane
            Vector3 screenPos = _mainCamera.WorldToScreenPoint(target.position);

            if (!isVeryClose && (screenPos.z <= 0 || screenPos.z > _mainCamera.farClipPlane ||
                screenPos.x < -100 || screenPos.x > Screen.width + 100 ||
                screenPos.y < -100 || screenPos.y > Screen.height + 100))
            {
                if (label.activeSelf) label.SetActive(false);
                offScreen++;
                continue;
            }

            // 3. Occlusion Check (Elevator Logic) - skip if very close or disabled
            bool isOccluded = !isVeryClose && useElevatorLogic; // Assume occluded until proven visible (unless very close or disabled)
            Vector3 finalTargetPos = target.position;

            if (!isVeryClose && useElevatorLogic)
            {
                float maxElevatorHeight = 100f; // Max height to lift label
                float elevatorStep = 10f; // Check every 10 meters

                for (float heightOffset = 0; heightOffset <= maxElevatorHeight; heightOffset += elevatorStep)
                {
                    Vector3 testPos = target.position + Vector3.up * heightOffset;
                    Vector3 direction = (testPos - camPos).normalized;
                    Vector3 rayStart = camPos + direction * 0.2f; // Offset to avoid self-collision
                    float distToTarget = Vector3.Distance(camPos, testPos);

                    // Check if line of sight is clear
                    if (!Physics.Raycast(rayStart, direction, distToTarget - 0.5f, occlusionLayers))
                    {
                        // Path is clear!
                        isOccluded = false;
                        finalTargetPos = testPos;
                        
                        // If we moved it up, recalculate screen position
                        if (heightOffset > 0)
                        {
                            screenPos = _mainCamera.WorldToScreenPoint(finalTargetPos);
                        }
                        break; 
                    }
                }
            }
            else
            {
                isOccluded = false;
            }

            if (isOccluded)
            {
                if (label.activeSelf) label.SetActive(false);
                occluded++;
                continue;
            }

            // If we are here, the label IS visible (possibly elevated).
            // Now calculate screen rect for overlap checking.
            
            // Get Rect size (approximate or actual)
            RectTransform rt = label.transform as RectTransform;
            float width = rt != null ? rt.rect.width : 200f;
            float height = rt != null ? rt.rect.height : 50f;
            
            // Apply offset
            Vector3 finalScreenPos = new Vector3(screenPos.x, screenPos.y + verticalOffset, 0);
            
            Rect currentRect = new Rect(finalScreenPos.x - width/2, finalScreenPos.y - height/2, width, height);

            _candidates.Add(new LabelCandidate()
            {
                label = label,
                distance = distance,
                screenRect = currentRect,
                isVeryClose = isVeryClose
            });
        }

        // Cleanup destroyed targets
        foreach (var t in toRemove) _trackedPOIs.Remove(t);

        // --- OVERLAP CULLING PHASE (only if enabled) ---
        
        if (useOverlapCulling)
        {
            // Sort by distance (Closest first)
            _candidates.Sort((a, b) => a.distance.CompareTo(b.distance));

            foreach (var candidate in _candidates)
            {
                // NEVER CULL: Very close POIs always show, skip overlap check
                bool overlaps = false;
                if (!candidate.isVeryClose)
                {
                    // Check if label overlaps with any closer labels
                    foreach (var occupied in _occupiedRects)
                    {
                        if (candidate.screenRect.Overlaps(occupied))
                        {
                            overlaps = true;
                            break;
                        }
                    }
                }

                if (overlaps)
                {
                    // Hidden by a closer label
                    if (candidate.label.activeSelf) candidate.label.SetActive(false);
                    overlapped++;
                }
                else
                {
                    // Show label that passed all culling checks (or is very close)
                    if (!candidate.label.activeSelf) candidate.label.SetActive(true);
                    visible++;

                    // Set position correctly for ScreenSpaceOverlay canvas
                    RectTransform rt = candidate.label.transform as RectTransform;
                    if (rt != null)
                    {
                        rt.position = new Vector3(candidate.screenRect.center.x, candidate.screenRect.center.y, 0);
                    }

                    // Mark this screen space as occupied
                    _occupiedRects.Add(candidate.screenRect);
                }
            }
        }
        else
        {
            // Overlap culling disabled - show all candidates
            foreach (var candidate in _candidates)
            {
                if (!candidate.label.activeSelf) candidate.label.SetActive(true);
                visible++;

                // Set position correctly for ScreenSpaceOverlay canvas
                RectTransform rt = candidate.label.transform as RectTransform;
                if (rt != null)
                {
                    rt.position = new Vector3(candidate.screenRect.center.x, candidate.screenRect.center.y, 0);
                }
            }
        }

        // Log stats every 5 seconds
        if (Time.frameCount % 300 == 0 && totalPOIs > 0)
        {
            string elevatorStatus = useElevatorLogic ? "ON" : "OFF";
            string overlapStatus = useOverlapCulling ? "ON" : "OFF";
            Debug.Log($"[POICanvas] {totalPOIs} POIs: {visible} visible | Hidden: {tooFar} too-far, {offScreen} off-screen, {occluded} occluded, {overlapped} overlapped | Elevator: {elevatorStatus}, Overlap: {overlapStatus}");

            // Log sample POIs showing tile indexes
            int sampleCount = 0;
            foreach (var kvp in _trackedPOIs)
            {
                if (kvp.Key != null && sampleCount < 3)
                {
                    string labelStatus = kvp.Value != null && kvp.Value.activeSelf ? "VISIBLE" : "HIDDEN";
                    float dist = Vector3.Distance(camPos, kvp.Key.position);
                    Debug.Log($"[POICanvas] Sample: {kvp.Key.name} | {labelStatus} | {dist:F0}m");
                    sampleCount++;
                }
            }
        }
    }

public void RegisterPOI(Transform target, string text)
    {
        if (target == null)
        {
            Debug.LogWarning("[POICanvas] RegisterPOI called with null target");
            return;
        }

        if (_trackedPOIs.ContainsKey(target))
        {
            return;
        }

        // Spawn UI Label
        GameObject uiLabel = Instantiate(labelPrefab, canvas.transform);
        uiLabel.name = $"UI_Label_{text}";

        var tmp = uiLabel.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null)
        {
            tmp.text = text;
            tmp.color = labelColor;
            tmp.fontSize = fontSize;
        }
        else
        {
            Debug.LogError($"[POICanvas] No TextMeshProUGUI found in label for: {text}");
        }

        _trackedPOIs.Add(target, uiLabel);
    }

    public void UnregisterPOI(Transform target)
    {
        if (target != null && _trackedPOIs.TryGetValue(target, out GameObject label))
        {
            if (label != null) Destroy(label);
            _trackedPOIs.Remove(target);
        }
    }

    private void EnsureCanvas()
    {
        if (canvas != null) return;

        GameObject canvasObj = new GameObject("POI_Canvas");
        canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // On top of everything

        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasObj.AddComponent<GraphicRaycaster>();

        Debug.Log("[TileLoad] Created POI_Canvas for label rendering (ScreenSpaceOverlay, sortingOrder=100)");
    }

    private void CreateDefaultLabelPrefab()
    {
        // Create a simple text prefab in memory
        labelPrefab = new GameObject("DefaultLabelPrefab");
        labelPrefab.transform.SetParent(transform); // Hide it under manager
        labelPrefab.SetActive(false);

        var tmp = labelPrefab.AddComponent<TextMeshProUGUI>();
        tmp.text = "POI";
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = labelColor;
        tmp.enableWordWrapping = false;
        
        // Add shadow/outline for readability
        var shadow = labelPrefab.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.8f);
        shadow.effectDistance = new Vector2(2, -2);
        
        // Ensure rect transform is centered
        RectTransform rt = labelPrefab.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(400, 100); // 400x100 for large text
    }
}