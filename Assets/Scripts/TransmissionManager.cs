using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

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
    private bool isGeneratingShot;
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
    }
    private readonly Dictionary<string, StoryState> storyStates = new Dictionary<string, StoryState>();

    // signal.id → storyId (so HandleLocationEnter / shot gen knows which story belongs to the signal)
    private readonly Dictionary<string, string> signalStoryMap = new Dictionary<string, string>();

    // ── Location transmission state ───────────────────────
    private Signal activeLocationSignal;
    private float locationSpawnTime;
    private const float LOCATION_CHECK_INTERVAL = 3f;
    private const float LOCATION_REFRESH_INTERVAL = 1200f; // 20 minutes
    private const float LOCATION_MAX_DISTANCE = 1600f; // ~1 mile
    private const float PROXIMITY_SWAP_DISTANCE = 10f; // if a beam is this close and isn't current loc, swap to it

    // ── Tracking which signals we've already handled ──────
    private readonly HashSet<string> primedSignals = new HashSet<string>();
    private readonly HashSet<string> shotTriggeredSignals = new HashSet<string>();

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

        Debug.Log("[TransmissionManager] Subscribed to SignalDirectorV2 events");

        // Check if a primary already exists (spawned before we subscribed)
        StartCoroutine(CheckExistingPrimary());
        StartCoroutine(LocationTransmissionLoop());
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
        if (SignalDirectorV2.Instance != null)
        {
            SignalDirectorV2.Instance.OnSignalSpawned -= HandleSignalSpawned;
            SignalDirectorV2.Instance.OnSignalStateChanged -= HandleSignalStateChanged;
            SignalDirectorV2.Instance.OnPrimaryChained -= HandlePrimaryChained;
            SignalDirectorV2.Instance.OnLocationEnter -= HandleLocationEnter;
        }
    }

    // ── Signal event handlers ─────────────────────────────

    void HandleSignalSpawned(Signal sig)
    {
        if (sig.role != SignalRole.PrimaryPursuit) return;
        if (primedSignals.Contains(sig.id)) return;
        primedSignals.Add(sig.id);

        // Prime a new story for this primary signal
        StartCoroutine(PrimeStoryCoroutine(sig));
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
        var body = JsonUtility.ToJson(new PrimeRequest
        {
            userId = GetUserId(),
            location = $"{lat:F4},{lng:F4}",
            coordinates = new Coords { latitude = lat, longitude = lng }
        });

        Debug.Log($"[TransmissionManager] Priming story for signal {sig.id}");

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
        if (isGeneratingShot) yield break;

        // Prefer the signal's own story; fall back to the current pursuit story.
        string storyId = null;
        if (sig != null) signalStoryMap.TryGetValue(sig.id, out storyId);
        if (string.IsNullOrEmpty(storyId)) storyId = activeStoryId;
        if (string.IsNullOrEmpty(storyId)) yield break;

        isGeneratingShot = true;

        var (lat, lng) = GetPlayerGPS();
        var body = JsonUtility.ToJson(new GenerateShotRequest
        {
            userId = GetUserId(),
            storyId = storyId,
            location = $"{lat:F4},{lng:F4}",
            coordinates = new Coords { latitude = lat, longitude = lng }
        });

        Debug.Log($"[TransmissionManager] Generating shot for {storyId}, signal {(sig != null ? sig.id : "null")}");

        bool shotSuccess = false;
        if (APIManager.Instance != null)
        {
            yield return APIManager.Instance.Post("/story/generate-shot", body, (ok, resp) => {
                shotSuccess = ok;
            });
        }

        if (!shotSuccess)
        {
            Debug.LogWarning("[TransmissionManager] Generate-shot failed");
            isGeneratingShot = false;
            yield break;
        }

        // Ensure a listener exists for this specific story (idempotent).
        BeginStoryListener(storyId);
        isGeneratingShot = false;
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

    // Opens (or reuses) a dedicated SSE listener for the given storyId.
    // Multiple concurrent listeners are supported — a new pursuit chain does
    // NOT stop prior listeners, so pending shots still get delivered.
    void BeginStoryListener(string storyId)
    {
        if (string.IsNullOrEmpty(storyId)) return;

        var fb = FirebaseRestClient.Instance;
        if (fb == null)
        {
            Debug.LogWarning("[TransmissionManager] FirebaseRestClient not ready — cannot open story listener");
            return;
        }

        string path = $"/users/{SanitizeUserId(GetUserId())}/stories/{storyId}";
        Debug.Log($"[TransmissionManager] Opening story SSE listener on {path}");
        fb.StartListening(path, data => OnStorySSEData(storyId, data));
    }

    void StopStoryListener(string storyId)
    {
        if (string.IsNullOrEmpty(storyId)) return;
        if (FirebaseRestClient.Instance == null) return;
        string path = $"/users/{SanitizeUserId(GetUserId())}/stories/{storyId}";
        FirebaseRestClient.Instance.StopListening(path);
    }

    void StopShotListener()
    {
        if (FirebaseRestClient.Instance != null) FirebaseRestClient.Instance.StopListening();
    }

    // Fires on every SSE data line from Firebase RTDB on a specific story node.
    // Payloads arrive as {"path":"/<field>","data":<value>} (or path:"/" with the whole record).
    // We refetch on any real data frame — FetchAndDeliver is idempotent (it only
    // re-emits events on actual state changes, deduped via state.character /
    // deliveredShotCount / lastProgressKey), so this covers shell arrival,
    // intermediate shot progress (imageUrl, dialogAudioUrl, status transitions),
    // and shot-ready in one path.
    void OnStorySSEData(string storyId, string data)
    {
        if (string.IsNullOrEmpty(data)) return;
        StartCoroutine(FetchAndDeliver(storyId));
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
        }

        // Deliver any newly-ready shots in order for this story.
        if (resp.shots != null)
        {
            while (state.deliveredShotCount < resp.shots.Length)
            {
                var shot = resp.shots[state.deliveredShotCount];
                if (shot.status != "ready") break;

                state.deliveredShotCount++;

                Debug.Log($"[TransmissionManager] Transmission #{shot.shotNumber} ready ({storyId}): {shot.dialog}");

                var boundSignal = activeLocationSignal
                    ?? SignalDirectorV2.Instance?.GetCurrentPrimary();
                var td = new TransmissionData
                {
                    storyId = storyId,
                    shotId = shot.id,
                    shotNumber = shot.shotNumber,
                    transmissionType = boundSignal != null
                        ? (SignalDirectorV2.IsLocationTransmission(boundSignal) ? "location" : "ambient")
                        : null,
                    character = state.character,
                    specialItem = boundSignal != null ? boundSignal.specialItem : state.objectName,
                    nextTeaser = shot.nextTeaser,
                    dialog = shot.dialog,
                    latitude = boundSignal != null ? boundSignal.latitude : 0.0,
                    longitude = boundSignal != null ? boundSignal.longitude : 0.0,
                    locationName = activeLocationSignal != null ? activeLocationSignal.locationName : null,
                    locationCategory = activeLocationSignal != null ? activeLocationSignal.locationCategory : null,
                    imageUrl = shot.imageUrl,
                    audioUrl = shot.audioUrl,
                    videoUrl = shot.videoUrl,
                    hasImage = shot.hasImage,
                    hasVideo = shot.hasVideo,
                    hasAudio = shot.hasAudio
                };

                OnTransmissionReady?.Invoke(td);

                // Write the LLM-authored next-teaser onto the bound location
                // signal so UpdateLocationHUD renders it verbatim (no local
                // fallback needed once the backend ships one).
                if (activeLocationSignal != null)
                    activeLocationSignal.teaser = shot.nextTeaser;

                // Pursuit HUD only tracks the active pursuit story.
                if (storyId == activeStoryId)
                {
                    pendingTeaser = shot.nextTeaser;
                    OnTeaserUpdated?.Invoke(pendingTeaser);
                    UpdatePursuitHUD(state.character, pendingTeaser);
                }
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
        string specialItem = SignalDirectorV2.PickSpecialItem();
        string teaser = GenerateLocationTeaser(pick.Name, pick.MainCategoryGroup, specialItem);

        var sig = director.SpawnLocationTransmission(
            pick.GeoLocation.x, pick.GeoLocation.y,
            pick.Name, pick.MainCategoryGroup, teaser, specialItem);

        if (sig != null)
        {
            activeLocationSignal = sig;
            locationSpawnTime = Time.time;
            Debug.Log($"[TransmissionManager] Location transmission: '{pick.Name}' ({pick.MainCategoryGroup}) at {pick.Distance:F0}m — {teaser}");

            // Proactively prime + generate so the slide is ready by the time the user arrives.
            StartCoroutine(PrimeAndGenerateForLocation(sig));
        }
    }

    // {0} = place name, {1} = special item
    static string GenerateLocationTeaser(string name, string category, string item)
    {
        string[] barTeasers = {
            "WALK TO {0}. a {1} waits at the bar",
            "GO. the bartender at {0} has a {1} with your name",
            "MOVE. one {1} per walker. {0} pours nothing for sitters",
            "WALK TO {0}. a {1} is on the third stool from the door",
            "GO NOW. {0} only hands the {1} to people who walked"
        };
        string[] coffeeTeasers = {
            "WALK TO {0}. a fresh {1} is steaming on the counter",
            "GO. the barista at {0} set a {1} aside for you",
            "MOVE. {0} brewed your {1} — it won't wait",
            "WALK. a {1} sits next to a humming lightbulb at {0}",
            "GO TO {0}. a {1} is yours if you arrive on foot"
        };
        string[] foodTeasers = {
            "WALK TO {0}. a {1} is plated and waiting",
            "GO. the kitchen at {0} prepped a {1} just for walkers",
            "MOVE. {0} is holding a {1} at the counter — keep moving",
            "WALK NOW. a {1} with your table number is at {0}",
            "GO TO {0}. the {1} is yours. stand still and it's someone else's"
        };
        string[] defaultTeasers = {
            "WALK TO {0}. a {1} will be handed to you at the door",
            "GO. the {1} at {0} is real, but only if you walk",
            "MOVE. {0} is the address. the {1} is the reward",
            "WALK NOW. stand still and the {1} at {0} gets reassigned",
            "GO TO {0}. a {1} is sitting under a flickering light"
        };

        string[] pool;
        switch (category)
        {
            case "bar": pool = barTeasers; break;
            case "coffee": pool = coffeeTeasers; break;
            case "food": pool = foodTeasers; break;
            default: pool = defaultTeasers; break;
        }

        string safeName = string.IsNullOrEmpty(name) ? "this place" : name;
        string safeItem = string.IsNullOrEmpty(item) ? "mystery drop" : item;
        return string.Format(pool[UnityEngine.Random.Range(0, pool.Length)], safeName, safeItem);
    }

    void HandleLocationEnter(Signal sig)
    {
        Debug.Log($"[TransmissionManager] Location ENTER: '{sig.locationName}'");

        // If we have an active story, generate a shot at this location
        if (!string.IsNullOrEmpty(activeStoryId))
        {
            StartCoroutine(GenerateShotCoroutine(sig));
        }
        else
        {
            // Prime a story first, then generate a shot
            StartCoroutine(PrimeAndGenerateForLocation(sig));
        }
    }

    IEnumerator PrimeAndGenerateForLocation(Signal sig)
    {
        yield return PrimeStoryCoroutine(sig);
        if (!string.IsNullOrEmpty(activeStoryId))
        {
            yield return GenerateShotCoroutine(sig);
        }
    }

    // ── HUD integration ─────────────────────────────────────

    void UpdatePursuitHUD(string character, string teaser)
    {
        var director = SignalDirectorV2.Instance;
        if (director == null) return;

        if (!string.IsNullOrEmpty(character))
            director.SetPursuitLabel(character.ToUpper());
        else
            director.SetPursuitLabel(null);

        director.SetPursuitTeaser(teaser);
    }

    // ── Public API ────────────────────────────────────────

    public string ActiveStoryId => activeStoryId;
    public string ActiveCharacter =>
        (!string.IsNullOrEmpty(activeStoryId) && storyStates.TryGetValue(activeStoryId, out var s)) ? s.character : null;
    public string PendingTeaser => pendingTeaser;
    public bool IsGenerating => isGeneratingShot || isPriming;

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

    // Narrative metadata — shared across both types, drives teaser + dialog
    public string character;
    public string specialItem;
    public string nextTeaser;
    public string dialog;

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
}

[Serializable]
class GenerateShotRequest
{
    public string userId;
    public string storyId;
    public string location;
    public Coords coordinates;
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
    public string dialog;
    public string nextTeaser;
    public string imageUrl;
    public string audioUrl;
    public string videoUrl;
    public bool hasImage;
    public bool hasVideo;
    public bool hasAudio;
}
