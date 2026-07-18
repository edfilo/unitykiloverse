using UnityEngine;

public static class BootState
{
    public static bool AllowRender { get; private set; }
    public static bool AllowGPS { get; private set; }
    public static bool AllowTeleport { get; private set; }
    public static bool AllowMap { get; private set; }
    public static bool AllowPlayer { get; private set; }

    /// <summary> Set when at least one map tile has been loaded (so boot can complete after tiles on screen). </summary>
    public static bool FirstTilesLoaded { get; private set; }
    private static int _completedBuildingTiles;
    private static int _completedRoadTiles;
    private static float _lastInitialLayerCompletionTime = -1f;
    private static float _firstTilesLoadedTime = -1f;

    public static int CompletedBuildingTiles => _completedBuildingTiles;
    public static int CompletedRoadTiles => _completedRoadTiles;
    public static bool InitialMapLayersSettled
    {
        get
        {
            bool trackedLayersSettled = _completedBuildingTiles > 0 && _completedRoadTiles > 0 &&
                _lastInitialLayerCompletionTime >= 0f &&
                Time.realtimeSinceStartup - _lastInitialLayerCompletionTime >= 1.5f;
            // Some native map paths visibly finish but do not invoke the legacy
            // per-layer completion hook. Do not leave the marquee loading forever.
            bool visibleMapFallback = FirstTilesLoaded && _firstTilesLoadedTime >= 0f &&
                Time.realtimeSinceStartup - _firstTilesLoadedTime >= 8f;
            return trackedLayersSettled || visibleMapFallback;
        }
    }
    public static bool InitialRenderReady => InitialMapLayersSettled && SignalBeamBridge.InitialPopulationReady;

    public static void MarkTileRenderComplete(string layerName)
    {
        if (string.IsNullOrEmpty(layerName)) return;
        if (layerName.IndexOf("building", System.StringComparison.OrdinalIgnoreCase) >= 0)
            _completedBuildingTiles++;
        else if (layerName.IndexOf("road", System.StringComparison.OrdinalIgnoreCase) >= 0)
            _completedRoadTiles++;
        else
            return;
        _lastInitialLayerCompletionTime = Time.realtimeSinceStartup;
    }
    public static void SetFirstTilesLoaded()
    {
        if (FirstTilesLoaded) return;
        FirstTilesLoaded = true;
        _firstTilesLoadedTime = Time.realtimeSinceStartup;
        BootDiagnostics.Mark("BootState FirstTilesLoaded");
    }

    /// <summary> In editor, heavy systems must not run until this many seconds after boot complete. Prevents main-thread pile-up and lockup. </summary>
    private static float _postBootGraceEndTime;
    public static bool PostBootGracePeriodElapsed =>
        !Application.isEditor || _postBootGraceEndTime <= 0f || Time.realtimeSinceStartup >= _postBootGraceEndTime;

    /// <summary> Editor: time when AllowPlayer was set. Scripts can skip heavy work for 2s after this (avoids lockup from pile-up after broken-up boot). </summary>
    public static float AllowPlayerTime { get; private set; }

    public static void SetRenderAllowed()
    {
        AllowRender = true;
        BootDiagnostics.Mark("BootState AllowRender");
    }

    public static void SetGPSAllowed()
    {
        AllowGPS = true;
        BootDiagnostics.Mark("BootState AllowGPS");
    }

    public static void SetTeleportAllowed()
    {
        AllowTeleport = true;
        BootDiagnostics.Mark("BootState AllowTeleport");
    }

    public static void SetMapAllowed()
    {
        AllowMap = true;
        BootDiagnostics.Mark("BootState AllowMap");
    }

    public static void SetPlayerAllowed()
    {
        AllowPlayer = true;
#if UNITY_EDITOR
        AllowPlayerTime = Time.realtimeSinceStartup;
        _postBootGraceEndTime = Time.realtimeSinceStartup + 8f; // 8s grace: no heavy work so editor stays responsive
#endif
        BootDiagnostics.Mark("BootState AllowPlayer");
    }
}
