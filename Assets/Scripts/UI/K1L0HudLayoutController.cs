using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Single owner for live HUD layout slots. HUD producers register their
/// RectTransforms here instead of manually guessing vertical positions.
/// </summary>
public class K1L0HudLayoutController : MonoBehaviour
{
    private class Slot : MonoBehaviour
    {
        public int order;
    }

    private static K1L0HudLayoutController instance;
    private RectTransform topStack;
    private RectTransform actionStack;
    private readonly List<Slot> topSlots = new List<Slot>();
    private readonly List<Slot> actionSlots = new List<Slot>();

    public static RectTransform TopStack => EnsureExists().topStack;
    public static RectTransform ActionStack => EnsureExists().actionStack;

    public static bool IsManaged(RectTransform rect)
    {
        return rect != null && rect.GetComponent<Slot>() != null;
    }

    public static void RegisterTopElement(RectTransform rect, string name, int order, float preferredHeight, float minHeight = -1f)
    {
        if (rect == null) return;
        var controller = EnsureExists();
        controller.Register(rect, name, order, preferredHeight, minHeight, controller.topStack, controller.topSlots);
    }

    public static void RegisterActionElement(RectTransform rect, string name, int order, float preferredHeight, float minHeight = -1f)
    {
        if (rect == null) return;
        var controller = EnsureExists();
        controller.Register(rect, name, order, preferredHeight, minHeight, controller.actionStack, controller.actionSlots);
    }

    public static void Refresh()
    {
        EnsureExists().RefreshLayout();
    }

    public static void SetMapHudVisible(bool visible)
    {
        var controller = EnsureExists();
        if (controller.topStack != null) controller.topStack.gameObject.SetActive(visible);
        if (controller.actionStack != null) controller.actionStack.gameObject.SetActive(visible);
    }

    private static K1L0HudLayoutController EnsureExists()
    {
        if (instance != null) return instance;
        var go = new GameObject("K1L0HudLayoutController");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<K1L0HudLayoutController>();
        instance.Build();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        if (topStack == null || actionStack == null) Build();
    }

    private void Build()
    {
        if (topStack != null && actionStack != null) return;

        if (topStack == null)
            topStack = CreateStack("K1L0TopHudStack", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 360f), new RectOffset(12, 12, 6, 0), 2f, TextAnchor.UpperLeft);

        if (actionStack == null)
            actionStack = CreateStack("K1L0ActionHudStack", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 86f), new Vector2(0f, 96f), new RectOffset(12, 12, 0, 0), 4f, TextAnchor.LowerCenter);
    }

    private RectTransform CreateStack(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta, RectOffset padding, float spacing, TextAnchor alignment)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(K1L0CanvasRoot.HUD, false);
        var stack = go.GetComponent<RectTransform>();
        stack.anchorMin = anchorMin;
        stack.anchorMax = anchorMax;
        stack.pivot = pivot;
        stack.anchoredPosition = anchoredPosition;
        stack.sizeDelta = sizeDelta;

        var layout = go.AddComponent<VerticalLayoutGroup>();
        layout.padding = padding;
        layout.spacing = spacing;
        layout.childAlignment = alignment;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var fitter = go.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return stack;
    }

    private void Register(RectTransform rect, string name, int order, float preferredHeight, float minHeight, RectTransform parentStack, List<Slot> slotList)
    {
        rect.SetParent(parentStack, false);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = Vector2.zero;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        if (!string.IsNullOrWhiteSpace(name))
            rect.gameObject.name = name;

        var layout = rect.GetComponent<LayoutElement>();
        if (layout == null) layout = rect.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = minHeight >= 0f ? minHeight : preferredHeight;
        layout.preferredHeight = preferredHeight;
        layout.flexibleHeight = 0f;
        layout.flexibleWidth = 1f;

        var slot = rect.GetComponent<Slot>();
        if (slot == null) slot = rect.gameObject.AddComponent<Slot>();
        slot.order = order;

        if (!slotList.Contains(slot)) slotList.Add(slot);
        SortSlots(slotList);
        RefreshLayout();
    }

    private void SortSlots(List<Slot> slotList)
    {
        slotList.RemoveAll(s => s == null);
        slotList.Sort((a, b) => a.order.CompareTo(b.order));
        for (int i = 0; i < slotList.Count; i++)
            slotList[i].transform.SetSiblingIndex(i);
    }

    private void RefreshLayout()
    {
        if (topStack != null)
        {
            SortSlots(topSlots);
            LayoutRebuilder.ForceRebuildLayoutImmediate(topStack);
        }
        if (actionStack != null)
        {
            SortSlots(actionSlots);
            LayoutRebuilder.ForceRebuildLayoutImmediate(actionStack);
        }
    }
}
