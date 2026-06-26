using UnityEngine;
using System.Collections.Generic;
using TMPro;

// ─────────────────────────────────────────────────────────────────
// SignalBeamBridge  –  Visual layer for SignalDirectorV2
// ─────────────────────────────────────────────────────────────────
// Subscribes to SignalDirectorV2 events, instantiates BeamAvatar
// GameObjects, and repositions them every frame (floating origin).
// ─────────────────────────────────────────────────────────────────

public class SignalBeamBridge : MonoBehaviour
{
    public static SignalBeamBridge Instance { get; private set; }
    private const string ShowDistanceLabelsPref = "k1lo_showBeamDistanceLabels";
    private static bool hudSuppressed;
    public static bool ShowDistanceLabels => PlayerPrefs.GetInt(ShowDistanceLabelsPref, 1) != 0;

    public static void SetDistanceLabelsVisible(bool visible)
    {
        PlayerPrefs.SetInt(ShowDistanceLabelsPref, visible ? 1 : 0);
        PlayerPrefs.Save();
        if (!visible && Instance != null) Instance.HideAllDistanceLabels();
    }

    public static void SetHudSuppressed(bool suppressed)
    {
        hudSuppressed = suppressed;
    }

    private SignalDirectorV2 director;
    private readonly Dictionary<string, GameObject> beamsBySignalId = new Dictionary<string, GameObject>();
    private readonly Dictionary<string, BeamLabelEntry> labelsBySignalId = new Dictionary<string, BeamLabelEntry>();
    private Transform container;

    // Prefab + material (loaded same way as VirtualGridSpawner)
    private GameObject beamPrefab;
    private Material particleBeamMaterial;
    private KiloWorld.Rendering.KiloWorldMasterProfile profile;
    private Canvas uiCanvas;
    private Camera mainCamera;
    private KiloFirstPersonController playerController;
    private float nextLabelDebugAt;

    private class BeamLabelEntry
    {
        public GameObject go;
        public RectTransform rt;
        public TextMeshProUGUI tmp;
    }

    private class BeamLabelCandidate
    {
        public BeamLabelEntry label;
        public Signal signal;          // needed for locationName lookup in update pass
        public Vector3 screenPos;
        public float distanceMeters;
        public Rect screenRect;
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        // Container for all signal beams
        var root = GameObject.Find("RuntimeObjectsRoot");
        container = new GameObject("SignalBeams").transform;
        if (root != null) container.SetParent(root.transform);

        // Load prefab
        beamPrefab = Resources.Load<GameObject>("BeamAvatar");
        if (beamPrefab == null)
        {
            Debug.LogWarning("[SignalBeamBridge] BeamAvatar prefab not found, creating fallback sphere");
            beamPrefab = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            beamPrefab.AddComponent<BeamAvatar>();
            beamPrefab.SetActive(false);
            beamPrefab.name = "SignalBeamFallback";
        }

        // Load material
        particleBeamMaterial = Resources.Load<Material>("Materials/ParticleBeam");
        Debug.Log($"[SignalBeamBridge] Beam prefab={(beamPrefab != null ? beamPrefab.name : "null")} particleMaterial={(particleBeamMaterial != null ? particleBeamMaterial.name : "null")} profile={(profile != null ? profile.name : "null")}");

        // Profile
        profile = KiloWorld.Rendering.Systems.RenderManager.Instance?.profile;

        uiCanvas = K1L0CanvasRoot.WorldCanvas;
        mainCamera = Camera.main;
    }

    void OnEnable()
    {
        // Defer subscription until director exists
    }

    void OnDisable()
    {
        if (director != null)
        {
            director.OnSignalSpawned -= HandleSpawned;
            director.OnSignalRemoved -= HandleRemoved;
            director.OnSignalStateChanged -= HandleStateChanged;
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        // Wait for director
        if (director == null)
        {
            director = SignalDirectorV2.Instance;
            if (director == null) return;
            director.OnSignalSpawned += HandleSpawned;
            director.OnSignalRemoved += HandleRemoved;
            director.OnSignalStateChanged += HandleStateChanged;
            Debug.Log("[SignalBeamBridge] Subscribed to SignalDirectorV2");

            var existing = director.ActiveSignals;
            for (int i = 0; i < existing.Count; i++)
            {
                HandleSpawned(existing[i]);
                HandleStateChanged(existing[i]);
            }
            Debug.Log($"[SignalBeamBridge] Backfilled {existing.Count} pre-existing signals");
        }

        // Reposition all beams every frame (floating origin)
        var signals = director.ActiveSignals;
        for (int i = 0; i < signals.Count; i++)
        {
            var sig = signals[i];
            if (!beamsBySignalId.TryGetValue(sig.id, out var go)) continue;
            if (go == null) continue;

            Vector3 worldPos = director.SignalToWorldPos(sig);
            var orb = go.GetComponent<BeamAvatar>();
            if (orb != null)
                orb.SetPosition(worldPos);
            else
                go.transform.position = worldPos;
        }

        UpdateBeamDistanceLabels(signals);
    }

    // ── Event handlers ────────────────────────────────────────

    private void HandleSpawned(Signal sig)
    {
        if (beamsBySignalId.ContainsKey(sig.id)) return;

        var go = Instantiate(beamPrefab, Vector3.zero, Quaternion.identity, container);
        go.name = $"Signal_{sig.role}_{sig.id}";
        go.SetActive(false); // starts hidden, shown when state → Visible

        var orb = go.GetComponent<BeamAvatar>();
            if (orb != null)
            {
                ApplyProfileSettings(orb);
                ApplySignalAppearance(orb, sig);
                orb.RebuildBeamSystem();
            }

        beamsBySignalId[sig.id] = go;
        sig.visualGO = go;

        EnsureLabel(sig.id);

        string label = !string.IsNullOrEmpty(sig.locationName) ? sig.locationName : (!string.IsNullOrEmpty(sig.teaser) ? sig.teaser : "");
        Debug.Log($"[SignalBeamBridge] Created beam for {sig.role} {sig.id} tx={sig.transmissionType} ring={sig.poolRingIndex} ext={sig.externalKey} label='{label}'");
    }

    private void HandleRemoved(Signal sig)
    {
        if (beamsBySignalId.TryGetValue(sig.id, out var go))
        {
            if (go != null) Destroy(go);
            beamsBySignalId.Remove(sig.id);
        }
        if (labelsBySignalId.TryGetValue(sig.id, out var lbl))
        {
            if (lbl != null && lbl.go != null) Destroy(lbl.go);
            labelsBySignalId.Remove(sig.id);
        }
        sig.visualGO = null;
    }

    private void HandleStateChanged(Signal sig)
    {
        if (!beamsBySignalId.TryGetValue(sig.id, out var go)) return;
        if (go == null) return;

        // Show/hide based on state
        bool visible = sig.state != SignalState.Hidden && sig.state != SignalState.CoolingDown;
        go.SetActive(visible);
        if (labelsBySignalId.TryGetValue(sig.id, out var lbl) && lbl != null && lbl.go != null)
            lbl.go.SetActive(visible);
    }

    // ── Profile / appearance ──────────────────────────────────

    private void ApplyProfileSettings(BeamAvatar orb)
    {
        if (profile == null) return;

        orb.glowColor = profile.orbs.glowColor;
        orb.emissionIntensity = profile.orbs.emissionIntensity;
        orb.orbSize = profile.orbs.orbSize;
        orb.showBeam = profile.orbs.showBeam;
        orb.beamColor = profile.orbs.beamColor;
        orb.beamEmission = profile.orbs.beamEmission;
        orb.beamWidth = profile.orbs.beamWidth;
        orb.beamHeight = profile.orbs.beamHeight;
        orb.particleCount = profile.orbs.particleCount;
        orb.particleEmissionColor = profile.orbs.particleEmissionColor;
        orb.particleSpeed = profile.orbs.particleSpeed;
        orb.particleChaos = profile.orbs.particleChaos;
        orb.particleBaseSize = profile.orbs.particleBaseSize;

        if (particleBeamMaterial != null)
            orb.particleBeamMaterial = particleBeamMaterial;

        ForceLaserVisibility(orb);
    }

    private void ApplySignalAppearance(BeamAvatar orb, Signal sig)
    {
        // Role affects size/density (primary vs secondary vs distant).
        // TransmissionType affects color (location/artifact/transmitter).
        switch (sig.role)
        {
            case SignalRole.PrimaryPursuit:
                // Full size — no changes needed
                break;
            case SignalRole.SecondaryNearby:
                orb.orbSize *= 0.6f;
                orb.particleCount = Mathf.RoundToInt(orb.particleCount * 0.5f);
                break;
            case SignalRole.DistantBackground:
                orb.orbSize *= 0.4f;
                orb.particleCount = Mathf.RoundToInt(orb.particleCount * 0.3f);
                break;
            case SignalRole.LocationTransmission:
                // Slightly smaller than primary (color is set by TransmissionType below)
                orb.orbSize *= 0.8f;
                orb.particleCount = Mathf.RoundToInt(orb.particleCount * 0.7f);
                break;
        }

        // Visible portal colors: location vs ambient. Transmitter is legacy/user-initiated only.
        Color c;
        switch (sig.transmissionType)
        {
            case TransmissionType.Location:
                c = new Color(0.65f, 0.25f, 1.0f, 1.0f); // purple
                break;
            case TransmissionType.Artifact:
            case TransmissionType.Transmitter:
            default:
                c = new Color(0.25f, 0.6f, 1.0f, 1.0f); // ambient blue
                break;
        }

        orb.glowColor = c;
        orb.particleEmissionColor = c;
        orb.beamColor = c;
        ForceLaserVisibility(orb);
    }

    private void ForceLaserVisibility(BeamAvatar orb)
    {
        if (orb == null) return;
        orb.showBeam = true;
        orb.visualMode = BeamAvatar.BeamVisualMode.SpaceLaser;
        orb.beamHeight = Mathf.Max(orb.beamHeight, 1200f);
        orb.beamWidth = Mathf.Max(orb.beamWidth, 3.5f);
        orb.beamEmission = Mathf.Max(orb.beamEmission, 120f);
        orb.useMagicalParticles = true;
        orb.particleCount = Mathf.Max(orb.particleCount, 1200);
        orb.particleBaseSize = Mathf.Max(orb.particleBaseSize, 0.7f);
        orb.particleSpeed = Mathf.Max(orb.particleSpeed, 8f);
        orb.particleDensity = Mathf.Max(orb.particleDensity, 1.25f);
        orb.hideOrbMesh = false;
        orb.orbSize = Mathf.Max(orb.orbSize, 1.25f);
        orb.emissionIntensity = Mathf.Max(orb.emissionIntensity, 4f);
    }

    private void EnsureLabel(string signalId)
    {
        if (labelsBySignalId.ContainsKey(signalId)) return;
        RectTransform labelParent = K1L0CanvasRoot.World;
        if (uiCanvas == null) uiCanvas = K1L0CanvasRoot.WorldCanvas;

        var go = new GameObject($"BeamMeters_{signalId}", typeof(RectTransform));
        go.transform.SetParent(labelParent != null ? labelParent : transform, false);

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(92f, 18f);
        rt.pivot = new Vector2(0.5f, 0f);

        var textGO = new GameObject("Text", typeof(RectTransform));
        textGO.transform.SetParent(go.transform, false);
        var textRt = textGO.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = "";
        tmp.fontSize = 9f;
        tmp.color = new Color(1f, 1f, 1f, 0.82f);
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        tmp.fontStyle = FontStyles.Normal;
        tmp.outlineWidth = 0f;

        labelsBySignalId[signalId] = new BeamLabelEntry { go = go, rt = rt, tmp = tmp };
    }

    private void UpdateBeamDistanceLabels(IReadOnlyList<Signal> signals)
    {
        if (playerController == null) playerController = FindFirstObjectByType<KiloFirstPersonController>();
        bool godModeLabelsAllowed = playerController != null && playerController.IsGodView && !KiloFirstPersonController.IsNativePanelOpen;

        if (!ShowDistanceLabels || !godModeLabelsAllowed)
        {
            HideAllDistanceLabels();
            return;
        }
        if (signals == null) return;
        if (mainCamera == null || !mainCamera.isActiveAndEnabled) mainCamera = Camera.main;
        if (mainCamera == null) return;

        var candidates = new List<BeamLabelCandidate>();

        for (int i = 0; i < signals.Count; i++)
        {
            var sig = signals[i];
            if (sig == null) continue;

            if (!labelsBySignalId.TryGetValue(sig.id, out var lbl) || lbl == null || lbl.go == null)
                continue;

            bool visible = sig.state != SignalState.Hidden && sig.state != SignalState.CoolingDown;
            if (!visible)
            {
                if (lbl.go.activeSelf) lbl.go.SetActive(false);
                continue;
            }

            Vector3 baseWorld = director.SignalToWorldPos(sig);
            // Sit the label high above the beam base so it reads as attached
            // to the laser column rather than the ground orb.
            Vector3 worldPos = baseWorld + Vector3.up * 145f;
            Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);

            bool onScreen = screenPos.z > 0 &&
                            screenPos.x > -120f && screenPos.x < Screen.width + 120f &&
                            screenPos.y > -120f && screenPos.y < Screen.height + 120f;

            if (onScreen != lbl.go.activeSelf) lbl.go.SetActive(onScreen);
            if (!onScreen) continue;
            screenPos.x = Mathf.Clamp(screenPos.x, 16f, Screen.width - 16f);
            screenPos.y = Mathf.Clamp(screenPos.y, 16f, Screen.height - 58f);

            float distM = director.DistanceToSignal(sig);
            float w = lbl.rt != null ? lbl.rt.sizeDelta.x : 92f;
            float h = lbl.rt != null ? lbl.rt.sizeDelta.y : 18f;
            var rect = new Rect(screenPos.x - w * 0.5f, screenPos.y, w, h);
            candidates.Add(new BeamLabelCandidate
            {
                label = lbl,
                signal = sig,
                screenPos = screenPos,
                distanceMeters = distM,
                screenRect = rect
            });
        }

        candidates.Sort((a, b) => a.distanceMeters.CompareTo(b.distanceMeters));

        // Column-stacking overlap arbitration. Closest label keeps its
        // natural screen position. Each subsequent label, if it would
        // collide with one already placed, snaps its X to the colliding
        // label's center (joining that column) and moves Y just above the
        // tallest rect already occupying that X range. Up to STACK_TRIES
        // iterations to clear; if still colliding, place anyway (last
        // resort — better than a missing label).
        const float STACK_GAP = 4f;
        const int STACK_TRIES = 8;
        var occupied = new List<Rect>(candidates.Count);

        int visibleCount = 0;
        for (int i = 0; i < candidates.Count; i++)
        {
            var c = candidates[i];
            if (visibleCount >= 16)
            {
                if (c.label.go.activeSelf) c.label.go.SetActive(false);
                continue;
            }

            // Set text + width BEFORE collision testing so the rect we
            // measure is the rect we'll actually draw.
            if (c.label.tmp != null)
            {
                string txt = !string.IsNullOrEmpty(c.signal?.locationName)
                    ? c.signal.locationName
                    : (!string.IsNullOrEmpty(c.signal?.teaser) ? c.signal.teaser : $"{Mathf.RoundToInt(c.distanceMeters)}m");
                c.label.tmp.text = txt;
            }
            if (c.label.tmp != null && c.label.rt != null)
            {
                float preferred = c.label.tmp.GetPreferredValues().x + 4f;
                float w = Mathf.Clamp(preferred, 42f, 180f);
                c.label.rt.sizeDelta = new Vector2(w, c.label.rt.sizeDelta.y);
            }

            // Build the rect from the just-updated size (rather than the
            // stale c.screenRect, which was sampled with last frame's width).
            Vector2 size = c.label.rt != null
                ? c.label.rt.sizeDelta
                : new Vector2(c.screenRect.width, c.screenRect.height);
            // Pivot is (0.5, 0): rect.y is the BOTTOM in screen space.
            Rect rect = new Rect(c.screenPos.x - size.x * 0.5f, c.screenPos.y, size.x, size.y);

            // Iteratively resolve collisions by joining a column + stacking.
            for (int t = 0; t < STACK_TRIES; t++)
            {
                int hitIdx = -1;
                for (int j = 0; j < occupied.Count; j++)
                {
                    if (rect.Overlaps(occupied[j])) { hitIdx = j; break; }
                }
                if (hitIdx < 0) break;

                // Adopt the offender's X column (anchored on the closer
                // label that's already placed — closer wins X).
                float anchorCx = occupied[hitIdx].center.x;
                rect = new Rect(anchorCx - rect.width * 0.5f, rect.y, rect.width, rect.height);

                // Stack above whichever already-placed rect's X range
                // overlaps ours and has the highest top edge.
                float topY = rect.y;
                for (int k = 0; k < occupied.Count; k++)
                {
                    var o = occupied[k];
                    if (o.xMax > rect.xMin && o.xMin < rect.xMax && o.yMax > topY)
                        topY = o.yMax;
                }
                rect = new Rect(rect.x, topY + STACK_GAP, rect.width, rect.height);
            }

            if (!c.label.go.activeSelf) c.label.go.SetActive(true);
            occupied.Add(rect);
            visibleCount++;

            // Final placement — pivot (0.5, 0) so the GO sits at
            // (centerX, bottomY) of the resolved rect.
            if (c.label.rt != null)
            {
                Vector2 finalScreen = new Vector2(rect.center.x, rect.yMin);
                var parent = c.label.rt.parent as RectTransform;
                if (parent != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, finalScreen, null, out var localPoint))
                    c.label.rt.anchoredPosition = localPoint;
                else
                    c.label.rt.position = new Vector3(finalScreen.x, finalScreen.y, 0f);
            }
        }

        if (Time.unscaledTime >= nextLabelDebugAt)
        {
            nextLabelDebugAt = Time.unscaledTime + 5f;
            Debug.Log($"[SignalBeamBridge] labels visible={visibleCount} candidates={candidates.Count} signals={signals.Count} pref={ShowDistanceLabels}");
        }
    }

    private void HideAllDistanceLabels()
    {
        foreach (var entry in labelsBySignalId.Values)
        {
            if (entry != null && entry.go != null && entry.go.activeSelf)
                entry.go.SetActive(false);
        }
    }
}
