using UnityEngine;
using System.Collections.Generic;

public class StreetLampManager : MonoBehaviour
{
    [Header("Street Lamp Settings")]
    [SerializeField] private Color lampColor = new Color(1f, 0.9f, 0.7f); // Warm street light
    [SerializeField] private float lampIntensity = 3f;
    [SerializeField] private float lampRange = 15f;
    [SerializeField] private float lampHeight = 5f;
    [SerializeField] private float spacing = 20f; // Distance between lamps

    [Header("Performance")]
    [SerializeField] private int maxLamps = 50;
    [SerializeField] private bool castShadows = false;

    private List<Light> activeLamps = new List<Light>();
    private Transform player;

    void Start()
    {
        player = GameObject.Find("Player")?.transform;
        InvokeRepeating("UpdateStreetLamps", 3f, 5f);
    }

    void UpdateStreetLamps()
    {
        if (player == null) return;

        // Find roads/streets in the area
        GameObject kiloMap = GameObject.Find("KiloMap");
        if (kiloMap == null) return;

        // Create lamps along a grid pattern near player
        Vector3 playerPos = player.position;
        
        // Clear old lamps that are too far
        for (int i = activeLamps.Count - 1; i >= 0; i--)
        {
            if (activeLamps[i] == null || 
                Vector3.Distance(activeLamps[i].transform.position, playerPos) > lampRange * 3)
            {
                if (activeLamps[i] != null) Destroy(activeLamps[i].gameObject);
                activeLamps.RemoveAt(i);
            }
        }

        // Add new lamps if needed
        if (activeLamps.Count < maxLamps)
        {
            PlaceLampsNearPlayer(playerPos);
        }
    }

    void PlaceLampsNearPlayer(Vector3 center)
    {
        int gridSize = 3; // 3x3 grid around player
        
        for (int x = -gridSize; x <= gridSize; x++)
        {
            for (int z = -gridSize; z <= gridSize; z++)
            {
                if (activeLamps.Count >= maxLamps) return;

                Vector3 lampPos = center + new Vector3(x * spacing, lampHeight, z * spacing);
                
                // Check if lamp already exists nearby
                bool exists = false;
                foreach (Light lamp in activeLamps)
                {
                    if (lamp != null && Vector3.Distance(lamp.transform.position, lampPos) < spacing * 0.5f)
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    CreateStreetLamp(lampPos);
                }
            }
        }
    }

    void CreateStreetLamp(Vector3 position)
    {
        GameObject lampObj = new GameObject("StreetLamp");
        lampObj.transform.position = position;
        lampObj.transform.SetParent(transform);

        Light light = lampObj.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = lampColor;
        light.intensity = lampIntensity;
        light.range = lampRange;
        light.shadows = castShadows ? LightShadows.Soft : LightShadows.None;
        
        // Add slight random variation
        light.intensity += Random.Range(-0.2f, 0.2f);

        activeLamps.Add(light);
    }

    void OnDestroy()
    {
        // Clean up all lamps
        foreach (Light lamp in activeLamps)
        {
            if (lamp != null) Destroy(lamp.gameObject);
        }
        activeLamps.Clear();
    }

    // Public method to adjust lamp settings at runtime
    public void SetLampProperties(Color color, float intensity, float range)
    {
        lampColor = color;
        lampIntensity = intensity;
        lampRange = range;

        foreach (Light lamp in activeLamps)
        {
            if (lamp != null)
            {
                lamp.color = color;
                lamp.intensity = intensity;
                lamp.range = range;
            }
        }
    }
}
