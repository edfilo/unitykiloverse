using UnityEngine;

[ExecuteInEditMode]
public class NightAtmosphereSetup : MonoBehaviour
{
    void OnEnable()
    {
        Debug.Log("[NightAtmosphereSetup] Setting up night atmosphere...");
        
        // Add DarkSkySetup
        if (GetComponent<DarkSkySetup>() == null)
        {
            var sky = gameObject.AddComponent<DarkSkySetup>();
            Debug.Log("[NightAtmosphereSetup] Added DarkSkySetup");
        }
        
        // Add StreetLampManager
        if (GetComponent<StreetLampManager>() == null)
        {
            var lamps = gameObject.AddComponent<StreetLampManager>();
            Debug.Log("[NightAtmosphereSetup] Added StreetLampManager");
        }
        
        Debug.Log("[NightAtmosphereSetup] Night atmosphere setup complete!");
    }
}
