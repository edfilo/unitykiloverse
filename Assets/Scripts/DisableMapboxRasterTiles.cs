using UnityEngine;

[ExecuteAlways]
public class DisableMapboxRasterTiles : MonoBehaviour
{
    private void Start()
    {
        // Find and disable StaticApiLayerModuleScript
        var staticImagery = FindObjectOfType<Mapbox.Example.Scripts.ModuleBehaviours.StaticApiLayerModuleScript>();
        if (staticImagery != null)
        {
            staticImagery.enabled = false;
            Debug.Log("[DisableMapboxRasterTiles] Disabled StaticApiLayerModuleScript - raster tiles are now off");
            
            // Also try to hide any existing tile GameObjects
            Transform parent = staticImagery.transform;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name.Contains("tile") || child.name.Contains("Tile"))
                {
                    child.gameObject.SetActive(false);
                }
            }
        }
        else
        {
            Debug.Log("[DisableMapboxRasterTiles] No StaticApiLayerModuleScript found - raster tiles may already be disabled");
        }
    }
}
