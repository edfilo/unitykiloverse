using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Kiloverse.Mapbox;
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

    private static readonly Color BeamColor = new Color(1f, 0.15f, 0.05f, 1f);
    private static readonly Color LabelColor = new Color(1f, 0.4f, 0.3f, 0.9f);

    private readonly List<BeamEntry> pool = new List<BeamEntry>();
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

        // Deduplicate and filter to those with world positions
        HashSet<string> seen = new HashSet<string>();
        List<TransmitterScanner.TransmitterData> unique = new List<TransmitterScanner.TransmitterData>();
        foreach (var t in nearest)
        {
            if (!t.HasWorldPosition) continue;
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

        for (int i = 0; i < pool.Count; i++)
        {
            var entry = pool[i];
            if (i < unique.Count)
            {
                var data = unique[i];
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

                if (entry.currentName != data.Name)
                {
                    entry.currentName = data.Name;
                    string dist = data.Distance < 200f
                        ? $"{Mathf.RoundToInt(data.Distance)}m"
                        : $"{data.Distance / 1609.34f:F1}mi";
                    entry.label.text = $"{data.Name.ToUpperInvariant()}\n<size=70%>{dist}</size>";
                }
            }
            else if (entry.root.activeSelf)
            {
                entry.particles.Stop();
                entry.root.SetActive(false);
                entry.currentName = null;
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
        main.startColor = BeamColor;
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
                new GradientColorKey(BeamColor, 0f),
                new GradientColorKey(new Color(1f, 0.3f, 0.1f), 0.5f),
                new GradientColorKey(new Color(1f, 0.5f, 0.2f), 1f)
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
            mat.SetColor("_BaseColor", BeamColor);
            mat.SetColor("_EmissionColor", BeamColor * 10f);
            return mat;
        }

        // Fallback: create from scratch
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        Material fallback = new Material(shader);
        fallback.SetTexture("_BaseMap", beamTexture);
        fallback.SetColor("_BaseColor", BeamColor);
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
