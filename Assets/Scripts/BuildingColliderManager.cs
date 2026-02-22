using System.Collections.Generic;
using UnityEngine;

public class BuildingColliderManager : MonoBehaviour
{
    public static BuildingColliderManager Instance;

    [Header("Settings")]
    public bool enableCollisionDetection = false; // TEMPORARILY DISABLED
    public float scanInterval = 0.5f; // More frequent updates for smoother syncing
    [Tooltip("Add colliders to buildings within this distance")]
    public float addColliderRadius = 250f;
    [Tooltip("Remove colliders from buildings beyond this distance")]
    public float removeColliderRadius = 300f;

    [Header("Collider Type")]
    public bool useBoxColliders = true;

    [Header("Debug")]
    public bool showDebugGizmos = true;
    public bool verboseLogging = false;

    [Header("Stats")]
    public int activeColliders = 0;
    public int totalBuildingsFound = 0;

    private float _timer;
    private GameObject _mapRoot;
    private Transform _player;
    
    // For Gizmos only
    private List<Bounds> _debugColliderBounds = new List<Bounds>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[BuildingColliderManager] Duplicate detected on {gameObject.name}. Destroying self.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
        Debug.Log("[BuildingColliderManager] Spatial Collider System Online.");
    }

    private int _searchFrame;
    private static bool _loggedFirstRun;

    private void Update()
    {
        // Editor: no work until post-boot grace period elapsed (prevents lockup)
        if (Application.isEditor && !BootState.PostBootGracePeriodElapsed)
            return;
        // Editor: don't hammer FindFirstObjectByType during "waiting for tiles" (prevents lockup)
        if (Application.isEditor && !BootState.FirstTilesLoaded)
            return;
        // CRITICAL: Don't run expensive GetComponentsInChildren if collision detection is disabled
        if (!enableCollisionDetection)
            return;
        if (!_loggedFirstRun)
        {
            _loggedFirstRun = true;
            BootDiagnostics.Mark("BuildingColliderManager first Update after boot");
        }
        
        // CRITICAL: Early exit if collision detection disabled - prevents expensive GetComponentsInChildren
        if (!enableCollisionDetection)
        {
            return;
        }

        if (_player == null)
        {
            _searchFrame++;
            if (Application.isEditor && _searchFrame < 10)
                return;
            var controller = FindFirstObjectByType<KiloFirstPersonController>();
            if (controller != null) 
            {
                _player = controller.transform;
            }
            else
            {
                _player = Camera.main?.transform;
            }
        }

        if (_player == null) return;

        _timer += Time.deltaTime;
        if (_timer >= scanInterval)
        {
            _timer = 0;
            UpdateColliders();
        }
    }

    private void UpdateColliders()
    {
        // CRITICAL: Early return if disabled - GetComponentsInChildren is VERY expensive
        if (!enableCollisionDetection) return; // DISABLED

        if (_player == null) return;

        if (_mapRoot == null)
        {
            FindMapRoot();
            if (_mapRoot == null) return;
        }

        // CRITICAL FIX: GetComponentsInChildren searches ALL objects in scene - can freeze editor
        // Only run this if collision detection is actually enabled AND we're past grace period
        if (Application.isEditor && BootState.AllowPlayer)
        {
            float timeSinceAllowPlayer = Time.realtimeSinceStartup - BootState.AllowPlayerTime;
            if (timeSinceAllowPlayer < 10f) // Wait 10 seconds after boot before expensive scan
            {
                return;
            }
        }

        MeshRenderer[] renderers = _mapRoot.GetComponentsInChildren<MeshRenderer>(true);
        if (renderers == null) return;

        Vector3 playerPos = _player.position;
        int added = 0;
        int updated = 0;
        int removed = 0;
        totalBuildingsFound = 0;
        
        _debugColliderBounds.Clear();

        foreach (var r in renderers)
        {
            GameObject go = r.gameObject;
            if (!IsBuilding(go)) continue;

            BoxCollider existingBox = go.GetComponent<BoxCollider>();
            Collider existingCol = go.GetComponent<Collider>();

            // Always check inactive buildings for collider removal
            // (Frustum culling deactivates tiles, we should clean up their colliders)
            if (!r.enabled || !go.activeInHierarchy)
            {
                if (existingCol != null)
                {
                    Destroy(existingCol);
                    removed++;
                }
                continue;
            }

            totalBuildingsFound++;

            // Bounds check
            Vector3 buildingCenter = r.bounds.center;
            float distance = Vector3.Distance(playerPos, buildingCenter);

            // IN RANGE: Add or Update
            if (distance <= addColliderRadius)
            {
                if (existingBox == null && existingCol == null)
                {
                    // Case 1: No collider, add new Box
                    AddBoxCollider(go, r);
                    added++;
                }
                else if (existingBox != null)
                {
                    // Case 2: Has BoxCollider, UPDATE IT to match current mesh
                    // This fixes the "out of sync" issue when Mapbox recycles objects
                    UpdateBoxCollider(go, existingBox, r);
                    updated++;
                }
                // (Case 3: Has non-box collider, ignore or leave as is)

                // Track for Gizmos
                _debugColliderBounds.Add(r.bounds);
            }
            // OUT OF RANGE: Remove
            else if (distance > removeColliderRadius)
            {
                if (existingCol != null)
                {
                    Destroy(existingCol);
                    removed++;
                }
            }
        }

        activeColliders = _debugColliderBounds.Count;

        // Log every 15 seconds with TileLoad tag
        if (Time.frameCount % 900 == 0 && (added > 0 || removed > 0 || activeColliders > 0))
        {
            Debug.Log($"[TileLoad] Building Colliders: {activeColliders} active | This scan: +{added} added, ~{updated} updated, -{removed} removed | {totalBuildingsFound} buildings found");
        }
    }

    private void FindMapRoot()
    {
        // CRITICAL: Cache result - don't search repeatedly
        if (_mapRoot != null) return;
        
        _mapRoot = GameObject.Find("CitySimulatorMap");
        if (_mapRoot == null) _mapRoot = GameObject.Find("Map");
        if (_mapRoot == null) _mapRoot = GameObject.Find("ArTableMap");

        if (_mapRoot == null)
        {
            // CRITICAL FIX: FindObjectsByType<MonoBehaviour> searches ALL MonoBehaviours - extremely expensive!
            // Use FindFirstObjectByType with specific type instead
            var mapInfo = FindFirstObjectByType<Kiloverse.Mapbox.KiloverseMapInfo>();
            if (mapInfo != null)
            {
                _mapRoot = mapInfo.gameObject;
            }
            else
            {
                // Last resort: only search if absolutely necessary, and throttle it
                if (Application.isEditor && BootState.AllowPlayer)
                {
                    float timeSinceAllowPlayer = Time.realtimeSinceStartup - BootState.AllowPlayerTime;
                    if (timeSinceAllowPlayer < 5f) return; // Don't do expensive search right after boot
                }
                var allObjects = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
                foreach (var mb in allObjects)
                {
                    if (mb.GetType().Name.Contains("KiloverseMapInfo") || mb.GetType().Name == "AbstractMap")
                    {
                        _mapRoot = mb.gameObject;
                        break;
                    }
                }
            }
        }
    }

    private bool IsBuilding(GameObject go)
    {
        string lowerName = go.name.ToLower();
        if (lowerName.Contains("road") || 
            lowerName.Contains("terrain") || 
            lowerName.Contains("ground") ||
            lowerName.Contains("label") ||
            lowerName.Contains("poi") ||
            lowerName.Contains("canvas") ||
            lowerName.Contains("event"))
        {
            return false;
        }
        if (lowerName.Contains("building")) return true;
        return go.GetComponent<MeshRenderer>() != null;
    }

    private void AddBoxCollider(GameObject go, MeshRenderer renderer)
    {
        BoxCollider box = go.AddComponent<BoxCollider>();
        UpdateBoxCollider(go, box, renderer);
    }

    private void UpdateBoxCollider(GameObject go, BoxCollider box, MeshRenderer renderer)
    {
        // Calculate bounds in local space
        Bounds worldBounds = renderer.bounds;
        
        // Transform world center to local space
        box.center = go.transform.InverseTransformPoint(worldBounds.center);
        
        // Size is scale-independent if we inverse transform vector, 
        // BUT determining exact local size from world bounds on rotated/scaled object is tricky.
        // Simple approach: Use Renderer local bounds if possible, but Mapbox meshes often bake scale.
        // Safest approach for Mapbox tiles:
        // World Axis Aligned Bounds (AABB) might be too loose if building is rotated.
        // However, Mapbox buildings are usually part of a tile mesh.
        
        // Better approximation for size:
        Vector3 worldSize = worldBounds.size;
        // Divide by lossyScale to get local size estimation
        Vector3 parentScale = go.transform.lossyScale;
        box.size = new Vector3(
            parentScale.x > 0 ? worldSize.x / parentScale.x : 0,
            parentScale.y > 0 ? worldSize.y / parentScale.y : 0,
            parentScale.z > 0 ? worldSize.z / parentScale.z : 0
        );
    }

    [ContextMenu("Force Update Now")]
    public void ForceUpdate()
    {
        UpdateColliders();
    }

    [ContextMenu("Clear All Colliders")]
    public void ClearAllColliders()
    {
        if (_mapRoot == null) FindMapRoot();
        if (_mapRoot == null) return;

        Collider[] colliders = _mapRoot.GetComponentsInChildren<Collider>(true);
        foreach (var c in colliders)
        {
            if (c is BoxCollider || c is MeshCollider)
            {
                Destroy(c);
            }
        }
        _debugColliderBounds.Clear();
        activeColliders = 0;
        Debug.Log("[BuildingColliderManager] Cleared all managed colliders.");
    }

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;

        Vector3 center = transform.position;
        if (_player != null) center = _player.position;
        else if (Camera.main != null) center = Camera.main.transform.position;

        // Draw range rings
        Gizmos.color = new Color(0, 1, 0, 0.3f);
        Gizmos.DrawWireSphere(center, addColliderRadius);

        Gizmos.color = new Color(1, 0, 0, 0.3f);
        Gizmos.DrawWireSphere(center, removeColliderRadius);

        // Draw actual tracked colliders
        Gizmos.color = new Color(0, 1, 1, 0.5f); // Cyan
        foreach (var bounds in _debugColliderBounds)
        {
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }
    }
}
