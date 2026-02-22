// LabelManager.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Singleton that owns a single overlay canvas and pools EntityLabel UI objects.
/// All EntityLabelController components register with this manager.
/// </summary>
public class LabelManager : MonoBehaviour
{
    public static LabelManager Instance { get; private set; }

    [Header("Prefab & Canvas Settings")]
    [Tooltip("Prefab that contains EntityLabel component and UI elements (name, health bar, icon).")]
    public GameObject labelPrefab;

    // The canvas that will hold all label instances.
    private Canvas overlayCanvas;

    // Pool of inactive label instances.
    private readonly Queue<EntityLabel> labelPool = new Queue<EntityLabel>();

    // Active mapping: target Transform -> label instance.
    private readonly Dictionary<Transform, EntityLabel> activeLabels = new Dictionary<Transform, EntityLabel>();

    // Smoothing factor for screen‑space movement (0 = instant, 1 = never move)
    private const float SmoothFactor = 0.15f;

    private Camera mainCam;
    private float nextLogTime = 0f;
    private const float LogInterval = 3f; // Log every 3 seconds

    private void Awake()
    {
        // Enforce singleton.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Find or create the overlay canvas.
        overlayCanvas = FindObjectOfType<Canvas>();
        if (overlayCanvas == null || overlayCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            GameObject canvasGO = new GameObject("WorldOverlayCanvas");
            overlayCanvas = canvasGO.AddComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = 1000; // on top of everything.
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
        }
        else
        {
            // Force overlay mode and high sorting order for safety.
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = 1000;
        }

        mainCam = Camera.main;
    }

    #region Pooling Helpers
    private EntityLabel GetLabel()
    {
        if (labelPool.Count > 0)
        {
            var pooledLbl = labelPool.Dequeue();
            pooledLbl.gameObject.SetActive(true);
            return pooledLbl;
        }
        // Instantiate a new label from the prefab.
        if (labelPrefab == null)
        {
            Debug.LogError("[LabelManager] No labelPrefab assigned – cannot create labels.");
            return null;
        }
        var go = Instantiate(labelPrefab, overlayCanvas.transform);
        var lbl = go.GetComponent<EntityLabel>();
        if (lbl == null)
        {
            Debug.LogError("[LabelManager] labelPrefab does not contain an EntityLabel component.");
        }
        return lbl;
    }

    private void ReleaseLabel(EntityLabel lbl)
    {
        lbl.gameObject.SetActive(false);
        labelPool.Enqueue(lbl);
    }
    #endregion

    #region Public API (called by EntityLabelController)
    /// <summary>
    /// Register a target object for a UI label.
    /// </summary>
    public void Register(Transform target, string displayName, float health = 1f, Sprite icon = null)
    {
        if (target == null) return;
        if (!activeLabels.TryGetValue(target, out var label))
        {
            label = GetLabel();
            if (label == null)
            {
                Debug.LogError("[LabelManager] Failed to get label from pool/prefab!");
                return; 
            }
            activeLabels[target] = label;
        }
        label.SetName(displayName);
        label.SetHealth(health);
        label.SetIcon(icon);
    }

    public void Unregister(Transform target)
    {
        if (target == null) return;
        if (activeLabels.TryGetValue(target, out var label))
        {
            activeLabels.Remove(target);
            ReleaseLabel(label);
        }
    }

    public void UpdateHealth(Transform target, float health)
    {
        if (activeLabels.TryGetValue(target, out var label))
        {
            label.SetHealth(health);
        }
    }
    #endregion

    private void LateUpdate()
    {
        if (mainCam == null) mainCam = Camera.main;
        
        Transform closestTarget = null;
        Transform farthestTarget = null;
        float closestDist = float.MaxValue;
        float farthestDist = 0f;
        int visibleCount = 0;

        foreach (var kvp in activeLabels)
        {
            var target = kvp.Key;
            var label = kvp.Value;
            if (target == null)
            {
                // Target destroyed – clean up later.
                continue;
            }

            // World point a little above the target (tweak offset per use‑case)
            Vector3 worldPos = target.position + Vector3.up * 2.5f;
            Vector3 screenPos = mainCam.WorldToScreenPoint(worldPos);

            // Hide if behind camera.
            if (screenPos.z < 0)
            {
                label.gameObject.SetActive(false);
                continue;
            }

            // Clamp to screen bounds (optional).
            screenPos.x = Mathf.Clamp(screenPos.x, 0f, Screen.width);
            screenPos.y = Mathf.Clamp(screenPos.y, 0f, Screen.height);

            // Smooth movement.
            RectTransform rt = label.GetComponent<RectTransform>();
            Vector3 cur = rt.position;
            rt.position = Vector3.Lerp(cur, screenPos, SmoothFactor);
            label.gameObject.SetActive(true);
            visibleCount++;

            // Track closest/farthest
            float dist = screenPos.z;
            if (dist < closestDist)
            {
                closestDist = dist;
                closestTarget = target;
            }
            if (dist > farthestDist)
            {
                farthestDist = dist;
                farthestTarget = target;
            }
        }

        // Periodic summary logging
        if (Time.time >= nextLogTime)
        {
            nextLogTime = Time.time + LogInterval;
            if (visibleCount > 0)
            {
                string closestInfo = closestTarget != null ? $"{closestTarget.position} (dist:{closestDist:F1})" : "none";
                string farthestInfo = farthestTarget != null ? $"{farthestTarget.position} (dist:{farthestDist:F1})" : "none";
                Debug.Log($"[LabelManager] {visibleCount} visible labels | Closest: {closestInfo} | Farthest: {farthestInfo}");
            }
        }

        // Remove any entries whose target was destroyed.
        var dead = new List<Transform>();
        foreach (var kvp in activeLabels)
        {
            if (kvp.Key == null) dead.Add(kvp.Key);
        }
        foreach (var t in dead)
        {
            if (activeLabels.TryGetValue(t, out var lbl)) ReleaseLabel(lbl);
            activeLabels.Remove(t);
        }
    }
}
