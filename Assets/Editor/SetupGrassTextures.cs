using UnityEngine;
using UnityEditor;
using KiloWorld.Rendering;

public class SetupGrassTextures : EditorWindow
{
    [MenuItem("Tools/Setup Grass Ground Textures")]
    static void SetupGrass()
    {
        // Load MysticalNight profile
        KiloWorldMasterProfile profile = AssetDatabase.LoadAssetAtPath<KiloWorldMasterProfile>("Assets/MysticalNight.asset");

        if (profile == null)
        {
            EditorUtility.DisplayDialog("Error", "MysticalNight profile not found!", "OK");
            return;
        }

        // Load grass textures
        Texture2D grassColor = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/BuildingTextures/Grass001_1K-JPG_Color.jpg");
        Texture2D grassNormal = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/BuildingTextures/Grass001_1K-JPG_NormalGL.jpg");
        Texture2D grassAO = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/BuildingTextures/Grass001_1K-JPG_AmbientOcclusion.jpg");

        if (grassColor == null || grassNormal == null)
        {
            EditorUtility.DisplayDialog("Error", "Grass textures not found in Assets/BuildingTextures/", "OK");
            return;
        }

        // Apply to ground material settings
        profile.ground.groundTexture = grassColor;
        profile.ground.groundNormal = grassNormal;

        // Set tiling for ground (grass repeats more than bricks/roads)
        profile.ground.groundTiling = new Vector2(50f, 50f);

        Debug.Log("[SetupGrassTextures] Applied grass textures to ground material");

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("Success", "Grass textures applied to ground!\n\nGround tiling set to 50x50 for natural grass repetition.", "OK");
    }
}
