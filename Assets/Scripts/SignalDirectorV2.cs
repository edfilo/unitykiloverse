using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using Kiloverse.Mapbox;

// ─────────────────────────────────────────────────────────────────
// SignalDirectorV2  –  Stateful signal lifecycle & placement
// ─────────────────────────────────────────────────────────────────
// Replaces VirtualGridSpawner's 3×3 fixed grid with a small,
// role-based active set: 1 primary pursuit + up to 3 secondary
// + up to 2 distant background signals.
//
// This file owns the GAMEPLAY LOOP only:
//   spawn → visible → pursued → locked → ready → resolved → chain
// It does NOT own visuals, radar, driving detection, or stories.
// ─────────────────────────────────────────────────────────────────

#region Enums

public enum SignalRole
{
    PrimaryPursuit,
    SecondaryNearby,
    DistantBackground,
    LocationTransmission   // Tied to a real-world place from TransmitterScanner
}

// ──────────────────────────────────────────────────────────────────
// TRANSMISSION TAXONOMY
// ──────────────────────────────────────────────────────────────────
// Everything the user sees as a "transmission" in the HUD is a Signal.
// Every Signal carries `transmissionType` as first-class metadata.
// There are exactly TWO user-facing types of transmission:
//
//   TransmissionType.Location
//     — Bound to a real POI on the map (SignalRole.LocationTransmission)
//     — Shown on the LocationRow of the HUD
//     — Survives in the world until the 20-min refresh or ENTER/cooldown
//
//   TransmissionType.Ambient
//     — Virtual/ephemeral pursuit signal not tied to a POI (every other
//       SignalRole: PrimaryPursuit, SecondaryNearby, DistantBackground)
//     — Shown on the AmbientRow (historically the "PursuitRow")
//     — Drifts/churns with the pursuit cycle
//
// Both render as beams in the world, hence "beam" is not a type name.
// When writing new code, refer to them as "location transmissions" and
// "ambient transmissions". Branch on `sig.transmissionType` directly;
// the IsLocationTransmission / IsAmbientTransmission helpers are sugar.
public enum TransmissionType { Location, Ambient }

public enum SignalType
{
    Presence,
    Anomaly,
    Chain
}

public enum SignalState
{
    Hidden,
    Visible,
    Pursued,
    Locked,
    ReadyToInterpret,
    Interpreting,
    Resolved,
    CoolingDown
}

#endregion

#region Signal Data

[System.Serializable]
public class Signal
{
    // Identity
    public string id;
    public SignalRole role;
    public SignalType type;                  // gameplay category (Presence/Anomaly/Chain)
    public TransmissionType transmissionType; // HUD taxonomy (Location/Ambient) — see enum at top of file
    public SignalState state;

    // Position — stored in all three forms so every consumer has what it needs
    public Vector2d mercatorPosition; // stable across floating-origin shifts
    public double latitude;           // WGS84
    public double longitude;          // WGS84

    // Timing
    public float spawnTime;          // Time.time when created
    public float lastStateChange;    // Time.time of most recent state transition
    public float pursuitStartTime;   // Time.time when pursuit began (-1 if never)

    // Chaining
    public string chainParentId;     // id of the signal this was chained from (null if root)

    // Narrative metadata — shared by both transmission types, drives teaser + dialog
    public string character;         // e.g. "cassie", "daniel" — null until a story primes
    public string specialItem;       // the object/reward — e.g. "red velvet ribbon"
    public string teaser;            // the HUD sentence (backend-authored or locally generated)

    // Location-only metadata (null on Ambient transmissions)
    public string locationName;      // e.g. "Recon Brewing at Meeder"
    public string locationCategory;  // e.g. "brewery", "bar", "coffee_shop"

    // Runtime visual handle (set externally by whatever renders beams)
    [NonSerialized] public GameObject visualGO;

    // Cached building bounds for ENTER proximity (set by SignalDirectorV2)
    [NonSerialized] public Bounds? buildingBounds;
    [NonSerialized] public float buildingBoundsTime; // Time.time when cached

    public Signal()
    {
        id = Guid.NewGuid().ToString("N").Substring(0, 8);
        pursuitStartTime = -1f;
    }

    public float Age => Time.time - spawnTime;
    public float TimeSinceStateChange => Time.time - lastStateChange;

    public void SetState(SignalState next)
    {
        if (state == next) return;
        var prev = state;
        state = next;
        lastStateChange = Time.time;
        if (next == SignalState.Pursued) pursuitStartTime = Time.time;
        Debug.Log($"[Signal {id}] {prev} → {next}  role={role}");
    }
}

#endregion

public class SignalDirectorV2 : MonoBehaviour
{
    // ── Singleton ──────────────────────────────────────────────
    public static SignalDirectorV2 Instance { get; private set; }

    // ── Tuning ─────────────────────────────────────────────────
    [Header("Spawn Distances (meters in Mercator ≈ Unity units)")]
    [Tooltip("Min distance for primary pursuit signal")]
    public float primaryMinDist = 50f;
    [Tooltip("Max distance for primary pursuit signal")]
    public float primaryMaxDist = 100f;

    [Tooltip("Min distance for secondary signals")]
    public float secondaryMinDist = 250f;
    [Tooltip("Max distance for secondary signals")]
    public float secondaryMaxDist = 350f;

    [Tooltip("Min distance for distant signals")]
    public float distantMinDist = 1000f;
    [Tooltip("Max distance for distant signals")]
    public float distantMaxDist = 1500f;

    [Header("Slot Limits")]
    public int maxSecondary = 2;
    public int maxDistant = 2;

    [Header("Churn")]
    [Tooltip("Seconds of no-pursuit before abandoned signals may churn")]
    public float churnWindowSeconds = 1500f; // 25 minutes

    [Tooltip("After churn window, seconds between individual signal replacements")]
    public float churnIntervalSeconds = 120f; // one every 2 min

    [Header("Pursuit")]
    [Tooltip("Distance at which the primary becomes Pursued (meters)")]
    public float pursuitRadius = 30f;

    [Tooltip("Distance at which a Pursued signal becomes Locked (meters)")]
    public float lockRadius = 8f;

    [Tooltip("Seconds the player must stay within lockRadius to trigger ReadyToInterpret")]
    public float lockDwellSeconds = 2f;

    [Tooltip("Seconds the Interpreting state lasts before Resolved")]
    public float interpretDuration = 3f;

    [Tooltip("Seconds a Resolved signal lingers before removal")]
    public float cooldownDuration = 5f;

    [Header("Timing")]
    [Tooltip("How often the director ticks (seconds)")]
    public float tickInterval = 1f;

    // ── Active set ─────────────────────────────────────────────
    private readonly List<Signal> signals = new List<Signal>();
    public IReadOnlyList<Signal> ActiveSignals => signals;

    // ── Events (for external systems: visuals, audio, HUD) ────
    public event Action<Signal> OnSignalSpawned;
    public event Action<Signal> OnSignalStateChanged;
    public event Action<Signal> OnSignalRemoved;
    public event Action<Signal, Signal> OnPrimaryChained; // (resolved, newPrimary)

    [Header("Road Snapping")]
    [Tooltip("Max distance to snap a signal to a road point. If no road within this range, signal is hidden.")]
    public float maxSnapDistance = 50f;

    [Tooltip("Offset from road edge onto sidewalk (meters)")]
    public float sidewalkOffset = 1.5f;

    // ── Internal state ─────────────────────────────────────────
    private float lastTickTime;
    private float lastChurnTime;
    private float lastPursuitTime = -1f; // last time a primary was in Pursued/Locked/etc
    private float lastStatusLogTime;
    private KiloverseMapInfo map;
    private GameObject playerObj;
    private bool initialized;

    // ── On-screen debug overlay ───────────────────────────────
    private TextMeshProUGUI debugText;

    // ── Player marker (discovery/zoomed-out mode) ─────────────
    private GameObject playerMarkerGO;
    private RectTransform playerMarkerRt;
    private Image playerMarkerDot;
    private Image playerMarkerRing;

    // ── Beam Transmission HUD (historically "pursuit row") ─────
    // Shows the nearest beam transmission's teaser, morphs into the
    // ENTER button when a beam is the active enter target.
    private TextMeshProUGUI pursuitLabel;     // green foreground (blinks)
    private TextMeshProUGUI pursuitTeaser;
    private TextMeshProUGUI pursuitDist;       // distance label under compass
    private RectTransform pursuitArrowRt;
    private Image pursuitCompassRing;
    private GameObject pursuitCompassGO;       // hidden when row becomes ENTER
    private GameObject pursuitRow;
    private GameObject pursuitPanel;
    private Image pursuitRowBg;                // darker backing revealed in ENTER mode
    private UnityEngine.UI.Button pursuitRowButton; // tap target in ENTER mode
    private string pursuitLabelOverride;
    private int pursuitLabelNormalFontSize = 16;
    private int pursuitLabelEnterFontSize = 44;
    private Vector2 pursuitLabelNormalOffset = new Vector2(52, 0);

    // ── Location Transmission HUD ─────────────────────────────
    private TextMeshProUGUI locLabel;         // green foreground (blinks)
    private TextMeshProUGUI locDist;           // distance label under compass
    private RectTransform locArrowRt;
    private Image locCompassRing;
    private GameObject locCompassGO;           // compass container, hidden when row becomes ENTER button
    private GameObject locRow;
    private GameObject locPanel;
    private Image locRowBg;                    // darker backing revealed when row is in ENTER mode
    private UnityEngine.UI.Button locRowButton; // tap target when row is ENTER mode
    private int locLabelNormalFontSize = 16;
    private int locLabelEnterFontSize = 44;
    private Vector2 locLabelNormalOffset = new Vector2(52, 0);
    private Vector2 locLabelEnterOffset = Vector2.zero;
    private TextMeshProUGUI locEnterText;
    private GameObject locEnterGO;
    private float locEnterFirstShownTime = -1f;   // when ENTER first became eligible
    private Signal locEnterStickySignal;           // signal ENTER is sticking to
    private const float ENTER_MIN_VISIBLE_SECONDS = 120f; // keep ENTER on for at least 2 min
    private const float ENTER_PROXIMITY_METERS = 10f;
    private Signal enterCandidate;                 // the signal the ENTER button currently targets

    // ── Shared enter-state (populated by ComputeEnterState, read by both rows)
    // These answer "is there an active ENTER target, and what kind?" — so the
    // location row morphs into ENTER only for location transmissions and the
    // beam row morphs into ENTER only for beam transmissions.
    private Signal enterTarget;
    private bool showEnter;
    private TransmissionType enterTargetType;

    // ── Player radius ring (god view) ────────────────────────
    private LineRenderer playerRing;
    private const float RING_RADIUS = 10f;
    private const int RING_SEGMENTS = 48;

    public void SetPursuitLabel(string text)
    {
        pursuitLabelOverride = text;
        var primary = GetPrimary();
        if (primary != null) primary.character = text;
    }

    public void SetPursuitTeaser(string text)
    {
        var primary = GetPrimary();
        if (primary != null) primary.teaser = text;
    }

    private bool hudSuppressed;
    public void SuppressHUD(bool suppress)
    {
        hudSuppressed = suppress;
        if (pursuitRow != null) pursuitRow.SetActive(!suppress);
        if (locRow != null) locRow.SetActive(!suppress);
        if (locEnterGO != null) locEnterGO.SetActive(!suppress);
    }

    // {0} = character, {1} = special item, {2} = countdown "M:SS" (already wrapped in white <color> at call site)
    // Tone: assertive walk-to-retrieve. Lead with a verb in caps. Object stays Lynchian —
    // booth seats, humming lightbulbs, things that know you.
    private static readonly string[] pursuitTeasers = new[]
    {
        "WALK TO {0}. a {1} is on the booth seat {2}",
        "GO. {0} has a {1} and the lights keep flickering {2}",
        "MOVE. {0} is humming over a {1} that has your name {2}",
        "WALK. {0} left the door cracked. the {1} is on the counter {2}",
        "GO NOW. {0} is in the back booth with a {1} {2}",
        "WALK TO {0}. the red curtain is already open. a {1} waits {2}",
        "RUN. {0} won't hold the {1} forever {2}",
        "MOVE. the {1} is wet and warm and it knows you {2}",
        "WALK. {0} set two cups out. a {1} between them {2}",
        "GO. {0} keeps saying your name backwards over a {1} {2}",
        "WALK TO {0}. the {1} hums in their pocket {2}",
        "MOVE. {0} keeps glancing at the door, holding a {1} {2}",
        "GO. {0} only hands the {1} over in person {2}",
        "WALK. the street dims every second you don't. {0}. {1} {2}",
        "GO TO {0}. they're pretending not to wait. a {1} on the table {2}",
        "WALK. {0} laid the {1} out like an offering {2}",
    };

    // Pool of walk-reward items. Short, seductive, Lynchian — diner booths, red curtains,
    // small perfect objects that feel slightly wrong.
    private static readonly string[] specialItems = new[]
    {
        "red velvet ribbon", "still-warm slice of pie", "humming lightbulb",
        "owl feather", "single pearl earring", "cassette labeled MAYBE",
        "lipstick kiss on a napkin", "key with no door", "matchbook from a place that closed",
        "polaroid of a stranger with your face", "phone number written in eyeliner",
        "lock of blonde hair", "small perfect peach", "silk bag of baby teeth",
        "glass of milk, ice-cold", "postcard signed only X",
    };

    public static string PickSpecialItem()
    {
        return specialItems[UnityEngine.Random.Range(0, specialItems.Length)];
    }

    // ──────────────────────────────────────────────────────────────
    // Transmission-type helpers — see the TransmissionType enum at the
    // top of this file. Reads `sig.transmissionType` directly; the field
    // is set at spawn time and is the source of truth.
    // ──────────────────────────────────────────────────────────────
    public static TransmissionType TypeOf(Signal sig)
        => sig != null ? sig.transmissionType : TransmissionType.Ambient;

    public static bool IsLocationTransmission(Signal sig)
        => sig != null && sig.transmissionType == TransmissionType.Location;

    public static bool IsAmbientTransmission(Signal sig)
        => sig != null && sig.transmissionType == TransmissionType.Ambient;

    /// <summary>Fired when user taps ENTER on a location transmission.</summary>
    public event Action<Signal> OnLocationEnter;

    // ── Road geometry cache ────────────────────────────────────
    private List<Vector2d> roadPoints;       // all walkable road vertices in Mercator
    private Transform roadLayerFolder;
    private int lastKnownRoadMeshCount = -1;
    private bool roadsWereAvailable;         // tracks if we've ever had roads

    private static readonly HashSet<string> WalkableRoadClasses = new HashSet<string>
    {
        "residential", "living_street", "pedestrian", "footway", "path",
        "tertiary", "secondary", "primary", "unclassified", "service", "steps",
        "cycleway", "bridleway", "track", "primary_link", "secondary_link", "tertiary_link"
    };

    // ───────────────────────────────────────────────────────────
    // Auto-bootstrap (no manual scene setup needed)
    // ───────────────────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoBootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("SignalDirectorV2");
        DontDestroyOnLoad(go);
        go.AddComponent<SignalDirectorV2>();
        go.AddComponent<SignalBeamBridge>();

        // Disable old VirtualGridSpawner so they don't fight
        var oldSpawner = UnityEngine.Object.FindFirstObjectByType<VirtualGridSpawner>();
        if (oldSpawner != null)
        {
            oldSpawner.enabled = false;
            Debug.Log("[SignalDirector] Disabled old VirtualGridSpawner");
        }

        Debug.Log("[SignalDirector] Auto-bootstrapped SignalDirectorV2 + SignalBeamBridge");
    }

    // ───────────────────────────────────────────────────────────
    // Unity lifecycle
    // ───────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Input.compass.enabled = true;
        Input.location.Start();
    }

    void Update()
    {
        if (Time.frameCount % 300 == 0)
            Debug.Log($"[SignalDirector] Update tick f={Time.frameCount} init={initialized}");

        if (!initialized)
        {
            TryInitialize();
            return;
        }

        // Enter-state is shared by both rows — compute once per frame so the
        // location and beam rows agree on which transmission (if any) is
        // currently the ENTER target.
        ComputeEnterState();

        // HUD updates every frame for smooth distance
        UpdatePursuitHUD();
        UpdateLocationHUD();
        UpdatePlayerRing();
        UpdatePlayerMarker();

        if (Time.time - lastTickTime < tickInterval) return;
        lastTickTime = Time.time;

        Tick();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ───────────────────────────────────────────────────────────
    // Initialization (waits for GPS + map like VirtualGridSpawner)
    // ───────────────────────────────────────────────────────────

    private float lastInitLogTime;

    private void TryInitialize()
    {
        bool gpsReady = GPSLocationController.GPSReady;

        if (map == null)
            map = FindFirstObjectByType<KiloverseMapInfo>();

        if (playerObj == null)
            playerObj = GameObject.Find("Player");

        // Log what we're waiting on (every 3s)
        if (Time.time - lastInitLogTime >= 3f)
        {
            lastInitLogTime = Time.time;
            Debug.Log($"[SignalDirector] TryInitialize: GPS={gpsReady} map={map != null} player={playerObj != null}");
        }

        if (!gpsReady) return;
        if (map == null) return;
        if (playerObj == null) return;

        initialized = true;
        lastTickTime = Time.time;
        lastChurnTime = Time.time;
        Debug.Log("[SignalDirector] Initialized.");

        CreateDebugOverlay();
        CreatePursuitHUD();
        CreateLocationHUD();
        CreatePlayerMarker();

        // Seed the initial active set
        EnsurePrimary();
        FillSecondaries();
        FillDistant();
    }

    private void CreateDebugOverlay()
    {
        var go = new GameObject("SignalDebugLabel");
        go.transform.SetParent(K1L0CanvasRoot.HUD, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(1, 0);
        rt.pivot = new Vector2(0, 0);
        rt.anchoredPosition = new Vector2(4, 104);
        rt.sizeDelta = new Vector2(0, 160);

        debugText = go.AddComponent<TextMeshProUGUI>();
        var font = Resources.Load<TMP_FontAsset>("Fonts/IBMPlexMono-Regular SDF");
        if (font == null) font = TMP_Settings.defaultFontAsset;
        debugText.font = font;
        debugText.fontSize = 14;
        debugText.color = new Color(1f, 0.9f, 0.5f, 0.85f);
        debugText.alignment = TextAlignmentOptions.BottomLeft;
        debugText.raycastTarget = false;
        debugText.enableWordWrapping = false;
        debugText.enabled = ProfileEditorModal.ShowBeamDebug;
        ProfileEditorModal.OnDebugTogglesChanged += () => {
            if (debugText != null) debugText.enabled = ProfileEditorModal.ShowBeamDebug;
        };
    }

    private void CreatePursuitHUD()
    {
        var font = Resources.Load<TMP_FontAsset>("Fonts/IBMPlexMono-Regular SDF");
        if (font == null) font = TMP_Settings.defaultFontAsset;

        // Row container — holds compass + text side by side. Morphs into an
        // ENTER button when the active enter target is a beam transmission.
        pursuitRow = new GameObject("PursuitRow");
        pursuitRow.transform.SetParent(K1L0CanvasRoot.HUD, false);
        var rowRt = pursuitRow.AddComponent<RectTransform>();
        rowRt.anchorMin = new Vector2(0f, 1f);
        rowRt.anchorMax = new Vector2(1f, 1f);
        rowRt.pivot = new Vector2(0f, 1f);
        rowRt.anchoredPosition = new Vector2(12, -165);
        rowRt.sizeDelta = new Vector2(-24, 58);
        pursuitPanel = pursuitRow;

        // Row backing — transparent by default, darkens when the row becomes the ENTER button
        pursuitRowBg = pursuitRow.AddComponent<Image>();
        pursuitRowBg.color = new Color(0f, 0f, 0f, 0f);
        // Button on the row — only interactable when showEnter + target is a beam
        pursuitRowButton = pursuitRow.AddComponent<UnityEngine.UI.Button>();
        pursuitRowButton.targetGraphic = pursuitRowBg;
        pursuitRowButton.onClick.AddListener(OnEnterLocationTapped);
        pursuitRowButton.interactable = false;

        // Compass circle + arrow (left side)
        pursuitCompassGO = CreateCompassWidget(pursuitRow.transform, "PursuitCompass", out pursuitCompassRing, out pursuitArrowRt, out pursuitDist);

        // Foreground text layer — green, blinks, with heavy drop shadow
        pursuitLabel = CreateHUDTextLayer(pursuitRow.transform, "PursuitLabel", font, pursuitLabelNormalFontSize, pursuitLabelNormalOffset);
        pursuitLabel.color = new Color(0.47f, 1f, 0.54f, 1f);
        ApplyHeavyShadow(pursuitLabel);

        pursuitTeaser = null;
    }

    private void CreateLocationHUD()
    {
        var font = Resources.Load<TMP_FontAsset>("Fonts/IBMPlexMono-Regular SDF");
        if (font == null) font = TMP_Settings.defaultFontAsset;

        // Row container — same layout as pursuit: compass + text
        locRow = new GameObject("LocRow");
        locRow.transform.SetParent(K1L0CanvasRoot.HUD, false);
        var rowRt = locRow.AddComponent<RectTransform>();
        rowRt.anchorMin = new Vector2(0f, 1f);
        rowRt.anchorMax = new Vector2(1f, 1f);
        rowRt.pivot = new Vector2(0f, 1f);
        rowRt.anchoredPosition = new Vector2(12, -245);
        rowRt.sizeDelta = new Vector2(-24, 58);
        locPanel = locRow;

        // Row backing — transparent by default, darkens when the row becomes the ENTER button
        locRowBg = locRow.AddComponent<Image>();
        locRowBg.color = new Color(0f, 0f, 0f, 0f);
        // Button on the row — only active when showEnter is true
        locRowButton = locRow.AddComponent<UnityEngine.UI.Button>();
        locRowButton.targetGraphic = locRowBg;
        locRowButton.onClick.AddListener(OnEnterLocationTapped);
        locRowButton.interactable = false;

        // Compass circle + arrow (left side)
        locCompassGO = CreateCompassWidget(locRow.transform, "LocCompass", out locCompassRing, out locArrowRt, out locDist);

        // Foreground text layer — green, blinks, with heavy drop shadow
        locLabel = CreateHUDTextLayer(locRow.transform, "LocLabel", font, locLabelNormalFontSize, locLabelNormalOffset);
        locLabel.color = new Color(0.47f, 1f, 0.54f, 1f);
        ApplyHeavyShadow(locLabel);

        // ENTER button — centered, large, unmissable
        locEnterGO = new GameObject("LocEnter");
        locEnterGO.transform.SetParent(K1L0CanvasRoot.HUD, false);
        var eRt = locEnterGO.AddComponent<RectTransform>();
        eRt.anchorMin = new Vector2(0.5f, 0.5f);
        eRt.anchorMax = new Vector2(0.5f, 0.5f);
        eRt.pivot = new Vector2(0.5f, 0.5f);
        eRt.anchoredPosition = new Vector2(0, 0);
        eRt.sizeDelta = new Vector2(700, 140);

        // Tap area + subtle backing
        var enterBgImg = locEnterGO.AddComponent<Image>();
        enterBgImg.color = new Color(0f, 0f, 0f, 0.55f);
        var enterBtn = locEnterGO.AddComponent<UnityEngine.UI.Button>();
        enterBtn.onClick.AddListener(OnEnterLocationTapped);

        // Enter fg layer
        var enterFgGO = new GameObject("LocEnterFg");
        enterFgGO.transform.SetParent(locEnterGO.transform, false);
        var efRt = enterFgGO.AddComponent<RectTransform>();
        efRt.anchorMin = Vector2.zero; efRt.anchorMax = Vector2.one;
        efRt.offsetMin = Vector2.zero; efRt.offsetMax = Vector2.zero;
        locEnterText = enterFgGO.AddComponent<TextMeshProUGUI>();
        locEnterText.font = font;
        locEnterText.fontSize = 56;
        locEnterText.text = "> ENTER TRANSMISSION_";
        locEnterText.color = new Color(0.47f, 1f, 0.54f, 1f);
        locEnterText.alignment = TextAlignmentOptions.Center;
        locEnterText.raycastTarget = false;
        locEnterText.richText = true;
        ApplyHeavyShadow(locEnterText);

        locPanel.SetActive(false);
        locEnterGO.SetActive(false);
    }

    private void CreatePlayerMarker()
    {
        float markerSize = 26f;

        playerMarkerGO = new GameObject("PlayerMarker");
        playerMarkerGO.transform.SetParent(K1L0CanvasRoot.HUD, false);
        playerMarkerRt = playerMarkerGO.AddComponent<RectTransform>();
        playerMarkerRt.anchorMin = new Vector2(0f, 0f);
        playerMarkerRt.anchorMax = new Vector2(0f, 0f);
        playerMarkerRt.pivot = new Vector2(0.5f, 0.5f);
        playerMarkerRt.sizeDelta = new Vector2(markerSize, markerSize);

        // Outer pulse ring
        var ringGO = new GameObject("Ring");
        ringGO.transform.SetParent(playerMarkerGO.transform, false);
        var ringRt = ringGO.AddComponent<RectTransform>();
        ringRt.anchorMin = Vector2.zero; ringRt.anchorMax = Vector2.one;
        ringRt.offsetMin = Vector2.zero; ringRt.offsetMax = Vector2.zero;
        playerMarkerRing = ringGO.AddComponent<Image>();
        playerMarkerRing.color = new Color(0.47f, 1f, 0.54f, 0.55f);
        playerMarkerRing.raycastTarget = false;
        CreateRingSprite(playerMarkerRing);

        // Inner filled dot
        var dotGO = new GameObject("Dot");
        dotGO.transform.SetParent(playerMarkerGO.transform, false);
        var dotRt = dotGO.AddComponent<RectTransform>();
        dotRt.anchorMin = new Vector2(0.5f, 0.5f);
        dotRt.anchorMax = new Vector2(0.5f, 0.5f);
        dotRt.pivot = new Vector2(0.5f, 0.5f);
        dotRt.anchoredPosition = Vector2.zero;
        dotRt.sizeDelta = new Vector2(markerSize * 0.45f, markerSize * 0.45f);
        playerMarkerDot = dotGO.AddComponent<Image>();
        playerMarkerDot.color = new Color(0.47f, 1f, 0.54f, 1f);
        playerMarkerDot.raycastTarget = false;
        CreateFilledCircleSprite(playerMarkerDot);

        playerMarkerGO.SetActive(false);
    }

    private void UpdatePlayerMarker()
    {
        if (playerMarkerGO == null) return;

        bool shouldShow = K1L0Mode.IsDiscovery && playerObj != null && Camera.main != null && !hudSuppressed;
        if (!shouldShow)
        {
            if (playerMarkerGO.activeSelf) playerMarkerGO.SetActive(false);
            return;
        }

        Vector3 screenPos = Camera.main.WorldToScreenPoint(playerObj.transform.position);
        bool onScreen = screenPos.z > 0f
                        && screenPos.x > 0f && screenPos.x < Screen.width
                        && screenPos.y > 0f && screenPos.y < Screen.height;

        if (onScreen != playerMarkerGO.activeSelf) playerMarkerGO.SetActive(onScreen);
        if (!onScreen) return;

        playerMarkerRt.position = new Vector3(screenPos.x, screenPos.y, 0f);

        // Pulse the ring so the marker reads as "you are here" rather than a static dot
        float pulse = Mathf.PingPong(Time.time * 1.2f, 1f);
        float scale = 1f + pulse * 0.35f;
        playerMarkerRt.localScale = new Vector3(scale, scale, 1f);
        if (playerMarkerRing != null)
            playerMarkerRing.color = new Color(0.47f, 1f, 0.54f, 0.55f - pulse * 0.3f);
    }

    private void OnEnterLocationTapped()
    {
        // Prefer the current enter candidate (nearest beam within 10m, any role);
        // fall back to the active LocationTransmission if sticky-window kept ENTER visible
        // after the player stepped away.
        var target = enterCandidate ?? GetLocationTransmission();
        if (target == null) return;

        EnsureLocationMetadata(target);
        Debug.Log($"[SignalDirector] ENTER tapped for '{target.locationName}' (role={target.role})");

        // Resolve the storyId bound to this signal so the frame can pin to it,
        // surviving any pursuit chains that happen while the shot is generating.
        string storyId = TransmissionManager.Instance != null
            ? TransmissionManager.Instance.GetStoryIdForSignal(target.id)
            : null;

        var frame = TransmissionFrame.Instance;
        if (frame != null)
            frame.ShowLoading(target.locationName, target.locationCategory, storyId);

        OnLocationEnter?.Invoke(target);
        TransitionTo(target, SignalState.Interpreting);
    }

    // Non-LocationTransmission signals (pursuit/secondary/distant) represent virtual
    // characters, not real POIs. The TransmissionFrame header should carry the
    // character's name while the backend generates the shot — leaving locationName
    // blank here lets OnTransmissionReady overwrite it with whatever the story
    // backend returns (character context, scene, etc.).
    private void EnsureLocationMetadata(Signal sig)
    {
        if (!string.IsNullOrEmpty(sig.locationName)) return;

        sig.locationName = string.IsNullOrEmpty(pursuitLabelOverride)
            ? "INCOMING TRANSMISSION"
            : pursuitLabelOverride.ToUpper();
        sig.locationCategory = "transmission";
    }

    // Nearest non-cooldown, non-resolved signal within ENTER_PROXIMITY_METERS of
    // the player — any role. The whole point of the game is "see beam, walk close,
    // ENTER", so every visible beam should be enterable.
    private Signal FindNearestEnterableSignal(Vector2d playerMerc)
    {
        Signal best = null;
        float bestDist = float.MaxValue;
        for (int i = 0; i < signals.Count; i++)
        {
            var s = signals[i];
            if (s.state == SignalState.CoolingDown) continue;
            if (s.state == SignalState.Interpreting || s.state == SignalState.Resolved) continue;
            float d = DistanceTo(s, playerMerc);
            if (d <= ENTER_PROXIMITY_METERS && d < bestDist)
            {
                bestDist = d;
                best = s;
            }
        }
        return best;
    }

    // Compute the shared ENTER state: is there an active enter target, what
    // kind of transmission is it, and should the row(s) morph into ENTER mode.
    // Writes to fields: enterCandidate, enterTarget, showEnter, enterTargetType.
    // Called once per frame before the two HUD updates.
    private void ComputeEnterState()
    {
        var playerMerc = GetPlayerMercator();
        var loc = GetLocationTransmission();

        // Any beam within 10m is enterable, not just the LocationTransmission. The
        // player's mental model is "see transmission → walk close → ENTER"; gating
        // only on location transmissions meant beam transmissions the player could
        // literally stand next to were silently unenterable.
        var nearestBeam = FindNearestEnterableSignal(playerMerc);
        enterCandidate = nearestBeam;

        float dist = loc != null ? DistanceTo(loc, playerMerc) : float.MaxValue;
        Vector3 playerWorld = playerObj != null ? playerObj.transform.position : Vector3.zero;
        bool insideBuilding = loc != null && IsPlayerInsideLocationBuilding(loc, playerWorld);
        bool locProximity = loc != null
                            && (insideBuilding || dist <= 10f)
                            && loc.state != SignalState.Interpreting
                            && loc.state != SignalState.Resolved;
        bool proximityMet = locProximity || nearestBeam != null;

        // Whichever signal triggered ENTER becomes the sticky target so the button
        // stays on for ENTER_MIN_VISIBLE_SECONDS even if GPS jitter bumps the player
        // just outside the 10m ring.
        Signal stickyTarget = nearestBeam != null ? nearestBeam : (locProximity ? loc : null);
        if (proximityMet && stickyTarget != null)
        {
            if (locEnterStickySignal != stickyTarget || locEnterFirstShownTime < 0f)
            {
                locEnterStickySignal = stickyTarget;
                locEnterFirstShownTime = Time.time;
            }
        }
        bool withinStickyWindow = locEnterStickySignal != null
                                  && locEnterFirstShownTime >= 0f
                                  && (Time.time - locEnterFirstShownTime) < ENTER_MIN_VISIBLE_SECONDS
                                  && locEnterStickySignal.state != SignalState.Interpreting
                                  && locEnterStickySignal.state != SignalState.Resolved
                                  && locEnterStickySignal.state != SignalState.CoolingDown;
        showEnter = proximityMet || withinStickyWindow;

        if (showEnter && enterCandidate == null)
            enterCandidate = locEnterStickySignal;

        enterTarget = showEnter ? (enterCandidate ?? locEnterStickySignal) : null;
        enterTargetType = TypeOf(enterTarget);

        if (!showEnter && locEnterStickySignal != null)
        {
            locEnterStickySignal = null;
            locEnterFirstShownTime = -1f;
        }
    }

    private void UpdateLocationHUD()
    {
        if (locLabel == null || locRow == null) return;

        var playerMerc = GetPlayerMercator();
        var loc = GetLocationTransmission();

        bool hasTeaser = loc != null && loc.state != SignalState.CoolingDown;
        if (!hasTeaser)
        {
            locRow.SetActive(false);
            if (locEnterGO != null) locEnterGO.SetActive(false);
            return;
        }

        locRow.SetActive(!hudSuppressed);

        string teaser = loc != null ? (loc.teaser ?? "") : "";
        float dist = loc != null ? DistanceTo(loc, playerMerc) : float.MaxValue;

        // This row only morphs into ENTER when the active enter target is the
        // LOCATION transmission. Beam-enter lives on the beam row instead.
        bool showLocationEnter = showEnter && enterTargetType == TransmissionType.Location;

        // Legacy centered ENTER overlay — disabled. Row transforms into the button instead.
        if (locEnterGO != null && locEnterGO.activeSelf) locEnterGO.SetActive(false);

        // Countdown + teaser sentence are only rendered in non-ENTER (teaser) mode,
        // and that mode requires a LocationTransmission to exist.
        string sentence = "";
        if (loc != null)
        {
            float locRemaining = Mathf.Max(0f, churnWindowSeconds - loc.Age);
            string locCd = $"<color=#FFFFFFFF>{Mathf.FloorToInt(locRemaining / 60f)}:{Mathf.FloorToInt(locRemaining % 60f):D2}</color>";
            sentence = string.IsNullOrEmpty(teaser)
                ? $"WALK to {(loc.locationName ?? "UNKNOWN").ToUpper()} in {locCd} → claim your {loc.specialItem}"
                : $"{teaser} · {locCd}";
        }

        if (locDist != null) locDist.text = loc != null ? $"{dist:F0}m" : "";

        float tBlink = Mathf.PingPong(Time.time * 0.5f, 1f);
        float textAlpha = 0.4f + tBlink * 0.6f;

        if (showLocationEnter)
        {
            // Row becomes the ENTER button: big centered label, dark backing, tappable.
            if (locCompassGO != null && locCompassGO.activeSelf) locCompassGO.SetActive(false);
            if (locRowBg != null) locRowBg.color = new Color(0f, 0f, 0f, 0.65f);
            if (locRowButton != null) locRowButton.interactable = !hudSuppressed;

            var labelRt = locLabel.rectTransform;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            locLabel.fontSize = locLabelEnterFontSize;
            locLabel.alignment = TextAlignmentOptions.Center;

            string targetName = enterTarget?.locationName;
            locLabel.text = string.IsNullOrEmpty(targetName)
                ? "> ENTER TRANSMISSION_"
                : $"> ENTER {targetName.ToUpper()}_";

            float blink = Mathf.PingPong(Time.time * 1.5f, 1f);
            float ea = 0.45f + blink * 0.55f;
            locLabel.color = new Color(0.47f, 1f, 0.54f, ea);

            // Resize row for a more button-like tap target
            var rRt = locRow.GetComponent<RectTransform>();
            if (rRt != null && rRt.sizeDelta.y < 110f) rRt.sizeDelta = new Vector2(rRt.sizeDelta.x, 110f);
        }
        else
        {
            // Normal teaser row: compass + sentence, no backing, not tappable.
            if (locCompassGO != null && !locCompassGO.activeSelf) locCompassGO.SetActive(true);
            if (locRowBg != null) locRowBg.color = new Color(0f, 0f, 0f, 0f);
            if (locRowButton != null) locRowButton.interactable = false;

            var labelRt = locLabel.rectTransform;
            labelRt.offsetMin = new Vector2(locLabelNormalOffset.x, locLabelNormalOffset.y);
            labelRt.offsetMax = Vector2.zero;
            locLabel.fontSize = locLabelNormalFontSize;
            locLabel.alignment = TextAlignmentOptions.MidlineLeft;
            locLabel.text = sentence;
            locLabel.color = new Color(0.47f, 1f, 0.54f, textAlpha);

            var rRt = locRow.GetComponent<RectTransform>();
            if (rRt != null && rRt.sizeDelta.y > 58f) rRt.sizeDelta = new Vector2(rRt.sizeDelta.x, 58f);

            // Compass ring — steady
            if (locCompassRing != null)
                locCompassRing.color = new Color(0.47f, 1f, 0.54f, 0.6f);

            // Rotate compass arrow
            if (locArrowRt != null && loc != null)
            {
                float relAngle = RelativeAngleTo(loc, playerMerc);
                locArrowRt.localRotation = Quaternion.Euler(0, 0, -relAngle);
                var arrowImg = locArrowRt.GetComponent<Image>();
                if (arrowImg != null)
                    arrowImg.color = new Color(0.47f, 1f, 0.54f, textAlpha);
            }
        }
    }

    private void UpdatePlayerRing()
    {
        if (playerObj == null) return;

        var fpc = playerObj.GetComponent<KiloFirstPersonController>();
        bool showRing = fpc != null && fpc.IsGodView;

        if (showRing)
        {
            if (playerRing == null)
            {
                var ringGO = new GameObject("PlayerRing");
                playerRing = ringGO.AddComponent<LineRenderer>();
                playerRing.useWorldSpace = true;
                playerRing.loop = true;
                playerRing.positionCount = RING_SEGMENTS;
                playerRing.widthMultiplier = 0.3f;
                playerRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                playerRing.receiveShadows = false;

                // Unlit green material
                var mat = new Material(Shader.Find("Sprites/Default"));
                mat.color = new Color(0.47f, 1f, 0.54f, 0.5f);
                playerRing.material = mat;
                playerRing.startColor = new Color(0.47f, 1f, 0.54f, 0.5f);
                playerRing.endColor = new Color(0.47f, 1f, 0.54f, 0.5f);
            }

            playerRing.enabled = true;
            Vector3 center = playerObj.transform.position;
            center.y += 0.05f; // slightly above ground
            for (int i = 0; i < RING_SEGMENTS; i++)
            {
                float angle = (float)i / RING_SEGMENTS * Mathf.PI * 2f;
                playerRing.SetPosition(i, new Vector3(
                    center.x + Mathf.Cos(angle) * RING_RADIUS,
                    center.y,
                    center.z + Mathf.Sin(angle) * RING_RADIUS
                ));
            }
        }
        else if (playerRing != null)
        {
            playerRing.enabled = false;
        }
    }

    private void UpdatePursuitHUD()
    {
        if (pursuitLabel == null || pursuitRow == null) return;

        var primary = GetPrimary();
        if (primary == null)
        {
            pursuitRow.SetActive(false);
            return;
        }

        pursuitRow.SetActive(!hudSuppressed);

        var playerMerc = GetPlayerMercator();
        float dist = DistanceTo(primary, playerMerc);

        // This row only morphs into ENTER when the active enter target is an
        // Ambient transmission. Location-enter lives on the location row.
        bool showAmbientEnter = showEnter && enterTargetType == TransmissionType.Ambient;

        if (pursuitDist != null) pursuitDist.text = $"{dist:F0}m";

        float tBlink = Mathf.PingPong(Time.time * 0.5f, 1f);
        float textAlpha = 0.4f + tBlink * 0.6f;

        if (showAmbientEnter)
        {
            // Row becomes the ENTER button for the ambient transmission.
            if (pursuitCompassGO != null && pursuitCompassGO.activeSelf) pursuitCompassGO.SetActive(false);
            if (pursuitRowBg != null) pursuitRowBg.color = new Color(0f, 0f, 0f, 0.65f);
            if (pursuitRowButton != null) pursuitRowButton.interactable = !hudSuppressed;

            var labelRt = pursuitLabel.rectTransform;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            pursuitLabel.fontSize = pursuitLabelEnterFontSize;
            pursuitLabel.alignment = TextAlignmentOptions.Center;

            // Ambient transmissions don't have a POI name — look up the bound
            // story's character via TransmissionManager.
            string targetName = null;
            var tm = TransmissionManager.Instance;
            if (enterTarget != null && tm != null)
            {
                var sid = tm.GetStoryIdForSignal(enterTarget.id);
                if (!string.IsNullOrEmpty(sid))
                    targetName = tm.GetStoryShell(sid).character;
            }
            pursuitLabel.text = string.IsNullOrEmpty(targetName)
                ? "> ENTER TRANSMISSION_"
                : $"> ENTER {targetName.ToUpper()}_";

            float blink = Mathf.PingPong(Time.time * 1.5f, 1f);
            float ea = 0.45f + blink * 0.55f;
            pursuitLabel.color = new Color(0.47f, 1f, 0.54f, ea);

            var rRt = pursuitRow.GetComponent<RectTransform>();
            if (rRt != null && rRt.sizeDelta.y < 110f) rRt.sizeDelta = new Vector2(rRt.sizeDelta.x, 110f);
            return;
        }

        // Normal teaser row: compass + rotating sentence.
        if (pursuitCompassGO != null && !pursuitCompassGO.activeSelf) pursuitCompassGO.SetActive(true);
        if (pursuitRowBg != null) pursuitRowBg.color = new Color(0f, 0f, 0f, 0f);
        if (pursuitRowButton != null) pursuitRowButton.interactable = false;

        var normalRt = pursuitLabel.rectTransform;
        normalRt.offsetMin = new Vector2(pursuitLabelNormalOffset.x, pursuitLabelNormalOffset.y);
        normalRt.offsetMax = Vector2.zero;
        pursuitLabel.fontSize = pursuitLabelNormalFontSize;
        pursuitLabel.alignment = TextAlignmentOptions.MidlineLeft;

        var rowRt = pursuitRow.GetComponent<RectTransform>();
        if (rowRt != null && rowRt.sizeDelta.y > 58f) rowRt.sizeDelta = new Vector2(rowRt.sizeDelta.x, 58f);

        // Countdown — time remaining before primary churns (rendered solid white)
        float pursuitRemaining = Mathf.Max(0f, churnWindowSeconds - primary.Age);
        string pursuitCd = $"<color=#FFFFFFFF>{Mathf.FloorToInt(pursuitRemaining / 60f)}:{Mathf.FloorToInt(pursuitRemaining % 60f):D2}</color>";

        string teaser;
        if (!string.IsNullOrEmpty(primary.teaser))
        {
            // Prefer LLM-authored teaser from the Signal (set via SetPursuitTeaser
            // when a story shot lands). Append countdown to keep the churn visible.
            teaser = $"{primary.teaser} {pursuitCd}";
        }
        else
        {
            // Fallback: rotate through the local template pool every 30s until the
            // backend ships a teaser for this signal.
            string charName = !string.IsNullOrEmpty(primary.character)
                ? primary.character.ToLower()
                : (string.IsNullOrEmpty(pursuitLabelOverride) ? "someone" : pursuitLabelOverride.ToLower());
            string item = string.IsNullOrEmpty(primary.specialItem) ? "mystery drop" : primary.specialItem;

            int seed = Mathf.Abs(primary.id?.GetHashCode() ?? 0);
            int timeSlot = Mathf.FloorToInt(Time.time / 30f);
            int idx = (seed + timeSlot) % pursuitTeasers.Length;
            teaser = string.Format(pursuitTeasers[idx], charName, item, pursuitCd);
        }

        pursuitLabel.text = teaser;
        pursuitLabel.color = new Color(0.47f, 1f, 0.54f, textAlpha);

        // Rotate compass arrow to point toward signal relative to device heading
        if (pursuitArrowRt != null)
        {
            float relAngle = RelativeAngleTo(primary, playerMerc);
            pursuitArrowRt.localRotation = Quaternion.Euler(0, 0, -relAngle);
            var arrowImg = pursuitArrowRt.GetComponent<Image>();
            if (arrowImg != null)
                arrowImg.color = new Color(0.47f, 1f, 0.54f, textAlpha);
        }

        // Compass ring — steady, no blink
        if (pursuitCompassRing != null)
            pursuitCompassRing.color = new Color(0.47f, 1f, 0.54f, 0.6f);
    }

    // ───────────────────────────────────────────────────────────
    // Core tick
    // ───────────────────────────────────────────────────────────

    private void Tick()
    {
        Vector2d playerMerc = GetPlayerMercator();

        // 1. Advance state machines on every signal
        for (int i = signals.Count - 1; i >= 0; i--)
            AdvanceSignal(signals[i], playerMerc);

        // 2. Remove finished signals
        for (int i = signals.Count - 1; i >= 0; i--)
        {
            if (signals[i].state == SignalState.CoolingDown &&
                signals[i].TimeSinceStateChange >= cooldownDuration)
            {
                RemoveSignal(signals[i]);
            }
        }

        // 3. Ensure we always have a primary
        EnsurePrimary();

        // 4. Fill secondary / distant slots
        FillSecondaries();
        FillDistant();

        // 5. Re-snap signals to roads once roads become available
        RefreshRoadCache();
        if (!roadsWereAvailable && roadPoints != null && roadPoints.Count > 0)
        {
            roadsWereAvailable = true;
            Debug.Log($"[SignalDirector] Roads now available ({roadPoints.Count} points) — re-snapping all signals");
            ReSnapAllSignals(playerMerc);
        }

        // 6. Churn abandoned signals (only when no pursuit is active)
        HandleChurn(playerMerc);

        // 7. Periodic status log (every 2s)
        if (Time.time - lastStatusLogTime >= 2f)
        {
            lastStatusLogTime = Time.time;
            LogStatus(playerMerc);
        }
    }

    private void LogStatus(Vector2d playerMerc)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[SignalDirector] ── Status ({signals.Count} signals) ──");
        for (int i = 0; i < signals.Count; i++)
        {
            var s = signals[i];
            float dist = DistanceTo(s, playerMerc);
            sb.AppendLine($"  {s.role,-20} {s.state,-18} dist={dist,6:F1}m  age={s.Age:F0}s  id={s.id}");
        }
        Debug.Log(sb.ToString());

        // Update on-screen overlay
        if (debugText != null)
        {
            var dsb = new System.Text.StringBuilder();
            dsb.AppendLine($"SIGNALS ({signals.Count})  roads={roadPoints?.Count ?? 0}");
            for (int i = 0; i < signals.Count; i++)
            {
                var s = signals[i];
                float dist = DistanceTo(s, playerMerc);
                string tag = s.role == SignalRole.PrimaryPursuit ? "PRI" :
                             s.role == SignalRole.SecondaryNearby ? "SEC" : "FAR";
                dsb.AppendLine($" {tag} {dist,5:F0}m {s.state}");
            }
            debugText.text = dsb.ToString();
        }
    }

    /// <summary>
    /// Re-snap all signals to the nearest road point within their role's distance ring.
    /// Called once when roads first become available after initial spawn.
    /// </summary>
    private void ReSnapAllSignals(Vector2d playerMerc)
    {
        for (int i = 0; i < signals.Count; i++)
        {
            var sig = signals[i];
            float minD, maxD;
            switch (sig.role)
            {
                case SignalRole.PrimaryPursuit:  minD = primaryMinDist;   maxD = primaryMaxDist;   break;
                case SignalRole.SecondaryNearby:  minD = secondaryMinDist; maxD = secondaryMaxDist; break;
                case SignalRole.DistantBackground:minD = distantMinDist;  maxD = distantMaxDist;   break;
                default: continue;
            }
            var oldPos = sig.mercatorPosition;
            sig.mercatorPosition = PickPosition(playerMerc, minD, maxD);
            var newLatLon = Conversions.WebMercatorToLatitudeLongitude(sig.mercatorPosition);
            sig.latitude = newLatLon.Latitude;
            sig.longitude = newLatLon.Longitude;
            float oldDist = DistanceTo(new Signal { mercatorPosition = oldPos }, playerMerc);
            float newDist = DistanceTo(sig, playerMerc);
            Debug.Log($"[SignalDirector] Re-snapped {sig.role} {sig.id}: {oldDist:F0}m → {newDist:F0}m (road)");
        }
    }

    // ───────────────────────────────────────────────────────────
    // State machine per signal
    // ───────────────────────────────────────────────────────────

    private void AdvanceSignal(Signal sig, Vector2d playerMerc)
    {
        float dist = DistanceTo(sig, playerMerc);

        switch (sig.state)
        {
            case SignalState.Hidden:
                // Hidden signals become visible after a brief delay (1s)
                if (sig.TimeSinceStateChange >= 1f)
                    TransitionTo(sig, SignalState.Visible);
                break;

            case SignalState.Visible:
                // Primary and LocationTransmission can be pursued
                if ((sig.role == SignalRole.PrimaryPursuit || sig.role == SignalRole.LocationTransmission)
                    && dist <= pursuitRadius)
                    TransitionTo(sig, SignalState.Pursued);
                break;

            case SignalState.Pursued:
                lastPursuitTime = Time.time;
                if (dist <= lockRadius)
                    TransitionTo(sig, SignalState.Locked);
                // If player wanders far away, drop back to Visible
                if (dist > pursuitRadius * 1.5f)
                    TransitionTo(sig, SignalState.Visible);
                break;

            case SignalState.Locked:
                lastPursuitTime = Time.time;
                // Must dwell inside lock radius
                if (dist > lockRadius * 1.5f)
                {
                    TransitionTo(sig, SignalState.Pursued);
                }
                else if (sig.TimeSinceStateChange >= lockDwellSeconds)
                {
                    TransitionTo(sig, SignalState.ReadyToInterpret);
                }
                break;

            case SignalState.ReadyToInterpret:
                lastPursuitTime = Time.time;
                // Hold here until the player taps ENTER (OnEnterLocationTapped drives
                // the transition). If the player walks away, drop back to Pursued /
                // Visible like the other gated states do, so the beam isn't eternally
                // stuck waiting for input the player no longer cares about.
                if (dist > lockRadius * 1.5f)
                    TransitionTo(sig, SignalState.Pursued);
                break;

            case SignalState.Interpreting:
                lastPursuitTime = Time.time;
                if (sig.TimeSinceStateChange >= interpretDuration)
                    TransitionTo(sig, SignalState.Resolved);
                break;

            case SignalState.Resolved:
                // Chain a new primary if this was the primary (not for location transmissions)
                if (sig.role == SignalRole.PrimaryPursuit)
                    ChainNewPrimary(sig, playerMerc);
                TransitionTo(sig, SignalState.CoolingDown);
                break;

            case SignalState.CoolingDown:
                // Removal handled in Tick()
                break;
        }
    }

    public void TransitionTo(Signal sig, SignalState next)
    {
        sig.SetState(next);
        OnSignalStateChanged?.Invoke(sig);
    }

    // ───────────────────────────────────────────────────────────
    // Slot management
    // ───────────────────────────────────────────────────────────

    private Signal GetPrimary()
    {
        for (int i = 0; i < signals.Count; i++)
            if (signals[i].role == SignalRole.PrimaryPursuit &&
                signals[i].state != SignalState.CoolingDown)
                return signals[i];
        return null;
    }

    private int CountByRole(SignalRole role)
    {
        int c = 0;
        for (int i = 0; i < signals.Count; i++)
            if (signals[i].role == role && signals[i].state != SignalState.CoolingDown)
                c++;
        return c;
    }

    private void EnsurePrimary()
    {
        if (GetPrimary() != null) return;
        var playerMerc = GetPlayerMercator();
        SpawnSignal(SignalRole.PrimaryPursuit, SignalType.Presence, playerMerc,
                    primaryMinDist, primaryMaxDist, null);
    }

    private void FillSecondaries()
    {
        var playerMerc = GetPlayerMercator();
        while (CountByRole(SignalRole.SecondaryNearby) < maxSecondary)
        {
            SpawnSignal(SignalRole.SecondaryNearby, SignalType.Presence, playerMerc,
                        secondaryMinDist, secondaryMaxDist, null);
        }
    }

    private void FillDistant()
    {
        var playerMerc = GetPlayerMercator();
        while (CountByRole(SignalRole.DistantBackground) < maxDistant)
        {
            SpawnSignal(SignalRole.DistantBackground, SignalType.Presence, playerMerc,
                        distantMinDist, distantMaxDist, null);
        }
    }

    // ───────────────────────────────────────────────────────────
    // Spawning
    // ───────────────────────────────────────────────────────────

    private Signal SpawnSignal(SignalRole role, SignalType type,
                               Vector2d origin, float minDist, float maxDist,
                               string chainParentId)
    {
        Vector2d pos = PickPosition(origin, minDist, maxDist);
        var latLon = Conversions.WebMercatorToLatitudeLongitude(pos);

        var sig = new Signal
        {
            role = role,
            type = type,
            transmissionType = TransmissionType.Ambient,
            state = SignalState.Hidden,
            mercatorPosition = pos,
            latitude = latLon.Latitude,
            longitude = latLon.Longitude,
            spawnTime = Time.time,
            lastStateChange = Time.time,
            chainParentId = chainParentId,
            specialItem = PickSpecialItem()
        };

        signals.Add(sig);
        Debug.Log($"[SignalDirector] Spawned {role}/{type} @ dist={DistanceTo(sig, origin):F0}m  id={sig.id}");
        OnSignalSpawned?.Invoke(sig);
        return sig;
    }

    /// <summary>
    /// Pick a road point within [minDist, maxDist] of origin.
    /// Gathers all road points in that ring, picks one at random.
    /// Falls back to a random ring position if no roads are loaded yet.
    /// </summary>
    private Vector2d PickPosition(Vector2d origin, float minDist, float maxDist)
    {
        RefreshRoadCache();

        if (roadPoints != null && roadPoints.Count > 0)
        {
            // Collect all road points within the distance ring
            var candidates = new List<Vector2d>();
            for (int i = 0; i < roadPoints.Count; i++)
            {
                double dx = roadPoints[i].x - origin.x;
                double dy = roadPoints[i].y - origin.y;
                double d = Math.Sqrt(dx * dx + dy * dy);
                if (d >= minDist && d <= maxDist)
                    candidates.Add(roadPoints[i]);
            }

            if (candidates.Count > 0)
            {
                var chosen = candidates[UnityEngine.Random.Range(0, candidates.Count)];
                Debug.Log($"[SignalDirector] Snapped to road point ({candidates.Count} candidates in {minDist}-{maxDist}m ring)");
                return chosen;
            }

            // Widen search: find the nearest road point within 2x the max range
            Vector2d best = default;
            double bestDist = double.MaxValue;
            float widenMax = maxDist * 2f;
            for (int i = 0; i < roadPoints.Count; i++)
            {
                double dx = roadPoints[i].x - origin.x;
                double dy = roadPoints[i].y - origin.y;
                double d = Math.Sqrt(dx * dx + dy * dy);
                if (d >= minDist * 0.5f && d <= widenMax && d < bestDist)
                {
                    bestDist = d;
                    best = roadPoints[i];
                }
            }
            if (bestDist < double.MaxValue)
            {
                Debug.Log($"[SignalDirector] Widened snap: nearest road at {bestDist:F0}m (wanted {minDist}-{maxDist}m)");
                return best;
            }

            Debug.LogWarning($"[SignalDirector] No road points found in {minDist}-{widenMax}m ring ({roadPoints.Count} total road points)");
        }

        // Fallback: random ring position (no roads loaded yet)
        float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float dist  = UnityEngine.Random.Range(minDist, maxDist);
        Debug.LogWarning("[SignalDirector] No road data — using random position");
        return new Vector2d(
            origin.x + dist * Math.Cos(angle),
            origin.y + dist * Math.Sin(angle)
        );
    }

    // ───────────────────────────────────────────────────────────
    // Road geometry cache
    // ───────────────────────────────────────────────────────────

    private void RefreshRoadCache()
    {
        // Find road layer folder if not cached
        if (roadLayerFolder == null)
        {
            var runtimeRoot = GameObject.Find("RuntimeObjectsRoot");
            if (runtimeRoot != null)
                roadLayerFolder = runtimeRoot.transform.Find("road layer objects");
            if (roadLayerFolder == null)
            {
                var roadObj = GameObject.Find("road layer objects");
                if (roadObj != null) roadLayerFolder = roadObj.transform;
            }
            if (roadLayerFolder == null) return; // roads not loaded yet
        }

        // Rebuild cache when road mesh count changes
        MeshFilter[] meshes = roadLayerFolder.GetComponentsInChildren<MeshFilter>();
        if (roadPoints != null && meshes.Length == lastKnownRoadMeshCount) return;

        lastKnownRoadMeshCount = meshes.Length;
        roadPoints = new List<Vector2d>();
        int skipped = 0;

        foreach (MeshFilter mf in meshes)
        {
            if (mf.sharedMesh == null) continue;

            // Check road class via metadata (reflection to avoid compile dep)
            var meta = mf.gameObject.GetComponent("RoadSegmentMetadata");
            if (meta != null)
            {
                var field = meta.GetType().GetField("roadClass");
                if (field != null)
                {
                    string roadClass = field.GetValue(meta) as string;
                    if (!string.IsNullOrEmpty(roadClass) && !WalkableRoadClasses.Contains(roadClass))
                    {
                        skipped++;
                        continue;
                    }
                }
            }

            // Extract sidewalk positions from ribbon mesh vertex pairs.
            // ZossRoadStack generates pairs: v[0]=left, v[1]=right, v[2]=left, v[3]=right...
            // We compute the center of each pair and offset outward for sidewalk placement.
            Mesh mesh = mf.sharedMesh;
            Vector3[] verts = mesh.vertices;
            Transform tr = mf.transform;
            var centerLatLng = map.MapInformation.Position;
            var centerMerc = Conversions.LatitudeLongitudeToWebMercator(centerLatLng);

            for (int vi = 0; vi + 1 < verts.Length; vi += 2)
            {
                Vector3 leftWorld  = tr.TransformPoint(verts[vi]);
                Vector3 rightWorld = tr.TransformPoint(verts[vi + 1]);

                // Road center
                Vector3 center = (leftWorld + rightWorld) * 0.5f;

                // Perpendicular direction (from center toward right edge), in XZ plane
                Vector3 toRight = rightWorld - center;
                toRight.y = 0;
                float edgeDist = toRight.magnitude;
                if (edgeDist < 0.01f) continue; // degenerate

                Vector3 perpNorm = toRight / edgeDist;

                // Sidewalk point: road edge + offset outward
                Vector3 sidewalk = rightWorld + perpNorm * sidewalkOffset;

                roadPoints.Add(new Vector2d(
                    centerMerc.x + sidewalk.x,
                    centerMerc.y + sidewalk.z
                ));
            }
        }

        Debug.Log($"[SignalDirector] Road cache: {roadPoints.Count} walkable points from {meshes.Length} meshes ({skipped} non-walkable skipped)");
    }

    // ───────────────────────────────────────────────────────────
    // Chaining
    // ───────────────────────────────────────────────────────────

    private void ChainNewPrimary(Signal resolved, Vector2d playerMerc)
    {
        // Spawn chained primary slightly farther than the default range
        float chainMin = primaryMinDist * 1.2f;
        float chainMax = primaryMaxDist * 1.4f;

        var next = SpawnSignal(SignalRole.PrimaryPursuit, SignalType.Chain,
                               playerMerc, chainMin, chainMax, resolved.id);

        Debug.Log($"[SignalDirector] Chained {resolved.id} → {next.id}");
        OnPrimaryChained?.Invoke(resolved, next);
    }

    // ───────────────────────────────────────────────────────────
    // Churn (abandoned signals only)
    // ───────────────────────────────────────────────────────────

    private void HandleChurn(Vector2d playerMerc)
    {
        // Never churn while the player is actively pursuing
        if (IsInPursuit()) return;

        // Churn window: must have had no pursuit for churnWindowSeconds
        float timeSincePursuit = (lastPursuitTime < 0f)
            ? (Time.time - signals[0].spawnTime) // never pursued — measure from first spawn
            : (Time.time - lastPursuitTime);

        if (timeSincePursuit < churnWindowSeconds) return;

        // Rate-limit replacements
        if (Time.time - lastChurnTime < churnIntervalSeconds) return;

        // Find the oldest non-primary, non-pursued signal to churn
        Signal oldest = null;
        for (int i = 0; i < signals.Count; i++)
        {
            var s = signals[i];
            if (s.role == SignalRole.PrimaryPursuit) continue;
            if (s.state == SignalState.CoolingDown) continue;
            if (oldest == null || s.spawnTime < oldest.spawnTime)
                oldest = s;
        }

        if (oldest == null) return;

        Debug.Log($"[SignalDirector] Churning {oldest.role} {oldest.id} (age {oldest.Age:F0}s)");
        RemoveSignal(oldest);
        lastChurnTime = Time.time;

        // Also churn the primary so the player gets a fresh one
        var primary = GetPrimary();
        if (primary != null && primary.state == SignalState.Visible)
        {
            Debug.Log($"[SignalDirector] Churning stale primary {primary.id} (age {primary.Age:F0}s)");
            RemoveSignal(primary);
        }

        // EnsurePrimary / Fill will repopulate on next tick
    }

    private bool IsInPursuit()
    {
        var p = GetPrimary();
        if (p == null) return false;
        return p.state == SignalState.Pursued ||
               p.state == SignalState.Locked ||
               p.state == SignalState.ReadyToInterpret ||
               p.state == SignalState.Interpreting;
    }

    // ───────────────────────────────────────────────────────────
    // Removal
    // ───────────────────────────────────────────────────────────

    private void RemoveSignal(Signal sig)
    {
        signals.Remove(sig);
        Debug.Log($"[SignalDirector] Removed {sig.role} {sig.id}");
        OnSignalRemoved?.Invoke(sig);
    }

    // ───────────────────────────────────────────────────────────
    // Coordinate helpers (same convention as VirtualGridSpawner)
    // ───────────────────────────────────────────────────────────

    private Vector2d GetPlayerMercator()
    {
        if (map == null || map.MapInformation == null) return new Vector2d(0, 0);
        var centerLatLng = map.MapInformation.Position;
        var centerMerc = Conversions.LatitudeLongitudeToWebMercator(centerLatLng);
        Vector3 p = playerObj.transform.position;
        return new Vector2d(centerMerc.x + p.x, centerMerc.y + p.z);
    }

    /// <summary>
    /// Convert a signal's Mercator position to Unity world space.
    /// Call every frame for floating-origin correctness.
    /// </summary>
    public Vector3 SignalToWorldPos(Signal sig)
    {
        if (map == null || map.MapInformation == null) return Vector3.zero;
        var centerLatLng = map.MapInformation.Position;
        var centerMerc = Conversions.LatitudeLongitudeToWebMercator(centerLatLng);
        Vector3 pos = new Vector3(
            (float)(sig.mercatorPosition.x - centerMerc.x),
            1f, // spawn height above ground
            (float)(sig.mercatorPosition.y - centerMerc.y)
        );
        // Account for floating-origin map transform offset (same as location beams)
        pos += map.transform.position;
        return pos;
    }

    private float DistanceTo(Signal sig, Vector2d mercPos)
    {
        double dx = sig.mercatorPosition.x - mercPos.x;
        double dy = sig.mercatorPosition.y - mercPos.y;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// Find the nearest building to a location signal and cache its XZ bounds.
    /// Returns true if the player's world position is inside the building footprint.
    /// </summary>
    private bool IsPlayerInsideLocationBuilding(Signal loc, Vector3 playerWorldPos)
    {
        // Refresh building bounds every 5s (buildings shift with floating origin)
        if (loc.buildingBounds == null || Time.time - loc.buildingBoundsTime > 5f)
        {
            loc.buildingBounds = FindBuildingBoundsNear(loc);
            loc.buildingBoundsTime = Time.time;
        }

        if (loc.buildingBounds == null) return false;

        Bounds b = loc.buildingBounds.Value;
        // Check XZ overlap (ignore Y height) — player is "inside" the building footprint
        return playerWorldPos.x >= b.min.x && playerWorldPos.x <= b.max.x
            && playerWorldPos.z >= b.min.z && playerWorldPos.z <= b.max.z;
    }

    private Bounds? FindBuildingBoundsNear(Signal loc)
    {
        Vector3 signalWorld = SignalToWorldPos(loc);
        float bestDist = 40f; // max search radius in world units
        Bounds? bestBounds = null;

        // Search all buildings with renderers
        var allMeta = FindObjectsByType<BuildingMetadata>(FindObjectsSortMode.None);
        for (int i = 0; i < allMeta.Length; i++)
        {
            var mr = allMeta[i].GetComponent<MeshRenderer>();
            if (mr == null) continue;

            Bounds b = mr.bounds;
            // Check XZ distance from signal to building center
            float dx = signalWorld.x - b.center.x;
            float dz = signalWorld.z - b.center.z;
            float d = Mathf.Sqrt(dx * dx + dz * dz);

            // Also check if signal point is inside building bounds (best match)
            bool inside = signalWorld.x >= b.min.x && signalWorld.x <= b.max.x
                       && signalWorld.z >= b.min.z && signalWorld.z <= b.max.z;

            if (inside)
            {
                // Signal is literally inside this building — strong match
                Debug.Log($"[SignalDirector] Building match for '{loc.locationName}': '{allMeta[i].buildingName}' (signal inside bounds)");
                return b;
            }

            if (d < bestDist)
            {
                bestDist = d;
                bestBounds = b;
            }
        }

        if (bestBounds != null)
            Debug.Log($"[SignalDirector] Building match for '{loc.locationName}': nearest at {bestDist:F1}m");
        return bestBounds;
    }

    /// <summary>Absolute bearing (0=N, 90=E) from player to signal in degrees.</summary>
    private float BearingTo(Signal sig, Vector2d playerMerc)
    {
        double dx = sig.mercatorPosition.x - playerMerc.x;
        double dy = sig.mercatorPosition.y - playerMerc.y;
        float angle = (float)(Math.Atan2(dx, dy) * (180.0 / Math.PI));
        return (angle + 360f) % 360f;
    }

    /// <summary>Relative compass direction from device heading to signal (ASCII-safe).</summary>
    private string CompassTo(Signal sig, Vector2d playerMerc)
    {
        float absBearing = BearingTo(sig, playerMerc);
        float heading = Input.compass.enabled ? Input.compass.trueHeading : 0f;
        float rel = (absBearing - heading + 360f) % 360f;
        if (rel <= 22.5f || rel > 337.5f) return "N ^";
        if (rel <= 67.5f)  return "NE />";
        if (rel <= 112.5f) return "E >>";
        if (rel <= 157.5f) return "SE \\>";
        if (rel <= 202.5f) return "S v";
        if (rel <= 247.5f) return "SW <\\";
        if (rel <= 292.5f) return "W <<";
        return "NW </";
    }

    /// <summary>Relative angle from device heading to signal (0=ahead, clockwise). Used for arrow rotation.</summary>
    private float RelativeAngleTo(Signal sig, Vector2d playerMerc)
    {
        float absBearing = BearingTo(sig, playerMerc);
        float heading = Input.compass.enabled ? Input.compass.trueHeading : 0f;
        return (absBearing - heading + 360f) % 360f;
    }

    /// <summary>Heavy black drop-shadow on TMP text via underlay material keyword — readable on any background.</summary>
    private void ApplyHeavyShadow(TextMeshProUGUI tmp)
    {
        if (tmp == null) return;
        var mat = tmp.fontMaterial; // instance (safe to mutate)
        mat.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        mat.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 1f));
        mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.7f);
        mat.SetFloat(ShaderUtilities.ID_UnderlayDilate, 1f);
        mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
        mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, 0f);
    }

    private TextMeshProUGUI CreateHUDTextLayer(Transform parent, string name, TMP_FontAsset font, float fontSize, Vector2 leftOffset)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.offsetMin = new Vector2(leftOffset.x, leftOffset.y);
        rt.offsetMax = Vector2.zero;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.fontSize = fontSize;
        tmp.text = "";
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.raycastTarget = false;
        tmp.enableWordWrapping = true;
        tmp.richText = true;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        return tmp;
    }

    private GameObject CreateCompassWidget(Transform parent, string name, out Image ringImg, out RectTransform arrowRt, out TextMeshProUGUI distanceTmp)
    {
        float compassSize = 42f;

        // Container
        var container = new GameObject(name);
        container.transform.SetParent(parent, false);
        var cRt = container.AddComponent<RectTransform>();
        cRt.anchorMin = new Vector2(0f, 0.5f);
        cRt.anchorMax = new Vector2(0f, 0.5f);
        cRt.pivot = new Vector2(0f, 0.5f);
        cRt.anchoredPosition = Vector2.zero;
        cRt.sizeDelta = new Vector2(compassSize, compassSize);

        // Black filled circle background
        var bgGO = new GameObject("CompassBg");
        bgGO.transform.SetParent(container.transform, false);
        var bgRt = bgGO.AddComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        var bgImg = bgGO.AddComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 1f);
        bgImg.raycastTarget = false;
        CreateFilledCircleSprite(bgImg);

        // Ring (circle outline)
        var ringGO = new GameObject("Ring");
        ringGO.transform.SetParent(container.transform, false);
        var ringRt = ringGO.AddComponent<RectTransform>();
        ringRt.anchorMin = Vector2.zero;
        ringRt.anchorMax = Vector2.one;
        ringRt.offsetMin = Vector2.zero;
        ringRt.offsetMax = Vector2.zero;
        ringImg = ringGO.AddComponent<Image>();
        ringImg.color = new Color(0.47f, 1f, 0.54f, 0.6f);
        ringImg.raycastTarget = false;
        CreateRingSprite(ringImg);

        // Arrow inside the ring
        var arrowGO = new GameObject("Arrow");
        arrowGO.transform.SetParent(container.transform, false);
        arrowRt = arrowGO.AddComponent<RectTransform>();
        arrowRt.anchorMin = new Vector2(0.5f, 0.5f);
        arrowRt.anchorMax = new Vector2(0.5f, 0.5f);
        arrowRt.pivot = new Vector2(0.5f, 0.5f);
        arrowRt.anchoredPosition = Vector2.zero;
        arrowRt.sizeDelta = new Vector2(compassSize * 0.7f, compassSize * 0.7f);
        var arrowImg = arrowGO.AddComponent<Image>();
        arrowImg.color = new Color(0.47f, 1f, 0.54f, 0.95f);
        arrowImg.raycastTarget = false;
        CreateArrowSprite(arrowImg);

        // Distance label — sits directly under the compass circle
        var distGO = new GameObject("Distance");
        distGO.transform.SetParent(container.transform, false);
        var distRt = distGO.AddComponent<RectTransform>();
        distRt.anchorMin = new Vector2(0.5f, 0f);
        distRt.anchorMax = new Vector2(0.5f, 0f);
        distRt.pivot = new Vector2(0.5f, 1f);
        distRt.anchoredPosition = new Vector2(0f, -2f);
        distRt.sizeDelta = new Vector2(104f, 18f);
        var distFont = Resources.Load<TMP_FontAsset>("Fonts/IBMPlexMono-Regular SDF");
        if (distFont == null) distFont = TMP_Settings.defaultFontAsset;
        distanceTmp = distGO.AddComponent<TextMeshProUGUI>();
        distanceTmp.font = distFont;
        distanceTmp.fontSize = 13;
        distanceTmp.text = "";
        distanceTmp.alignment = TextAlignmentOptions.Top;
        distanceTmp.color = Color.white;
        distanceTmp.raycastTarget = false;
        ApplyHeavyShadow(distanceTmp);

        return container;
    }

    private void CreateRingSprite(Image img)
    {
        // Procedural circle outline
        int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(0, 0, 0, 0);

        float center = size / 2f;
        float outerR = center - 1f;
        float innerR = outerR - 2.5f; // 2.5px ring thickness

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d >= innerR && d <= outerR)
                    pixels[y * size + x] = new Color32(255, 255, 255, 255);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        img.sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private void CreateFilledCircleSprite(Image img)
    {
        int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(0, 0, 0, 0);

        float center = size / 2f;
        float radius = center - 1f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                if (dx * dx + dy * dy <= radius * radius)
                    pixels[y * size + x] = new Color32(255, 255, 255, 255);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        img.sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private void CreateArrowSprite(Image img)
    {
        // Procedural arrow: shaft + arrowhead pointing up
        int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(0, 0, 0, 0);

        int cx = size / 2;
        var white = new Color32(255, 255, 255, 255);

        // Shaft: vertical line from y=2 to y=20, 2px wide
        for (int y = 2; y <= 20; y++)
        {
            for (int x = cx - 1; x <= cx; x++)
                if (x >= 0 && x < size) pixels[y * size + x] = white;
        }

        // Arrowhead: triangle from y=18 to y=30
        for (int y = 18; y < size; y++)
        {
            float t = (float)(y - 18) / (size - 1 - 18); // 0 at base, 1 at tip
            float halfWidth = (1f - t) * 7f; // 7px half-width at base
            int hw = Mathf.Max(0, (int)halfWidth);
            for (int x = cx - hw; x <= cx + hw; x++)
                if (x >= 0 && x < size) pixels[y * size + x] = white;
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        img.sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    // ───────────────────────────────────────────────────────────
    // Public API (for external systems)
    // ───────────────────────────────────────────────────────────

    /// <summary>Get the current primary pursuit signal, or null.</summary>
    public Signal GetCurrentPrimary() => GetPrimary();

    /// <summary>
    /// Externally trigger interpretation on the primary (e.g. from a tap).
    /// Returns true if the signal was in a valid state to interpret.
    /// </summary>
    public bool TryInterpretPrimary()
    {
        var p = GetPrimary();
        if (p == null) return false;
        if (p.state == SignalState.ReadyToInterpret)
        {
            TransitionTo(p, SignalState.Interpreting);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Force-resolve the primary (debug / testing).
    /// </summary>
    public void DebugResolvePrimary()
    {
        var p = GetPrimary();
        if (p == null) return;
        TransitionTo(p, SignalState.Resolved);
    }

    /// <summary>
    /// Get all signals of a given role.
    /// </summary>
    public List<Signal> GetSignalsByRole(SignalRole role)
    {
        var result = new List<Signal>();
        for (int i = 0; i < signals.Count; i++)
            if (signals[i].role == role)
                result.Add(signals[i]);
        return result;
    }

    /// <summary>
    /// Spawn a LocationTransmission signal at a known GPS position.
    /// Returns the signal, or null if one already exists.
    /// </summary>
    public Signal SpawnLocationTransmission(double latitude, double longitude,
                                            string name, string category, string teaser,
                                            string specialItem = null)
    {
        // Only one location transmission at a time
        for (int i = 0; i < signals.Count; i++)
            if (signals[i].role == SignalRole.LocationTransmission &&
                signals[i].state != SignalState.CoolingDown)
                return null;

        var merc = Conversions.LatitudeLongitudeToWebMercator(
            new LatitudeLongitude(latitude, longitude));

        var sig = new Signal
        {
            role = SignalRole.LocationTransmission,
            type = SignalType.Presence,
            transmissionType = TransmissionType.Location,
            state = SignalState.Hidden,
            mercatorPosition = merc,
            latitude = latitude,
            longitude = longitude,
            spawnTime = Time.time,
            lastStateChange = Time.time,
            locationName = name,
            locationCategory = category,
            teaser = teaser,
            specialItem = specialItem ?? PickSpecialItem()
        };

        signals.Add(sig);
        Debug.Log($"[SignalDirector] Spawned LocationTransmission '{name}' id={sig.id}");
        OnSignalSpawned?.Invoke(sig);
        return sig;
    }

    /// <summary>Get the current location transmission signal, or null.</summary>
    public Signal GetLocationTransmission()
    {
        for (int i = 0; i < signals.Count; i++)
            if (signals[i].role == SignalRole.LocationTransmission &&
                signals[i].state != SignalState.CoolingDown)
                return signals[i];
        return null;
    }

    /// <summary>Remove the current location transmission signal.</summary>
    public void RemoveLocationTransmission()
    {
        var loc = GetLocationTransmission();
        if (loc != null) RemoveSignal(loc);
    }

    /// <summary>Distance from player to a signal, in meters.</summary>
    public float DistanceToSignal(Signal sig)
    {
        return DistanceTo(sig, GetPlayerMercator());
    }
}
