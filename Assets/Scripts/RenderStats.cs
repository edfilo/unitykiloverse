using UnityEngine;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class RenderStats : MonoBehaviour
{
    [Header("Stats")]
    public int totalRenderers;
    public int activeRenderers;
    public int visibleRenderers;
    public int totalVertices;
    public int totalTriangles;
    public int drawCalls;

    [Header("By Type")]
    public int buildingRenderers;
    public int roadRenderers;
    public int activeBuildingRenderers;
    public int activeRoadRenderers;

    [Header("Auto Update")]
    public bool updateEverySecond = true;
    private float lastUpdateTime;

    void Update()
    {
        if (updateEverySecond && Time.time - lastUpdateTime > 1f)
        {
            UpdateStats();
            lastUpdateTime = Time.time;
        }
    }

    [ContextMenu("Update Stats Now")]
    public void UpdateStats()
    {
        var allRenderers = FindObjectsOfType<MeshRenderer>(true); // Include inactive
        var allFilters = FindObjectsOfType<MeshFilter>(true);

        totalRenderers = allRenderers.Length;
        activeRenderers = allRenderers.Count(r => r.gameObject.activeInHierarchy);

        // Count only enabled renderers for visibility check
        var enabledRenderers = allRenderers.Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
        visibleRenderers = enabledRenderers.Count(r => r.isVisible);

        // Count by type
        buildingRenderers = allRenderers.Count(r => r.name.ToLower().Contains("building"));
        roadRenderers = allRenderers.Count(r => r.name.ToLower().Contains("road"));
        activeBuildingRenderers = allRenderers.Count(r => r.name.ToLower().Contains("building") && r.gameObject.activeInHierarchy && r.enabled);
        activeRoadRenderers = allRenderers.Count(r => r.name.ToLower().Contains("road") && r.gameObject.activeInHierarchy && r.enabled);

        // Count vertices and triangles (only active objects)
        long verts = 0;
        long tris = 0;
        foreach (var filter in allFilters)
        {
            if (filter != null && filter.gameObject.activeInHierarchy && filter.sharedMesh != null)
            {
                verts += filter.sharedMesh.vertexCount;
                tris += filter.sharedMesh.triangles.Length / 3;
            }
        }
        totalVertices = (int)verts;
        totalTriangles = (int)tris;

        #if UNITY_EDITOR
        // Get draw calls from stats
        drawCalls = UnityEditor.UnityStats.drawCalls;
        #endif

        Debug.Log($"<color=cyan>========== RENDER STATISTICS ==========</color>");
        Debug.Log($"Total Renderers in Scene: {totalRenderers:N0}");
        Debug.Log($"Active (enabled hierarchy): {activeRenderers:N0}");
        Debug.Log($"Visible (in camera frustum): {visibleRenderers:N0}");
        Debug.Log($"Draw Calls: {drawCalls:N0}");
        Debug.Log($"\n<color=yellow>Buildings:</color> {buildingRenderers:N0} total, {activeBuildingRenderers:N0} active");
        Debug.Log($"<color=yellow>Roads:</color> {roadRenderers:N0} total, {activeRoadRenderers:N0} active");
        Debug.Log($"\n<color=yellow>Mesh Data (active only):</color>");
        Debug.Log($"  Vertices: {totalVertices:N0}");
        Debug.Log($"  Triangles: {totalTriangles:N0}");
        Debug.Log($"<color=cyan>=======================================</color>");
    }

    void OnGUI()
    {
        if (!Application.isPlaying) return;

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 18;
        style.normal.textColor = Color.white;
        style.alignment = TextAnchor.UpperLeft;

        float x = 10;
        float y = 10;
        float lineHeight = 22;

        GUI.Label(new Rect(x, y, 400, lineHeight), $"Total Renderers: {totalRenderers:N0}", style);
        y += lineHeight;
        GUI.Label(new Rect(x, y, 400, lineHeight), $"Active: {activeRenderers:N0}", style);
        y += lineHeight;

        style.normal.textColor = Color.green;
        GUI.Label(new Rect(x, y, 400, lineHeight), $"VISIBLE: {visibleRenderers:N0}", style);
        y += lineHeight;

        style.normal.textColor = Color.yellow;
        GUI.Label(new Rect(x, y, 400, lineHeight), $"Draw Calls: {drawCalls:N0}", style);
        y += lineHeight;

        style.normal.textColor = Color.cyan;
        GUI.Label(new Rect(x, y, 400, lineHeight), $"Buildings: {activeBuildingRenderers:N0}/{buildingRenderers:N0}", style);
        y += lineHeight;
        GUI.Label(new Rect(x, y, 400, lineHeight), $"Roads: {activeRoadRenderers:N0}/{roadRenderers:N0}", style);
    }
}
