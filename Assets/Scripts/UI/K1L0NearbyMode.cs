using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class K1L0NearbyMode : MonoBehaviour
{
    private static readonly Color TerminalGreen = new Color(0.56f, 1f, 0.62f, 1f);
    private static readonly Color TerminalDim = new Color(0.56f, 1f, 0.62f, 1f);
    private static readonly Color ArrowGreen = new Color(0.74f, 1f, 0.77f, 1f);
    private static readonly Color LineBgColor = new Color(0.01f, 0.02f, 0.01f, 0.82f);
    private const int MaxLocations = 20;
    private const float RefreshInterval = 0.25f;
    private const float LineBgPadH = 4f;
    private const float LineBgPadV = 1f;

    private TMP_FontAsset font;
    private TextMeshProUGUI teaserText;
    private TextMeshProUGUI bodyText;
    private RectTransform teaserRowsContainer;
    private RectTransform teaserArrowContainer;
    private RectTransform lineBgContainer;
    private readonly List<Image> lineBgPool = new List<Image>();
    private readonly List<Image> arrowPool = new List<Image>();
    private readonly List<Image> teaserArrowPool = new List<Image>();
    private readonly List<float> teaserArrowAngles = new List<float>();
    private readonly List<float> lineArrowAngles = new List<float>();
    private readonly List<TransmitterScanner.TransmitterData> currentVisible = new List<TransmitterScanner.TransmitterData>(MaxLocations);
    private readonly Dictionary<string, K1L0GlassChrome> filterChromes = new Dictionary<string, K1L0GlassChrome>();
    private readonly Dictionary<string, Button> filterButtons = new Dictionary<string, Button>();
    private float lastRefresh;
    private string currentFilter = "all";
    private KiloFirstPersonController cachedPlayer;
    private bool initialized;
    private static Sprite arrowSprite;
    private readonly List<TeaserRow> teaserRows = new List<TeaserRow>(3);

    private sealed class TeaserRow
    {
        public GameObject root;
        public Image arrow;
        public TextMeshProUGUI distance;
        public TextMeshProUGUI title;
    }

    public void Initialize(RectTransform parent, TMP_FontAsset monoFont)
    {
        font = monoFont;

        RectTransform rt = gameObject.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(8f, 8f);
        rt.offsetMax = new Vector2(-8f, -8f);

        CreateTeaserRows(rt);
        CreateCommandBar(rt);
        bodyText = CreateBodyText(rt);
        initialized = true;
        Refresh();
    }

    private void CreateTeaserRows(RectTransform parent)
    {
        GameObject containerGO = new GameObject("NearbyTeaserRows", typeof(RectTransform));
        containerGO.transform.SetParent(parent, false);
        teaserRowsContainer = containerGO.GetComponent<RectTransform>();
        teaserRowsContainer.anchorMin = new Vector2(0f, 1f);
        teaserRowsContainer.anchorMax = new Vector2(1f, 1f);
        teaserRowsContainer.pivot = new Vector2(0.5f, 1f);
        teaserRowsContainer.anchoredPosition = Vector2.zero;
        teaserRowsContainer.sizeDelta = new Vector2(0f, 66f);

        for (int i = 0; i < 3; i++)
            teaserRows.Add(CreateTeaserRow(teaserRowsContainer, i));
    }

    private TeaserRow CreateTeaserRow(RectTransform parent, int index)
    {
        GameObject rowGO = new GameObject($"TeaserRow{index}", typeof(RectTransform));
        rowGO.transform.SetParent(parent, false);
        RectTransform rowRT = rowGO.GetComponent<RectTransform>();
        rowRT.anchorMin = new Vector2(0f, 1f);
        rowRT.anchorMax = new Vector2(1f, 1f);
        rowRT.pivot = new Vector2(0.5f, 1f);
        rowRT.anchoredPosition = new Vector2(0f, -index * 22f);
        rowRT.sizeDelta = new Vector2(0f, 22f);

        GameObject arrowGO = new GameObject("Arrow", typeof(RectTransform), typeof(Image));
        arrowGO.transform.SetParent(rowGO.transform, false);
        Image arrow = arrowGO.GetComponent<Image>();
        arrow.sprite = GetArrowSprite();
        arrow.color = ArrowGreen;
        arrow.raycastTarget = false;
        RectTransform arrowRT = arrowGO.GetComponent<RectTransform>();
        arrowRT.anchorMin = new Vector2(0f, 0.5f);
        arrowRT.anchorMax = new Vector2(0f, 0.5f);
        arrowRT.pivot = new Vector2(0.5f, 0.5f);
        arrowRT.anchoredPosition = new Vector2(18f, 0f);
        arrowRT.sizeDelta = new Vector2(14f, 18f);

        TextMeshProUGUI distance = CreateRowText(rowGO.transform, "Distance", 17f, Color.white, TextAlignmentOptions.MidlineLeft);
        RectTransform distanceRT = distance.rectTransform;
        distanceRT.anchorMin = new Vector2(0f, 0f);
        distanceRT.anchorMax = new Vector2(0f, 1f);
        distanceRT.offsetMin = new Vector2(48f, 0f);
        distanceRT.offsetMax = new Vector2(126f, 0f);

        TextMeshProUGUI title = CreateRowText(rowGO.transform, "Title", 17f, new Color(0.74f, 1f, 0.77f, 1f), TextAlignmentOptions.MidlineLeft);
        RectTransform titleRT = title.rectTransform;
        titleRT.anchorMin = new Vector2(0f, 0f);
        titleRT.anchorMax = new Vector2(1f, 1f);
        titleRT.offsetMin = new Vector2(132f, 0f);
        titleRT.offsetMax = new Vector2(-4f, 0f);

        return new TeaserRow { root = rowGO, arrow = arrow, distance = distance, title = title };
    }

    private TextMeshProUGUI CreateRowText(Transform parent, string name, float size, Color color, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false;
        return tmp;
    }

    private TextMeshProUGUI CreateTeaserText(RectTransform parent)
    {
        GameObject go = new GameObject("NearbyTeasers", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, 66f);

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.fontSize = 17f;
        tmp.color = TerminalDim;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.richText = true;
        tmp.text = string.Empty;

        GameObject arrowGO = new GameObject("NearbyTeaserArrows", typeof(RectTransform));
        arrowGO.transform.SetParent(parent, false);
        teaserArrowContainer = arrowGO.GetComponent<RectTransform>();
        teaserArrowContainer.anchorMin = rt.anchorMin;
        teaserArrowContainer.anchorMax = rt.anchorMax;
        teaserArrowContainer.pivot = rt.pivot;
        teaserArrowContainer.anchoredPosition = rt.anchoredPosition;
        teaserArrowContainer.sizeDelta = rt.sizeDelta;
        arrowGO.transform.SetAsLastSibling();

        return tmp;
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
        lineBgContainer.offsetMax = new Vector2(-6f, -106f);

        GameObject go = new GameObject("NearbyBody", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.offsetMin = new Vector2(0f, 0f);
        rt.offsetMax = new Vector2(-6f, -106f);

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
        barRT.anchoredPosition = new Vector2(0f, -68f);
        barRT.sizeDelta = new Vector2(0f, 34f);

        HorizontalLayoutGroup layout = barGO.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 4f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        CreateFilterButton(barGO.transform, "all", "[ALL]");
        CreateFilterButton(barGO.transform, "coffee", "[CAFE]");
        CreateFilterButton(barGO.transform, "bar", "[BAR]");
        CreateFilterButton(barGO.transform, "food", "[FOOD]");
        CreateFilterButton(barGO.transform, "convenience", "[SUP]");
    }

    private void CreateFilterButton(Transform parent, string category, string label)
    {
        GameObject go = new GameObject(category, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(Mathf.Max(50f, label.Length * 7f + 14f), 30f);

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
        tmp.fontSize = 11f;
        tmp.color = TerminalGreen;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
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
        if (!initialized)
            return;

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
        if (!initialized || bodyText == null || lineBgContainer == null)
            return;

        lastRefresh = Time.time;
        currentVisible.Clear();
        lineArrowAngles.Clear();
        teaserArrowAngles.Clear();

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

        UpdateTeaserRows();

        StringBuilder sb = new StringBuilder(512);

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
                string distance = FormatDistance(entry.Distance);

                float directionAngle = 0f;
                if (cachedPlayer != null && entry.HasWorldPosition)
                {
                    Vector3 toTarget = entry.WorldPosition - cachedPlayer.transform.position;
                    toTarget.y = 0f;
                    if (toTarget.sqrMagnitude > 0.0001f)
                    {
                        directionAngle = Vector3.SignedAngle(forward, toTarget.normalized, Vector3.up);
                    }
                }
                lineArrowAngles.Add(directionAngle);

                sb.AppendLine($"    <color=#FFFFFF>{distance,-7}</color> <color=#BCFFC5>{entry.Name.ToUpperInvariant()}</color>");
            }
        }

        bodyText.text = sb.ToString();
        UpdateLineBackgrounds();
    }

    private void UpdateTeaserRows()
    {
        if (teaserRows.Count == 0)
            return;

        SignalDirectorV2.NearbyTeaserInfo[] infos = SignalDirectorV2.Instance != null
            ? SignalDirectorV2.Instance.GetNearbyTeaserInfos()
            : null;

        for (int i = 0; i < teaserRows.Count; i++)
        {
            TeaserRow row = teaserRows[i];
            if (row == null || row.root == null) continue;

            bool hasInfo = infos != null && i < infos.Length;
            SignalDirectorV2.NearbyTeaserInfo info = hasInfo ? infos[i] : new SignalDirectorV2.NearbyTeaserInfo();

            if (hasInfo && info.hasSignal)
            {
                row.arrow.gameObject.SetActive(true);
                row.arrow.color = ArrowGreen;
                row.arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -info.relativeAngle);
                row.distance.text = info.distanceText;
                row.distance.color = Color.white;
                row.title.text = info.title;
                row.title.color = ArrowGreen;
            }
            else
            {
                row.arrow.gameObject.SetActive(false);
                row.distance.text = "";
                row.title.text = hasInfo ? info.scanningText : "scanning...";
                row.title.color = new Color(0.49f, 1f, 0.55f, 0.74f);
            }
        }
    }

    private void UpdateLineBackgrounds()
    {
        bodyText.ForceMeshUpdate();
        TMP_TextInfo info = bodyText.textInfo;
        int lineCount = info.lineCount;
        RectTransform textRT = bodyText.GetComponent<RectTransform>();
        Vector2 textSize = textRT.rect.size;

        int visibleIdx = 0;
        int arrowIdx = 0;
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
            if (arrowIdx < lineArrowAngles.Count)
            {
                Image arrow;
                if (arrowIdx < arrowPool.Count)
                {
                    arrow = arrowPool[arrowIdx];
                    arrow.gameObject.SetActive(true);
                }
                else
                {
                    GameObject arrowGO = new GameObject($"LineArrow{arrowIdx}", typeof(RectTransform), typeof(Image));
                    arrowGO.transform.SetParent(lineBgContainer, false);
                    arrow = arrowGO.GetComponent<Image>();
                    arrow.sprite = GetArrowSprite();
                    arrow.type = Image.Type.Simple;
                    arrow.preserveAspect = false;
                    arrow.raycastTarget = false;
                    arrowPool.Add(arrow);
                }

                RectTransform arrowRT = arrow.GetComponent<RectTransform>();
                arrowRT.anchorMin = new Vector2(0.5f, 0.5f);
                arrowRT.anchorMax = new Vector2(0.5f, 0.5f);
                arrowRT.pivot = new Vector2(0.5f, 0.5f);
                arrow.color = ArrowGreen;
                arrowRT.anchoredPosition = new Vector2(line.lineExtents.min.x + 12f, centerY);
                arrowRT.sizeDelta = new Vector2(14f, 18f);
                arrowRT.localRotation = Quaternion.Euler(0f, 0f, -lineArrowAngles[arrowIdx]);
                arrowRT.localScale = Vector3.one;
                arrowIdx++;
            }
            visibleIdx++;
        }

        for (int i = visibleIdx; i < lineBgPool.Count; i++)
        {
            lineBgPool[i].gameObject.SetActive(false);
        }
        for (int i = arrowIdx; i < arrowPool.Count; i++)
        {
            arrowPool[i].gameObject.SetActive(false);
        }
    }

    private void UpdateTeaserArrows()
    {
        if (teaserText == null || teaserArrowContainer == null)
            return;

        teaserText.ForceMeshUpdate();
        TMP_TextInfo info = teaserText.textInfo;
        int arrowIdx = 0;
        for (int i = 0; i < info.lineCount; i++)
        {
            TMP_LineInfo line = info.lineInfo[i];
            if (line.characterCount == 0) continue;
            if (arrowIdx >= teaserArrowAngles.Count) break;

            Image arrow;
            if (arrowIdx < teaserArrowPool.Count)
            {
                arrow = teaserArrowPool[arrowIdx];
                arrow.gameObject.SetActive(true);
            }
            else
            {
                GameObject arrowGO = new GameObject($"TeaserArrow{arrowIdx}", typeof(RectTransform), typeof(Image));
                arrowGO.transform.SetParent(teaserText.transform, false);
                arrow = arrowGO.GetComponent<Image>();
                arrow.sprite = GetArrowSprite();
                arrow.type = Image.Type.Simple;
                arrow.preserveAspect = false;
                arrow.raycastTarget = false;
                teaserArrowPool.Add(arrow);
            }

            RectTransform arrowRT = arrow.GetComponent<RectTransform>();
            arrowRT.anchorMin = new Vector2(0.5f, 0.5f);
            arrowRT.anchorMax = new Vector2(0.5f, 0.5f);
            arrowRT.pivot = new Vector2(0.5f, 0.5f);
            float centerY = (line.lineExtents.max.y + line.lineExtents.min.y) * 0.5f;
            arrow.color = ArrowGreen;
            arrowRT.anchoredPosition = new Vector2(line.lineExtents.min.x + 12f, centerY);
            arrowRT.sizeDelta = new Vector2(14f, 18f);
            arrowRT.localRotation = Quaternion.Euler(0f, 0f, -teaserArrowAngles[arrowIdx]);
            arrowRT.localScale = Vector3.one;
            arrowIdx++;
        }

        for (int i = arrowIdx; i < teaserArrowPool.Count; i++)
            teaserArrowPool[i].gameObject.SetActive(false);
    }

    private string FormatDistance(float meters)
    {
        float miles = meters / 1609.34f;
        if (miles < 0.33f)
            return $"{Mathf.RoundToInt(meters * 3.28084f)}ft";
        return $"{miles:F1}mi";
    }

    private static Sprite GetArrowSprite()
    {
        if (arrowSprite != null) return arrowSprite;

        const int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color clear = new Color(1f, 1f, 1f, 0f);
        Color solid = Color.white;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, clear);
        }

        int center = size / 2;
        int top = 27;
        int bottom = 5;
        for (int y = bottom; y <= top; y++)
        {
            float t = (top - y) / (float)(top - bottom);
            int halfWidth = Mathf.RoundToInt(Mathf.Lerp(1f, 5f, t));
            for (int x = center - halfWidth; x <= center + halfWidth; x++)
                tex.SetPixel(x, y, solid);
        }

        tex.Apply(false, true);
        tex.filterMode = FilterMode.Bilinear;
        arrowSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        return arrowSprite;
    }

}
