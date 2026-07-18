using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

/// Masked, glitching item thumbnail displayed at the top of a signal beam.
/// The beam keeps its own particles; the item visual itself uses no particles.
public sealed class BeamItemHologram : MonoBehaviour
{
    private const float TopOffsetMeters = 6f;
    private static float targetViewportHeight = 0.14f;
    private const float MinWorldSize = 4.5f;
    private static float maxWorldSize = 96f;
    private static float glitchAmount = 0.62f;
    private static readonly Dictionary<string, Texture2D> TextureCache = new();
    private GameObject visual;
    private Material material;
    private string requestedKey = "";
    private float beamHeight = 18f;
    private Coroutine loadRoutine;
    private bool artworkReady;
    private float nextVisibilityLogTime;
    private Vector3 worldAnchor;
    private bool hasWorldAnchor;
    private float floatPhase;

    private void Awake()
    {
        floatPhase = Mathf.Repeat(GetInstanceID() * .6180339f, 100f);
    }

    public void Configure(string colorUrl, string depthUrl, bool useElementsFallback, int particleBudget, Material unusedBeamMaterial)
    {
        // This is now one inexpensive quad, not a particle budget. Every beam that
        // has item artwork should show it, including distant/location beams.
        int enabled = 1;
        string key = $"{colorUrl}|{depthUrl}|{useElementsFallback}|{enabled}";
        if (key == requestedKey) return;
        requestedKey = key;
        artworkReady = false;
        if (loadRoutine != null) StopCoroutine(loadRoutine);
        EnsureVisual();
        visual.SetActive(false);
        Debug.Log($"[BeamItemGlitch] configure beam={name} enabled={enabled} color={(string.IsNullOrWhiteSpace(colorUrl) ? "missing" : "set")} depth={(string.IsNullOrWhiteSpace(depthUrl) ? "missing" : "set")}");
        if (material == null)
        {
            Debug.LogError("[BeamItemGlitch] K1L0/BeamItemGlitch shader missing from player build");
            return;
        }
        material.SetFloat("_DebugSolid", 0f);
        if (string.IsNullOrWhiteSpace(colorUrl) && useElementsFallback)
        {
            var gemstone = Resources.Load<Texture2D>("BeamDiagnostics/ElementsGemstone");
            if (gemstone == null)
            {
                Debug.LogError("[BeamItemGlitch] ElementsGemstone fallback asset missing");
                return;
            }
            material.SetTexture("_MainTex", gemstone);
            material.SetTexture("_MaskTex", gemstone);
            material.SetFloat("_HasMask", 0f);
            material.SetFloat("_DebugSolid", 0f);
            visual.SetActive(true);
            artworkReady = true;
            Debug.Log($"[BeamItemGlitch] Elements gemstone fallback applied beam={name} image={gemstone.width}x{gemstone.height}");
            return;
        }
        if (string.IsNullOrWhiteSpace(colorUrl))
        {
            Debug.LogWarning($"[BeamItemGlitch] hidden beam={name}: missing color URL");
            return;
        }
        loadRoutine = StartCoroutine(LoadAndApply(colorUrl, depthUrl, key));
    }

    public void RetryPendingArtwork()
    {
        if (artworkReady) return;
        if (loadRoutine != null) StopCoroutine(loadRoutine);
        loadRoutine = null;
        // Configure will be called immediately after activation. Clearing the
        // key lets it restart a coroutine that could not run on an inactive GO.
        requestedKey = "";
    }

    public void SetBeamHeight(float height)
    {
        beamHeight = Mathf.Max(1f, height);
    }

    public void SetWorldAnchor(Vector3 anchor)
    {
        worldAnchor = anchor;
        hasWorldAnchor = true;
    }

    private void EnsureVisual()
    {
        if (visual != null) return;
        visual = GameObject.CreatePrimitive(PrimitiveType.Quad);
        visual.name = "ItemGlitchThumbnail";
        visual.transform.SetParent(transform, false);
        visual.transform.localPosition = new Vector3(0f, beamHeight + TopOffsetMeters, 0f);
        visual.transform.localScale = Vector3.one * MinWorldSize;
        var collider = visual.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
        var shader = Shader.Find("K1L0/BeamItemGlitch");
        if (shader != null) material = new Material(shader);
        else Debug.LogError("[BeamItemGlitch] Shader.Find failed for K1L0/BeamItemGlitch");
        if (material != null)
        {
            var renderer = visual.GetComponent<MeshRenderer>();
            renderer.material = material;
            // A brief, restrained glitch cadence keeps the thumbnail legible.
            material.SetFloat("_GlitchAmount", glitchAmount);
        }
        visual.SetActive(false);
    }

    private IEnumerator LoadAndApply(string colorUrl, string depthUrl, string expectedKey)
    {
        yield return LoadTexture(colorUrl);
        if (expectedKey != requestedKey || material == null) yield break;
        if (!TextureCache.TryGetValue(colorUrl, out var color))
        {
            visual.SetActive(false);
            Debug.LogWarning($"[BeamItemGlitch] hidden beam={name}: color texture unavailable url={colorUrl}");
            yield break;
        }

        // Color is authoritative. Show it as soon as it is ready rather than
        // holding a diagnostic square on-screen while an optional mask loads.
        material.SetTexture("_MainTex", color);
        material.SetTexture("_MaskTex", color);
        material.SetFloat("_HasMask", 0f);
        material.SetFloat("_DebugSolid", 0f);
        visual.SetActive(true);
        artworkReady = true;

        bool hasMask = false;
        if (!string.IsNullOrWhiteSpace(depthUrl))
        {
            yield return LoadTexture(depthUrl);
            if (expectedKey != requestedKey || material == null) yield break;
            if (TextureCache.TryGetValue(depthUrl, out var mask))
            {
                material.SetTexture("_MaskTex", mask);
                material.SetFloat("_HasMask", 1f);
                hasMask = true;
            }
        }
        Debug.Log($"[BeamItemGlitch] ready beam={name} image={color.width}x{color.height} mask={hasMask} localY={visual.transform.localPosition.y:F1} scale={visual.transform.localScale.x:F1}");
        loadRoutine = null;
    }

    private static IEnumerator LoadTexture(string url)
    {
        if (string.IsNullOrWhiteSpace(url) || TextureCache.ContainsKey(url)) yield break;
        using var request = UnityWebRequestTexture.GetTexture(url, false);
        yield return request.SendWebRequest();
        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[BeamItemGlitch] texture failed url={url} code={request.responseCode}: {request.error}");
            yield break;
        }
        var texture = DownloadHandlerTexture.GetContent(request);
        if (texture != null) TextureCache[url] = texture;
    }

    private void LateUpdate()
    {
        if (visual == null || !visual.activeSelf) return;
        // BeamAvatar pulses its root scale. Use world-space placement and scale so
        // that pulse cannot multiply this child's height or apparent size. The
        // geometric endpoint can sit above the visibly fading particle tip, so
        // keep its projected Y inside the viewport while preserving X and depth.
        Vector3 beamTop = hasWorldAnchor
            ? worldAnchor
            : transform.position + Vector3.up * (beamHeight + TopOffsetMeters);
        // Keep the collectible attached to the beam cap. Its low point is the
        // visible beam tip and its high point is the established top anchor;
        // it no longer dives toward the ground between cycles.
        Vector3 lowAnchor = beamTop - Vector3.up * 8f;
        float verticalCycle = .5f + .5f * Mathf.Cos(Time.time * .42f + floatPhase);
        verticalCycle = verticalCycle * verticalCycle * (3f - 2f * verticalCycle);
        Vector3 worldTop = Vector3.Lerp(lowAnchor, beamTop, verticalCycle);
        var cam = Camera.main;
        if (cam != null)
        {
            Vector3 towardCamera = cam.transform.position - worldTop;
            if (towardCamera.sqrMagnitude > 0.001f)
                worldTop += towardCamera.normalized * 10f;
        }
        // Hold a nearly constant angular size instead of using one enormous
        // world-space quad. Close items no longer engulf the screen and far
        // items remain legible. Clamps keep extreme distances believable.
        float worldSize = MinWorldSize;
        float cameraDistance = 0f;
        if (cam != null)
        {
            cameraDistance = Vector3.Distance(cam.transform.position, worldTop);
            if (cam.orthographic)
                worldSize = cam.orthographicSize * 2f * targetViewportHeight;
            else
            {
                worldSize = 2f * cameraDistance * Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad * .5f) * targetViewportHeight;
            }
        }
        worldSize = Mathf.Clamp(worldSize, MinWorldSize, maxWorldSize);
        // A slow asymmetric drift makes the item feel suspended without
        // detaching it from the stable beam-top anchor. Depth travel is
        // intentionally stronger than the lateral motion so the image seems
        // to float toward/away from the viewer instead of merely sliding.
        if (cam != null)
        {
            float drift = Mathf.Sin(Time.time * .53f + floatPhase) * .62f +
                          Mathf.Sin(Time.time * .21f + floatPhase * 1.73f) * .38f;
            float depthDrift = Mathf.Sin(Time.time * .37f + floatPhase * 1.31f) * .72f +
                               Mathf.Sin(Time.time * .16f + floatPhase * .77f) * .28f;
            float distanceWobble = Mathf.Lerp(1f, 2.35f,
                Mathf.InverseLerp(140f, 1200f, cameraDistance));
            worldTop += cam.transform.right * (worldSize * .055f * drift * Mathf.Lerp(1f, 1.55f, distanceWobble - 1f));
            worldTop += cam.transform.forward * (worldSize * .16f * depthDrift * distanceWobble);
        }
        visual.transform.position = worldTop;
        visual.transform.localScale = new Vector3(
            worldSize / Mathf.Max(0.001f, Mathf.Abs(transform.lossyScale.x)),
            worldSize / Mathf.Max(0.001f, Mathf.Abs(transform.lossyScale.y)),
            worldSize / Mathf.Max(0.001f, Mathf.Abs(transform.lossyScale.z)));
        if (cam != null) visual.transform.rotation = cam.transform.rotation;
        material?.SetFloat("_TimeOffset", Time.time);
        if (Time.unscaledTime >= nextVisibilityLogTime && cam != null)
        {
            nextVisibilityLogTime = Time.unscaledTime + 10f;
            Vector3 screen = cam.WorldToScreenPoint(visual.transform.position);
            var renderer = visual.GetComponent<MeshRenderer>();
            Debug.Log($"[BeamItemGlitch] visibility beam={name} viewport={Screen.width}x{Screen.height} screen=({screen.x:F0},{screen.y:F0},{screen.z:F0}) active={visual.activeInHierarchy} renderer={renderer != null && renderer.enabled} scale={visual.transform.lossyScale.x:F1}");
        }
    }

    public static void SetViewportHeight(float value)
    {
        targetViewportHeight = Mathf.Clamp(value, .04f, .28f);
    }

    public static void SetMaxWorldSize(float value)
    {
        maxWorldSize = Mathf.Clamp(value, 16f, 180f);
    }

    public static void SetGlitchAmount(float value)
    {
        glitchAmount = Mathf.Clamp01(value);
        foreach (var item in FindObjectsByType<BeamItemHologram>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            item.material?.SetFloat("_GlitchAmount", glitchAmount);
    }

    public bool TryGetScreenRect(Camera cam, out Rect rect)
    {
        rect = default;
        if (cam == null || visual == null || !visual.activeInHierarchy) return false;
        var renderer = visual.GetComponent<Renderer>();
        if (renderer == null || !renderer.enabled) return false;

        Bounds b = renderer.bounds;
        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, 0f);
        Vector3 max = new Vector3(float.MinValue, float.MinValue, 0f);
        bool anyInFront = false;
        for (int x = -1; x <= 1; x += 2)
        for (int y = -1; y <= 1; y += 2)
        for (int z = -1; z <= 1; z += 2)
        {
            Vector3 p = cam.WorldToScreenPoint(b.center + Vector3.Scale(b.extents, new Vector3(x, y, z)));
            if (p.z <= 0f) continue;
            anyInFront = true;
            min.x = Mathf.Min(min.x, p.x); min.y = Mathf.Min(min.y, p.y);
            max.x = Mathf.Max(max.x, p.x); max.y = Mathf.Max(max.y, p.y);
        }
        if (!anyInFront) return false;
        rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        return rect.width > 1f && rect.height > 1f;
    }

    public bool TryGetStableTopScreenRect(Camera cam, out Rect rect)
    {
        if (!TryGetScreenRect(cam, out rect) || !hasWorldAnchor) return false;
        Vector3 currentCenter = cam.WorldToScreenPoint(visual.transform.position);
        Vector3 stableCenter = cam.WorldToScreenPoint(worldAnchor);
        if (currentCenter.z <= 0f || stableCenter.z <= 0f) return false;
        rect.position += new Vector2(stableCenter.x - currentCenter.x, stableCenter.y - currentCenter.y);
        return true;
    }
}
