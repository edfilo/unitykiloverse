using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kiloverse.Mapbox;

public class CheckTallestBuildings
{
    private struct BuildingEntry
    {
        public string Name;
        public float RenderedHeightMeters;
        public float OvertureHeightMeters;
        public int NumFloors;
        public Vector3 WorldPosition;
        public GameObject GameObject;
        public string NearestPOI;
    }

    [MenuItem("Debug/List Top 10 Tallest Buildings (with POI clues)")]
    public static void ListTallest()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[CheckTallestBuildings] Must be in Play mode - buildings are loaded at runtime. Start the game first.");
            return;
        }

        var buildingLayerRoot = GameObject.Find("building layer objects");
        if (buildingLayerRoot == null)
        {
            Debug.LogWarning("[CheckTallestBuildings] 'building layer objects' not found. Has the map loaded yet?");
        }

        var entries = new List<BuildingEntry>();

        // 1. From BuildingMetadata (has building names from Overture)
        foreach (var bm in Object.FindObjectsOfType<BuildingMetadata>())
        {
            if (bm == null || !bm.gameObject.activeInHierarchy) continue;
            var mr = bm.GetComponent<MeshRenderer>();
            float height = mr != null && mr.enabled ? mr.bounds.size.y : bm.worldHeightMeters;
            if (height < 3f || height > 500f) continue; // Skip tiny or tile-sized objects
            entries.Add(new BuildingEntry
            {
                Name = string.IsNullOrEmpty(bm.buildingName) ? "(unnamed)" : bm.buildingName,
                RenderedHeightMeters = height,
                OvertureHeightMeters = bm.heightMeters,
                NumFloors = bm.numFloors,
                WorldPosition = mr != null ? mr.bounds.center : bm.transform.position,
                GameObject = bm.gameObject,
                NearestPOI = null
            });
        }

        // 2. Fallback: scan building layer mesh renderers (catch any without BuildingMetadata)
        if (buildingLayerRoot != null)
        {
            foreach (Transform t in buildingLayerRoot.GetComponentsInChildren<Transform>())
            {
                var go = t.gameObject;
                if (go.GetComponent<BuildingMetadata>() != null) continue; // Already counted
                var mr = go.GetComponent<MeshRenderer>();
                if (mr == null || !mr.enabled) continue;
                if (!go.name.Contains("building") && !go.name.Contains("Building")) continue;
                if (go.name.Contains("Tile") || go.name.Contains("Container")) continue;
                float height = mr.bounds.size.y;
                if (height < 3f || height > 500f) continue;
                entries.Add(new BuildingEntry
                {
                    Name = "(no metadata)",
                    RenderedHeightMeters = height,
                    OvertureHeightMeters = 0,
                    NumFloors = 0,
                    WorldPosition = mr.bounds.center,
                    GameObject = go,
                    NearestPOI = null
                });
            }
        }

        if (entries.Count == 0)
        {
            Debug.LogWarning("[CheckTallestBuildings] No buildings found. Ensure you're in Play mode and the map has loaded.");
            return;
        }

        // Deduplicate by GameObject
        entries = entries.GroupBy(e => e.GameObject).Select(g => g.First()).ToList();

        // Collect POIs for proximity matching
        var pois = new List<(string displayName, Vector3 pos)>();
        var poiLayer = GameObject.Find("poi_label layer objects");
        if (poiLayer == null) poiLayer = GameObject.Find("POI_Anchors");
        if (poiLayer != null)
        {
            foreach (Transform t in poiLayer.GetComponentsInChildren<Transform>())
            {
                var go = t.gameObject;
                if (!go.activeInHierarchy) continue;
                string text = GetPOIDisplayName(go);
                if (!string.IsNullOrEmpty(text)) pois.Add((text, go.transform.position));
            }
        }
        foreach (var go in Object.FindObjectsOfType<GameObject>().Where(g => (g.name.Contains("POI") || g.name.Contains("poi_label")) && g.activeInHierarchy))
        {
            string text = GetPOIDisplayName(go);
            if (!string.IsNullOrEmpty(text)) pois.Add((text, go.transform.position));
        }

        // Assign nearest POI to each building (within 150m)
        const float poiRadius = 150f;
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            string nearest = null;
            float nearestDist = poiRadius;
            foreach (var poi in pois)
            {
                float d = Vector3.Distance(e.WorldPosition, poi.pos);
                if (d < nearestDist) { nearestDist = d; nearest = $"{poi.displayName} ({d:F0}m)"; }
            }
            e.NearestPOI = nearest;
            entries[i] = e;
        }

        var sorted = entries.OrderByDescending(e => e.RenderedHeightMeters).Take(10).ToList();

        Debug.Log($"=== TOP 10 TALLEST BUILDINGS (of {entries.Count} total) ===");
        for (int i = 0; i < sorted.Count; i++)
        {
            var e = sorted[i];
            string poiClue = string.IsNullOrEmpty(e.NearestPOI) ? "—" : e.NearestPOI;
            string overtureStr = e.OvertureHeightMeters > 0 ? $" Overture: {e.OvertureHeightMeters:F1}m" : (e.NumFloors > 0 ? $" ~{e.NumFloors * 4}m from floors" : "");
            Debug.Log($"{i + 1}. {e.Name} | Rendered: {e.RenderedHeightMeters:F1}m{overtureStr} | Floors: {e.NumFloors} | Near: {poiClue}");
            Debug.Log($"   GameObject: {e.GameObject.name} | Pos: ({e.WorldPosition.x:F0}, {e.WorldPosition.y:F0}, {e.WorldPosition.z:F0})");
        }
    }

    [MenuItem("Debug/Write Tallest Buildings to File")]
    public static void WriteTallestToFile()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[CheckTallestBuildings] Must be in Play mode.");
            return;
        }
        var entries = CollectTallestEntries(15);
        if (entries.Count == 0)
        {
            Debug.LogWarning("[CheckTallestBuildings] No buildings found.");
            return;
        }
        var path = Path.Combine(Application.dataPath, "..", "tallest_buildings.json");
        path = Path.GetFullPath(path);
        var lines = new List<string>();
        lines.Add("[");
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            var sb = new System.Text.StringBuilder();
            sb.Append($"  {{\"rank\":{i + 1},\"name\":\"{EscapeJson(e.Name)}\",\"renderedHeight\":{e.RenderedHeightMeters:F1},\"overtureHeight\":{e.OvertureHeightMeters:F1},\"numFloors\":{e.NumFloors},\"gameObject\":\"{EscapeJson(e.GameObject.name)}\",\"pos\":[{e.WorldPosition.x:F1},{e.WorldPosition.y:F1},{e.WorldPosition.z:F1}],\"nearestPOI\":\"{EscapeJson(e.NearestPOI ?? "")}\"}}");
            if (i < entries.Count - 1) sb.Append(",");
            lines.Add(sb.ToString());
        }
        lines.Add("]");
        File.WriteAllText(path, string.Join("\n", lines));
        Debug.Log($"[CheckTallestBuildings] Wrote top {entries.Count} to {path}");
    }

    private static string EscapeJson(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
    }

    private static List<BuildingEntry> CollectTallestEntries(int take)
    {
        var entries = new List<BuildingEntry>();
        var buildingLayerRoot = GameObject.Find("building layer objects");
        foreach (var bm in Object.FindObjectsOfType<BuildingMetadata>())
        {
            if (bm == null || !bm.gameObject.activeInHierarchy) continue;
            var mr = bm.GetComponent<MeshRenderer>();
            float height = mr != null && mr.enabled ? mr.bounds.size.y : bm.worldHeightMeters;
            if (height < 3f || height > 500f) continue;
            entries.Add(new BuildingEntry
            {
                Name = string.IsNullOrEmpty(bm.buildingName) ? "(unnamed)" : bm.buildingName,
                RenderedHeightMeters = height,
                OvertureHeightMeters = bm.heightMeters,
                NumFloors = bm.numFloors,
                WorldPosition = mr != null ? mr.bounds.center : bm.transform.position,
                GameObject = bm.gameObject,
                NearestPOI = null
            });
        }
        if (buildingLayerRoot != null)
        {
            foreach (Transform t in buildingLayerRoot.GetComponentsInChildren<Transform>())
            {
                var go = t.gameObject;
                if (go.GetComponent<BuildingMetadata>() != null) continue;
                var mr = go.GetComponent<MeshRenderer>();
                if (mr == null || !mr.enabled) continue;
                if (!go.name.Contains("building") && !go.name.Contains("Building")) continue;
                if (go.name.Contains("Tile") || go.name.Contains("Container")) continue;
                float height = mr.bounds.size.y;
                if (height < 3f || height > 500f) continue;
                entries.Add(new BuildingEntry { Name = "(no metadata)", RenderedHeightMeters = height, OvertureHeightMeters = 0, NumFloors = 0, WorldPosition = mr.bounds.center, GameObject = go, NearestPOI = null });
            }
        }
        entries = entries.GroupBy(e => e.GameObject).Select(g => g.First()).ToList();
        var pois = new List<(string, Vector3)>();
        var poiLayer = GameObject.Find("poi_label layer objects") ?? GameObject.Find("POI_Anchors");
        if (poiLayer != null)
            foreach (Transform t in poiLayer.GetComponentsInChildren<Transform>())
            {
                var go = t.gameObject;
                if (!go.activeInHierarchy) continue;
                string text = GetPOIDisplayName(go);
                if (!string.IsNullOrEmpty(text)) pois.Add((text, go.transform.position));
            }
        foreach (var go in Object.FindObjectsOfType<GameObject>().Where(g => (g.name.Contains("POI") || g.name.Contains("poi_label")) && g.activeInHierarchy))
        {
            string text = GetPOIDisplayName(go);
            if (!string.IsNullOrEmpty(text)) pois.Add((text, go.transform.position));
        }
        const float poiRadius = 150f;
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            string nearest = null;
            float nearestDist = poiRadius;
            foreach (var poi in pois)
            {
                float d = Vector3.Distance(e.WorldPosition, poi.Item2);
                if (d < nearestDist) { nearestDist = d; nearest = $"{poi.Item1} ({d:F0}m)"; }
            }
            e.NearestPOI = nearest;
            entries[i] = e;
        }
        return entries.OrderByDescending(e => e.RenderedHeightMeters).Take(take).ToList();
    }

    private static string GetPOIDisplayName(GameObject go)
    {
        var tmp = go.GetComponentInChildren<TMPro.TextMeshPro>();
        if (tmp != null && !string.IsNullOrEmpty(tmp.text)) return tmp.text;
        return go.name;
    }
}
