using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

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
    public int stepsLast7Days = -1;

    // Granular step data for ping button
    private Dictionary<int, int> cachedStepIntervals = new Dictionary<int, int>();
    
    // Static dictionary to map start timestamps to callbacks
    private static Dictionary<double, System.Action<int>> pendingIntervalCallbacks = new Dictionary<double, System.Action<int>>();

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

        // Initialize tracking
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
        #else
        // Fallback for Editor / Mac / Windows
        stepsLast24Hours = Random.Range(0, 10000);
        stepsLastHour = Random.Range(0, 1000);
        
        // stepsLast7Days is handled by the UI calling GetLast7DaysSteps, 
        // but we can set a default here just in case UI reads it early
        stepsLast7Days = Random.Range(0, 70000);
        
        Debug.Log($"[PedometerService] Simulated Data: 24h={stepsLast24Hours}, 1h={stepsLastHour}, 7d={stepsLast7Days}");
        #endif
    }

    public void GetLast7DaysSteps(System.Action<List<DailyStepData>> callback)
    {
        #if UNITY_IOS && !UNITY_EDITOR
        onHistoryLoadedCallback = callback;
        tempHistoryList = new List<DailyStepData>();
        pendingHistoryRequests = 7;

        System.DateTime today = System.DateTime.Today; // Local time midnight

        for (int i = 0; i < 7; i++)
        {
            System.DateTime dayStart = today.AddDays(-i);
            System.DateTime dayEnd = dayStart.AddDays(1).AddSeconds(-1);

            double startTs = (dayStart.ToUniversalTime() - new System.DateTime(1970, 1, 1)).TotalSeconds;
            double endTs = (dayEnd.ToUniversalTime() - new System.DateTime(1970, 1, 1)).TotalSeconds;

            _QueryPedometerData(startTs, endTs, OnHistoryDayReceived);
        }
        #else
        // Simulate for Editor
        var list = new List<DailyStepData>();
        for(int i=0; i<7; i++) {
            list.Add(new DailyStepData { date = System.DateTime.Today.AddDays(-i), steps = Random.Range(1000, 10000) });
        }
        callback?.Invoke(list);
        #endif
    }

    public void GetLast24HoursBreakdown(System.Action<List<HourlyStepData>> callback)
    {
        #if UNITY_IOS && !UNITY_EDITOR
        onHourlyHistoryCallback = callback;
        tempHourlyList = new List<HourlyStepData>();
        pendingHourlyRequests = 24;

        double now = (System.DateTime.UtcNow - new System.DateTime(1970, 1, 1)).TotalSeconds;
        // Align now to top of hour
        long nowSeconds = (long)now;
        long topOfHour = nowSeconds - (nowSeconds % 3600);

        for (int i = 0; i < 24; i++)
        {
            double endTs = topOfHour - (i * 3600);
            double startTs = endTs - 3600;

            _QueryPedometerData(startTs, endTs, OnHourlyBreakdownReceived);
        }
        #else
        var list = new List<HourlyStepData>();
        System.DateTime now = System.DateTime.Now;
        for(int i=0; i<24; i++) {
            list.Add(new HourlyStepData { time = now.AddHours(-i), steps = Random.Range(50, 500) });
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
        // Simulate for Editor - scale based on time interval
        int simulatedSteps = Random.Range(minutes / 2, minutes * 2);
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

    [AOT.MonoPInvokeCallback(typeof(PedometerCallback))]
    private static void OnHourlyStepsReceived(int steps, double distance, double start, double end)
    {
        var instance = FindObjectOfType<PedometerService>();
        if (instance != null)
        {
            instance.stepsLastHour = steps;
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
            Debug.Log($"[PedometerService] Steps Last 24 Hours: {steps}");
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
                instance.stepsLast7Days = total7Days;
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
            DeviceIDManager.Instance.AddSteps(newSteps);
            previousStepCount = stepCount;
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
            GUILayout.Label($"All-Time Steps: {DeviceIDManager.Instance.AllTimeSteps:N0}", style);
            
            if (stepsLastHour >= 0)
                GUILayout.Label($"Last Hour: {stepsLastHour}", style);
            
            if (stepsLast24Hours >= 0)
                GUILayout.Label($"Last 24h: {stepsLast24Hours}", style);

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
