using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class K1L0StatusMode : MonoBehaviour
{
    private static readonly Color TerminalDim = new Color(0.56f, 1f, 0.62f, 1f);
    private static readonly Color LineBgColor = new Color(0.01f, 0.02f, 0.01f, 0.82f);
    private const float LineBgPadH = 4f;
    private const float LineBgPadV = 1f;

    private TMP_FontAsset font;
    private RectTransform rootRt;
    private TextMeshProUGUI bodyText;
    private RectTransform lineBgContainer;
    private RectTransform tileContainer;
    private readonly List<RectTransform> tilePool = new List<RectTransform>();
    private readonly List<Image> lineBgPool = new List<Image>();
    private static readonly Color TileFill = new Color(0.015f, 0.07f, 0.03f, 0.92f);
    private static readonly Color TileBorder = new Color(0.30f, 0.85f, 0.42f, 0.85f);
    private static readonly Color SymbolColor = new Color(0.74f, 1f, 0.79f, 1f);
    private List<PedometerService.HourlyStepData> hourlySteps = new List<PedometerService.HourlyStepData>();
    private float lastUpdate;
    private float lastHourlyRequest = -999f;
    private bool hourlyRequestInFlight;
    private float simulatedDust;
    private bool initialized;
    private const float HourlyRefreshSeconds = 300f;
    private static readonly Dictionary<string, string> ElementSymbols = new Dictionary<string, string>
    {
        { "actinium", "Ac" }, { "aluminum", "Al" }, { "aluminium", "Al" }, { "antimony", "Sb" },
        { "barium", "Ba" }, { "beryllium", "Be" }, { "bismuth", "Bi" }, { "boron", "B" },
        { "cadmium", "Cd" }, { "cerium", "Ce" }, { "chromium", "Cr" }, { "cobalt", "Co" },
        { "copper", "Cu" }, { "dysprosium", "Dy" }, { "erbium", "Er" }, { "europium", "Eu" },
        { "gadolinium", "Gd" }, { "gallium", "Ga" }, { "germanium", "Ge" }, { "hafnium", "Hf" },
        { "holmium", "Ho" }, { "indium", "In" }, { "lanthanum", "La" }, { "lutetium", "Lu" },
        { "manganese", "Mn" }, { "molybdenum", "Mo" }, { "neodymium", "Nd" }, { "nickel", "Ni" },
        { "niobium", "Nb" }, { "palladium", "Pd" }, { "praseodymium", "Pr" }, { "rhenium", "Re" },
        { "samarium", "Sm" }, { "scandium", "Sc" }, { "selenium", "Se" }, { "tantalum", "Ta" },
        { "tellurium", "Te" }, { "terbium", "Tb" }, { "thorium", "Th" }, { "thulium", "Tm" },
        { "titanium", "Ti" }, { "tungsten", "W" }, { "vanadium", "V" }, { "ytterbium", "Yb" },
        { "yttrium", "Y" }, { "zinc", "Zn" }, { "zirconium", "Zr" }
    };

    public void Initialize(RectTransform parent, TMP_FontAsset monoFont)
    {
        font = monoFont;

        RectTransform rt = gameObject.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(8f, 8f);
        rt.offsetMax = new Vector2(-8f, -8f);
        rootRt = rt;

        // Line background container — renders behind text
        GameObject bgGO = new GameObject("LineBgs", typeof(RectTransform));
        bgGO.transform.SetParent(rt, false);
        lineBgContainer = bgGO.GetComponent<RectTransform>();
        lineBgContainer.anchorMin = Vector2.zero;
        lineBgContainer.anchorMax = Vector2.one;
        lineBgContainer.offsetMin = new Vector2(0f, 0f);
        lineBgContainer.offsetMax = new Vector2(-6f, 0f);

        GameObject textGO = new GameObject("StatusBody", typeof(RectTransform));
        textGO.transform.SetParent(rt, false);
        RectTransform textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(0f, 0f);
        textRT.offsetMax = new Vector2(-6f, 0f);

        bodyText = textGO.AddComponent<TextMeshProUGUI>();
        bodyText.font = font;
        bodyText.fontSize = 16f;
        bodyText.color = TerminalDim;
        bodyText.alignment = TextAlignmentOptions.TopLeft;
        bodyText.enableWordWrapping = true;
        bodyText.overflowMode = TextOverflowModes.Overflow;
        bodyText.richText = true;

        // Periodic-table tile grid for collected rare-earth elements, placed below the
        // text. Top-anchored, full width; height driven by its content fitter.
        GameObject tilesGO = new GameObject("ElementTiles", typeof(RectTransform));
        tilesGO.transform.SetParent(rt, false);
        tileContainer = tilesGO.GetComponent<RectTransform>();
        tileContainer.anchorMin = new Vector2(0f, 1f);
        tileContainer.anchorMax = new Vector2(1f, 1f);
        tileContainer.pivot = new Vector2(0.5f, 1f);
        tileContainer.offsetMin = new Vector2(0f, tileContainer.offsetMin.y);
        tileContainer.offsetMax = new Vector2(-6f, tileContainer.offsetMax.y);
        GridLayoutGroup grid = tilesGO.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(60f, 72f);
        grid.spacing = new Vector2(6f, 6f);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperLeft;
        ContentSizeFitter fitter = tilesGO.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        simulatedDust = Random.Range(0.28f, 0.74f);
        initialized = true;
        Request7DaySteps();
        RequestHourlySteps();
        UpdateMetrics();
    }

    private void OnEnable()
    {
        if (!initialized)
            return;

        UpdateMetrics();
        Request7DaySteps();
        RequestHourlySteps();
    }

    private void Update()
    {
        if (Time.time - lastUpdate > 3f)
        {
            UpdateMetrics();
        }
    }

    private void Request7DaySteps()
    {
        var ped = Object.FindFirstObjectByType<PedometerService>();
        if (ped != null)
        {
            ped.GetLast7DaysSteps(_ => { });
        }
    }

    private void RequestHourlySteps()
    {
        if (hourlyRequestInFlight) return;
        var ped = Object.FindFirstObjectByType<PedometerService>();
        if (ped == null) return;

        hourlyRequestInFlight = true;
        lastHourlyRequest = Time.time;
        ped.GetLast24HoursBreakdown(list =>
        {
            hourlyRequestInFlight = false;
            hourlySteps = list != null
                ? new List<PedometerService.HourlyStepData>(list)
                : new List<PedometerService.HourlyStepData>();
            if (initialized)
                UpdateMetrics();
        });
    }

    private void UpdateMetrics()
    {
        if (!initialized || bodyText == null || lineBgContainer == null)
            return;

        lastUpdate = Time.time;
        if (Time.time - lastHourlyRequest > HourlyRefreshSeconds)
            RequestHourlySteps();

        var ped = Object.FindFirstObjectByType<PedometerService>();
        int steps24 = ped != null ? ped.stepsLast24Hours : -1;
        int steps7d = ped != null ? ped.stepsLast7Days : -1;

        int kilosync = ped != null ? ped.kilosyncSteps : -1;
        bool inert = ped == null || !ped.kilosyncReady || ped.isKilosyncInert;
        var inventory = TransmissionManager.Instance != null
            ? TransmissionManager.Instance.GetRareEarthInventory()
            : new List<TransmissionManager.InventoryMaterial>();

        StringBuilder sb = new StringBuilder(512);
        sb.AppendLine("<color=#8EFF9F>> STRENGTH (STEPS)</color>");
        sb.AppendLine($"> steps: <color=#BCFFC5>{FormatSteps(kilosync)}</color>");
        sb.AppendLine($"> 24h:   <color=#BCFFC5>{FormatSteps(steps24)}</color>");
        sb.AppendLine($"> 7d:    <color=#BCFFC5>{FormatSteps(steps7d)}</color>");
        if (inert)
            sb.AppendLine("<color=#6EFF84>> kilosync inert</color>");
        sb.AppendLine();
        sb.AppendLine("<color=#6EFF84>> hourly walk</color>");
        sb.AppendLine(BuildHourlyWalkChart());
        sb.AppendLine();
        sb.AppendLine("<color=#6EFF84>> rare earth</color>");
        var elementTotals = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < inventory.Count; i++)
        {
            var item = inventory[i];
            if (item == null || string.IsNullOrWhiteSpace(item.material)) continue;
            if (item.grams <= 0) continue;

            string element = ElementNameForDisplay(item.material);
            if (!elementTotals.ContainsKey(element)) elementTotals[element] = 0;
            elementTotals[element] += item.grams;
        }

        bool showedElement = elementTotals.Count > 0;
        var elementNames = new List<string>(elementTotals.Keys);
        elementNames.Sort(System.StringComparer.OrdinalIgnoreCase);
        if (!showedElement)
            sb.AppendLine("> none collected");

        bodyText.text = sb.ToString();
        UpdateLineBackgrounds();
        BuildElementTiles(elementNames, elementTotals);
    }

    // Renders each collected element as a periodic-table tile: a bordered box with a
    // large chemical symbol, the full element name, and the collected grams.
    private void BuildElementTiles(List<string> elementNames, Dictionary<string, int> totals)
    {
        if (tileContainer == null) return;

        for (int i = 0; i < elementNames.Count; i++)
        {
            string element = elementNames[i];
            RectTransform tile = GetTile(i);
            tile.gameObject.SetActive(true);
            string symbol = ChemicalSymbolFor(element);
            string grams = $"{totals[element]:N0}g";

            TextMeshProUGUI[] texts = tile.GetComponentsInChildren<TextMeshProUGUI>(true);
            texts[0].text = symbol;          // large symbol
            texts[1].text = element;         // full name
            texts[2].text = grams;           // grams, corner
        }

        for (int i = elementNames.Count; i < tilePool.Count; i++)
            tilePool[i].gameObject.SetActive(false);

        // Push the grid down so it sits beneath the rendered text block.
        bodyText.ForceMeshUpdate();
        float textHeight = bodyText.preferredHeight;
        tileContainer.anchoredPosition = new Vector2(0f, -(textHeight + 14f));
    }

    private RectTransform GetTile(int index)
    {
        if (index < tilePool.Count)
            return tilePool[index];

        GameObject tileGO = new GameObject($"ElementTile{index}", typeof(RectTransform), typeof(Image));
        tileGO.transform.SetParent(tileContainer, false);
        Image box = tileGO.GetComponent<Image>();
        box.color = TileFill;
        box.raycastTarget = false;
        Outline outline = tileGO.AddComponent<Outline>();
        outline.effectColor = TileBorder;
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        // Large chemical symbol, centred.
        var sym = AddTileText(tileGO.transform, "Symbol", 26f, SymbolColor, TextAlignmentOptions.Center);
        Anchor(sym, new Vector2(0f, 0.18f), new Vector2(1f, 0.95f));

        // Full element name along the bottom.
        var name = AddTileText(tileGO.transform, "Name", 9.5f, TerminalDim, TextAlignmentOptions.Center);
        Anchor(name, new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.24f));

        // Grams in the top-right corner, like an atomic mass.
        var g = AddTileText(tileGO.transform, "Grams", 8.5f, new Color(0.74f, 1f, 0.79f, 1f), TextAlignmentOptions.TopRight);
        Anchor(g, new Vector2(0.0f, 0.74f), new Vector2(0.96f, 0.99f));

        RectTransform rt = tileGO.GetComponent<RectTransform>();
        tilePool.Add(rt);
        return rt;
    }

    private TextMeshProUGUI AddTileText(Transform parent, string name, float size, Color color, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        t.font = font;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.enableWordWrapping = false;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        return t;
    }

    private static void Anchor(TextMeshProUGUI t, Vector2 min, Vector2 max)
    {
        RectTransform rt = t.rectTransform;
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static string ChemicalSymbolFor(string material)
    {
        if (string.IsNullOrWhiteSpace(material)) return "--";
        string key = material.Trim().ToLowerInvariant();
        int space = key.IndexOf(' ');
        if (space > 0) key = key.Substring(0, space);
        key = key.Replace(",", "").Replace(".", "");
        if (ElementSymbols.TryGetValue(key, out string symbol)) return symbol;
        return material.Trim().Length <= 2 ? material.Trim() : material.Trim().Substring(0, 2).ToUpperInvariant();
    }

    private static string ElementNameForDisplay(string material)
    {
        if (string.IsNullOrWhiteSpace(material)) return "element";
        string normalized = material.Trim().ToLowerInvariant();
        string[] parts = normalized.Split(new[] { ' ', ',', '.', '-', '_', '/', '\\', ':', ';', '(', ')', '[', ']' }, System.StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            string token = parts[i];
            if (ElementSymbols.ContainsKey(token))
                return char.ToUpperInvariant(token[0]) + token.Substring(1);
        }

        string first = parts.Length > 0 ? parts[0] : normalized;
        return string.IsNullOrWhiteSpace(first)
            ? "element"
            : char.ToUpperInvariant(first[0]) + first.Substring(1);
    }

    private static string FormatSteps(int steps)
    {
        return steps >= 0 ? steps.ToString("N0") : "pending";
    }

    private string BuildHourlyWalkChart()
    {
        if (hourlySteps == null || hourlySteps.Count == 0)
            return "> pending hourly steps";

        hourlySteps.Sort((a, b) => a.time.CompareTo(b.time));
        int count = Mathf.Min(24, hourlySteps.Count);
        int start = Mathf.Max(0, hourlySteps.Count - count);
        int maxSteps = 1;
        for (int i = start; i < hourlySteps.Count; i++)
            maxSteps = Mathf.Max(maxSteps, hourlySteps[i].steps);

        const string blocks = "▁▂▃▄▅▆▇█";
        StringBuilder bars = new StringBuilder(count);
        int total = 0;
        for (int i = start; i < hourlySteps.Count; i++)
        {
            int steps = Mathf.Max(0, hourlySteps[i].steps);
            total += steps;
            int idx = steps <= 0
                ? 0
                : Mathf.Clamp(Mathf.CeilToInt((steps / (float)maxSteps) * blocks.Length) - 1, 0, blocks.Length - 1);
            bars.Append(blocks[idx]);
        }

        return $"> <color=#BCFFC5>{bars}</color> <size=12>{total:N0}/24h max {maxSteps:N0}/hr</size>";
    }

    private string BuildMetricLine(string name, float amount, string value)
    {
        int total = 22;
        int filled = Mathf.Clamp(Mathf.RoundToInt(total * Mathf.Clamp01(amount)), 0, total);
        string bar = new string('=', filled).PadRight(total, '.');
        return $"> {name,-10} [{bar}] <color=#BCFFC5>{value}</color>";
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
}
