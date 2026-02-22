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
    public static void SetFirstTilesLoaded()
    {
        if (FirstTilesLoaded) return;
        FirstTilesLoaded = true;
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
