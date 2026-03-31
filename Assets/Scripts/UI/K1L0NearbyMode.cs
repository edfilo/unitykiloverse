using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class K1L0NearbyMode : MonoBehaviour
{
    private static readonly Color TerminalGreen = new Color(0.56f, 1f, 0.62f, 1f);
    private static readonly Color TerminalDim = new Color(0.56f, 1f, 0.62f, 1f);
    private static readonly Color LineBgColor = new Color(0.01f, 0.02f, 0.01f, 0.82f);
    private const int MaxLocations = 20;
    private const float RefreshInterval = 0.25f;
    private const float LineBgPadH = 4f;
    private const float LineBgPadV = 1f;

    private TMP_FontAsset font;
    private TextMeshProUGUI bodyText;
    private RectTransform lineBgContainer;
    private readonly List<Image> lineBgPool = new List<Image>();
    private readonly List<TransmitterScanner.TransmitterData> currentVisible = new List<TransmitterScanner.TransmitterData>(MaxLocations);
    private readonly Dictionary<string, K1L0GlassChrome> filterChromes = new Dictionary<string, K1L0GlassChrome>();
    private readonly Dictionary<string, Button> filterButtons = new Dictionary<string, Button>();
    private float lastRefresh;
    private string currentFilter = "all";
    private float simulatedHydration;
    private KiloFirstPersonController cachedPlayer;

    public void Initialize(RectTransform parent, TMP_FontAsset monoFont)
    {
        font = monoFont;

        RectTransform rt = gameObject.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(8f, 8f);
        rt.offsetMax = new Vector2(-8f, -8f);

        simulatedHydration = Random.Range(0.42f, 0.86f);
        CreateCommandBar(rt);
        bodyText = CreateBodyText(rt);
        Refresh();
    }

    private TextMeshProUGUI CreateBodyText(RectTransform parent)
    {
        // Line background container — inserted first so it renders behind text
        GameObject bgGO = new GameObject("LineBgs", typeof(RectTransform));
        bgGO.transform.SetParent(parent, false);
        lineBgContainer = bgGO.GetComponent<RectTransform>();
        lineBgContainer.anchorMin = new Vector2(0f, 0f);
        lineBgContainer.anchorMax = new Vector2(1f, 1f);
        lineBgContainer.offsetMin = new Vector2(0f, 0f);
        lineBgContainer.offsetMax = new Vector2(-6f, -40f);

        GameObject go = new GameObject("NearbyBody", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.offsetMin = new Vector2(0f, 0f);
        rt.offsetMax = new Vector2(-6f, -40f);

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.fontSize = 15f;
        tmp.color = TerminalDim;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.richText = true;
        tmp.text = string.Empty;
        return tmp;
    }

    private void CreateCommandBar(RectTransform parent)
    {
        GameObject barGO = new GameObject("Commands", typeof(RectTransform));
        barGO.transform.SetParent(parent, false);
        RectTransform barRT = barGO.GetComponent<RectTransform>();
        barRT.anchorMin = new Vector2(0f, 1f);
        barRT.anchorMax = new Vector2(1f, 1f);
        barRT.pivot = new Vector2(0.5f, 1f);
        barRT.anchoredPosition = Vector2.zero;
        barRT.sizeDelta = new Vector2(0f, 34f);

        HorizontalLayoutGroup layout = barGO.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        CreateFilterButton(barGO.transform, "all", "[ALL]");
        CreateFilterButton(barGO.transform, "coffee", "[COFFEE]");
        CreateFilterButton(barGO.transform, "bar", "[BAR]");
        CreateFilterButton(barGO.transform, "food", "[FOOD]");
        CreateFilterButton(barGO.transform, "convenience", "[SUPPLY]");
    }

    private void CreateFilterButton(Transform parent, string category, string label)
    {
        GameObject go = new GameObject(category, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(Mathf.Max(70f, label.Length * 8f + 18f), 30f);

        LayoutElement layout = go.AddComponent<LayoutElement>();
        layout.minHeight = 30f;
        layout.preferredHeight = 30f;
        layout.preferredWidth = rt.sizeDelta.x;

        K1L0GlassChrome chrome = K1L0GlassFactory.AttachChrome(go.transform, $"Cmd_{category}", K1L0GlassFactory.ControlStyle);
        chrome.blurFill.material = null;
        chrome.blurFill.color = new Color(0.02f, 0.07f, 0.02f, 0.95f);
        chrome.overlay.color = new Color(0.01f, 0.04f, 0.01f, 0.90f);
        chrome.border.color = new Color(0.28f, 0.96f, 0.38f, 0.24f);
        chrome.accent.color = new Color(0.22f, 1f, 0.32f, 0.08f);
        chrome.sheen.color = new Color(0f, 0f, 0f, 0f);
        chrome.blurFill.raycastTarget = true;
        filterChromes[category] = chrome;

        Button button = go.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = chrome.blurFill;
        button.onClick.AddListener(() => SetFilter(category));
        filterButtons[category] = button;

        GameObject labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(go.transform, false);
        RectTransform labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = Vector2.zero;
        labelRT.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.fontSize = 13f;
        tmp.color = TerminalGreen;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.text = label;
        tmp.raycastTarget = false;
    }

    private void SetFilter(string category)
    {
        currentFilter = category;
        if (TransmitterScanner.Instance != null)
        {
            TransmitterScanner.Instance.disableFiltering = false;
            TransmitterScanner.Instance.SetCategoryFilter(category);
        }

        foreach (var entry in filterChromes)
        {
            bool active = entry.Key == category;
            entry.Value.border.color = active
                ? new Color(0.44f, 1f, 0.54f, 0.44f)
                : new Color(0.28f, 0.96f, 0.38f, 0.20f);
            entry.Value.SetAccent(active ? 1f : 0f);
        }

        Refresh();
    }

    private void OnEnable()
    {
        Refresh();
    }

    private void Update()
    {
        if (Time.time - lastRefresh > RefreshInterval)
        {
            Refresh();
        }
    }

    public void Refresh()
    {
        lastRefresh = Time.time;
        simulatedHydration = Mathf.Clamp01(simulatedHydration + Random.Range(-0.02f, 0.015f));
        currentVisible.Clear();

        List<TransmitterScanner.TransmitterData> nearest = null;
        if (TransmitterScanner.Instance != null)
        {
            nearest = TransmitterScanner.Instance.GetNearestUnfiltered(MaxLocations);
        }

        if (nearest != null && currentFilter != "all")
        {
            nearest = nearest.FindAll(t => t.MainCategoryGroup == currentFilter);
        }

        if (nearest != null)
        {
            // Deduplicate by name (API can return same place from multiple search passes)
            HashSet<string> seen = new HashSet<string>();
            foreach (var t in nearest)
            {
                string key = t.Name.ToLowerInvariant().Trim();
                if (seen.Contains(key)) continue;
                seen.Add(key);
                currentVisible.Add(t);
                if (currentVisible.Count >= MaxLocations) break;
            }
        }

        float hydration = simulatedHydration;

        StringBuilder sb = new StringBuilder(512);
        sb.AppendLine($"<color=#8EFF9F>> NEARBY</color>");
        sb.AppendLine($"<color=#6EFF84>> hydration {Mathf.RoundToInt(hydration * 100f)}%</color>");
        sb.AppendLine();

        if (currentVisible.Count == 0)
        {
            sb.AppendLine("<color=#A8FFB3>> scanning nearby carriers...</color>");
            sb.AppendLine();
            sb.AppendLine("<color=#7DFF8D>> no returns in current filter</color>");
        }
        else
        {
            if (cachedPlayer == null) cachedPlayer = Object.FindFirstObjectByType<KiloFirstPersonController>();
            Vector3 forward = cachedPlayer != null ? cachedPlayer.transform.forward : Vector3.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }
            forward.Normalize();

            for (int i = 0; i < currentVisible.Count; i++)
            {
                var entry = currentVisible[i];
                float miles = entry.Distance / 1609.34f;
                string distance;
                if (entry.Distance < 200f)
                {
                    distance = $"{Mathf.RoundToInt(entry.Distance)}m";
                }
                else
                {
                    distance = $"{miles:F1}mi";
                }

                string arrow = "--";
                if (cachedPlayer != null && entry.HasWorldPosition)
                {
                    Vector3 toTarget = entry.WorldPosition - cachedPlayer.transform.position;
                    toTarget.y = 0f;
                    if (toTarget.sqrMagnitude > 0.0001f)
                    {
                        float angle = Vector3.SignedAngle(forward, toTarget.normalized, Vector3.up);
                        arrow = AngleToArrow(angle);
                    }
                }

                string icon = CategoryIcon(entry.MainCategoryGroup);
                sb.AppendLine($"> {icon} {arrow} {distance,-6} <color=#BCFFC5>{entry.Name.ToUpperInvariant()}</color>");
            }
        }

        bodyText.text = sb.ToString();
        UpdateLineBackgrounds();
    }

    private void UpdateLineBackgrounds()
    {
        bodyText.ForceMeshUpdate();
        TMP_TextInfo info = bodyText.textInfo;
        int lineCount = info.lineCount;
        RectTransform textRT = bodyText.GetComponent<RectTransform>();
        Vector2 textSize = textRT.rect.size;

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
                GameObject bgGO = new GameObject($"LineBg{visibleIdx}");
                bgGO.transform.SetParent(lineBgContainer, false);
                bg = bgGO.AddComponent<Image>();
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

    private string CategoryIcon(string category)
    {
        switch (category)
        {
            case "coffee": return "<color=#A8FFB3>\u25CF</color>";
            case "bar": return "<color=#FFD68A>\u25CF</color>";
            case "food": return "<color=#FF9E8A>\u25CF</color>";
            case "convenience": return "<color=#8AD4FF>\u25CF</color>";
            default: return "<color=#8EFF9F>\u25C6</color>";
        }
    }

    private string AngleToArrow(float angle)
    {
        // 8 compass directions using Unicode arrows
        if (angle >= -22.5f && angle < 22.5f) return "\u2191";       // ↑ ahead
        if (angle >= 22.5f && angle < 67.5f) return "\u2197";        // ↗
        if (angle >= 67.5f && angle < 112.5f) return "\u2192";       // →
        if (angle >= 112.5f && angle < 157.5f) return "\u2198";      // ↘
        if (angle >= 157.5f || angle < -157.5f) return "\u2193";     // ↓ behind
        if (angle >= -157.5f && angle < -112.5f) return "\u2199";    // ↙
        if (angle >= -112.5f && angle < -67.5f) return "\u2190";     // ←
        return "\u2196";                                               // ↖
    }
}
