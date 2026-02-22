using UnityEngine;
using UnityEditor;
using KiloWorld.Rendering;

public class MaterialTextureSetup : EditorWindow
{
    [MenuItem("Tools/Setup Material Textures")]
    static void SetupTextures()
    {
        // Load MysticalNight profile specifically
        KiloWorldMasterProfile profile = AssetDatabase.LoadAssetAtPath<KiloWorldMasterProfile>("Assets/MysticalNight.asset");
        
        if (profile == null)
        {
            EditorUtility.DisplayDialog("Error", "Failed to load KiloWorldMasterProfile", "OK");
            return;
        }
        
        // Load brick textures
        Texture2D brickColor = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/BuildingTextures/Bricks097_1K-JPG_Color.jpg");
        Texture2D brickNormal = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/BuildingTextures/Bricks097_1K-JPG_NormalGL.jpg");
        Texture2D brickAO = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/BuildingTextures/Bricks097_1K-JPG_AmbientOcclusion.jpg");
        Texture2D brickRoughness = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/BuildingTextures/Bricks097_1K-JPG_Roughness.jpg");
        
        // Apply ONLY texture assignments to wall material (preserve existing tuned settings)
        profile.buildings.zossWallAlbedo = brickColor;
        profile.buildings.zossWallNormal = brickNormal;
        profile.buildings.zossWallOcclusionMap = brickAO;
        profile.buildings.zossWallRoughnessMap = brickRoughness;

        Debug.Log("[MaterialTextureSetup] Applied brick textures to wall material");

        // Load asphalt textures (using asphalt6 variant 1 - good balance of detail)
        Texture2D asphaltAlbedo = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/RoadTextures/textures/asphalt6/1/T_1_Albedo_asphalt6_(G).tga");
        Texture2D asphaltNormal = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/RoadTextures/textures/asphalt6/1/T_1_Normal_asphalt6.png");
        Texture2D asphaltHeight = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/RoadTextures/textures/asphalt6/1/T_1_Displacement_asphalt6.png");

        // Apply ONLY texture assignments to road material (preserve existing tuned settings)
        profile.roads.roadAlbedo = asphaltAlbedo;
        profile.roads.roadNormal = asphaltNormal;
        profile.roads.roadHeightMap = asphaltHeight;

        Debug.Log("[MaterialTextureSetup] Applied asphalt6 textures to road material");

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("Success", "Material textures applied to MysticalNight profile!\n\nWall materials: Brick textures added\nRoad materials: Asphalt6 textures added\n\nAll your existing settings (tiling, smoothness, metallic, emission) have been preserved.\n\nRenderManager will apply these textures automatically when the scene loads.", "OK");
    }
}
