using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class K1L0Dock : MonoBehaviour
{
    public const int TransmitAction = -100;

    public System.Action<int> OnButtonTapped;

    private Graphic[] iconImages;
    private Image[] bgImages;
    private RectTransform[] buttonRects;
    private bool[] activeStates = new bool[4];
    private readonly Vector3[] cornerBuffer = new Vector3[4];
    private RectTransform dockRect;
    private float lastLayoutWidth = -1f;
    private float lastTapTime = -999f;
    private int lastTapPanel = -1;

    // Maps visual button index to panel/action index.
    private static readonly int[] panelMap = { 0, 3, 1, 2 };
    private static readonly string[] labels = { "[LOCATION]", "[TRANSMIT]", "[+]", "[SETTINGS]" };

    public static void ClearCachedMaterials()
    {
        K1L0GlassFactory.ClearCachedMaterials();
    }

    public void Initialize(RectTransform parent, TMP_FontAsset monoFont)
    {
        ClearCachedMaterials();
        dockRect = gameObject.AddComponent<RectTransform>();
        dockRect.SetParent(parent, false);
        dockRect.anchorMin = Vector2.zero;
        dockRect.anchorMax = Vector2.one;
        dockRect.offsetMin = Vector2.zero;
        dockRect.offsetMax = Vector2.zero;

        // Add a Canvas override so dock renders in front of teasers
        var overrideCanvas = gameObject.AddComponent<Canvas>();
        overrideCanvas.overrideSorting = true;
        overrideCanvas.sortingOrder = 1100; // Above HUD (1000)
        gameObject.AddComponent<GraphicRaycaster>();

        Sprite rectSpr = K1L0GlassFactory.ControlRectSprite;

        iconImages = new Graphic[labels.Length];
        bgImages = new Image[labels.Length];
        buttonRects = new RectTransform[labels.Length];

        for (int i = 0; i < labels.Length; i++)
        {
            int idx = i;

            GameObject btnGO = new GameObject($"DockBtn_{labels[i]}");
            btnGO.transform.SetParent(dockRect, false);
            RectTransform btnRT = btnGO.AddComponent<RectTransform>();
            btnRT.anchorMin = new Vector2(0.5f, 0f);
            btnRT.anchorMax = new Vector2(0.5f, 0f);
            btnRT.pivot = new Vector2(0.5f, 0f);
            buttonRects[i] = btnRT;

            Image bg = btnGO.AddComponent<Image>();
            bg.sprite = rectSpr;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0f, 0f, 0f, 1f);
            bg.raycastTarget = true;
            bgImages[i] = bg;

            Button btn = btnGO.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => OnTap(panelMap[idx]));

            GameObject labelGO = new GameObject("Label");
            labelGO.transform.SetParent(btnGO.transform, false);
            RectTransform labelRT = labelGO.AddComponent<RectTransform>();
            labelRT.anchorMin = Vector2.zero;
            labelRT.anchorMax = Vector2.one;
            labelRT.offsetMin = Vector2.zero;
            labelRT.offsetMax = Vector2.zero;
            TextMeshProUGUI label = labelGO.AddComponent<TextMeshProUGUI>();
            label.font = monoFont;
            label.fontSize = i == 1 ? 14f : 11f;
            label.text = labels[i];
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(0.84f, 0.90f, 0.97f, 1f);
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            iconImages[i] = label;
        }

        LayoutButtons();
    }

    void LateUpdate()
    {
        LayoutButtons();
    }

    void LayoutButtons()
    {
        if (dockRect == null || buttonRects == null) return;

        float safeWidth = dockRect.rect.width > 1f ? dockRect.rect.width : Screen.safeArea.width;
        if (Mathf.Abs(safeWidth - lastLayoutWidth) < 0.5f) return;
        lastLayoutWidth = safeWidth;

        float[] baseWidths = { 86f, 112f, 64f, 86f };
        float baseGap = 8f;
        float baseTotal = 0f;
        for (int i = 0; i < baseWidths.Length; i++) baseTotal += baseWidths[i];
        baseTotal += baseGap * (baseWidths.Length - 1);

        float available = Mathf.Max(280f, safeWidth - 24f);
        float scale = Mathf.Clamp(available / baseTotal, 0.78f, 1f);
        float gap = baseGap * scale;
        float total = 0f;
        for (int i = 0; i < baseWidths.Length; i++) total += baseWidths[i] * scale;
        total += gap * (baseWidths.Length - 1);

        float x = -total * 0.5f;
        for (int i = 0; i < buttonRects.Length; i++)
        {
            if (buttonRects[i] == null) continue;
            float width = baseWidths[i] * scale;
            buttonRects[i].sizeDelta = new Vector2(width, 48f);
            buttonRects[i].anchoredPosition = new Vector2(x + width * 0.5f, 10f);
            x += width + gap;
        }
    }

    static Sprite CreateLocationPinSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float alpha = 0f;
                float ny = (float)y / size;
                float nx = (float)x / size;

                float hx = nx - 0.5f, hy = ny - 0.68f;
                float hDist = Mathf.Sqrt(hx * hx + hy * hy);
                if (hDist < 0.24f) alpha = 1f;
                else if (hDist < 0.26f) alpha = (0.26f - hDist) / 0.02f;

                if (hDist < 0.10f) alpha = 0f;
                else if (hDist < 0.12f) alpha = Mathf.Min(alpha, (hDist - 0.10f) / 0.02f);

                if (ny < 0.50f && ny > 0.15f)
                {
                    float tailWidth = 0.14f * (ny - 0.15f) / 0.35f;
                    float ddx = Mathf.Abs(nx - 0.5f);
                    if (ddx < tailWidth) alpha = Mathf.Max(alpha, 1f);
                    else if (ddx < tailWidth + 0.02f) alpha = Mathf.Max(alpha, (tailWidth + 0.02f - ddx) / 0.02f);
                }

                tex.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100);
    }

    static Sprite CreateCircleSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - r + 0.5f;
                float dy = y - r + 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(r - dist);
                tex.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100);
    }

    static Sprite CreatePersonSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (float)x / size;
                float ny = (float)y / size;
                float alpha = 0f;

                float hx = nx - 0.5f, hy = ny - 0.72f;
                float hDist = Mathf.Sqrt(hx * hx + hy * hy);
                if (hDist < 0.14f) alpha = 1f;
                else if (hDist < 0.16f) alpha = (0.16f - hDist) / 0.02f;

                if (ny < 0.52f && ny > 0.15f)
                {
                    float t = (0.52f - ny) / 0.37f;
                    float halfW = 0.15f + t * 0.18f;
                    float ddx = Mathf.Abs(nx - 0.5f);
                    if (ny > 0.42f)
                    {
                        float shoulderR = 0.10f;
                        float sx = ddx - (halfW - shoulderR);
                        float sy = ny - 0.42f;
                        if (sx > 0)
                        {
                            float sd = Mathf.Sqrt(sx * sx + sy * sy);
                            if (sd < shoulderR) alpha = Mathf.Max(alpha, 1f);
                            else if (sd < shoulderR + 0.02f) alpha = Mathf.Max(alpha, (shoulderR + 0.02f - sd) / 0.02f);
                        }
                        else if (ddx < halfW) alpha = Mathf.Max(alpha, 1f);
                    }
                    else
                    {
                        if (ddx < halfW) alpha = Mathf.Max(alpha, 1f);
                        else if (ddx < halfW + 0.02f) alpha = Mathf.Max(alpha, (halfW + 0.02f - ddx) / 0.02f);
                    }
                }
                tex.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100);
    }

    void OnTap(int panelIndex)
    {
        if (panelIndex == lastTapPanel && Time.unscaledTime - lastTapTime < 0.25f)
        {
            Debug.Log($"[K1L0Dock] Suppressed duplicate tap target={panelIndex}");
            return;
        }

        lastTapPanel = panelIndex;
        lastTapTime = Time.unscaledTime;
        Debug.Log($"[K1L0Dock] Tap target={panelIndex}");
        OnButtonTapped?.Invoke(panelIndex);
    }

    public bool TryHandleScreenPoint(Vector2 screenPoint, Camera eventCamera)
    {
        int hitIndex = HitTest(screenPoint, 36f);
        if (hitIndex < 0)
            return false;

        int panelIndex = panelMap[hitIndex];
        OnTap(panelIndex);
        return true;
    }

    public string GetDebugHitSummary(Vector2 screenPoint)
    {
        if (buttonRects == null)
            return "dock=null";

        System.Text.StringBuilder sb = new System.Text.StringBuilder(256);
        sb.Append($"point=({screenPoint.x:F0},{screenPoint.y:F0})");
        for (int i = 0; i < buttonRects.Length; i++)
        {
            Rect rect = GetExpandedScreenRect(buttonRects[i], 36f);
            sb.Append($" btn{i}[panel={panelMap[i]}]=({rect.xMin:F0},{rect.yMin:F0},{rect.xMax:F0},{rect.yMax:F0}) hit={rect.Contains(screenPoint)}");
        }
        return sb.ToString();
    }

    private int HitTest(Vector2 screenPoint, float padding)
    {
        if (buttonRects == null)
            return -1;

        for (int i = 0; i < buttonRects.Length; i++)
        {
            Rect rect = GetExpandedScreenRect(buttonRects[i], padding);
            if (!rect.Contains(screenPoint))
                continue;

            Debug.Log($"[K1L0Dock] Hit button={i} panel={panelMap[i]} {GetDebugHitSummary(screenPoint)}");
            return i;
        }

        return -1;
    }

    private Rect GetExpandedScreenRect(RectTransform rt, float padding)
    {
        if (rt == null) return Rect.zero;
        rt.GetWorldCorners(cornerBuffer);
        float minX = Mathf.Min(cornerBuffer[0].x, cornerBuffer[1].x, cornerBuffer[2].x, cornerBuffer[3].x) - padding;
        float maxX = Mathf.Max(cornerBuffer[0].x, cornerBuffer[1].x, cornerBuffer[2].x, cornerBuffer[3].x) + padding;
        float minY = Mathf.Min(cornerBuffer[0].y, cornerBuffer[1].y, cornerBuffer[2].y, cornerBuffer[3].y) - padding;
        float maxY = Mathf.Max(cornerBuffer[0].y, cornerBuffer[1].y, cornerBuffer[2].y, cornerBuffer[3].y) + padding;
        return Rect.MinMaxRect(minX, minY, maxX, maxY);
    }

    void UpdateButtonStates()
    {
        for (int i = 0; i < iconImages.Length; i++)
        {
            int panelIndex = panelMap[i];
            bool active = panelIndex >= 0 && panelIndex < activeStates.Length && activeStates[panelIndex];
            bgImages[i].color = active
                ? new Color(0.05f, 0.42f, 0.08f, 0.96f)
                : new Color(0f, 0f, 0f, 1f);
            if (active)
            {
                iconImages[i].color = new Color(0.47f, 1f, 0.54f, 1f); // Green when active
            }
            else
            {
                iconImages[i].color = new Color(0.84f, 0.90f, 0.97f, 1f); // White when inactive
            }
        }
    }

    public void SetActiveButton(int index, bool active)
    {
        if (index >= 0 && index < activeStates.Length)
            activeStates[index] = active;
        UpdateButtonStates();
    }
}
