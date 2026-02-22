using UnityEngine;

/// <summary>
/// TEMP: Minimal boot for editor - bypasses entire boot system to get player + skybox without freeze.
/// Set all flags immediately so RenderManager and Player work.
/// </summary>
public class MinimalBoot : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        return; // Disabled - full boot system is re-enabled
        var go = new GameObject("MinimalBoot");
        DontDestroyOnLoad(go);
        go.AddComponent<MinimalBoot>();
    }

    private void Start()
    {
        StartCoroutine(SetFlagsAfterFrame());
    }

    private System.Collections.IEnumerator SetFlagsAfterFrame()
    {
        yield return null; // Wait one frame for scene to settle
        BootState.SetRenderAllowed();
        BootState.SetGPSAllowed();
        BootState.SetTeleportAllowed();
        BootState.SetMapAllowed();
        BootState.SetPlayerAllowed();
        Debug.Log("[MinimalBoot] All boot flags set - player + skybox should work");
    }
}
