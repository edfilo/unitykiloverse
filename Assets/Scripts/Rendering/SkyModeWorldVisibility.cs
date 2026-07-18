using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Keeps the dynamic sky alive in Sky Mode while hiding costly/irrelevant map
/// geometry and particle fountains. State is restored exactly on return.
/// </summary>
public sealed class SkyModeWorldVisibility : MonoBehaviour
{
    private static SkyModeWorldVisibility instance;
    private readonly Dictionary<Renderer, bool> rendererStates = new();
    private readonly Dictionary<ParticleSystem, bool> particlePlayingStates = new();
    private readonly Dictionary<Behaviour, bool> behaviourStates = new();
    private static readonly HashSet<string> SuspendedTypes = new()
    {
        "K1L0LocationBeams", "POILabelBridge", "BeamTapDetector",
        "TransmitterScanner", "UserPresenceManager", "UnicornPugManager",
        "BeamAvatar", "SignalBeamBridge", "SignalDirectorV2", "VirtualGridSpawner",
        "VolumetricFog", "VolumetricFogManager"
    };
    private static readonly string[] HiddenMapRoots =
    {
        "building layer objects", "road layer objects", "land layer objects",
        "water layer objects", "poi_label layer objects", "POI_Anchors"
    };
    private bool skyMode;
    private bool savedRenderSettingsFog;
    private bool hasSavedFogState;
    private GameObject worldLabelRoot;
    private bool savedWorldLabelRootActive;
    private bool hasSavedWorldLabelRootState;
    private float nextRefresh;

    public static void SetSkyMode(bool enabled)
    {
        EnsureInstance().Apply(enabled);
    }

    private static SkyModeWorldVisibility EnsureInstance()
    {
        if (instance != null) return instance;
        var host = new GameObject("Sky Mode World Visibility");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<SkyModeWorldVisibility>();
        return instance;
    }

    private void Update()
    {
        if (!skyMode || Time.unscaledTime < nextRefresh) return;
        SuppressWorld();
        nextRefresh = Time.unscaledTime + 0.5f;
    }

    private void Apply(bool enabled)
    {
        if (skyMode == enabled) return;
        skyMode = enabled;
        if (enabled)
        {
            savedRenderSettingsFog = RenderSettings.fog;
            hasSavedFogState = true;
            RenderSettings.fog = false;
            SuppressHudLabels();
            SuppressWorld();
            nextRefresh = Time.unscaledTime + 0.5f;
        }
        else
        {
            RestoreWorld();
            RestoreHudLabels();
        }
    }

    private void SuppressHudLabels()
    {
        // Hide existing labels synchronously before their update producers are
        // disabled. Otherwise their final screen coordinates remain visible.
        SignalBeamBridge.SetHudSuppressed(true);
        POILabelBridge.SetHudSuppressed(true);
        var director = SignalDirectorV2.Instance;
        if (director != null) director.SuppressHUD(true);

        var world = K1L0CanvasRoot.World;
        worldLabelRoot = world != null ? world.gameObject : null;
        if (worldLabelRoot != null)
        {
            savedWorldLabelRootActive = worldLabelRoot.activeSelf;
            hasSavedWorldLabelRootState = true;
            worldLabelRoot.SetActive(false);
        }
    }

    private void RestoreHudLabels()
    {
        if (hasSavedWorldLabelRootState && worldLabelRoot != null)
            worldLabelRoot.SetActive(savedWorldLabelRootActive);

        // Re-enable producers first (RestoreWorld), then release suppression so
        // their next Update computes fresh positions from the map camera.
        SignalBeamBridge.SetHudSuppressed(false);
        POILabelBridge.SetHudSuppressed(false);
        var director = SignalDirectorV2.Instance;
        if (director != null) director.SuppressHUD(false);

        worldLabelRoot = null;
        hasSavedWorldLabelRootState = false;
    }

    private void SuppressWorld()
    {
        // RenderSettings fog and Volumetric Fog & Mist are separate systems.
        // Keep both off throughout sky mode; their prior enabled states are
        // restored below along with the other suspended behaviours.
        RenderSettings.fog = false;

        // Hide every streamed ground-map layer. We leave the objects alive so
        // their tile state remains warm, but removing their renderers eliminates
        // the map draw cost while the camera is looking only at the sky.
        foreach (string rootName in HiddenMapRoots)
        {
            var root = GameObject.Find(rootName);
            if (root == null) continue;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                SuppressRenderer(renderer);
        }

        // BeamAvatar owns both particle plumes and projector/item quads. Target
        // it explicitly so weather, stars, and other sky particle systems keep
        // simulating and rendering in Sky Mode.
        foreach (var beam in FindObjectsByType<BeamAvatar>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (beam == null) continue;
            foreach (var renderer in beam.GetComponentsInChildren<Renderer>(true))
                SuppressRenderer(renderer);
            foreach (var particles in beam.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (!particlePlayingStates.ContainsKey(particles))
                    particlePlayingStates.Add(particles, particles.isPlaying);
                particles.Pause(true);
            }
        }

        foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (behaviour == null || !SuspendedTypes.Contains(behaviour.GetType().Name)) continue;
            if (!behaviourStates.ContainsKey(behaviour)) behaviourStates.Add(behaviour, behaviour.enabled);
            behaviour.enabled = false;
        }
    }

    private void SuppressRenderer(Renderer renderer)
    {
        if (renderer == null) return;
        if (!rendererStates.ContainsKey(renderer))
            rendererStates.Add(renderer, renderer.enabled);
        renderer.enabled = false;
    }

    private void RestoreWorld()
    {
        foreach (var entry in rendererStates)
            if (entry.Key != null) entry.Key.enabled = entry.Value;

        foreach (var entry in particlePlayingStates)
            if (entry.Key != null && entry.Value) entry.Key.Play(true);

        foreach (var entry in behaviourStates)
            if (entry.Key != null) entry.Key.enabled = entry.Value;

        if (hasSavedFogState)
            RenderSettings.fog = savedRenderSettingsFog;

        rendererStates.Clear();
        particlePlayingStates.Clear();
        behaviourStates.Clear();
        hasSavedFogState = false;
    }
}
