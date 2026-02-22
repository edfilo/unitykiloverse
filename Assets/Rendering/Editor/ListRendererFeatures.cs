using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering.Universal;
using System.Linq;
using System;

public class ListRendererFeatures : EditorWindow
{
    [MenuItem("KiloWorld/Debug/List Available Renderer Features")]
    public static void ListFeatures()
    {
        var featureTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.IsSubclassOf(typeof(ScriptableRendererFeature)) && !type.IsAbstract)
            .ToList();

        Debug.Log($"Found {featureTypes.Count} Renderer Features:");
        foreach (var type in featureTypes)
        {
            Debug.Log($"- {type.Name} ({type.FullName})");
        }
    }
}
