using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Login screen with Apple Sign-In
/// Shows on app launch if user is not authenticated
/// </summary>
public class LoginUI : MonoBehaviour
{
    private GameObject loginPanel;
    private GameObject loginCanvas;
    private TextMeshProUGUI _statusTMP;
    private bool isShowing = false;

    void Start()
    {
#if UNITY_EDITOR
        // Auto-skip login in Editor so we don't have to click every time
        Debug.Log("[LoginUI] Editor detected — auto-skipping login");
        var auth = FirebaseAuthManager.Instance;
        if (auth != null && !auth.isAuthenticated)
        {
            auth.userId = "EDITOR_TEST_USER";
            auth.isAuthenticated = true;
            auth.displayName = "Editor Test User";
        }
        return;
#endif

        if (FirebaseAuthManager.Instance != null && FirebaseAuthManager.Instance.isAuthenticated)
        {
            Debug.Log("[LoginUI] Already authenticated, skipping login");
            return;
        }

        Debug.Log("[LoginUI] Not authenticated, showing login screen");
        CreateLoginUI();
        ShowLogin();
        StartCoroutine(EnsureOnTop());

        FirebaseAuthManager.Instance.OnAuthStateChanged += OnAuthStateChanged;
        FirebaseAuthManager.Instance.OnAuthError += OnAuthError;

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
        }

        // Create full-screen canvas
        loginCanvas = new GameObject("LoginCanvas");
        Canvas canvas = loginCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;
        canvas.overrideSorting = true;
        canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.None;
        DontDestroyOnLoad(loginCanvas);

        CanvasScaler scaler = loginCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);

        GraphicRaycaster raycaster = loginCanvas.AddComponent<GraphicRaycaster>();
        raycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;

        // Go above all existing canvases
        Canvas[] allCanvases = FindObjectsOfType<Canvas>();
        int maxOrder = 0;
        foreach (var c in allCanvases)
        {
            if (c != canvas && c.sortingOrder > maxOrder) maxOrder = c.sortingOrder;
        }
        canvas.sortingOrder = maxOrder + 1;

        // Dark background panel
        loginPanel = new GameObject("LoginPanel");
        loginPanel.layer = 5; // UI layer
        loginPanel.transform.SetParent(loginCanvas.transform, false);

        Image bg = loginPanel.AddComponent<Image>();

        RectTransform panelRect = loginPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.sizeDelta = Vector2.zero;
        panelRect.anchoredPosition = Vector2.zero;

        bg.sprite = CreateWhiteSprite();
        bg.color = new Color(0.08f, 0.08f, 0.12f, 1f); // Dark background
        bg.raycastTarget = true;
        ApplyLoginBackgroundMaterial(bg);

        RectTransform bgRect = loginPanel.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        // Load IBM Plex Mono font
        TMP_FontAsset monoFont = Resources.Load<TMP_FontAsset>("Fonts/IBMPlexMono-Regular SDF");
        if (monoFont == null)
            monoFont = TMP_Settings.defaultFontAsset;

        // Giant K1L0 title
        CreateTMPText(loginPanel.transform, "Title", "K1L0", monoFont, 160,
            new Vector2(0, 350), new Vector2(900, 250), Color.white, FontStyles.Bold);

        CreateTMPText(loginPanel.transform, "Subtitle", "STEP INTO THE RIGHT TIMELINE", monoFont, 28,
            new Vector2(0, 200), new Vector2(800, 60), new Color(0.75f, 0.85f, 1f, 0.7f), FontStyles.UpperCase);

        // Apple Sign-In Button
        CreateAppleButton(loginPanel.transform, monoFont);

        // Dev bypass button
        CreateSkipButton(loginPanel.transform, monoFont);

        // Status text
        var statusGO = CreateTMPText(loginPanel.transform, "Status", "", monoFont, 24,
            new Vector2(0, -200), new Vector2(800, 100), Color.yellow, FontStyles.Normal);
        _statusTMP = statusGO.GetComponent<TextMeshProUGUI>();

        // Terms text
        CreateTMPText(loginPanel.transform, "Terms",
            "BY CONTINUING YOU AGREE TO CONNECT WITH\nOTHER KILOVERSE TRAVELERS AND SHARE\nSTORIES ACROSS TIMELINES.",
            monoFont, 18, new Vector2(0, -550), new Vector2(800, 120),
            new Color(1f, 1f, 1f, 0.4f), FontStyles.Normal);

        loginPanel.SetActive(false);
    }

    static void ApplyLoginBackgroundMaterial(Image bg)
    {
        string[] shaderNames = {
            "UI/Default",
            "Sprites/Default",
            "Unlit/Texture",
            "Unlit/Color",
            "UI/Lit/Transparent",
            "Universal Render Pipeline/Lit",
            "Universal Render Pipeline/Unlit",
            "Universal Render Pipeline/2D/Sprite-Lit-Default",
            "Universal Render Pipeline/2D/Sprite-Unlit-Default"
        };

        foreach (string name in shaderNames)
        {
            Shader s = Shader.Find(name);
            if (s != null)
            {
                Material mat = new Material(s);
                if (mat.HasProperty("_Color"))
                    mat.SetColor("_Color", Color.black);
                else if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", Color.black);

                bg.material = mat;
                return;
            }
        }
    }

    static Sprite CreateWhiteSprite()
    {
        const int size = 4;
        Texture2D tex = new Texture2D(size, size);
        Color[] pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply(false);
        tex.filterMode = FilterMode.Bilinear;
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    void CreateAppleButton(Transform parent, TMP_FontAsset font)
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
        btnBg.sprite = CreateWhiteSprite();
        btnBg.color = Color.white;
        btnBg.raycastTarget = true;

        Button btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = btnBg;
        btn.onClick.AddListener(OnSignInButtonClick);

        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(btnGO.transform, false);

        RectTransform textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI txt = textGO.AddComponent<TextMeshProUGUI>();
        txt.text = "\uF8FF  SIGN IN WITH APPLE";
        txt.font = font;
        txt.fontSize = 36;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = Color.black;
        txt.fontStyle = FontStyles.Bold;
    }

    void CreateSkipButton(Transform parent, TMP_FontAsset font)
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
        btnBg.sprite = CreateWhiteSprite();
        btnBg.color = new Color(0.3f, 0.3f, 0.3f, 1f);
        btnBg.raycastTarget = true;

        Button btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = btnBg;
        btn.onClick.AddListener(OnSkipButtonClick);

        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(btnGO.transform, false);

        RectTransform textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI txt = textGO.AddComponent<TextMeshProUGUI>();
        txt.text = "SKIP (DEV MODE)";
        txt.font = font;
        txt.fontSize = 28;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = Color.white;
    }

    GameObject CreateTMPText(Transform parent, string name, string text, TMP_FontAsset font, float fontSize, Vector2 position, Vector2 size, Color color, FontStyles style)
    {
        GameObject textGO = new GameObject(name);
        textGO.transform.SetParent(parent, false);

        RectTransform textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = position;
        textRect.sizeDelta = size;

        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.font = font;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = style;
        tmp.enableWordWrapping = true;

        return textGO;
    }

    void OnSignInButtonClick()
    {
        Debug.Log("[LoginUI] Sign-in button clicked");
        SetStatus("SIGNING IN...", Color.yellow);

        if (AppleSignInHandler.Instance.IsAvailable() || Application.isEditor)
        {
            AppleSignInHandler.Instance.SignIn();
        }
        else
        {
            SetStatus("SIGN IN WITH APPLE NOT AVAILABLE ON THIS DEVICE", Color.red);
        }
    }

    void OnSkipButtonClick()
    {
        Debug.Log("[LoginUI] Skip button clicked - bypassing auth");
        SetStatus("SIGNING IN...", Color.yellow);

        var authManager = FirebaseAuthManager.Instance;
        authManager.userId = "EDITOR_TEST_USER";
        authManager.isAuthenticated = true;
        authManager.displayName = "Editor Test User";

        OnAuthStateChanged(true);
    }

    void OnAppleSignInSuccess(string idToken, string nonce, string fullName)
    {
        Debug.Log("[LoginUI] Apple sign-in successful, exchanging for Firebase token...");
        SetStatus("COMPLETING SIGN-IN...", Color.yellow);

        #if UNITY_EDITOR
        FirebaseAuthManager.Instance.userId = "EDITOR_TEST_USER";
        FirebaseAuthManager.Instance.isAuthenticated = true;
        FirebaseAuthManager.Instance.displayName = fullName;
        OnAuthStateChanged(true);
        #else
        FirebaseAuthManager.Instance.SignInWithApple(idToken, nonce, fullName);
        #endif
    }

    void OnAppleSignInFailed(string error)
    {
        Debug.LogError($"[LoginUI] Apple sign-in failed: {error}");
        SetStatus($"SIGN-IN FAILED: {error}", Color.red);
    }

    void OnAuthError(string error)
    {
        Debug.LogError($"[LoginUI] Firebase auth failed: {error}");
        SetStatus($"AUTH FAILED: {error}", Color.red);
    }

    void OnAuthStateChanged(bool isAuthenticated)
    {
        if (isAuthenticated)
        {
            Debug.Log("[LoginUI] Authentication successful, hiding login");
            SetStatus("SIGNED IN", Color.green);
            Invoke("HideLogin", 1f);
        }
    }

    void SetStatus(string text, Color color)
    {
        if (_statusTMP != null)
        {
            _statusTMP.text = text;
            _statusTMP.color = color;
        }
    }

    public void ShowLogin()
    {
        if (loginPanel == null)
        {
            CreateLoginUI();
            if (FirebaseAuthManager.Instance != null)
            {
                FirebaseAuthManager.Instance.OnAuthStateChanged -= OnAuthStateChanged;
                FirebaseAuthManager.Instance.OnAuthStateChanged += OnAuthStateChanged;
                FirebaseAuthManager.Instance.OnAuthError -= OnAuthError;
                FirebaseAuthManager.Instance.OnAuthError += OnAuthError;
            }
            if (AppleSignInHandler.Instance != null)
            {
                AppleSignInHandler.Instance.OnAppleSignInSuccess -= OnAppleSignInSuccess;
                AppleSignInHandler.Instance.OnAppleSignInSuccess += OnAppleSignInSuccess;
                AppleSignInHandler.Instance.OnAppleSignInFailed -= OnAppleSignInFailed;
                AppleSignInHandler.Instance.OnAppleSignInFailed += OnAppleSignInFailed;
            }
        }
        if (loginPanel != null)
        {
            loginPanel.SetActive(true);
            isShowing = true;
            Canvas.ForceUpdateCanvases();
        }
    }

    void HideLogin()
    {
        isShowing = false;

        if (loginCanvas != null)
        {
            Destroy(loginCanvas);
            loginCanvas = null;
        }

        loginPanel = null;
        _statusTMP = null;
    }

    IEnumerator EnsureOnTop()
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();

        Canvas canvas = loginCanvas != null ? loginCanvas.GetComponent<Canvas>() : null;
        if (canvas != null)
        {
            Canvas[] allCanvases = FindObjectsOfType<Canvas>();
            int maxOrder = 0;
            foreach (var c in allCanvases)
            {
                if (c != canvas && c.sortingOrder > maxOrder)
                    maxOrder = c.sortingOrder;
            }
            canvas.sortingOrder = maxOrder + 10;
        }
    }
}
