using UnityEngine;

public class PlayerBootGate : MonoBehaviour
{
    private KiloFirstPersonController _player;
    private bool _disabled;
    private int _searchFrames;
    private static bool _loggedPostBoot;
    private static float _allowPlayerTime = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        var go = new GameObject("PlayerBootGate");
        DontDestroyOnLoad(go);
        go.AddComponent<PlayerBootGate>();
    }

    private void Update()
    {
        if (BootState.AllowPlayer && !_loggedPostBoot)
        {
            _loggedPostBoot = true;
            _allowPlayerTime = Time.realtimeSinceStartup;
            BootDiagnostics.Mark("PlayerBootGate first Update after AllowPlayer");
        }
        if (_player == null)
        {
#if UNITY_EDITOR
            // Editor: don't run Find for 3s after AllowPlayer - FindFirstObjectByType can lock the editor in big scenes
            if (_allowPlayerTime >= 0f && (Time.realtimeSinceStartup - _allowPlayerTime) < 3f)
                return;
#endif
            // Throttle Find (expensive in big scenes) - only search every 20 frames to avoid post-boot lockup
            _searchFrames++;
            if (_searchFrames < 20) return;
            _searchFrames = 0;
            _player = FindFirstObjectByType<KiloFirstPersonController>();
        }

        if (_player == null) return;

        if (!BootState.AllowPlayer)
        {
            if (!_disabled)
            {
                _player.enabled = false;
                _disabled = true;
                BootDiagnostics.Mark("PlayerBootGate disabled");
            }
            return;
        }

        // Editor: wait for post-boot grace period so we don't trigger heavy Update() pile-up
        if (Application.isEditor && !BootState.PostBootGracePeriodElapsed)
        {
            return;
        }

        if (_disabled)
        {
            _player.enabled = true;
            _disabled = false;
            BootDiagnostics.Mark("PlayerBootGate enabled (first frame after boot)");
        }
    }
}
