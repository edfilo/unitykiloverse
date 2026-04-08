using UnityEngine;
using System;

/// <summary>
/// Global app mode. Systems check K1L0Mode.Current to adapt behavior.
/// Discovery = zoomed-out map view. Walk = first-person street level.
/// </summary>
public static class K1L0Mode
{
    public enum Mode
    {
        Discovery,  // Zoomed-out isometric/top-down map view
        Walk        // First-person street-level view
    }

    public static Mode Current { get; private set; } = Mode.Discovery;

    /// <summary>Fires when mode changes. Arg is the new mode.</summary>
    public static event Action<Mode> OnModeChanged;

    public static void Set(Mode mode)
    {
        if (Current == mode) return;
        Current = mode;
        Debug.Log($"[K1L0Mode] Switched to {mode}");
        OnModeChanged?.Invoke(mode);
    }

    public static bool IsDiscovery => Current == Mode.Discovery;
    public static bool IsWalk => Current == Mode.Walk;
}
