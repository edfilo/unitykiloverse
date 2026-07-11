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
            if (!particlePlayingStates.ContainsKey(particles))
                particlePlayingStates.Add(particles, particles.isPlaying);
            particles.Pause(true);
            SuppressRenderer(particles.GetComponent<ParticleSystemRenderer>());
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

        rendererStates.Clear();
        particlePlayingStates.Clear();
    }
}
