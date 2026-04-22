using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class K1L0Dock : MonoBehaviour
{
    public System.Action<int> OnButtonTapped;

    private Image[] iconImages;
    private Image[] bgImages;
    private RectTransform[] buttonRects;
    private bool[] activeStates = new bool[3];

    // Maps visual button index → panel index (skipping removed status panel)
    private static readonly int[] panelMap = { 0, 2 };

    public static void ClearCachedMaterials()
    {
        K1L0GlassFactory.ClearCachedMaterials();
    }

    public void Initialize(RectTransform parent, TMP_FontAsset monoFont)
    {
        ClearCachedMaterials();
        RectTransform rt = gameObject.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // Add a Canvas override so dock renders in front of teasers
        var overrideCanvas = gameObject.AddComponent<Canvas>();
        overrideCanvas.overrideSorting = true;
        overrideCanvas.sortingOrder = 1100; // Above HUD (1000)
        gameObject.AddComponent<GraphicRaycaster>();

        Sprite[] icons = {
            CreateLocationPinSprite(64),
            CreatePersonSprite(64)
        };

        Sprite circleSpr = CreateCircleSprite(128);

        iconImages = new Image[2];
        bgImages = new Image[2];
        buttonRects = new RectTransform[2];

        for (int i = 0; i < 2; i++)
        {
            int idx = i;

            GameObject btnGO = new GameObject($"CornerBtn{i}");
            btnGO.transform.SetParent(rt, false);
            RectTransform btnRT = btnGO.AddComponent<RectTransform>();
            btnRT.sizeDelta = new Vector2(72, 72);
            buttonRects[i] = btnRT;

            // Position: left corner or right corner
            if (i == 0)
            {
                btnRT.anchorMin = new Vector2(0, 0);
                btnRT.anchorMax = new Vector2(0, 0);
                btnRT.pivot = new Vector2(0, 0);
                btnRT.anchoredPosition = new Vector2(20, 0);
            }
            else
            {
                btnRT.anchorMin = new Vector2(1, 0);
                btnRT.anchorMax = new Vector2(1, 0);
                btnRT.pivot = new Vector2(1, 0);
                btnRT.anchoredPosition = new Vector2(-20, 0);
            }

            // Round background — opaque black
            Image bg = btnGO.AddComponent<Image>();
            bg.sprite = circleSpr;
            bg.type = Image.Type.Simple;
            bg.color = new Color(0f, 0f, 0f, 1f);
            bg.raycastTarget = true;
            bgImages[i] = bg;

            Button btn = btnGO.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => OnTap(panelMap[idx]));

            // Icon
            GameObject iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(btnGO.transform, false);
            RectTransform iconRT = iconGO.AddComponent<RectTransform>();
            iconRT.anchorMin = new Vector2(0.5f, 0.5f);
            iconRT.anchorMax = new Vector2(0.5f, 0.5f);
            iconRT.sizeDelta = new Vector2(34, 34);
            Image iconImg = iconGO.AddComponent<Image>();
            iconImg.sprite = icons[i];
            iconImg.color = new Color(0.84f, 0.90f, 0.97f, 1f);
            iconImg.raycastTarget = false;
            iconImages[i] = iconImg;
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
        OnButtonTapped?.Invoke(panelIndex);
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

            OnTap(panelMap[i]);
            return true;
        }

        return false;
    }

    void UpdateButtonStates()
    {
        for (int i = 0; i < iconImages.Length; i++)
        {
            bool active = activeStates[panelMap[i]];
            bgImages[i].color = new Color(0f, 0f, 0f, 1f); // Always opaque black
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
