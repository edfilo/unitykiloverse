#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.Linq;

// DISABLED: TeleportManager now creates UI programmatically from KiloSettings profile
// No longer needs editor-time button assignment

/*
[InitializeOnLoad]
public class SetupTeleportButtons
{
    static SetupTeleportButtons()
    {
        EditorApplication.delayCall += Setup;
    }

    static void Setup()
    {
        // 1. Find or create TeleportManager
        var managerGO = GameObject.Find("TeleportManager");
        if (managerGO == null)
        {
            managerGO = new GameObject("TeleportManager");
        }

        var manager = managerGO.GetComponent<TeleportManager>();
        if (manager == null)
        {
            manager = managerGO.AddComponent<TeleportManager>();
        }

        // 2. Assign Map
        if (manager.map == null)
        {
            manager.map = GameObject.FindObjectOfType<Mapbox.Example.Scripts.Map.KiloverseMapInfo>();
        }

        EditorUtility.SetDirty(manager);
    }
}
*/
#endif
