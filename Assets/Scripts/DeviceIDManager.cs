using UnityEngine;

/// <summary>
/// Manages persistent device ID and all-time step count across app restarts.
/// Uses PlayerPrefs for simple, persistent storage.
/// </summary>
public class DeviceIDManager : MonoBehaviour
{
    private static DeviceIDManager _instance;
    public static DeviceIDManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<DeviceIDManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("DeviceIDManager");
                    _instance = go.AddComponent<DeviceIDManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    // PlayerPrefs keys
    private const string KEY_DEVICE_ID = "KiloDeviceID";
    private const string KEY_ALL_TIME_STEPS = "KiloAllTimeSteps";

    // Cached values
    private string _deviceID;
    private int _allTimeSteps;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        // Load on startup
        LoadData();
    }

    /// <summary>
    /// Gets or creates a persistent device ID.
    /// This ID survives app restarts but NOT uninstalls.
    /// </summary>
    public string DeviceID
    {
        get
        {
            if (string.IsNullOrEmpty(_deviceID))
            {
                LoadData();
            }
            return _deviceID;
        }
    }

    /// <summary>
    /// Gets the current user ID (Firebase UID if authenticated, otherwise device ID)
    /// This is the preferred method for identifying users
    /// </summary>
    public string GetCurrentUserId()
    {
        // Check if Firebase auth is available and user is authenticated
        if (FirebaseAuthManager.Instance != null &&
            FirebaseAuthManager.Instance.isAuthenticated &&
            !string.IsNullOrEmpty(FirebaseAuthManager.Instance.userId))
        {
            return FirebaseAuthManager.Instance.userId;
        }

        // Fallback to device ID
        return DeviceID;
    }

    /// <summary>
    /// Gets the all-time step count accumulated across all app sessions.
    /// </summary>
    public int AllTimeSteps
    {
        get { return _allTimeSteps; }
    }

    /// <summary>
    /// Adds steps to the all-time count and saves immediately.
    /// </summary>
    public void AddSteps(int steps)
    {
        if (steps <= 0) return;

        _allTimeSteps += steps;
        PlayerPrefs.SetInt(KEY_ALL_TIME_STEPS, _allTimeSteps);
        PlayerPrefs.Save();

        Debug.Log($"[DeviceIDManager] Added {steps} steps. Total: {_allTimeSteps}");
    }

    /// <summary>
    /// Manually set the all-time step count (useful for syncing from server).
    /// </summary>
    public void SetAllTimeSteps(int totalSteps)
    {
        _allTimeSteps = totalSteps;
        PlayerPrefs.SetInt(KEY_ALL_TIME_STEPS, _allTimeSteps);
        PlayerPrefs.Save();

        Debug.Log($"[DeviceIDManager] Set all-time steps to: {_allTimeSteps}");
    }

    /// <summary>
    /// Resets the step counter (use with caution).
    /// </summary>
    public void ResetStepCount()
    {
        _allTimeSteps = 0;
        PlayerPrefs.SetInt(KEY_ALL_TIME_STEPS, 0);
        PlayerPrefs.Save();

        Debug.Log("[DeviceIDManager] Reset step count to 0");
    }

    /// <summary>
    /// Loads data from PlayerPrefs.
    /// </summary>
    private void LoadData()
    {
        // Load or create device ID
        if (PlayerPrefs.HasKey(KEY_DEVICE_ID))
        {
            _deviceID = PlayerPrefs.GetString(KEY_DEVICE_ID);
            Debug.Log($"[DeviceIDManager] Loaded existing Device ID: {_deviceID}");
        }
        else
        {
            // Generate new GUID-based ID
            _deviceID = System.Guid.NewGuid().ToString();
            PlayerPrefs.SetString(KEY_DEVICE_ID, _deviceID);
            PlayerPrefs.Save();
            Debug.Log($"[DeviceIDManager] Created new Device ID: {_deviceID}");
        }

        // Load all-time steps
        _allTimeSteps = PlayerPrefs.GetInt(KEY_ALL_TIME_STEPS, 0);
        Debug.Log($"[DeviceIDManager] Loaded all-time steps: {_allTimeSteps}");
    }

    /// <summary>
    /// Forces a save of current data to PlayerPrefs.
    /// </summary>
    public void ForceSave()
    {
        PlayerPrefs.SetString(KEY_DEVICE_ID, _deviceID);
        PlayerPrefs.SetInt(KEY_ALL_TIME_STEPS, _allTimeSteps);
        PlayerPrefs.Save();

        Debug.Log("[DeviceIDManager] Forced save complete");
    }

    private void OnApplicationQuit()
    {
        // Ensure data is saved on quit
        ForceSave();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        // Save when app goes to background (important on mobile)
        if (pauseStatus)
        {
            ForceSave();
        }
    }

    // Debug info for GUI
    /*
    private void OnGUI()
    {
        #if UNITY_EDITOR || DEVELOPMENT_BUILD
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 20;
        style.normal.textColor = Color.yellow;

        string info = $"Device ID: {_deviceID}\nAll-Time Steps: {_allTimeSteps:N0}";

        // Shadow
        style.normal.textColor = Color.black;
        GUI.Label(new Rect(12, Screen.height - 82, 500, 100), info, style);

        // Text
        style.normal.textColor = Color.yellow;
        GUI.Label(new Rect(10, Screen.height - 80, 500, 100), info, style);
        #endif
    }
    */
}
