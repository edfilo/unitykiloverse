using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;

/// <summary>
/// Creates a URP road material using the RoadTextures asset pack
/// Optimized for 5m-wide SDK3 road lines
/// </summary>
public class CreateRoadMaterialFromAsset : MonoBehaviour
{
    [ContextMenu("Create Road Material")]
    void CreateMaterial()
    {
        // Load textures from asphalt6/1 (cleaner look)
        Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/RoadTextures/textures/asphalt6/1/T_1_Albedo_asphalt6_(G).tga");
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/RoadTextures/textures/asphalt6/1/T_1_Normal_asphalt6.png");
        Texture2D displacement = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/RoadTextures/textures/asphalt6/1/T_1_Displacement_asphalt6.png");

        if (albedo == null)
        {
            Debug.LogError("[CreateRoadMaterial] Could not load albedo texture!");
            return;
        }

        // Create new URP Lit material
        Shader urpLitShader = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLitShader == null)
        {
            Debug.LogError("[CreateRoadMaterial] URP Lit shader not found!");
            return;
        }

        Material roadMat = new Material(urpLitShader);
        roadMat.name = "KiloverseRoads_PBR";

        // Set textures
        roadMat.SetTexture("_BaseMap", albedo);
        if (normal != null) roadMat.SetTexture("_BumpMap", normal);
        
        // Configure for roads
        roadMat.SetFloat("_Smoothness", 0.15f); // Rough asphalt
        roadMat.SetFloat("_Metallic", 0.0f); // No metallic
        
        // Save material
        string savePath = "Assets/KiloverseMapbox/Materials/KiloverseRoads_PBR.mat";
        AssetDatabase.CreateAsset(roadMat, savePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[CreateRoadMaterial] ✓ Created URP road material at: {savePath}");
        Debug.Log($"[CreateRoadMaterial] Textures: Albedo={albedo.name}, Normal={normal?.name ?? "none"}");
        
        // Update the road stack to use this material
        UpdateRoadStack(savePath);
        
        // Self-destruct
        DestroyImmediate(this);
    }

    void UpdateRoadStack(string materialPath)
    {
        // Load the road stack
        var roadStack = AssetDatabase.LoadAssetAtPath("Assets/KiloverseMapbox/ModifierStacks/KiloverseRoadStack.asset",
            System.Type.GetType("Mapbox.VectorModule.MeshGeneration.MeshModifiers.ModifierStackObject, MapboxVectorModule"));

        if (roadStack == null)
        {
            Debug.LogWarning("[CreateRoadMaterial] Could not find KiloverseRoadStack.asset");
            return;
        }

        // Load the new material
        Material newMat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (newMat == null)
        {
            Debug.LogWarning($"[CreateRoadMaterial] Could not load material: {materialPath}");
            return;
        }

        SerializedObject so = new SerializedObject(roadStack);
        
        // Find the MaterialModifierObject in GoModifiers
        SerializedProperty goModifiers = so.FindProperty("GoModifiers");
        if (goModifiers != null && goModifiers.isArray && goModifiers.arraySize > 0)
        {
            // Get first GoModifier (should be MaterialModifierObject)
            SerializedProperty materialModifier = goModifiers.GetArrayElementAtIndex(0);
            SerializedProperty materialsArray = materialModifier.FindPropertyRelative("Materials");
            
            if (materialsArray != null && materialsArray.isArray)
            {
                materialsArray.arraySize = 1;
                materialsArray.GetArrayElementAtIndex(0).objectReferenceValue = newMat;
                
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(roadStack as UnityEngine.Object);
                
                Debug.Log("[CreateRoadMaterial] ✓ Updated KiloverseRoadStack to use new PBR material!");
            }
        }
    }

    void Start()
    {
        // Auto-run
        CreateMaterial();
    }
}
#endif
