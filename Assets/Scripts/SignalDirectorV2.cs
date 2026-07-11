using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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
// Visible portal taxonomy is now two user-facing types:
//
//   TransmissionType.Location
//     — Bound to a real POI on the map (SignalRole.LocationTransmission)
//     — Shown on the LocationRow of the HUD
//     — Survives in the world until the 20-min refresh or ENTER/cooldown
//
//   TransmissionType.Artifact
//     — Runtime representation for ambient portals.
//
// Transmitter is retained for legacy/API compatibility only. Transmitting is
// user-initiated from the transmitter UI, not a visible spawned beam category.
public enum TransmissionType { Location, Artifact, Transmitter }

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
    public string artifactContainer; // backend-authored container detail for artifact beams
    public string teaser;            // the HUD sentence (backend-authored or locally generated)

    // Location-only metadata (null on Artifact / Transmitter transmissions)
    public string locationName;      // e.g. "Recon Brewing at Meeder"
    public string locationCategory;  // e.g. "brewery", "bar", "coffee_shop"
    // Stable key for external sources (e.g., POI name). Used to de-dup multi-location beams.
    public string externalKey;

    // Optional ring-slot index used by the ambient pool spawner (1..N). -1 when not used.
    public int poolRingIndex;

    // Runtime visual handle (set externally by whatever renders beams)
    [NonSerialized] public GameObject visualGO;

    // Cached building bounds for ENTER proximity (set by SignalDirectorV2)
    [NonSerialized] public Bounds? buildingBounds;
    [NonSerialized] public float buildingBoundsTime; // Time.time when cached

    public Signal()
    {
        id = Guid.NewGuid().ToString("N").Substring(0, 8);
        pursuitStartTime = -1f;
        poolRingIndex = -1;
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

    [Header("Ambient Pool (Rings)")]
    [Tooltip("If enabled, spawns a persistent pool of artifact/transmitter beams in 75m rings around the player.")]
    public bool useConcentricAmbientPool = true;
    [Tooltip("Ring spacing in meters.")]
    public float ambientRingStepMeters = 75f;
    [Tooltip("Remove and stop spawning ambient beams beyond this many miles.")]
    public float ambientPoolMaxMiles = 1.1f;
    [Tooltip("Hard cap on the number of ambient ring beams shown at once. Set high (24+) to render every beam the backend returns; 1 = only the single nearest disturbance.")]
    public int ambientRingMaxCount = 24;
    [Range(0f, 1f)]
    [Tooltip("Chance each ambient beam is an Artifact (else Transmitter). Ignored when alternating rings is enabled.")]
    public float ambientArtifactChance = 0.5f;
    [Tooltip("If true, ring 1 is Artifact (closest), ring 2 is Transmitter, alternating outward.")]
    public bool ambientAlternateArtifactTransmitter = true;

    [Header("Location Beams (POI)")]
    [Tooltip("Show purple beams for nearby locations within this many miles (from TransmitterScanner).")]
    public float locationBeamMaxMiles = 1.1f;
    [Tooltip("Max number of location beams to show at once.")]
    public int maxLocationBeams = 20;

    [Header("Ambient Pool (Backend)")]
    [Tooltip("If enabled, non-location beams come from the backend shared Firestore pool (no fake local beams).")]
    public bool useBackendConcentricBeams = true;
    [Tooltip("Native Swift owns world/nearby. Unity mirrors pushed ambient beams instead of polling the backend.")]
    public bool useNativeDrivenAmbientBeams = true;
    [Tooltip("How often to refresh ring beams from the backend (seconds).")]
    public float backendBeamRefreshSeconds = 5f;
    [Tooltip("Only rescan beams if the player moved at least this many meters since the last scan.")]
    public float backendBeamRescanMeters = 50f;
    [Tooltip("Failsafe: rescan at least this often even if movement threshold isn't crossed.")]
    public float backendBeamMaxIntervalSeconds = 120f;
    [Tooltip("Do not show or request ambient portals until kilosync is active.")]
    public bool requireKilosyncForAmbientPortals = true;
    [Tooltip("Minimum active walking-session steps required before a new disturbance spawns.")]
    public int ambientMinStepsToSpawn = 110;
    [Tooltip("Step threshold below which a walking bucket is considered inactive — i.e. the user is no longer walking briskly. Drives pedometer reset.")]
    [Range(10, 500)] public int momentumGraceSteps = 50;
    [Tooltip("Minutes before newly spawned ambient portals expire in Firestore.")]
    [Range(1f, 240f)] public float ambientBeamTtlMinutes = 30f;
    [Tooltip("Meters from an ambient transmission portal required before Enter Portal can collect it.")]
    [Range(1f, 100f)] public float ambientCollectRadiusMeters = 16f;
    [Tooltip("Optional clip played when new ambient portals appear.")]
    public AudioClip ambientPortalSpawnClip;
    [Range(0f, 1f)]
    public float ambientPortalSpawnVolume = 0.8f;
    public float ambientPortalSpawnSoundCooldown = 0.35f;
    private float _lastBackendBeamRefreshTime = -999f;
    private bool _backendBeamRequestInFlight = false;
    private double _lastBackendScanLat = double.NaN;
    private double _lastBackendScanLng = double.NaN;
    private float _lastBackendScanTime = -999f;
    private Vector2d _lastMovementBearingMercator;
    private bool _hasMovementBearingSample = false;
    private bool _hasMovementBearing = false;
    private float _lastMovementBearingDegrees = 0f;
    private float _lastAmbientPortalSpawnSoundTime = -999f;
    private bool _ambientPortalsWereAllowed = false;
    private readonly HashSet<string> visitedAmbientBeamIds = new HashSet<string>();
    private float _nextNativeEnvironmentPushTime = -999f;
    private bool _sentNativeEnvironmentState = false;
    private bool _lastNativeInsideBuilding = false;

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void K1L0SetEnvironmentState(string json);
#endif

    [Serializable]
    private class BackendBeamDoc
    {
        public string id;
        public int ringIndex;
        public string type; // "artifact" | "transmitter"
        public double lat;
        public double lng;
        public string label;
        public string material;
        public string container;
        public string senderName;
        public string artifactSenderName;
        public string lore;
        public double distanceMeters;
    }

    [Serializable]
    private class BackendNearbyBeamsResponse
    {
        public bool ok;
        public bool includeBeams;
        public float maxMiles;
        public BackendBeamDoc[] beams;
        public bool fillPending;
    }

    private int ComputeAmbientRingIndex(float distanceMeters, float stepMeters, int ringCount)
    {
        if (stepMeters <= 0f || distanceMeters < 0f) return -1;
        int ringIndex = Mathf.Max(1, Mathf.RoundToInt(distanceMeters / stepMeters));
        if (ringIndex < 1 || ringIndex > ringCount) return -1;
        return ringIndex;
    }

    [Serializable]
    private class BackendFillMissingResponse
    {
        public bool ok;
        public int created;
        public BackendBeamDoc[] beams;
    }

    private float lastLocationSyncTime;

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
    private bool loggedAmbientPoolConfig;

    // ── On-screen debug overlay ───────────────────────────────
    private TextMeshProUGUI debugText;
    private TextMeshProUGUI beamAuditText;
    private float nextBeamAuditTime = 0f;
    private string lastBeamAuditLine = "";
    private string lastSettingsBeamDebugText = "RING DEBUG\nPORTAL AUDIT: waiting...";
    private RectTransform storiesStripRect;
    private const float TeaserRowLeftInset = 12f;
    private const float TeaserRowHeight = 24f;
    private const float TeaserRowGap = 2f;
    private const float DisturbanceCompassInset = 16f;
    private const float DisturbanceLabelInset = 100f;
    private const float MainActionHeight = 46f;
    private const float MainActionSubtextHeight = 34f;
    private static readonly Color TeaserGreen = new Color(0.47f, 1f, 0.54f, 1f);
    private static readonly Color TeaserRed = new Color(1f, 0.18f, 0.15f, 1f);
    private static readonly string[] FallbackSenderNames =
    {
        "Mara", "Theo", "June", "Cass", "Iris", "Vale", "Nico", "Orla",
        "Milo", "Zara", "Lena", "Otis", "Sable", "Remy", "Vera", "Jules"
    };
    private const float TeaserBelowStoriesGap = 8f;
    private const float DefaultStoriesBottomFromTop = 196f;
    [SerializeField] private bool showMapTeaserRows = false;

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
    private GameObject pursuitRowBorder;       // red 4-strip frame, visible only in ENTER mode
    private UnityEngine.UI.Button pursuitRowButton; // tap target in ENTER mode
	    private string pursuitLabelOverride;
	    private int pursuitLabelNormalFontSize = 12;
	    private int pursuitLabelEnterFontSize = 44;
	    private Vector2 pursuitLabelNormalOffset = new Vector2(34, 0);

	    // ── Artifact Transmission HUD (second beam row) ───────────
	    // Always shows the nearest artifact transmission so the player can
	    // find/collect an artifact even when the primary beam is a transmitter.
	    private TextMeshProUGUI artifactLabel;
	    private TextMeshProUGUI artifactDist;
	    private RectTransform artifactArrowRt;
	    private Image artifactCompassRing;
	    private GameObject artifactCompassGO;
    private bool showLegacyArtifactHud = false;
	    private GameObject artifactRow;
	    private Image artifactRowBg;
	    private GameObject artifactRowBorder;
	    private UnityEngine.UI.Button artifactRowButton;
	    private int artifactLabelNormalFontSize = 12;
	    private int artifactLabelEnterFontSize = 44;
	    private Vector2 artifactLabelNormalOffset = new Vector2(34, 0);

	    // ── Location Transmission HUD ─────────────────────────────
	    private TextMeshProUGUI locLabel;         // green foreground (blinks)
    private TextMeshProUGUI locDist;           // distance label under compass
    private RectTransform locArrowRt;
    private Image locCompassRing;
    private GameObject locCompassGO;           // compass container, hidden when row becomes ENTER button
    private GameObject locRow;
    private GameObject locPanel;
    private Image locRowBg;                    // darker backing revealed when row is in ENTER mode
    private GameObject locRowBorder;           // red 4-strip frame, visible only in ENTER mode
    private UnityEngine.UI.Button locRowButton; // tap target when row is ENTER mode
    private int locLabelNormalFontSize = 12;
    private int locLabelEnterFontSize = 44;
    private Vector2 locLabelNormalOffset = new Vector2(34, 0);
    private Vector2 locLabelEnterOffset = Vector2.zero;
    private TextMeshProUGUI locEnterText;
    private TextMeshProUGUI locEnterSubtext;
    private GameObject locEnterGO;
    private GameObject locEnterBorder;
    private Image locEnterButtonBg;
    private UnityEngine.UI.Button locEnterButton;
    private Text dailyStepsLabel;
    private TextMeshProUGUI weeklyStepsLabel;
    private GameObject stepsWidgetRoot;
    private Text stepsHeroLabel;
    private static Font cleanSansStepsUiFont;
    private TextMeshProUGUI stepsMetaLabel;
    private TextMeshProUGUI stepsCtaLabel;
    private TextMeshProUGUI stepsDistanceLabel;
    private GameObject stepsCompassRow;
    private GameObject stepsCompassGO;
    private RectTransform stepsCompassArrowRt;
    private Image stepsCompassRing;
    private PedometerService pedometerService;
    private struct MomentumSample
    {
        public float time;
        public int steps;
        public Vector2d mercator;
        public float pedometerDistanceMeters;
    }
    private readonly List<MomentumSample> momentumSamples = new List<MomentumSample>();
    private float nextMomentumSampleTime;
    private float walkingMomentum;
    private bool walkingMomentumReady;
    private bool isWalkingWithMomentum;
    private float lastMetersPerStep;
    private float lastStepsPerMinute;
    // Sub-scores from the last momentum calc, kept for the readable "why low" panel.
    private float lastCadenceScore;
    private float lastStrideScore;
    private float lastDisplacementScore;
    private float lastMetersMoved;
    private float lastWindowMinutes;
    private int motionResetBaseSteps = -1;
    private int activeMomentumSteps = 0;
    private bool wasWalkingWithMomentumForStreak = false;
    private float lastWalkingMomentumTime = -999f;
    private const float MomentumSampleIntervalSeconds = 5f;
    private const float MomentumWindowSeconds = 180f;
    private const float MomentumWalkingThreshold = 0.35f;
    // Walking-momentum time grace stays fixed at 90s now that the user-facing
    // slider controls a step threshold instead of a minutes value.
    private const float MomentumSessionGraceSeconds = 90f;
    private float nextInsideBuildingCheckTime;
    private bool cachedInsideBuilding;
    private bool loggedAmbientBlockedByKilosync;
    private float nextAmbientBlockedLogTime;
    private bool enterOverlayPointerDown;
    private int lastEnterOverlayFrame = -1;
    private float locEnterFirstShownTime = -1f;   // when ENTER first became eligible
    private Signal locEnterStickySignal;           // signal ENTER is sticking to
    private const float ENTER_MIN_VISIBLE_SECONDS = 120f; // keep ENTER on for at least 2 min
    // ENTER radius is type-specific:
    // - Ambient transmission beams use ambientCollectRadiusMeters.
    // - Location beams: <=10m OR within 10m of the containing building footprint
    private const float ENTER_PROXIMITY_LOCATION_METERS = 10f;  // Location point distance
    private const float ENTER_PROXIMITY_BUILDING_EDGE_METERS = 10f; // Distance-to-footprint allowance
    private const float ENTER_HIDE_DISTANCE_METERS = 20f; // hide ENTER after moving ~20m away
    private Signal enterCandidate;                 // the signal the ENTER button currently targets
    private float AmbientCollectRadiusMeters => Mathf.Clamp(ambientCollectRadiusMeters, 1f, 100f);

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
        if (pursuitRow != null) pursuitRow.SetActive(MapTeaserRowsVisible);
        if (artifactRow != null) artifactRow.SetActive(!suppress);
        if (locRow != null) locRow.SetActive(MapTeaserRowsVisible);
        if (locEnterGO != null) locEnterGO.SetActive(!suppress);
        if (stepsWidgetRoot != null) stepsWidgetRoot.SetActive(!suppress);
        else if (dailyStepsLabel != null) dailyStepsLabel.gameObject.SetActive(!suppress);
    }

    private bool MapTeaserRowsVisible => showMapTeaserRows && !hudSuppressed;

    public string GetNearbyTeaserText()
    {
        return $"> {TeaserLineOrDefault(artifactLabel, "scanning ambient locations...")}";
    }

    public struct NearbyTeaserInfo
    {
        public bool hasSignal;
        public string title;
        public string distanceText;
        public float relativeAngle;
        public string scanningText;
    }

    public NearbyTeaserInfo[] GetNearbyTeaserInfos()
    {
        var playerMerc = GetPlayerMercator();
        Signal ambient = GetNearestAmbientSignal(playerMerc);

        return new[]
        {
            BuildNearbyTeaserInfo(ambient, "scanning ambient locations...", TransmissionType.Artifact, playerMerc),
        };
    }

    private NearbyTeaserInfo BuildNearbyTeaserInfo(Signal signal, string scanningText, TransmissionType type, Vector2d playerMerc)
    {
        if (signal == null)
        {
            return new NearbyTeaserInfo
            {
                hasSignal = false,
                title = scanningText,
                distanceText = "",
                relativeAngle = 0f,
                scanningText = scanningText
            };
        }

        string title;
        if (type == TransmissionType.Artifact || type == TransmissionType.Transmitter)
        {
            // Don't reveal the material/sender atop locations — just signal a disturbance.
            title = "NEARBY DISTURBANCE";
        }
        else
        {
            title = !string.IsNullOrEmpty(signal.locationName) ? signal.locationName : "location";
        }

        float distance = DistanceTo(signal, playerMerc);
        return new NearbyTeaserInfo
        {
            hasSignal = true,
            title = title.ToUpperInvariant(),
            distanceText = FormatTeaserDistancePlain(distance),
            relativeAngle = RelativeAngleTo(signal, playerMerc),
            scanningText = scanningText
        };
    }

    private static string TeaserLineOrDefault(TextMeshProUGUI label, string fallback)
    {
        if (label == null || string.IsNullOrWhiteSpace(label.text)) return fallback;
        return label.text.Trim();
    }

    private static string GetSignalSenderName(Signal signal)
    {
        string named = FirstNameOrNull(signal != null ? signal.character : null);
        if (!string.IsNullOrWhiteSpace(named)) return named;
        string seed = signal != null && !string.IsNullOrWhiteSpace(signal.id) ? signal.id : "k1l0";
        int index = Mathf.Abs(seed.GetHashCode()) % FallbackSenderNames.Length;
        return FallbackSenderNames[index];
    }

    private static string FormatTeaserDistance(float meters)
    {
        return $"<color=#FFFFFF>{FormatTeaserDistancePlain(meters)}</color>";
    }

    private static string FormatTeaserDistancePlain(float meters)
    {
        float miles = meters / 1609.34f;
        if (miles < 0.33f)
            return $"{Mathf.RoundToInt(meters * 3.28084f)}ft";
        return $"{miles:F1}mi";
    }

    private Signal GetNearestSignalByType(TransmissionType transmissionType, Vector2d playerMerc)
    {
        Signal best = null;
        float bestDist = float.MaxValue;
        for (int i = 0; i < signals.Count; i++)
        {
            var s = signals[i];
            if (s == null) continue;
            if (s.role == SignalRole.LocationTransmission) continue;
            if (s.state == SignalState.CoolingDown) continue;
            if (s.transmissionType != transmissionType) continue;

            float d = DistanceTo(s, playerMerc);
            if (d < bestDist)
            {
                bestDist = d;
                best = s;
            }
        }
        return best;
    }

    private Signal GetNearestAmbientSignal(Vector2d playerMerc)
    {
        Signal best = null;
        float bestDist = float.MaxValue;
        for (int i = 0; i < signals.Count; i++)
        {
            var s = signals[i];
            if (s == null) continue;
            if (s.role == SignalRole.LocationTransmission) continue;
            if (s.transmissionType == TransmissionType.Location) continue;
            if (s.state == SignalState.CoolingDown) continue;
            if (s.state == SignalState.Interpreting || s.state == SignalState.Resolved) continue;

            float d = DistanceTo(s, playerMerc);
            if (d < bestDist)
            {
                bestDist = d;
                best = s;
            }
        }
        return best;
    }

    // ──────────────────────────────────────────────────────────────
    // Transmission-type helpers — see the TransmissionType enum at the
    // top of this file. Reads `sig.transmissionType` directly; the field
    // is set at spawn time and is the source of truth.
    // ──────────────────────────────────────────────────────────────
    public static TransmissionType TypeOf(Signal sig)
        => sig != null ? sig.transmissionType : TransmissionType.Artifact;

    public static bool IsLocationTransmission(Signal sig)
        => sig != null && sig.transmissionType == TransmissionType.Location;

    public static bool IsArtifactTransmission(Signal sig)
        => sig != null && sig.transmissionType == TransmissionType.Artifact;

    public static bool IsTransmitterTransmission(Signal sig)
        => sig != null && sig.transmissionType == TransmissionType.Transmitter;

    /// <summary>Sugar for "anything that is not a location transmission" (Artifact OR Transmitter).</summary>
    public static bool IsAmbientTransmission(Signal sig)
        => sig != null && sig.transmissionType != TransmissionType.Location;

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

        Debug.Log("[SignalDirector] Auto-bootstrapped SignalDirectorV2 + SignalBeamBridge");
    }

    // ───────────────────────────────────────────────────────────
    // Unity lifecycle
    // ───────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Game rule: concentric pool uses 75m spacing.
        // Serialized scene values can drift; enforce at runtime to avoid showing too many rings at once.
        ambientRingStepMeters = 75f;
        LoadMomentumGatePrefs();

        Input.compass.enabled = true;
        Input.location.Start();
    }

    void Start()
    {
        StartCoroutine(LoadSfxConfig());
    }

    private void LoadMomentumGatePrefs()
    {
        if (PlayerPrefs.HasKey("k1lo_ambientMinStepsToSpawn"))
            ambientMinStepsToSpawn = Mathf.RoundToInt(PlayerPrefs.GetFloat("k1lo_ambientMinStepsToSpawn"));
        if (PlayerPrefs.HasKey("k1lo_momentumGraceSteps"))
            momentumGraceSteps = PlayerPrefs.GetInt("k1lo_momentumGraceSteps");
        if (PlayerPrefs.HasKey("k1lo_ambientBeamTtlMinutes"))
            ambientBeamTtlMinutes = PlayerPrefs.GetFloat("k1lo_ambientBeamTtlMinutes");
        else if (PlayerPrefs.HasKey("k1lo_ambientBeamTtlHours"))
            ambientBeamTtlMinutes = PlayerPrefs.GetFloat("k1lo_ambientBeamTtlHours") * 60f;
        if (PlayerPrefs.HasKey("k1lo_ambientCollectRadiusMeters"))
            ambientCollectRadiusMeters = PlayerPrefs.GetFloat("k1lo_ambientCollectRadiusMeters");
        ambientMinStepsToSpawn = Mathf.Clamp(ambientMinStepsToSpawn, 0, 2000);
        momentumGraceSteps = Mathf.Clamp(momentumGraceSteps, 10, 500);
        ambientBeamTtlMinutes = Mathf.Clamp(ambientBeamTtlMinutes, 1f, 240f);
        ambientCollectRadiusMeters = Mathf.Clamp(ambientCollectRadiusMeters, 1f, 100f);
        // Render every ambient beam the backend returns — clamp only if a
        // serialized scene value pushed it absurdly low.
        if (ambientRingMaxCount < 24) ambientRingMaxCount = 24;
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
        UpdateEnterOverlay();
        UpdateEnterOverlayFallbackTap();

        // HUD updates every frame for smooth distance
        UpdateMovementBearingSample();
        ApplyTopHudVerticalLayout();
        UpdatePursuitHUD();
        UpdateArtifactHUD();
        UpdateLocationHUD();
        UpdateStepsHUD();
	        UpdatePlayerRing();
	        UpdatePlayerMarker();
        PushNativeEnvironmentStateIfNeeded();

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
	    private float mapInitializedTime = -1f;
	    private const float LocationRevealDelaySeconds = 8f;

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
        mapInitializedTime = Time.time;
        Debug.Log("[SignalDirector] Initialized.");

        // Force the current "live pool" tuning on boot (prevents serialized scene values
        // from drifting away from the intended defaults while we iterate).
        if (useConcentricAmbientPool)
        {
            ambientRingStepMeters = 75f;
            ambientPoolMaxMiles = 1.1f;
            ambientAlternateArtifactTransmitter = true;
        }

        CreateDebugOverlay();
        CreateStepsHUD();
        CreatePursuitHUD();
        CreateArtifactHUD();
        CreateLocationHUD();
	        CreatePlayerMarker();

        // Seed the initial active set
        EnsurePrimary();
        FillSecondaries();
        FillDistant();
    }

    private void CreateDebugOverlay()
    {
        debugText = null;
        beamAuditText = null;
        return;

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

        // Always-on beam audit line (every 10s) — quick sanity check for ring spacing.
        var auditGO = new GameObject("BeamAuditLabel");
        auditGO.transform.SetParent(K1L0CanvasRoot.HUD, false);
        var art = auditGO.AddComponent<RectTransform>();
        art.anchorMin = new Vector2(1f, 0f);
        art.anchorMax = new Vector2(1f, 0f);
        art.pivot = new Vector2(1f, 0f);
        art.anchoredPosition = new Vector2(-20f, 230f);
        art.sizeDelta = new Vector2(320f, 64f);

        beamAuditText = auditGO.AddComponent<TextMeshProUGUI>();
        beamAuditText.font = font;
        beamAuditText.fontSize = 10f;
        beamAuditText.color = new Color(0.47f, 1f, 0.54f, 0.95f);
        beamAuditText.alignment = TextAlignmentOptions.BottomRight;
        beamAuditText.raycastTarget = false;
        beamAuditText.textWrappingMode = TextWrappingModes.Normal;
        beamAuditText.overflowMode = TextOverflowModes.Truncate;
        ApplyHeavyShadow(beamAuditText);
    }

    private void CreateStepsHUD()
    {
        var font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (font == null) font = TMP_Settings.defaultFontAsset;
        stepsWidgetRoot = CreateStepWidget("StepsBlock", 19, font);
        dailyStepsLabel = stepsHeroLabel;
        weeklyStepsLabel = null;
    }

    private GameObject CreateStepWidget(string name, int order, TMP_FontAsset font)
    {
        var row = new GameObject(name);
        row.transform.SetParent(K1L0CanvasRoot.HUD, false);
        var rowRt = row.AddComponent<RectTransform>();
        rowRt.anchorMin = new Vector2(0f, 1f);
        rowRt.anchorMax = new Vector2(1f, 1f);
        rowRt.pivot = new Vector2(0f, 1f);
        rowRt.sizeDelta = new Vector2(-24f, 245f);
        K1L0HudLayoutController.RegisterTopElement(rowRt, name, order, 245f, 24f);

        var stack = row.AddComponent<VerticalLayoutGroup>();
        stack.padding = new RectOffset(0, 0, 44, 0);
        stack.spacing = 0f;
        stack.childAlignment = TextAnchor.UpperCenter;
        stack.childControlWidth = true;
        stack.childControlHeight = true;
        stack.childForceExpandWidth = true;
        stack.childForceExpandHeight = false;

        stepsHeroLabel = CreateStepsHeroText("StepsHero", row.transform, 56, new Color(1f, 1f, 1f, 0.96f), 62f);
        stepsHeroLabel.text = "0";

        var stepsUnitLabel = CreateStepsText("StepsUnit", row.transform, font, 11f, new Color(1f, 1f, 1f, 0.72f), 18f, TextAlignmentOptions.Midline);
        stepsUnitLabel.text = "steps";

        stepsMetaLabel = CreateStepsText("StepsMeta", row.transform, font, 11f, new Color(1f, 1f, 1f, 0.82f), 16f, TextAlignmentOptions.Midline);
        stepsMetaLabel.text = "24h: ...    7d: ...";

        CreateLayoutSpacer("StepsCtaSpacer", row.transform, 8f);
        stepsCtaLabel = CreateStepsText("StepsCta", row.transform, font, 13f, new Color(0.47f, 1f, 0.54f, 0.95f), 24f, TextAlignmentOptions.Midline);
        stepsCtaLabel.color = new Color(0.47f, 1f, 0.54f, 0.95f);
        stepsCtaLabel.overflowMode = TextOverflowModes.Overflow;
        stepsCtaLabel.text = "WALK TO BUILD YOUR STRENGTH";

        stepsCompassRow = new GameObject("StepsCompassRow");
        stepsCompassRow.transform.SetParent(row.transform, false);
        var compassRowRt = stepsCompassRow.AddComponent<RectTransform>();
        compassRowRt.sizeDelta = new Vector2(0f, 84f);
        var compassLayout = stepsCompassRow.AddComponent<LayoutElement>();
        compassLayout.preferredHeight = 84f;
        compassLayout.minHeight = 0f;
        compassLayout.flexibleWidth = 1f;

        stepsCompassGO = CreateLargeStepCompass(stepsCompassRow.transform, out stepsCompassRing, out stepsCompassArrowRt, out stepsDistanceLabel);
        stepsCompassRow.SetActive(false);
        return row;
    }

    private static Font LoadCleanSansStepsUiFont()
    {
        if (cleanSansStepsUiFont != null) return cleanSansStepsUiFont;

        try
        {
            cleanSansStepsUiFont = Font.CreateDynamicFontFromOSFont(
                new[] { "SF Pro Display", "SF Pro Text", "Helvetica Neue", "Helvetica", "Arial" },
                96);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SignalDirectorV2] Failed to create clean sans steps font: {e.Message}");
        }

        return cleanSansStepsUiFont != null ? cleanSansStepsUiFont : Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    private Text CreateStepsHeroText(string name, Transform parent, int fontSize, Color color, float height)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0f, height);

        var layout = go.AddComponent<LayoutElement>();
        layout.preferredHeight = height;
        layout.minHeight = height;
        layout.flexibleWidth = 1f;

        var label = go.AddComponent<Text>();
        label.font = LoadCleanSansStepsUiFont();
        label.fontSize = fontSize;
        label.fontStyle = FontStyle.Normal;
        label.alignment = TextAnchor.LowerCenter;
        label.color = color;
        label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        return label;
    }

    private void CreateLayoutSpacer(string name, Transform parent, float height)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0f, height);
        var layout = go.AddComponent<LayoutElement>();
        layout.preferredHeight = height;
        layout.minHeight = height;
        layout.flexibleWidth = 1f;
    }

    private TextMeshProUGUI CreateStepsText(string name, Transform parent, TMP_FontAsset font, float fontSize, Color color, float height, TextAlignmentOptions alignment, bool useShadow = true)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0f, height);

        var layout = go.AddComponent<LayoutElement>();
        layout.preferredHeight = height;
        layout.minHeight = height;
        layout.flexibleWidth = 1f;

        var label = go.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.color = color;
        label.margin = Vector4.zero;
        label.raycastTarget = false;
        label.richText = true;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        if (useShadow)
        {
            ApplyHeavyShadow(label);
        }
        else
        {
            label.fontStyle = FontStyles.Normal;
            label.fontWeight = FontWeight.Regular;
            label.enableVertexGradient = false;
            label.outlineWidth = 0f;
            label.outlineColor = Color.clear;
            var shadows = go.GetComponents<Shadow>();
            for (int i = 0; i < shadows.Length; i++)
                Destroy(shadows[i]);
        }
        return label;
    }

    private GameObject CreateLargeStepCompass(Transform parent, out Image ringImg, out RectTransform arrowRt, out TextMeshProUGUI distanceTmp)
    {
        const float compassSize = 58f;
        var container = new GameObject("StepsCompass");
        container.transform.SetParent(parent, false);
        var cRt = container.AddComponent<RectTransform>();
        cRt.anchorMin = new Vector2(0.5f, 0.5f);
        cRt.anchorMax = new Vector2(0.5f, 0.5f);
        cRt.pivot = new Vector2(0.5f, 0.5f);
        cRt.anchoredPosition = new Vector2(0f, 8f);
        cRt.sizeDelta = new Vector2(compassSize, compassSize);

        var bgGO = new GameObject("CompassBg");
        bgGO.transform.SetParent(container.transform, false);
        var bgRt = bgGO.AddComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        var bgImg = bgGO.AddComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.96f);
        bgImg.raycastTarget = false;
        CreateFilledCircleSprite(bgImg);

        var ringGO = new GameObject("Ring");
        ringGO.transform.SetParent(container.transform, false);
        var ringRt = ringGO.AddComponent<RectTransform>();
        ringRt.anchorMin = Vector2.zero;
        ringRt.anchorMax = Vector2.one;
        ringRt.offsetMin = Vector2.zero;
        ringRt.offsetMax = Vector2.zero;
        ringImg = ringGO.AddComponent<Image>();
        ringImg.color = new Color(0.47f, 1f, 0.54f, 0.9f);
        ringImg.raycastTarget = false;
        CreateRingSprite(ringImg);

        var arrowGO = new GameObject("Arrow");
        arrowGO.transform.SetParent(container.transform, false);
        arrowRt = arrowGO.AddComponent<RectTransform>();
        arrowRt.anchorMin = new Vector2(0.5f, 0.5f);
        arrowRt.anchorMax = new Vector2(0.5f, 0.5f);
        arrowRt.pivot = new Vector2(0.5f, 0.5f);
        arrowRt.anchoredPosition = Vector2.zero;
        arrowRt.sizeDelta = new Vector2(compassSize * 0.66f, compassSize * 0.66f);
        var arrowImg = arrowGO.AddComponent<Image>();
        arrowImg.color = new Color(0.47f, 1f, 0.54f, 1f);
        arrowImg.raycastTarget = false;
        CreateArrowSprite(arrowImg);

        var distGO = new GameObject("Distance");
        distGO.transform.SetParent(container.transform, false);
        var distRt = distGO.AddComponent<RectTransform>();
        distRt.anchorMin = new Vector2(0.5f, 0f);
        distRt.anchorMax = new Vector2(0.5f, 0f);
        distRt.pivot = new Vector2(0.5f, 1f);
        distRt.anchoredPosition = new Vector2(0f, -2f);
        distRt.sizeDelta = new Vector2(96f, 16f);
        distanceTmp = distGO.AddComponent<TextMeshProUGUI>();
        distanceTmp.font = Resources.Load<TMP_FontAsset>("Fonts/IBMPlexMono-Regular SDF") ?? TMP_Settings.defaultFontAsset;
        distanceTmp.fontSize = 10f;
        distanceTmp.text = "";
        distanceTmp.alignment = TextAlignmentOptions.Top;
        distanceTmp.color = Color.white;
        distanceTmp.raycastTarget = false;
        ApplyHeavyShadow(distanceTmp);

        return container;
    }

    private void UpdateStepsHUD()
    {
        if (stepsHeroLabel == null) return;
        if (pedometerService == null) pedometerService = FindFirstObjectByType<PedometerService>();
        int rawMain = pedometerService != null ? pedometerService.kilosyncSteps : -1;
        int daily = pedometerService != null ? pedometerService.stepsLast24Hours : -1;
        int weekly = pedometerService != null ? pedometerService.stepsLast7Days : -1;
        if (pedometerService != null)
            pedometerService.RefreshWalkingBucketsIfDue(false, PedometerService.WalkingBucketMinutes, Mathf.Max(0, ambientMinStepsToSpawn), momentumGraceSteps);
        bool active = pedometerService != null && pedometerService.walkBucketReady && !pedometerService.walkCurrentBucketInactive;
        int liveWalkSteps = pedometerService != null && pedometerService.walkBucketReady
            ? Mathf.Max(0, pedometerService.walkWindowSteps)
            : Mathf.Max(0, rawMain);

        var playerMerc = GetPlayerMercator();
        float distanceMeters = float.MaxValue;
        Signal nearest = active ? GetNearestAmbientPortal(playerMerc, out distanceMeters) : null;

        stepsHeroLabel.text = FormatStepCount(liveWalkSteps);
        if (stepsMetaLabel != null)
            stepsMetaLabel.text = $"24h: {FormatStepCount(daily)}    7d: {FormatStepCount(weekly)}";

        bool anomaly = nearest != null;
        if (stepsCtaLabel != null)
        {
            stepsCtaLabel.text = !active ? "WALK TO BUILD YOUR STRENGTH" : anomaly ? "ANOMALY DETECTED" : "CONTINUE WALKING";
            stepsCtaLabel.color = anomaly ? new Color(1f, 0.2f, 0.18f, 0.95f) : new Color(0.47f, 1f, 0.54f, 0.95f);
        }

        if (stepsCompassRow != null) stepsCompassRow.SetActive(anomaly);
        if (stepsCompassGO != null) stepsCompassGO.SetActive(anomaly);
        if (anomaly)
        {
            if (stepsCompassArrowRt != null)
                stepsCompassArrowRt.localEulerAngles = new Vector3(0f, 0f, -RelativeAngleTo(nearest, playerMerc));
            if (stepsDistanceLabel != null)
                stepsDistanceLabel.text = FormatTeaserDistancePlain(distanceMeters);
            if (stepsCompassRing != null)
                stepsCompassRing.color = new Color(1f, 0.2f, 0.18f, 0.9f);
        }
    }

    private void UpdateActiveMomentumSteps(int rawSteps)
    {
        if (rawSteps < 0)
        {
            activeMomentumSteps = -1;
            return;
        }

        if (motionResetBaseSteps < 0 || rawSteps < motionResetBaseSteps)
            motionResetBaseSteps = rawSteps;

        bool recentlyWalking = wasWalkingWithMomentumForStreak &&
                               Time.unscaledTime - lastWalkingMomentumTime <= MomentumSessionGraceSeconds;

        if (!walkingMomentumReady)
        {
            if (!recentlyWalking)
            {
                wasWalkingWithMomentumForStreak = false;
                // Include steps already taken since the movement baseline (the
                // ones taken while it still says WALK), so they aren't discarded
                // when momentum finally establishes.
                activeMomentumSteps = Mathf.Max(0, rawSteps - motionResetBaseSteps);
            }
            return;
        }

        if (!isWalkingWithMomentum)
        {
            if (recentlyWalking)
            {
                activeMomentumSteps = Mathf.Max(0, rawSteps - motionResetBaseSteps);
                return;
            }

            wasWalkingWithMomentumForStreak = false;
            motionResetBaseSteps = rawSteps;
            activeMomentumSteps = 0;
            return;
        }

        lastWalkingMomentumTime = Time.unscaledTime;
        if (!wasWalkingWithMomentumForStreak)
        {
            wasWalkingWithMomentumForStreak = true;
            motionResetBaseSteps = rawSteps;
        }

        activeMomentumSteps = Mathf.Max(0, rawSteps - motionResetBaseSteps);
    }

    private void UpdateWalkingMomentum(int currentSteps)
    {
        if (currentSteps < 0 || map == null || map.MapInformation == null || playerObj == null) return;
        if (Time.unscaledTime < nextMomentumSampleTime && momentumSamples.Count > 0) return;

        nextMomentumSampleTime = Time.unscaledTime + MomentumSampleIntervalSeconds;
        Vector2d mercator = GetPlayerMercator();
        momentumSamples.Add(new MomentumSample
        {
            time = Time.unscaledTime,
            steps = currentSteps,
            mercator = mercator,
            pedometerDistanceMeters = pedometerService != null ? Mathf.Max(0f, (float)pedometerService.distanceMeters) : 0f
        });

        float cutoff = Time.unscaledTime - MomentumWindowSeconds;
        while (momentumSamples.Count > 1 && momentumSamples[0].time < cutoff)
            momentumSamples.RemoveAt(0);

        if (momentumSamples.Count < 2)
        {
            walkingMomentumReady = false;
            walkingMomentum = 0f;
            isWalkingWithMomentum = false;
            return;
        }

        MomentumSample oldest = momentumSamples[0];
        MomentumSample latest = momentumSamples[momentumSamples.Count - 1];
        float elapsedMinutes = Mathf.Max(0.01f, (latest.time - oldest.time) / 60f);
        int stepDelta = Mathf.Max(0, latest.steps - oldest.steps);
        double dx = latest.mercator.x - oldest.mercator.x;
        double dy = latest.mercator.y - oldest.mercator.y;
        float mapMeters = Mathf.Max(0f, (float)Math.Sqrt(dx * dx + dy * dy));
        float pedometerMeters = Mathf.Max(0f, latest.pedometerDistanceMeters - oldest.pedometerDistanceMeters);
        float meters = Mathf.Max(mapMeters, pedometerMeters);

        lastStepsPerMinute = stepDelta / elapsedMinutes;
        lastMetersPerStep = stepDelta > 0 ? meters / stepDelta : 0f;

        float cadenceScore = Mathf.InverseLerp(8f, 45f, lastStepsPerMinute);
        float displacementScore = Mathf.InverseLerp(8f, 45f, meters);
        float strideScore = 0f;
        if (stepDelta >= 6)
        {
            if (lastMetersPerStep < 0.20f) strideScore = 0f;
            else if (lastMetersPerStep <= 0.75f) strideScore = Mathf.InverseLerp(0.20f, 0.75f, lastMetersPerStep);
            else if (lastMetersPerStep <= 1.80f) strideScore = Mathf.InverseLerp(1.80f, 0.75f, lastMetersPerStep);
        }

        walkingMomentum = Mathf.Clamp01(cadenceScore * Mathf.Max(strideScore, displacementScore * 0.5f));
        walkingMomentumReady = latest.time - oldest.time >= Mathf.Min(30f, MomentumWindowSeconds);
        isWalkingWithMomentum = walkingMomentumReady && walkingMomentum >= MomentumWalkingThreshold;

        // Keep sub-scores for the readable diagnostic.
        lastCadenceScore = cadenceScore;
        lastStrideScore = strideScore;
        lastDisplacementScore = displacementScore;
        lastMetersMoved = meters;
        lastWindowMinutes = elapsedMinutes;
    }

    // Plain-language explanation of why momentum is low, plus the formula.
    // Shown under the WALK prompt so it's clear what to do to fix it.
    private string BuildMomentumDiagnostic()
    {
        if (!walkingMomentumReady)
            return "<size=11>warming up… keep walking ~30s to measure</size>";

        // Identify the weakest contributor.
        // momentum = cadence × max(stride, displacement×0.5)
        float moveTerm = Mathf.Max(lastStrideScore, lastDisplacementScore * 0.5f);
        var reasons = new System.Collections.Generic.List<string>();
        if (lastCadenceScore < 0.5f)
            reasons.Add($"slow pace ({lastStepsPerMinute:F0} steps/min, want 25+)");
        if (moveTerm < 0.5f)
        {
            if (lastStrideScore <= lastDisplacementScore * 0.5f)
                reasons.Add($"not covering ground ({lastMetersMoved:F0} m in {lastWindowMinutes:F0} min)");
            else
                reasons.Add($"short strides ({lastMetersPerStep:F2} m/step, want ~0.75)");
        }
        if (reasons.Count == 0) reasons.Add("just below threshold — keep going");

        int pct = Mathf.RoundToInt(walkingMomentum * 100f);
        int needPct = Mathf.RoundToInt(MomentumWalkingThreshold * 100f);
        string why = string.Join("; ", reasons);
        return
            $"<size=12>why low: {why}</size>\n" +
            $"<size=10>momentum {pct}% (need {needPct}%)</size>\n" +
            $"<size=9>formula: cadence × max(stride, move×0.5)</size>";
    }

    private string BuildMomentumLine(bool inert)
    {
        if (inert) return "<size=11>momentum: inert</size>";
        if (!walkingMomentumReady) return "<size=11>momentum: scanning walk...</size>";
        string state = isWalkingWithMomentum ? "walking" : "low";
        int percent = Mathf.RoundToInt(walkingMomentum * 100f);
        return $"<size=11>momentum: {state} {percent}%  {lastStepsPerMinute:F0} spm  {lastMetersPerStep:F2} m/step</size>";
    }

    private string BuildWalkingBucketLine(bool inert)
    {
        if (pedometerService == null) return "<size=11>walking: no pedometer</size>";
        if (!pedometerService.walkBucketReady) return "<size=11>walking: measuring session...</size>";
        bool inactive = pedometerService.walkCurrentBucketInactive;
        int target = Mathf.Max(1, ambientMinStepsToSpawn);
        if (!inactive && !inert)
        {
            // Signal locked / active (strength bars shown alongside) — emit percent only.
            int pct = Mathf.Clamp(Mathf.RoundToInt((pedometerService.walkWindowSteps / (float)target) * 100f), 0, 999);
            return $"<size=11>walking: active {pct}%</size>";
        }
        if (inactive)
        {
            return $"<size=11>walking: inactive session {pedometerService.walkWindowSteps}/{target}  current {pedometerService.walkCurrentBucketSteps}st/{pedometerService.walkCurrentBucketMeters:F0}m</size>";
        }
        // Grace / "keep walking" — show debug steps until signal arrives.
        int remaining = Mathf.Max(0, target - pedometerService.walkWindowSteps);
        return $"<size=11>walking: keep walking session {pedometerService.walkWindowSteps}/{target}  current {pedometerService.walkCurrentBucketSteps}st/{pedometerService.walkCurrentBucketMeters:F0}m</size>\n<size=10>(signal in {remaining} steps)</size>";
    }

    private string BuildWalkingBucketDiagnostic()
    {
        if (pedometerService == null)
            return "<size=11>no pedometer data</size>";
        if (!pedometerService.walkBucketReady)
            return "<size=11>measuring activity… start walking</size>";
        string firstLine = pedometerService.walkCurrentBucketInactive
            ? "current bucket inactive"
            : $"session {pedometerService.walkWindowSteps}/{Mathf.Max(0, ambientMinStepsToSpawn)} steps";
        return $"<size=11>{firstLine}</size>\n" +
               $"<size=10>bucket {pedometerService.walkInactiveBucketMinutes}m needs {pedometerService.walkInactiveStepThreshold}st/{pedometerService.walkInactiveMetersThreshold:F0}m; active buckets {pedometerService.walkActiveBuckets}</size>";
    }

    private bool AmbientPortalsAllowedByActivity()
    {
        if (!requireKilosyncForAmbientPortals) return true;
        if (pedometerService == null) pedometerService = FindFirstObjectByType<PedometerService>();
        if (pedometerService == null) return false;
        int minSteps = Mathf.Max(0, ambientMinStepsToSpawn);
        pedometerService.RefreshWalkingBucketsIfDue(false, PedometerService.WalkingBucketMinutes, minSteps, momentumGraceSteps);
        return pedometerService.HasWalkingBucketSignal(minSteps);
    }

    private void UpdateMovementBearingSample()
    {
        if (map == null || map.MapInformation == null || playerObj == null) return;
        Vector2d current = GetPlayerMercator();
        if (!_hasMovementBearingSample)
        {
            _lastMovementBearingMercator = current;
            _hasMovementBearingSample = true;
            return;
        }

        double dx = current.x - _lastMovementBearingMercator.x;
        double dy = current.y - _lastMovementBearingMercator.y;
        double meters = Math.Sqrt(dx * dx + dy * dy);
        if (meters < 3d) return;

        _lastMovementBearingDegrees = ((float)(Math.Atan2(dx, dy) * (180.0 / Math.PI)) + 360f) % 360f;
        _lastMovementBearingMercator = current;
        _hasMovementBearing = true;
    }

    private void RemoveAmbientPortalSignals()
    {
        for (int i = signals.Count - 1; i >= 0; i--)
        {
            var s = signals[i];
            if (s == null) continue;
            if (s.role == SignalRole.SecondaryNearby && s.state != SignalState.CoolingDown)
                RemoveSignal(s);
        }
    }

    private AudioClip _beamSpawnPewClip;
    private static readonly string[] _sfxSlotNames = { "beam_spawn", "beam_collect", "incoming_transmission", "response_tap", "proximity_alert" };
    private readonly Dictionary<string, AudioClip> _sfxSlots = new Dictionary<string, AudioClip>();

    private void PlayAmbientPortalSpawnSound()
    {
        if (Time.unscaledTime - _lastAmbientPortalSpawnSoundTime < Mathf.Max(0f, ambientPortalSpawnSoundCooldown)) return;
        PlayAmbientPortalSoundUnchecked("beam_spawn");
        _lastAmbientPortalSpawnSoundTime = Time.unscaledTime;
    }

    public void PlayAmbientPortalCollectSound()
    {
        PlayAmbientPortalSoundUnchecked("beam_collect");
    }

    public void PlaySfxSlot(string slot)
    {
        PlayAmbientPortalSoundUnchecked(slot);
    }

    private void PlayAmbientPortalSoundUnchecked(string slot = "beam_spawn")
    {
        AudioClip clip = null;
        if (_sfxSlots.TryGetValue(slot, out AudioClip sfxClip) && sfxClip != null)
            clip = sfxClip;
        else
        {
            // Fall back to the bundled BeamSpawn.wav for any slot not yet configured.
            if (_beamSpawnPewClip == null)
                _beamSpawnPewClip = Resources.Load<AudioClip>("Audio/BeamSpawn");
            clip = _beamSpawnPewClip != null ? _beamSpawnPewClip : ambientPortalSpawnClip;
        }
        if (clip == null) return;

        var go = new GameObject("AmbientPortalSpawnSFX");
        var source = go.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = ambientPortalSpawnVolume;
        source.spatialBlend = 0f;
        source.playOnAwake = false;
        source.Play();
        Destroy(go, clip.length + 0.25f);
    }

    private IEnumerator LoadSfxConfig()
    {
        yield return new WaitUntil(() => APIManager.Instance != null);
        string url = APIManager.Instance.GetBaseURL() + "/api/k1l0/sfx/config";
        using (var req = UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
            {
                string json = req.downloadHandler.text;
                foreach (string slot in _sfxSlotNames)
                {
                    string clipUrl = ExtractJsonSlotUrl(json, slot);
                    if (!string.IsNullOrEmpty(clipUrl))
                        StartCoroutine(DownloadSfxClip(slot, clipUrl));
                }
            }
            else
            {
                Debug.LogWarning($"[SignalDirectorV2] SFX config fetch failed: {req.error}");
            }
        }
    }

    private IEnumerator DownloadSfxClip(string slot, string clipUrl)
    {
        using (var req = UnityWebRequestMultimedia.GetAudioClip(clipUrl, AudioType.MPEG))
        {
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(req);
                if (clip != null)
                {
                    _sfxSlots[slot] = clip;
                    Debug.Log($"[SignalDirectorV2] SFX loaded for slot '{slot}'");
                }
            }
            else
            {
                Debug.LogWarning($"[SignalDirectorV2] SFX download failed for slot '{slot}': {req.error}");
            }
        }
    }

    private static string ExtractJsonSlotUrl(string json, string slot)
    {
        int ki = json.IndexOf("\"" + slot + "\"", StringComparison.Ordinal);
        if (ki < 0) return null;
        int urlKey = json.IndexOf("\"url\"", ki, StringComparison.Ordinal);
        if (urlKey < 0) return null;
        int colon = json.IndexOf(':', urlKey + 5);
        if (colon < 0) return null;
        int q1 = json.IndexOf('"', colon + 1);
        if (q1 < 0) return null;
        int q2 = json.IndexOf('"', q1 + 1);
        if (q2 < 0) return null;
        return json.Substring(q1 + 1, q2 - q1 - 1);
    }

    private string BuildActivityPrompt(bool inert)
    {
        if (!inert)
        {
            if (walkingMomentumReady && !isWalkingWithMomentum)
            {
                if (IsPlayerInsideBuildingCached())
                    return "Go outside and walk to build momentum.";
                return "Start walking outside to build momentum.";
            }
            return "";
        }

        Signal loc = GetLocationTransmission();
        if (loc != null && showEnter && enterTarget == loc && !string.IsNullOrWhiteSpace(loc.locationName))
            return $"you are at {loc.locationName}. go outside and walk {Mathf.Max(0, ambientMinStepsToSpawn)} steps to establish kilosync.";

        if (IsPlayerInsideBuildingCached())
            return $"Go outside and take {Mathf.Max(0, ambientMinStepsToSpawn)} steps to establish kilosync.";

        return $"Start walking... Take {Mathf.Max(0, ambientMinStepsToSpawn)} steps to establish kilosync.";
    }

    private bool IsPlayerInsideBuildingCached()
    {
        if (Time.unscaledTime < nextInsideBuildingCheckTime) return cachedInsideBuilding;
        nextInsideBuildingCheckTime = Time.unscaledTime + 2f;
        cachedInsideBuilding = false;

        if (playerObj == null) return false;
        Vector3 p = playerObj.transform.position;
        var renderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
            string n = r.gameObject.name;
            if (string.IsNullOrEmpty(n) || n.IndexOf("building", StringComparison.OrdinalIgnoreCase) < 0) continue;
            Bounds b = r.bounds;
            if (p.x >= b.min.x && p.x <= b.max.x && p.z >= b.min.z && p.z <= b.max.z && p.y >= b.min.y - 1f && p.y <= b.max.y + 3f)
            {
                cachedInsideBuilding = true;
                break;
            }
        }
        return cachedInsideBuilding;
    }

    public bool IsPlayerLikelyInsideBuilding()
    {
        return IsPlayerInsideBuildingCached();
    }

    private void PushNativeEnvironmentStateIfNeeded()
    {
        if (Time.unscaledTime < _nextNativeEnvironmentPushTime) return;
        _nextNativeEnvironmentPushTime = Time.unscaledTime + 2f;

        bool insideBuilding = IsPlayerInsideBuildingCached();
        if (_sentNativeEnvironmentState && insideBuilding == _lastNativeInsideBuilding) return;

        _sentNativeEnvironmentState = true;
        _lastNativeInsideBuilding = insideBuilding;

#if UNITY_IOS && !UNITY_EDITOR
        K1L0SetEnvironmentState($"{{\"known\":true,\"indoors\":{(insideBuilding ? "true" : "false")}}}");
#endif
    }

    private string BuildStepStatusLine(bool active)
    {
        if (!active) return "NO ACTIVITY DETECTED";

        var playerMerc = GetPlayerMercator();
        Signal nearest = GetNearestAmbientPortal(playerMerc, out float distanceMeters);
        if (nearest == null) return "CONTINUE WALKING";

        string arrow = ArrowGlyphForRelativeAngle(RelativeAngleTo(nearest, playerMerc));
        return $"There is something at {FormatTeaserDistancePlain(distanceMeters)} {arrow}";
    }

    private Signal GetNearestAmbientPortal(Vector2d playerMerc, out float distanceMeters)
    {
        Signal nearest = null;
        distanceMeters = float.MaxValue;
        for (int i = 0; i < signals.Count; i++)
        {
            var s = signals[i];
            if (s == null) continue;
            if (s.state == SignalState.CoolingDown) continue;
            if (s.role == SignalRole.LocationTransmission) continue;
            if (s.transmissionType == TransmissionType.Location) continue;

            float d = (float)DistanceTo(s, playerMerc);
            if (d < distanceMeters)
            {
                distanceMeters = d;
                nearest = s;
            }
        }
        return nearest;
    }

    private static string ArrowGlyphForRelativeAngle(float angle)
    {
        angle = (angle + 360f) % 360f;
        if (angle < 22.5f || angle >= 337.5f) return "↑";
        if (angle < 67.5f) return "↗";
        if (angle < 112.5f) return "→";
        if (angle < 157.5f) return "↘";
        if (angle < 202.5f) return "↓";
        if (angle < 247.5f) return "↙";
        if (angle < 292.5f) return "←";
        return "↖";
    }

    private static string BuildStepsHeroText(int main, int daily, int weekly, string statusLine, bool showWalk = false)
    {
        string hero = showWalk ? "WALK" : FormatStepCount(main);
        return $"<size=10>steps since stop</size>\n" +
               $"<font=\"LiberationSans SDF\"><size=54>{hero}</size></font>\n" +
               $"<size=10>24h: {FormatStepCount(daily)}    7d: {FormatStepCount(weekly)}</size>" +
               (string.IsNullOrWhiteSpace(statusLine) ? "" : $"\n<size=12>{statusLine}</size>");
    }

    private static string FormatStepCount(int steps)
    {
        if (steps < 0) return "...";
        return steps.ToString("N0");
    }

    // True when the player is within enter-proximity of the active location
    // transmission (point distance or building footprint). Mirrors the enter
    // gate used elsewhere so "at a location" means the same thing everywhere.
    private bool IsPlayerAtLocation()
    {
        var loc = GetLocationTransmission();
        if (loc == null) return false;
        if (loc.state == SignalState.Interpreting || loc.state == SignalState.Resolved) return false;
        Vector2d playerMerc = GetPlayerMercator();
        float dist = DistanceTo(loc, playerMerc);
        Vector3 playerWorld = playerObj != null ? playerObj.transform.position : Vector3.zero;
        bool nearBuilding = IsPlayerNearLocationBuilding(loc, playerWorld, ENTER_PROXIMITY_BUILDING_EDGE_METERS);
        return nearBuilding || dist <= ENTER_PROXIMITY_LOCATION_METERS;
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
        rowRt.anchoredPosition = new Vector2(TeaserRowLeftInset, -(DefaultStoriesBottomFromTop + TeaserBelowStoriesGap));
        rowRt.sizeDelta = new Vector2(-24, TeaserRowHeight);
        K1L0HudLayoutController.RegisterTopElement(rowRt, "PursuitRow", 20, TeaserRowHeight, TeaserRowHeight);
        pursuitPanel = pursuitRow;

        // Row backing — transparent by default, darkens when the row becomes the ENTER button
        pursuitRowBg = pursuitRow.AddComponent<Image>();
        pursuitRowBg.color = new Color(0f, 0f, 0f, 0f);
        // Button on the row — only interactable when showEnter + target is a beam
        pursuitRowButton = pursuitRow.AddComponent<UnityEngine.UI.Button>();
        pursuitRowButton.targetGraphic = pursuitRowBg;
        pursuitRowButton.onClick.AddListener(OnTransmitTapped);
        pursuitRowButton.interactable = false;

        // Red ENTER-mode border frame (4 thin strips). Hidden until ENTER state.
        pursuitRowBorder = CreateBorderFrame(pursuitRow.transform, new Color(1f, 0.15f, 0.15f, 1f), 3f);

        // Compass circle + arrow (left side)
        pursuitCompassGO = CreateCompassWidget(pursuitRow.transform, "PursuitCompass", out pursuitCompassRing, out pursuitArrowRt, out pursuitDist);

        // Foreground text layer — green, blinks, with heavy drop shadow
        pursuitLabel = CreateHUDTextLayer(pursuitRow.transform, "PursuitLabel", font, pursuitLabelNormalFontSize, pursuitLabelNormalOffset);
        pursuitLabel.color = new Color(0.47f, 1f, 0.54f, 1f);
        ApplyHeavyShadow(pursuitLabel);

	        pursuitTeaser = null;
	    }

	    private void CreateArtifactHUD()
	    {
	        var font = Resources.Load<TMP_FontAsset>("Fonts/IBMPlexMono-Regular SDF");
	        if (font == null) font = TMP_Settings.defaultFontAsset;

	        artifactRow = new GameObject("ArtifactRow");
	        artifactRow.transform.SetParent(K1L0CanvasRoot.HUD, false);
	        var rowRt = artifactRow.AddComponent<RectTransform>();
	        rowRt.anchorMin = new Vector2(0f, 1f);
	        rowRt.anchorMax = new Vector2(1f, 1f);
	        rowRt.pivot = new Vector2(0f, 1f);
	        rowRt.anchoredPosition = new Vector2(TeaserRowLeftInset, -(DefaultStoriesBottomFromTop + TeaserBelowStoriesGap + TeaserRowHeight + TeaserRowGap));
	        rowRt.sizeDelta = new Vector2(-24, TeaserRowHeight);
	        K1L0HudLayoutController.RegisterTopElement(rowRt, "ArtifactRow", 21, TeaserRowHeight, TeaserRowHeight);

	        artifactRowBg = artifactRow.AddComponent<Image>();
	        artifactRowBg.color = new Color(0f, 0f, 0f, 0f);

	        artifactRowButton = artifactRow.AddComponent<UnityEngine.UI.Button>();
	        artifactRowButton.targetGraphic = artifactRowBg;
	        artifactRowButton.onClick.AddListener(OnViewArtifactTapped);
	        artifactRowButton.interactable = false;

	        artifactRowBorder = CreateBorderFrame(artifactRow.transform, new Color(1f, 0.15f, 0.15f, 1f), 3f);

	        artifactCompassGO = CreateCompassWidget(artifactRow.transform, "ArtifactCompass", out artifactCompassRing, out artifactArrowRt, out artifactDist);

	        artifactLabel = CreateHUDTextLayer(artifactRow.transform, "ArtifactLabel", font, artifactLabelNormalFontSize, artifactLabelNormalOffset);
	        artifactLabel.color = new Color(0.47f, 1f, 0.54f, 1f);
	        ApplyHeavyShadow(artifactLabel);
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
	        rowRt.anchoredPosition = new Vector2(TeaserRowLeftInset, -(DefaultStoriesBottomFromTop + TeaserBelowStoriesGap + (TeaserRowHeight + TeaserRowGap) * 2f));
	        rowRt.sizeDelta = new Vector2(-24, TeaserRowHeight);
	        K1L0HudLayoutController.RegisterTopElement(rowRt, "LocRow", 22, TeaserRowHeight, TeaserRowHeight);
	        locPanel = locRow;

        // Row backing — transparent by default, darkens when the row becomes the ENTER button
        locRowBg = locRow.AddComponent<Image>();
        locRowBg.color = new Color(0f, 0f, 0f, 0f);
        // Button on the row — only active when showEnter is true
        locRowButton = locRow.AddComponent<UnityEngine.UI.Button>();
        locRowButton.targetGraphic = locRowBg;
        locRowButton.onClick.AddListener(OnEnterLocationTapped);
        locRowButton.interactable = false;

        // Red ENTER-mode border frame (4 thin strips). Hidden until ENTER state.
        locRowBorder = CreateBorderFrame(locRow.transform, new Color(1f, 0.15f, 0.15f, 1f), 3f);

        // Compass circle + arrow (left side)
        locCompassGO = CreateCompassWidget(locRow.transform, "LocCompass", out locCompassRing, out locArrowRt, out locDist);

        // Foreground text layer — green, blinks, with heavy drop shadow
        locLabel = CreateHUDTextLayer(locRow.transform, "LocLabel", font, locLabelNormalFontSize, locLabelNormalOffset);
        locLabel.color = new Color(0.47f, 1f, 0.54f, 1f);
        ApplyHeavyShadow(locLabel);

        // Main action button — full-width, flashing, shown when a beam/location is enterable.
        locEnterGO = new GameObject("MainAction");
        locEnterGO.transform.SetParent(K1L0CanvasRoot.HUD, false);
        var eRt = locEnterGO.AddComponent<RectTransform>();
        eRt.anchorMin = new Vector2(0f, 1f);
        eRt.anchorMax = new Vector2(1f, 1f);
        eRt.pivot = new Vector2(0f, 1f);
        eRt.anchoredPosition = new Vector2(TeaserRowLeftInset, -(DefaultStoriesBottomFromTop + TeaserBelowStoriesGap + (TeaserRowHeight + TeaserRowGap) * 3f + 6f));
        eRt.sizeDelta = new Vector2(-24f, MainActionHeight + MainActionSubtextHeight + 3f);

        var subtextGO = new GameObject("MainActionSubtext");
        subtextGO.transform.SetParent(locEnterGO.transform, false);
        var stRt = subtextGO.AddComponent<RectTransform>();
        stRt.anchorMin = new Vector2(0f, 1f);
        stRt.anchorMax = new Vector2(1f, 1f);
        stRt.pivot = new Vector2(0.5f, 1f);
        stRt.offsetMin = new Vector2(0f, -(MainActionSubtextHeight));
        stRt.offsetMax = Vector2.zero;
        locEnterSubtext = subtextGO.AddComponent<TextMeshProUGUI>();
        locEnterSubtext.font = font;
        locEnterSubtext.fontSize = 10f;
        locEnterSubtext.lineSpacing = -10f;
        locEnterSubtext.characterSpacing = 0f;
        locEnterSubtext.text = "enter portal";
        locEnterSubtext.color = new Color(1f, 0.18f, 0.15f, 0.8f);
        locEnterSubtext.alignment = TextAlignmentOptions.Center;
        locEnterSubtext.raycastTarget = false;
        ApplyHeavyShadow(locEnterSubtext);

        var buttonGO = new GameObject("MainActionButton");
        buttonGO.transform.SetParent(locEnterGO.transform, false);
        var btnRt = buttonGO.AddComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(0f, 0f);
        btnRt.anchorMax = new Vector2(1f, 0f);
        btnRt.pivot = new Vector2(0.5f, 0f);
        btnRt.offsetMin = Vector2.zero;
        btnRt.offsetMax = new Vector2(0f, MainActionHeight);

        locEnterButtonBg = buttonGO.AddComponent<Image>();
        locEnterButtonBg.color = new Color(0f, 0f, 0f, 0.70f);
        locEnterButton = buttonGO.AddComponent<UnityEngine.UI.Button>();
        locEnterButton.onClick.AddListener(OnEnterOverlayTapped);
        locEnterBorder = CreateBorderFrame(buttonGO.transform, new Color(1f, 0.15f, 0.15f, 1f), 2.5f);

        // Enter fg layer
        var enterFgGO = new GameObject("MainActionText");
        enterFgGO.transform.SetParent(buttonGO.transform, false);
        var efRt = enterFgGO.AddComponent<RectTransform>();
        efRt.anchorMin = Vector2.zero; efRt.anchorMax = Vector2.one;
        efRt.offsetMin = Vector2.zero; efRt.offsetMax = Vector2.zero;
        locEnterText = enterFgGO.AddComponent<TextMeshProUGUI>();
        locEnterText.font = font;
        locEnterText.fontSize = 14;
        locEnterText.text = "> ENTER TRANSMISSION_";
        locEnterText.color = new Color(0.47f, 1f, 0.54f, 1f);
        locEnterText.alignment = TextAlignmentOptions.Center;
        locEnterText.raycastTarget = false;
        locEnterText.richText = true;
        locEnterText.overflowMode = TextOverflowModes.Ellipsis;
        ApplyHeavyShadow(locEnterText);

        K1L0HudLayoutController.RegisterActionElement(eRt, "MainAction", 0, MainActionHeight + MainActionSubtextHeight + 3f, MainActionHeight + MainActionSubtextHeight + 3f);
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
        // Location row should always enter the location (not "nearest beam").
        var target = GetLocationTransmission();
        HandleEnterTapped(target);
    }

    private void OnEnterOverlayTapped()
    {
        if (lastEnterOverlayFrame == Time.frameCount) return;
        lastEnterOverlayFrame = Time.frameCount;
        HandleEnterTapped(enterTarget);
    }

    private void OnTransmitTapped()
    {
        // Transmitting is user-initiated now; spawned map beams do not open a transmitter flow.
    }

    public void MarkAmbientPortalVisited(Signal sig)
    {
        if (sig == null) return;
        if (sig.role == SignalRole.LocationTransmission || sig.transmissionType == TransmissionType.Location) return;

        string beamId = !string.IsNullOrWhiteSpace(sig.externalKey) ? sig.externalKey.Trim() : "";
        if (!string.IsNullOrEmpty(beamId) && visitedAmbientBeamIds.Add(beamId) && APIManager.Instance != null)
            StartCoroutine(PostBeamVisit(beamId));

        RemoveSignal(sig);
    }

    private IEnumerator PostBeamVisit(string beamId)
    {
        string safeId = beamId.Replace("\\", "\\\\").Replace("\"", "\\\"");
        string userId = "";
        var tm = TransmissionManager.Instance ?? FindFirstObjectByType<TransmissionManager>();
        if (tm != null)
            userId = tm.GetUserIdForClient() ?? "";
        string safeUserId = userId.Replace("\\", "\\\\").Replace("\"", "\\\"");
        string payload = $"{{\"beamId\":\"{safeId}\",\"userId\":\"{safeUserId}\"}}";
        bool ok = false;
        string response = null;
        yield return APIManager.Instance.Post("/k1l0/beams/visit", payload, (success, text) =>
        {
            ok = success;
            response = text;
        });

        if (!ok)
            Debug.LogWarning($"[SignalDirector] Beam visit failed beamId={beamId} response={response}");
        else
            Debug.Log($"[SignalDirector] Beam visit deleted beamId={beamId}");
    }

    private void OnViewArtifactTapped()
    {
        // Ambient row enters the nearest ambient portal. If the button is visible but
        // we find none in-range, log it so we can debug mismatched distance gates.
        var playerMerc = GetPlayerMercator();
        Signal nearest = null;
        float nearestDist = float.MaxValue;
        for (int i = 0; i < signals.Count; i++)
        {
            var s = signals[i];
            if (s == null) continue;
            if (s.role == SignalRole.LocationTransmission) continue;
            if (s.transmissionType == TransmissionType.Location) continue;
            if (s.state == SignalState.CoolingDown) continue;
            if (s.state == SignalState.Interpreting || s.state == SignalState.Resolved) continue;
            float d = (float)DistanceTo(s, playerMerc);
            if (d < nearestDist)
            {
                nearestDist = d;
                nearest = s;
            }
        }

        if (nearest == null)
        {
            Debug.LogWarning("[SignalDirector] View Ambient tapped but no ambient signals exist");
            return;
        }

        if (nearestDist > AmbientCollectRadiusMeters)
        {
            Debug.LogWarning($"[SignalDirector] View Ambient tapped but nearest is {nearestDist:F1}m away (needs <= {AmbientCollectRadiusMeters}m)");
            return;
        }

        HandleEnterTapped(nearest);
    }

    private void HandleEnterTapped(Signal target)
    {
        if (target == null) return;

        Debug.Log($"[SignalDirector] ENTER tapped (type={target.transmissionType}, role={target.role}, id={target.id})");

        EnsureLocationMetadata(target);
        Debug.Log($"[SignalDirector] ENTER → LocationExchangeModal for '{target.locationName}' (type={target.transmissionType}, role={target.role})");

        var locationModal = LocationExchangeModal.Instance ?? FindFirstObjectByType<LocationExchangeModal>();
        if (locationModal == null)
        {
            Debug.LogWarning("[SignalDirector] LocationExchangeModal missing; creating it now");
            var go = new GameObject("LocationExchangeModal");
            locationModal = go.AddComponent<LocationExchangeModal>();
            locationModal.Initialize();
        }

        if (locationModal != null)
        {
            locationModal.Show(target);
        }
        else
        {
            Debug.LogWarning("[SignalDirector] LocationExchangeModal still missing after create");
        }
        TransitionTo(target, SignalState.Interpreting);
    }

    // Non-LocationTransmission signals (pursuit/secondary/distant) represent virtual
    // characters, not real POIs. The TransmissionFrame header should carry the
    // character's name while the backend generates the shot — leaving locationName
    // blank here lets OnTransmissionReady overwrite it with whatever the story
    // backend returns (character context, scene, etc.).
    private void EnsureLocationMetadata(Signal sig)
    {
        if (sig == null || sig.transmissionType != TransmissionType.Location) return;
        if (!string.IsNullOrEmpty(sig.locationName)) return;

        sig.locationName = "location";
        sig.locationCategory = "location";
    }

    private void ApplyTopHudVerticalLayout()
    {
        if (K1L0HudLayoutController.IsManaged(pursuitRow != null ? pursuitRow.transform as RectTransform : null) ||
            K1L0HudLayoutController.IsManaged(artifactRow != null ? artifactRow.transform as RectTransform : null) ||
            K1L0HudLayoutController.IsManaged(locRow != null ? locRow.transform as RectTransform : null))
        {
            return;
        }

        float teaserTop = GetStoriesBottomFromTop() + TeaserBelowStoriesGap;
        SetTopTeaserRowPosition(pursuitRow, teaserTop);
        SetTopTeaserRowPosition(artifactRow, teaserTop + TeaserRowHeight + TeaserRowGap);
        SetTopTeaserRowPosition(locRow, teaserTop + (TeaserRowHeight + TeaserRowGap) * 2f);
        PositionEnterButtonUnderTeasers(teaserTop + (TeaserRowHeight + TeaserRowGap) * 3f + 6f);
    }

    private float GetStoriesBottomFromTop()
    {
        if (storiesStripRect == null)
        {
            var strip = GameObject.Find("StoriesStripRoot");
            storiesStripRect = strip != null ? strip.GetComponent<RectTransform>() : null;
        }
        var rect = storiesStripRect;
        if (rect == null) return DefaultStoriesBottomFromTop;

        float topOffset = Mathf.Abs(rect.anchoredPosition.y);
        float height = rect.rect.height > 1f ? rect.rect.height : rect.sizeDelta.y;
        if (height <= 1f) height = DefaultStoriesBottomFromTop;
        return topOffset + height;
    }

    private void SetTopTeaserRowPosition(GameObject row, float yFromTop)
    {
        if (row == null) return;
        var rect = row.GetComponent<RectTransform>();
        if (rect == null) return;
        rect.anchoredPosition = new Vector2(TeaserRowLeftInset, -yFromTop);
        if (rect.sizeDelta.y <= 30f)
            rect.sizeDelta = new Vector2(-24f, TeaserRowHeight);
    }

    private void PositionEnterButtonUnderTeasers(float yFromTop)
    {
        var rect = locEnterGO != null ? locEnterGO.transform as RectTransform : null;
        if (rect == null) return;
        if (K1L0HudLayoutController.IsManaged(rect))
        {
            K1L0HudLayoutController.Refresh();
            return;
        }
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(TeaserRowLeftInset, -yFromTop);
        rect.sizeDelta = new Vector2(-24f, MainActionHeight + MainActionSubtextHeight + 3f);
    }

    // Nearest non-cooldown, non-resolved beam (Artifact/Transmitter) within the
    // close-range enter radius. Location transmissions are handled separately
    // because they can become enterable via building-footprint proximity.
    private Signal FindNearestEnterableSignal(Vector2d playerMerc)
    {
        Signal best = null;
        float bestDist = float.MaxValue;
        for (int i = 0; i < signals.Count; i++)
        {
            var s = signals[i];
            if (TypeOf(s) == TransmissionType.Location) continue;
            if (s.state == SignalState.CoolingDown) continue;
            if (s.state == SignalState.Interpreting || s.state == SignalState.Resolved) continue;
            float d = DistanceTo(s, playerMerc);
            if (d <= AmbientCollectRadiusMeters && d < bestDist)
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
        bool nearBuilding = loc != null && IsPlayerNearLocationBuilding(loc, playerWorld, ENTER_PROXIMITY_BUILDING_EDGE_METERS);
        bool locProximity = loc != null
                            && (nearBuilding || dist <= ENTER_PROXIMITY_LOCATION_METERS)
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
                                  && IsStillWithinEnterHideDistance(locEnterStickySignal, playerMerc)
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

    private bool IsStillWithinEnterHideDistance(Signal sig, Vector2d playerMerc)
    {
        if (sig == null) return false;
        float dist = DistanceTo(sig, playerMerc);
        if (sig.transmissionType != TransmissionType.Location)
            return dist <= ENTER_HIDE_DISTANCE_METERS;

        // Location beams: treat building perimeter as enterable space.
        Vector3 playerWorld = playerObj != null ? playerObj.transform.position : Vector3.zero;
        bool nearBuilding = IsPlayerNearLocationBuilding(sig, playerWorld, ENTER_HIDE_DISTANCE_METERS);
        return nearBuilding || dist <= ENTER_HIDE_DISTANCE_METERS;
    }

    private void UpdateEnterOverlay()
    {
        if (locEnterGO == null || locEnterText == null) return;

        bool shouldShow = showEnter && enterTarget != null && !hudSuppressed
                          && enterTarget.state != SignalState.CoolingDown
                          && enterTarget.state != SignalState.Interpreting
                          && enterTarget.state != SignalState.Resolved;

        if (locEnterGO.activeSelf != shouldShow)
        {
            locEnterGO.SetActive(shouldShow);
            K1L0HudLayoutController.Refresh();
        }
        if (!shouldShow) return;
        locEnterGO.transform.SetAsLastSibling();
        PositionEnterButtonUnderTeasers(GetStoriesBottomFromTop() + TeaserBelowStoriesGap + TeaserRowHeight + TeaserRowGap + 6f);

        string subtext;
        switch (enterTargetType)
        {
            case TransmissionType.Location:
                subtext = string.IsNullOrEmpty(enterTarget.locationName)
                    ? "enter portal"
                    : $"enter portal\nYou are at {enterTarget.locationName}";
                break;
            case TransmissionType.Artifact:
                subtext = "enter portal\nmystery object";
                break;
            case TransmissionType.Transmitter:
            default:
                subtext = $"enter portal\n{FormatSignalGpsTitle(enterTarget)}";
                break;
        }

        locEnterText.text = "enter portal";
        float blink = Mathf.PingPong(Time.time * 1.5f, 1f);
        float a = 0.45f + blink * 0.55f;
        locEnterText.color = new Color(TeaserRed.r, TeaserRed.g, TeaserRed.b, a);
        if (locEnterSubtext != null)
        {
            locEnterSubtext.text = subtext;
            locEnterSubtext.color = new Color(TeaserRed.r, TeaserRed.g, TeaserRed.b, 0.40f + blink * 0.50f);
        }
        if (locEnterButtonBg != null)
            locEnterButtonBg.color = new Color(0f, 0f, 0f, 0.46f + blink * 0.28f);
        SetBorderFrameColor(locEnterBorder, new Color(TeaserRed.r, TeaserRed.g, TeaserRed.b, 0.55f + blink * 0.45f));
    }

    private void UpdateEnterOverlayFallbackTap()
    {
        if (locEnterGO == null || !locEnterGO.activeInHierarchy) return;

        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
            {
                enterOverlayPointerDown = IsEnterOverlayPoint(touch.position);
            }
            else if (touch.phase == TouchPhase.Canceled)
            {
                enterOverlayPointerDown = false;
            }
            else if (touch.phase == TouchPhase.Ended && enterOverlayPointerDown)
            {
                bool releasedInside = IsEnterOverlayPoint(touch.position);
                enterOverlayPointerDown = false;
                if (releasedInside) OnEnterOverlayTapped();
            }
        }
        else
        {
            if (Input.GetMouseButtonDown(0))
            {
                enterOverlayPointerDown = IsEnterOverlayPoint(Input.mousePosition);
            }
            else if (Input.GetMouseButtonUp(0) && enterOverlayPointerDown)
            {
                bool releasedInside = IsEnterOverlayPoint(Input.mousePosition);
                enterOverlayPointerDown = false;
                if (releasedInside) OnEnterOverlayTapped();
            }
        }
    }

    private bool IsEnterOverlayPoint(Vector2 screenPoint)
    {
        var rect = locEnterGO != null ? locEnterGO.transform as RectTransform : null;
        return rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint, null);
    }

	    private void UpdateLocationHUD()
	    {
	        if (locLabel == null || locRow == null) return;

	        var playerMerc = GetPlayerMercator();
	        var loc = GetLocationTransmission();

	        bool locAvailable = loc != null && loc.state != SignalState.CoolingDown;
	        bool revealAllowed = mapInitializedTime > 0f && (Time.time - mapInitializedTime) >= LocationRevealDelaySeconds;

	        // Location names are rendered by SignalBeamBridge at the beam top.
	        // Keep this legacy teaser row hidden so the same place is not labeled twice.
	        if (locAvailable && revealAllowed)
	        {
	            if (locRow.activeSelf)
	            {
	                locRow.SetActive(false);
	                K1L0HudLayoutController.Refresh();
	            }
	            if (locCompassGO != null && locCompassGO.activeSelf) locCompassGO.SetActive(false);
	            if (locRowButton != null) locRowButton.interactable = false;
	            locLabel.text = "";
	            if (locDist != null) locDist.text = "";
	            return;
	        }

	        // Always show the location row once HUD exists (it becomes a "scanning..." hint until we have a location).
	        locRow.SetActive(MapTeaserRowsVisible);

	        string teaser = loc != null ? (loc.teaser ?? "") : "";
	        float dist = loc != null ? DistanceTo(loc, playerMerc) : float.MaxValue;

	        // Location row enters when the player is actually near the location.
	        Vector3 playerWorld = playerObj != null ? playerObj.transform.position : Vector3.zero;
	        bool insideBuilding = loc != null && IsPlayerNearLocationBuilding(loc, playerWorld, ENTER_PROXIMITY_BUILDING_EDGE_METERS);
	        bool showLocationEnter = loc != null
	                                 && (insideBuilding || dist <= ENTER_PROXIMITY_LOCATION_METERS)
	                                 && loc.state != SignalState.Interpreting
	                                 && loc.state != SignalState.Resolved
	                                 && loc.state != SignalState.CoolingDown;

	        // ENTER overlay is handled by UpdateEnterOverlay().

	        // Suspense: keep scanning copy visible for a few seconds after the map appears,
	        // and whenever no location is currently available.
	        if (!locAvailable || !revealAllowed)
	        {
	            // Force non-ENTER mode visuals while scanning.
	            if (locCompassGO != null && locCompassGO.activeSelf) locCompassGO.SetActive(false);
	            if (locRowBg != null) locRowBg.color = new Color(0f, 0f, 0f, 0f);
	            if (locRowBorder != null && locRowBorder.activeSelf) locRowBorder.SetActive(false);
	            if (locRowButton != null) locRowButton.interactable = false;
	            var rowRt = locRow.GetComponent<RectTransform>();
	            if (rowRt != null && rowRt.sizeDelta.y > 30f) rowRt.sizeDelta = new Vector2(rowRt.sizeDelta.x, 30f);

	            var labelRt = locLabel.rectTransform;
	            labelRt.offsetMin = new Vector2(0f, 0f);
	            labelRt.offsetMax = Vector2.zero;
	            locLabel.fontSize = locLabelNormalFontSize;
	            locLabel.alignment = TextAlignmentOptions.MidlineLeft;
		            locLabel.text = "scanning locations...";

		            float scanBlink = Mathf.PingPong(Time.time * 0.5f, 1f);
		            float scanAlpha = 0.4f + scanBlink * 0.6f;
		            locLabel.color = new Color(0.47f, 1f, 0.54f, scanAlpha);
	            if (locDist != null) locDist.text = "";
	            return;
	        }

	        // Countdown + teaser sentence are only rendered in non-ENTER (teaser) mode,
	        // and that mode requires a LocationTransmission to exist.
	        string sentence = "";
		        if (loc != null)
		        {
		            string locName = string.IsNullOrEmpty(loc.locationName) ? "UNKNOWN" : loc.locationName;
		            string distStr = FormatTeaserDistance(dist);
		            sentence = $"{locName} {distStr}";
		        }

        if (locDist != null) locDist.text = loc != null ? $"{dist:F0}m" : "";

        float tBlink = Mathf.PingPong(Time.time * 0.5f, 1f);
        float textAlpha = 0.4f + tBlink * 0.6f;
        Color activeColor = showEnter && enterTargetType == TransmissionType.Location ? TeaserRed : TeaserGreen;

        // The centered ENTER overlay is the single location enter control.
        // Keep the row in teaser mode so we don't render duplicate "Enter <place>" labels.
        bool useLocationRowEnterMode = false;
        if (useLocationRowEnterMode && showLocationEnter)
        {
            // Row becomes the ENTER button: big centered label, solid black backing,
            // red border frame, tappable.
            if (locCompassGO != null && locCompassGO.activeSelf) locCompassGO.SetActive(false);
            if (locRowBg != null) locRowBg.color = new Color(0f, 0f, 0f, 1f);
            if (locRowBorder != null && !locRowBorder.activeSelf) locRowBorder.SetActive(true);
            if (locRowButton != null) locRowButton.interactable = !hudSuppressed;

            var labelRt = locLabel.rectTransform;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            locLabel.fontSize = locLabelEnterFontSize;
            locLabel.alignment = TextAlignmentOptions.Center;

	            string targetName = enterTarget?.locationName;
	            locLabel.text = string.IsNullOrEmpty(targetName)
	                ? "Enter"
	                : $"Enter {targetName}";

            float blink = Mathf.PingPong(Time.time * 1.5f, 1f);
            float ea = 0.45f + blink * 0.55f;
            locLabel.color = new Color(0.47f, 1f, 0.54f, ea);

            // Resize row for a more button-like tap target
            var rRt = locRow.GetComponent<RectTransform>();
            if (rRt != null && rRt.sizeDelta.y < 110f) rRt.sizeDelta = new Vector2(rRt.sizeDelta.x, 110f);
        }
        else
        {
            // Normal teaser row: compass + sentence, no backing, no border, not tappable.
            if (locCompassGO != null && !locCompassGO.activeSelf) locCompassGO.SetActive(true);
            if (locRowBg != null) locRowBg.color = new Color(0f, 0f, 0f, 0f);
            if (locRowBorder != null && locRowBorder.activeSelf) locRowBorder.SetActive(false);
            if (locRowButton != null) locRowButton.interactable = false;

            var labelRt = locLabel.rectTransform;
            labelRt.offsetMin = new Vector2(locLabelNormalOffset.x, locLabelNormalOffset.y);
            labelRt.offsetMax = Vector2.zero;
            locLabel.fontSize = locLabelNormalFontSize;
            locLabel.alignment = TextAlignmentOptions.MidlineLeft;
            locLabel.text = sentence;
            locLabel.color = new Color(activeColor.r, activeColor.g, activeColor.b, textAlpha);

            var rRt = locRow.GetComponent<RectTransform>();
            if (rRt != null && rRt.sizeDelta.y > 30f) rRt.sizeDelta = new Vector2(rRt.sizeDelta.x, 30f);

            // Compass ring — steady
            if (locCompassRing != null)
                locCompassRing.color = new Color(activeColor.r, activeColor.g, activeColor.b, 0.6f);

            // Rotate compass arrow
            if (locArrowRt != null && loc != null)
            {
                float relAngle = RelativeAngleTo(loc, playerMerc);
                locArrowRt.localRotation = Quaternion.Euler(0, 0, -relAngle);
                var arrowImg = locArrowRt.GetComponent<Image>();
                if (arrowImg != null)
                    arrowImg.color = new Color(activeColor.r, activeColor.g, activeColor.b, textAlpha);
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
        pursuitRow.SetActive(false);
    }

    private void UpdateArtifactHUD()
    {
        if (artifactLabel == null || artifactRow == null) return;
        if (!showLegacyArtifactHud)
        {
            artifactRow.SetActive(false);
            return;
        }

        var playerMerc = GetPlayerMercator();

        // Find nearest ambient portal signal (any role except LocationTransmission).
        Signal nearest = null;
        float nearestDist = float.MaxValue;
        for (int i = 0; i < signals.Count; i++)
        {
            var s = signals[i];
            if (s == null) continue;
            if (s.state == SignalState.CoolingDown) continue;
            if (s.role == SignalRole.LocationTransmission) continue;
            if (s.transmissionType == TransmissionType.Location) continue;

	            float d = (float)DistanceTo(s, playerMerc);
	            if (d < nearestDist)
	            {
	                nearestDist = d;
	                nearest = s;
	            }
	        }

	        if (nearest == null)
	        {
            artifactRow.SetActive(!hudSuppressed);
            if (artifactCompassGO != null && artifactCompassGO.activeSelf) artifactCompassGO.SetActive(false);
	            if (artifactRowBg != null) artifactRowBg.color = new Color(0f, 0f, 0f, 0f);
	            if (artifactRowBorder != null && artifactRowBorder.activeSelf) artifactRowBorder.SetActive(false);
	            if (artifactRowButton != null) artifactRowButton.interactable = false;
	            if (artifactDist != null) artifactDist.text = "";
	            var scanRowRt = artifactRow.GetComponent<RectTransform>();
	            if (scanRowRt != null && scanRowRt.sizeDelta.y > 30f) scanRowRt.sizeDelta = new Vector2(scanRowRt.sizeDelta.x, 30f);
	            var scanRt = artifactLabel.rectTransform;
	            scanRt.offsetMin = new Vector2(0f, 0f);
	            scanRt.offsetMax = Vector2.zero;
	            artifactLabel.fontSize = artifactLabelNormalFontSize;
	            artifactLabel.alignment = TextAlignmentOptions.MidlineLeft;
	            float scanBlink = Mathf.PingPong(Time.time * 0.5f, 1f);
            artifactLabel.fontSize = artifactLabelEnterFontSize;   // hero-sized WALK
            artifactLabel.text = "WALK";
            artifactLabel.color = new Color(0.47f, 1f, 0.54f, 0.55f + scanBlink * 0.45f);
            return;
        }

        artifactRow.SetActive(!hudSuppressed);

	        float tBlink = Mathf.PingPong(Time.time * 0.5f, 1f);
	        float textAlpha = 0.4f + tBlink * 0.6f;
	        Color activeColor = showEnter && enterTargetType != TransmissionType.Location ? TeaserRed : TeaserGreen;

	        // Normal artifact row (always visible).
	        if (artifactCompassGO != null && !artifactCompassGO.activeSelf) artifactCompassGO.SetActive(true);
	        if (artifactRowBg != null) artifactRowBg.color = new Color(0f, 0f, 0f, 0f);
	        if (artifactRowBorder != null && artifactRowBorder.activeSelf) artifactRowBorder.SetActive(false);
	        if (artifactRowButton != null) artifactRowButton.interactable = false; // ENTER is handled by the shared enter overlay

	        var normalRt = artifactLabel.rectTransform;
		        normalRt.offsetMin = new Vector2(DisturbanceLabelInset, artifactLabelNormalOffset.y);
		        normalRt.offsetMax = Vector2.zero;
		        artifactLabel.fontSize = artifactLabelNormalFontSize;
		        artifactLabel.alignment = TextAlignmentOptions.MidlineLeft;

	        var rowRt = artifactRow.GetComponent<RectTransform>();
	        if (rowRt != null && rowRt.sizeDelta.y > 30f) rowRt.sizeDelta = new Vector2(rowRt.sizeDelta.x, 30f);

		        if (artifactCompassGO != null)
		        {
		            artifactCompassGO.transform.localScale = Vector3.one * 1.7f;  // bigger arrow/compass
		            var compassRt = artifactCompassGO.transform as RectTransform;
		            if (compassRt != null) compassRt.anchoredPosition = new Vector2(DisturbanceCompassInset, 0f);
		        }
	        float dMeters = nearestDist;
	        artifactLabel.text = "DISTURBANCE";
	        artifactLabel.color = new Color(activeColor.r, activeColor.g, activeColor.b, textAlpha);
	        if (artifactDist != null)
	        {
	            var distRt = artifactDist.rectTransform;
	            distRt.anchorMin = new Vector2(0f, 0f);
	            distRt.anchorMax = new Vector2(0f, 0f);
	            distRt.pivot = new Vector2(0f, 1f);
	            distRt.anchoredPosition = new Vector2(0f, -1f);
	            distRt.sizeDelta = new Vector2(128f, 36f);
	            artifactDist.transform.gameObject.SetActive(true);
	            artifactDist.text = FormatTeaserDistancePlain(dMeters);
	            artifactDist.fontSize = 40f;   // hero-sized distance, kept inside left safe area
	            artifactDist.alignment = TextAlignmentOptions.TopLeft;
	            artifactDist.color = Color.white;
	        }

	        if (artifactArrowRt != null)
	        {
	            float relAngle = RelativeAngleTo(nearest, playerMerc);
	            artifactArrowRt.localRotation = Quaternion.Euler(0, 0, -relAngle);
	            var arrowImg = artifactArrowRt.GetComponent<Image>();
	            if (arrowImg != null)
	                arrowImg.color = new Color(activeColor.r, activeColor.g, activeColor.b, textAlpha);
	        }

	        if (artifactCompassRing != null)
	            artifactCompassRing.color = new Color(activeColor.r, activeColor.g, activeColor.b, 0.6f);
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

        // 1b. Ambient portal beams are proximity collectibles now. Collection
        // should not require pressing the old ENTER/modal flow.
        AutoCollectAmbientPortals(playerMerc);

        // 2. Remove finished signals
        for (int i = signals.Count - 1; i >= 0; i--)
        {
            if (signals[i].state == SignalState.CoolingDown &&
                signals[i].TimeSinceStateChange >= cooldownDuration)
            {
                RemoveSignal(signals[i]);
            }
        }

        // 3. Sync nearby POI location beams (purple)
        SyncLocationSignals(playerMerc);

        // 4. Maintain ambient pool (artifact/transmitter) around the player
        EnsurePrimary();
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

    private static string NormalizeExternalKey(string s)
    {
        return string.IsNullOrWhiteSpace(s) ? null : s.Trim().ToLowerInvariant();
    }

    private static string FirstNameOrNull(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        string trimmed = name.Trim();
        int space = trimmed.IndexOf(' ');
        return space > 0 ? trimmed.Substring(0, space) : trimmed;
    }

    // Stable (non-randomized) 32-bit hash for cross-session keys.
    private static uint Fnv1a32(string s)
    {
        unchecked
        {
            const uint offset = 2166136261u;
            const uint prime = 16777619u;
            uint h = offset;
            for (int i = 0; i < s.Length; i++)
            {
                h ^= s[i];
                h *= prime;
            }
            return h;
        }
    }

    private static string StableIdFromKey(string prefix, string key)
    {
        string k = $"{prefix}:{key}";
        uint h = Fnv1a32(k);
        return h.ToString("x8");
    }

    private void SyncLocationSignals(Vector2d playerMerc)
    {
        if (Time.time - lastLocationSyncTime < 1.0f) return;
        lastLocationSyncTime = Time.time;

        var scanner = TransmitterScanner.Instance;
        if (scanner == null) return;

        float maxMeters = locationBeamMaxMiles * 1609.34f;
        float removeMeters = Mathf.Max(maxMeters, 1f) * 1.05f; // slight hysteresis

        var nearest = scanner.GetNearestUnfiltered(Mathf.Max(1, maxLocationBeams));
        if (nearest == null) return;

        var want = new HashSet<string>();
        var createdThisSync = new HashSet<string>();
        for (int i = 0; i < nearest.Count; i++)
        {
            var t = nearest[i];
            if (t == null) continue;
            if (t.Distance > maxMeters) continue;
            string key = NormalizeExternalKey(t.Name);
            if (string.IsNullOrEmpty(key)) continue;
            want.Add(key);
            bool created;
            EnsureLocationSignal(t, key, out created);
            if (created) createdThisSync.Add(key);
        }

        // Prune location signals that dropped out of range / list
        for (int i = signals.Count - 1; i >= 0; i--)
        {
            var s = signals[i];
            if (s == null) continue;
            if (s.role != SignalRole.LocationTransmission) continue;
            if (s.state == SignalState.CoolingDown) continue;

            string key = NormalizeExternalKey(s.externalKey) ?? NormalizeExternalKey(s.locationName);
            float d = DistanceTo(s, playerMerc);
            bool keep = !string.IsNullOrEmpty(key) && want.Contains(key) && d <= removeMeters;
            // Avoid create-then-immediate-remove thrash: never prune signals created this sync.
        if (!keep && !string.IsNullOrEmpty(key) && createdThisSync.Contains(key))
                keep = true;
            if (!keep)
                RemoveSignal(s);
        }
    }

    private void AutoCollectAmbientPortals(Vector2d playerMerc)
    {
        if (!AmbientPortalsAllowedByActivity()) return;

        Signal best = null;
        float bestDist = float.MaxValue;
        for (int i = 0; i < signals.Count; i++)
        {
            var s = signals[i];
            if (s == null) continue;
            if (s.role != SignalRole.SecondaryNearby) continue;
            if (s.state == SignalState.CoolingDown || s.state == SignalState.Interpreting || s.state == SignalState.Resolved) continue;

            float d = DistanceTo(s, playerMerc);
            if (d <= AmbientCollectRadiusMeters && d < bestDist)
            {
                best = s;
                bestDist = d;
            }
        }

        if (best == null) return;

        string item = !string.IsNullOrWhiteSpace(best.specialItem)
            ? best.specialItem.Trim()
            : (!string.IsNullOrWhiteSpace(best.teaser) ? best.teaser.Trim() : "Rare Earth");
        Debug.Log($"[SignalDirector] Auto-collected ambient portal id={best.id} external={best.externalKey} item='{item}' dist={bestDist:F1}m radius={AmbientCollectRadiusMeters:F1}m");

        PlayAmbientPortalCollectSound();
        MarkAmbientPortalVisited(best);
        ClearEnterStateFor(best);
    }

    private void ClearEnterStateFor(Signal sig)
    {
        if (sig == null) return;
        if (enterTarget == sig) enterTarget = null;
        if (enterCandidate == sig) enterCandidate = null;
        if (locEnterStickySignal == sig)
        {
            locEnterStickySignal = null;
            locEnterFirstShownTime = -1f;
        }
        showEnter = false;
        if (locEnterGO != null && locEnterGO.activeSelf)
        {
            locEnterGO.SetActive(false);
            K1L0HudLayoutController.Refresh();
        }
    }

    private void EnsureLocationSignal(TransmitterScanner.TransmitterData t, string normalizedKey, out bool created)
    {
        created = false;
        // Find existing by external key
        for (int i = 0; i < signals.Count; i++)
        {
            var s = signals[i];
            if (s == null) continue;
            if (s.role != SignalRole.LocationTransmission) continue;
            if (s.state == SignalState.CoolingDown) continue;
            if (!string.IsNullOrEmpty(s.externalKey) && string.Equals(s.externalKey, normalizedKey, StringComparison.Ordinal))
            {
                // Update metadata (best-effort)
                s.locationName = t.Name;
                s.locationCategory = t.MainCategoryGroup;
                s.latitude = t.GeoLocation.x;
                s.longitude = t.GeoLocation.y;
                s.mercatorPosition = Conversions.LatitudeLongitudeToWebMercator(new LatitudeLongitude(s.latitude, s.longitude));
                if (!string.IsNullOrWhiteSpace(t.ArtifactLabel)) s.specialItem = t.ArtifactLabel;
                if (!string.IsNullOrWhiteSpace(t.ArtifactContainer)) s.artifactContainer = t.ArtifactContainer;
                if (!string.IsNullOrWhiteSpace(t.ArtifactSenderName)) s.character = t.ArtifactSenderName;
                if (s.state == SignalState.Hidden) TransitionTo(s, SignalState.Visible);
                return;
            }
        }

        // Create new
        var sig = new Signal
        {
            id = StableIdFromKey("loc", normalizedKey),
            role = SignalRole.LocationTransmission,
            type = SignalType.Presence,
            transmissionType = TransmissionType.Location,
            state = SignalState.Visible,
            mercatorPosition = Conversions.LatitudeLongitudeToWebMercator(new LatitudeLongitude(t.GeoLocation.x, t.GeoLocation.y)),
            latitude = t.GeoLocation.x,
            longitude = t.GeoLocation.y,
            spawnTime = Time.time,
            lastStateChange = Time.time,
            locationName = t.Name,
            locationCategory = t.MainCategoryGroup,
            teaser = "",
            specialItem = !string.IsNullOrWhiteSpace(t.ArtifactLabel) ? t.ArtifactLabel : "",
            artifactContainer = !string.IsNullOrWhiteSpace(t.ArtifactContainer) ? t.ArtifactContainer : "",
            character = !string.IsNullOrWhiteSpace(t.ArtifactSenderName) ? t.ArtifactSenderName : "",
            externalKey = normalizedKey
        };

        signals.Add(sig);
        OnSignalSpawned?.Invoke(sig);
        OnSignalStateChanged?.Invoke(sig);
        created = true;
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

        UpdateBeamAudit(playerMerc);

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
            if (!string.IsNullOrEmpty(lastBeamAuditLine))
                dsb.AppendLine(lastBeamAuditLine);
            debugText.text = dsb.ToString();
        }
    }

    private void UpdateBeamAudit(Vector2d playerMerc)
    {
        if (Time.time < nextBeamAuditTime) return;
        nextBeamAuditTime = Time.time + 10f;

        // Ambient ring beams are SecondaryNearby with a valid ring index.
        var ringDists = new List<(int ring, float dist)>();
        var playerLL = Conversions.WebMercatorToLatitudeLongitude(playerMerc);
        for (int i = 0; i < signals.Count; i++)
        {
            var s = signals[i];
            if (s == null) continue;
            if (s.role != SignalRole.SecondaryNearby) continue;
            if (s.state == SignalState.CoolingDown) continue;
            if (s.poolRingIndex < 1) continue;
            float d = (float)GeoDistanceMeters(playerLL.Latitude, playerLL.Longitude, s.latitude, s.longitude);
            ringDists.Add((s.poolRingIndex, d));
        }

        ringDists.Sort((a, b) => a.ring.CompareTo(b.ring));

        float nearest = float.MaxValue;
        for (int i = 0; i < ringDists.Count; i++)
            if (ringDists[i].dist < nearest) nearest = ringDists[i].dist;

        float step = Mathf.Max(1f, ambientRingStepMeters);
        float threshold = step * 0.85f; // used for "two rings too close" heuristic

        // Primary check: do any beams have a stored ring index that doesn't match their current distance ring?
        bool problem = false;
        string problemDetail = "";
        for (int i = 0; i < ringDists.Count; i++)
        {
            int storedRing = ringDists[i].ring;
            float dist = ringDists[i].dist;
            int computedRing = ComputeAmbientRingIndex(dist, step, ambientRingMaxCount);
            if (computedRing != storedRing)
            {
                problem = true;
                problemDetail = $"ring mismatch stored={storedRing} computed={computedRing} dist={dist:F0}m";
                break;
            }
        }

        // Secondary check: if stored rings match, detect suspicious clustering (two rings closer than one step).
        if (!problem)
        {
            for (int i = 0; i < ringDists.Count; i++)
            {
                for (int j = i + 1; j < ringDists.Count; j++)
                {
                    int ringA = ringDists[i].ring;
                    int ringB = ringDists[j].ring;
                    if (ringA == ringB) continue;
                    float delta = Mathf.Abs(ringDists[i].dist - ringDists[j].dist);
                    if (delta < threshold)
                    {
                        problem = true;
                        problemDetail = $"rings {ringA}/{ringB} too close Δ={delta:F0}m (<{threshold:F0}m) ({ringDists[i].dist:F0}m vs {ringDists[j].dist:F0}m)";
                        break;
                    }
                }
                if (problem) break;
            }
        }

        if (problem)
        {
            lastBeamAuditLine = $"PORTAL AUDIT (10s): PROBLEM — {problemDetail}";
            Debug.LogWarning($"[BeamAudit] {lastBeamAuditLine}");
        }
        else
        {
            // Keep it short on-screen; the full list is noisy.
            string missingNearby = (nearest > 75f || ringDists.Count == 0) ? "  missing nearby" : "";
            lastBeamAuditLine = $"PORTAL AUDIT (10s): ok (rings={ringDists.Count}){missingNearby}";
            Debug.Log($"[BeamAudit] {lastBeamAuditLine}");
        }

        if (beamAuditText != null)
        {
            bool warn = problem;
            beamAuditText.color = warn ? new Color(1f, 0.35f, 0.25f, 0.98f) : new Color(0.47f, 1f, 0.54f, 0.95f);
            beamAuditText.text = lastBeamAuditLine;
        }

        lastSettingsBeamDebugText =
            $"RING DEBUG\n{lastBeamAuditLine}\n" +
            $"signals={signals.Count} roads={roadPoints?.Count ?? 0} nearest={(nearest == float.MaxValue ? "none" : nearest.ToString("F0") + "m")}";
    }

    public string GetBeamDebugTextForSettings()
    {
        return string.IsNullOrWhiteSpace(lastSettingsBeamDebugText)
            ? "RING DEBUG\nPORTAL AUDIT: waiting..."
            : lastSettingsBeamDebugText;
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
                case SignalRole.SecondaryNearby:
                    if (useConcentricAmbientPool && sig.poolRingIndex >= 1)
                    {
                        float step = Mathf.Max(5f, ambientRingStepMeters);
                        float r = sig.poolRingIndex * step;
                        minD = Mathf.Max(0f, r - step * 0.5f);
                        maxD = r + step * 0.5f;
                    }
                    else
                    {
                        minD = secondaryMinDist;
                        maxD = secondaryMaxDist;
                    }
                    break;
                case SignalRole.DistantBackground:minD = distantMinDist;  maxD = distantMaxDist;   break;
                default: continue;
            }
            var oldPos = sig.mercatorPosition;
            if (!TryPickRoadPointInRing(playerMerc, minD, maxD, out var snappedPos))
            {
                Debug.LogWarning($"[SignalDirector] Re-snap skipped {sig.role} {sig.id}: no road point in {minD:F0}-{maxD:F0}m ring");
                continue;
            }
            sig.mercatorPosition = snappedPos;
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
	        if (useConcentricAmbientPool) return; // no primary pursuit in pool mode
	        if (GetPrimary() != null) return;
	        var playerMerc = GetPlayerMercator();
		        SpawnSignal(SignalRole.PrimaryPursuit, SignalType.Presence, playerMerc,
		                    primaryMinDist, primaryMaxDist, null,
		                    TransmissionType.Artifact);
	    }

		    private void FillSecondaries()
		    {
            if (!AmbientPortalsAllowedByActivity())
            {
                _ambientPortalsWereAllowed = false;
                if (!loggedAmbientBlockedByKilosync || Time.unscaledTime >= nextAmbientBlockedLogTime)
                {
                    nextAmbientBlockedLogTime = Time.unscaledTime + 15f;
                    string pedState = pedometerService != null
                        ? $"stepCount={pedometerService.stepCount} kilosync={pedometerService.kilosyncSteps} sixHour={pedometerService.stepsLast6Hours} sessionSteps={pedometerService.walkWindowSteps}/{ambientMinStepsToSpawn} currentBucket={pedometerService.walkCurrentBucketSteps}st/{pedometerService.walkCurrentBucketMeters:F0}m inactive={pedometerService.walkCurrentBucketInactive} bucketMinutes={pedometerService.walkInactiveBucketMinutes} activeBuckets={pedometerService.walkActiveBuckets} bucketReady={pedometerService.walkBucketReady} bucketWalking={pedometerService.HasWalkingBucketSignal(Mathf.Max(0, ambientMinStepsToSpawn))} ready={pedometerService.kilosyncReady} inert={pedometerService.isKilosyncInert}"
                        : "pedometer=null";
                    Debug.Log($"[SignalDirector] Ambient portals blocked: {pedState}. Walk enough in the last 25 minutes to enable spawning.");
                    loggedAmbientBlockedByKilosync = true;
                }
                return;
            }
            if (!_ambientPortalsWereAllowed)
            {
                _ambientPortalsWereAllowed = true;
                _lastBackendBeamRefreshTime = -999f;
                _lastBackendScanLat = double.NaN;
                _lastBackendScanLng = double.NaN;
                _lastBackendScanTime = -999f;
                Debug.Log("[SignalDirector] Ambient portals enabled by activity; forcing immediate backend scan.");
            }
            loggedAmbientBlockedByKilosync = false;

            if (useNativeDrivenAmbientBeams)
            {
                return;
            }

		        if (!useConcentricAmbientPool)
		        {
		            var playerMerc = GetPlayerMercator();
		            while (CountByRole(SignalRole.SecondaryNearby) < maxSecondary)
	            {
		                SpawnSignal(SignalRole.SecondaryNearby, SignalType.Presence, playerMerc,
		                            secondaryMinDist, secondaryMaxDist, null,
		                            TransmissionType.Artifact);
	            }
	            return;
	        }

	        var origin = GetPlayerMercator();
	        if (useBackendConcentricBeams)
	        {
	            EnsureBackendConcentricBeams(origin);
	            return;
	        }

	        float poolMaxMeters = Mathf.Max(0f, ambientPoolMaxMiles) * 1609.34f;
	        float step = Mathf.Max(5f, ambientRingStepMeters);
	        int ringCount = Mathf.FloorToInt(poolMaxMeters / step);
	        ringCount = Mathf.Clamp(ringCount, 1, Mathf.Max(1, ambientRingMaxCount));
	        if (ringCount <= 0) return;

	        if (!loggedAmbientPoolConfig)
	        {
	            loggedAmbientPoolConfig = true;
	            Debug.Log($"[SignalDirector] Ambient pool: maxMiles={ambientPoolMaxMiles:F2} step={ambientRingStepMeters:F0} ringCount={ringCount} cap={ambientRingMaxCount} alternate={ambientAlternateArtifactTransmitter}");
	        }

	        // Cleanup legacy ambient beams (from before ring indexing existed).
	        // In pool mode we ONLY want one ambient beam per ring (poolRingIndex 1..N).
	        for (int i = signals.Count - 1; i >= 0; i--)
	        {
	            var s = signals[i];
	            if (s == null) continue;
	            if (s.role != SignalRole.SecondaryNearby) continue;
	            if (s.state == SignalState.CoolingDown) continue;
	            if (s.poolRingIndex >= 1) continue;
	            RemoveSignal(s);
	        }

	        // Remove ambient ring beams that drift too far away.
	        for (int i = signals.Count - 1; i >= 0; i--)
	        {
	            var s = signals[i];
	            if (s == null) continue;
	            if (s.role != SignalRole.SecondaryNearby) continue;
	            if (s.state == SignalState.CoolingDown) continue;
	            float d = DistanceTo(s, origin);
	            if (d > poolMaxMeters * 1.05f) RemoveSignal(s);
	        }

	        // Ensure one beam per ring at ~ringIndex*step.
	        for (int ringIndex = 1; ringIndex <= ringCount; ringIndex++)
	        {
	            bool have = false;
	            for (int i = 0; i < signals.Count; i++)
	            {
	                var s = signals[i];
	                if (s == null) continue;
	                if (s.role != SignalRole.SecondaryNearby) continue;
	                if (s.state == SignalState.CoolingDown) continue;
	                if (s.poolRingIndex == ringIndex) { have = true; break; }
	            }
	            if (have) continue;

	            float r = ringIndex * step;
	            float minD = Mathf.Max(0f, r - step * 0.5f);
	            float maxD = r + step * 0.5f;
		            TransmissionType t = TransmissionType.Artifact;

	            // Strict: if we can't find a road point within the band's ring, skip spawning this ring.
	            if (!TryPickRoadPointInRing(origin, minD, maxD, out var pos)) continue;

	            var sig = SpawnSignalAtPosition(SignalRole.SecondaryNearby, SignalType.Presence, pos, null, t);
	            if (sig != null) sig.poolRingIndex = ringIndex;
	        }
	    }

    public void ApplyNativeWorldNearby(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;

        BackendNearbyBeamsResponse parsed = null;
        try
        {
            parsed = JsonUtility.FromJson<BackendNearbyBeamsResponse>(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SignalDirector] Native world beam parse failed: {e.Message}");
            return;
        }

        if (parsed == null || !parsed.ok || !parsed.includeBeams || parsed.beams == null)
            return;

        StartCoroutine(SyncAndFillBackendRings(parsed, 0, 0));
    }

    private void FillDistant()
    {
        if (useConcentricAmbientPool) return; // pool replaces distant background beams

        var playerMerc = GetPlayerMercator();
        while (CountByRole(SignalRole.DistantBackground) < maxDistant)
            SpawnSignal(SignalRole.DistantBackground, SignalType.Presence, playerMerc,
                        distantMinDist, distantMaxDist, null,
                        TransmissionType.Artifact);
    }

    private void EnsureBackendConcentricBeams(Vector2d originMercator)
    {
        if (APIManager.Instance == null) return;
        if (_backendBeamRequestInFlight) return;
        if (Time.unscaledTime - _lastBackendBeamRefreshTime < Mathf.Max(0.25f, backendBeamRefreshSeconds)) return;

        _lastBackendBeamRefreshTime = Time.unscaledTime;
        var latLon = Conversions.WebMercatorToLatitudeLongitude(originMercator);

        // Movement-gated rescan: only hit backend when we've moved ~50m (plus a slow failsafe timer).
        float since = Time.unscaledTime - _lastBackendScanTime;
        bool timeForFailsafe = since >= Mathf.Max(5f, backendBeamMaxIntervalSeconds);
        bool havePrev = double.IsFinite(_lastBackendScanLat) && double.IsFinite(_lastBackendScanLng);
        bool movedEnough = !havePrev || GeoDistanceMeters(_lastBackendScanLat, _lastBackendScanLng, latLon.Latitude, latLon.Longitude) >= Mathf.Max(5f, backendBeamRescanMeters);
        if (!movedEnough && !timeForFailsafe) return;

        _lastBackendScanLat = latLon.Latitude;
        _lastBackendScanLng = latLon.Longitude;
        _lastBackendScanTime = Time.unscaledTime;
        StartCoroutine(FetchBackendConcentricBeams(latLon.Latitude, latLon.Longitude));
    }

    private IEnumerator FetchBackendConcentricBeams(double latitude, double longitude)
    {
        _backendBeamRequestInFlight = true;

        string payload =
            "{" +
            $"\"latitude\":{latitude}," +
            $"\"longitude\":{longitude}," +
            $"\"maxMiles\":{ambientPoolMaxMiles}," +
            $"\"stepMeters\":{Mathf.Max(5f, ambientRingStepMeters)}," +
            $"\"minDistanceMeters\":{Mathf.Max(100f, ambientRingStepMeters * 0.8f)}," +
            $"\"ttlMinutes\":{Mathf.Clamp(ambientBeamTtlMinutes, 1f, 240f).ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
            $"\"movementBearing\":{(_hasMovementBearing ? _lastMovementBearingDegrees.ToString(System.Globalization.CultureInfo.InvariantCulture) : "null")}" +
            "}";

        bool done = false;
        bool ok = false;
        string resp = null;

        yield return APIManager.Instance.Post("/k1l0/beams/nearby", payload, (success, text) =>
        {
            ok = success;
            resp = text;
            done = true;
        });

        while (!done) yield return null;

        BackendNearbyBeamsResponse parsed = null;
        if (ok && !string.IsNullOrEmpty(resp))
        {
            try { parsed = JsonUtility.FromJson<BackendNearbyBeamsResponse>(resp); }
            catch (Exception e) { Debug.LogWarning($"[SignalDirector] Backend beam parse failed: {e.Message}"); }
        }

        if (parsed != null && parsed.ok && parsed.beams != null)
        {
            yield return SyncAndFillBackendRings(parsed, latitude, longitude);
            if (parsed.fillPending || parsed.beams.Length == 0)
            {
                _lastBackendScanTime = Time.unscaledTime - Mathf.Max(5f, backendBeamMaxIntervalSeconds) + 10f;
                Debug.Log($"[SignalDirector] Backend beam fill pending/empty; retrying nearby scan in ~10s (fillPending={parsed.fillPending}, beams={parsed.beams.Length})");
            }
        }

        _backendBeamRequestInFlight = false;
    }

    private IEnumerator SyncAndFillBackendRings(BackendNearbyBeamsResponse parsed, double latitude, double longitude)
    {
        if (parsed == null || parsed.beams == null) yield break;

        // Backend/native owns which mystery beams exist and are eligible.
        // Unity is just the renderer: preserve every valid returned beam
        // instead of applying a second local ring-count cap.
        var chosen = new List<BackendBeamDoc>(parsed.beams.Length);
        Array.Sort(parsed.beams, (a, b) =>
        {
            double da = a != null ? a.distanceMeters : double.MaxValue;
            double db = b != null ? b.distanceMeters : double.MaxValue;
            return da.CompareTo(db);
        });
        for (int i = 0; i < parsed.beams.Length; i++)
        {
            var b = parsed.beams[i];
            if (b == null || string.IsNullOrEmpty(b.id)) continue;
            // Artifact beams must have real content to be considered valid.
            if (string.Equals(b.type, "artifact", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(b.material) && string.IsNullOrEmpty(b.label) && string.IsNullOrEmpty(b.lore))
                continue;
            chosen.Add(b);
        }

        // The backend now owns shared placement and replenishment. The client only
        // mirrors the returned set so multiple nearby users do not stamp independent
        // ring sets into Firestore or hide valid backend-authored objects locally.
        int ringCount = chosen.Count;
        var chosenByRing = new BackendBeamDoc[ringCount + 1];
        for (int i = 0; i < chosen.Count; i++)
            chosenByRing[i + 1] = chosen[i];

        // Debug: log the chosen per-ring set so we can diagnose "all blue" (artifact) cases.
        int chosenArtifact = 0, chosenTransmitter = 0;
        for (int ringIndex = 1; ringIndex <= ringCount; ringIndex++)
        {
            var b = chosenByRing[ringIndex];
            if (b == null) continue;
            if (string.Equals(b.type, "transmitter", StringComparison.OrdinalIgnoreCase)) chosenTransmitter++;
            else chosenArtifact++;
        }
        Debug.Log($"[SignalDirector] Backend beams mirrored: returned={parsed.beams.Length} selected={chosenArtifact + chosenTransmitter} artifact={chosenArtifact} transmitter={chosenTransmitter}");

        SyncChosenRingBeams(chosenByRing, ringCount);
    }

    private void SyncChosenRingBeams(BackendBeamDoc[] chosenByRing, int ringCount)
    {
        var want = new HashSet<string>();
        for (int ringIndex = 1; ringIndex <= ringCount; ringIndex++)
        {
            var b = chosenByRing[ringIndex];
            if (b == null || string.IsNullOrEmpty(b.id)) continue;
            want.Add(b.id);
        }

        // Remove any old backend beams not present in the latest selection.
        for (int i = signals.Count - 1; i >= 0; i--)
        {
            var s = signals[i];
            if (s == null) continue;
            if (s.role != SignalRole.SecondaryNearby) continue;
            if (s.state == SignalState.CoolingDown) continue;
            // In backend pool mode we ONLY want the selected backend beams.
            // Remove any legacy/local ambient beams (no externalKey) and any stale backend beams.
            if (string.IsNullOrEmpty(s.externalKey) || !want.Contains(s.externalKey))
                RemoveSignal(s);
        }

        for (int ringIndex = 1; ringIndex <= ringCount; ringIndex++)
        {
            var b = chosenByRing[ringIndex];
            if (b == null || string.IsNullOrEmpty(b.id)) continue;

            Signal existing = null;
            for (int j = 0; j < signals.Count; j++)
            {
                var s = signals[j];
                if (s == null) continue;
                if (s.role != SignalRole.SecondaryNearby) continue;
                if (s.state == SignalState.CoolingDown) continue;
                if (string.Equals(s.externalKey, b.id, StringComparison.Ordinal)) { existing = s; break; }
            }

            var ll = new LatitudeLongitude(b.lat, b.lng);
            var merc = Conversions.LatitudeLongitudeToWebMercator(ll);
            TransmissionType t = string.Equals(b.type, "transmitter", StringComparison.OrdinalIgnoreCase)
                ? TransmissionType.Transmitter
                : TransmissionType.Artifact;

            bool isArtifact = t == TransmissionType.Artifact;
            string artifactName = isArtifact && !string.IsNullOrEmpty(b.material) ? b.material : (!string.IsNullOrEmpty(b.label) ? b.label : null);
            string teaser = isArtifact && !string.IsNullOrEmpty(b.material) ? b.material : (!string.IsNullOrEmpty(b.label) ? b.label : (string.IsNullOrEmpty(b.lore) ? "" : b.lore));
            string senderName = !string.IsNullOrEmpty(b.senderName) ? b.senderName : b.artifactSenderName;

            if (existing == null)
            {
                var sig = SpawnSignalAtPositionWithMetadata(SignalRole.SecondaryNearby, SignalType.Presence, merc, null, t, b.id, ringIndex, teaser);
                if (sig == null) continue;
                if (t == TransmissionType.Artifact && !string.IsNullOrEmpty(artifactName))
                    sig.specialItem = artifactName;
                if (t == TransmissionType.Artifact && !string.IsNullOrEmpty(b.container))
                    sig.artifactContainer = b.container;
                if (t == TransmissionType.Artifact && !string.IsNullOrEmpty(senderName))
                    sig.character = senderName;
                Debug.Log($"[SignalDirector] RingBeam spawn ring={ringIndex} type={b.type} tx={t} dist={b.distanceMeters:F1}m id={b.id} label='{b.label}'");
            }
            else
            {
                existing.mercatorPosition = merc;
                existing.latitude = b.lat;
                existing.longitude = b.lng;
                existing.transmissionType = t;
                existing.poolRingIndex = ringIndex;
                existing.teaser = teaser;
                if (t == TransmissionType.Artifact && !string.IsNullOrEmpty(artifactName))
                    existing.specialItem = artifactName;
                if (t == TransmissionType.Artifact)
                    existing.artifactContainer = b.container ?? "";
                if (t == TransmissionType.Artifact && !string.IsNullOrEmpty(senderName))
                    existing.character = senderName;
            }
        }
    }

    private (double lat, double lng) ComputeRingLatLng(double originLat, double originLng, int ringIndex, float stepMeters)
    {
        uint seed = Fnv1a32($"{originLat:F5},{originLng:F5}:r{ringIndex}");
        float ang = ((seed % 1000000) / 1000000f) * Mathf.PI * 2f;
        float r = ringIndex * stepMeters;
        // IMPORTANT: WebMercator coordinates are not 1:1 meters away from the equator; using them as meters
        // compresses rings at mid-latitudes and causes many beams to appear "too close".
        // Use a geodesic offset to keep ring spacing in true meters.
        const double EarthRadiusMeters = 6371000.0;
        double brng = ang; // radians
        double d = r;

        double lat1 = originLat * Math.PI / 180.0;
        double lon1 = originLng * Math.PI / 180.0;
        double dr = d / EarthRadiusMeters;

        double sinLat1 = Math.Sin(lat1);
        double cosLat1 = Math.Cos(lat1);
        double sinDr = Math.Sin(dr);
        double cosDr = Math.Cos(dr);

        double lat2 = Math.Asin(sinLat1 * cosDr + cosLat1 * sinDr * Math.Cos(brng));
        double lon2 = lon1 + Math.Atan2(Math.Sin(brng) * sinDr * cosLat1, cosDr - sinLat1 * Math.Sin(lat2));

        double outLat = lat2 * 180.0 / Math.PI;
        double outLon = lon2 * 180.0 / Math.PI;
        return (outLat, outLon);
    }

    private double GeoDistanceMeters(double lat1, double lng1, double lat2, double lng2)
    {
        double R = 6371000.0;
        double dLat = (lat2 - lat1) * Math.PI / 180.0;
        double dLng = (lng2 - lng1) * Math.PI / 180.0;
        double a =
            Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
            Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
            Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }


    // ───────────────────────────────────────────────────────────
    // Spawning
    // ───────────────────────────────────────────────────────────

	    private Signal SpawnSignal(SignalRole role, SignalType type,
	                               Vector2d origin, float minDist, float maxDist,
	                               string chainParentId,
	                               TransmissionType? forceTransmissionType = null,
	                               Vector2d? avoidPos = null,
	                               float avoidMinDistMeters = 0f)
	    {
	        Vector2d pos = PickPosition(origin, minDist, maxDist, avoidPos, avoidMinDistMeters);
	        var latLon = Conversions.WebMercatorToLatitudeLongitude(pos);

	        var ambientPick = forceTransmissionType ?? TransmissionType.Artifact;
	        if (ambientPick == TransmissionType.Transmitter)
	            ambientPick = TransmissionType.Artifact;

	        var sig = new Signal
	        {
            role = role,
            type = type,
	            transmissionType = ambientPick,
	            state = SignalState.Hidden,
            mercatorPosition = pos,
            latitude = latLon.Latitude,
            longitude = latLon.Longitude,
            spawnTime = Time.time,
            lastStateChange = Time.time,
	            chainParentId = chainParentId
	        };

	        // Artifact beams must use backend-authored material names; no local noun fallback.
	        if (sig.transmissionType == TransmissionType.Artifact && string.IsNullOrWhiteSpace(sig.specialItem))
	        {
	            sig.specialItem = "artifact";
	        }

		        signals.Add(sig);
	        Debug.Log($"[SignalDirector] Spawned {role}/{type} @ dist={DistanceTo(sig, origin):F0}m  id={sig.id}");
	        OnSignalSpawned?.Invoke(sig);
        if (role == SignalRole.SecondaryNearby)
            PlayAmbientPortalSpawnSound();
	        return sig;
	    }

    /// <summary>
    /// Pick a road point within [minDist, maxDist] of origin.
    /// Gathers all road points in that ring, picks one at random.
    /// Falls back to a random ring position if no roads are loaded yet.
    /// </summary>
    private Vector2d PickPosition(Vector2d origin, float minDist, float maxDist, Vector2d? avoidPos = null, float avoidMinDistMeters = 0f)
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
                {
                    if (avoidPos.HasValue && avoidMinDistMeters > 0f)
                    {
                        double ax = roadPoints[i].x - avoidPos.Value.x;
                        double ay = roadPoints[i].y - avoidPos.Value.y;
                        double ad = Math.Sqrt(ax * ax + ay * ay);
                        if (ad < avoidMinDistMeters) continue;
                    }
                    candidates.Add(roadPoints[i]);
                }
            }

            if (candidates.Count > 0)
            {
                var chosen = candidates[UnityEngine.Random.Range(0, candidates.Count)];
                Debug.Log($"[SignalDirector] Snapped to road point ({candidates.Count} candidates in {minDist}-{maxDist}m ring)");
                return chosen;
            }

            Debug.LogWarning($"[SignalDirector] No road points found in {minDist}-{maxDist}m ring ({roadPoints.Count} total road points)");
        }

        // Fallback: random ring position (no roads loaded yet)
        float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float dist  = UnityEngine.Random.Range(minDist, maxDist);
        Debug.LogWarning("[SignalDirector] No road data — using random position");
        var fallback = new Vector2d(
            origin.x + dist * Math.Cos(angle),
            origin.y + dist * Math.Sin(angle)
        );
        if (avoidPos.HasValue && avoidMinDistMeters > 0f)
        {
            double ax = fallback.x - avoidPos.Value.x;
            double ay = fallback.y - avoidPos.Value.y;
            double ad = Math.Sqrt(ax * ax + ay * ay);
            if (ad < avoidMinDistMeters)
                fallback = new Vector2d(origin.x - dist * Math.Cos(angle), origin.y - dist * Math.Sin(angle));
        }
        return fallback;
    }

    private bool TryPickRoadPointInRing(Vector2d origin, float minDist, float maxDist, out Vector2d picked)
    {
        picked = default;
        RefreshRoadCache();
        if (roadPoints == null || roadPoints.Count == 0) return false;

        // Collect candidates strictly within the band.
        var candidates = new List<Vector2d>();
        for (int i = 0; i < roadPoints.Count; i++)
        {
            double dx = roadPoints[i].x - origin.x;
            double dy = roadPoints[i].y - origin.y;
            double d = Math.Sqrt(dx * dx + dy * dy);
            if (d >= minDist && d <= maxDist) candidates.Add(roadPoints[i]);
        }
        if (candidates.Count == 0) return false;
        picked = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        return true;
    }

    private Signal SpawnSignalAtPosition(SignalRole role, SignalType type, Vector2d pos, string chainParentId, TransmissionType transmissionType)
    {
        if (transmissionType == TransmissionType.Transmitter)
            transmissionType = TransmissionType.Artifact;
        var latLon = Conversions.WebMercatorToLatitudeLongitude(pos);
        var sig = new Signal
        {
            role = role,
            type = type,
            transmissionType = transmissionType,
            state = SignalState.Visible,
            mercatorPosition = pos,
            latitude = latLon.Latitude,
            longitude = latLon.Longitude,
            spawnTime = Time.time,
            lastStateChange = Time.time,
            chainParentId = chainParentId
        };

        if (sig.transmissionType == TransmissionType.Artifact && string.IsNullOrWhiteSpace(sig.specialItem))
        {
            sig.specialItem = "artifact";
        }

	        signals.Add(sig);
	        OnSignalSpawned?.Invoke(sig);
	        OnSignalStateChanged?.Invoke(sig);
        if (role == SignalRole.SecondaryNearby)
            PlayAmbientPortalSpawnSound();
	        return sig;
	    }

    // Backend ring beams need metadata set BEFORE events fire so BeamBridge logs
    // the correct ring + external id and visuals can key off teaser.
    private Signal SpawnSignalAtPositionWithMetadata(
        SignalRole role,
        SignalType type,
        Vector2d pos,
        string chainParentId,
        TransmissionType transmissionType,
        string externalKey,
        int poolRingIndex,
        string teaser)
    {
        if (transmissionType == TransmissionType.Transmitter)
            transmissionType = TransmissionType.Artifact;
        var latLon = Conversions.WebMercatorToLatitudeLongitude(pos);
        var sig = new Signal
        {
            role = role,
            type = type,
            transmissionType = transmissionType,
            state = SignalState.Visible,
            mercatorPosition = pos,
            latitude = latLon.Latitude,
            longitude = latLon.Longitude,
            spawnTime = Time.time,
            lastStateChange = Time.time,
            chainParentId = chainParentId,
            externalKey = externalKey,
            poolRingIndex = poolRingIndex,
            teaser = teaser
        };

        if (sig.transmissionType == TransmissionType.Artifact && string.IsNullOrWhiteSpace(sig.specialItem))
        {
            sig.specialItem = "artifact";
        }

        signals.Add(sig);
        OnSignalSpawned?.Invoke(sig);
        OnSignalStateChanged?.Invoke(sig);
        if (role == SignalRole.SecondaryNearby)
            PlayAmbientPortalSpawnSound();
        return sig;
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
        if (signals == null || signals.Count == 0) return;

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
        // Using WebMercator deltas as "meters" overstates distance away from the equator.
        // For any signal with valid lat/lng, prefer a true geodesic distance.
        if (sig != null && double.IsFinite(sig.latitude) && double.IsFinite(sig.longitude) && map != null && map.MapInformation != null)
        {
            var playerLL = map.MapInformation.Position;
            return (float)GeoDistanceMeters(playerLL.Latitude, playerLL.Longitude, sig.latitude, sig.longitude);
        }

        double dx = sig.mercatorPosition.x - mercPos.x;
        double dy = sig.mercatorPosition.y - mercPos.y;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// Find the nearest building to a location signal and cache its XZ bounds.
    /// Returns true if the player is inside OR within <paramref name="maxEdgeDistanceMeters"/>
    /// of the building footprint (XZ). This makes location beams enterable when the
    /// location maps to a building but the signal point isn't on the exact doorway.
    /// </summary>
    private bool IsPlayerNearLocationBuilding(Signal loc, Vector3 playerWorldPos, float maxEdgeDistanceMeters)
    {
        // Refresh building bounds every 5s (buildings shift with floating origin)
        if (loc.buildingBounds == null || Time.time - loc.buildingBoundsTime > 5f)
        {
            loc.buildingBounds = FindBuildingBoundsNear(loc);
            loc.buildingBoundsTime = Time.time;
        }

        if (loc.buildingBounds == null) return false;

        Bounds b = loc.buildingBounds.Value;
        float d = DistanceXZPointToBounds(playerWorldPos, b);
        return d <= Mathf.Max(0f, maxEdgeDistanceMeters);
    }

    private static float DistanceXZPointToBounds(Vector3 point, Bounds bounds)
    {
        float dx = 0f;
        if (point.x < bounds.min.x) dx = bounds.min.x - point.x;
        else if (point.x > bounds.max.x) dx = point.x - bounds.max.x;

        float dz = 0f;
        if (point.z < bounds.min.z) dz = bounds.min.z - point.z;
        else if (point.z > bounds.max.z) dz = point.z - bounds.max.z;

        return Mathf.Sqrt(dx * dx + dz * dz);
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

    /// <summary>Relative angle from device heading to signal (0=ahead, clockwise). Used for arrow rotation.</summary>
    private float RelativeAngleTo(Signal sig, Vector2d playerMerc)
    {
        float absBearing = BearingTo(sig, playerMerc);
        float heading = GetLiveHeadingDegrees();
        return (absBearing - heading + 360f) % 360f;
    }

    private float GetLiveHeadingDegrees()
    {
        if (playerObj != null && (!Application.isMobilePlatform || GPSLocationController.GPSDisabled))
            return playerObj.transform.eulerAngles.y;

        if (Input.compass.enabled && Input.compass.headingAccuracy >= 0f)
            return Input.compass.trueHeading;

        return playerObj != null ? playerObj.transform.eulerAngles.y : 0f;
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

    // Creates a 4-strip border frame that hugs the parent rect's edges. Returns
    // the container GameObject so callers can SetActive(true/false) to flash it.
    private GameObject CreateBorderFrame(Transform parent, Color color, float thickness)
    {
        var holder = new GameObject("EnterBorder");
        holder.transform.SetParent(parent, false);
        var hRt = holder.AddComponent<RectTransform>();
        hRt.anchorMin = Vector2.zero;
        hRt.anchorMax = Vector2.one;
        hRt.offsetMin = Vector2.zero;
        hRt.offsetMax = Vector2.zero;

        // top, bottom, left, right
        for (int i = 0; i < 4; i++)
        {
            var go = new GameObject($"Edge{i}");
            go.transform.SetParent(holder.transform, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            var rt = go.GetComponent<RectTransform>();
            switch (i)
            {
                case 0: // top
                    rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
                    rt.pivot = new Vector2(0.5f, 1f);
                    rt.sizeDelta = new Vector2(0, thickness);
                    rt.anchoredPosition = Vector2.zero;
                    break;
                case 1: // bottom
                    rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0);
                    rt.pivot = new Vector2(0.5f, 0f);
                    rt.sizeDelta = new Vector2(0, thickness);
                    rt.anchoredPosition = Vector2.zero;
                    break;
                case 2: // left
                    rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(0, 1);
                    rt.pivot = new Vector2(0f, 0.5f);
                    rt.sizeDelta = new Vector2(thickness, 0);
                    rt.anchoredPosition = Vector2.zero;
                    break;
                case 3: // right
                    rt.anchorMin = new Vector2(1, 0); rt.anchorMax = new Vector2(1, 1);
                    rt.pivot = new Vector2(1f, 0.5f);
                    rt.sizeDelta = new Vector2(thickness, 0);
                    rt.anchoredPosition = Vector2.zero;
                    break;
            }
        }
        holder.SetActive(false);
        return holder;
    }

    private void SetBorderFrameColor(GameObject holder, Color color)
    {
        if (holder == null) return;
        if (!holder.activeSelf) holder.SetActive(true);
        var edges = holder.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < edges.Length; i++)
        {
            if (edges[i] != null) edges[i].color = color;
        }
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
        float compassSize = 28f;

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

        var distGO = new GameObject("Distance");
        distGO.transform.SetParent(container.transform, false);
        distGO.SetActive(false);
        var distRt = distGO.AddComponent<RectTransform>();
        distRt.anchorMin = new Vector2(0.5f, 0f);
        distRt.anchorMax = new Vector2(0.5f, 0f);
        distRt.pivot = new Vector2(0.5f, 1f);
        distRt.anchoredPosition = new Vector2(0f, -1f);
        distRt.sizeDelta = new Vector2(86f, 14f);
        var distFont = Resources.Load<TMP_FontAsset>("Fonts/IBMPlexMono-Regular SDF");
        if (distFont == null) distFont = TMP_Settings.defaultFontAsset;
        distanceTmp = distGO.AddComponent<TextMeshProUGUI>();
        distanceTmp.font = distFont;
        distanceTmp.fontSize = 10;
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
    /// Returns the signal. If a signal for the same location already exists, updates and returns it.
    /// </summary>
    public Signal SpawnLocationTransmission(double latitude, double longitude,
                                            string name, string category, string teaser,
                                            string specialItem = "")
    {
        string key = NormalizeExternalKey(name);
        if (!string.IsNullOrEmpty(key))
        {
            for (int i = 0; i < signals.Count; i++)
            {
                var s = signals[i];
                if (s == null) continue;
                if (s.role != SignalRole.LocationTransmission) continue;
                if (s.state == SignalState.CoolingDown) continue;
                if (!string.IsNullOrEmpty(s.externalKey) && string.Equals(s.externalKey, key, StringComparison.Ordinal))
                {
                    s.latitude = latitude;
                    s.longitude = longitude;
                    s.mercatorPosition = Conversions.LatitudeLongitudeToWebMercator(new LatitudeLongitude(latitude, longitude));
                    s.locationName = name;
                    s.locationCategory = category;
                    s.teaser = teaser;
                    s.specialItem = specialItem ?? "";
                    if (s.state == SignalState.Hidden) TransitionTo(s, SignalState.Visible);
                    return s;
                }
            }
        }

        var merc = Conversions.LatitudeLongitudeToWebMercator(
            new LatitudeLongitude(latitude, longitude));

        var sig = new Signal
        {
            id = !string.IsNullOrEmpty(key) ? StableIdFromKey("loc", key) : Guid.NewGuid().ToString("N").Substring(0, 8),
            role = SignalRole.LocationTransmission,
            type = SignalType.Presence,
            transmissionType = TransmissionType.Location,
            state = SignalState.Visible,
            mercatorPosition = merc,
            latitude = latitude,
            longitude = longitude,
            spawnTime = Time.time,
            lastStateChange = Time.time,
            locationName = name,
            locationCategory = category,
            teaser = teaser,
            specialItem = specialItem ?? "",
            externalKey = key
        };

        signals.Add(sig);
        Debug.Log($"[SignalDirector] Spawned LocationTransmission '{name}' id={sig.id}");
        OnSignalSpawned?.Invoke(sig);
        OnSignalStateChanged?.Invoke(sig);
        return sig;
    }

    /// <summary>Get the nearest location transmission signal, or null.</summary>
    public Signal GetLocationTransmission()
    {
        return GetNearestLocationTransmission();
    }

    public Signal GetNearestLocationTransmission()
    {
        var playerMerc = GetPlayerMercator();
        Signal best = null;
        float bestDist = float.MaxValue;
        for (int i = 0; i < signals.Count; i++)
        {
            var s = signals[i];
            if (s == null) continue;
            if (s.role != SignalRole.LocationTransmission) continue;
            if (s.state == SignalState.CoolingDown) continue;
            float d = DistanceTo(s, playerMerc);
            if (d < bestDist)
            {
                bestDist = d;
                best = s;
            }
        }
        return best;
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

    private static string FormatSignalGpsTitle(Signal signal)
    {
        if (signal == null) return "gps --, --";
        return $"gps {signal.latitude:F5}, {signal.longitude:F5}";
    }
}
