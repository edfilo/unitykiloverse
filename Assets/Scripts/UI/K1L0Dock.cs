using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class K1L0Dock : MonoBehaviour
{
    public System.Action<int> OnButtonTapped;

    private K1L0GlassChrome[] buttonChromes;
    private Image[] iconImages;
    private RectTransform[] buttonRects;
    private bool[] activeStates = new bool[3];

    public static void ClearCachedMaterials()
    {
        K1L0GlassFactory.ClearCachedMaterials();
    }

    public void Initialize(RectTransform parent, TMP_FontAsset monoFont)
    {
        ClearCachedMaterials();
        RectTransform rt = gameObject.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0.5f, 0);
        rt.anchorMax = new Vector2(0.5f, 0);
        rt.pivot = new Vector2(0.5f, 0);
        rt.anchoredPosition = new Vector2(0, 36);
        rt.sizeDelta = new Vector2(352, 56);

        HorizontalLayoutGroup hlg = gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 12;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;

        Sprite[] icons = {
            CreateLocationPinSprite(64),
            CreateBarChartSprite(64),
            CreatePersonSprite(64)
        };

        buttonChromes = new K1L0GlassChrome[3];
        iconImages = new Image[3];
        buttonRects = new RectTransform[3];
        K1L0GlassStyle buttonStyle = K1L0GlassFactory.DockButtonStyle;

        for (int i = 0; i < 3; i++)
        {
            int idx = i;

            GameObject btnGO = new GameObject($"Btn{i}");
            btnGO.transform.SetParent(rt, false);
            RectTransform btnRT = btnGO.AddComponent<RectTransform>();
            btnRT.sizeDelta = new Vector2(108, 42);
            buttonRects[i] = btnRT;
            LayoutElement le = btnGO.AddComponent<LayoutElement>();
            le.preferredWidth = 108;
            le.preferredHeight = 42;

            buttonChromes[i] = K1L0GlassFactory.AttachChrome(btnGO.transform, $"Btn{i}", buttonStyle);
            ConfigureDockChrome(buttonChromes[i]);

            Button btn = btnGO.AddComponent<Button>();
            btn.targetGraphic = buttonChromes[i].blurFill;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => OnTap(idx));

            // Icon
            GameObject iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(btnGO.transform, false);
            RectTransform iconRT = iconGO.AddComponent<RectTransform>();
            iconRT.anchorMin = new Vector2(0.5f, 0.5f);
            iconRT.anchorMax = new Vector2(0.5f, 0.5f);
            iconRT.sizeDelta = new Vector2(24, 24);
            Image iconImg = iconGO.AddComponent<Image>();
            iconImg.sprite = icons[i];
            iconImg.color = new Color(0.84f, 0.90f, 0.97f, 0.88f);
            iconImg.raycastTarget = false;
            iconImages[i] = iconImg;
        }

        // Auto-screenshot after 3 seconds for iteration
        StartCoroutine(AutoScreenshot());
    }

    IEnumerator AutoScreenshot()
    {
        yield return new WaitForSeconds(3f);
        var ss = K1L0Screenshot.Instance;
        if (ss != null)
        {
            Debug.Log("[K1L0Dock] Auto-screenshot firing");
            ss.Capture();
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

    static Sprite CreateBarChartSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        float[] barHeights = { 0.4f, 0.7f, 0.55f, 0.85f };
        float barWidth = 0.13f;
        float gap = 0.05f;
        float totalW = barWidth * 4 + gap * 3;
        float startX = 0.5f - totalW * 0.5f;
        float baseY = 0.15f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (float)x / size;
                float ny = (float)y / size;
                float alpha = 0f;

                for (int b = 0; b < 4; b++)
                {
                    float bx = startX + b * (barWidth + gap);
                    float top = baseY + barHeights[b] * 0.7f;
                    if (nx >= bx && nx <= bx + barWidth && ny >= baseY && ny <= top)
                    {
                        alpha = 1f;
                        float cornerR = barWidth * 0.3f;
                        if (ny > top - cornerR)
                        {
                            float cdx = 0f;
                            if (nx < bx + cornerR) cdx = bx + cornerR - nx;
                            else if (nx > bx + barWidth - cornerR) cdx = nx - (bx + barWidth - cornerR);
                            if (cdx > 0)
                            {
                                float cdy = ny - (top - cornerR);
                                float cd = Mathf.Sqrt(cdx * cdx + cdy * cdy);
                                if (cd > cornerR) alpha = 0f;
                                else if (cd > cornerR - 0.02f) alpha = (cornerR - cd) / 0.02f;
                            }
                        }
                    }
                }
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

    void OnTap(int index)
    {
        OnButtonTapped?.Invoke(index);
    }

    void ConfigureDockChrome(K1L0GlassChrome chrome)
    {
        if (chrome == null)
            return;

        chrome.shadow.sprite = K1L0GlassFactory.DockRectSprite;
        chrome.blurFill.sprite = K1L0GlassFactory.DockRectSprite;
        chrome.overlay.sprite = K1L0GlassFactory.DockRectSprite;
        chrome.border.sprite = K1L0GlassFactory.DockBorderSprite;
        chrome.accent.sprite = K1L0GlassFactory.DockRectSprite;
        chrome.sheen.sprite = K1L0GlassFactory.DockRectSprite;

        chrome.shadow.rectTransform.offsetMin = new Vector2(1f, -3f);
        chrome.shadow.rectTransform.offsetMax = new Vector2(-1f, -1f);

        chrome.blurFill.rectTransform.offsetMin = new Vector2(0f, 0f);
        chrome.blurFill.rectTransform.offsetMax = new Vector2(0f, 0f);

        chrome.overlay.rectTransform.offsetMin = new Vector2(1f, 1f);
        chrome.overlay.rectTransform.offsetMax = new Vector2(-1f, -1f);

        chrome.accent.rectTransform.offsetMin = new Vector2(3f, 3f);
        chrome.accent.rectTransform.offsetMax = new Vector2(-3f, -3f);

        if (chrome.sheenMask != null)
        {
            chrome.sheenMask.anchorMin = new Vector2(0.18f, 0.70f);
            chrome.sheenMask.anchorMax = new Vector2(0.82f, 0.90f);
            chrome.sheenMask.offsetMin = Vector2.zero;
            chrome.sheenMask.offsetMax = Vector2.zero;
        }

        if (chrome.sheen != null)
        {
            chrome.sheen.rectTransform.offsetMin = new Vector2(2f, -2f);
            chrome.sheen.rectTransform.offsetMax = new Vector2(-2f, 2f);
        }
    }

    public bool TryHandleScreenPoint(Vector2 screenPoint, Camera eventCamera)
    {
        if (buttonRects == null)
            return false;

        for (int i = 0; i < buttonRects.Length; i++)
        {
            RectTransform buttonRect = buttonRects[i];
            if (buttonRect == null)
                continue;

            if (!RectTransformUtility.RectangleContainsScreenPoint(buttonRect, screenPoint, eventCamera))
                continue;

            OnTap(i);
            return true;
        }

        return false;
    }

    void UpdateButtonStates()
    {
        for (int i = 0; i < buttonChromes.Length; i++)
        {
            K1L0GlassChrome chrome = buttonChromes[i];
            if (activeStates[i])
            {
                chrome.SetAccent(0.18f);
                chrome.border.color = new Color(0.66f, 0.84f, 1f, 0.34f);
                chrome.overlay.color = new Color(0.016f, 0.026f, 0.046f, 0.09f);
                iconImages[i].color = new Color(0.5f, 0.85f, 1f, 1f);
            }
            else
            {
                chrome.SetAccent(0f);
                chrome.border.color = chrome.baseBorder;
                chrome.overlay.color = chrome.baseOverlay;
                iconImages[i].color = new Color(0.87f, 0.92f, 0.98f, 0.90f);
            }
        }
    }

    public void SetActiveButton(int index, bool active)
    {
        activeStates[index] = active;
        UpdateButtonStates();
    }
}
