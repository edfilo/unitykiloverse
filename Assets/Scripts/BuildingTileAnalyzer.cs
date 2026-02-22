using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Kiloverse.Mapbox;

/// <summary>
/// One-time script to analyze which building tiles are loaded
/// </summary>
public class BuildingTileAnalyzer : MonoBehaviour
{
    void Start()
    {
        AnalyzeBuildingTiles();
    }

    void AnalyzeBuildingTiles()
    {
        var buildingLayerRoot = GameObject.Find("building layer objects");
        if (buildingLayerRoot == null)
        {
            Debug.LogWarning("[BuildingTileAnalyzer] 'building layer objects' not found!");
            return;
        }

        HashSet<int> tileIndices = new HashSet<int>();
        int totalBuildings = buildingLayerRoot.transform.childCount;
        
        // Sample first 1000 buildings to extract tile indices
        for (int i = 0; i < Mathf.Min(totalBuildings, 1000); i++)
        {
            var child = buildingLayerRoot.transform.GetChild(i);
            string name = child.name;
            
            // Extract tile index from name like "Overture_building_T3_872_AJ_Palumbo_Center_(DU)"
            int tIndex = name.IndexOf("_T");
            if (tIndex >= 0)
            {
                int underscoreAfter = name.IndexOf("_", tIndex + 2);
                if (underscoreAfter > 0)
                {
                    string tileNum = name.Substring(tIndex + 2, underscoreAfter - tIndex - 2);
                    if (int.TryParse(tileNum, out int tileIdx))
                    {
                        tileIndices.Add(tileIdx);
                    }
                }
            }
        }

        var sortedTiles = tileIndices.OrderBy(t => t).ToList();
        Debug.Log($"[BuildingTileAnalyzer] Found {tileIndices.Count} unique building tiles (sampled {Mathf.Min(totalBuildings, 1000)} of {totalBuildings}): T{string.Join(", T", sortedTiles)}");
        
        // Now we need to map these tile indices back to actual z/x/y coordinates
        // The tile index increments globally, but we need to know which z/x/y they represent
        Debug.Log($"[BuildingTileAnalyzer] NOTE: These are global tile indices (T0, T1, T2...), not z/x/y coordinates.");
        Debug.Log($"[BuildingTileAnalyzer] To see z/x/y coordinates, we need to check OvertureMapManager logs during tile loading.");
        
        // Destroy this GameObject after analysis
        Destroy(gameObject);
    }
}
