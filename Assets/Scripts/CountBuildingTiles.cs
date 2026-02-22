using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class CountBuildingTiles : MonoBehaviour
{
    [ContextMenu("Count Building Tiles")]
    public void CountTiles()
    {
        var buildingLayer = GameObject.Find("building layer objects");
        if (buildingLayer == null)
        {
            Debug.LogError("building layer objects not found!");
            return;
        }

        HashSet<string> tileIndexes = new HashSet<string>();
        Dictionary<string, int> tileCount = new Dictionary<string, int>();
        Dictionary<string, int> activeTileCount = new Dictionary<string, int>();
        int totalBuildings = buildingLayer.transform.childCount;
        int activeBuildings = 0;

        for (int i = 0; i < buildingLayer.transform.childCount; i++)
        {
            Transform child = buildingLayer.transform.GetChild(i);
            if (child.gameObject.activeInHierarchy)
                activeBuildings++;

            // Extract tile index from name like "Overture_building_T5_123"
            string name = child.name;
            int tIndex = name.IndexOf("_T");
            if (tIndex >= 0)
            {
                int underscoreAfter = name.IndexOf("_", tIndex + 2);
                if (underscoreAfter > 0)
                {
                    string tileNum = name.Substring(tIndex + 2, underscoreAfter - tIndex - 2);
                    tileIndexes.Add(tileNum);
                    
                    if (!tileCount.ContainsKey(tileNum))
                    {
                        tileCount[tileNum] = 0;
                        activeTileCount[tileNum] = 0;
                    }
                    tileCount[tileNum]++;
                    if (child.gameObject.activeInHierarchy)
                        activeTileCount[tileNum]++;
                }
            }
        }

        var sortedTiles = tileIndexes.OrderBy(t => int.Parse(t)).ToList();
        Debug.Log($"[BuildingTiles] Total: {totalBuildings} buildings, {activeBuildings} active");
        Debug.Log($"[BuildingTiles] Unique tiles: {tileIndexes.Count} (T{string.Join(", T", sortedTiles)})");
        
        foreach (var tile in sortedTiles)
        {
            Debug.Log($"[BuildingTiles] T{tile}: {activeTileCount[tile]}/{tileCount[tile]} active");
        }
    }
}
