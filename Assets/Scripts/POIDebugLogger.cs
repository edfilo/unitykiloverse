using UnityEngine;

public class POIDebugLogger : MonoBehaviour
{
    private void Awake()
    {
        Debug.Log($"[POIDebugLogger] Awake - GameObject: {gameObject.name}, Position: {transform.position}, LocalPos: {transform.localPosition}");
    }

    private void OnEnable()
    {
        Debug.Log($"[POIDebugLogger] OnEnable - GameObject: {gameObject.name}, Position: {transform.position}, LocalPos: {transform.localPosition}, Parent: {(transform.parent ? transform.parent.name : "null")}");
    }

    private void Start()
    {
        Debug.Log($"[POIDebugLogger] Start - GameObject: {gameObject.name}, World Position: {transform.position}");

        // Log all parent transforms up the hierarchy
        Transform current = transform.parent;
        int level = 0;
        while (current != null)
        {
            Debug.Log($"[POIDebugLogger]   Parent[{level}]: {current.name} at {current.position}");
            current = current.parent;
            level++;
        }
    }
}
