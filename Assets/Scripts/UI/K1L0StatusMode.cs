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
    private TextMeshProUGUI bodyText;
    private RectTransform lineBgContainer;
    private readonly List<Image> lineBgPool = new List<Image>();
    private float lastUpdate;
    private float simulatedHydration;
    private float simulatedDust;

    public void Initialize(RectTransform parent, TMP_FontAsset monoFont)
    {
        font = monoFont;

        RectTransform rt = gameObject.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(8f, 8f);
        rt.offsetMax = new Vector2(-8f, -8f);

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

        simulatedHydration = Random.Range(0.42f, 0.86f);
        simulatedDust = Random.Range(0.28f, 0.74f);
        Request7DaySteps();
        UpdateMetrics();
    }

    private void OnEnable()
    {
        UpdateMetrics();
        Request7DaySteps();
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

    private void UpdateMetrics()
    {
        lastUpdate = Time.time;

        var ped = Object.FindFirstObjectByType<PedometerService>();
        int steps24 = ped != null ? ped.stepsLast24Hours : -1;
        int steps7d = ped != null ? ped.stepsLast7Days : -1;

        simulatedHydration = Mathf.Clamp01(simulatedHydration + Random.Range(-0.04f, 0.03f));
        simulatedDust = Mathf.Clamp01(simulatedDust + Random.Range(-0.03f, 0.05f));

        float movement = steps24 > 0 ? Mathf.Clamp01(steps24 / 14000f) : 0.08f;
        float signal = Mathf.Clamp01(((TransmitterScanner.Instance != null ? Mathf.Min(6, TransmitterScanner.Instance.GetAll().Count) : 0) / 6f) * 0.7f +
                                     ((FirebaseAuthManager.Instance != null && FirebaseAuthManager.Instance.isAuthenticated) ? 0.3f : 0.08f));

        StringBuilder sb = new StringBuilder(512);
        sb.AppendLine("<color=#8EFF9F>> STATUS / LOCAL NODE</color>");
        sb.AppendLine("<color=#6EFF84>> four live system rails follow</color>");
        sb.AppendLine();
        sb.AppendLine(BuildMetricLine("HYDRATION", simulatedHydration, $"{Mathf.RoundToInt(simulatedHydration * 100f)}%"));
        sb.AppendLine(BuildMetricLine("SIGNAL", signal, $"{Mathf.RoundToInt(signal * 100f)}%"));
        sb.AppendLine(BuildMetricLine("MOVEMENT", movement, steps24 > 0 ? $"{steps24:N0} steps" : "pending"));
        sb.AppendLine(BuildMetricLine("DUST", simulatedDust, $"{Mathf.RoundToInt(Mathf.Lerp(28f, 420f, simulatedDust))}g"));
        sb.AppendLine();
        sb.Append("<color=#73FF88>> seven day movement cache: ");
        sb.Append(steps7d > 0 ? $"{steps7d:N0} steps</color>" : "pending</color>");

        bodyText.text = sb.ToString();
        UpdateLineBackgrounds();
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
