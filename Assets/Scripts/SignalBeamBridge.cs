using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

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
        if (suppressed && Instance != null) Instance.HideAllDistanceLabels();
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

    private class BeamLabelEntry
    {
        public GameObject go;
        public RectTransform rt;
        public TextMeshProUGUI tmp;
    }

    private class BeamLabelCandidate
    {
        public BeamLabelEntry label;
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
        if (uiCanvas == null) uiCanvas = K1L0CanvasRoot.WorldCanvas;

        var go = new GameObject($"BeamMeters_{signalId}", typeof(RectTransform));
        go.transform.SetParent(uiCanvas != null ? uiCanvas.transform : transform, false);

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(140f, 44f);
        rt.pivot = new Vector2(0.5f, 0f);

        // Solid black backing plate for readability (debug)
        var bg = go.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.72f);
        bg.raycastTarget = false;

        var textGO = new GameObject("Text", typeof(RectTransform));
        textGO.transform.SetParent(go.transform, false);
        var textRt = textGO.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(6f, 4f);
        textRt.offsetMax = new Vector2(-6f, -4f);

        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = "";
        tmp.fontSize = 16f;
        tmp.color = new Color(1f, 1f, 1f, 0.95f);
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        tmp.fontStyle = FontStyles.Bold;
        tmp.outlineWidth = 0.55f;
        tmp.outlineColor = new Color32(0, 0, 0, 220);

        labelsBySignalId[signalId] = new BeamLabelEntry { go = go, rt = rt, tmp = tmp };
    }

    private void UpdateBeamDistanceLabels(IReadOnlyList<Signal> signals)
    {
        if (hudSuppressed || !ShowDistanceLabels)
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
            Vector3 worldPos = baseWorld + Vector3.up * 120f; // higher above the beam base for debugging
            Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);

            bool onScreen = screenPos.z > 0 &&
                            screenPos.x > 0 && screenPos.x < Screen.width &&
                            screenPos.y > 0 && screenPos.y < Screen.height;

            if (onScreen != lbl.go.activeSelf) lbl.go.SetActive(onScreen);
            if (!onScreen) continue;

            float distM = director.DistanceToSignal(sig);
            float w = lbl.rt != null ? lbl.rt.sizeDelta.x : 140f;
            float h = lbl.rt != null ? lbl.rt.sizeDelta.y : 44f;
            var rect = new Rect(screenPos.x - w * 0.5f, screenPos.y, w, h);
            candidates.Add(new BeamLabelCandidate
            {
                label = lbl,
                screenPos = screenPos,
                distanceMeters = distM,
                screenRect = rect
            });
        }

        candidates.Sort((a, b) => a.distanceMeters.CompareTo(b.distanceMeters));

        var occupied = new List<Rect>();
        for (int i = 0; i < candidates.Count; i++)
        {
            var c = candidates[i];
            bool overlaps = false;
            for (int j = 0; j < occupied.Count; j++)
            {
                if (c.screenRect.Overlaps(occupied[j]))
                {
                    overlaps = true;
                    break;
                }
            }

            if (overlaps)
            {
                if (c.label.go.activeSelf) c.label.go.SetActive(false);
                continue;
            }

            if (!c.label.go.activeSelf) c.label.go.SetActive(true);
            occupied.Add(c.screenRect);

            if (c.label.tmp != null)
                c.label.tmp.text = $"{Mathf.RoundToInt(c.distanceMeters)}m";
            if (c.label.rt != null)
                c.label.rt.position = new Vector3(c.screenPos.x, c.screenPos.y, 0f);
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
