using System.ComponentModel;
using Mapbox.BaseModule.Unity;
using Mapbox.VectorModule.MeshGeneration.Unity;
using Mapbox.VectorModule.MeshGeneration.MeshModifiers;
using UnityEngine;

[DisplayName("Building Label Generator")]
[CreateAssetMenu(menuName = "Mapbox/Modifiers/Building Label Generator")]
public class BuildingLabelGeneratorObject : ScriptableMeshModifierObject, IPolygonMeshModifier
{
    private void OnEnable()
    {
        Debug.Log("[BuildingLabelGeneratorObject] OnEnable called");
    }

    [Tooltip("Parent transform to hold all generated labels")]
    public Transform LabelParent;

    private BuildingLabelGenerator _labelGeneratorImplementation;
    protected override MeshModifier _meshModifierImplementation => _labelGeneratorImplementation;

    public override void ConstructModifier(UnityContext unityContext)
    {
        Debug.Log("[BuildingLabelGeneratorObject] ConstructModifier START");
        
        // Find or create label parent
        if (LabelParent == null)
        {
            Debug.Log("[BuildingLabelGeneratorObject] Creating BuildingLabels parent");
            GameObject parentObj = GameObject.Find("BuildingLabels");
            if (parentObj == null)
            {
                parentObj = new GameObject("BuildingLabels");
                Debug.Log("[BuildingLabelGeneratorObject] Created new BuildingLabels GameObject");
            }
            else
            {
                Debug.Log("[BuildingLabelGeneratorObject] Found existing BuildingLabels");
            }
            LabelParent = parentObj.transform;
        }

        _labelGeneratorImplementation = new BuildingLabelGenerator(LabelParent);
        Debug.Log("[BuildingLabelGeneratorObject] Constructed label generator modifier");
    }
}
