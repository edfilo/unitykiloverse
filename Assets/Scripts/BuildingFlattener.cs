using System.Collections.Generic;
using UnityEngine;
using Kiloverse.Mapbox;

/// <summary>
/// When the player walks into the 2D footprint of a building, hide the
/// extruded walls and paint a flat roof polygon on the ground in their
/// place — so the player isn't visually buried inside an opaque box on
/// the zoomed map. Restores the original geometry the moment the player
/// steps back outside.
///
/// Cost budget: with ~100 visible buildings, the per-tick check is
/// distance-prefilter + AABB + point-in-polygon for ones in range, which
/// is well under a millisecond. Polygon extraction happens once per
/// building on first proximity and is cached.
///
/// Relies on ZossBuildingStack's invariant: the first N vertices in the
/// mesh are the roof, raised by totalHeightTile from the original
/// footprint. Wall vertices are appended after them.
/// </summary>
public class BuildingFlattener : MonoBehaviour
{
    public static BuildingFlattener Instance;

    [Header("Toggle")]
    [Tooltip("Master switch. Off = nothing flattens, no per-frame work.")]
    public bool enableFlattening = true;

    [Header("Scan")]
    [Tooltip("Seconds between footprint scans. 0.2-0.3 is plenty for walking pace.")]
    public float scanInterval = 0.25f;

    [Tooltip("Buildings further than this from the player are ignored. Keep tight — 200m is generous for a zoomed map.")]
    public float prefilterRadiusMeters = 200f;

    [Header("Look")]
    [Tooltip("Roof tint applied to the flat overlay polygon. Default is a slightly darker grey so the footprint reads against terrain.")]
    public Color roofColor = new Color(0.34f, 0.36f, 0.40f, 1f);

    [Tooltip("Height above ground at which the flat roof polygon sits, so it doesn't z-fight with the terrain.")]
    public float overlayLift = 0.05f;

    [Header("Debug")]
    public bool verbose = false;
    public int currentlyFlattened = 0;

    private Transform _player;
    private float _timer;
    private Material _sharedRoofMaterial;
    private readonly Dictionary<BuildingMetadata, FlattenState> _states = new Dictionary<BuildingMetadata, FlattenState>();
    private readonly HashSet<BuildingMetadata> _frameInside = new HashSet<BuildingMetadata>();
    private readonly List<BuildingMetadata> _toRestore = new List<BuildingMetadata>();

    private class FlattenState
    {
        public MeshRenderer renderer;
        public bool rendererWasEnabled;
        public GameObject overlay;        // null until first flatten
        public Vector3[] footprintWorld;  // world-space roof verts (XZ used for poly test)
        public float groundY;             // world Y of the roof polygon overlay
        public Bounds worldBounds;        // cached MeshRenderer.bounds for prefilter
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Update()
    {
        if (!enableFlattening) return;

        if (_player == null)
        {
            var controller = FindFirstObjectByType<KiloFirstPersonController>();
            _player = controller != null ? controller.transform : (Camera.main != null ? Camera.main.transform : null);
            if (_player == null) return;
        }

        _timer += Time.deltaTime;
        if (_timer < scanInterval) return;
        _timer = 0;

        Vector3 pos = _player.position;
        float prefilterSq = prefilterRadiusMeters * prefilterRadiusMeters;

        // Sweep visible buildings. FindObjectsByType has been heavy on this
        // project (BuildingColliderManager notes the same), so we only call
        // it on the scan tick — not every frame.
        var all = FindObjectsByType<BuildingMetadata>(FindObjectsSortMode.None);
        _frameInside.Clear();

        for (int i = 0; i < all.Length; i++)
        {
            var bm = all[i];
            if (bm == null) continue;
            Vector3 bpos = bm.transform.position;
            float dx = bpos.x - pos.x, dz = bpos.z - pos.z;
            if (dx * dx + dz * dz > prefilterSq) continue;

            if (!_states.TryGetValue(bm, out var state))
            {
                state = ExtractFootprint(bm);
                if (state == null) continue;
                _states[bm] = state;
            }

            // Cheap AABB pre-test in case the player's just brushing the
            // bounding box but isn't really in the polygon — saves running
            // point-in-poly on every building in range.
            if (!state.worldBounds.Contains(new Vector3(pos.x, state.worldBounds.center.y, pos.z))) continue;

            if (PointInPolygonXZ(pos, state.footprintWorld))
            {
                _frameInside.Add(bm);
                if (state.overlay == null) Flatten(bm, state);
            }
        }

        // Restore anything the player has left. Iterate over a snapshot so
        // we can mutate _states while walking it.
        _toRestore.Clear();
        foreach (var kv in _states)
        {
            if (kv.Value.overlay != null && !_frameInside.Contains(kv.Key))
                _toRestore.Add(kv.Key);
        }
        for (int i = 0; i < _toRestore.Count; i++) Restore(_toRestore[i]);

        currentlyFlattened = _frameInside.Count;
    }

    // ── Footprint extraction ───────────────────────────────────────────────
    // ZossBuildingStack moves the original 2D polygon vertices UP to roof
    // height (line 173) and THEN appends wall vertices. So the roof verts
    // are the first cluster at the mesh's maxY. We grab everything at that
    // top plane, in original index order, and drop them to the ground plane
    // (mesh.bounds.min.y) for the footprint polygon used by point-in-poly
    // AND for the flat overlay geometry.
    private FlattenState ExtractFootprint(BuildingMetadata bm)
    {
        var mf = bm.GetComponentInChildren<MeshFilter>();
        var mr = bm.GetComponentInChildren<MeshRenderer>();
        if (mf == null || mr == null || mf.sharedMesh == null) return null;

        var mesh = mf.sharedMesh;
        var verts = mesh.vertices;
        if (verts.Length < 3) return null;

        float maxY = mesh.bounds.max.y;
        float minY = mesh.bounds.min.y;
        float roofThreshold = maxY - 0.001f;

        // Collect roof verts in original order. World-space transform once.
        Matrix4x4 ltw = mf.transform.localToWorldMatrix;
        var roofWorld = new List<Vector3>(16);
        for (int i = 0; i < verts.Length; i++)
        {
            if (verts[i].y >= roofThreshold)
                roofWorld.Add(ltw.MultiplyPoint3x4(verts[i]));
            // Stop when we leave the contiguous roof block — once walls
            // start (verts with y < roofThreshold), the rest are wall geom.
            else if (roofWorld.Count > 0) break;
        }
        if (roofWorld.Count < 3) return null;

        // Drop roof verts to the ground plane for the polygon used in the
        // footprint test AND the flat overlay quad.
        float groundWorldY = ltw.MultiplyPoint3x4(new Vector3(0, minY, 0)).y;
        for (int i = 0; i < roofWorld.Count; i++)
            roofWorld[i] = new Vector3(roofWorld[i].x, groundWorldY, roofWorld[i].z);

        return new FlattenState
        {
            renderer = mr,
            rendererWasEnabled = mr.enabled,
            overlay = null,
            footprintWorld = roofWorld.ToArray(),
            groundY = groundWorldY + overlayLift,
            worldBounds = mr.bounds,
        };
    }

    // ── Flatten / Restore ─────────────────────────────────────────────────
    private void Flatten(BuildingMetadata bm, FlattenState state)
    {
        if (state.renderer == null) return; // tile unloaded
        state.rendererWasEnabled = state.renderer.enabled;
        state.renderer.enabled = false;
        state.overlay = BuildRoofOverlay(bm.transform, state);
        if (verbose) Debug.Log($"[BuildingFlattener] Flattened '{bm.buildingName}' (id={bm.featureId})");
    }

    private void Restore(BuildingMetadata bm)
    {
        if (!_states.TryGetValue(bm, out var state)) return;
        if (state.renderer != null) state.renderer.enabled = state.rendererWasEnabled;
        if (state.overlay != null) Destroy(state.overlay);
        // If the building itself is gone (tile unload), drop the cache entry
        // so we don't keep iterating a dead reference forever.
        if (bm == null || state.renderer == null) _states.Remove(bm);
        else state.overlay = null;
        if (verbose) Debug.Log($"[BuildingFlattener] Restored");
    }

    // ── Roof polygon mesh + material ──────────────────────────────────────
    // Overlay is a standalone GameObject at the footprint centroid in world
    // space (NOT parented to the building — that keeps positioning simple
    // and survives Mapbox detaching/re-tiling). On building destruction
    // we'll catch the dangling renderer on the next scan and Restore.
    private GameObject BuildRoofOverlay(Transform _unused, FlattenState state)
    {
        int n = state.footprintWorld.Length;
        float cx = 0, cz = 0;
        for (int i = 0; i < n; i++) { cx += state.footprintWorld[i].x; cz += state.footprintWorld[i].z; }
        cx /= n; cz /= n;

        var go = new GameObject("K1L0 Building Roof Overlay");
        go.transform.position = new Vector3(cx, state.groundY, cz);
        go.transform.rotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        var localVerts = new Vector3[n];
        for (int i = 0; i < n; i++)
            localVerts[i] = new Vector3(state.footprintWorld[i].x - cx, 0, state.footprintWorld[i].z - cz);

        var tris = new int[(n - 2) * 3];
        for (int i = 0; i < n - 2; i++)
        {
            tris[i * 3 + 0] = 0;
            tris[i * 3 + 1] = i + 1;
            tris[i * 3 + 2] = i + 2;
        }

        var mesh = new Mesh { name = "K1L0 Roof Polygon" };
        mesh.vertices = localVerts;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = EnsureRoofMaterial();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        return go;
    }

    private Material EnsureRoofMaterial()
    {
        if (_sharedRoofMaterial != null) return _sharedRoofMaterial;
        var sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Unlit/Color");
        _sharedRoofMaterial = new Material(sh) { name = "K1L0 Roof Overlay" };
        if (_sharedRoofMaterial.HasProperty("_BaseColor")) _sharedRoofMaterial.SetColor("_BaseColor", roofColor);
        if (_sharedRoofMaterial.HasProperty("_Color")) _sharedRoofMaterial.SetColor("_Color", roofColor);
        return _sharedRoofMaterial;
    }

    // Standard ray-cast point-in-polygon, XZ plane only.
    private static bool PointInPolygonXZ(Vector3 p, Vector3[] poly)
    {
        bool inside = false;
        int n = poly.Length;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            float xi = poly[i].x, zi = poly[i].z;
            float xj = poly[j].x, zj = poly[j].z;
            bool intersect = ((zi > p.z) != (zj > p.z)) &&
                             (p.x < (xj - xi) * (p.z - zi) / ((zj - zi) + 1e-9f) + xi);
            if (intersect) inside = !inside;
        }
        return inside;
    }
}
