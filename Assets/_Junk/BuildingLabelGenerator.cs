using UnityEngine;
using Mapbox.BaseModule.Data.Interfaces;
using Mapbox.VectorModule.MeshGeneration.MeshModifiers;
using Mapbox.BaseModule.Data;
using Mapbox.BaseModule.Map;
using Mapbox.BaseModule.Utilities;
using System.Collections.Generic;
using System.Linq;
using TMPro;

/// <summary>
/// MeshModifier that creates individual 3D labels for each building feature
/// during Mapbox's mesh generation process.
/// </summary>
public class BuildingLabelGenerator : MeshModifier, IPolygonMeshModifier
{
    private Transform _labelParent;
    private Dictionary<long, GameObject> _existingLabels = new Dictionary<long, GameObject>();
    
    public BuildingLabelGenerator(Transform labelParent)
    {
        _labelParent = labelParent;
    }

    public override void Run(VectorFeatureUnity feature, MeshData md, IMapInformation mapInfo)
    {
        if (md.Vertices.Count == 0 || feature == null)
            return;

        // Get building height from properties
        float buildingHeightMeters = 10f;
        if (feature.Properties.ContainsKey("height"))
        {
            buildingHeightMeters = System.Convert.ToSingle(feature.Properties["height"]);
        }
        else if (feature.Properties.ContainsKey("render_height"))
        {
            buildingHeightMeters = System.Convert.ToSingle(feature.Properties["render_height"]);
        }

        // Calculate building centroid from vertices
        Vector3 centroid = Vector3.zero;
        foreach (var vertex in md.Vertices)
        {
            centroid += vertex;
        }
        centroid /= md.Vertices.Count;

        // Get unique ID for this building
        long buildingId = feature.Data.Id.GetHashCode();

        // Check if label already exists
        if (_existingLabels.ContainsKey(buildingId))
        {
            return; // Already labeled
        }

        // Create label GameObject
        GameObject labelObj = new GameObject($"Label_Building_{buildingId}");
        labelObj.transform.SetParent(_labelParent, false);
        
        // Position label above building centroid
        Vector3 labelPosition = centroid;
        labelPosition.y += buildingHeightMeters * 0.001f; // Convert to tile space (approx)
        labelObj.transform.localPosition = labelPosition;
        labelObj.transform.localScale = Vector3.one * 0.02f; // World scale

        // Create text child
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(labelObj.transform, false);
        textObj.transform.localPosition = Vector3.zero;

        // Add TextMeshPro component
        TextMeshPro textMesh = textObj.AddComponent<TextMeshPro>();
        textMesh.text = $"{buildingHeightMeters:F1}m";
        textMesh.fontSize = 3;
        textMesh.alignment = TextAlignmentOptions.Center;
        textMesh.color = Color.yellow;
        textMesh.rectTransform.sizeDelta = new Vector2(5f, 2f);
        
        // Add outline
        textMesh.outlineWidth = 0.3f;
        textMesh.outlineColor = Color.black;
        
        // Render on top
        textMesh.renderer.material.renderQueue = 4500;

        // Add billboard component
        var billboard = labelObj.AddComponent<BillboardLabel>();

        // Store reference
        _existingLabels[buildingId] = labelObj;

        Debug.Log($"[BuildingLabelGenerator] Created label for building {buildingId}: {buildingHeightMeters:F1}m at {labelPosition}");
    }
}

/// <summary>
/// Makes the label always face the camera
/// </summary>
public class BillboardLabel : MonoBehaviour
{
    private void LateUpdate()
    {
        if (Camera.main != null)
        {
            Vector3 toCamera = transform.position - Camera.main.transform.position;
            if (toCamera.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(toCamera, Vector3.up);
            }
        }
    }
}
