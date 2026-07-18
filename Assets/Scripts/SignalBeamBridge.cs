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
    public static bool InitialPopulationReady
    {
        get
        {
            if (Instance == null || Instance.director == null) return false;
            return Instance.beamsBySignalId.Count >= Instance.director.ActiveSignals.Count;
        }
    }
    private const string ShowDistanceLabelsPref = "k1lo_showBeamDistanceLabels";
    private const float BeamMinVisualHeight = 20f;
    private const float BeamMaxVisualHeight = 150f;
    private const float BeamTargetPixelsAboveHorizon = 150f;
    private const float BeamTopSafeMarginPixels = 130f;
    private const float BeamMinScreenHeightPixels = 90f;
    private static bool hudSuppressed;
    public static bool ShowDistanceLabels => PlayerPrefs.GetInt(ShowDistanceLabelsPref, 1) != 0;
    public static int ActiveSignalCount => Instance != null && Instance.director != null
        ? Instance.director.ActiveSignals.Count : -1;
    public static int SpawnedBeamCount => Instance != null ? Instance.beamsBySignalId.Count : -1;
    public static int VisibleBeamCount
    {
        get
        {
            if (Instance == null) return -1;
            int count = 0;
            foreach (var beam in Instance.beamsBySignalId.Values)
                if (beam != null && beam.activeInHierarchy) count++;
            return count;
        }
    }

    public static void RestoreMapRuntime()
    {
        if (Instance == null) return;
        Instance.enabled = true;
        if (Instance.container != null)
            Instance.container.gameObject.SetActive(true);
        if (Instance.director == null)
            Instance.director = SignalDirectorV2.Instance;
        if (Instance.director == null) return;

        var signals = Instance.director.ActiveSignals;
        for (int i = 0; i < signals.Count; i++)
        {
            Instance.HandleSpawned(signals[i]);
            Instance.HandleStateChanged(signals[i]);
        }
    }

    public static void SetDistanceLabelsVisible(bool visible)
    {
        PlayerPrefs.SetInt(ShowDistanceLabelsPref, visible ? 1 : 0);
        PlayerPrefs.Save();
        if (!visible && Instance != null) Instance.HideAllDistanceLabels();
    }

    public static void SetHudSuppressed(bool suppressed)
    {
        hudSuppressed = suppressed;
        // Suppression can happen immediately before this component is disabled
        // for sky mode, so do not wait for another Update to hide stale labels.
        if (suppressed && Instance != null)
            Instance.HideAllDistanceLabels();
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
    private bool visualReady;

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
        public bool anchoredToItem;
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
        visualReady = true;
        TrySubscribeToDirector();
    }

    void OnEnable()
    {
        if (visualReady) TrySubscribeToDirector();
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
            TrySubscribeToDirector();
            if (director == null) return;
        }

        // Reposition all beams every frame (floating origin)
        var signals = director.ActiveSignals;
        for (int i = 0; i < signals.Count; i++)
        {
            var sig = signals[i];
            if (!beamsBySignalId.ContainsKey(sig.id))
                HandleSpawned(sig);
            if (!beamsBySignalId.TryGetValue(sig.id, out var go)) continue;
            if (go == null) continue;

            Vector3 worldPos = director.SignalToWorldPos(sig);
            var orb = go.GetComponent<BeamAvatar>();
            float visualBeamHeight = 18f;
            if (orb != null)
            {
                orb.SetPosition(worldPos);
                if (mainCamera == null || !mainCamera.isActiveAndEnabled) mainCamera = Camera.main;
                if (mainCamera != null)
                {
                    visualBeamHeight = ComputeScreenClampedBeamHeight(mainCamera, worldPos);
                    orb.SetVisualBeamHeight(visualBeamHeight);
                }
            }
            else
                go.transform.position = worldPos;

            // The distance label and item quad share one authoritative beam-top
            // anchor. Do not let the billboard estimate the endpoint separately.
            var hologram = go.GetComponent<BeamItemHologram>();
            if (hologram != null)
                hologram.SetWorldAnchor(worldPos + Vector3.up * (visualBeamHeight + 8f));
        }

        UpdateBeamDistanceLabels(signals);
    }

    private void TrySubscribeToDirector()
    {
        if (!visualReady) return;
        if (director == null)
            director = SignalDirectorV2.Instance;
        if (director == null) return;

        // OnDisable removes these handlers while preserving the director
        // reference. Always remove-before-add so re-enabling cannot either miss
        // removal events or accumulate duplicate subscriptions.
        director.OnSignalSpawned -= HandleSpawned;
        director.OnSignalRemoved -= HandleRemoved;
        director.OnSignalStateChanged -= HandleStateChanged;
        director.OnSignalSpawned += HandleSpawned;
        director.OnSignalRemoved += HandleRemoved;
        director.OnSignalStateChanged += HandleStateChanged;

        var existing = director.ActiveSignals;
        var activeIds = new HashSet<string>();
        for (int i = 0; i < existing.Count; i++)
        {
            if (existing[i] == null) continue;
            activeIds.Add(existing[i].id);
            HandleSpawned(existing[i]);
            HandleStateChanged(existing[i]);
        }

        // A collection can complete while this bridge is disabled. Reconcile
        // the visual dictionaries immediately so those missed removals do not
        // leave uncollectable beams or labels dangling in the world.
        var staleIds = new List<string>();
        foreach (var id in beamsBySignalId.Keys)
            if (!activeIds.Contains(id)) staleIds.Add(id);
        for (int i = 0; i < staleIds.Count; i++)
            RemoveVisual(staleIds[i]);

        Debug.Log($"[SignalBeamBridge] Subscribed/backfilled active={existing.Count} removedStale={staleIds.Count}");
    }

    private void RemoveVisual(string signalId)
    {
        if (beamsBySignalId.TryGetValue(signalId, out var go))
        {
            if (go != null) Destroy(go);
            beamsBySignalId.Remove(signalId);
        }
        if (labelsBySignalId.TryGetValue(signalId, out var lbl))
        {
            if (lbl != null && lbl.go != null) Destroy(lbl.go);
            labelsBySignalId.Remove(signalId);
        }
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

        // Diagnostic item-top visual is attached to every beam, even before
        // backend artwork arrives, so placement can be tested independently.
        RefreshHologram(sig);

        EnsureLabel(sig.id);

        // Location signals are commonly born directly in Visible state. They
        // do not emit a subsequent state-change event, so leaving the freshly
        // instantiated beam inactive here made every location beam and item
        // disappear despite valid signal data.
        HandleStateChanged(sig);

        string label = !string.IsNullOrEmpty(sig.locationName) ? sig.locationName : (!string.IsNullOrEmpty(sig.teaser) ? sig.teaser : "");
        Debug.Log($"[SignalBeamBridge] Created beam for {sig.role} {sig.id} tx={sig.transmissionType} ring={sig.poolRingIndex} ext={sig.externalKey} label='{label}'");
    }

    private void HandleRemoved(Signal sig)
    {
        RemoveVisual(sig.id);
        sig.visualGO = null;
    }

    public void RefreshHologram(Signal sig)
    {
        if (sig == null || !beamsBySignalId.TryGetValue(sig.id, out var go) || go == null) return;
        var hologram = go.GetComponent<BeamItemHologram>();
        if (hologram == null) hologram = go.AddComponent<BeamItemHologram>();

        int budget = sig.poolRingIndex == 1 ? 2500
            : (sig.poolRingIndex == 2 || sig.poolRingIndex == 3 ? 750 : 0);
        if (sig.hologramParticleBudget > 0) budget = sig.hologramParticleBudget;
        var orb = go.GetComponent<BeamAvatar>();
        bool useElementsFallback = sig.transmissionType == TransmissionType.Artifact &&
            string.IsNullOrWhiteSpace(sig.hologramImageUrl);
        hologram.Configure(sig.hologramImageUrl, sig.hologramDepthUrl, useElementsFallback, budget,
            orb != null ? orb.particleBeamMaterial : particleBeamMaterial);
    }

    private void HandleStateChanged(Signal sig)
    {
        if (!beamsBySignalId.TryGetValue(sig.id, out var go)) return;
        if (go == null) return;

        // Show/hide based on state
        bool visible = sig.state != SignalState.Hidden && sig.state != SignalState.CoolingDown;
        go.SetActive(visible);
        if (visible)
        {
            var hologram = go.GetComponent<BeamItemHologram>();
            hologram?.RetryPendingArtwork();
            RefreshHologram(sig);
        }
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

    private float ComputeScreenClampedBeamHeight(Camera cam, Vector3 baseWorld)
    {
        if (cam == null) return BeamMaxVisualHeight;

        Vector3 baseScreen = cam.WorldToScreenPoint(baseWorld);
        if (baseScreen.z <= 0f) return BeamMaxVisualHeight;

        float horizonY = EstimateHorizonScreenY(cam, baseWorld.y);
        float targetTopY = horizonY + BeamTargetPixelsAboveHorizon;
        targetTopY = Mathf.Clamp(
            targetTopY,
            baseScreen.y + BeamMinScreenHeightPixels,
            Screen.height - BeamTopSafeMarginPixels);

        Vector3 fullTopScreen = cam.WorldToScreenPoint(baseWorld + Vector3.up * BeamMaxVisualHeight);
        if (fullTopScreen.z <= 0f || fullTopScreen.y <= targetTopY)
            return BeamMaxVisualHeight;

        float low = BeamMinVisualHeight;
        float high = BeamMaxVisualHeight;
        for (int i = 0; i < 8; i++)
        {
            float mid = (low + high) * 0.5f;
            Vector3 midScreen = cam.WorldToScreenPoint(baseWorld + Vector3.up * mid);
            if (midScreen.z > 0f && midScreen.y > targetTopY)
                high = mid;
            else
                low = mid;
        }
        return Mathf.Clamp(low, BeamMinVisualHeight, BeamMaxVisualHeight);
    }

    private float EstimateHorizonScreenY(Camera cam, float groundY)
    {
        Vector3 flatForward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
        if (flatForward.sqrMagnitude < 0.0001f)
            return Screen.height * 0.5f;

        Vector3 probe = cam.transform.position + flatForward.normalized * 2000f;
        probe.y = groundY;
        Vector3 screen = cam.WorldToScreenPoint(probe);
        if (screen.z <= 0f || float.IsNaN(screen.y) || float.IsInfinity(screen.y))
            return Screen.height * 0.5f;

        return Mathf.Clamp(screen.y, Screen.height * 0.2f, Screen.height * 0.8f);
    }

    private void EnsureLabel(string signalId)
    {
        if (labelsBySignalId.ContainsKey(signalId)) return;
        // Keep labels on an always-active screen-space canvas so volumetric
        // fog, smoke and post effects cannot occlude them. The iOS app disables
        // HUDCanvas because Swift owns the main HUD, so labels must not live there.
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
        // Bumped from 9 → 12 and switched to a bold outlined style. The old
        // 9-pt near-white text was hard to read against the world map; adding
        // a small dark outline pulls the label off any background.
        tmp.fontSize = 12f;
        var readableFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (readableFont != null) tmp.font = readableFont;
        tmp.color = new Color(1f, 1f, 1f, 1.0f);
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false;
        tmp.fontStyle = FontStyles.Bold;
        tmp.outlineWidth = 0.18f;
        tmp.outlineColor = new Color32(0, 0, 0, 220);

        labelsBySignalId[signalId] = new BeamLabelEntry { go = go, rt = rt, tmp = tmp };
    }

    private void UpdateBeamDistanceLabels(IReadOnlyList<Signal> signals)
    {
        if (playerController == null) playerController = FindFirstObjectByType<KiloFirstPersonController>();
        bool mapLabelsAllowed = playerController != null && !KiloFirstPersonController.IsNativePanelOpen;

        if (hudSuppressed || !ShowDistanceLabels || !mapLabelsAllowed)
        {
            HideAllDistanceLabels();
            return;
        }
        if (signals == null) return;
        if (mainCamera == null || !mainCamera.isActiveAndEnabled) mainCamera = Camera.main;
        if (mainCamera == null) return;

        // Sweep pass — deactivate any label whose owning signal is no longer
        // in the active signals[] list. Without this, a label whose signal
        // gets removed silently (server refresh replacing the list, state
        // transitioning to something we didn't enumerate, etc.) would stay
        // stuck at its last screen position because the update loop never
        // touches it again.
        if (labelsBySignalId.Count > 0)
        {
            var activeIds = new HashSet<string>(signals.Count);
            for (int i = 0; i < signals.Count; i++)
            {
                if (signals[i] != null && !string.IsNullOrEmpty(signals[i].id))
                    activeIds.Add(signals[i].id);
            }
            foreach (var kv in labelsBySignalId)
            {
                if (!activeIds.Contains(kv.Key) && kv.Value?.go != null && kv.Value.go.activeSelf)
                    kv.Value.go.SetActive(false);
            }
        }

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
            float labelHeight = ComputeScreenClampedBeamHeight(mainCamera, baseWorld);
            Vector3 worldPos = baseWorld + Vector3.up * (labelHeight + 8f);
            Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);
            bool anchoredToItem = false;

            // Item placement is already perspective-correct and clamped for
            // readability. Use its stable SCREEN rect as the authoritative
            // title anchor so the title remains snug regardless of camera
            // pitch, beam distance, or the item's world-space scale. The
            // stable rect follows the non-bobbing anchor, so titles do not
            // inherit the item's float animation.
            if (beamsBySignalId.TryGetValue(sig.id, out var beamGo) && beamGo != null)
            {
                var ownItem = beamGo.GetComponent<BeamItemHologram>();
                if (ownItem != null && ownItem.TryGetStableTopScreenRect(mainCamera, out var ownItemRect))
                {
                    screenPos = new Vector3(ownItemRect.center.x, ownItemRect.yMax + 6f, 1f);
                    anchoredToItem = true;
                }
            }

            // Reject NaN/Infinity too — a degenerate WorldToScreenPoint result
            // used to slip through and leave the label frozen at its last valid
            // anchoredPosition, which read as "stuck" on screen.
            bool onScreen = screenPos.z > 0 &&
                            !float.IsNaN(screenPos.x) && !float.IsInfinity(screenPos.x) &&
                            !float.IsNaN(screenPos.y) && !float.IsInfinity(screenPos.y) &&
                            screenPos.x > -120f && screenPos.x < Screen.width + 120f &&
                            screenPos.y > -120f && screenPos.y < Screen.height + 120f;

            if (onScreen != lbl.go.activeSelf) lbl.go.SetActive(onScreen);
            if (!onScreen) continue;

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
                screenRect = rect,
                anchoredToItem = anchoredToItem
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
        var occupied = new List<Rect>(candidates.Count * 2);
        // Item thumbnails are first-class occupied screen regions. This keeps
        // stacked labels above thumbnails belonging to any visible beam.
        foreach (var pair in beamsBySignalId)
        {
            if (pair.Value == null || !pair.Value.activeInHierarchy) continue;
            var item = pair.Value.GetComponent<BeamItemHologram>();
            // Reserve the item's fixed upper anchor, not its animated current
            // position. Otherwise each bob cycle pushes stacked titles up and
            // down even though their own beam anchor is stationary.
            if (item != null && item.TryGetStableTopScreenRect(mainCamera, out var itemRect))
                occupied.Add(itemRect);
        }

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
            // Location beams identify the place. Non-location beams stay
            // mysterious until collected/tuned; keep their real label in data.
            string labelText = c.signal != null && c.signal.transmissionType == TransmissionType.Location
                ? (string.IsNullOrEmpty(c.signal.locationName) ? null : c.signal.locationName)
                : null;
            labelText = TruncatePlaceLabel(labelText);
            if (labelText == null)
            {
                if (c.label.go.activeSelf) c.label.go.SetActive(false);
                continue;
            }
            if (c.label.tmp != null)
            {
                c.label.tmp.text = labelText;
            }
            if (c.label.tmp != null && c.label.rt != null)
            {
                const float maxLabelWidth = 112f;
                Vector2 preferred = c.label.tmp.GetPreferredValues(labelText, maxLabelWidth - 4f, 40f);
                float w = Mathf.Clamp(preferred.x + 4f, 42f, maxLabelWidth);
                float h = Mathf.Clamp(preferred.y + 2f, 18f, 34f);
                c.label.rt.sizeDelta = new Vector2(w, h);
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

    private static string TruncatePlaceLabel(string label)
    {
        if (string.IsNullOrWhiteSpace(label)) return label;
        const int maxCharacters = 30;
        string normalized = string.Join(" ", label.Trim().Split(
            new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries));
        if (normalized.Length <= maxCharacters) return normalized;

        // Keep the complete words that fit before the ellipsis. If the first
        // word itself is too long, fall back to a hard character boundary.
        const int contentBudget = maxCharacters - 3;
        int boundary = normalized.LastIndexOf(' ', contentBudget);
        if (boundary <= 0) boundary = contentBudget;
        return normalized.Substring(0, boundary).TrimEnd() + "...";
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
