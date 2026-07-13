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
        "TransmitterScanner", "UserPresenceManager", "UnicornPugManager"
    };
    private bool skyMode;
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
            SuppressWorld();
            nextRefresh = Time.unscaledTime + 0.5f;
        }
        else
        {
            RestoreWorld();
        }
    }

    private void SuppressWorld()
    {
        var buildings = GameObject.Find("building layer objects");
        if (buildings != null)
        {
            foreach (var renderer in buildings.GetComponentsInChildren<Renderer>(true))
                SuppressRenderer(renderer);
        }

        foreach (var particles in FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (particles.GetComponentInParent<SkyWeatherVolume>() != null) continue;
            if (!particlePlayingStates.ContainsKey(particles))
                particlePlayingStates.Add(particles, particles.isPlaying);
            particles.Pause(true);
            SuppressRenderer(particles.GetComponent<ParticleSystemRenderer>());
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

        // Mapbox can stream renderers while a native panel is open. Those may
        // first be observed disabled and therefore cannot rely solely on the
        // captured state when the player returns to Map.
        var buildings = GameObject.Find("building layer objects");
        if (buildings != null)
            foreach (var renderer in buildings.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = true;

        rendererStates.Clear();
        particlePlayingStates.Clear();
        behaviourStates.Clear();
    }
}
