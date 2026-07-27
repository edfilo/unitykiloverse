using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;

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
    private const float BeamMinVisualHeight = 8f;
    private const float BeamMaxVisualHeight = 150f;
    // The item quad is about 14% of viewport height. Keeping its low bob
    // anchor one half-thumbnail above the horizon is the lowest placement
    // that leaves the complete image in the sky. BeamItemGlitch uses
    // ZTest Always, so buildings cannot depth-occlude it at this height.
    private const float BeamTargetPixelsAboveHorizon = 64f;
    private const float BeamTopSafeMarginPixels = 80f;
    private const float BeamMinScreenHeightPixels = 64f;
    private static bool ambientSpotlightEnabled = true;
    private static float ambientSpotlightIntensity = 7f;
    private static float ambientSpotlightRange = 65f;
    private static float ambientSpotlightAngle = 17f;
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

    public static bool FocusCameraOnSignal(string signalId, float holdSeconds = 4f)
    {
        if (Instance == null || string.IsNullOrWhiteSpace(signalId) ||
            !Instance.beamsBySignalId.TryGetValue(signalId, out var beam) || beam == null)
            return false;

        Vector3 target = beam.transform.position;
        var item = beam.GetComponent<BeamItemHologram>();
        if (item != null) item.TryGetCurrentWorldPosition(out target);
        var controller = FindFirstObjectByType<KiloFirstPersonController>();
        if (controller == null) return false;
        controller.TemporarilyFaceWorldPoint(target, holdSeconds);
        return true;
    }

    public static void SetAmbientSpotlightEnabled(bool enabled)
    {
        ambientSpotlightEnabled = enabled;
        if (!enabled && Instance?.ambientItemSpotlight != null)
            Instance.ambientItemSpotlight.enabled = false;
    }

    public static void SetAmbientSpotlightIntensity(float value)
    {
        ambientSpotlightIntensity = Mathf.Clamp(value, 0f, 15f);
    }

    public static void SetAmbientSpotlightRange(float value)
    {
        ambientSpotlightRange = Mathf.Clamp(value, 20f, 120f);
    }

    public static void SetAmbientSpotlightAngle(float value)
    {
        ambientSpotlightAngle = Mathf.Clamp(value, 5f, 35f);
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
    private string expandedLabelAnchorId;
    private float expandedLabelUntil;
    private const float LabelExpansionDuration = 8f;
    private Light ambientItemSpotlight;
    private string ambientSpotlightSignalId;
    private float nextAmbientSpotlightSelectionAt;
    private bool visualReady;
    private bool itemTapTracking;
    private int itemTapFingerId = -1;
    private Vector2 itemTapStart;
    private float itemTapStartedAt;
    private const float ItemTapMaxDuration = .55f;

    private class BeamLabelEntry
    {
        public GameObject go;
        public RectTransform rt;
        public TextMeshProUGUI tmp;
        public string signalId;
        public int groupMemberCount = 1;
    }

    private class BeamLabelCandidate
    {
        public BeamLabelEntry label;
        public Signal signal;          // needed for locationName lookup in update pass
        public Vector3 screenPos;
        public float distanceMeters;
        public Rect screenRect;
        public string baseText;
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
                hologram.SetWorldAnchor(worldPos + Vector3.up * (visualBeamHeight + BeamTopAnchorOffset(mainCamera, worldPos)));
        }

        UpdateFloatingItemTap(signals);
        UpdateBeamDistanceLabels(signals);
        UpdateAmbientItemSpotlight(signals);
    }

    private void UpdateFloatingItemTap(IReadOnlyList<Signal> signals)
    {
        if (mainCamera == null || !mainCamera.isActiveAndEnabled) mainCamera = Camera.main;
        if (mainCamera == null || signals == null || KiloFirstPersonController.IsNativePanelOpen)
        {
            itemTapTracking = false;
            itemTapFingerId = -1;
            return;
        }

#if UNITY_IOS || UNITY_ANDROID
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.phase == TouchPhase.Began && !itemTapTracking)
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.fingerId))
                    continue;
                itemTapTracking = true;
                itemTapFingerId = touch.fingerId;
                itemTapStart = touch.position;
                itemTapStartedAt = Time.unscaledTime;
                continue;
            }
            if (!itemTapTracking || touch.fingerId != itemTapFingerId) continue;
            if (touch.phase == TouchPhase.Canceled)
            {
                itemTapTracking = false;
                itemTapFingerId = -1;
            }
            else if (touch.phase == TouchPhase.Ended)
            {
                float maxTravel = Mathf.Clamp(Screen.dpi > 0f ? Screen.dpi * .075f : 28f, 22f, 42f);
                bool isTap = Time.unscaledTime - itemTapStartedAt <= ItemTapMaxDuration &&
                             Vector2.Distance(itemTapStart, touch.position) <= maxTravel;
                itemTapTracking = false;
                itemTapFingerId = -1;
                if (isTap) TryOpenFloatingItemAt(touch.position, signals);
            }
        }
#else
        if (Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())
            {
                itemTapTracking = true;
                itemTapStart = Input.mousePosition;
                itemTapStartedAt = Time.unscaledTime;
            }
        }
        if (itemTapTracking && Input.GetMouseButtonUp(0))
        {
            Vector2 end = Input.mousePosition;
            bool isTap = Time.unscaledTime - itemTapStartedAt <= ItemTapMaxDuration &&
                         Vector2.Distance(itemTapStart, end) <= 10f;
            itemTapTracking = false;
            if (isTap) TryOpenFloatingItemAt(end, signals);
        }
#endif
    }

    private void TryOpenFloatingItemAt(Vector2 screenPoint, IReadOnlyList<Signal> signals)
    {
        Signal best = null;
        float bestScreenDistance = float.MaxValue;
        float hitPadding = Mathf.Clamp(Screen.height * .014f, 12f, 24f);

        for (int i = 0; i < signals.Count; i++)
        {
            Signal signal = signals[i];
            if (signal == null || signal.state == SignalState.Hidden || signal.state == SignalState.CoolingDown ||
                !beamsBySignalId.TryGetValue(signal.id, out var beam) || beam == null || !beam.activeInHierarchy)
                continue;

            var item = beam.GetComponent<BeamItemHologram>();
            if (item == null || !item.TryGetScreenRect(mainCamera, out Rect rect)) continue;
            rect.xMin -= hitPadding;
            rect.xMax += hitPadding;
            rect.yMin -= hitPadding;
            rect.yMax += hitPadding;
            if (!rect.Contains(screenPoint)) continue;

            float screenDistance = ((Vector2)rect.center - screenPoint).sqrMagnitude;
            if (screenDistance >= bestScreenDistance) continue;
            bestScreenDistance = screenDistance;
            best = signal;
        }

        if (best == null) return;
        float distance = director != null ? director.DistanceToSignal(best) : 0f;
        K1L0HUD.DeliverNativeFloatingItemTap(best, distance);
        Debug.Log($"[SignalBeamBridge] Item tap hit signal={best.id} type={best.transmissionType} distance={distance:F1}m");
    }

    private void UpdateAmbientItemSpotlight(IReadOnlyList<Signal> signals)
    {
        if (!ambientSpotlightEnabled)
        {
            if (ambientItemSpotlight != null) ambientItemSpotlight.enabled = false;
            return;
        }
        if (signals == null || mainCamera == null) return;

        if (Time.unscaledTime >= nextAmbientSpotlightSelectionAt)
        {
            nextAmbientSpotlightSelectionAt = Time.unscaledTime + .5f;
            ambientSpotlightSignalId = null;
            float nearestSqr = 300f * 300f;
            for (int i = 0; i < signals.Count; i++)
            {
                var sig = signals[i];
                if (sig == null || sig.transmissionType != TransmissionType.Artifact ||
                    sig.state == SignalState.Hidden || sig.state == SignalState.CoolingDown ||
                    !beamsBySignalId.TryGetValue(sig.id, out var beam) || beam == null ||
                    !beam.activeInHierarchy) continue;

                Vector3 basePosition = director.SignalToWorldPos(sig);
                Vector3 viewport = mainCamera.WorldToViewportPoint(basePosition + Vector3.up * 12f);
                if (viewport.z <= 0f || viewport.x < -.12f || viewport.x > 1.12f) continue;
                float sqr = (basePosition - mainCamera.transform.position).sqrMagnitude;
                if (sqr >= nearestSqr) continue;
                nearestSqr = sqr;
                ambientSpotlightSignalId = sig.id;
            }
        }

        if (string.IsNullOrEmpty(ambientSpotlightSignalId))
        {
            if (ambientItemSpotlight != null) ambientItemSpotlight.enabled = false;
            return;
        }

        Signal selected = null;
        for (int i = 0; i < signals.Count; i++)
        {
            if (signals[i] != null && signals[i].id == ambientSpotlightSignalId)
            {
                selected = signals[i];
                break;
            }
        }
        if (selected == null) return;

        if (ambientItemSpotlight == null)
        {
            var lightGO = new GameObject("AmbientItemUplinkSpotlight");
            lightGO.transform.SetParent(container, false);
            ambientItemSpotlight = lightGO.AddComponent<Light>();
            ambientItemSpotlight.type = LightType.Spot;
            ambientItemSpotlight.color = new Color(.30f, .48f, 1f);
            ambientItemSpotlight.innerSpotAngle = 7f;
            ambientItemSpotlight.shadows = LightShadows.None;
            ambientItemSpotlight.renderMode = LightRenderMode.Auto;
            ambientItemSpotlight.bounceIntensity = 0f;
        }

        ambientItemSpotlight.transform.position = director.SignalToWorldPos(selected) + Vector3.up * .35f;
        ambientItemSpotlight.transform.rotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
        ambientItemSpotlight.intensity = ambientSpotlightIntensity;
        ambientItemSpotlight.range = ambientSpotlightRange;
        ambientItemSpotlight.spotAngle = ambientSpotlightAngle;
        ambientItemSpotlight.innerSpotAngle = Mathf.Min(ambientSpotlightAngle * .42f, ambientSpotlightAngle - 1f);
        ambientItemSpotlight.enabled = true;
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
        // TEMP: hide the vertical connector system while retaining the beam
        // GameObject as the stable location/item/label anchor.
        orb.showBeam = false;
        orb.useMagicalParticles = false;

        // Connector presentation intentionally parked for quick restoration:
        // orb.showBeam = true;
        // orb.visualMode = BeamAvatar.BeamVisualMode.SpaceLaser;
        // orb.beamHeight = Mathf.Max(orb.beamHeight, 1200f);
        // orb.beamWidth = Mathf.Max(orb.beamWidth, 3.5f);
        // orb.beamEmission = Mathf.Max(orb.beamEmission, 120f);
        // orb.useMagicalParticles = true;
        // orb.particleCount = Mathf.Max(orb.particleCount, 1200);
        // orb.particleBaseSize = Mathf.Max(orb.particleBaseSize, 0.7f);
        // orb.particleSpeed = Mathf.Max(orb.particleSpeed, 8f);
        // orb.particleDensity = Mathf.Max(orb.particleDensity, 1.25f);
        // orb.hideOrbMesh = false;
        // orb.orbSize = Mathf.Max(orb.orbSize, 1.25f);
        // orb.emissionIntensity = Mathf.Max(orb.emissionIntensity, 4f);
    }

    // Nearby anchors hug the ground in front of the building; far anchors keep
    // the horizon float. Blend runs from ~200 steps out to ~half a mile.
    private const float BeamNearDistanceMeters = 150f;
    private const float BeamFarDistanceMeters = 800f;
    private const float BeamNearGroundHeight = 1.6f;

    // 0 at <= near distance (ground-hugging), 1 at >= far distance (sky float).
    private float BeamNearFarBlend(Camera cam, Vector3 baseWorld)
    {
        if (cam == null) return 1f;
        Vector3 flat = baseWorld - cam.transform.position;
        flat.y = 0f;
        return Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(BeamNearDistanceMeters, BeamFarDistanceMeters, flat.magnitude));
    }

    // Vertical gap between the beam top and the label/item anchor shrinks as
    // the anchor drops toward the ground so near items sit just off the street.
    private float BeamTopAnchorOffset(Camera cam, Vector3 baseWorld)
    {
        return Mathf.Lerp(0.6f, 8f, BeamNearFarBlend(cam, baseWorld));
    }

    private float ComputeScreenClampedBeamHeight(Camera cam, Vector3 baseWorld)
    {
        if (cam == null) return BeamMaxVisualHeight;

        float blend = BeamNearFarBlend(cam, baseWorld);

        Vector3 baseScreen = cam.WorldToScreenPoint(baseWorld);
        if (baseScreen.z <= 0f) return Mathf.Lerp(BeamNearGroundHeight, BeamMaxVisualHeight, blend);

        float horizonY = EstimateHorizonScreenY(cam, baseWorld.y);
        float targetTopY = horizonY + BeamTargetPixelsAboveHorizon;
        targetTopY = Mathf.Clamp(
            targetTopY,
            baseScreen.y + BeamMinScreenHeightPixels,
            Screen.height - BeamTopSafeMarginPixels);

        Vector3 fullTopScreen = cam.WorldToScreenPoint(baseWorld + Vector3.up * BeamMaxVisualHeight);
        if (fullTopScreen.z <= 0f || fullTopScreen.y <= targetTopY)
            return Mathf.Lerp(BeamNearGroundHeight, BeamMaxVisualHeight, blend);

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
        float skyHeight = Mathf.Clamp(low, BeamMinVisualHeight, BeamMaxVisualHeight);
        return Mathf.Lerp(BeamNearGroundHeight, skyHeight, blend);
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
        tmp.raycastTarget = true;
        tmp.fontStyle = FontStyles.Bold;
        tmp.outlineWidth = 0.18f;
        tmp.outlineColor = new Color32(0, 0, 0, 220);

        var entry = new BeamLabelEntry { go = go, rt = rt, tmp = tmp, signalId = signalId };
        var button = go.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = tmp;
        button.onClick.AddListener(() => ExpandLabelGroup(entry));
        labelsBySignalId[signalId] = entry;
    }

    private void ExpandLabelGroup(BeamLabelEntry entry)
    {
        if (entry == null || entry.groupMemberCount <= 1 || string.IsNullOrEmpty(entry.signalId))
            return;

        expandedLabelAnchorId = entry.signalId;
        expandedLabelUntil = Time.unscaledTime + LabelExpansionDuration;
        Debug.Log($"[SignalBeamBridge] Expanded {entry.groupMemberCount} place labels for {LabelExpansionDuration:0}s");
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
            Vector3 worldPos = baseWorld + Vector3.up * (labelHeight + BeamTopAnchorOffset(mainCamera, baseWorld));
            Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);

            // Labels use a stable authored ceiling: max item Y plus half the
            // fully rendered item height. They therefore remain stationary while the
            // item bobs underneath them.
            if (beamsBySignalId.TryGetValue(sig.id, out var beamGo) && beamGo != null)
            {
                var ownItem = beamGo.GetComponent<BeamItemHologram>();
                if (ownItem != null)
                    ownItem.TryGetStableLabelScreenPoint(mainCamera, out screenPos);
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

            string labelText = sig.transmissionType == TransmissionType.Location
                ? (string.IsNullOrEmpty(sig.locationName) ? null : TruncatePlaceLabel(sig.locationName))
                : null;
            if (labelText == null)
            {
                if (lbl.go.activeSelf) lbl.go.SetActive(false);
                continue;
            }
            if (lbl.tmp != null) lbl.tmp.text = labelText;
            Vector2 labelSize = SizeLabel(lbl, labelText, 112f);
            var rect = new Rect(screenPos.x - labelSize.x * .5f, screenPos.y + 4f,
                labelSize.x, labelSize.y);
            candidates.Add(new BeamLabelCandidate
            {
                label = lbl,
                signal = sig,
                screenPos = screenPos,
                distanceMeters = director.DistanceToSignal(sig),
                screenRect = rect,
                baseText = labelText
            });
        }

        // Resolve collision components in screen space. Each connected group
        // keeps only its nearest place and summarizes the hidden labels instead
        // of building a tall stack into the sky.
        candidates.Sort((a, b) => a.distanceMeters.CompareTo(b.distanceMeters));
        int[] parent = new int[candidates.Count];
        for (int i = 0; i < parent.Length; i++) parent[i] = i;
        for (int i = 0; i < candidates.Count; i++)
        for (int j = i + 1; j < candidates.Count; j++)
        {
            Rect a = ExpandRect(candidates[i].screenRect, 5f);
            Rect b = ExpandRect(candidates[j].screenRect, 5f);
            if (a.Overlaps(b)) Union(parent, i, j);
        }
        var groupMembers = new Dictionary<int, List<int>>();
        for (int i = 0; i < candidates.Count; i++)
        {
            int root = Find(parent, i);
            if (!groupMembers.TryGetValue(root, out var members))
                groupMembers[root] = members = new List<int>();
            members.Add(i);
        }

        int visibleCount = 0;
        foreach (var members in groupMembers.Values)
        {
            // candidates are distance-sorted, so the first member is closest.
            var c = candidates[members[0]];
            // Keep the anchor active between frames so a pointer-down is not
            // cancelled before pointer-up. Only the summarized members hide.
            for (int i = 1; i < members.Count; i++)
            {
                var hiddenLabel = candidates[members[i]].label;
                if (hiddenLabel.go.activeSelf) hiddenLabel.go.SetActive(false);
            }
            int hiddenCount = members.Count - 1;
            c.label.groupMemberCount = members.Count;
            bool expanded = hiddenCount > 0 &&
                            c.signal.id == expandedLabelAnchorId &&
                            Time.unscaledTime < expandedLabelUntil;

            string labelText;
            float maxWidth;
            float maxHeight;
            if (expanded)
            {
                var expandedLines = new List<string>(members.Count);
                for (int i = 0; i < members.Count; i++)
                    expandedLines.Add(candidates[members[i]].baseText);
                labelText = string.Join("\n", expandedLines);
                maxWidth = 170f;
                maxHeight = Mathf.Clamp(members.Count * 18f + 4f, 38f, 180f);
            }
            else
            {
                labelText = hiddenCount > 0
                    ? $"{c.baseText}\n<size=78%>+ {hiddenCount} {(hiddenCount == 1 ? "place" : "places")}</size>"
                    : c.baseText;
                maxWidth = hiddenCount > 0 ? 150f : 112f;
                maxHeight = hiddenCount > 0 ? 44f : 34f;
            }
            if (c.label.tmp != null) c.label.tmp.text = labelText;
            Vector2 size = SizeLabel(c.label, labelText, maxWidth, maxHeight);
            Rect rect = new Rect(c.screenPos.x - size.x * .5f, c.screenPos.y + 4f,
                size.x, size.y);
            if (!c.label.go.activeSelf) c.label.go.SetActive(true);
            visibleCount++;

            // Final placement — pivot (0.5, 0) so the GO sits at
            // (centerX, bottomY) of the resolved rect.
            if (c.label.rt != null)
            {
                Vector2 finalScreen = new Vector2(rect.center.x, rect.yMin);
                var rectParent = c.label.rt.parent as RectTransform;
                if (rectParent != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(rectParent, finalScreen, null, out var localPoint))
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

    private static Vector2 SizeLabel(BeamLabelEntry label, string text, float maxWidth, float maxHeight = 34f)
    {
        if (label?.tmp == null || label.rt == null)
            return new Vector2(92f, 18f);
        Vector2 preferred = label.tmp.GetPreferredValues(text, maxWidth - 4f, maxHeight - 2f);
        Vector2 size = new Vector2(
            Mathf.Clamp(preferred.x + 4f, 42f, maxWidth),
            Mathf.Clamp(preferred.y + 2f, 18f, maxHeight));
        label.rt.sizeDelta = size;
        return size;
    }

    private static Rect ExpandRect(Rect rect, float padding)
    {
        return new Rect(rect.xMin - padding, rect.yMin - padding,
            rect.width + padding * 2f, rect.height + padding * 2f);
    }

    private static int Find(int[] parent, int index)
    {
        while (parent[index] != index)
        {
            parent[index] = parent[parent[index]];
            index = parent[index];
        }
        return index;
    }

    private static void Union(int[] parent, int a, int b)
    {
        int rootA = Find(parent, a);
        int rootB = Find(parent, b);
        if (rootA != rootB) parent[rootB] = rootA;
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
