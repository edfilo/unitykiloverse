using UnityEditor;
using UnityEngine;

public class ForceReimportPOIStack
{
    [MenuItem("KiloWorld/Force Reimport POI Stack")]
    public static void ForceReimport()
    {
        string path = "Assets/KiloverseMapbox/Modifiers/POIStack.asset";
        
        // Force Unity to reload from disk
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        AssetDatabase.Refresh();
        
        Debug.Log("[ForceReimport] Reimported POIStack from disk - should now show LayerType: Point");
    }
}