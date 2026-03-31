using UnityEngine;
using System.Collections;
using Kiloverse.Mapbox;

public class GPSLocationController : MonoBehaviour
{
    [Header("Settings")]
    public bool useGPSOnMobile = true; // Re-enabled with iOS freeze fix
    public bool followUser = true;
    public float minUpdateDistance = 10f; // Meters
    public float updateInterval = 1f; // Seconds

    [Header("References")]
    public KiloverseMapInfo map;

    [Header("Compass")]
    public bool useCompass = false;
    public float compassSmoothing = 5f;
    public Transform targetToRotate; // Assign Player transform (not camera)

    private LocationInfo lastLocation;
    private bool isInitialized = false;
    private bool isMapInitialized = false;

    // Public flag for other systems to check if GPS is ready
    public static bool GPSReady { get; private set; } = false;

    IEnumerator Start()
    {
        while (!BootState.AllowGPS)
        {
            yield return null;
        }
        BootDiagnostics.Mark("GPS Start allowed");
        BootDiagnostics.Mark("GPS.Start begin");
        Debug.Log("========== [GPS] START() CALLED ==========");
        Debug.Log($"[GPS] Platform: {Application.platform}");
        Debug.Log($"[GPS] isMobilePlatform: {Application.isMobilePlatform}");
        Debug.Log($"[GPS] Time: {System.DateTime.Now:HH:mm:ss.fff}");

        if (map == null) map = GetComponent<KiloverseMapInfo>();
        Debug.Log($"[GPS] Map component: {(map != null ? "FOUND" : "NULL")}");

        // Default to rotating the Player GameObject (not the camera)
        if (targetToRotate == null)
        {
            Debug.Log("[GPS] Searching for Player GameObject...");
            // Find the Player GameObject (parent of Main Camera)
            if (Camera.main != null && Camera.main.transform.parent != null)
            {
                targetToRotate = Camera.main.transform.parent;
                Debug.Log($"[GPS] ✓ Auto-assigned targetToRotate to Player: {targetToRotate.name}");
            }
            else
            {
                Debug.LogWarning("[GPS] ⚠ Could not find Player parent of Main Camera!");
            }
        }
        else
        {
            Debug.Log($"[GPS] targetToRotate already set: {targetToRotate.name}");
        }

        // In Editor: follow player without GPS by tracking movement
        // On Mobile: use actual GPS
        if (!Application.isMobilePlatform)
        {
            Debug.Log("[GPS] Editor/Desktop mode - using simulated GPS");
            StartCoroutine(EditorModeUpdate());
            yield break;
        }

        Debug.Log("[GPS] Mobile platform confirmed - proceeding with real GPS");
        Debug.Log($"[GPS] useGPSOnMobile setting: {useGPSOnMobile}");

        if (!useGPSOnMobile)
        {
            Debug.LogWarning("[GPS] GPS disabled by useGPSOnMobile setting - using default location");
            GPSReady = true;
            yield break;
        }

        // Check permissions - GPS is REQUIRED to play
        Debug.Log("[GPS] Checking system location permissions...");
        Debug.Log($"[GPS] Input.location.isEnabledByUser: {Input.location.isEnabledByUser}");
        Debug.Log($"[GPS] Input.location.status (before Start): {Input.location.status}");

        if (!Input.location.isEnabledByUser)
        {
            Debug.LogWarning("[GPS] Location services disabled by user - using default location");
            GPSReady = true;
            ShowGPSRequiredAlert("Location services are disabled. Using default location from settings.");
            yield break;
        }

        Debug.Log("[GPS] ✓ Location services are enabled system-wide");

        // Start service - iOS will handle permission dialog asynchronously
        Debug.Log("[GPS] ========== STARTING LOCATION SERVICE ==========");
        Debug.Log($"[GPS] Time before Start(): {System.DateTime.Now:HH:mm:ss.fff}");
        Debug.Log($"[GPS] Status before Start(): {Input.location.status}");

        Debug.Log("[GPS] ========================================");
        Debug.Log("[GPS] CALLING Input.location.Start(5f, 0f)...");
        Debug.Log("[GPS] ========================================");
        BootDiagnostics.Mark("GPS Input.location.Start");
        Input.location.Start(5f, 0f); // Accuracy 5m, Update distance 0m
        // Location service started

        Debug.Log("[GPS] Enabling compass...");
        Input.compass.enabled = true;
        Debug.Log("[GPS] ✓ Compass enabled");

        // Don't poll status - just wait for iOS to call back with a location or failure
        // Check status periodically but not in tight loop
        // Yielding to let other systems initialize
        yield return null;

        // Wait a frame for iOS to process the Start() request
        yield return null;

        Debug.Log("[GPS] ========================================");
        Debug.Log("[GPS] ✓✓✓ AFTER YIELD - COROUTINE RESUMED ✓✓✓");
        Debug.Log("[GPS] This line proves the coroutine continued!");
        Debug.Log("[GPS] ========================================");
        BootDiagnostics.Mark("GPS after yield");
        Debug.Log($"[GPS] Time after yield: {System.DateTime.Now:HH:mm:ss.fff}");
        Debug.Log($"[GPS] Status after yield: {Input.location.status}");

        // If already failed (permission denied immediately), handle it
        if (Input.location.status == LocationServiceStatus.Failed)
        {
            Debug.LogWarning("[GPS] GPS failed immediately - using default location");
            GPSReady = true;
            ShowGPSRequiredAlert("Location permission was denied. Using default location from settings.");
            yield break;
        }

        // Wait for status to change from Initializing (iOS is getting first location fix)
        // Check every 2 seconds instead of every frame
        Debug.Log("[GPS] Entering wait loop for GPS initialization...");
        BootDiagnostics.Mark("GPS waiting for fix");
        int waitCount = 0;
        const float initTimeoutSeconds = 30f;
        float initStartTime = Time.realtimeSinceStartup;
        while (Input.location.status == LocationServiceStatus.Initializing)
        {
            if (Time.realtimeSinceStartup - initStartTime > initTimeoutSeconds)
            {
                Debug.LogWarning("[GPS] GPS initialization timed out - using default location");
                BootDiagnostics.Mark("GPS init timeout");
                GPSReady = true;
                ShowGPSRequiredAlert("GPS timed out. Using default location from settings.");
                yield break;
            }
            waitCount++;
            Debug.Log($"[GPS] [{waitCount}] Status: {Input.location.status} - waiting for first location fix... Time: {System.DateTime.Now:HH:mm:ss.fff}");
            yield return new WaitForSeconds(2f);
            Debug.Log($"[GPS] [{waitCount}] After 2s wait - Status: {Input.location.status}");
        }

        // Status changed - check result
        Debug.Log($"[GPS] ========== STATUS CHANGED ==========");
        Debug.Log($"[GPS] Final status: {Input.location.status}");
        Debug.Log($"[GPS] Time: {System.DateTime.Now:HH:mm:ss.fff}");

        if (Input.location.status == LocationServiceStatus.Failed)
        {
            Debug.LogWarning("[GPS] GPS failed after initialization - using default location");
            GPSReady = true;
            ShowGPSRequiredAlert("GPS failed to get your location. Using default location from settings.");
            yield break;
        }

        // Success! GPS is now Running
        Debug.Log("[GPS] ========== ✓✓✓ GPS SUCCESS ✓✓✓ ==========");
        Debug.Log("[GPS] GPS is now RUNNING");
        Debug.Log($"[GPS] Last location: lat={Input.location.lastData.latitude}, lon={Input.location.lastData.longitude}");
        BootDiagnostics.Mark("GPS ready running");
        isInitialized = true;
        GPSReady = true;

        // NOW create services that depend on GPS (after GPS is ready!)
        Debug.Log("[GPS] Creating dependent services (after GPS ready)...");
        EnsureServices();
        Debug.Log("[GPS] ✓ Dependent services created");

        // Start update loop
        Debug.Log("[GPS] Starting UpdateLocationRoutine()...");
        StartCoroutine(UpdateLocationRoutine());
        Debug.Log("[GPS] ========== GPS Start() COMPLETE ==========");
        BootDiagnostics.Mark("GPS.Start complete");
    }

    private float compassPauseTimer = 0f;

    public void PauseCompass(float duration)
    {
        compassPauseTimer = duration;
    }

    private static bool _loggedFirstUpdateAfterBoot;
    void Update()
    {
        if (BootState.AllowPlayer && !_loggedFirstUpdateAfterBoot)
        {
            _loggedFirstUpdateAfterBoot = true;
            BootDiagnostics.Mark("GPSLocationController first Update after AllowPlayer");
        }
        if (compassPauseTimer > 0)
        {
            compassPauseTimer -= Time.deltaTime;
            return; // Skip compass update
        }

        if (isInitialized && useCompass && targetToRotate != null)
        {
            // Update Rotation
            float heading = Input.compass.trueHeading;
            // If heading is 0, it might be invalid, but we'll assume it's North.
            // Smoothly rotate
            Quaternion targetRotation = Quaternion.Euler(0, heading, 0);
            targetToRotate.rotation = Quaternion.Slerp(targetToRotate.rotation, targetRotation, Time.deltaTime * compassSmoothing);
        }
    }

    IEnumerator UpdateLocationRoutine()
    {
        while (isInitialized)
        {
            if (Input.location.status == LocationServiceStatus.Running)
            {
                LocationInfo loc = Input.location.lastData;
                
                // Check if we have new data based on timestamp
                if (lastLocation.timestamp != loc.timestamp)
                {
                    UpdateMap(loc);
                    lastLocation = loc;
                }
            }
            yield return new WaitForSeconds(updateInterval);
        }
    }

    IEnumerator EditorModeUpdate()
    {
        // In editor, mark GPS ready immediately (no real GPS needed).
        GPSReady = true;
        Debug.Log("[GPS] Editor mode: GPSReady set immediately (no real GPS)");
        BootDiagnostics.Mark("GPS editor ready");

        // Wait for map reference before creating dependent services.
        float waitStart = Time.realtimeSinceStartup;
        while (map == null)
        {
            if (Time.realtimeSinceStartup - waitStart > 10f)
            {
                Debug.LogWarning("[GPS] Editor: Map not found after 10s; delaying dependent services.");
                yield break;
            }
            yield return new WaitForSeconds(0.5f);
        }

        Debug.Log("[GPS] Editor mode: Map initialized (KiloverseMapInfo). Player can explore freely. Tiles load based on player GPS position.");

        // Create dependent services (after map exists) - yield to prevent blocking
        Debug.Log("[GPS] Editor: Creating dependent services...");
        yield return null; // Yield before creating services to prevent blocking boot
        EnsureServices();
        yield return null; // Yield after creating services
        Debug.Log("[GPS] Editor: ✓ Dependent services created");
    }


    void UpdateMap(LocationInfo loc)
    {
        if (map != null && followUser)
        {
            var latLon = new LatitudeLongitude(loc.latitude, loc.longitude);

            if (!isMapInitialized)
            {
                // First Update: Center the Map on the User
                map.SetPosition(loc.latitude, loc.longitude);

                isMapInitialized = true;
                Debug.Log($"[GPS] ✓ Map Initialized at GPS location: ({loc.latitude:F6}, {loc.longitude:F6})");
                Debug.Log($"[GPS] Tiles will now load based on your location. Move around to see new areas!");
            }
            else
            {
                // Subsequent Updates: FLOATING ORIGIN SYSTEM
                // Map center tracks GPS, player stays at Unity origin (0,0,0)
                // Tiles reposition automatically in OvertureMapManager (conveyor belt effect)
                map.SetPosition(loc.latitude, loc.longitude);

                if (targetToRotate != null)
                {
                    // CRITICAL: Keep player at Unity origin (0, 0, 0) - world moves around player
                    // Only preserve Y coordinate (height)
                    targetToRotate.position = new Vector3(0f, targetToRotate.position.y, 0f);

                    // Update player's GPS field (KiloFirstPersonController.playerGPS)
                    var playerController = targetToRotate.GetComponent<KiloFirstPersonController>();
                    if (playerController != null)
                    {
                        playerController.playerGPS = new LatitudeLongitude(loc.latitude, loc.longitude);
                    }
                }

                Debug.Log($"[GPS] Updated GPS: ({loc.latitude:F6}, {loc.longitude:F6}) | Player stays at Unity origin (0,0,0)");
            }
        }
    }
void EnsureServices()
    {
        Debug.Log("[GPS] EnsureServices() called");

        // In editor, skip PedometerService creation (not available on Mac) but still create UserPresenceManager for API pings
        if (Application.isEditor)
        {
            Debug.Log("[GPS] Editor mode: Skipping PedometerService creation (not available on Mac)");
            // Still create UserPresenceManager so heartbeat/ping runs and APIManager gets initialized
            if (GetComponent<UserPresenceManager>() == null)
            {
                Debug.Log("[GPS] Editor: Creating UserPresenceManager for API pings...");
                gameObject.AddComponent<UserPresenceManager>();
                Debug.Log("[GPS] ✓ Auto-added UserPresenceManager (editor)");
            }
            return;
        }

        if (GetComponent<PedometerService>() == null)
        {
            Debug.Log("[GPS] Creating PedometerService...");
            gameObject.AddComponent<PedometerService>();
            Debug.Log("[GPS] ✓ Auto-added PedometerService.");
        }
        else
        {
            Debug.Log("[GPS] PedometerService already exists");
        }
        
        // Create PedometerUI directly if it doesn't exist
        // PedometerUI removed - now handled by UILayoutManager
        // UILayoutManager creates the modular UI components (WeatherView, StepsView, etc.)
        
        if (GetComponent<UserPresenceManager>() == null)
        {
            Debug.Log("[GPS] Creating UserPresenceManager...");
            gameObject.AddComponent<UserPresenceManager>();
            Debug.Log("[GPS] ✓ Auto-added UserPresenceManager.");
        }
    }

    void OnDisable()
    {
        if (isInitialized)
        {
            Input.location.Stop();
            Input.compass.enabled = false;
        }
    }

    void ShowGPSRequiredAlert(string message)
    {
        Debug.LogError($"[GPS] GPS REQUIRED: {message}");

        // On iOS, use native alert dialog
#if UNITY_IOS && !UNITY_EDITOR
        if (Application.platform == RuntimePlatform.IPhonePlayer)
        {
            UnityEngine.iOS.Device.SetNoBackupFlag(Application.persistentDataPath);
            // Use iOS native alert
            StartCoroutine(ShowIOSAlert("GPS Required", message));
        }
#endif

        // For all platforms, also show Unity UI alert if available
        // TODO: Create a proper UI alert system
        // For now, just log the error - user will see grey screen and check logs
    }

    IEnumerator ShowIOSAlert(string title, string message)
    {
#if UNITY_IOS && !UNITY_EDITOR
        // Simple iOS alert using Unity's built-in functionality
        // Note: Unity doesn't have a built-in alert, so we'll just log it prominently
        Debug.LogError($"===== {title} =====");
        Debug.LogError(message);
        Debug.LogError("===================");
#endif
        yield return null;
    }
}
