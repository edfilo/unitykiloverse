using UnityEditor;
using Mapbox.VectorModule.MeshGeneration.Unity;

public class FixPOIStack
{
    [MenuItem("KiloWorld/Fix POI Stack Layer Type")]
[MenuItem("KiloWorld/Fix POI Stack Layer Type")]
    public static void FixLayerType()
    {
        var poiStack = AssetDatabase.LoadAssetAtPath<ModifierStackObject>("Assets/KiloverseMapbox/Modifiers/POIStack.asset");
        if (poiStack != null)
        {
            poiStack.Settings.LayerType = 0; // 0 = Point
            EditorUtility.SetDirty(poiStack);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            UnityEngine.Debug.Log("[FixPOIStack] Set LayerType to Point (0) and saved");
        }
        else
        {
            UnityEngine.Debug.LogError("[FixPOIStack] Could not find POIStack.asset");
        }
    }
}