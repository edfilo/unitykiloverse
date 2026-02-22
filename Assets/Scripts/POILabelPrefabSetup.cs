using UnityEngine;
using TMPro;

/// <summary>
/// Helper script to create a POI label prefab at runtime or in the editor.
/// Can be used as a component on a GameObject or called statically.
/// </summary>
public class POILabelPrefabSetup : MonoBehaviour
{
    [Header("Label Appearance")]
    public float fontSize = 3f;
    public Color textColor = Color.white;
    public Font font;

    [Header("Background")]
    public bool addBackground = true;
    public Color backgroundColor = new Color(0, 0, 0, 0.7f);
    public Vector2 backgroundSize = new Vector2(4f, 1f);

    /// <summary>
    /// Creates a simple POI label prefab programmatically.
    /// Returns a GameObject that can be used as a prefab.
    /// </summary>
    public static GameObject CreatePOILabelPrefab(string prefabName = "POILabel")
    {
        GameObject labelRoot = new GameObject(prefabName);

        // Add TextMeshPro component
        TextMeshPro tmp = labelRoot.AddComponent<TextMeshPro>();
        tmp.text = "POI";
        tmp.fontSize = 3f;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.horizontalAlignment = HorizontalAlignmentOptions.Center;
        tmp.verticalAlignment = VerticalAlignmentOptions.Middle;

        // Set sorting layer to render on top
        tmp.sortingOrder = 100;

        // Add a simple background quad
        GameObject background = GameObject.CreatePrimitive(PrimitiveType.Quad);
        background.name = "Background";
        background.transform.SetParent(labelRoot.transform);
        background.transform.localPosition = new Vector3(0, 0, 0.1f);
        background.transform.localScale = new Vector3(4f, 1f, 1f);

        // Remove collider from background
        var collider = background.GetComponent<Collider>();
        if (collider != null)
        {
            if (Application.isPlaying) Destroy(collider);
            else DestroyImmediate(collider);
        }

        // Set background material
        Renderer bgRenderer = background.GetComponent<Renderer>();
        bgRenderer.material = new Material(Shader.Find("Sprites/Default"));
        bgRenderer.material.color = new Color(0, 0, 0, 0.7f);

        Debug.Log($"[POILabelPrefabSetup] Created POI label prefab: {prefabName}");
        return labelRoot;
    }

    /// <summary>
    /// Editor helper: Creates a prefab when button is clicked in inspector.
    /// </summary>
    [ContextMenu("Create POI Label Prefab")]
    private void CreatePrefab()
    {
        GameObject prefab = CreatePOILabelPrefab("POILabel");

        // Apply customizations
        TextMeshPro tmp = prefab.GetComponent<TextMeshPro>();
        tmp.fontSize = fontSize;
        tmp.color = textColor;

        if (font != null)
        {
            tmp.font = TMP_FontAsset.CreateFontAsset(font);
        }

        if (!addBackground)
        {
            Transform bg = prefab.transform.Find("Background");
            if (bg != null) DestroyImmediate(bg.gameObject);
        }
        else
        {
            Transform bg = prefab.transform.Find("Background");
            if (bg != null)
            {
                bg.localScale = new Vector3(backgroundSize.x, backgroundSize.y, 1f);
                bg.GetComponent<Renderer>().material.color = backgroundColor;
            }
        }

        Debug.Log("[POILabelPrefabSetup] Prefab created and ready to use!");
    }
}
