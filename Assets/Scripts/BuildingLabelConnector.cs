using UnityEngine;

/// <summary>
/// Automatically calculates building height and registers a UI label.
/// Attach this to your building prefabs or add it via a Mapbox Modifier.
/// </summary>
public class BuildingLabelConnector : MonoBehaviour
{
    [Tooltip("Height offset above the building top")]
    public float verticalOffset = 2.0f;

    private GameObject labelAnchor;

    private void Start()
    {
        // Debug.Log($"[BuildingLabelConnector] Starting on {gameObject.name}");

        // 1. Calculate the top of the building
        float height = 0f;
        float topY = transform.position.y;

        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds combinedBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                combinedBounds.Encapsulate(renderers[i].bounds);
            }
            height = combinedBounds.size.y;
            topY = combinedBounds.max.y;
        }
        else
        {
            // Fallback to collider if no renderer
            Collider c = GetComponent<Collider>();
            if (c != null)
            {
                height = c.bounds.size.y;
                topY = c.bounds.max.y;
            }
        }

        // 2. Create an anchor point for the label
        labelAnchor = new GameObject("LabelAnchor");
        labelAnchor.transform.SetParent(transform);
        labelAnchor.transform.position = new Vector3(transform.position.x, topY + verticalOffset, transform.position.z);

        // 3. Register with the LabelManager
        string labelText = $"{height:F1}m";
        if (LabelManager.Instance != null)
        {
            LabelManager.Instance.Register(labelAnchor.transform, labelText, 1f, null);
            // Debug.Log($"[BuildingLabelConnector] Registered label for {gameObject.name} with height {labelText}");
        }
        else
        {
            Debug.LogError("[BuildingLabelConnector] LabelManager.Instance is null! Is LabelManager in the scene?");
        }
    }

    private void OnDisable()
    {
        if (labelAnchor != null)
        {
            LabelManager.Instance?.Unregister(labelAnchor.transform);
        }
    }
}
