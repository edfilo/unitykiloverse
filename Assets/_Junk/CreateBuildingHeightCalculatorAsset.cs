using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor script to create BuildingHeightCalculator asset.
/// Run via: Tools -> Create Building Height Calculator Asset
/// </summary>
public class CreateBuildingHeightCalculatorAsset
{
    [MenuItem("Tools/Create Building Height Calculator Asset")]
    public static void CreateAsset()
    {
        // Create instance
        var asset = ScriptableObject.CreateInstance<BuildingHeightCalculatorObject>();

        // Configure default values
        asset.metersPerFloor = 4f;
        asset.minimumHeight = 8f;

        // Save to Assets folder
        string path = "Assets/BuildingHeightCalculator.asset";
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Select it in project
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = asset;

        Debug.Log($"[CreateBuildingHeightCalculatorAsset] Created asset at: {path}");
    }
}
