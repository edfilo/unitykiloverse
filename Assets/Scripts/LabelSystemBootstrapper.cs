using UnityEngine;

public class LabelSystemBootstrapper : MonoBehaviour
{
    void Start()
    {
        // 1. Run the Setup (Creates Prefab & LabelManager)
        GameObject tempSetup = new GameObject("TempSetup");
        tempSetup.AddComponent<RuntimeLabelSetup>().Setup();
        Destroy(tempSetup);

        // 2. Ensure AutoAttacher exists (Finds buildings and adds connectors)
        if (FindObjectOfType<BuildingLabelAutoAttacher>() == null)
        {
            GameObject attacher = new GameObject("LabelAttacher");
            attacher.AddComponent<BuildingLabelAutoAttacher>();
            DontDestroyOnLoad(attacher); // Keep it alive across scenes if needed
            Debug.Log("[LabelSystemBootstrapper] System initialized.");
        }
    }
}
