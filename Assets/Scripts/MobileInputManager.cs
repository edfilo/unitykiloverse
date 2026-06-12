using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class MobileInputManager : MonoBehaviour
{
    private static MobileInputManager instance;
    private static float lastCameraToggleTime = -10f;
    private static float lastGpsToggleTime = -10f;

    [Header("Settings")]
    public bool forceMobileMode = false;
    public float joystickSize = 150f;
    public float handleSize = 50f;

    [Header("References")]
    public KiloFirstPersonController playerController;

    private GameObject canvasGO;
    private RectTransform moveJoystick;
    private RectTransform rotateJoystick;
    private Vector2 moveInput;
    private Vector2 rotateInput;

    private float currentRotateVelocity = 0f;
    private bool isTouching = false;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    void Start()
    {
        // Check if we should enable mobile controls
        bool isMobile = Application.isMobilePlatform || forceMobileMode;

        if (!isMobile)
        {
            Destroy(this);
            return;
        }

        if (playerController == null)
            playerController = FindObjectOfType<KiloFirstPersonController>();

        Debug.Log($"[MobileInputManager] Start completed. PlayerController: {playerController != null}");

        // Ensure PedometerService exists (Auto-Spawn)
        if (FindObjectOfType<PedometerService>() == null)
        {
            Debug.Log("[MobileInputManager] Spawning PedometerService...");
            GameObject pedometerGO = new GameObject("PedometerService");
            pedometerGO.AddComponent<PedometerService>();
        }

        CreateMobileUI();
    }

    void Update()
    {
        if (playerController != null)
        {
            // Apply Inertia if not touching
            if (!isTouching)
            {
                if (Mathf.Abs(currentRotateVelocity) > 0.001f)
                {
                    currentRotateVelocity *= 0.95f; // Decay factor (adjust for feel)
                    rotateInput.x = currentRotateVelocity;
                }
                else
                {
                    rotateInput.x = 0f;
                    currentRotateVelocity = 0f;
                }
            }

            // Map inputs to controller. With GPS on, drag only rotates (movement comes from real GPS).
            // With GPS testing-disabled, drag-Y also moves forward/back so the player can be walked manually.
            float moveY = GPSLocationController.GPSDisabled ? moveInput.y : 0f;
            playerController.externalInput = new Vector2(rotateInput.x, moveY);
            
            // Debug.Log($"[MobileInputManager] Sending Input: {playerController.externalInput}");
            
            // SignalCameraSwipe removed - Drag now moves player, Tap toggles view
        }
        else
        {
            // Debug warning throttled
            if (Time.frameCount % 300 == 0) Debug.LogWarning("[MobileInputManager] PlayerController is NULL! Input ignored.");
        }
    }

    void CreateMobileUI()
    {
        // Create Canvas
        canvasGO = new GameObject("MobileControlsCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -1; // Low order so HUD buttons (100+) stay on top
        
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920); // Portrait reference
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // Create EventSystem if missing
        if (FindObjectOfType<EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        // Create Full Screen Touch Panel (below the GPS toggle so the button stays clickable).
        // - GPS enabled (normal): horizontal drag rotates camera (no movement).
        // - GPS disabled (debug): vertical drag moves forward/back, horizontal rotates.
        GameObject touchPanel = new GameObject("TouchPanel");
        touchPanel.transform.SetParent(canvasGO.transform);
        Image panelImg = touchPanel.AddComponent<Image>();
        panelImg.color = Color.clear; // Invisible

        RectTransform panelRect = touchPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        // Add Drag Handler
        FullScreenDragHandler handler = touchPanel.AddComponent<FullScreenDragHandler>();
        handler.sensitivity = 0.005f; // Adjust sensitivity as needed
        handler.OnInput = (move, rotate) =>
        {
            // Only allow manual movement when GPS is disabled.
            float gatedMove = GPSLocationController.GPSDisabled ? move : 0f;
            moveInput = new Vector2(0, gatedMove); // Y is forward/back
            rotateInput = new Vector2(rotate, 0); // X is rotate left/right
            if (rotate != 0) currentRotateVelocity = rotate; // Capture velocity
        };
        handler.OnStateChange = (dragging) => { isTouching = dragging; };
        handler.OnTap = () =>
        {
            if (playerController == null) return;
            if (Time.unscaledTime - lastCameraToggleTime < 0.35f) return;
            lastCameraToggleTime = Time.unscaledTime;
            Debug.Log("[MobileInput] TAP → ToggleCameraView");
            playerController.ToggleCameraView();
        };

        // GPS toggle button (testing only) — small, bottom-right — inside safe area.
        RectTransform safeArea = CreateSafeAreaRect(canvasGO.transform);
        CreateGpsToggleButton(safeArea);
    }

    RectTransform CreateSafeAreaRect(Transform parent)
    {
        var go = new GameObject("SafeArea");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();

        Rect safe = Screen.safeArea;
        Vector2 anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
        Vector2 anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        return rt;
    }

    void CreateGpsToggleButton(Transform parent)
    {
        var go = new GameObject("GpsToggleButton");
        go.transform.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.55f);

        var rt = go.GetComponent<RectTransform>();
        // Bottom-left inside safe area, but lifted above the dock icons (Geo/etc).
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(24f, 260f);
        rt.sizeDelta = new Vector2(160f, 56f);

        var btn = go.AddComponent<Button>();

        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(go.transform, false);
        var label = labelGo.AddComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.alignment = TextAnchor.MiddleCenter;
        label.fontSize = 22;
        label.color = Color.white;
        label.text = GPSLocationController.GPSDisabled ? "GPS: OFF" : "GPS: ON";
        var lrt = label.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;

        btn.onClick.AddListener(() =>
        {
            // Some scenes have multiple active UI input modules; on iOS this can result in
            // duplicate click events for a single tap. Debounce to avoid toggling twice.
            if (Time.unscaledTime - lastGpsToggleTime < 0.35f) return;
            lastGpsToggleTime = Time.unscaledTime;

            GPSLocationController.GPSDisabled = !GPSLocationController.GPSDisabled;
            label.text = GPSLocationController.GPSDisabled ? "GPS: OFF" : "GPS: ON";
            img.color = GPSLocationController.GPSDisabled
                ? new Color(0.6f, 0.1f, 0.1f, 0.7f)
                : new Color(0f, 0f, 0f, 0.55f);
            Debug.Log($"[MobileInput] GPSDisabled toggled → {GPSLocationController.GPSDisabled}");
        });
    }

    // Helper class for full screen drag logic
    public class FullScreenDragHandler : MonoBehaviour, IDragHandler, IPointerUpHandler, IPointerDownHandler
    {
        public System.Action<float, float> OnInput; // move (y), rotate (x)
        public System.Action<bool> OnStateChange;
        public System.Action OnTap;
        public float sensitivity = 0.01f;

        private Vector2 startPos;
        private float startTime;
        private bool isDragging = false;
        private bool hasMoved = false; // True if any OnDrag was received
        private float lastDragEndTime = -1f; // Cooldown to ignore phantom taps after drags
        private enum Axis { None, Horizontal, Vertical }
        private Axis lockedAxis = Axis.None;
        private const float LOCK_THRESHOLD_PX = 5f;
        private const float TAP_TIME = 0.2f;
        private const float TAP_THRESHOLD_PX = 10f; // Must be nearly stationary

        private GPSLocationController gps;

        void Start()
        {
            gps = FindObjectOfType<GPSLocationController>();
            if (gps == null) Debug.LogWarning("[MobileInput] GPSLocationController not found!");
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            Debug.Log($"[MobileInput] Pointer Down on: {eventData.pointerEnter?.name}");

            if (ShouldPassTouchToHud(eventData))
            {
                Debug.Log("[MobileInput] HUD element detected in raycast stack - passing through touch");
                isDragging = false;
                return;
            }

            // Fallback: direct button ancestry check on the pointer target
            if (eventData.pointerEnter != null)
            {
                var button = eventData.pointerEnter.GetComponentInParent<Button>();
                if (button != null)
                {
                    Debug.Log($"[MobileInput] Button detected: {button.name} - passing through touch");
                    isDragging = false;
                    return;
                }
            }

            startPos = eventData.position;
            startTime = Time.time;
            isDragging = true;
            hasMoved = false;
            lockedAxis = Axis.None;
            OnStateChange?.Invoke(true);
        }

        bool ShouldPassTouchToHud(PointerEventData eventData)
        {
            if (EventSystem.current == null)
                return false;

            var results = new List<RaycastResult>(16);
            EventSystem.current.RaycastAll(eventData, results);

            foreach (RaycastResult result in results)
            {
                GameObject go = result.gameObject;
                if (go == null || go == gameObject)
                    continue;

                if (go.GetComponentInParent<Button>() != null)
                    return true;

                Transform hudRoot = go.transform.GetComponentInParent<Canvas>()?.transform;
                if (hudRoot != null && hudRoot.name == "K1L0_Canvas")
                    return true;
            }

            return false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!isDragging) return;
            hasMoved = true;
            Vector2 currentPos = eventData.position;
            Vector2 delta = currentPos - startPos;
            
            Debug.Log($"[MobileInput] Dragging. Delta: {delta}, Locked: {lockedAxis}");

            // Determine Axis Lock if not yet locked
            if (lockedAxis == Axis.None)
            {
                float ax = Mathf.Abs(delta.x);
                float ay = Mathf.Abs(delta.y);
                if (ax > LOCK_THRESHOLD_PX || ay > LOCK_THRESHOLD_PX)
                {
                    // Lock to the dominant axis (prevents tiny sideways jitter from breaking vertical movement).
                    lockedAxis = ax >= ay ? Axis.Horizontal : Axis.Vertical;
                }
            }

            // Apply Locking
            if (lockedAxis == Axis.Horizontal)
            {
                delta.y = 0; // Ignore vertical
                if (gps != null) gps.PauseCompass(5.0f); // Pause compass while rotating
            }
            else if (lockedAxis == Axis.Vertical)
            {
                delta.x = 0; // Ignore horizontal
            }
            else
            {
                // Below threshold, ignore both to prevent jitter
                return; 
            }

            // Normalize delta based on screen height to be resolution independent
            float screenFactor = Screen.height; 
            
            // Calculate inputs
            // Up/Down (Y) -> Move Forward/Back
            float moveY = Mathf.Clamp(delta.y / (screenFactor * 0.1f), -1f, 1f);
            
            // Left/Right (X) -> Rotate
            float rotateX = Mathf.Clamp(delta.x / (screenFactor * 0.1f), -1f, 1f);

            OnInput?.Invoke(moveY, rotateX);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            // Tap detection: must be short duration AND finger barely moved
            if (isDragging)
            {
                Vector2 totalDelta = eventData.position - startPos;
                float dur = Time.time - startTime;
                // A tap is: very short hold, AND less than 3px movement in any direction
                // Any horizontal or vertical movement = swipe, not tap
                // Ignore phantom zero-duration taps that fire immediately after a drag release
                float timeSinceLastDrag = Time.time - lastDragEndTime;
                bool isTap = !hasMoved
                    && dur < TAP_TIME
                    && dur > 0.01f // Must have actual duration (phantom taps have dur=0)
                    && timeSinceLastDrag > 0.15f // Cooldown after drags
                    && Mathf.Abs(totalDelta.x) < 3f
                    && Mathf.Abs(totalDelta.y) < 3f;
                Debug.Log($"[MobileInput] PointerUp: hasMoved={hasMoved} dur={dur:F3}s delta=({totalDelta.x:F1},{totalDelta.y:F1}) dragCooldown={timeSinceLastDrag:F3}s isTap={isTap}");
                if (hasMoved) lastDragEndTime = Time.time;
                if (isTap)
                {
                    Debug.Log("[MobileInput] ✓ TAP → ToggleCameraView");
                    OnTap?.Invoke();
                }
            }

            isDragging = false;
            lockedAxis = Axis.None;
            OnStateChange?.Invoke(false);
            // Do NOT reset OnInput to 0 here if we want inertia to take over in Update
            // But we must reset Y (Move) because movement shouldn't have inertia
            OnInput?.Invoke(0, 0); // Actually, we reset, but manager captured velocity
        }
    }
}
