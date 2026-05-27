using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Firebase.Database;

// ─────────────────────────────────────────────────────────────────
// TransmissionManager  –  Connects SignalDirectorV2 to the Story Engine
// ─────────────────────────────────────────────────────────────────
// When the primary signal spawns → primes a new story on the server.
// When the primary enters ReadyToInterpret → fires generate-shot.
// Listens via Firebase RTDB SSE for the shot to flip to "ready", then reveals.
// Auto-bootstraps alongside SignalDirectorV2 (DontDestroyOnLoad).
// ─────────────────────────────────────────────────────────────────

public class TransmissionManager : MonoBehaviour
{
    public static TransmissionManager Instance { get; private set; }

    // ── "Pursuit" story (drives HUD label/teaser). Points into storyStates. ─
    private string activeStoryId;
    private string pendingTeaser;
    private bool isPriming;

    // ── Per-story state keyed by storyId ──────────────────
    private class StoryState
    {
        public string storyId;
        public string character;
        public string objectName;
        public string premise;
        public int deliveredShotCount; // how many shots we've already dispatched to UI
        public string lastProgressKey; // dedupe OnShotProgress events
        // Per-shot videoUrl snapshot at last delivery — lets us re-fire
        // OnTransmissionReady when video lands after the still was already shown.
        public readonly Dictionary<int, string> deliveredVideoUrl = new Dictionary<int, string>();
    }
    private readonly Dictionary<string, StoryState> storyStates = new Dictionary<string, StoryState>();

    // signal.id → storyId (so HandleLocationEnter / shot gen knows which story belongs to the signal)
    private readonly Dictionary<string, string> signalStoryMap = new Dictionary<string, string>();
    // storyId → signal metadata snapshot (so UI doesn't drift when the primary chains)
    private readonly Dictionary<string, Signal> storySignalSnapshot = new Dictionary<string, Signal>();

    // ── Location transmission state ───────────────────────
    private Signal activeLocationSignal;
    private float locationSpawnTime;
    private const float LOCATION_CHECK_INTERVAL = 3f;
    private const float LOCATION_REFRESH_INTERVAL = 1200f; // 20 minutes
    private const float LOCATION_MAX_DISTANCE = 1600f; // ~1 mile
    private const float PROXIMITY_SWAP_DISTANCE = 10f; // if a beam is this close and isn't current loc, swap to it

    // ── Tracking which signals we've already handled ──────
    private readonly HashSet<string> primedSignals = new HashSet<string>();
    private readonly HashSet<string> enteredSignals = new HashSet<string>();
    private readonly HashSet<string> shotTriggeredSignals = new HashSet<string>();
    // Location signals that already have a generated shot — re-entering should
    // re-show the cached transmission via the existing Firebase listener,
    // not regenerate another shot. Keyed by locationName (lowercased) so a
    // re-spawned Sheetz signal (new GUID, same place) reuses the same story.
    private readonly Dictionary<string, string> locationStoryByName = new Dictionary<string, string>();
    // Location keys whose prime is currently in flight — prevents a second
    // Location ENTER (during the prime window) from racing in and creating a
    // duplicate story for the same place.
    private readonly HashSet<string> primingLocationKeys = new HashSet<string>();

    // ── Items (Artifacts) — RTDB users/<userId>/items ─────
    private readonly List<string> cachedItems = new List<string>();
    private DatabaseReference itemsRef;
    private EventHandler<ValueChangedEventArgs> itemsHandler;
    private string itemsUserId;

    static string LocationKey(Signal sig)
    {
        if (sig == null) return "";
        // Only POI-bound signals get a name-based cache key. Pursuit/secondary
        // beams may carry the "INCOMING TRANSMISSION" HUD placeholder which
        // would otherwise collide every ambient beam onto one cache slot.
        if (sig.role != SignalRole.LocationTransmission) return sig.id;
        return string.IsNullOrEmpty(sig.locationName) ? sig.id : sig.locationName.Trim().ToLowerInvariant();
    }

    static string LocationDropKey(Signal sig)
    {
        string raw = LocationKey(sig);
        if (string.IsNullOrWhiteSpace(raw)) raw = sig != null ? sig.id : "unknown";
        var sb = new StringBuilder(raw.Length);
        for (int i = 0; i < raw.Length; i++)
        {
            char c = raw[i];
            if (char.IsLetterOrDigit(c) || c == '_' || c == '-') sb.Append(char.ToLowerInvariant(c));
            else if (char.IsWhiteSpace(c)) sb.Append('_');
        }
        return sb.Length > 0 ? sb.ToString() : "unknown";
    }
    // Stories whose generate-shot request is queued, waiting for prime to
    // finish in RTDB. The Firebase listener fires generate-shot the instant
    // primeStatus → ready (observed via state.character being set).
    // Value carries the location context so the deferred call still includes it.
    private struct PendingShot
    {
        public string signalId;
        public string locationName;
        public string locationCategory;
        public string transmissionType; // "location" | "artifact" | "transmitter"
        public string artifact;         // optional override (transmitter flow)
        public string userAction;       // optional user instruction (transmitter flow)
    }
    private readonly Dictionary<string, PendingShot> pendingShotForStory = new Dictionary<string, PendingShot>();

    // ── Events (for UI) ───────────────────────────────────
    public event Action<TransmissionData> OnTransmissionReady;
    public event Action<string> OnTeaserUpdated; // next_teaser text
    public event Action<string, string> OnStoryPrimed; // character, premise  (pursuit)
    public event Action<string, string, string, string> OnStoryShellReady; // storyId, character, object, premise (any story)
    // storyId, shotNumber, status ("generating"|"image_ready"|"rendering_video"|"ready"), hasImage, hasAudio
    public event Action<string, int, string, bool, bool> OnShotProgress;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoBootstrap()
    {
        if (Instance != null) return;

        // Wait for SignalDirectorV2 to exist
        var director = SignalDirectorV2.Instance;
        if (director == null)
        {
            // SignalDirectorV2 bootstraps itself too; we'll attach to the same GO
            // Retry next frame via a temporary helper
            var helper = new GameObject("TransmissionBootHelper").AddComponent<BootHelper>();
            DontDestroyOnLoad(helper.gameObject);
            return;
        }

        AttachToDirector(director);
    }

    static void AttachToDirector(SignalDirectorV2 director)
    {
        if (Instance != null) return;
        var tm = director.gameObject.AddComponent<TransmissionManager>();
        Debug.Log("[TransmissionManager] Attached to SignalDirectorV2 GO");
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        var director = SignalDirectorV2.Instance;
        if (director == null)
        {
            Debug.LogWarning("[TransmissionManager] No SignalDirectorV2 found");
            return;
        }

        director.OnSignalSpawned += HandleSignalSpawned;
        director.OnSignalStateChanged += HandleSignalStateChanged;
        director.OnPrimaryChained += HandlePrimaryChained;
        director.OnLocationEnter += HandleLocationEnter;
        director.OnSignalRemoved += HandleSignalRemoved;

        Debug.Log("[TransmissionManager] Subscribed to SignalDirectorV2 events");

        // Memo: do not create/prime stories until the user ENTERs a beam (or hits SEND in transmitter UI).
        // Disabled: location signals are now synced directly by SignalDirectorV2 from TransmitterScanner.

        BeginItemsListener();
        CleanupUnenteredBeamsOnInit();
    }

    IEnumerator CheckExistingPrimary()
    {
        // Retry periodically — signals may spawn before API is connected,
        // and API auto-connect can take 30s+ if tethered IP is wrong
        for (int attempt = 0; attempt < 30; attempt++)
        {
            yield return new WaitForSeconds(attempt < 6 ? 3f : 10f);

            if (!string.IsNullOrEmpty(activeStoryId)) yield break; // already primed via event

            var director = SignalDirectorV2.Instance;
            if (director == null) continue;

            foreach (var sig in director.ActiveSignals)
            {
                if (sig.role == SignalRole.PrimaryPursuit && !primedSignals.Contains(sig.id))
                {
                    Debug.Log($"[TransmissionManager] Found existing primary {sig.id} (attempt {attempt+1}), priming story");
                    primedSignals.Add(sig.id);
                    yield return PrimeStoryCoroutine(sig);
                    if (!string.IsNullOrEmpty(activeStoryId)) yield break; // success
                    // Failed — allow retry
                    primedSignals.Remove(sig.id);
                    break;
                }
            }
        }

        Debug.Log("[TransmissionManager] CheckExistingPrimary exhausted retries");
    }

    void OnDestroy()
    {
        StopShotListener();
        StopItemsListener();
        if (SignalDirectorV2.Instance != null)
        {
            SignalDirectorV2.Instance.OnSignalSpawned -= HandleSignalSpawned;
            SignalDirectorV2.Instance.OnSignalStateChanged -= HandleSignalStateChanged;
            SignalDirectorV2.Instance.OnPrimaryChained -= HandlePrimaryChained;
            SignalDirectorV2.Instance.OnLocationEnter -= HandleLocationEnter;
            SignalDirectorV2.Instance.OnSignalRemoved -= HandleSignalRemoved;
        }
    }

    void BeginItemsListener()
    {
        FirebaseBootstrap.WhenReady(() =>
        {
            string uid = GetUserId();
            if (string.IsNullOrWhiteSpace(uid)) return;
            string safeUid = SanitizeUserId(uid);
            if (itemsRef != null && itemsUserId == safeUid) return;
            if (itemsRef != null) StopItemsListener();

            string path = $"users/{safeUid}/items";
            var db = FirebaseDatabase.DefaultInstance;
            itemsRef = db.GetReference(path);
            itemsUserId = safeUid;
            itemsHandler = (sender, args) =>
            {
                if (args.DatabaseError != null) return;
                if (args.Snapshot == null) return;

                cachedItems.Clear();
                foreach (var child in args.Snapshot.Children)
                {
                    if (child == null) continue;
                    string a = child.Child("artifact")?.Value?.ToString();
                    if (!string.IsNullOrWhiteSpace(a)) cachedItems.Add(a.Trim());
                }

                cachedItems.Sort(StringComparer.OrdinalIgnoreCase);
                Debug.Log($"[TransmissionManager] Items updated: {cachedItems.Count}");

                // If transmitter modal is open, refresh its inventory list.
                var modal = TransmitterEnterModal.Instance;
                if (modal != null) modal.RefreshInventory();
            };
            itemsRef.ValueChanged += itemsHandler;
        });
    }

    void CleanupUnenteredBeamsOnInit()
    {
        FirebaseBootstrap.WhenReady(() =>
        {
            string uid = GetUserId();
            if (string.IsNullOrWhiteSpace(uid)) return;

            var db = FirebaseDatabase.DefaultInstance;
            string path = $"users/{SanitizeUserId(uid)}/beams";
            var beamsRef = db.GetReference(path);

            beamsRef.GetValueAsync().ContinueWith(task =>
            {
                if (task == null || !task.IsCompletedSuccessfully) return;
                var snap = task.Result;
                if (snap == null || !snap.Exists) return;

                foreach (var child in snap.Children)
                {
                    if (child == null) continue;
                    bool entered = false;
                    try
                    {
                        var ev = child.Child("entered")?.Value;
                        if (ev is bool b) entered = b;
                        else if (ev != null) bool.TryParse(ev.ToString(), out entered);
                    }
                    catch { /* ignore */ }

                    if (!entered)
                        beamsRef.Child(child.Key).RemoveValueAsync();
                }
            });
        });
    }

    void StopItemsListener()
    {
        if (itemsRef != null && itemsHandler != null)
            itemsRef.ValueChanged -= itemsHandler;
        itemsRef = null;
        itemsHandler = null;
        itemsUserId = null;
    }

    // ── Beams (Live pool) — RTDB users/<userId>/beams/<signalId> ─────

    void UpsertBeamRecord(Signal sig, string storyId, bool entered)
    {
        if (sig == null) return;
        string uid = GetUserId();
        if (string.IsNullOrWhiteSpace(uid)) return;

        FirebaseBootstrap.WhenReady(() =>
        {
            var db = FirebaseDatabase.DefaultInstance;
            string path = $"users/{SanitizeUserId(uid)}/beams/{sig.id}";
            var beamRef = db.GetReference(path);

            string t = (sig.transmissionType == TransmissionType.Location) ? "location"
                : (sig.transmissionType == TransmissionType.Artifact ? "artifact" : "transmitter");

            var payload = new Dictionary<string, object>
            {
                { "id", sig.id },
                { "type", t },
                { "lat", sig.latitude },
                { "lng", sig.longitude },
                { "externalKey", sig.externalKey ?? "" },
                { "locationName", sig.locationName ?? "" },
                { "locationCategory", sig.locationCategory ?? "" },
                { "artifact", sig.specialItem ?? "" },
                { "entered", entered },
                { "storyId", storyId ?? "" },
                { "updatedAt", ServerValue.Timestamp }
            };

            // Create createdAt only once (best-effort).
            beamRef.Child("createdAt").GetValueAsync().ContinueWith(task =>
            {
                if (task == null || !task.IsCompletedSuccessfully) return;
                if (task.Result == null || !task.Result.Exists)
                    beamRef.Child("createdAt").SetValueAsync(ServerValue.Timestamp);
                beamRef.UpdateChildrenAsync(payload);
            });
        });
    }

    void DeleteBeamRecord(string signalId)
    {
        if (string.IsNullOrWhiteSpace(signalId)) return;
        string uid = GetUserId();
        if (string.IsNullOrWhiteSpace(uid)) return;

        FirebaseBootstrap.WhenReady(() =>
        {
            var db = FirebaseDatabase.DefaultInstance;
            string path = $"users/{SanitizeUserId(uid)}/beams/{signalId}";
            db.GetReference(path).RemoveValueAsync();
        });
    }

    void AttachStoryToBeamRecord(string signalId, string storyId)
    {
        if (string.IsNullOrWhiteSpace(signalId)) return;
        if (string.IsNullOrWhiteSpace(storyId)) return;
        string uid = GetUserId();
        if (string.IsNullOrWhiteSpace(uid)) return;

        FirebaseBootstrap.WhenReady(() =>
        {
            var db = FirebaseDatabase.DefaultInstance;
            string path = $"users/{SanitizeUserId(uid)}/beams/{signalId}";
            var beamRef = db.GetReference(path);
            var payload = new Dictionary<string, object>
            {
                { "storyId", storyId },
                { "entered", true },
                { "updatedAt", ServerValue.Timestamp }
            };
            beamRef.UpdateChildrenAsync(payload);
        });
    }

    // ── Signal event handlers ─────────────────────────────

    void HandleSignalSpawned(Signal sig)
    {
        if (sig == null) return;

        // Every visible beam is part of the live pool — persist to RTDB with no storyId.
        UpsertBeamRecord(sig, storyId: null, entered: false);
    }

    void HandleSignalRemoved(Signal sig)
    {
        if (sig == null) return;
        if (enteredSignals.Contains(sig.id)) return; // keep entered beams
        DeleteBeamRecord(sig.id);
    }

    void HandleSignalStateChanged(Signal sig)
    {
        if (sig.role != SignalRole.PrimaryPursuit) return;

        // When signal becomes ReadyToInterpret, generate a shot for ITS story
        // (not the currently-active pursuit, which may have chained past it).
        if (sig.state == SignalState.ReadyToInterpret && !shotTriggeredSignals.Contains(sig.id))
        {
            shotTriggeredSignals.Add(sig.id);
            string sid = GetStoryIdForSignal(sig.id);
            if (!string.IsNullOrEmpty(sid))
            {
                StartCoroutine(GenerateShotCoroutine(sig));
            }
        }
    }

    void HandlePrimaryChained(Signal resolved, Signal newPrimary)
    {
        Debug.Log($"[TransmissionManager] Primary chained: {resolved.id} → {newPrimary.id}");
        // The new primary's spawn event will trigger a new story prime
        // But we want to CONTINUE the existing story, not start a new one
        // So we mark it as already primed and just generate the next shot
        if (!string.IsNullOrEmpty(activeStoryId))
        {
            primedSignals.Add(newPrimary.id);
            // Don't prime a new story — continue the chain
        }
    }

    // ── API Coroutines ────────────────────────────────────

    string GetUserId()
    {
        if (DeviceIDManager.Instance != null)
            return DeviceIDManager.Instance.GetCurrentUserId();
        return "k1l0_anonymous";
    }

    /// <summary>
    /// Get approximate GPS coordinates for the API call.
    /// </summary>
    (double lat, double lng) GetPlayerGPS()
    {
        var player = UnityEngine.Object.FindFirstObjectByType<KiloFirstPersonController>();
        if (player != null)
            return (player.playerGPS.Latitude, player.playerGPS.Longitude);
        return (0, 0);
    }

    IEnumerator PrimeStoryCoroutine(Signal sig)
    {
        if (isPriming) yield break;
        isPriming = true;

        var (lat, lng) = GetPlayerGPS();
        // Only real POI signals (LocationTransmission) carry a real locationName.
        // Pursuit/Secondary/Distant beams may have a placeholder ("INCOMING
        // TRANSMISSION") stamped by EnsureLocationMetadata for HUD display —
        // never let that placeholder reach the prompt.
        bool isLocationSignal = sig != null && sig.role == SignalRole.LocationTransmission;
        var body = JsonUtility.ToJson(new PrimeRequest
        {
            userId = GetUserId(),
            location = $"{lat:F4},{lng:F4}",
            coordinates = new Coords { latitude = lat, longitude = lng },
            locationName = isLocationSignal ? sig.locationName : null,
            locationCategory = isLocationSignal ? sig.locationCategory : null
        });

        Debug.Log($"[TransmissionManager] Priming story for signal {sig.id} (loc='{(isLocationSignal ? sig.locationName : "")}', role={sig?.role})");

        string responseText = null;
        bool success = false;

        if (APIManager.Instance != null)
        {
            yield return APIManager.Instance.Post("/story/prime", body, (ok, resp) => {
                success = ok;
                responseText = resp;
            });
        }

        if (success && !string.IsNullOrEmpty(responseText))
        {
            var resp = JsonUtility.FromJson<PrimeResponse>(responseText);
            if (resp.ok)
            {
                activeStoryId = resp.storyId;
                signalStoryMap[sig.id] = resp.storyId;
                storySignalSnapshot[resp.storyId] = sig;
                if (!storyStates.ContainsKey(resp.storyId))
                    storyStates[resp.storyId] = new StoryState { storyId = resp.storyId };
                Debug.Log($"[TransmissionManager] Story shell primed: {activeStoryId} (awaiting character via Firebase)");
                UpdatePursuitHUD("...", null);
                // Open a dedicated listener for THIS story. Multiple stories can
                // listen concurrently (e.g. user entered a frame on the previous
                // pursuit while a new pursuit chains in).
                BeginStoryListener(resp.storyId);
            }
            else
            {
                Debug.LogWarning($"[TransmissionManager] Prime failed: {responseText}");
            }
        }
        else
        {
            Debug.LogWarning($"[TransmissionManager] Prime request failed: {responseText}");
        }

        isPriming = false;
    }

    IEnumerator GenerateShotCoroutine(Signal sig)
    {
        // Prefer the signal's own story; fall back to the current pursuit story.
        string storyId = null;
        bool sigOwnsStory = false;
        if (sig != null && signalStoryMap.TryGetValue(sig.id, out storyId) && !string.IsNullOrEmpty(storyId))
            sigOwnsStory = true;
        if (string.IsNullOrEmpty(storyId)) storyId = activeStoryId;
        if (string.IsNullOrEmpty(storyId)) yield break;

        // Only inherit the signal's location context when:
        //  (a) the signal *owns* this story (vs. falling back to active ambient), AND
        //  (b) the signal is a real POI (LocationTransmission), not a pursuit beam
        //      stamped with the "INCOMING TRANSMISSION" placeholder.
        // Ambient beams stay independent of nearby POIs no matter what.
        bool sigIsRealLocation = sig != null && sig.role == SignalRole.LocationTransmission;
        var pending = new PendingShot
        {
            signalId = sig != null ? sig.id : null,
            locationName = sigOwnsStory && sigIsRealLocation ? sig.locationName : null,
            locationCategory = sigOwnsStory && sigIsRealLocation ? sig.locationCategory : null,
            transmissionType = sig != null
                ? sig.transmissionType.ToString().ToLowerInvariant()
                : "artifact",
            artifact = sig != null ? sig.specialItem : null,
            userAction = null
        };

        // Make sure the listener is open so we observe prime completion.
        BeginStoryListener(storyId);

        // If prime is already ready (character known), fire immediately.
        // Otherwise queue — the Firebase listener will fire when prime lands.
        if (storyStates.TryGetValue(storyId, out var state) && !string.IsNullOrEmpty(state.character))
        {
            yield return FireGenerateShot(storyId, pending);
        }
        else
        {
            pendingShotForStory[storyId] = pending;
            Debug.Log($"[TransmissionManager] Queued generate-shot for {storyId} (loc='{pending.locationName}') — waiting for prime via RTDB");
        }
    }

    IEnumerator FireGenerateShot(string storyId, PendingShot pending)
    {
        var (lat, lng) = GetPlayerGPS();
        if (!string.IsNullOrEmpty(pending.signalId))
            AttachStoryToBeamRecord(pending.signalId, storyId);

        var body = JsonUtility.ToJson(new GenerateShotRequest
        {
            userId = GetUserId(),
            storyId = storyId,
            location = $"{lat:F4},{lng:F4}",
            coordinates = new Coords { latitude = lat, longitude = lng },
            locationName = pending.locationName,
            locationCategory = pending.locationCategory,
            transmissionType = string.IsNullOrEmpty(pending.transmissionType) ? "artifact" : pending.transmissionType,
            artifact = string.IsNullOrEmpty(pending.artifact) ? null : pending.artifact,
            userAction = string.IsNullOrEmpty(pending.userAction) ? null : pending.userAction
        });

        Debug.Log($"[TransmissionManager] Generating shot for {storyId} (loc='{pending.locationName}')");

        bool shotSuccess = false;
        if (APIManager.Instance != null)
        {
            yield return APIManager.Instance.Post("/story/generate-shot", body, (ok, resp) => {
                shotSuccess = ok;
            });
        }

        if (!shotSuccess)
        {
            Debug.LogWarning($"[TransmissionManager] Generate-shot failed for {storyId}");
        }
    }

    IEnumerator TransmitAndGenerateForTransmitter(Signal sig, string artifact, string userAction)
    {
        if (isPriming) yield break;
        isPriming = true;

        var (lat, lng) = GetPlayerGPS();
        var (placeName, placeCategory) = GetNearbyPlaceContext(lat, lng);
        if (sig != null && !string.IsNullOrWhiteSpace(sig.locationName))
        {
            placeName = sig.locationName.Trim();
            if (!string.IsNullOrWhiteSpace(sig.locationCategory))
                placeCategory = sig.locationCategory.Trim();
        }
        var body = JsonUtility.ToJson(new PrimeTransmitterRequest
        {
            userId = GetUserId(),
            location = $"{lat:F4},{lng:F4}",
            coordinates = new Coords { latitude = lat, longitude = lng },
            locationName = placeName,
            locationCategory = placeCategory,
            transmissionType = "transmitter",
            artifact = artifact,
            userAction = userAction
        });

        string responseText = null;
        bool success = false;
        if (APIManager.Instance != null)
        {
            yield return APIManager.Instance.Post("/story/transmit", body, (ok, resp) => {
                success = ok;
                responseText = resp;
            });
        }

        if (success && !string.IsNullOrEmpty(responseText))
        {
            var resp = JsonUtility.FromJson<PrimeResponse>(responseText);
            if (resp.ok && !string.IsNullOrEmpty(resp.storyId))
            {
                activeStoryId = resp.storyId;
                signalStoryMap[sig.id] = resp.storyId;
                storySignalSnapshot[resp.storyId] = sig;
                if (!storyStates.ContainsKey(resp.storyId))
                    storyStates[resp.storyId] = new StoryState { storyId = resp.storyId };

                sig.specialItem = artifact;

                var frame = TransmissionFrame.Instance;
                if (frame != null)
                    frame.ShowLoading("TRANSMISSION", "ambient", resp.storyId);

                BeginStoryListener(resp.storyId);

                UpdatePursuitHUD("...", null);
            }
            else
            {
                Debug.LogWarning($"[TransmissionManager] Transmitter transmit failed: {responseText}");
            }
        }
        else
        {
            Debug.LogWarning($"[TransmissionManager] Transmitter transmit request failed: {responseText}");
        }

        isPriming = false;
    }

    // If the player is effectively "at" a POI location beam while transmitting, include it as context.
    private (string name, string category) GetNearbyPlaceContext(double playerLat, double playerLng)
    {
        var dir = SignalDirectorV2.Instance;
        if (dir == null) return (null, null);

        string bestName = null;
        string bestCat = null;
        double bestDist = double.MaxValue;

        var list = dir.ActiveSignals;
        for (int i = 0; i < list.Count; i++)
        {
            var s = list[i];
            if (s == null) continue;
            if (s.role != SignalRole.LocationTransmission) continue;
            if (string.IsNullOrWhiteSpace(s.locationName)) continue;
            if (!double.IsFinite(s.latitude) || !double.IsFinite(s.longitude)) continue;

            double d = HaversineMeters(playerLat, playerLng, s.latitude, s.longitude);
            if (d < bestDist)
            {
                bestDist = d;
                bestName = s.locationName;
                bestCat = s.locationCategory;
            }
        }

        // Only attach place context when you're truly "at" the place (prevents random nearby POIs leaking in).
        if (bestDist <= 25.0) return (bestName, bestCat);
        return (null, null);
    }

    private static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371000.0;
        double dLat = (lat2 - lat1) * System.Math.PI / 180.0;
        double dLon = (lon2 - lon1) * System.Math.PI / 180.0;
        double a =
            System.Math.Sin(dLat / 2) * System.Math.Sin(dLat / 2) +
            System.Math.Cos(lat1 * System.Math.PI / 180.0) * System.Math.Cos(lat2 * System.Math.PI / 180.0) *
            System.Math.Sin(dLon / 2) * System.Math.Sin(dLon / 2);
        double c = 2 * System.Math.Atan2(System.Math.Sqrt(a), System.Math.Sqrt(1 - a));
        return R * c;
    }

    // ── Firebase RTDB listener (replaces old polling) ────────────────
    // Firebase path can't contain . # $ [ ] — mirror server's sanitizeUserId.
    static string SanitizeUserId(string id)
    {
        if (string.IsNullOrEmpty(id)) return "";
        var sb = new StringBuilder(id.Length);
        for (int i = 0; i < id.Length; i++)
        {
            char c = id[i];
            if (c == '.' || c == '#' || c == '$' || c == '[' || c == ']') sb.Append('_');
            else sb.Append(c);
        }
        return sb.ToString();
    }

    // Track active Firebase listeners so we can detach them cleanly.
    private readonly Dictionary<string, DatabaseReference> _storyRefs = new Dictionary<string, DatabaseReference>();
    private readonly Dictionary<string, EventHandler<ValueChangedEventArgs>> _storyHandlers = new Dictionary<string, EventHandler<ValueChangedEventArgs>>();

    // Opens (or reuses) a Firebase RTDB ValueChanged listener for the given storyId.
    // Multiple concurrent listeners are supported — a new pursuit chain does
    // NOT stop prior listeners, so pending shots still get delivered.
    void BeginStoryListener(string storyId)
    {
        if (string.IsNullOrEmpty(storyId)) return;
        if (_storyRefs.ContainsKey(storyId)) return; // already listening

        FirebaseBootstrap.WhenReady(() =>
        {
            if (_storyRefs.ContainsKey(storyId)) return;
            string path = $"users/{SanitizeUserId(GetUserId())}/stories/{storyId}";
            Debug.Log($"[TransmissionManager] Opening Firebase listener on {path}");

            var db = FirebaseDatabase.DefaultInstance;
            var reference = db.GetReference(path);
            EventHandler<ValueChangedEventArgs> handler = (sender, args) =>
            {
                if (args.DatabaseError != null)
                {
                    Debug.LogWarning($"[TransmissionManager] RTDB error on {path}: {args.DatabaseError.Message}");
                    return;
                }
                if (args.Snapshot == null || !args.Snapshot.Exists) return;
                StartCoroutine(FetchAndDeliver(storyId));
            };
            reference.ValueChanged += handler;
            _storyRefs[storyId] = reference;
            _storyHandlers[storyId] = handler;
        });
    }

    void StopStoryListener(string storyId)
    {
        if (string.IsNullOrEmpty(storyId)) return;
        if (_storyRefs.TryGetValue(storyId, out var reference) &&
            _storyHandlers.TryGetValue(storyId, out var handler))
        {
            reference.ValueChanged -= handler;
        }
        _storyRefs.Remove(storyId);
        _storyHandlers.Remove(storyId);
    }

    void StopShotListener()
    {
        foreach (var kv in _storyRefs)
        {
            if (_storyHandlers.TryGetValue(kv.Key, out var handler))
                kv.Value.ValueChanged -= handler;
        }
        _storyRefs.Clear();
        _storyHandlers.Clear();
    }

    IEnumerator FetchAndDeliver(string storyId)
    {
        if (APIManager.Instance == null) yield break;
        if (string.IsNullOrEmpty(storyId)) yield break;

        var userId = GetUserId();
        var queryParams = $"?userId={UnityWebRequest.EscapeURL(userId)}&storyId={UnityWebRequest.EscapeURL(storyId)}";

        string pollResponse = null;
        bool pollOk = false;
        yield return APIManager.Instance.Get($"/story/status{queryParams}", (ok, resp) => {
            pollOk = ok;
            pollResponse = resp;
        });

        if (!pollOk || string.IsNullOrEmpty(pollResponse)) yield break;

        var resp = JsonUtility.FromJson<StatusResponse>(pollResponse);
        if (!resp.ok) yield break;

        if (!storyStates.TryGetValue(storyId, out var state))
        {
            state = new StoryState { storyId = storyId };
            storyStates[storyId] = state;
        }

        // Character arrived (or updated) via background LLM.
        if (!string.IsNullOrEmpty(resp.character) && resp.character != state.character)
        {
            state.character = resp.character;
            state.objectName = resp.artifact;
            state.premise = resp.premise;
            Debug.Log($"[TransmissionManager] Character arrived ({storyId}): {state.character} seeks \"{state.objectName}\"");
            OnStoryShellReady?.Invoke(storyId, state.character, state.objectName, state.premise);

            // Write the character onto the bound location signal so it carries
            // the same narrative metadata as ambient transmissions do via the
            // primary pursuit signal (see SetPursuitLabel).
            if (activeLocationSignal != null)
            {
                activeLocationSignal.character = state.character;
                if (string.IsNullOrEmpty(activeLocationSignal.specialItem))
                    activeLocationSignal.specialItem = state.objectName;
            }

            // Only update the pursuit HUD if this is the pursuit story.
            if (storyId == activeStoryId)
            {
                OnStoryPrimed?.Invoke(state.character, state.premise);
                UpdatePursuitHUD(state.character, pendingTeaser);
            }

            // Prime is now ready — flush any queued generate-shot for this story.
            if (pendingShotForStory.TryGetValue(storyId, out var queued))
            {
                pendingShotForStory.Remove(storyId);
                Debug.Log($"[TransmissionManager] Prime ready, firing queued generate-shot for {storyId} (loc='{queued.locationName}')");
                StartCoroutine(FireGenerateShot(storyId, queued));
            }
        }

        // Deliver any newly-ready shots in order for this story.
        if (resp.shots != null)
        {
            while (state.deliveredShotCount < resp.shots.Length)
            {
                var shot = resp.shots[state.deliveredShotCount];
                // Deliver as soon as there's a still to show — video arrives later
                // and triggers a re-fire below. generating/queued → hold.
                if (!IsShotVisible(shot)) break;

                state.deliveredShotCount++;
                state.deliveredVideoUrl[shot.shotNumber] = shot.videoUrl ?? "";

                Debug.Log($"[TransmissionManager] Transmission #{shot.shotNumber} visible ({storyId}, status={shot.status})");

                DispatchTransmission(storyId, state, shot);
            }

            // Re-fire delivery for any already-delivered shot whose videoUrl
            // just landed (image was shown as placeholder; swap to video now).
            for (int i = 0; i < state.deliveredShotCount && i < resp.shots.Length; i++)
            {
                var shot = resp.shots[i];
                if (string.IsNullOrEmpty(shot.videoUrl)) continue;
                string prev;
                if (state.deliveredVideoUrl.TryGetValue(shot.shotNumber, out prev) && prev == shot.videoUrl) continue;
                state.deliveredVideoUrl[shot.shotNumber] = shot.videoUrl;
                Debug.Log($"[TransmissionManager] Transmission #{shot.shotNumber} video ready ({storyId})");
                DispatchTransmission(storyId, state, shot);
            }

            // After draining ready shots, the next shot (if any) is the one
            // currently being generated. Emit its progress so the UI can show
            // a concrete stage label.
            if (state.deliveredShotCount < resp.shots.Length)
            {
                var inFlight = resp.shots[state.deliveredShotCount];
                string progressKey = $"{inFlight.shotNumber}:{inFlight.status}:{inFlight.hasImage}:{inFlight.hasAudio}";
                if (progressKey != state.lastProgressKey)
                {
                    state.lastProgressKey = progressKey;
                    OnShotProgress?.Invoke(storyId, inFlight.shotNumber, inFlight.status ?? "generating", inFlight.hasImage, inFlight.hasAudio);
                }
            }
        }
    }

    // A shot is visible once an image URL exists — UI shows the still as a
    // placeholder while the video renders. Covers "image_ready",
    // "rendering_video", and "ready" without hard-coding backend status names.
    static bool IsShotVisible(ShotStatus shot)
    {
        if (shot == null) return false;
        if (shot.status == "ready" || shot.status == "image_ready" || shot.status == "rendering_video") return true;
        return !string.IsNullOrEmpty(shot.imageUrl) || shot.hasImage;
    }

    void DispatchTransmission(string storyId, StoryState state, ShotStatus shot)
    {
        var boundSignal = activeLocationSignal;
        if (boundSignal == null)
        {
            if (!string.IsNullOrEmpty(storyId) && storySignalSnapshot.TryGetValue(storyId, out var snap) && snap != null)
                boundSignal = snap;
            else
                boundSignal = SignalDirectorV2.Instance?.GetCurrentPrimary();
        }
        string nextTeaser = shot.nextTeaser;
        if (boundSignal != null &&
            boundSignal.transmissionType == TransmissionType.Transmitter &&
            !string.IsNullOrEmpty(nextTeaser))
        {
            // UI copy tweak: "Transmitter Nearby" → "Musical Transmitter Nearby"
            // (only for transmitter-type pursuit beams)
            const string prefix = "Transmitter Nearby";
            string trimmed = nextTeaser.TrimStart();
            if (trimmed.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
            {
                nextTeaser = "Musical " + trimmed;
            }
        }
        var td = new TransmissionData
        {
            storyId = storyId,
            shotId = shot.id,
            shotNumber = shot.shotNumber,
            transmissionType = boundSignal != null
                ? boundSignal.transmissionType.ToString().ToLowerInvariant()
                : null,
            character = state.character,
            specialItem = boundSignal != null ? boundSignal.specialItem : state.objectName,
            nextTeaser = nextTeaser,
            latitude = boundSignal != null ? boundSignal.latitude : 0.0,
            longitude = boundSignal != null ? boundSignal.longitude : 0.0,
            // Only stamp a concrete POI location when this transmission is explicitly a
            // LocationTransmission. Pursuit/transmitter/artifact stories should not
            // look like they occurred "at" the nearest coffee shop unless the user
            // actually entered that POI beam.
            locationName = (boundSignal != null && boundSignal.role == SignalRole.LocationTransmission) ? boundSignal.locationName : null,
            locationCategory = (boundSignal != null && boundSignal.role == SignalRole.LocationTransmission) ? boundSignal.locationCategory : null,
            imageUrl = shot.imageUrl,
            audioUrl = shot.audioUrl,
            videoUrl = shot.videoUrl,
            hasImage = shot.hasImage,
            hasVideo = shot.hasVideo,
            hasAudio = shot.hasAudio
        };

        OnTransmissionReady?.Invoke(td);

        // Write the LLM-authored next-teaser onto the bound location signal
        // so UpdateLocationHUD renders it verbatim.
        if (activeLocationSignal != null)
            activeLocationSignal.teaser = nextTeaser;

        // Pursuit HUD only tracks the active pursuit story.
        if (storyId == activeStoryId)
        {
            pendingTeaser = nextTeaser;
            OnTeaserUpdated?.Invoke(pendingTeaser);
            UpdatePursuitHUD(state.character, pendingTeaser);
        }
    }

    // ── Location Transmission ─────────────────────────────────

    IEnumerator LocationTransmissionLoop()
    {
        // Wait for TransmitterScanner to have data
        yield return new WaitForSeconds(15f);

        while (true)
        {
            yield return new WaitForSeconds(LOCATION_CHECK_INTERVAL);
            TrySpawnLocationTransmission();
        }
    }

    void TrySpawnLocationTransmission()
    {
        var director = SignalDirectorV2.Instance;
        if (director == null) return;

        var scanner = TransmitterScanner.Instance;
        if (scanner == null) return;

        var all = scanner.GetNearestUnfiltered(20);
        if (all == null || all.Count == 0)
        {
            // No beams in range — if existing loc has aged out, drop it
            var stale = director.GetLocationTransmission();
            if (stale != null && Time.time - locationSpawnTime >= LOCATION_REFRESH_INTERVAL)
            {
                director.RemoveLocationTransmission();
                activeLocationSignal = null;
            }
            return;
        }

        // Find the nearest beam overall + nearest within the promote-immediately band
        var candidates = new List<TransmitterScanner.TransmitterData>();
        TransmitterScanner.TransmitterData nearestClose = null;
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i].Distance <= LOCATION_MAX_DISTANCE)
                candidates.Add(all[i]);
            if (all[i].Distance <= PROXIMITY_SWAP_DISTANCE && (nearestClose == null || all[i].Distance < nearestClose.Distance))
                nearestClose = all[i];
        }

        var existing = director.GetLocationTransmission();
        if (existing != null)
        {
            // Expire after 20 minutes — remove and let a new one spawn next tick
            if (Time.time - locationSpawnTime >= LOCATION_REFRESH_INTERVAL)
            {
                Debug.Log($"[TransmissionManager] Location transmission expired after 20min, removing '{existing.locationName}'");
                director.RemoveLocationTransmission();
                activeLocationSignal = null;
                return; // will spawn new one next check
            }

            // Proximity swap: any visible beam within 10m should become the active
            // LocationTransmission so ENTER shows up there. K1L0LocationBeams draws 14
            // nearby POIs and the user can walk up to any of them — whichever they reach
            // first becomes the transmission they can enter.
            if (nearestClose != null && !string.Equals(existing.locationName, nearestClose.Name, StringComparison.Ordinal))
            {
                Debug.Log($"[TransmissionManager] Proximity swap: user {nearestClose.Distance:F0}m from '{nearestClose.Name}' — replacing '{existing.locationName}'");
                director.RemoveLocationTransmission();
                activeLocationSignal = null;
                // fall through to spawn a new one targeted at nearestClose
            }
            else
            {
                return;
            }
        }

        if (candidates.Count == 0) return;

        // Prefer the closest reachable POI so the user always has a believable
        // walking goal. Random-from-band let 700m+ targets win even when closer
        // POIs were on screen. The 20-min refresh + cooldown after ENTER will
        // naturally rotate to the next-nearest place.
        candidates.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        var pick = nearestClose ?? candidates[0];

        var sig = director.SpawnLocationTransmission(
            pick.GeoLocation.x, pick.GeoLocation.y,
            pick.Name, pick.MainCategoryGroup, "", "");

        if (sig != null)
        {
            activeLocationSignal = sig;
            locationSpawnTime = Time.time;
            Debug.Log($"[TransmissionManager] Location transmission: '{pick.Name}' ({pick.MainCategoryGroup}) at {pick.Distance:F0}m");
            // Memo: do not prime/generate stories until the user explicitly ENTERs.
            // Location ENTER triggers PrimeAndGenerateForLocation(sig).
        }
    }

    void HandleLocationEnter(Signal sig)
    {
        Debug.Log($"[TransmissionManager] Location ENTER: '{sig.locationName}'");
        if (sig != null)
        {
            enteredSignals.Add(sig.id);
            UpsertBeamRecord(sig, storyId: null, entered: true);
        }

        string locKey = LocationKey(sig);

        // Cached by location name — re-entering (even via a fresh signal
        // GUID, or a duplicate POI registration) replays the existing story
        // instead of generating a new shot.
        if (!string.IsNullOrEmpty(locKey) && locationStoryByName.TryGetValue(locKey, out var cachedSid) && !string.IsNullOrEmpty(cachedSid))
        {
            Debug.Log($"[TransmissionManager] Location ENTER cached: '{locKey}' → {cachedSid}");
            signalStoryMap[sig.id] = cachedSid;
            BeginStoryListener(cachedSid);
            StartCoroutine(FetchAndDeliver(cachedSid));
            return;
        }

        // Race guard: if a prime is already in flight for this location key,
        // a second ENTER shouldn't kick off another. Drop and let the first
        // prime populate the cache; subsequent enters will hit the cache.
        if (!string.IsNullOrEmpty(locKey) && primingLocationKeys.Contains(locKey))
        {
            Debug.Log($"[TransmissionManager] Location ENTER while prime in flight for '{locKey}' — dropping duplicate");
            return;
        }

        // If we have an active story, attach the location to it.
        if (!string.IsNullOrEmpty(activeStoryId))
        {
            if (!string.IsNullOrEmpty(locKey)) locationStoryByName[locKey] = activeStoryId;
            // If a story was already primed (or even has an image ready),
            // entering the location should immediately surface whatever exists.
            BeginStoryListener(activeStoryId);
            StartCoroutine(FetchAndDeliver(activeStoryId));
            StartCoroutine(GenerateShotCoroutine(sig));
            return;
        }

        // Claim the key BEFORE the prime kicks off so any concurrent ENTER
        // (or POI-scanner duplicate signal) sees us mid-prime and bails.
        if (!string.IsNullOrEmpty(locKey)) primingLocationKeys.Add(locKey);
        StartCoroutine(PrimeAndGenerateForLocation(sig));
    }

    IEnumerator PrimeAndGenerateForLocation(Signal sig)
    {
        string locKey = LocationKey(sig);
        try
        {
            yield return PrimeStoryCoroutine(sig);
            if (!string.IsNullOrEmpty(activeStoryId))
            {
                if (!string.IsNullOrEmpty(locKey)) locationStoryByName[locKey] = activeStoryId;
                yield return GenerateShotCoroutine(sig);
            }
        }
        finally
        {
            if (!string.IsNullOrEmpty(locKey)) primingLocationKeys.Remove(locKey);
        }
    }

    IEnumerator PrimeAndGenerateForLocationExchange(Signal sig, string artifact, string userAction)
    {
        if (sig == null) yield break;
        if (isPriming) yield break;
        isPriming = true;

        string locKey = LocationKey(sig);
        if (!string.IsNullOrEmpty(locKey)) primingLocationKeys.Add(locKey);

        var (lat, lng) = GetPlayerGPS();
        var body = JsonUtility.ToJson(new PrimeRequest
        {
            userId = GetUserId(),
            location = $"{lat:F4},{lng:F4}",
            coordinates = new Coords { latitude = lat, longitude = lng },
            locationName = sig.locationName,
            locationCategory = sig.locationCategory,
            transmissionType = "location",
            artifact = artifact,
            userAction = userAction
        });

        string responseText = null;
        bool success = false;
        Debug.Log($"[TransmissionManager] Priming location exchange loc='{sig.locationName}' item='{artifact}'");

        if (APIManager.Instance != null)
        {
            yield return APIManager.Instance.Post("/story/prime", body, (ok, resp) =>
            {
                success = ok;
                responseText = resp;
            });
        }

        if (success && !string.IsNullOrEmpty(responseText))
        {
            var resp = JsonUtility.FromJson<PrimeResponse>(responseText);
            if (resp.ok && !string.IsNullOrEmpty(resp.storyId))
            {
                activeStoryId = resp.storyId;
                signalStoryMap[sig.id] = resp.storyId;
                storySignalSnapshot[resp.storyId] = sig;
                if (!string.IsNullOrEmpty(locKey)) locationStoryByName[locKey] = resp.storyId;
                if (!storyStates.ContainsKey(resp.storyId))
                    storyStates[resp.storyId] = new StoryState { storyId = resp.storyId };

                var pending = new PendingShot
                {
                    signalId = sig.id,
                    locationName = sig.locationName,
                    locationCategory = sig.locationCategory,
                    transmissionType = "location",
                    artifact = artifact,
                    userAction = userAction
                };

                BeginStoryListener(resp.storyId);
                if (storyStates.TryGetValue(resp.storyId, out var state) && !string.IsNullOrEmpty(state.character))
                {
                    yield return FireGenerateShot(resp.storyId, pending);
                }
                else
                {
                    pendingShotForStory[resp.storyId] = pending;
                    Debug.Log($"[TransmissionManager] Queued location exchange shot for {resp.storyId} (loc='{sig.locationName}', item='{artifact}')");
                }

                UpdatePursuitHUD("...", null);
            }
            else
            {
                Debug.LogWarning($"[TransmissionManager] Location exchange prime failed: {responseText}");
            }
        }
        else
        {
            Debug.LogWarning($"[TransmissionManager] Location exchange prime request failed: {responseText}");
        }

        if (!string.IsNullOrEmpty(locKey)) primingLocationKeys.Remove(locKey);
        isPriming = false;
    }

    // ── HUD integration ─────────────────────────────────────

    void UpdatePursuitHUD(string character, string teaser)
    {
        var director = SignalDirectorV2.Instance;
        if (director == null) return;

        if (!string.IsNullOrEmpty(character))
        {
            int sp = character.IndexOf(' ');
            string first = sp > 0 ? character.Substring(0, sp) : character;
            director.SetPursuitLabel(first.ToUpper());
        }
        else
            director.SetPursuitLabel(null);

        director.SetPursuitTeaser(teaser);
    }

    // ── Public API ────────────────────────────────────────

    public string ActiveStoryId => activeStoryId;
    public string ActiveCharacter =>
        (!string.IsNullOrEmpty(activeStoryId) && storyStates.TryGetValue(activeStoryId, out var s)) ? s.character : null;
    public string PendingTeaser => pendingTeaser;
    public bool IsGenerating => isPriming || pendingShotForStory.Count > 0;

    // Best-effort local inventory: all artifacts (objectName) we've seen from primed stories.
    public List<string> GetKnownArtifacts()
    {
        BeginItemsListener();
        if (cachedItems.Count > 0)
            return new List<string>(cachedItems);

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in storyStates)
        {
            var obj = kv.Value != null ? kv.Value.objectName : null;
            if (!string.IsNullOrWhiteSpace(obj)) set.Add(obj.Trim());
        }
        var list = set.ToList();
        list.Sort(StringComparer.OrdinalIgnoreCase);
        return list;
    }

    // Writes an accepted artifact into RTDB at: users/<userId>/items/<pushId>
    public void AcceptItem(string artifact, string storyId, int shotNumber)
    {
        if (string.IsNullOrWhiteSpace(artifact)) return;
        string itemName = artifact.Trim();
        string uid = GetUserId();
        if (string.IsNullOrWhiteSpace(uid)) return;

        AddCachedItem(itemName);

        FirebaseBootstrap.WhenReady(() =>
        {
            var db = FirebaseDatabase.DefaultInstance;
            string path = $"users/{SanitizeUserId(uid)}/items";
            var itemRef = db.GetReference(path).Push();

            var payload = new Dictionary<string, object>
            {
                { "artifact", itemName },
                { "storyId", storyId ?? "" },
                { "shotNumber", shotNumber },
                { "createdAt", ServerValue.Timestamp }
            };

            itemRef.SetValueAsync(payload);
            Debug.Log($"[TransmissionManager] Accepted item '{itemName}' → {path}/{itemRef.Key}");
        });
    }

    private void AddCachedItem(string itemName)
    {
        if (string.IsNullOrWhiteSpace(itemName)) return;
        string clean = itemName.Trim();
        if (!cachedItems.Exists(x => string.Equals(x, clean, StringComparison.OrdinalIgnoreCase)))
        {
            cachedItems.Add(clean);
            cachedItems.Sort(StringComparer.OrdinalIgnoreCase);
        }

        var modal = TransmitterEnterModal.Instance;
        if (modal != null) modal.RefreshInventory();
    }

    public string GetLocationExchangeObject(Signal sig)
    {
        if (sig != null && !string.IsNullOrWhiteSpace(sig.specialItem))
            return sig.specialItem.Trim();

        string category = sig != null && !string.IsNullOrWhiteSpace(sig.locationCategory)
            ? sig.locationCategory.ToLowerInvariant()
            : "";

        if (category.Contains("coffee") || category.Contains("cafe")) return "coffee sleeve";
        if (category.Contains("bar") || category.Contains("brew") || category.Contains("pub")) return "bent coaster";
        if (category.Contains("restaurant") || category.Contains("food")) return "receipt corner";
        if (category.Contains("gas") || category.Contains("fuel")) return "pump receipt";
        if (category.Contains("store") || category.Contains("shop")) return "paper tag";
        return "folded receipt";
    }

    public IEnumerator FetchLocationExchangeObject(Signal sig, Action<string> onDone)
    {
        if (sig == null)
        {
            onDone?.Invoke(null);
            yield break;
        }

        bool ready = false;
        FirebaseBootstrap.WhenReady(() => ready = true);
        float start = Time.unscaledTime;
        while (!ready && Time.unscaledTime - start < 5f) yield return null;
        if (!ready)
        {
            onDone?.Invoke(null);
            yield break;
        }

        string path = $"k1l0/locationDrops/{LocationDropKey(sig)}";
        var task = FirebaseDatabase.DefaultInstance.GetReference(path).GetValueAsync();
        yield return new WaitUntil(() => task.IsCompleted);

        if (task.IsFaulted || task.IsCanceled || task.Result == null || !task.Result.Exists)
        {
            onDone?.Invoke(null);
            yield break;
        }

        string item = task.Result.Child("artifact").Value as string;
        onDone?.Invoke(string.IsNullOrWhiteSpace(item) ? null : item.Trim());
    }

    private void SaveLocationExchangeItem(Signal sig, string item)
    {
        if (sig == null || string.IsNullOrWhiteSpace(item)) return;

        FirebaseBootstrap.WhenReady(() =>
        {
            string key = LocationDropKey(sig);
            string path = $"k1l0/locationDrops/{key}";
            var payload = new Dictionary<string, object>
            {
                { "artifact", item.Trim() },
                { "locationName", sig.locationName ?? "" },
                { "locationCategory", sig.locationCategory ?? "" },
                { "latitude", sig.latitude },
                { "longitude", sig.longitude },
                { "leftBy", GetUserId() ?? "" },
                { "updatedAt", ServerValue.Timestamp }
            };
            FirebaseDatabase.DefaultInstance.GetReference(path).SetValueAsync(payload);
            Debug.Log($"[TransmissionManager] Location drop saved '{item.Trim()}' → {path}");
        });
    }

    // Artifact-beam collection: seed an item directly from local beam seed/name/noun.
    public void AddItemFromArtifactBeam(int beamSeed, int beamIndex, string beamName, string beamNoun)
    {
        string artifact = $"{(beamName ?? "").Trim()} {(beamNoun ?? "").Trim()}".Trim();
        if (string.IsNullOrWhiteSpace(artifact)) return;

        string uid = GetUserId();
        if (string.IsNullOrWhiteSpace(uid)) return;

        FirebaseBootstrap.WhenReady(() =>
        {
            var db = FirebaseDatabase.DefaultInstance;
            string path = $"users/{SanitizeUserId(uid)}/items";
            var itemRef = db.GetReference(path).Push();

            var payload = new Dictionary<string, object>
            {
                { "artifact", artifact },
                { "kind", "artifact_beam" },
                { "beamSeed", beamSeed },
                { "beamIndex", beamIndex },
                { "beamName", beamName ?? "" },
                { "beamNoun", beamNoun ?? "" },
                { "createdAt", ServerValue.Timestamp }
            };

            itemRef.SetValueAsync(payload);
            Debug.Log($"[TransmissionManager] Collected artifact '{artifact}' → {path}/{itemRef.Key}");
        });
    }

    // Transmitter-enter flow: user picks an artifact + action, then we prime + generate-shot with that directive.
    public void StartTransmitterInteraction(Signal sig, string artifact, string userAction)
    {
        if (sig == null) return;
        if (string.IsNullOrWhiteSpace(artifact)) return;
        if (string.IsNullOrWhiteSpace(userAction)) return;
        artifact = artifact.Trim();
        userAction = BuildLiteralActionDirective(userAction.Trim());

        var frame = TransmissionFrame.Instance;
        if (frame != null)
            frame.ShowLoading("TRANSMISSION", "ambient", null);

        if (isPriming)
        {
            Debug.Log($"[TransmissionManager] Transmitter SEND queued (priming in progress) artifact='{artifact}'");
            StartCoroutine(WaitThenPrimeTransmitter(sig, artifact, userAction));
            return;
        }

        Debug.Log($"[TransmissionManager] Transmitter SEND starting artifact='{artifact}'");
        StartCoroutine(TransmitAndGenerateForTransmitter(sig, artifact, userAction));
    }

    public void StartLocationExchange(Signal sig, string artifact, string userAction, string leftItem)
    {
        if (sig == null) return;
        if (string.IsNullOrWhiteSpace(artifact)) artifact = GetLocationExchangeObject(sig);
        if (string.IsNullOrWhiteSpace(userAction)) return;

        artifact = artifact.Trim();
        userAction = BuildLiteralActionDirective(userAction.Trim());

        enteredSignals.Add(sig.id);
        UpsertBeamRecord(sig, storyId: null, entered: true);
        AcceptItem(artifact, null, 0);
        SaveLocationExchangeItem(sig, leftItem);

        var frame = TransmissionFrame.Instance;
        if (frame != null)
            frame.ShowLoading(sig.locationName, sig.locationCategory, null);

        if (isPriming)
        {
            Debug.Log($"[TransmissionManager] Location exchange queued (priming in progress) item='{artifact}' loc='{sig.locationName}'");
            StartCoroutine(WaitThenPrimeLocationExchange(sig, artifact, userAction));
            return;
        }

        Debug.Log($"[TransmissionManager] Location exchange starting item='{artifact}' loc='{sig.locationName}'");
        StartCoroutine(PrimeAndGenerateForLocationExchange(sig, artifact, userAction));
    }

    IEnumerator WaitThenPrimeLocationExchange(Signal sig, string artifact, string userAction)
    {
        float start = Time.unscaledTime;
        while (isPriming && Time.unscaledTime - start < 12f)
            yield return null;

        if (sig == null || string.IsNullOrWhiteSpace(artifact) || string.IsNullOrWhiteSpace(userAction)) yield break;
        StartCoroutine(PrimeAndGenerateForLocationExchange(sig, artifact.Trim(), userAction.Trim()));
    }

    IEnumerator WaitThenPrimeTransmitter(Signal sig, string artifact, string userAction)
    {
        float start = Time.unscaledTime;
        while (isPriming && Time.unscaledTime - start < 12f)
            yield return null;

        if (sig == null) yield break;
        if (string.IsNullOrWhiteSpace(artifact)) yield break;
        if (string.IsNullOrWhiteSpace(userAction)) yield break;

        Debug.Log($"[TransmissionManager] Transmitter SEND starting after wait ({Time.unscaledTime - start:F1}s) artifact='{artifact}'");
        StartCoroutine(TransmitAndGenerateForTransmitter(sig, artifact.Trim(), BuildLiteralActionDirective(userAction.Trim())));
    }

    // Encourage the backend prompt builder to include the user's directive literally in image/video/music prompts.
    // This prevents vague paraphrases like "a fresh look" when the user said "get a haircut".
    private static string BuildLiteralActionDirective(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return raw;
        string action = raw.Trim();
        string gerund = ToGerund(action);
        // Include both the normalized gerund phrase and the original wording.
        return $"Depict literally: \"{gerund}\". User said: \"{action}\".";
    }

    private static string ToGerund(string action)
    {
        if (string.IsNullOrWhiteSpace(action)) return action;
        string a = action.Trim();
        // Very small, high-signal normalization for common imperatives.
        if (a.StartsWith("get ", System.StringComparison.OrdinalIgnoreCase))
            return "getting " + a.Substring(4);
        if (a.StartsWith("go ", System.StringComparison.OrdinalIgnoreCase))
            return "going " + a.Substring(3);
        if (a.StartsWith("take ", System.StringComparison.OrdinalIgnoreCase))
            return "taking " + a.Substring(5);
        if (a.StartsWith("make ", System.StringComparison.OrdinalIgnoreCase))
            return "making " + a.Substring(5);
        if (a.StartsWith("do ", System.StringComparison.OrdinalIgnoreCase))
            return "doing " + a.Substring(3);
        return a;
    }

    // Returns the storyId linked to a Signal id, or null.
    public string GetStoryIdForSignal(string signalId)
    {
        if (string.IsNullOrEmpty(signalId)) return null;
        return signalStoryMap.TryGetValue(signalId, out var sid) ? sid : null;
    }

    // Returns (character, object, premise) for a given storyId, or (null, null, null).
    public (string character, string objectName, string premise) GetStoryShell(string storyId)
    {
        if (!string.IsNullOrEmpty(storyId) && storyStates.TryGetValue(storyId, out var s))
            return (s.character, s.objectName, s.premise);
        return (null, null, null);
    }

    // Ensures a listener is open for the given story (no-op if already open).
    public void EnsureStoryListener(string storyId) => BeginStoryListener(storyId);

    // Trigger generate-shot for the signal's story, opening its listener if needed.
    public void EnsureShotForSignal(Signal sig)
    {
        if (sig == null) return;
        if (!signalStoryMap.ContainsKey(sig.id) && !string.IsNullOrEmpty(activeStoryId))
            signalStoryMap[sig.id] = activeStoryId;

        string sid = GetStoryIdForSignal(sig.id);
        if (string.IsNullOrEmpty(sid)) return;

        BeginStoryListener(sid);
        StartCoroutine(GenerateShotCoroutine(sig));
    }

    // ── Boot helper (waits for SignalDirectorV2) ──────────

    class BootHelper : MonoBehaviour
    {
        void Update()
        {
            if (TransmissionManager.Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            var director = SignalDirectorV2.Instance;
            if (director != null)
            {
                AttachToDirector(director);
                Destroy(gameObject);
            }
        }
    }
}

// ── Data classes for JSON serialization ───────────────────

[Serializable]
public class TransmissionData
{
    public string storyId;
    public string shotId;
    public int shotNumber;

    // Transmission taxonomy — "location" or "ambient" (mirrors TransmissionType enum)
    public string transmissionType;

    // Narrative metadata — character/artifact for the slide header, teaser for HUD
    public string character;
    public string specialItem;
    public string nextTeaser;

    // Coordinates of the transmission in the world
    public double latitude;
    public double longitude;

    // Location-only (null/empty on ambient)
    public string locationName;
    public string locationCategory;

    // Asset URLs
    public string imageUrl;
    public string audioUrl;
    public string videoUrl;
    public bool hasImage;
    public bool hasVideo;
    public bool hasAudio;
}

[Serializable]
class PrimeRequest
{
    public string userId;
    public string location;
    public Coords coordinates;
    public string locationName;
    public string locationCategory;
    public string transmissionType; // "location" | "artifact" | "transmitter"
    public string artifact;         // optional hint (artifact seeding)
    public string userAction;       // optional user instruction (transmitter flow)
}

[Serializable]
class GenerateShotRequest
{
    public string userId;
    public string storyId;
    public string location;
    public Coords coordinates;
    public string locationName;
    public string locationCategory;
    public string transmissionType; // "location" | "artifact" | "transmitter"
    public string artifact;         // optional override (transmitter flow)
    public string userAction;       // optional user instruction (transmitter flow)
}

[Serializable]
class PrimeTransmitterRequest
{
    public string userId;
    public string location;
    public Coords coordinates;
    public string locationName;
    public string locationCategory;
    public string transmissionType; // "transmitter"
    public string artifact;
    public string userAction;
}

[Serializable]
class Coords
{
    public double latitude;
    public double longitude;
}

[Serializable]
class PrimeResponse
{
    public bool ok;
    public string storyId;
    public string character;
    public string premise;
}

[Serializable]
class StatusResponse
{
    public bool ok;
    public string storyId;
    public string character;
    public string artifact;
    public string premise;
    public int shotCount;
    public ShotStatus[] shots;
}

[Serializable]
class ShotStatus
{
    public string id;
    public int shotNumber;
    public string status;
    public string nextTeaser;
    public string imageUrl;
    public string audioUrl;
    public string videoUrl;
    public bool hasImage;
    public bool hasVideo;
    public bool hasAudio;
}
