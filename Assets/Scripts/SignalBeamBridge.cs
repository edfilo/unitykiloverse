using UnityEngine;
using System.Collections.Generic;

// ─────────────────────────────────────────────────────────────────
// SignalBeamBridge  –  Visual layer for SignalDirectorV2
// ─────────────────────────────────────────────────────────────────
// Subscribes to SignalDirectorV2 events, instantiates BeamAvatar
// GameObjects, and repositions them every frame (floating origin).
// ─────────────────────────────────────────────────────────────────

public class SignalBeamBridge : MonoBehaviour
{
    public static SignalBeamBridge Instance { get; private set; }

    private SignalDirectorV2 director;
    private readonly Dictionary<string, GameObject> beamsBySignalId = new Dictionary<string, GameObject>();
    private Transform container;

    // Prefab + material (loaded same way as VirtualGridSpawner)
    private GameObject beamPrefab;
    private Material particleBeamMaterial;
    private KiloWorld.Rendering.KiloWorldMasterProfile profile;

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

        // Profile
        profile = KiloWorld.Rendering.Systems.RenderManager.Instance?.profile;
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
            ApplyRoleAppearance(orb, sig.role);
            orb.RebuildBeamSystem();
        }

        beamsBySignalId[sig.id] = go;
        sig.visualGO = go;

        Debug.Log($"[SignalBeamBridge] Created beam for {sig.role} {sig.id}");
    }

    private void HandleRemoved(Signal sig)
    {
        if (beamsBySignalId.TryGetValue(sig.id, out var go))
        {
            if (go != null) Destroy(go);
            beamsBySignalId.Remove(sig.id);
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
    }

    private void ApplyRoleAppearance(BeamAvatar orb, SignalRole role)
    {
        // Differentiate roles visually by scale and opacity for now.
        // Primary is full size, secondary smaller, distant smallest.
        switch (role)
        {
            case SignalRole.PrimaryPursuit:
                // Full size — no changes needed
                break;
            case SignalRole.SecondaryNearby:
                orb.orbSize *= 0.6f;
                orb.particleCount = Mathf.RoundToInt(orb.particleCount * 0.5f);
                orb.beamHeight *= 0.4f;
                break;
            case SignalRole.DistantBackground:
                orb.orbSize *= 0.4f;
                orb.particleCount = Mathf.RoundToInt(orb.particleCount * 0.3f);
                orb.beamHeight *= 0.25f;
                break;
            case SignalRole.LocationTransmission:
                // Slightly smaller than primary, distinct blue-ish tint
                orb.orbSize *= 0.8f;
                orb.particleCount = Mathf.RoundToInt(orb.particleCount * 0.7f);
                orb.glowColor = new Color(0.3f, 0.6f, 1f, 1f);
                orb.particleEmissionColor = new Color(0.4f, 0.7f, 1f, 1f);
                break;
        }
    }
}
