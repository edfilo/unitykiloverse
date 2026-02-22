using UnityEngine;
using UnityEditor;

public static class SaveBeamAvatarPrefab
{
    [MenuItem("Tools/Save BeamAvatar Prefab")]
    static void SavePrefab()
    {
        GameObject orb = GameObject.Find("BeamAvatar");
        if (orb == null)
        {
            Debug.LogError("BeamAvatar GameObject not found in scene!");
            return;
        }

        string path = "Assets/Prefabs/BeamAvatar.prefab";
        
        // Delete existing prefab if it exists
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
        {
            AssetDatabase.DeleteAsset(path);
        }

        // Create new prefab
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(orb, path);
        
        if (prefab != null)
        {
            Debug.Log($"✓ Created BeamAvatar prefab at {path}");
            
            // Delete from scene
            Object.DestroyImmediate(orb);
            Debug.Log("✓ Removed BeamAvatar from scene");
        }
        else
        {
            Debug.LogError("Failed to create prefab!");
        }
    }
}
