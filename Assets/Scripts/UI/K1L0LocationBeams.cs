using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Kiloverse.Mapbox;

/// <summary>
/// Spawns thin red particle beams at nearby TransmitterScanner locations with labels.
/// </summary>
public class K1L0LocationBeams : MonoBehaviour
{
    private const int MaxBeams = 20;
    private const float UpdateInterval = 2f;
    private const float BeamHeight = 120f;
    private const float BeamWidth = 0.15f;
    private const int ParticleCount = 150;
    private const float ParticleSpeed = 8f;
    private const float LabelHeight = 4f;
    private const float LabelFadeDistance = 1200f;
    private const float LabelMaxDistance = 2414f; // ~1.5 miles

    // Category beam colors
    private static readonly Color BeamColorCoffee = new Color(0.2f, 0.9f, 0.4f, 1f);       // Green
    private static readonly Color BeamColorBar = new Color(1f, 0.15f, 0.05f, 1f);           // Red
    private static readonly Color BeamColorFood = new Color(0.3f, 0.5f, 1f, 1f);            // Blue
    private static readonly Color BeamColorConvenience = new Color(1f, 0.6f, 0.1f, 1f);     // Orange
    private static readonly Color BeamColorDefault = new Color(0.6f, 0.8f, 1f, 1f);         // Light cyan

    private static readonly Color LabelColor = new Color(1f, 1f, 1f, 0.9f);

    private readonly List<BeamEntry> pool = new List<BeamEntry>();
    private readonly Dictionary<string, int> beamSlotByName = new Dictionary<string, int>();
    private TMP_FontAsset font;
    private Material beamMaterial;
    private Texture2D beamTexture;
    private float lastUpdate;
    private bool loggedOnce;
    private OvertureMapManager overtureManager;

    private class BeamEntry
    {
        public GameObject root;
        public ParticleSystem particles;
        public TextMeshPro label;
        public string currentName;
        public string currentCategory;
    }

    static Color CategoryColor(string group)
    {
        switch (group)
        {
            case "coffee": return BeamColorCoffee;
            case "bar": return BeamColorBar;
            case "food": return BeamColorFood;
            case "convenience": return BeamColorConvenience;
            default: return BeamColorDefault;
        }
    }

    public void Initialize(TMP_FontAsset monoFont)
    {
        font = monoFont;
        beamTexture = CreateSoftDot(32);
        beamMaterial = CreateBeamMaterial();

        for (int i = 0; i < MaxBeams; i++)
        {
            pool.Add(CreateBeamEntry(i));
        }
        Debug.Log("[K1L0LocationBeams] Initialized with " + MaxBeams + " beam slots");
    }

    void Update()
    {
        if (Time.time - lastUpdate < UpdateInterval) return;
        lastUpdate = Time.time;
        Refresh();
    }

    void Refresh()
    {
        var scanner = TransmitterScanner.Instance;
        if (scanner == null) return;

        var nearest = scanner.GetNearestUnfiltered(MaxBeams);
        if (nearest == null || nearest.Count == 0) return;

        // Deduplicate by name
        HashSet<string> seen = new HashSet<string>();
        List<TransmitterScanner.TransmitterData> unique = new List<TransmitterScanner.TransmitterData>();
        foreach (var t in nearest)
        {
            string key = t.Name.ToLowerInvariant().Trim();
            if (seen.Contains(key)) continue;
            seen.Add(key);
            unique.Add(t);
            if (unique.Count >= MaxBeams) break;
        }

        if (unique.Count == 0) return;

        // Get map info for GPS→world conversion (same as buildings use)
        if (overtureManager == null)
            overtureManager = Object.FindFirstObjectByType<OvertureMapManager>();

        IMapInformation mapInfo = overtureManager?.map?.MapInformation;
        Vector3 mapPos = overtureManager?.map != null ? overtureManager.map.transform.position : Vector3.zero;

        if (!loggedOnce && mapInfo != null)
        {
            loggedOnce = true;
            Debug.Log($"[K1L0LocationBeams] Placing {unique.Count} beams. mapPos={mapPos}. First: {unique[0].Name}");
        }

        Camera cam = Camera.main;
        Plane[] frustumPlanes = cam != null ? GeometryUtility.CalculateFrustumPlanes(cam) : null;

        // Assign each unique POI to a stable slot by name, so beams don't teleport
        // when the player moves and the sort order changes.
        var assignedSlots = new HashSet<int>();
        var assignments = new Dictionary<int, TransmitterScanner.TransmitterData>();
        foreach (var data in unique)
        {
            string key = data.Name.ToLowerInvariant().Trim();
            if (beamSlotByName.TryGetValue(key, out int slot) && slot >= 0 && slot < pool.Count)
            {
                assignments[slot] = data;
                assignedSlots.Add(slot);
            }
        }
        // Find free slots for any POI that doesn't have one yet, and reclaim slots
        // whose previous POI has dropped out of range.
        foreach (var data in unique)
        {
            string key = data.Name.ToLowerInvariant().Trim();
            if (beamSlotByName.ContainsKey(key) && assignedSlots.Contains(beamSlotByName[key])) continue;
            int freeSlot = -1;
            for (int s = 0; s < pool.Count; s++)
            {
                if (!assignedSlots.Contains(s)) { freeSlot = s; break; }
            }
            if (freeSlot < 0) break;
            beamSlotByName[key] = freeSlot;
            assignments[freeSlot] = data;
            assignedSlots.Add(freeSlot);
        }
        // Drop stale name→slot mappings whose POI is no longer in the top-N.
        var currentKeys = new HashSet<string>();
        foreach (var data in unique) currentKeys.Add(data.Name.ToLowerInvariant().Trim());
        var stale = new List<string>();
        foreach (var kv in beamSlotByName)
            if (!currentKeys.Contains(kv.Key)) stale.Add(kv.Key);
        foreach (var k in stale) beamSlotByName.Remove(k);

        for (int i = 0; i < pool.Count; i++)
        {
            var entry = pool[i];
            if (assignments.TryGetValue(i, out var data))
            {
                Vector3 pos;
                if (mapInfo != null)
                {
                    // Convert GPS → world position fresh each frame (same math as buildings)
                    var centerMercator = new Vector2d(mapInfo.CenterMercator.x, mapInfo.CenterMercator.y);
                    pos = Conversions.LatitudeLongitudeToWorldPosition(
                        data.GeoLocation.x, data.GeoLocation.y,
                        centerMercator, mapInfo.Scale);
                    pos += mapPos;
                }
                else
                {
                    pos = data.WorldPosition;
                }
                pos.y = 0f;
                entry.root.transform.position = pos;

                if (!entry.root.activeSelf)
                {
                    entry.root.SetActive(true);
                    entry.particles.Clear();
                    entry.particles.Play();
                }

                // Update name, distance, and category color
                string catGroup = data.MainCategoryGroup ?? "other";
                if (entry.currentName != data.Name || entry.currentCategory != catGroup)
                {
                    entry.currentName = data.Name;
                    entry.currentCategory = catGroup;

                    string dist = data.Distance < 200f
                        ? $"{Mathf.RoundToInt(data.Distance)}m"
                        : $"{data.Distance / 1609.34f:F1}mi";
                    entry.label.text = $"{data.Name.ToUpperInvariant()}\n<size=70%>{dist}</size>";

                    // Set beam particle color by category
                    Color catColor = CategoryColor(catGroup);
                    var main = entry.particles.main;
                    main.startColor = catColor;

                    // Update gradient
                    var col = entry.particles.colorOverLifetime;
                    Gradient grad = new Gradient();
                    grad.SetKeys(
                        new GradientColorKey[] {
                            new GradientColorKey(catColor, 0f),
                            new GradientColorKey(Color.Lerp(catColor, Color.white, 0.2f), 0.5f),
                            new GradientColorKey(Color.Lerp(catColor, Color.white, 0.35f), 1f)
                        },
                        new GradientAlphaKey[] {
                            new GradientAlphaKey(0f, 0f),
                            new GradientAlphaKey(0.8f, 0.05f),
                            new GradientAlphaKey(0.6f, 0.5f),
                            new GradientAlphaKey(0f, 1f)
                        }
                    );
                    col.color = grad;

                    entry.label.color = new Color(catColor.r, catColor.g, catColor.b, 0.9f);
                }

                // Frustum cull + distance fade for labels
                if (cam != null && entry.label != null)
                {
                    float distToCamera = Vector3.Distance(cam.transform.position, pos);
                    bool inFrustum = frustumPlanes != null &&
                        GeometryUtility.TestPlanesAABB(frustumPlanes, new Bounds(pos + Vector3.up * LabelHeight, Vector3.one * 5f));

                    if (inFrustum && distToCamera < LabelMaxDistance)
                    {
                        float alpha = distToCamera < LabelFadeDistance ? 0.9f :
                            Mathf.Lerp(0.9f, 0f, (distToCamera - LabelFadeDistance) / (LabelMaxDistance - LabelFadeDistance));
                        Color c = entry.label.color;
                        entry.label.color = new Color(c.r, c.g, c.b, alpha);
                        entry.label.gameObject.SetActive(true);
                    }
                    else
                    {
                        entry.label.gameObject.SetActive(false);
                    }
                }
            }
            else if (entry.root.activeSelf)
            {
                entry.particles.Stop();
                entry.root.SetActive(false);
                entry.currentName = null;
                entry.currentCategory = null;
            }
        }
    }

    BeamEntry CreateBeamEntry(int index)
    {
        GameObject root = new GameObject($"LocBeam_{index}");
        root.transform.SetParent(transform, false);
        root.SetActive(false);

        // Particle beam
        GameObject psGO = new GameObject("Particles");
        psGO.transform.SetParent(root.transform, false);
        psGO.transform.localPosition = Vector3.zero;
        psGO.transform.localRotation = Quaternion.identity;

        ParticleSystem ps = psGO.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var psr = psGO.GetComponent<ParticleSystemRenderer>();
        psr.renderMode = ParticleSystemRenderMode.Billboard;
        psr.material = beamMaterial;

        var main = ps.main;
        float lifetime = BeamHeight / ParticleSpeed;
        main.playOnAwake = false;
        main.startLifetime = lifetime;
        main.startSpeed = ParticleSpeed;
        main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 1.0f);
        main.startColor = BeamColorDefault;
        main.maxParticles = ParticleCount + 50;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.gravityModifier = 0f;

        var emission = ps.emission;
        emission.rateOverTime = ParticleCount / lifetime;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 0.5f;
        shape.radius = BeamWidth / 2f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(BeamColorDefault, 0f),
                new GradientColorKey(Color.Lerp(BeamColorDefault, Color.white, 0.2f), 0.5f),
                new GradientColorKey(Color.Lerp(BeamColorDefault, Color.white, 0.35f), 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.8f, 0.05f),
                new GradientAlphaKey(0.6f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        col.color = grad;

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.x = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);
        vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);
        vel.space = ParticleSystemSimulationSpace.Local;

        // World-space label
        GameObject labelGO = new GameObject("Label");
        labelGO.transform.SetParent(root.transform, false);
        labelGO.transform.localPosition = new Vector3(0f, LabelHeight, 0f);

        TextMeshPro tmp = labelGO.AddComponent<TextMeshPro>();
        tmp.font = font;
        tmp.fontSize = 3f;
        tmp.color = LabelColor;
        tmp.alignment = TextAlignmentOptions.Bottom;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.sortingOrder = 100;
        tmp.rectTransform.sizeDelta = new Vector2(20f, 5f);

        labelGO.AddComponent<K1L0BillboardLabel>();

        return new BeamEntry
        {
            root = root,
            particles = ps,
            label = tmp,
            currentName = null
        };
    }

    Material CreateBeamMaterial()
    {
        // Use the same material as BeamAvatar for consistency
        Material loaded = Resources.Load<Material>("Materials/ParticleBeam");
        if (loaded != null)
        {
            Material mat = new Material(loaded);
            mat.SetColor("_BaseColor", Color.white); // White base — particle system tints per-category
            mat.SetColor("_EmissionColor", Color.white * 5f);
            return mat;
        }

        // Fallback: create from scratch
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        Material fallback = new Material(shader);
        fallback.SetTexture("_BaseMap", beamTexture);
        fallback.SetColor("_BaseColor", Color.white);
        fallback.SetFloat("_Surface", 1f);
        fallback.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        fallback.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        fallback.renderQueue = 3000;
        return fallback;
    }

    Texture2D CreateSoftDot(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = size / 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / center;
                float alpha = Mathf.Clamp01(1f - dist * dist);
                tex.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
        }
        tex.Apply();
        return tex;
    }
}

public class K1L0BillboardLabel : MonoBehaviour
{
    void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
    }
}
