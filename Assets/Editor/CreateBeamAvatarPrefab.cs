using UnityEngine;
using UnityEditor;
using System.IO;

public class CreateBeamAvatarPrefab : EditorWindow
{
    [MenuItem("Tools/Create BeamAvatar Prefab")]
    static void CreateOrb()
    {
        // Create sphere
        GameObject orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        orb.name = "BeamAvatar";
        
        // Set scale
        orb.transform.localScale = Vector3.one * 0.5f;
        
        // Get renderer and create material
        MeshRenderer renderer = orb.GetComponent<MeshRenderer>();
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.name = "BeamAvatarMaterial";
        
        // Set material properties
        mat.SetColor("_BaseColor", new Color(1f, 0.8f, 0.3f, 1f));
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Smoothness", 0.9f);
        
        // Enable emission
        mat.EnableKeyword("_EMISSION");
        Color emissionColor = new Color(1f, 0.8f, 0.3f) * 2f; // HDR color
        mat.SetColor("_EmissionColor", emissionColor);
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        
        renderer.material = mat;
        
        // Add BeamAvatar component
        orb.AddComponent<BeamAvatar>();
        
        // Remove collider (not needed for visual effect)
        SphereCollider collider = orb.GetComponent<SphereCollider>();
        if (collider != null)
        {
            DestroyImmediate(collider);
        }
        
        // Save material
        string materialPath = "Assets/Materials";
        if (!Directory.Exists(materialPath))
        {
            Directory.CreateDirectory(materialPath);
        }
        AssetDatabase.CreateAsset(mat, $"{materialPath}/BeamAvatarMaterial.mat");
        
        // Save prefab
        string prefabPath = "Assets/Prefabs";
        if (!Directory.Exists(prefabPath))
        {
            Directory.CreateDirectory(prefabPath);
        }
        
        string prefabFilePath = $"{prefabPath}/BeamAvatar.prefab";
        PrefabUtility.SaveAsPrefabAsset(orb, prefabFilePath);
        
        // Select the prefab
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(prefabFilePath);
        
        // Clean up scene object
        DestroyImmediate(orb);
        
        AssetDatabase.Refresh();
        
        Debug.Log($"Created beam avatar prefab at: {prefabFilePath}");
        EditorUtility.DisplayDialog("Success", $"Beam avatar prefab created at:\\n{prefabFilePath}\\n\\nAssign it to the VirtualGridSpawner component.", "OK");
    }
}
