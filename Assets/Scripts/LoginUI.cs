using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Login screen with Apple Sign-In
/// Shows on app launch if user is not authenticated
/// </summary>
public class LoginUI : MonoBehaviour
{
    private GameObject loginPanel;
    private GameObject loginCanvas;
    private Text statusText;
    private bool isShowing = false;

    void Start()
    {
        Debug.Log("========== LoginUI.Start() CALLED ==========");

        // DISABLE LOGIN UI ENTIRELY FOR TESTING
        Debug.Log("[LoginUI] Login UI disabled - skipping");
        return;

        try
        {
            // FORCE SHOW FOR TESTING - ALWAYS SHOW THE LOGIN SCREEN
            bool FORCE_SHOW_LOGIN = false;

            #if UNITY_EDITOR
            // In Editor, clear any stored auth and always show login
            Debug.Log("[LoginUI] [EDITOR] Clearing stored auth");
            if (FirebaseAuthManager.Instance != null)
            {
                FirebaseAuthManager.Instance.SignOut();
                Debug.Log($"[LoginUI] After SignOut: isAuthenticated = {FirebaseAuthManager.Instance.isAuthenticated}");
            }
            else
            {
                Debug.LogError("[LoginUI] FirebaseAuthManager.Instance is NULL!");
            }
            #endif

            // Check if already authenticated (DISABLED FOR TESTING)
            if (!FORCE_SHOW_LOGIN && FirebaseAuthManager.Instance != null && FirebaseAuthManager.Instance.isAuthenticated)
            {
                Debug.Log("[LoginUI] User already authenticated, skipping login");
                return;
            }

            Debug.Log("[LoginUI] FORCE_SHOW_LOGIN=true - SHOWING RED TEST SCREEN");
            Debug.Log("[LoginUI] Not authenticated, showing login screen");
            CreateLoginUI();
            ShowLogin();

            // Ensure we stay on top even after other canvases are created
            StartCoroutine(EnsureOnTop());
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[LoginUI] Exception in Start: {e.Message}\n{e.StackTrace}");
        }

        // Listen for auth state changes
        FirebaseAuthManager.Instance.OnAuthStateChanged += OnAuthStateChanged;
        FirebaseAuthManager.Instance.OnAuthError += OnAuthError;

        // Set up Apple Sign-In handlers
        AppleSignInHandler.Instance.OnAppleSignInSuccess += OnAppleSignInSuccess;
        AppleSignInHandler.Instance.OnAppleSignInFailed += OnAppleSignInFailed;
    }

    void OnDestroy()
    {
        if (FirebaseAuthManager.Instance != null)
        {
            FirebaseAuthManager.Instance.OnAuthStateChanged -= OnAuthStateChanged;
        }

        if (AppleSignInHandler.Instance != null)
        {
            AppleSignInHandler.Instance.OnAppleSignInSuccess -= OnAppleSignInSuccess;
            AppleSignInHandler.Instance.OnAppleSignInFailed -= OnAppleSignInFailed;
        }
    }

    void CreateLoginUI()
    {
        // Ensure EventSystem exists
        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            Debug.Log("[LoginUI] Created EventSystem");
        }

        // Create full-screen canvas
        loginCanvas = new GameObject("LoginCanvas");
        Canvas canvas = loginCanvas.AddComponent<Canvas>();

        // Use simplest possible setup - ScreenSpaceOverlay needs no camera
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        Debug.Log($"[LoginUI] Using ScreenSpaceOverlay - simplest possible rendering mode");

        canvas.sortingOrder = short.MaxValue; // Absolute max value (32767)
        canvas.overrideSorting = true; // Force override
        canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.None; // Simplest rendering
        DontDestroyOnLoad(loginCanvas);

        CanvasScaler scaler = loginCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);

        GraphicRaycaster raycaster = loginCanvas.AddComponent<GraphicRaycaster>();
        raycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;

        // Find highest sorting order and go above it
        Canvas[] allCanvases = FindObjectsOfType<Canvas>();
        int maxOrder = 0;
        foreach (var c in allCanvases)
        {
            if (c != canvas && c.sortingOrder > maxOrder) maxOrder = c.sortingOrder;
        }
        canvas.sortingOrder = maxOrder + 1;

        Debug.Log($"[LoginUI] ===== CANVAS SETUP =====");
        Debug.Log($"[LoginUI] Canvas renderMode: {canvas.renderMode} (ScreenSpaceOverlay - no camera needed)");
        Debug.Log($"[LoginUI] Canvas sortingOrder: {canvas.sortingOrder} (max in scene was {maxOrder})");
        Debug.Log($"[LoginUI] Canvas enabled: {canvas.enabled}");
        Debug.Log($"[LoginUI] Canvas gameObject.activeInHierarchy: {canvas.gameObject.activeInHierarchy}");
        Debug.Log($"[LoginUI] Canvas layer: {canvas.gameObject.layer}");
        Debug.Log($"[LoginUI] ==========================");

        // Fully opaque dark background
        loginPanel = new GameObject("LoginPanel");
        loginPanel.layer = 5; // UI layer - so UI camera can see it
        loginPanel.transform.SetParent(loginCanvas.transform, false);

        Image bg = loginPanel.AddComponent<Image>();

        // Ensure panel is full screen (after adding Image which creates RectTransform)
        RectTransform panelRect = loginPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.sizeDelta = Vector2.zero;
        panelRect.anchoredPosition = Vector2.zero;
        // Create sprite - MUST have a sprite or Image won't render!
        bg.sprite = CreateWhiteSprite();
        bg.color = new Color(1f, 0f, 0f, 1f); // BRIGHT RED FOR TESTING - if Unity renders anything, this will be visible!
        Debug.Log($"[LoginUI] After CreateWhiteSprite - bg.sprite is {(bg.sprite != null ? "NOT NULL" : "NULL")}");
        if (bg.sprite == null)
        {
            Debug.LogError("[LoginUI] CRITICAL: Sprite is null! Image won't render!");
        }
        bg.raycastTarget = true; // Block raycasts to UI behind
        ApplyLoginBackgroundMaterial(bg); // iOS: force a non-stripped shader so panel is visible

        Debug.Log($"[LoginUI] ===== BACKGROUND IMAGE SETUP =====");
        Debug.Log($"[LoginUI] *** TESTING WITH BRIGHT RED BACKGROUND - SHOULD BE VERY VISIBLE! ***");
        Debug.Log($"[LoginUI] bg.sprite: {(bg.sprite != null ? bg.sprite.name : "NULL")}");
        Debug.Log($"[LoginUI] bg.color: {bg.color} (RED FOR TESTING)");
        Debug.Log($"[LoginUI] bg.material: {(bg.material != null ? bg.material.name : "NULL")}");
        Debug.Log($"[LoginUI] bg.material.shader: {(bg.material != null && bg.material.shader != null ? bg.material.shader.name : "NULL")}");
        Debug.Log($"[LoginUI] bg.enabled: {bg.enabled}");
        Debug.Log($"[LoginUI] bg.raycastTarget: {bg.raycastTarget}");
        Debug.Log($"[LoginUI] loginPanel.activeInHierarchy: {loginPanel.activeInHierarchy}");
        Debug.Log($"[LoginUI] ===================================");

        RectTransform bgRect = loginPanel.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        // Logo/Title
        CreateText(loginPanel.transform, "Title", "kiloworld", 80, new Vector2(0, 400), new Vector2(800, 150), Color.white, FontStyle.Bold);
        CreateText(loginPanel.transform, "Subtitle", "step into the right timeline", 36, new Vector2(0, 280), new Vector2(800, 80), new Color(1f, 1f, 1f, 0.8f), FontStyle.Normal);

        // Apple Sign-In Button
        CreateAppleButton(loginPanel.transform);

        // Dev bypass button (visible on all platforms)
        CreateSkipButton(loginPanel.transform);

        // Status text
        GameObject statusGO = CreateText(loginPanel.transform, "Status", "", 32, new Vector2(0, -200), new Vector2(800, 100), Color.yellow, FontStyle.Normal);
        statusText = statusGO.GetComponent<Text>();

        // Terms text
        CreateText(loginPanel.transform, "Terms", "By continuing you agree to connect with other Kiloverse travelers\nand share stories across timelines.", 28, new Vector2(0, -600), new Vector2(800, 120), new Color(1f, 1f, 1f, 0.7f), FontStyle.Normal);

        // ScreenSpaceOverlay doesn't need special layer handling

        // DON'T HIDE IT - we want to see it for testing!
        // loginPanel.SetActive(false);
    }


    /// <summary>
    /// On iOS/IL2CPP the default UI shader can be stripped, so the login panel is invisible (gray screen).
    /// Try known shader names and assign the first one found so the background actually renders.
    /// With URP enabled, we need to try URP shaders first.
    /// </summary>
    static void ApplyLoginBackgroundMaterial(Image bg)
    {
        Debug.Log($"[LoginUI] ========== SHADER DETECTION START ==========");
        Debug.Log($"[LoginUI] Platform: {Application.platform}");
        Debug.Log($"[LoginUI] Current bg.material: {(bg.material != null ? bg.material.name : "NULL")}");
        Debug.Log($"[LoginUI] Current bg.material.shader: {(bg.material != null && bg.material.shader != null ? bg.material.shader.name : "NULL")}");

        // Try shaders that are guaranteed to be in the build (from GraphicsSettings.asset)
        string[] shaderNames = {
            "UI/Default",                   // Built-in UI shader (fileID 7) - BEST for UI
            "Sprites/Default",              // Built-in sprite shader (fileID 10753)
            "Unlit/Texture",                // Built-in unlit (fileID 10783)
            "Unlit/Color",                  // Built-in unlit color
            "UI/Lit/Transparent",           // URP UI Lit (fileID 10770)
            "Universal Render Pipeline/Lit",
            "Universal Render Pipeline/Unlit",
            "Universal Render Pipeline/2D/Sprite-Lit-Default",
            "Universal Render Pipeline/2D/Sprite-Unlit-Default"
        };

        Debug.Log($"[LoginUI] Testing {shaderNames.Length} shaders...");

        foreach (string name in shaderNames)
        {
            Shader s = Shader.Find(name);
            Debug.Log($"[LoginUI] Shader.Find('{name}'): {(s != null ? "FOUND" : "NULL")}");

            if (s != null)
            {
                Debug.Log($"[LoginUI] Creating material from shader '{name}'...");
                Material mat = new Material(s);
                Debug.Log($"[LoginUI] Material created: {mat.name}, shader: {mat.shader.name}");

                // Set color property if available
                bool hasColorProp = mat.HasProperty("_Color");
                bool hasBaseColorProp = mat.HasProperty("_BaseColor");
                Debug.Log($"[LoginUI] Material properties: _Color={hasColorProp}, _BaseColor={hasBaseColorProp}");

                if (hasColorProp)
                {
                    mat.SetColor("_Color", Color.black);
                    Debug.Log($"[LoginUI] Set _Color to black");
                }
                else if (hasBaseColorProp)
                {
                    mat.SetColor("_BaseColor", Color.black);
                    Debug.Log($"[LoginUI] Set _BaseColor to black");
                }

                bg.material = mat;
                Debug.Log($"[LoginUI] ✓✓✓ APPLIED shader '{name}' to background!");
                Debug.Log($"[LoginUI] ========== SHADER DETECTION END (SUCCESS) ==========");
                return;
            }
        }

        // Fallback: Don't set material, use default Image shader
        Debug.LogError("[LoginUI] ⚠⚠⚠ NO SHADER FOUND! Using default Image shader (will likely be grey).");
        Debug.Log($"[LoginUI] Default material after fallback: {(bg.material != null ? bg.material.name : "NULL")}");
        Debug.Log($"[LoginUI] ========== SHADER DETECTION END (FAILED) ==========");
    }

    /// <summary>
    /// Creates a 1x1 white sprite that Image components can use to render solid colors.
    /// Image components require a sprite - without one, they won't render on iOS.
    /// Uses a runtime-created readable texture (whiteTexture is not readable on iOS and can fail to render).
    /// </summary>
    static Sprite CreateWhiteSprite()
    {
        Debug.Log("[LoginUI] CreateWhiteSprite() called");
        // On iOS, Texture2D.whiteTexture is not readable; Sprite.Create from it can produce invisible UI.
        // Create a small readable white texture so the sprite definitely renders.
        const int size = 4;
        Texture2D tex = new Texture2D(size, size);
        Debug.Log($"[LoginUI] Created Texture2D: {tex}");
        Color[] pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply(false); // Keep readable so sprite renders reliably on iOS
        tex.filterMode = FilterMode.Bilinear;
        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        Debug.Log($"[LoginUI] Created Sprite: {sprite} (name: {sprite?.name})");
        return sprite;
    }

    void CreateAppleButton(Transform parent)
    {
        GameObject btnGO = new GameObject("AppleSignInButton");
        btnGO.transform.SetParent(parent, false);

        RectTransform btnRect = btnGO.AddComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.5f, 0.5f);
        btnRect.anchorMax = new Vector2(0.5f, 0.5f);
        btnRect.pivot = new Vector2(0.5f, 0.5f);
        btnRect.anchoredPosition = new Vector2(0, 0);
        btnRect.sizeDelta = new Vector2(600, 120);

        Image btnBg = btnGO.AddComponent<Image>();
        btnBg.sprite = CreateWhiteSprite(); // Image needs sprite to render!
        btnBg.color = Color.white;
        btnBg.raycastTarget = true; // Ensure button receives clicks

        Button btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = btnBg; // Link button to its image
        btn.onClick.AddListener(OnSignInButtonClick);

        // Button text
        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(btnGO.transform, false);

        RectTransform textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text txt = textGO.AddComponent<Text>();
        txt.text = "Sign in with Apple";
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 40;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.black;
        txt.fontStyle = FontStyle.Bold;
    }

    void CreateSkipButton(Transform parent)
    {
        GameObject btnGO = new GameObject("SkipButton");
        btnGO.transform.SetParent(parent, false);

        RectTransform btnRect = btnGO.AddComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.5f, 0.5f);
        btnRect.anchorMax = new Vector2(0.5f, 0.5f);
        btnRect.pivot = new Vector2(0.5f, 0.5f);
        btnRect.anchoredPosition = new Vector2(0, -100);
        btnRect.sizeDelta = new Vector2(400, 80);

        Image btnBg = btnGO.AddComponent<Image>();
        btnBg.sprite = CreateWhiteSprite(); // Image needs sprite to render!
        btnBg.color = new Color(0.3f, 0.3f, 0.3f, 1f);
        btnBg.raycastTarget = true; // Ensure button receives clicks

        Button btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = btnBg; // Link button to its image
        btn.onClick.AddListener(OnSkipButtonClick);

        // Button text
        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(btnGO.transform, false);

        RectTransform textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text txt = textGO.AddComponent<Text>();
        txt.text = "Skip (Dev Mode)";
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 32;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.white;
    }

    GameObject CreateText(Transform parent, string name, string text, int fontSize, Vector2 position, Vector2 size, Color color, FontStyle style)
    {
        GameObject textGO = new GameObject(name);
        textGO.transform.SetParent(parent, false);

        RectTransform textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = position;
        textRect.sizeDelta = size;

        Text txt = textGO.AddComponent<Text>();
        txt.text = text;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = fontSize;
        txt.color = color;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.fontStyle = style;
        txt.horizontalOverflow = HorizontalWrapMode.Wrap;

        return textGO;
    }

    void OnSignInButtonClick()
    {
        Debug.Log("[LoginUI] Sign-in button clicked");
        statusText.text = "Signing in...";
        statusText.color = Color.yellow;

        // Check if Apple Sign-In is available
        if (AppleSignInHandler.Instance.IsAvailable() || Application.isEditor)
        {
            AppleSignInHandler.Instance.SignIn();
        }
        else
        {
            statusText.text = "Sign in with Apple not available on this device";
            statusText.color = Color.red;
        }
    }

    void OnSkipButtonClick()
    {
        Debug.Log("[LoginUI] ============ SKIP BUTTON CLICKED ============");
        Debug.Log("[LoginUI] [EDITOR] Skip button clicked - bypassing auth");

        if (statusText != null)
        {
            statusText.text = "Signing in...";
            statusText.color = Color.yellow;
        }

        // Ensure FirebaseAuthManager is initialized
        var authManager = FirebaseAuthManager.Instance;
        authManager.userId = "EDITOR_TEST_USER";
        authManager.isAuthenticated = true;
        authManager.displayName = "Editor Test User";

        // Manually trigger auth state changed
        OnAuthStateChanged(true);
    }

    void OnAppleSignInSuccess(string idToken, string nonce, string fullName)
    {
        Debug.Log($"[LoginUI] Apple sign-in successful, exchanging for Firebase token...");
        statusText.text = "Completing sign-in...";

        #if UNITY_EDITOR
        // In Editor, skip Firebase auth and just mark as authenticated
        Debug.Log("[LoginUI] [EDITOR] Bypassing Firebase auth - marking as authenticated");
        FirebaseAuthManager.Instance.userId = "EDITOR_TEST_USER";
        FirebaseAuthManager.Instance.isAuthenticated = true;
        FirebaseAuthManager.Instance.displayName = fullName;
        OnAuthStateChanged(true);
        #else
        // Exchange Apple token for Firebase credentials
        FirebaseAuthManager.Instance.SignInWithApple(idToken, nonce, fullName);
        #endif
    }

    void OnAppleSignInFailed(string error)
    {
        Debug.LogError($"[LoginUI] Apple sign-in failed: {error}");
        statusText.text = $"Sign-in failed: {error}";
        statusText.color = Color.red;
    }

    void OnAuthError(string error)
    {
        Debug.LogError($"[LoginUI] Firebase auth failed: {error}");
        if (statusText != null)
        {
            statusText.text = $"Auth failed: {error}";
            statusText.color = Color.red;
        }
    }

    void OnAuthStateChanged(bool isAuthenticated)
    {
        if (isAuthenticated)
        {
            Debug.Log("[LoginUI] Authentication successful, hiding login screen");
            statusText.text = "✓ Signed in successfully!";
            statusText.color = Color.green;

            // Hide login after short delay
            Invoke("HideLogin", 1f);
        }
    }

    void ShowLogin()
    {
        Debug.Log("[LoginUI] ========== ShowLogin() CALLED ==========");
        if (loginPanel != null)
        {
            Debug.Log("[LoginUI] loginPanel exists, activating it now...");
            loginPanel.SetActive(true);
            isShowing = true;
            Debug.Log($"[LoginUI] loginPanel.SetActive(true) called - panel should be VISIBLE now!");
            Debug.Log($"[LoginUI] loginPanel.activeSelf = {loginPanel.activeSelf}");
            Debug.Log($"[LoginUI] loginPanel.activeInHierarchy = {loginPanel.activeInHierarchy}");

            Debug.Log($"[LoginUI] ===== SHOWLOGIN CALLED =====");
            Debug.Log($"[LoginUI] loginPanel.activeSelf: {loginPanel.activeSelf}");
            Debug.Log($"[LoginUI] loginPanel.activeInHierarchy: {loginPanel.activeInHierarchy}");
            Debug.Log($"[LoginUI] loginCanvas.activeSelf: {loginCanvas.activeSelf}");
            Debug.Log($"[LoginUI] loginCanvas.activeInHierarchy: {loginCanvas.activeInHierarchy}");

            Canvas canvas = loginCanvas.GetComponent<Canvas>();
            Debug.Log($"[LoginUI] Canvas.enabled: {canvas.enabled}");
            Debug.Log($"[LoginUI] Canvas.renderMode: {canvas.renderMode}");
            Debug.Log($"[LoginUI] Canvas.sortingOrder: {canvas.sortingOrder}");

            Image bg = loginPanel.GetComponent<Image>();
            Debug.Log($"[LoginUI] Background Image.enabled: {bg.enabled}");
            Debug.Log($"[LoginUI] Background Image.color: {bg.color}");
            Debug.Log($"[LoginUI] Background Image.sprite: {(bg.sprite != null ? "EXISTS" : "NULL")}");
            Debug.Log($"[LoginUI] Background Image.material.shader: {bg.material.shader.name}");

            // Force canvas to update immediately
            Canvas.ForceUpdateCanvases();
            Debug.Log($"[LoginUI] Forced canvas update");

            // Log camera settings that might affect rendering
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                Debug.Log($"[LoginUI] Main Camera: {mainCam.name}");
                Debug.Log($"[LoginUI] Camera clearFlags: {mainCam.clearFlags}");
                Debug.Log($"[LoginUI] Camera backgroundColor: {mainCam.backgroundColor}");
                Debug.Log($"[LoginUI] Camera cullingMask: {mainCam.cullingMask}");
                Debug.Log($"[LoginUI] Camera depth: {mainCam.depth}");
            }

            Debug.Log($"[LoginUI] ==================================");
        }
        else
        {
            Debug.LogError("[LoginUI] Cannot show login - loginPanel is null!");
        }
    }

    void HideLogin()
    {
        isShowing = false;

        // Destroy the entire canvas to remove all blocking UI
        if (loginCanvas != null)
        {
            Debug.Log("[LoginUI] Destroying login canvas");
            Destroy(loginCanvas);
            loginCanvas = null;
        }

        if (loginPanel != null)
        {
            loginPanel = null;
        }

        statusText = null;
    }

    IEnumerator EnsureOnTop()
    {
        // Wait for all other Start() methods to complete
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();

        // Find the canvas and force it on top
        Canvas loginCanvas = GameObject.Find("LoginCanvas")?.GetComponent<Canvas>();
        if (loginCanvas != null)
        {
            Canvas[] allCanvases = FindObjectsOfType<Canvas>();
            int maxOrder = 0;
            foreach (var c in allCanvases)
            {
                if (c != loginCanvas && c.sortingOrder > maxOrder)
                {
                    maxOrder = c.sortingOrder;
                }
            }
            loginCanvas.sortingOrder = maxOrder + 10; // Go well above
            Debug.Log($"[LoginUI] Updated sorting order to {loginCanvas.sortingOrder} (max was {maxOrder})");
        }
    }
}
