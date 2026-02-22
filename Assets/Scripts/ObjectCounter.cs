using UnityEngine;

public class ObjectCounter : MonoBehaviour
{
    void Start()
    {
        CountObjects();
    }

    [ContextMenu("Count Scene Objects")]
    public void CountObjects()
    {
        GameObject[] allObjects = FindObjectsOfType<GameObject>();
        
        int roadSegments = 0;
        int buildings = 0;
        int labels = 0;
        int total = allObjects.Length;
        
        foreach (GameObject go in allObjects)
        {
            string name = go.name;
            if (name.Contains("Overture_segment") || name.Contains("road", System.StringComparison.OrdinalIgnoreCase))
                roadSegments++;
            else if (name.Contains("Overture_building") || name.Contains("building", System.StringComparison.OrdinalIgnoreCase))
                buildings++;
            else if (name.Contains("Label") || name.Contains("POI"))
                labels++;
        }
        
        Debug.Log($"====== SCENE OBJECT COUNT ======");
        Debug.Log($"Road Segments: {roadSegments}");
        Debug.Log($"Buildings: {buildings}");
        Debug.Log($"Labels/POIs: {labels}");
        Debug.Log($"Total GameObjects: {total}");
        Debug.Log($"================================");
    }
}
