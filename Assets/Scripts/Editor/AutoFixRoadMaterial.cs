using UnityEngine;
using UnityEditor;

[InitializeOnLoad]
public class AutoFixRoadMaterial
{
    static AutoFixRoadMaterial()
    {
        EditorApplication.delayCall += Fix;
    }

    static void Fix()
    {
        var roadStack = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/KiloverseMapbox/ModifierStacks/KiloverseRoadStack.asset");
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/KiloverseMapbox/Materials/KiloverseRoads_PBR.mat");
        
        if (roadStack == null || material == null)
        {
            Debug.LogWarning($"[AutoFixRoadMaterial] roadStack={roadStack != null}, material={material != null}");
            return;
        }

        var so = new SerializedObject(roadStack);
        var goModifiers = so.FindProperty("GoModifiers");
        
        if (goModifiers != null && goModifiers.arraySize > 0)
        {
            var matMod = goModifiers.GetArrayElementAtIndex(0);
            var materials = matMod.FindPropertyRelative("Materials");
            
            if (materials != null && materials.arraySize > 0)
            {
                var currentRef = materials.GetArrayElementAtIndex(0).objectReferenceValue;
                
                if (currentRef == null)
                {
                    materials.GetArrayElementAtIndex(0).objectReferenceValue = material;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(roadStack);
                    AssetDatabase.SaveAssets();
                    
                    Debug.Log("[AutoFixRoadMaterial] ✓✓ FIXED! Material assigned. Refresh Inspector.");
                }
                else
                {
                    Debug.Log($"[AutoFixRoadMaterial] Material already assigned: {currentRef.name}");
                }
            }
        }
    }
}
