using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;

// Forces every TMP_Text to use the Martian Mono font asset and renders white.
// Many UI scripts set .color = green/yellow/etc. every frame, so we re-sweep
// on a steady cadence rather than once at boot. The font asset's material
// already carries the drop shadow.
public class ForceWhiteText : MonoBehaviour
{
    private static ForceWhiteText _instance;
    private static TMP_FontAsset _martianMono;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (_instance != null) return;
        _martianMono = Resources.Load<TMP_FontAsset>("Fonts & Materials/MartianMono SDF");
        if (_martianMono == null)
        {
            Debug.LogWarning("[ForceWhiteText] MartianMono SDF font asset not found in Resources — falling back to white-only sweep");
        }
        var go = new GameObject("ForceWhiteText");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<ForceWhiteText>();
    }

    void Start()
    {
        StartCoroutine(SweepLoop());
        SceneManager.sceneLoaded += (s, m) => Sweep();
    }

    IEnumerator SweepLoop()
    {
        yield return null;
        Sweep();
        yield return new WaitForSeconds(0.5f);
        Sweep();
        var wait = new WaitForSeconds(0.25f);
        while (true) { Sweep(); yield return wait; }
    }

    static void Sweep()
    {
        var all = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            var t = all[i];
            if (_martianMono != null && t.font != _martianMono) t.font = _martianMono;
            if (t.color != Color.white) t.color = Color.white;
            if (t.enableVertexGradient) t.enableVertexGradient = false;
        }
    }
}
