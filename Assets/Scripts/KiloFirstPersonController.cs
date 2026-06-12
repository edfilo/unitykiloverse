using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UI;

using KiloWorld.Rendering;
using KiloWorld.Rendering.Systems;
using System.Collections.Generic;
using Kiloverse.Mapbox;

public class KiloFirstPersonController : MonoBehaviour
{
    [Header("References")]
    public CharacterController controller;
    public Transform cameraTransform;
    public Animator animator;
    public KiloverseMapInfo map;

    [Header("GPS Position (Source of Truth)")]
    [Tooltip("Player's GPS position - updated by arrow keys, used to position player in Unity space")]
    public LatitudeLongitude playerGPS;

    [Header("Helmet Replacement")]
    public GameObject motorcycleHelmetPrefab;
    public Vector3 helmetScale = Vector3.one;
    public Vector3 helmetPositionOffset = Vector3.zero;
    public bool forceHelmetWhite = true;
    public Color helmetOverrideColor = Color.white;

    [Header("Movement Settings")]
    public float moveSpeed = 50f;
    public float gpsOffMoveSpeed = 8f;
    public float gpsOffKeyboardSpeedMultiplier = 3f;
    public float gpsOffMobileSpeedMultiplier = 3f;
    public float rotationSpeed = 120f;

    [Header("Direction Cone")]
    public bool showDirectionCone = true;
    public float directionConeDistance = 1.7f;
    public float directionConeLength = 2.4f;
    public float directionConeWidth = 1.1f;
    public Color directionConeColor = new Color(0.25f, 1f, 0.35f, 0.72f);
    
    [Header("Animation Settings")]
    [SerializeField] private string speedParameter = "Speed";
    [SerializeField] private string walkingBoolParameter = "IsWalking";
    public float animationBlendSpeed = 5f;

    [Header("Camera Settings")]
    public float cameraPitch = 0f;
    public float cameraHeight = 1.6f;
    public float cameraDistance = -0.3f;

    private Vector3 moveDirection = Vector3.zero;
    private float lastLogTime = -5f; // Start at -5 so first log happens immediately
    private bool hasSpeedParameter;
    private PedometerService pedometerService;

    private string GetCardinalDirection(float yaw)
    {
        // Convert yaw to 0-360
        yaw = (yaw + 360f) % 360f;
        if (yaw >= 337.5f || yaw < 22.5f) return "N";
        if (yaw >= 22.5f && yaw < 67.5f) return "NE";
        if (yaw >= 67.5f && yaw < 112.5f) return "E";
        if (yaw >= 112.5f && yaw < 157.5f) return "SE";
        if (yaw >= 157.5f && yaw < 202.5f) return "S";
        if (yaw >= 202.5f && yaw < 247.5f) return "SW";
        if (yaw >= 247.5f && yaw < 292.5f) return "W";
        return "NW";
    }
    private bool hasWalkingBoolParameter;
    private KiloWorldMasterProfile profile;
    private GameObject directionConeObject;
    private Material directionConeMaterial;

    private bool isGodViewActive = false; // Toggles between default and God View
    public bool IsGodView => isGodViewActive;
    private float currentCameraTransitionTime = 1f; // Start completed (no animation until tap triggers it)
    private Vector3 cameraInitialLocalPos;
    private Quaternion cameraInitialLocalRot;

    void Awake()
    {
        Debug.Log("[KiloFirstPersonController] Awake() called on " + gameObject.name);
    }

    void Start()
    {
        Debug.Log("=== KILO FIRST PERSON CONTROLLER START ===");

        // Get components if not assigned
        if (controller == null)
            controller = GetComponent<CharacterController>();

        // On mobile, CharacterController capsule collider interacts with map tile geometry,
        // pushing player up/down when rotating near colliders → visible camera bob on touch release.
        // Disable it on mobile (not needed — movement is GPS-driven).
        if (controller != null)
        {
            if (Application.isMobilePlatform)
            {
                Debug.Log("[KiloFirstPersonController] Mobile: disabling CharacterController (causes camera drift)");
                controller.enabled = false;
            }
            else
            {
                controller.stepOffset = 0.6f;
                controller.skinWidth = 0.08f;
                controller.minMoveDistance = 0f;
                controller.slopeLimit = 50f;
            }
        }

        if (animator == null)
            animator = GetComponent<Animator>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);
        
        if (animator == null)
        {
            Debug.LogWarning("[KiloFirstPersonController] Animator component not found on player or children.");
        }
        else
        {
            // Disable root motion — it moves the player transform each frame,
            // causing camera lift/tilt on touch release
            if (animator.applyRootMotion)
            {
                Debug.Log("[KiloFirstPersonController] Disabling Animator root motion (prevents camera drift)");
                animator.applyRootMotion = false;
            }

            if (animator.runtimeAnimatorController != null)
            {
                CacheAnimatorParameters();
            }
            else
            {
                Debug.LogWarning("[KiloFirstPersonController] Animator has no RuntimeAnimatorController assigned!");
            }
        }

        // Get camera transform if not assigned
        if (cameraTransform == null)
        {
            Camera cam = GetComponentInChildren<Camera>(true);
            if (cam != null)
            {
                cameraTransform = cam.transform;
                Debug.Log($"[KiloFirstPersonController] Auto-assigned cameraTransform: {cameraTransform.name}");
            }
            else
            {
                Debug.LogWarning("[KiloFirstPersonController] Camera component not found on player or children.");
            }
        }

        if (map == null)
            map = FindFirstObjectByType<KiloverseMapInfo>();

        // Initialize player GPS to map center
        if (map != null)
        {
            playerGPS = map.MapInformation.Position;
            Debug.Log($"[KiloFirstPersonController] Initialized player GPS: ({playerGPS.Latitude:F6}, {playerGPS.Longitude:F6})");
        }

        // Get profile from RenderManager
        var renderManager = FindFirstObjectByType<RenderManager>();
        if (renderManager != null)
        {
            profile = renderManager.profile;
        }
        
        // Initialize camera positions based on profile
        if (profile != null && cameraTransform != null)
        {
            cameraInitialLocalPos = new Vector3(
                0f,
                profile.camera.fpPositionY,
                -profile.camera.fpPositionZ
            );
            cameraInitialLocalRot = Quaternion.Euler(
                profile.camera.fpRotationX,
                0f,
                0f
            );
            cameraTransform.localPosition = cameraInitialLocalPos;
            cameraTransform.localRotation = cameraInitialLocalRot;
        }

        // // Log component status
        // Debug.Log($"CharacterController: {(controller != null ? "FOUND" : "MISSING")}");
        // Debug.Log($"Animator: {(animator != null ? "FOUND" : "MISSING")}");
        // Debug.Log($"Camera Transform: {(cameraTransform != null ? "ASSIGNED" : "MISSING")}");
        // Debug.Log($"Map: {(map != null ? "FOUND" : "MISSING")}");
        // Debug.Log($"Player Position: {transform.position}");
        // Debug.Log($"Time.timeScale: {Time.timeScale}");
        // Debug.Log($"enabled: {enabled}");
        // Debug.Log($"gameObject.activeInHierarchy: {gameObject.activeInHierarchy}");

        // if (cameraTransform != null)
        //     Debug.Log($"Camera Position: {cameraTransform.position}");

        ReplaceHelmet();
        EnsureDirectionCone();
    }

    void ReplaceHelmet()
    {
        if (motorcycleHelmetPrefab == null)
        {
            Debug.LogWarning("[KiloFirstPersonController] Motorcycle Helmet Prefab not assigned! Please assign it in the Inspector.");
            return;
        }

        // Try to find the bones recursively
        Transform headBone = FindDeepChild(transform, "Bip001 Head");
        if (headBone == null) headBone = FindDeepChild(transform, "Head"); // Fallback

        Transform neckBone = FindDeepChild(transform, "Bip001 Neck"); 
        if (neckBone == null) neckBone = FindDeepChild(transform, "Neck"); // Fallback

        Debug.Log($"[KiloFirstPersonController] Head found: {(headBone != null ? headBone.name : "null")}, Neck found: {(neckBone != null ? neckBone.name : "null")}");

        if (headBone != null && neckBone != null)
        {
            // Instantiate helmet
            GameObject newHelmet = Instantiate(motorcycleHelmetPrefab);
            newHelmet.name = "MotorcycleHelmet_Instance";
            ApplyHelmetColor(newHelmet);
            
            // Apply user defined scale
            newHelmet.transform.localScale = helmetScale;

            // Initial position match (plus offset)
            newHelmet.transform.position = headBone.position + (headBone.rotation * helmetPositionOffset);
            newHelmet.transform.rotation = headBone.rotation;
            
            // Parent to Neck to move with body
            newHelmet.transform.SetParent(neckBone); 

            // Add script to strictly follow head motion
            HelmetFollower follower = newHelmet.AddComponent<HelmetFollower>();
            follower.target = headBone;
            follower.offset = helmetPositionOffset; 
            
            // Hide the original head by scaling the bone to near-zero
            headBone.localScale = Vector3.one * 0.0001f;
            
            Debug.Log("[KiloFirstPersonController] Helmet replaced successfully and follower attached.");
        }
        else
        {
            Debug.LogError("[KiloFirstPersonController] Could not find bones for helmet replacement.");
        }
    }

    private void ApplyHelmetColor(GameObject helmet)
    {
        if (!forceHelmetWhite || helmet == null) return;

        var renderers = helmet.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            var renderer = renderers[i];
            if (renderer == null) continue;
            var mats = renderer.materials;
            for (int j = 0; j < mats.Length; j++)
            {
                var mat = mats[j];
                if (mat == null) continue;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", helmetOverrideColor);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", helmetOverrideColor);
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", helmetOverrideColor * 0.12f);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", Mathf.Min(mat.GetFloat("_Metallic"), 0.15f));
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", Mathf.Max(mat.GetFloat("_Smoothness"), 0.45f));
            }
        }
    }

    Transform FindDeepChild(Transform aParent, string aName)
    {
        Queue<Transform> queue = new Queue<Transform>();
        queue.Enqueue(aParent);
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            if (c.name == aName)
                return c;
            foreach(Transform t in c)
                queue.Enqueue(t);
        }
        return null;
    }

    void Update()
    {
        HandleDesktopGpsToggle();
        HandleMovement();
        HandleRotation();
        HandleCameraToggleTap();
    }

    void HandleDesktopGpsToggle()
    {
        if (Application.isMobilePlatform || Keyboard.current == null) return;
        if (!Keyboard.current.gKey.wasPressedThisFrame) return;
        GPSLocationController.GPSDisabled = !GPSLocationController.GPSDisabled;
        Debug.Log($"[Controller] Desktop GPSDisabled toggled → {GPSLocationController.GPSDisabled}");
    }

    void LateUpdate()
    {
        ApplyCameraRotation();
    }

    [Header("Input Settings")]
    public Vector2 externalInput = Vector2.zero; // X = Rotation, Y = Movement
    [HideInInspector] public float cameraTransitionSpeed = 5f; // Deprecated - now uses profile.camera.transitionTime

    void HandleMovement()
    {
        if (map == null || map.MapInformation == null) return;

        // DEBUG: Log player position and camera direction every 5 seconds
        if (Time.time - lastLogTime > 5f)
        {
            lastLogTime = Time.time;
            Vector3 cameraForward = cameraTransform != null ? cameraTransform.forward : transform.forward;
            float cameraYaw = Mathf.Atan2(cameraForward.x, cameraForward.z) * Mathf.Rad2Deg;

            // Calculate offset from map center to player
            var mapCenterGPS = new LatitudeLongitude(map.MapInformation.Position.Latitude, map.MapInformation.Position.Longitude);
            var playerGPSKiloverse = new LatitudeLongitude(playerGPS.Latitude, playerGPS.Longitude);
            var mapCenterMercator = Conversions.LatitudeLongitudeToWebMercator(mapCenterGPS);
            var playerMercator = Conversions.LatitudeLongitudeToWebMercator(playerGPSKiloverse);
            var offsetFromCenter = playerMercator - mapCenterMercator;

            Debug.Log($"[Player] GPS: ({playerGPS.Latitude:F6}, {playerGPS.Longitude:F6}), Unity Pos: {transform.position}, Camera Yaw: {cameraYaw:F1}°, Facing: {GetCardinalDirection(cameraYaw)}");
            Debug.Log($"[Player] Map Center GPS: ({mapCenterGPS.Latitude:F6}, {mapCenterGPS.Longitude:F6}), Offset from center: ({offsetFromCenter.x:F1}m, {offsetFromCenter.y:F1}m)");
        }

        // Start with external input (mobile joystick)
        float vertical = externalInput.y;
        bool keyboardMovement = false;

        // Add Keyboard input if no external input
        if (vertical == 0f && Keyboard.current != null)
        {
            if (Keyboard.current.upArrowKey.isPressed || Keyboard.current.wKey.isPressed)
            {
                vertical = 1f;
                keyboardMovement = true;
            }
            else if (Keyboard.current.downArrowKey.isPressed || Keyboard.current.sKey.isPressed)
            {
                vertical = -1f;
                keyboardMovement = true;
            }
        }

        float effectiveMoveSpeed = GPSLocationController.GPSDisabled ? gpsOffMoveSpeed : moveSpeed;
        if (GPSLocationController.GPSDisabled && keyboardMovement)
            effectiveMoveSpeed *= gpsOffKeyboardSpeedMultiplier;
        else if (GPSLocationController.GPSDisabled && Application.isMobilePlatform)
            effectiveMoveSpeed *= gpsOffMobileSpeedMultiplier;
        float metersToMove = vertical * effectiveMoveSpeed * Time.deltaTime;

        if (metersToMove != 0f)
        {
            // Calculate movement direction in Unity space (Web Mercator meters).
            // GPS-off walking follows the character body heading; top/god camera forward can point sideways.
            Vector3 moveForward = GPSLocationController.GPSDisabled ? GetPlanarCharacterForward() : transform.forward;
            Vector3 movementVector = moveForward * metersToMove;

            // Convert current GPS to Web Mercator
            var mapCenterLatLng = new LatitudeLongitude(map.MapInformation.Position.Latitude, map.MapInformation.Position.Longitude);
            var playerLatLng = new LatitudeLongitude(playerGPS.Latitude, playerGPS.Longitude);
            var mapCenterMercator = Conversions.LatitudeLongitudeToWebMercator(mapCenterLatLng);
            var playerMercator = Conversions.LatitudeLongitudeToWebMercator(playerLatLng);
            var previousPlayerMercator = playerMercator;

            // Add movement in Mercator space
            playerMercator = new Vector2d(
                playerMercator.x + movementVector.x,
                playerMercator.y + movementVector.z
            );

            if (GPSLocationController.GPSDisabled)
            {
                double dx = playerMercator.x - previousPlayerMercator.x;
                double dy = playerMercator.y - previousPlayerMercator.y;
                float actualWorldMeters = (float)System.Math.Sqrt(dx * dx + dy * dy);
                if (pedometerService == null) pedometerService = FindFirstObjectByType<PedometerService>();
                if (pedometerService != null) pedometerService.RegisterVirtualMovementMeters(actualWorldMeters);
            }

            // Convert back to GPS (this is now the source of truth)
            var latLonStruct = Conversions.WebMercatorToLatitudeLongitude(playerMercator);
            playerGPS = new LatitudeLongitude(latLonStruct.Latitude, latLonStruct.Longitude);

            // CRITICAL: Update map center immediately to player GPS (prevents drift)
            // This makes tiles reposition on THIS frame, keeping player at origin
            map.SetPosition(playerGPS.Latitude, playerGPS.Longitude);

            // FLOATING ORIGIN: Player stays at Unity (0, 0, 0) - world moves around player
            // CharacterController still needs a tiny move to trigger collisions
            Vector3 targetPosition = new Vector3(0f, transform.position.y, 0f);

            if (isGodViewActive)
            {
                // GOD VIEW: Noclip - Keep at origin
                transform.position = targetPosition;
            }
            else if (controller != null)
            {
                // FIRST PERSON: Move CharacterController to origin (triggers collision detection)
                Vector3 moveVector = targetPosition - transform.position;
                controller.Move(moveVector);
            }
            else
            {
                // Fallback: Keep at origin
                transform.position = targetPosition;
            }
        }

        // Update walking animation
        bool isWalking = vertical != 0f;
        if (animator != null)
        {
            float targetSpeed = isWalking ? Mathf.Abs(vertical) : 0f;
            if (hasSpeedParameter && !string.IsNullOrEmpty(speedParameter))
            {
                float currentSpeed = animator.GetFloat(speedParameter);
                float newSpeed = Mathf.Lerp(currentSpeed, targetSpeed, animationBlendSpeed * Time.deltaTime);
                animator.SetFloat(speedParameter, newSpeed);
            }
            if (hasWalkingBoolParameter && !string.IsNullOrEmpty(walkingBoolParameter))
            {
                animator.SetBool(walkingBoolParameter, isWalking);
            }
        }
    }

    private Vector3 GetPlanarCharacterForward()
    {
        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;
        return forward.normalized;
    }

    private void EnsureDirectionCone()
    {
        if (!showDirectionCone || directionConeObject != null) return;

        directionConeObject = new GameObject("PlayerDirectionCone");
        directionConeObject.transform.SetParent(transform, false);
        directionConeObject.transform.localPosition = new Vector3(0f, 0.06f, directionConeDistance);
        directionConeObject.transform.localRotation = Quaternion.identity;
        directionConeObject.transform.localScale = Vector3.one;

        var meshFilter = directionConeObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = CreateDirectionConeMesh();

        var meshRenderer = directionConeObject.AddComponent<MeshRenderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        directionConeMaterial = new Material(shader);
        directionConeMaterial.SetColor("_BaseColor", directionConeColor);
        directionConeMaterial.SetColor("_Color", directionConeColor);
        directionConeMaterial.renderQueue = 3000;
        meshRenderer.material = directionConeMaterial;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        Destroy(directionConeObject.GetComponent<Collider>());
    }

    private Mesh CreateDirectionConeMesh()
    {
        float halfWidth = Mathf.Max(0.05f, directionConeWidth * 0.5f);
        float length = Mathf.Max(0.1f, directionConeLength);
        Mesh mesh = new Mesh();
        mesh.name = "PlayerDirectionConeMesh";
        mesh.vertices = new[]
        {
            new Vector3(-halfWidth, 0f, 0f),
            new Vector3(halfWidth, 0f, 0f),
            new Vector3(0f, 0f, length),
            new Vector3(-halfWidth * 0.35f, 0.01f, 0.15f),
            new Vector3(halfWidth * 0.35f, 0.01f, 0.15f),
            new Vector3(0f, 0.01f, length * 0.72f)
        };
        mesh.triangles = new[] { 0, 2, 1, 3, 4, 5 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    void HandleRotation()
    {
        // Start with external input (mobile joystick)
        float horizontal = externalInput.x;

        // Add Keyboard input if no external input
        if (horizontal == 0f && Keyboard.current != null)
        {
            if (Keyboard.current.leftArrowKey.isPressed || Keyboard.current.aKey.isPressed)
                horizontal = -1f;
            else if (Keyboard.current.rightArrowKey.isPressed || Keyboard.current.dKey.isPressed)
                horizontal = 1f;
        }

        // DEBUG: Trace rotation input
        if (Mathf.Abs(horizontal) > 0.1f)
        {
            Debug.Log($"[Controller] Rotating: {horizontal}");
        }

        // Rotate character
        if (horizontal != 0f)
        {
            transform.Rotate(Vector3.up, horizontal * rotationSpeed * Time.deltaTime);
        }
    }

    // Tap/click toggles camera mode (God View <-> First Person).
    // We keep this here (instead of a full-screen UI overlay) so we don't block
    // map pan/tap interactions.
    private Vector2 _tapStartPos;
    private float _tapStartTime;
    private bool _tapTracking;
    private const float TapMaxDurationSeconds = 0.25f;
    private const float TapMaxMovePixels = 18f;

    void HandleCameraToggleTap()
    {
        // Mouse click (mac/editor)
        if (!Application.isMobilePlatform)
        {
            bool leftClick = (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) || Input.GetMouseButtonDown(0);
            if (!leftClick) return;

            Vector2 mousePos = Mouse.current != null ? Mouse.current.position.ReadValue() : (Vector2)Input.mousePosition;
            bool overUi = IsPointerOverUI(mousePos);
            Debug.Log($"[CameraToggleTap] click pos={mousePos} overUi={overUi} eventSystem={(EventSystem.current != null)}");
            if (!overUi) ToggleCameraView();
            return;
        }

        // Touch tap (iOS/Android)
        if (Touchscreen.current == null)
            return;

        var touch = Touchscreen.current.primaryTouch;
        Vector2 pos = touch.position.ReadValue();
        bool pressed = touch.press.isPressed;

        if (pressed && !_tapTracking)
        {
            if (IsPointerOverUI(pos)) return;
            _tapTracking = true;
            _tapStartPos = pos;
            _tapStartTime = Time.unscaledTime;
            return;
        }

        if (!pressed && _tapTracking)
        {
            _tapTracking = false;
            float dt = Time.unscaledTime - _tapStartTime;
            float dist = Vector2.Distance(pos, _tapStartPos);
            if (dt <= TapMaxDurationSeconds && dist <= TapMaxMovePixels && !IsPointerOverUI(pos))
                ToggleCameraView();
        }
    }

    bool IsPointerOverUI(Vector2 screenPos)
    {
        if (EventSystem.current == null) return false;
        var ped = new PointerEventData(EventSystem.current) { position = screenPos };
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(ped, results);
        if (results == null || results.Count == 0) return false;

        // Only treat "real" interactive UI as blocking the camera-toggle click.
        // Some full-screen HUD panels are raycast targets for other reasons; those
        // should not prevent click-to-toggle from working on desktop.
        foreach (var r in results)
        {
            var go = r.gameObject;
            if (go == null) continue;
            if (go.GetComponentInParent<Selectable>() != null) return true;
            if (go.GetComponentInParent<ScrollRect>() != null) return true;
        }
        return false;
    }

    private void CacheAnimatorParameters()
    {
        hasSpeedParameter = AnimatorHasParameter(speedParameter, AnimatorControllerParameterType.Float);
        hasWalkingBoolParameter = AnimatorHasParameter(walkingBoolParameter, AnimatorControllerParameterType.Bool);

        if (!hasSpeedParameter && !string.IsNullOrEmpty(speedParameter))
            Debug.LogWarning($"[KiloFirstPersonController] Animator missing float parameter '{speedParameter}'.");
        if (!hasWalkingBoolParameter && !string.IsNullOrEmpty(walkingBoolParameter))
            Debug.LogWarning($"[KiloFirstPersonController] Animator missing bool parameter '{walkingBoolParameter}'.");
    }

    private bool AnimatorHasParameter(string paramName, AnimatorControllerParameterType type)
    {
        if (animator == null || string.IsNullOrEmpty(paramName))
            return false;

        foreach (var param in animator.parameters)
        {
            if (param.type == type && param.name == paramName)
                return true;
        }

        return false;
    }
    
    // Public method to be called by input managers (e.g., MobileInputManager)
    public void SignalCameraSwipe(float swipeMagnitude)
    {
        if (Mathf.Abs(swipeMagnitude) > 0.1f) // Any significant swipe magnitude
        {
            if (swipeMagnitude > 0 && !isGodViewActive) // Swipe Up
            {
                isGodViewActive = true;
                currentCameraTransitionTime = 0f; // Reset transition time
                Debug.Log($"[Camera] Starting transition to GOD VIEW - transitionTime: {profile?.camera.transitionTime ?? 0}s");
            }
            else if (swipeMagnitude < 0 && isGodViewActive) // Swipe Down
            {
                isGodViewActive = false;
                currentCameraTransitionTime = 0f; // Reset transition time
                Debug.Log($"[Camera] Starting transition to FIRST PERSON - transitionTime: {profile?.camera.transitionTime ?? 0}s");
            }
        }
    }

    public void ToggleCameraView()
    {
        isGodViewActive = !isGodViewActive;
        currentCameraTransitionTime = 0f;
        Debug.Log($"[Camera] Toggled to {(isGodViewActive ? "GOD VIEW" : "FIRST PERSON")}\n{UnityEngine.StackTraceUtility.ExtractStackTrace()}");
    }

    private void ApplyCameraRotation()
    {
        if (cameraTransform == null || profile == null) return;

        Vector3 targetPos;
        Quaternion targetRot;

        if (isGodViewActive)
        {
            targetPos = new Vector3(
                0f,
                profile.camera.godPositionY,
                -profile.camera.godPositionZ
            );
            targetRot = Quaternion.Euler(
                profile.camera.godRotationX,
                0f,
                0f
            );
        }
        else
        {
            targetPos = cameraInitialLocalPos;
            targetRot = cameraInitialLocalRot;
        }

        if (currentCameraTransitionTime < 1f)
        {
            float transitionSpeed = 1.0f / Mathf.Max(0.1f, profile.camera.transitionTime);
            currentCameraTransitionTime += Time.deltaTime * transitionSpeed;
            float t = Mathf.Clamp01(currentCameraTransitionTime);
            cameraTransform.localPosition = Vector3.Lerp(cameraTransform.localPosition, targetPos, t);
            cameraTransform.localRotation = Quaternion.Lerp(cameraTransform.localRotation, targetRot, t);
        }
        else
        {
            cameraTransform.localPosition = targetPos;
            cameraTransform.localRotation = targetRot;
        }

        var cameraComponent = cameraTransform != null ? cameraTransform.GetComponent<Camera>() : Camera.main;
        if (cameraComponent != null)
            cameraComponent.farClipPlane = profile.camera.farClipPlane;
    }

    public void ApplyCameraProfileNow()
    {
        currentCameraTransitionTime = 1f;
        ApplyCameraRotation();
    }
}
