using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class K1L0ProfileMode : MonoBehaviour
{
    private static readonly Color TerminalGreen = new Color(0.56f, 1f, 0.62f, 1f);
    private static readonly Color TerminalDim = new Color(0.56f, 1f, 0.62f, 1f);
    private static readonly Color LineBgColor = new Color(0.01f, 0.02f, 0.01f, 0.82f);
    private const float LineBgPadH = 4f;
    private const float LineBgPadV = 1f;

    private TMP_FontAsset monoFont;
    private TextMeshProUGUI bodyText;
    private TextMeshProUGUI authBtnLabel;
    private TextMeshProUGUI prodApiLabel;
    private K1L0GlassChrome authChrome;
    private K1L0GlassChrome prodChrome;
    private RectTransform lineBgContainer;
    private readonly List<Image> lineBgPool = new List<Image>();

    public void Initialize(RectTransform parent, TMP_FontAsset font)
    {
        monoFont = font;

        RectTransform rt = gameObject.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(8f, 8f);
        rt.offsetMax = new Vector2(-8f, -8f);

        bodyText = CreateBodyText(rt);

        var editBtn = CreateCommandButton(rt, "EditBtn", new Vector2(0f, 0f), new Vector2(0.48f, 0f), new Vector2(0f, 0f), Vector2.zero, new Vector2(0f, 32f), "EDIT PROFILE");
        editBtn.btn.onClick.AddListener(OnEditClick);

        var authBtn = CreateCommandButton(rt, "AuthBtn", new Vector2(0.52f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 32f), "LOGOUT");
        authBtn.btn.onClick.AddListener(OnAuthClick);
        authBtnLabel = authBtn.label;
        authChrome = authBtn.chrome;

        var prodBtn = CreateCommandButton(rt, "ProdApiBtn", new Vector2(0f, 0f), new Vector2(0.48f, 0f), new Vector2(0f, 0f), new Vector2(0f, 38f), new Vector2(0f, 32f), "PRODUCTION API OFF");
        prodBtn.btn.onClick.AddListener(OnProdApiToggle);
        prodApiLabel = prodBtn.label;
        prodChrome = prodBtn.chrome;

        var screenshotBtn = CreateCommandButton(rt, "ScreenshotBtn", new Vector2(0.52f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0f, 38f), new Vector2(0f, 32f), "SEND SCREENSHOT");
        screenshotBtn.btn.onClick.AddListener(OnScreenshotClick);

        LoadProfile();
        UpdateAuthButton();
        UpdateProdApiToggle();
    }

    private TextMeshProUGUI CreateBodyText(RectTransform parent)
    {
        // Line background container — renders behind text
        GameObject bgGO = new GameObject("LineBgs", typeof(RectTransform));
        bgGO.transform.SetParent(parent, false);
        lineBgContainer = bgGO.GetComponent<RectTransform>();
        lineBgContainer.anchorMin = new Vector2(0f, 0f);
        lineBgContainer.anchorMax = new Vector2(1f, 1f);
        lineBgContainer.offsetMin = new Vector2(0f, 82f);
        lineBgContainer.offsetMax = new Vector2(-6f, 0f);

        GameObject go = new GameObject("ProfileBody", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.offsetMin = new Vector2(0f, 82f);
        rt.offsetMax = new Vector2(-6f, 0f);

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = monoFont;
        tmp.fontSize = 15f;
        tmp.color = TerminalDim;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.richText = true;
        return tmp;
    }

    private (Button btn, K1L0GlassChrome chrome, TextMeshProUGUI label) CreateCommandButton(
        RectTransform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 position,
        Vector2 sizeDelta,
        string label)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = position;
        rt.sizeDelta = sizeDelta;

        K1L0GlassChrome chrome = K1L0GlassFactory.AttachChrome(go.transform, name, K1L0GlassFactory.ControlStyle);
        chrome.blurFill.material = null;
        chrome.blurFill.color = new Color(0.02f, 0.07f, 0.02f, 0.95f);
        chrome.overlay.color = new Color(0.01f, 0.04f, 0.01f, 0.90f);
        chrome.border.color = new Color(0.30f, 0.96f, 0.38f, 0.26f);
        chrome.accent.color = new Color(0.22f, 1f, 0.32f, 0.08f);
        chrome.sheen.color = new Color(0f, 0f, 0f, 0f);
        chrome.blurFill.raycastTarget = true;

        Button button = go.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = chrome.blurFill;

        GameObject labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(go.transform, false);
        RectTransform labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = Vector2.zero;
        labelRT.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.font = monoFont;
        tmp.fontSize = 12.5f;
        tmp.color = TerminalGreen;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.text = label;
        tmp.raycastTarget = false;

        return (button, chrome, tmp);
    }

    private void OnEnable()
    {
        LoadProfile();
        UpdateAuthButton();
        UpdateProdApiToggle();
    }

    private void LoadProfile()
    {
        string callSign = "---";
        string channel = "---";
        string signal = FirebaseAuthManager.Instance != null && FirebaseAuthManager.Instance.isAuthenticated ? "AUTHENTICATED" : "ANON";
        string deviceId = DeviceIDManager.Instance != null ? DeviceIDManager.Instance.GetCurrentUserId() : "offline";

        RenderProfileText(callSign, signal, channel, deviceId);

        if (FirebaseRestClient.Instance != null && DeviceIDManager.Instance != null)
        {
            string userId = DeviceIDManager.Instance.GetCurrentUserId();
            FirebaseRestClient.Instance.GetFirestoreData("users", userId,
                (response) =>
                {
                    try
                    {
                        callSign = ExtractStringField(response, "callSign") ?? "---";
                        channel = ExtractStringField(response, "instagram");
                        channel = string.IsNullOrEmpty(channel) ? "---" : "@" + channel;
                        RenderProfileText(callSign, signal, channel, deviceId);
                    }
                    catch
                    {
                        RenderProfileText(callSign, signal, channel, deviceId);
                    }
                },
                _ => { });
        }
    }

    private string ExtractStringField(string json, string fieldName)
    {
        // Firestore REST returns: {"fields":{"fieldName":{"stringValue":"val"}}}
        string key = $"\"{fieldName}\"";
        int idx = json.IndexOf(key);
        if (idx < 0) return null;
        string sv = "\"stringValue\":\"";
        int svIdx = json.IndexOf(sv, idx);
        if (svIdx < 0 || svIdx - idx > 60) return null;
        int start = svIdx + sv.Length;
        int end = json.IndexOf('"', start);
        if (end < 0) return null;
        return json.Substring(start, end - start);
    }

    private void RenderProfileText(string callSign, string signal, string channel, string deviceId)
    {
        StringBuilder sb = new StringBuilder(384);
        sb.AppendLine("<color=#8EFF9F>> PROFILE / TERMINAL</color>");
        sb.AppendLine("<color=#6EFF84>> local operator record loaded</color>");
        sb.AppendLine();
        sb.AppendLine($"> CALLSIGN  <color=#BCFFC5>{callSign}</color>");
        sb.AppendLine($"> SIGNAL    <color=#BCFFC5>{signal}</color>");
        sb.AppendLine($"> CHANNEL   <color=#BCFFC5>{channel}</color>");
        sb.AppendLine($"> DEVICE    <color=#BCFFC5>{TrimDevice(deviceId)}</color>");
        sb.AppendLine();
        sb.Append("<color=#73FF88>> command links unlock below</color>");
        bodyText.text = sb.ToString();
        UpdateLineBackgrounds();
    }

    private string TrimDevice(string deviceId)
    {
        if (string.IsNullOrEmpty(deviceId))
        {
            return "---";
        }

        return deviceId.Length <= 18 ? deviceId : deviceId.Substring(0, 18) + "...";
    }

    private void UpdateLineBackgrounds()
    {
        bodyText.ForceMeshUpdate();
        TMP_TextInfo info = bodyText.textInfo;
        int lineCount = info.lineCount;

        int visibleIdx = 0;
        for (int i = 0; i < lineCount; i++)
        {
            TMP_LineInfo line = info.lineInfo[i];
            if (line.characterCount == 0) continue;

            Image bg;
            if (visibleIdx < lineBgPool.Count)
            {
                bg = lineBgPool[visibleIdx];
                bg.gameObject.SetActive(true);
            }
            else
            {
                GameObject bgObj = new GameObject($"LineBg{visibleIdx}");
                bgObj.transform.SetParent(lineBgContainer, false);
                bg = bgObj.AddComponent<Image>();
                bg.raycastTarget = false;
                lineBgPool.Add(bg);
            }

            bg.color = LineBgColor;
            RectTransform rect = bg.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            float lineTop = line.lineExtents.max.y;
            float lineBottom = line.lineExtents.min.y;
            float lineHeight = lineTop - lineBottom + LineBgPadV * 2f;
            float lineWidth = line.lineExtents.max.x - line.lineExtents.min.x + LineBgPadH * 2f;
            float centerX = (line.lineExtents.min.x + line.lineExtents.max.x) * 0.5f;
            float centerY = (lineTop + lineBottom) * 0.5f;

            rect.anchoredPosition = new Vector2(centerX, centerY);
            rect.sizeDelta = new Vector2(lineWidth, lineHeight);
            visibleIdx++;
        }

        for (int i = visibleIdx; i < lineBgPool.Count; i++)
        {
            lineBgPool[i].gameObject.SetActive(false);
        }
    }

    private void OnEditClick()
    {
        ProfileEditorModal modal = Object.FindFirstObjectByType<ProfileEditorModal>();
        if (modal != null)
        {
            modal.OpenModal();
        }
    }

    private void OnAuthClick()
    {
        FirebaseAuthManager auth = FirebaseAuthManager.Instance;
        if (auth == null)
        {
            return;
        }

        if (auth.isAuthenticated)
        {
            auth.SignOut();
            LoadProfile();
            UpdateAuthButton();
        }
        else
        {
            LoginUI loginUI = Object.FindFirstObjectByType<LoginUI>();
            if (loginUI != null)
            {
                loginUI.ShowLogin();
            }
        }
    }

    private void UpdateAuthButton()
    {
        if (authBtnLabel == null)
        {
            return;
        }

        bool loggedIn = FirebaseAuthManager.Instance != null && FirebaseAuthManager.Instance.isAuthenticated;
        authBtnLabel.text = loggedIn ? "LOGOUT" : "LOGIN";
        if (authChrome != null)
        {
            authChrome.baseAccent = loggedIn ? new Color(0.70f, 0.20f, 0.20f, 0.24f) : new Color(0.22f, 1f, 0.32f, 0.14f);
            authChrome.SetAccent(1f);
        }
    }

    private void OnScreenshotClick()
    {
        K1L0Screenshot ss = K1L0Screenshot.Instance;
        if (ss != null)
        {
            ss.Capture();
        }
    }

    private void OnProdApiToggle()
    {
        bool current = APIManager.IsProductionOverride();
        APIManager.SetProductionOverride(!current);
        UpdateProdApiToggle();
    }

    private void UpdateProdApiToggle()
    {
        if (prodApiLabel == null)
        {
            return;
        }

        bool on = APIManager.IsProductionOverride();
        prodApiLabel.text = on ? "PRODUCTION API ON" : "PRODUCTION API OFF";
        if (prodChrome != null)
        {
            prodChrome.baseAccent = on ? new Color(0.22f, 1f, 0.32f, 0.18f) : new Color(0.18f, 0.72f, 1f, 0.16f);
            prodChrome.SetAccent(1f);
        }
    }

    [System.Serializable]
    private class ProfileData
    {
        public string callSign;
        public string instagram;
    }
}
