using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

public class PedometerService : MonoBehaviour
{
    [Header("Settings")]
    public float stepThreshold = 1.2f; // G-force threshold for a step
    public float stepDelay = 0.3f; // Minimum time between steps (seconds)
    public bool showDebugUI = false;

    [Header("Read Only")]
    public int stepCount = 0; // Current session steps
    public double distanceMeters = 0;
    public float currentGForce = 0f;

    // Track steps for all-time counting
    private int previousStepCount = 0;
    private int appliedLiveSteps = 0;
    private int kilosyncHistoricalBaseSteps = 0;
    private int kilosyncSixHourBaseSteps = 0;

    // Persist the live session step counter so it survives an app restart
    // (otherwise "steps since stop" resets to 0 each launch — most visible on
    // Mac/non-iOS where steps come from local movement, not HealthKit).
    private const string KEY_SESSION_STEPS = "KiloSessionStepCount";
    private const string KEY_SIM_HAS_STATE = "KiloSimHasState";
    private const string KEY_SIM_STEPS_LAST_HOUR = "KiloSimStepsLastHour";
    private const string KEY_SIM_STEPS_LAST_24H = "KiloSimStepsLast24Hours";
    private const string KEY_SIM_STEPS_LAST_48H = "KiloSimStepsLast48Hours";
    private const string KEY_SIM_STEPS_LAST_7D = "KiloSimStepsLast7Days";
    private const string KEY_SIM_STEPS_LAST_6H = "KiloSimStepsLast6Hours";
    private const string KEY_SIM_KILOSYNC_STEPS = "KiloSimKilosyncSteps";
    private const string KEY_SIM_APPLIED_LIVE_STEPS = "KiloSimAppliedLiveSteps";
    private const string KEY_SIM_DISTANCE_METERS = "KiloSimDistanceMeters";
    private const string KEY_SIM_VIRTUAL_REMAINDER_METERS = "KiloSimVirtualStepRemainderMeters";
    private float _nextSessionSave;
    private bool loadedSimulatedStepState = false;

    // --- iOS Native Interface ---
    // --- iOS Native Interface ---
    private delegate void PedometerCallback(int steps, double distance, double startTimestamp, double endTimestamp);

    #if UNITY_IOS && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void _StartPedometer(PedometerCallback callback);

    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void _StopPedometer();
    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void _QueryPedometerData(double startTimestamp, double endTimestamp, PedometerCallback callback);
    #endif

    private float lastStepTime = 0f;
    private float lowPassValue = 0f;
    private const float LowPassFilterFactor = 0.1f;

    // Historical Data
    public int stepsLastHour = -1;
    public int stepsLast24Hours = -1;
    public int stepsLast48Hours = -1;
    public int stepsLast7Days = -1;
    public int kilosyncSteps = -1;
    public int stepsLast6Hours = -1;
    public bool kilosyncReady = false;
    public bool isKilosyncInert = true;

    // Granular step data for ping button
    private Dictionary<int, int> cachedStepIntervals = new Dictionary<int, int>();
    private float virtualStepRemainderMeters = 0f;

    public const int WalkingBucketMinutes = 5;
    public const int WalkingBucketCount = 5;
    public const int WalkingSessionMaxBuckets = 48;
    public int walkWindowSteps = 0;
    public int walkRecentSteps = 0;
    public int walkActiveBuckets = 0;
    public int walkCurrentBucketSteps = 0;
    public double walkCurrentBucketMeters = 0d;
    public int walkInactiveBucketMinutes = WalkingBucketMinutes;
    public int walkInactiveStepThreshold = 20;
    public double walkInactiveMetersThreshold = 15d;
    public bool walkCurrentBucketInactive = true;
    public bool walkBucketReady = false;
    public bool walkBucketInFlight = false;
    public string walkBucketStatus = "warming up";
    private float nextWalkingBucketRefresh = 0f;
    private const float WalkingBucketRefreshSeconds = 60f;
    private const int MinuteStepBucketCount = 60;
    private const string StepMinuteSyncPathName = "stepMinutes";
    public bool minuteStepSyncReady = false;
    public long minuteStepSyncUpdatedAt = 0;
    public int minuteStepSyncLiveSteps = 0;
    private string lastMinuteStepSyncSignature = "";

    private struct VirtualStepEvent
    {
        public float time;
        public int steps;
        public float meters;
    }

    private readonly List<VirtualStepEvent> virtualStepEvents = new List<VirtualStepEvent>();
    
    // Static dictionary to map start timestamps to callbacks
    private static Dictionary<double, System.Action<int>> pendingIntervalCallbacks = new Dictionary<double, System.Action<int>>();
    private static Dictionary<double, PedometerBucketRequest> pendingWalkingBucketCallbacks = new Dictionary<double, PedometerBucketRequest>();
    private static Dictionary<double, PedometerBucketRequest> pendingMinuteStepCallbacks = new Dictionary<double, PedometerBucketRequest>();

    private struct PedometerBucketRequest
    {
        public System.DateTime start;
        public System.DateTime end;
    }

    public struct WalkingStepBucket
    {
        public System.DateTime start;
        public System.DateTime end;
        public int steps;
        public double distanceMeters;
    }

    public struct MinuteStepBucket
    {
        public System.DateTime start;
        public System.DateTime end;
        public int steps;
        public double distanceMeters;
    }

    private static List<WalkingStepBucket> tempWalkingBuckets;
    private static int pendingWalkingBucketRequests = 0;
    private static List<MinuteStepBucket> tempMinuteStepBuckets;
    private static int pendingMinuteStepRequests = 0;

        // 7-Day History

        public struct DailyStepData

        {

            public System.DateTime date;

            public int steps;

        }

    

        public struct HourlyStepData

        {

            public System.DateTime time; // Start of the hour

            public int steps;

        }

        

        private static System.Action<List<DailyStepData>> onHistoryLoadedCallback;

        private static List<DailyStepData> tempHistoryList;

        private static int pendingHistoryRequests = 0;

    

        // Hourly Breakdown State

        private static System.Action<List<HourlyStepData>> onHourlyHistoryCallback;

        private static List<HourlyStepData> tempHourlyList;

        private static int pendingHourlyRequests = 0;

    

        void Start()
    {
        StartCoroutine(DeferredStart());
    }

    private System.Collections.IEnumerator DeferredStart()
    {
        BootDiagnostics.Mark("PedometerService.Start");
        Debug.Log("[PedometerService] Start() called");

        // PedometerUI removed - now using UILayoutManager with modular components
        // (StepsView is created by UILayoutManager)

        // Restore the persisted session counter so it continues across restarts.
        // iOS overwrites stepCount from the native pedometer anyway, so only the
        // local-accumulation platforms (Mac/editor/Android) need this restore.
        #if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        loadedSimulatedStepState = false;
        stepCount = 0;
        previousStepCount = 0;
        distanceMeters = 0d;
        virtualStepRemainderMeters = 0f;
        ResetSimulatedStepTotals();
        SaveSimulatedStepState();
        PlayerPrefs.Save();
        Debug.Log("[PedometerService] Mac simulated steps reset to zero on launch.");
        #elif !UNITY_IOS || UNITY_EDITOR
        loadedSimulatedStepState = LoadSimulatedStepState();
        if (!loadedSimulatedStepState)
            stepCount = Mathf.Max(stepCount, PlayerPrefs.GetInt(KEY_SESSION_STEPS, 0));
        #endif

        // Initialize tracking. previousStepCount == stepCount so the restored
        // steps aren't re-applied to the all-time total (already counted before).
        previousStepCount = stepCount;
        yield return null;

        // Editor: skip native pedometer flow entirely and simulate immediately.
        if (Application.isEditor)
        {
            QueryHistoricalData();
            BootDiagnostics.Mark("PedometerService.Started");
            Debug.Log("[PedometerService] Editor simulated start complete");
            yield break;
        }

        // Query Historical Data (works on all platforms)
        BootDiagnostics.Mark("PedometerService.QueryHistoricalData");
        QueryHistoricalData();
        yield return null;

        #if UNITY_IOS && !UNITY_EDITOR
        // Start Native Pedometer
        _StartPedometer(OnPedometerUpdate);
        #endif

        BootDiagnostics.Mark("PedometerService.Started");
        Debug.Log($"[PedometerService] Started. All-time steps: {DeviceIDManager.Instance.AllTimeSteps}");
    }

    private void QueryHistoricalData()
    {
        #if UNITY_IOS && !UNITY_EDITOR
        double now = (System.DateTime.UtcNow - new System.DateTime(1970, 1, 1)).TotalSeconds;
        
        // Last Hour
        _QueryPedometerData(now - 3600, now, OnHourlyStepsReceived);
        
        // Last 24 Hours
        _QueryPedometerData(now - 86400, now, OnDailyStepsReceived);

        // Last 48 Hours
        _QueryPedometerData(now - 172800, now, On48HourStepsReceived);

        // Last 7 days
        GetLast7DaysSteps(null);

        GetLast24HoursBreakdown(UpdateKilosyncActivity);
        #else
        if (loadedSimulatedStepState)
        {
            RefreshLiveKilosyncState();
            UpdateSimulatedIntervalCache();
            Debug.Log($"[PedometerService] Simulated data restored. steps={stepCount} 1h={stepsLastHour} 24h={stepsLast24Hours} 7d={stepsLast7Days} kilosync={kilosyncSteps}");
            return;
        }

        InitializeSimulatedStepTotalsFromSession();
        SaveSimulatedStepState();
        
        Debug.Log("[PedometerService] Simulated Data reset to zero for desktop testing.");
        #endif
    }

    private void UpdateKilosyncActivity(List<HourlyStepData> hourly)
    {
        if (hourly == null || hourly.Count == 0)
        {
            kilosyncHistoricalBaseSteps = 0;
            kilosyncSixHourBaseSteps = 0;
            RefreshLiveKilosyncState();
            return;
        }

        hourly.Sort((a, b) => a.time.CompareTo(b.time));
        int latestInactiveIndex = -1;
        int latestSixHourSteps = 0;
        for (int i = 0; i < hourly.Count; i++)
        {
            System.DateTime windowEnd = hourly[i].time.AddHours(1);
            System.DateTime windowStart = windowEnd.AddHours(-6);
            int windowSteps = 0;
            for (int j = 0; j < hourly.Count; j++)
            {
                if (hourly[j].time >= windowStart && hourly[j].time < windowEnd)
                    windowSteps += Mathf.Max(0, hourly[j].steps);
            }
            latestSixHourSteps = windowSteps;
            if (windowSteps < 200)
                latestInactiveIndex = i;
        }

        int activeSteps = 0;
        for (int i = latestInactiveIndex + 1; i < hourly.Count; i++)
            activeSteps += Mathf.Max(0, hourly[i].steps);

        kilosyncHistoricalBaseSteps = activeSteps;
        kilosyncSixHourBaseSteps = latestSixHourSteps;
        RefreshLiveKilosyncState();
        Debug.Log($"[PedometerService] Kilosync steps={kilosyncSteps} 6h={stepsLast6Hours} inert={isKilosyncInert}");
    }

    private void RefreshLiveKilosyncState()
    {
        int live = Mathf.Max(0, stepCount);
        kilosyncSteps = Mathf.Max(0, kilosyncHistoricalBaseSteps) + live;
        stepsLast6Hours = Mathf.Max(0, kilosyncSixHourBaseSteps) + live;
        isKilosyncInert = stepsLast6Hours < 200;
        kilosyncReady = true;
    }

    public void GetLast7DaysSteps(System.Action<List<DailyStepData>> callback)
    {
        #if UNITY_IOS && !UNITY_EDITOR
        onHistoryLoadedCallback = callback;
        tempHistoryList = new List<DailyStepData>();
        pendingHistoryRequests = 7;

        System.DateTime now = System.DateTime.Now;
        System.DateTime today = System.DateTime.Today; // Local time midnight

        for (int i = 0; i < 7; i++)
        {
            System.DateTime dayStart = today.AddDays(-i);
            System.DateTime dayEnd = i == 0 ? now : dayStart.AddDays(1);

            double startTs = (dayStart.ToUniversalTime() - new System.DateTime(1970, 1, 1)).TotalSeconds;
            double endTs = (dayEnd.ToUniversalTime() - new System.DateTime(1970, 1, 1)).TotalSeconds;

            _QueryPedometerData(startTs, endTs, OnHistoryDayReceived);
        }
        #else
        var list = new List<DailyStepData>();
        for(int i=0; i<7; i++) {
            int steps = i == 0 ? Mathf.Max(0, stepsLast24Hours) : 0;
            list.Add(new DailyStepData { date = System.DateTime.Today.AddDays(-i), steps = steps });
        }
        callback?.Invoke(list);
        #endif
    }

    public void GetLast24HoursBreakdown(System.Action<List<HourlyStepData>> callback)
    {
        GetLastHoursBreakdown(24, callback);
    }

    public void GetLastHoursBreakdown(int hours, System.Action<List<HourlyStepData>> callback)
    {
        #if UNITY_IOS && !UNITY_EDITOR
        hours = Mathf.Clamp(hours, 1, 168);
        onHourlyHistoryCallback = callback;
        tempHourlyList = new List<HourlyStepData>();
        pendingHourlyRequests = hours;

        double now = (System.DateTime.UtcNow - new System.DateTime(1970, 1, 1)).TotalSeconds;
        // Align now to top of hour
        long nowSeconds = (long)now;
        long topOfHour = nowSeconds - (nowSeconds % 3600);

        for (int i = 0; i < hours; i++)
        {
            double startTs = topOfHour - (i * 3600);
            double endTs = i == 0 ? now : startTs + 3600;

            _QueryPedometerData(startTs, endTs, OnHourlyBreakdownReceived);
        }
        #else
        hours = Mathf.Clamp(hours, 1, 168);
        var list = new List<HourlyStepData>();
        System.DateTime now = System.DateTime.Now;
        for(int i=0; i<hours; i++) {
            int steps = i == 0 ? Mathf.Max(0, stepsLastHour) : 0;
            list.Add(new HourlyStepData { time = now.AddHours(-i), steps = steps });
        }
        callback?.Invoke(list);
        #endif
    }

    [AOT.MonoPInvokeCallback(typeof(PedometerCallback))]
    private static void OnHourlyBreakdownReceived(int steps, double distance, double start, double end)
    {
        if (tempHourlyList == null) return;
        
        System.DateTime date = new System.DateTime(1970, 1, 1).AddSeconds(start).ToLocalTime();
        
        tempHourlyList.Add(new HourlyStepData { time = date, steps = steps });
        pendingHourlyRequests--;
        
        if (pendingHourlyRequests <= 0)
        {
            tempHourlyList.Sort((a, b) => b.time.CompareTo(a.time)); // Newest first
            var callback = onHourlyHistoryCallback;
            var list = new List<HourlyStepData>(tempHourlyList);
            
            onHourlyHistoryCallback = null;
            tempHourlyList = null;
            
            callback?.Invoke(list);
        }
    }

    /// <summary>
    /// Query steps for a specific time interval in minutes
    /// Results are cached and callbacks are invoked when data is received
    /// </summary>
    public void GetStepsForInterval(int minutes, System.Action<int> callback)
    {
        #if UNITY_IOS && !UNITY_EDITOR
        double now = (System.DateTime.UtcNow - new System.DateTime(1970, 1, 1)).TotalSeconds;
        double startTime = now - (minutes * 60);

        // Store callback in static dictionary keyed by startTime
        // Use tolerance or exact match? Native plugin usually passes back exactly what we sent.
        // We wrap the user callback to also update the instance cache
        pendingIntervalCallbacks[startTime] = (steps) => {
            // Find instance to update cache
            var instance = FindObjectOfType<PedometerService>();
            if (instance != null)
            {
                instance.cachedStepIntervals[minutes] = steps;
            }
            Debug.Log($"[PedometerService] Steps Last {minutes}m: {steps}");
            callback?.Invoke(steps);
        };

        _QueryPedometerData(startTime, now, OnIntervalQueryReceived);
        #else
        int simulatedSteps = GetSimulatedStepsForInterval(minutes);
        cachedStepIntervals[minutes] = simulatedSteps;
        callback?.Invoke(simulatedSteps);
        #endif
    }

    [AOT.MonoPInvokeCallback(typeof(PedometerCallback))]
    private static void OnIntervalQueryReceived(int steps, double distance, double start, double end)
    {
        // Find matching callback by start time (allow small epsilon for float precision)
        // Dictionaries with double keys are tricky if precision varies.
        // We will search for a key that is very close.
        
        double matchedKey = -1;
        bool found = false;

        foreach (var key in pendingIntervalCallbacks.Keys)
        {
            if (System.Math.Abs(key - start) < 1.0) // 1 second tolerance
            {
                matchedKey = key;
                found = true;
                break;
            }
        }

        if (found)
        {
            var action = pendingIntervalCallbacks[matchedKey];
            pendingIntervalCallbacks.Remove(matchedKey);
            action?.Invoke(steps);
        }
        else
        {
            Debug.LogWarning($"[PedometerService] Received interval query result for start={start} but no pending callback found.");
        }
    }

    /// <summary>
    /// Get cached step data for an interval, or -1 if not yet loaded
    /// </summary>
    public int GetCachedStepsForInterval(int minutes)
    {
        return cachedStepIntervals.ContainsKey(minutes) ? cachedStepIntervals[minutes] : -1;
    }

    public void RefreshWalkingBucketsIfDue(bool force = false, int inactivityBucketMinutes = WalkingBucketMinutes, int minActiveSteps = 50, int inactivityStepThreshold = -1)
    {
        if (!force && Time.unscaledTime < nextWalkingBucketRefresh) return;
        if (walkBucketInFlight) return;

        nextWalkingBucketRefresh = Time.unscaledTime + WalkingBucketRefreshSeconds;
        ConfigureWalkingSessionClassifier(inactivityBucketMinutes, minActiveSteps, inactivityStepThreshold);

        #if UNITY_IOS && !UNITY_EDITOR
        QueryMinuteStepsFromPedometer();
        #else
        UpdateMinuteStepsFromVirtualEvents();
        #endif
    }

    public bool HasWalkingBucketSignal(int minSteps)
    {
        if (!walkBucketReady) return false;
        int required = Mathf.Max(0, minSteps);
        if (required <= 0) return true;
        return walkWindowSteps >= required;
    }

    public string GetWalkingBucketDiagnostic(int minSteps)
    {
        if (!walkBucketReady)
            return "<size=11>walking: measuring last 25m...</size>";
        bool gotSignal = HasWalkingBucketSignal(minSteps);
        int target = Mathf.Max(1, minSteps);
        if (gotSignal)
        {
            // Signal locked — strength bars are shown elsewhere; here just emit the percent.
            int pct = Mathf.Clamp(Mathf.RoundToInt((walkWindowSteps / (float)target) * 100f), 0, 999);
            return $"<size=11>walking: active {pct}%</size>";
        }
        if (walkCurrentBucketInactive)
        {
            return $"<size=11>walking: inactive session {K1L0StepFormatter.Value(walkWindowSteps)}/{K1L0StepFormatter.Value(target)}  current {K1L0StepFormatter.Steps(walkCurrentBucketSteps)}</size>";
        }
        // Grace / "keep walking" — show debug steps so the user can see how many more they need.
        int remaining = Mathf.Max(0, target - walkWindowSteps);
        return $"<size=11>walking: keep walking session {K1L0StepFormatter.Value(walkWindowSteps)}/{K1L0StepFormatter.Value(target)}  current {K1L0StepFormatter.Steps(walkCurrentBucketSteps)}</size>\n<size=10>(signal in {K1L0StepFormatter.Steps(remaining)})</size>";
    }

    private void ConfigureWalkingSessionClassifier(int inactivityBucketMinutes, int minActiveSteps, int inactivityStepThreshold = -1)
    {
        walkInactiveBucketMinutes = Mathf.Clamp(inactivityBucketMinutes, 1, 30);
        // If caller supplies an explicit step threshold (the new "RESET GRACE"
        // user slider), use it directly. Otherwise fall back to the old
        // minutes-derived value (4 steps/min) for backward compatibility.
        int chosenThreshold = inactivityStepThreshold > 0
            ? inactivityStepThreshold
            : Mathf.RoundToInt(walkInactiveBucketMinutes * 4f);
        walkInactiveStepThreshold = Mathf.Clamp(chosenThreshold, 3, 500);
        walkInactiveMetersThreshold = System.Math.Max(2d, walkInactiveBucketMinutes * 3d);
    }

    private void QueryWalkingBucketsFromPedometer()
    {
        #if UNITY_IOS && !UNITY_EDITOR
        walkBucketInFlight = true;
        tempWalkingBuckets = new List<WalkingStepBucket>();
        pendingWalkingBucketCallbacks.Clear();
        pendingWalkingBucketRequests = WalkingSessionMaxBuckets;

        System.DateTime utcNow = System.DateTime.UtcNow;
        double now = (utcNow - new System.DateTime(1970, 1, 1)).TotalSeconds;

        for (int index = 0; index < WalkingSessionMaxBuckets; index++)
        {
            double endTs = now - (index * walkInactiveBucketMinutes * 60);
            double startTs = endTs - (walkInactiveBucketMinutes * 60);
            System.DateTime start = new System.DateTime(1970, 1, 1).AddSeconds(startTs).ToLocalTime();
            System.DateTime end = new System.DateTime(1970, 1, 1).AddSeconds(endTs).ToLocalTime();
            pendingWalkingBucketCallbacks[startTs] = new PedometerBucketRequest { start = start, end = end };
            _QueryPedometerData(startTs, endTs, OnWalkingBucketReceived);
        }
        #endif
    }

    private void QueryMinuteStepsFromPedometer()
    {
        #if UNITY_IOS && !UNITY_EDITOR
        walkBucketInFlight = true;
        tempMinuteStepBuckets = new List<MinuteStepBucket>();
        pendingMinuteStepCallbacks.Clear();
        pendingMinuteStepRequests = MinuteStepBucketCount;

        System.DateTime utcNow = System.DateTime.UtcNow;
        double now = (utcNow - new System.DateTime(1970, 1, 1)).TotalSeconds;

        for (int index = 0; index < MinuteStepBucketCount; index++)
        {
            double endTs = now - (index * 60);
            double startTs = endTs - 60;
            System.DateTime start = new System.DateTime(1970, 1, 1).AddSeconds(startTs).ToLocalTime();
            System.DateTime end = new System.DateTime(1970, 1, 1).AddSeconds(endTs).ToLocalTime();
            pendingMinuteStepCallbacks[startTs] = new PedometerBucketRequest { start = start, end = end };
            _QueryPedometerData(startTs, endTs, OnMinuteStepBucketReceived);
        }
        #endif
    }

    [AOT.MonoPInvokeCallback(typeof(PedometerCallback))]
    private static void OnMinuteStepBucketReceived(int steps, double distance, double start, double end)
    {
        double matchedKey = -1;
        bool found = false;

        foreach (var key in pendingMinuteStepCallbacks.Keys)
        {
            if (System.Math.Abs(key - start) < 1.0)
            {
                matchedKey = key;
                found = true;
                break;
            }
        }

        if (found)
        {
            var request = pendingMinuteStepCallbacks[matchedKey];
            pendingMinuteStepCallbacks.Remove(matchedKey);
            tempMinuteStepBuckets?.Add(new MinuteStepBucket
            {
                start = request.start,
                end = request.end,
                steps = Mathf.Max(0, steps),
                distanceMeters = System.Math.Max(0d, distance)
            });
        }
        else
        {
            Debug.LogWarning($"[PedometerService] Minute step result start={start} had no pending callback.");
        }

        pendingMinuteStepRequests--;
        if (pendingMinuteStepRequests > 0) return;

        var instance = FindObjectOfType<PedometerService>();
        if (instance != null)
            instance.ApplyMinuteStepBuckets(tempMinuteStepBuckets, true);

        pendingMinuteStepCallbacks.Clear();
        tempMinuteStepBuckets = null;
    }

    [AOT.MonoPInvokeCallback(typeof(PedometerCallback))]
    private static void OnWalkingBucketReceived(int steps, double distance, double start, double end)
    {
        double matchedKey = -1;
        bool found = false;

        foreach (var key in pendingWalkingBucketCallbacks.Keys)
        {
            if (System.Math.Abs(key - start) < 1.0)
            {
                matchedKey = key;
                found = true;
                break;
            }
        }

        if (found)
        {
            var request = pendingWalkingBucketCallbacks[matchedKey];
            pendingWalkingBucketCallbacks.Remove(matchedKey);
            tempWalkingBuckets?.Add(new WalkingStepBucket
            {
                start = request.start,
                end = request.end,
                steps = Mathf.Max(0, steps),
                distanceMeters = System.Math.Max(0d, distance)
            });
        }
        else
        {
            Debug.LogWarning($"[PedometerService] Walking bucket result start={start} had no pending callback.");
        }

        pendingWalkingBucketRequests--;
        if (pendingWalkingBucketRequests > 0) return;

        var instance = FindObjectOfType<PedometerService>();
        if (instance != null)
            instance.ApplyWalkingBuckets(tempWalkingBuckets);

        pendingWalkingBucketCallbacks.Clear();
        tempWalkingBuckets = null;
    }

    private void UpdateWalkingBucketsFromVirtualEvents()
    {
        float cutoff = Time.unscaledTime - (30f * WalkingSessionMaxBuckets * 60f);
        while (virtualStepEvents.Count > 0 && virtualStepEvents[0].time < cutoff)
            virtualStepEvents.RemoveAt(0);

        var buckets = new List<WalkingStepBucket>();
        System.DateTime now = System.DateTime.Now;
        for (int index = 0; index < WalkingSessionMaxBuckets; index++)
        {
            float bucketEnd = Time.unscaledTime - (index * walkInactiveBucketMinutes * 60f);
            float bucketStart = bucketEnd - (walkInactiveBucketMinutes * 60f);
            int steps = 0;
            double meters = 0d;
            for (int i = 0; i < virtualStepEvents.Count; i++)
            {
                var e = virtualStepEvents[i];
                if (e.time > bucketStart && e.time <= bucketEnd)
                {
                    steps += Mathf.Max(0, e.steps);
                    meters += Mathf.Max(0f, e.meters);
                }
            }

            buckets.Add(new WalkingStepBucket
            {
                start = now.AddMinutes(-(index + 1) * walkInactiveBucketMinutes),
                end = now.AddMinutes(-index * walkInactiveBucketMinutes),
                steps = steps,
                distanceMeters = meters
            });
        }

        ApplyWalkingBuckets(buckets);
    }

    private void UpdateMinuteStepsFromVirtualEvents()
    {
        float cutoff = Time.unscaledTime - (MinuteStepBucketCount * 60f);
        while (virtualStepEvents.Count > 0 && virtualStepEvents[0].time < cutoff)
            virtualStepEvents.RemoveAt(0);

        var buckets = new List<MinuteStepBucket>();
        System.DateTime now = System.DateTime.Now;
        for (int index = 0; index < MinuteStepBucketCount; index++)
        {
            float bucketEnd = Time.unscaledTime - (index * 60f);
            float bucketStart = bucketEnd - 60f;
            int steps = 0;
            double meters = 0d;
            for (int i = 0; i < virtualStepEvents.Count; i++)
            {
                var e = virtualStepEvents[i];
                if (e.time > bucketStart && e.time <= bucketEnd)
                {
                    steps += Mathf.Max(0, e.steps);
                    meters += Mathf.Max(0f, e.meters);
                }
            }

            buckets.Add(new MinuteStepBucket
            {
                start = now.AddMinutes(-(index + 1)),
                end = now.AddMinutes(-index),
                steps = steps,
                distanceMeters = meters
            });
        }

        ApplyMinuteStepBuckets(buckets, true);
    }

    private void ApplyMinuteStepBuckets(List<MinuteStepBucket> minuteBuckets, bool writeToRtdb)
    {
        walkBucketInFlight = false;
        if (minuteBuckets == null || minuteBuckets.Count == 0)
        {
            minuteStepSyncReady = false;
            walkBucketReady = false;
            walkBucketStatus = "no minute steps";
            return;
        }

        minuteBuckets.Sort((a, b) => b.end.CompareTo(a.end));
        var aggregateBuckets = BuildWalkingBucketsFromMinuteBuckets(minuteBuckets);
        ApplyWalkingBuckets(aggregateBuckets);

        minuteStepSyncReady = true;
        minuteStepSyncUpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        minuteStepSyncLiveSteps = Mathf.Max(0, walkWindowSteps);

        if (writeToRtdb)
            WriteMinuteStepsToRtdb(minuteBuckets);
    }

    private List<WalkingStepBucket> BuildWalkingBucketsFromMinuteBuckets(List<MinuteStepBucket> minuteBuckets)
    {
        var buckets = new List<WalkingStepBucket>();
        int minutesPerBucket = Mathf.Clamp(walkInactiveBucketMinutes, 1, 30);
        for (int startIndex = 0; startIndex < minuteBuckets.Count; startIndex += minutesPerBucket)
        {
            int endIndex = Mathf.Min(minuteBuckets.Count, startIndex + minutesPerBucket);
            int steps = 0;
            double meters = 0d;
            DateTime start = minuteBuckets[startIndex].start;
            DateTime end = minuteBuckets[startIndex].end;
            for (int i = startIndex; i < endIndex; i++)
            {
                steps += Mathf.Max(0, minuteBuckets[i].steps);
                meters += System.Math.Max(0d, minuteBuckets[i].distanceMeters);
                if (minuteBuckets[i].start < start) start = minuteBuckets[i].start;
                if (minuteBuckets[i].end > end) end = minuteBuckets[i].end;
            }

            buckets.Add(new WalkingStepBucket
            {
                start = start,
                end = end,
                steps = steps,
                distanceMeters = meters
            });
        }
        return buckets;
    }

    private void WriteMinuteStepsToRtdb(List<MinuteStepBucket> minuteBuckets)
    {
        // Native iOS owns user-scoped realtime step sync. Unity only computes
        // local movement state now; it must not write directly to RTDB.
    }

    private string BuildMinuteStepSignature(List<MinuteStepBucket> minuteBuckets)
    {
        var sb = new StringBuilder(256);
        int count = Mathf.Min(MinuteStepBucketCount, minuteBuckets.Count);
        sb.Append(count).Append('|').Append(walkWindowSteps).Append('|').Append(walkCurrentBucketInactive ? 1 : 0);
        for (int i = 0; i < count; i++)
        {
            long end = new DateTimeOffset(minuteBuckets[i].end.ToUniversalTime()).ToUnixTimeSeconds();
            sb.Append('|').Append(end / 60).Append(':').Append(Mathf.Max(0, minuteBuckets[i].steps));
        }
        return sb.ToString();
    }

    private void ApplyWalkingBuckets(List<WalkingStepBucket> buckets)
    {
        walkBucketInFlight = false;
        if (buckets == null || buckets.Count == 0)
        {
            walkBucketReady = false;
            walkBucketStatus = "no walking buckets";
            return;
        }

        buckets.Sort((a, b) => b.end.CompareTo(a.end));
        int session = 0;
        int recent = 0;
        int active = 0;
        for (int i = 0; i < buckets.Count; i++)
        {
            int steps = Mathf.Max(0, buckets[i].steps);
            if (i < 2) recent += steps;
            bool inactive = IsInactiveWalkingBucket(steps, buckets[i].distanceMeters);
            if (i == 0)
            {
                walkCurrentBucketSteps = steps;
                walkCurrentBucketMeters = buckets[i].distanceMeters;
                walkCurrentBucketInactive = inactive;
            }
            if (inactive) break;
            session += steps;
            active++;
        }

        walkWindowSteps = session;
        walkRecentSteps = recent;
        walkActiveBuckets = active;
        walkBucketReady = true;
        walkBucketStatus = $"session={walkWindowSteps} current={walkCurrentBucketSteps}st/{walkCurrentBucketMeters:F0}m inactive={walkCurrentBucketInactive} bucket={walkInactiveBucketMinutes}m";
        Debug.Log($"[PedometerService] Walking buckets updated: {walkBucketStatus}");
    }

    private bool IsInactiveWalkingBucket(int steps, double meters)
    {
        return steps < walkInactiveStepThreshold || (meters > 0d && meters < walkInactiveMetersThreshold);
    }

    private int GetSimulatedStepsForInterval(int minutes)
    {
        if (minutes <= 0) return 0;
        if (virtualStepEvents.Count > 0)
        {
            float cutoff = Time.unscaledTime - minutes * 60f;
            int steps = 0;
            for (int i = virtualStepEvents.Count - 1; i >= 0; i--)
            {
                if (virtualStepEvents[i].time < cutoff) break;
                steps += Mathf.Max(0, virtualStepEvents[i].steps);
            }
            return steps;
        }
        if (minutes >= 60) return Mathf.Max(0, stepsLastHour);
        return Mathf.RoundToInt(Mathf.Max(0, stepsLastHour) * Mathf.Clamp01(minutes / 60f));
    }

    private void ResetSimulatedIntervalCache()
    {
        cachedStepIntervals.Clear();
        for (int minutes = 10; minutes <= 60; minutes += 10)
            cachedStepIntervals[minutes] = 0;
    }

    private void UpdateSimulatedIntervalCache()
    {
        for (int minutes = 10; minutes <= 60; minutes += 10)
            cachedStepIntervals[minutes] = GetSimulatedStepsForInterval(minutes);
    }

    [AOT.MonoPInvokeCallback(typeof(PedometerCallback))]
    private static void OnHourlyStepsReceived(int steps, double distance, double start, double end)
    {
        var instance = FindObjectOfType<PedometerService>();
        if (instance != null)
        {
            instance.stepsLastHour = steps;
            if (instance.appliedLiveSteps > 0)
                instance.stepsLastHour += instance.appliedLiveSteps;
            Debug.Log($"[PedometerService] Steps Last Hour: {steps}");
        }
    }

    [AOT.MonoPInvokeCallback(typeof(PedometerCallback))]
    private static void OnDailyStepsReceived(int steps, double distance, double start, double end)
    {
        var instance = FindObjectOfType<PedometerService>();
        if (instance != null)
        {
            instance.stepsLast24Hours = steps;
            if (instance.appliedLiveSteps > 0)
                instance.stepsLast24Hours += instance.appliedLiveSteps;
            Debug.Log($"[PedometerService] Steps Last 24 Hours: {steps}");
        }
    }
    
    [AOT.MonoPInvokeCallback(typeof(PedometerCallback))]
    private static void On48HourStepsReceived(int steps, double distance, double start, double end)
    {
        var instance = FindObjectOfType<PedometerService>();
        if (instance != null)
        {
            instance.stepsLast48Hours = steps;
            if (instance.appliedLiveSteps > 0)
                instance.stepsLast48Hours += instance.appliedLiveSteps;
            Debug.Log($"[PedometerService] Steps Last 48 Hours: {steps}");
        }
    }

    [AOT.MonoPInvokeCallback(typeof(PedometerCallback))]
    private static void OnHistoryDayReceived(int steps, double distance, double start, double end)
    {
        if (tempHistoryList == null) return;
        
        System.DateTime date = new System.DateTime(1970, 1, 1).AddSeconds(start).ToLocalTime().Date;
        
        tempHistoryList.Add(new DailyStepData { date = date, steps = steps });
        pendingHistoryRequests--;
        
        if (pendingHistoryRequests <= 0)
        {
            // Sort by date descending (newest first)
            tempHistoryList.Sort((a, b) => b.date.CompareTo(a.date));

            // Calculate total steps for last 7 days
            int total7Days = 0;
            foreach (var day in tempHistoryList)
            {
                total7Days += day.steps;
            }

            // Update the instance's 7-day total
            var instance = FindObjectOfType<PedometerService>();
            if (instance != null)
            {
                instance.stepsLast7Days = total7Days + instance.appliedLiveSteps;
                Debug.Log($"[PedometerService] Steps Last 7 Days: {total7Days}");
            }

            var callback = onHistoryLoadedCallback;
            var list = new List<DailyStepData>(tempHistoryList);

            // Clear static state
            onHistoryLoadedCallback = null;
            tempHistoryList = null;

            // Invoke on main thread (we are already on main thread due to dispatch_async in plugin)
            callback?.Invoke(list);
        }
    }

    // MonoPInvokeCallback is required for iOS callbacks
    [AOT.MonoPInvokeCallback(typeof(PedometerCallback))]
    private static void OnPedometerUpdate(int steps, double distance, double start, double end)
    {
        // Find the instance (static callback limitation)
        var instance = FindObjectOfType<PedometerService>();
        if (instance != null)
        {
            instance.stepCount = steps;
            instance.distanceMeters = distance;
        }
    }

    void Update()
    {
        // Check for step count increases and add to all-time total
        if (stepCount > previousStepCount)
        {
            int newSteps = stepCount - previousStepCount;
            ApplyLiveStepDelta(newSteps);
        }

        // Persist the session counter every few seconds so a restart resumes it.
        if (Time.unscaledTime >= _nextSessionSave)
        {
            _nextSessionSave = Time.unscaledTime + 5f;
            SaveSessionSteps();
        }

        #if UNITY_IOS && !UNITY_EDITOR
        // Do nothing, native plugin handles it
        return;
        #endif

        // Fallback: Accelerometer Logic for Editor/Android
        Vector3 acc = Input.acceleration;
        float currentMagnitude = acc.magnitude;

        lowPassValue = Mathf.Lerp(lowPassValue, currentMagnitude, LowPassFilterFactor);
        currentGForce = lowPassValue;

        if (currentGForce > stepThreshold && (Time.time - lastStepTime) > stepDelay)
        {
            RegisterStep();
        }
    }

    void RegisterStep()
    {
        stepCount++;
        lastStepTime = Time.time;
    }

    private void SaveSessionSteps()
    {
        PlayerPrefs.SetInt(KEY_SESSION_STEPS, Mathf.Max(0, stepCount));
        #if !UNITY_IOS || UNITY_EDITOR
        SaveSimulatedStepState();
        #endif
    }

    void OnApplicationPause(bool paused) { if (paused) { SaveSessionSteps(); PlayerPrefs.Save(); } }
    void OnApplicationQuit() { SaveSessionSteps(); PlayerPrefs.Save(); }

    private void ApplyLiveStepDelta(int steps)
    {
        if (steps <= 0) return;

        if (DeviceIDManager.Instance != null)
            DeviceIDManager.Instance.AddSteps(steps);

        appliedLiveSteps += steps;
        previousStepCount += steps;

        stepsLastHour = Mathf.Max(0, stepsLastHour) + steps;
        stepsLast24Hours = Mathf.Max(0, stepsLast24Hours) + steps;
        stepsLast48Hours = Mathf.Max(0, stepsLast48Hours) + steps;
        stepsLast7Days = Mathf.Max(0, stepsLast7Days) + steps;

        ApplyLiveStepsToWalkingWindow(steps);
        RefreshLiveKilosyncState();
        UpdateSimulatedIntervalCache();
        #if !UNITY_IOS || UNITY_EDITOR
        UpdateWalkingBucketsFromVirtualEvents();
        RefreshWalkingBucketsIfDue(false);
        SaveSimulatedStepState();
        #endif
    }

    private void ApplyLiveStepsToWalkingWindow(int steps)
    {
        if (steps <= 0) return;

        // The 60-minute RTDB/pedometer sync is authoritative, but it only refreshes
        // once per minute. Apply live pedometer deltas immediately so the HUD feels
        // alive while the next minute-bucket sync catches up and re-normalizes.
        walkWindowSteps = Mathf.Max(0, walkWindowSteps) + steps;
        walkRecentSteps = Mathf.Max(0, walkRecentSteps) + steps;
        walkCurrentBucketSteps = Mathf.Max(0, walkCurrentBucketSteps) + steps;
        walkBucketReady = true;
        walkCurrentBucketInactive = false;
        walkBucketStatus = $"live session={walkWindowSteps} current={walkCurrentBucketSteps}st/{walkCurrentBucketMeters:F0}m";
        minuteStepSyncLiveSteps = Mathf.Max(0, walkWindowSteps);
    }

    public void RegisterVirtualMovementMeters(float meters)
    {
        if (meters <= 0f) return;

        float stride = Mathf.Max(0.2f, EstimatedStrideLength);
        virtualStepRemainderMeters += meters;
        int steps = Mathf.FloorToInt(virtualStepRemainderMeters / stride);
        if (steps <= 0) return;

        virtualStepRemainderMeters -= steps * stride;
        stepCount += steps;
        float appliedMeters = steps * stride;
        distanceMeters += appliedMeters;
        RecordVirtualStepEvent(steps, appliedMeters);
        ApplyLiveStepDelta(steps);
    }

    private void RecordVirtualStepEvent(int steps, float meters)
    {
        if (steps <= 0) return;
        virtualStepEvents.Add(new VirtualStepEvent
        {
            time = Time.unscaledTime,
            steps = steps,
            meters = Mathf.Max(0f, meters)
        });

        float cutoff = Time.unscaledTime - (30f * WalkingSessionMaxBuckets * 60f);
        while (virtualStepEvents.Count > 0 && virtualStepEvents[0].time < cutoff)
            virtualStepEvents.RemoveAt(0);
    }

    void OnDisable()
    {
        #if UNITY_IOS && !UNITY_EDITOR
        _StopPedometer();
        #endif
    }

    void OnGUI()
    {
        if (showDebugUI)
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = 40;
            style.normal.textColor = Color.green;

            GUILayout.BeginArea(new Rect(50, 200, 600, 500));
            GUILayout.Label($"Session Steps: {stepCount}", style);
            GUILayout.Label($"All-Time Steps: {K1L0StepFormatter.Value(DeviceIDManager.Instance.AllTimeSteps)}", style);
            
            if (stepsLastHour >= 0)
                GUILayout.Label($"Last Hour: {K1L0StepFormatter.Value(stepsLastHour)}", style);
            
            if (stepsLast24Hours >= 0)
                GUILayout.Label($"Last 24h: {K1L0StepFormatter.Value(stepsLast24Hours)}", style);

            #if UNITY_IOS && !UNITY_EDITOR
            GUILayout.Label($"Dist: {distanceMeters:F1}m", style);
            #else
            GUILayout.Label($"(Simulated)", style);
            #endif

            GUILayout.EndArea();
        }
    }

    // Public method to reset steps (e.g. daily reset)
    public void ResetSteps()
    {
        stepCount = 0;
        previousStepCount = 0;
        distanceMeters = 0;
        stepsLastHour = 0;
        stepsLast24Hours = 0;
        stepsLast48Hours = 0;
        stepsLast7Days = 0;
        stepsLast6Hours = 0;
        kilosyncSteps = 0;
        appliedLiveSteps = 0;
        kilosyncHistoricalBaseSteps = 0;
        kilosyncSixHourBaseSteps = 0;
        isKilosyncInert = true;
        kilosyncReady = true;
        virtualStepRemainderMeters = 0f;
        virtualStepEvents.Clear();
        walkWindowSteps = 0;
        walkRecentSteps = 0;
        walkActiveBuckets = 0;
        walkCurrentBucketSteps = 0;
        walkCurrentBucketMeters = 0d;
        walkCurrentBucketInactive = true;
        walkBucketReady = false;
        walkBucketInFlight = false;
        walkBucketStatus = "reset";
        ResetSimulatedIntervalCache();
        #if !UNITY_IOS || UNITY_EDITOR
        SaveSimulatedStepState();
        PlayerPrefs.Save();
        #endif
    }

    private bool LoadSimulatedStepState()
    {
        if (PlayerPrefs.GetInt(KEY_SIM_HAS_STATE, 0) != 1)
            return false;

        stepCount = Mathf.Max(0, PlayerPrefs.GetInt(KEY_SESSION_STEPS, 0));
        previousStepCount = stepCount;
        stepsLastHour = Mathf.Max(0, PlayerPrefs.GetInt(KEY_SIM_STEPS_LAST_HOUR, 0));
        stepsLast24Hours = Mathf.Max(0, PlayerPrefs.GetInt(KEY_SIM_STEPS_LAST_24H, 0));
        stepsLast48Hours = Mathf.Max(0, PlayerPrefs.GetInt(KEY_SIM_STEPS_LAST_48H, 0));
        stepsLast7Days = Mathf.Max(0, PlayerPrefs.GetInt(KEY_SIM_STEPS_LAST_7D, 0));
        stepsLast6Hours = Mathf.Max(0, PlayerPrefs.GetInt(KEY_SIM_STEPS_LAST_6H, 0));
        kilosyncSteps = Mathf.Max(0, PlayerPrefs.GetInt(KEY_SIM_KILOSYNC_STEPS, stepCount));
        appliedLiveSteps = Mathf.Max(0, PlayerPrefs.GetInt(KEY_SIM_APPLIED_LIVE_STEPS, 0));
        distanceMeters = System.Math.Max(0d, PlayerPrefs.GetFloat(KEY_SIM_DISTANCE_METERS, 0f));
        virtualStepRemainderMeters = Mathf.Max(0f, PlayerPrefs.GetFloat(KEY_SIM_VIRTUAL_REMAINDER_METERS, 0f));

        if (stepCount > 0 && stepsLastHour == 0 && stepsLast24Hours == 0 && stepsLast7Days == 0 && kilosyncSteps == 0)
            InitializeSimulatedStepTotalsFromSession();

        kilosyncHistoricalBaseSteps = Mathf.Max(0, kilosyncSteps - stepCount);
        kilosyncSixHourBaseSteps = Mathf.Max(0, stepsLast6Hours - stepCount);
        isKilosyncInert = stepsLast6Hours < 200;
        kilosyncReady = true;
        UpdateSimulatedIntervalCache();
        return true;
    }

    private void SaveSimulatedStepState()
    {
        PlayerPrefs.SetInt(KEY_SIM_HAS_STATE, 1);
        PlayerPrefs.SetInt(KEY_SESSION_STEPS, Mathf.Max(0, stepCount));
        PlayerPrefs.SetInt(KEY_SIM_STEPS_LAST_HOUR, Mathf.Max(0, stepsLastHour));
        PlayerPrefs.SetInt(KEY_SIM_STEPS_LAST_24H, Mathf.Max(0, stepsLast24Hours));
        PlayerPrefs.SetInt(KEY_SIM_STEPS_LAST_48H, Mathf.Max(0, stepsLast48Hours));
        PlayerPrefs.SetInt(KEY_SIM_STEPS_LAST_7D, Mathf.Max(0, stepsLast7Days));
        PlayerPrefs.SetInt(KEY_SIM_STEPS_LAST_6H, Mathf.Max(0, stepsLast6Hours));
        PlayerPrefs.SetInt(KEY_SIM_KILOSYNC_STEPS, Mathf.Max(0, kilosyncSteps));
        PlayerPrefs.SetInt(KEY_SIM_APPLIED_LIVE_STEPS, Mathf.Max(0, appliedLiveSteps));
        PlayerPrefs.SetFloat(KEY_SIM_DISTANCE_METERS, (float)System.Math.Max(0d, distanceMeters));
        PlayerPrefs.SetFloat(KEY_SIM_VIRTUAL_REMAINDER_METERS, Mathf.Max(0f, virtualStepRemainderMeters));
    }

    private void ResetSimulatedStepTotals()
    {
        stepsLast24Hours = 0;
        stepsLast48Hours = 0;
        stepsLastHour = 0;
        stepsLast7Days = 0;
        stepsLast6Hours = 0;
        kilosyncSteps = 0;
        appliedLiveSteps = 0;
        kilosyncHistoricalBaseSteps = 0;
        kilosyncSixHourBaseSteps = 0;
        isKilosyncInert = true;
        kilosyncReady = true;
        ResetSimulatedIntervalCache();
    }

    private void InitializeSimulatedStepTotalsFromSession()
    {
        int restoredSteps = Mathf.Max(0, stepCount);
        stepsLastHour = restoredSteps;
        stepsLast24Hours = restoredSteps;
        stepsLast48Hours = restoredSteps;
        stepsLast7Days = restoredSteps;
        stepsLast6Hours = restoredSteps;
        kilosyncSteps = restoredSteps;
        appliedLiveSteps = 0;
        kilosyncHistoricalBaseSteps = 0;
        kilosyncSixHourBaseSteps = 0;
        isKilosyncInert = stepsLast6Hours < 200;
        kilosyncReady = true;
        UpdateSimulatedIntervalCache();
    }

    /// <summary>
    /// Returns the user's estimated stride length in meters.
    /// Uses real distance/steps from iOS if available, otherwise defaults to 0.762m.
    /// </summary>
    public float EstimatedStrideLength
    {
        get
        {
            // If we have real distance and enough steps to be statistically significant
            if (distanceMeters > 0 && stepCount > 20)
            {
                return (float)(distanceMeters / stepCount);
            }
            // Fallback to average (0.762 meters = 2.5 feet)
            return 0.762f;
        }
    }
}

public static class K1L0StepFormatter
{
    public const float DefaultStrideMeters = 0.762f;

    public static int EstimateFromMeters(float meters, float strideMeters = DefaultStrideMeters)
    {
        return Mathf.Max(0, Mathf.RoundToInt(Mathf.Max(0f, meters) / Mathf.Max(0.2f, strideMeters)));
    }

    public static string Value(int steps)
    {
        int safeSteps = Mathf.Max(0, steps);
        if (safeSteps <= 1000) return safeSteps.ToString(CultureInfo.InvariantCulture);
        return (safeSteps / 1000f).ToString("0.#", CultureInfo.InvariantCulture) + "k";
    }

    public static string Steps(int steps)
    {
        return Value(steps) + " steps";
    }

    public static string FromMeters(float meters, float strideMeters = DefaultStrideMeters)
    {
        return Steps(EstimateFromMeters(meters, strideMeters));
    }
}
