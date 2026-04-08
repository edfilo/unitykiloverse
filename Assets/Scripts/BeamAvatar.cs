using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;

/// <summary>
/// Simple glowing orb component. Attach to a sphere to make it glow.
/// </summary>
[RequireComponent(typeof(MeshRenderer))]
public class BeamAvatar : MonoBehaviour
{
    [Header("Appearance")]
    [ColorUsage(false, true)]
    public Color glowColor = new Color(1f, 0.8f, 0.3f); // Warm orange glow
    
    [Range(0, 10)]
    public float emissionIntensity = 2f;
    
    [Range(0.1f, 10f)]
    public float orbSize = 2f; // Bigger for visibility
    
    [Header("Animation")]
    public bool floatAnimation = true;
    public float floatSpeed = 1f;
    public float floatHeight = 0.3f;

    public bool pulseAnimation = true;
    public float pulseSpeed = 2f;
    public float pulseAmount = 0.3f;

    [Header("Light Beam (Legacy)")]
    public bool showBeam = true;
    public float beamHeight = 4828f; // 3 miles
    [ColorUsage(false, true)]
    public Color beamColor = Color.white;
    [Range(0.1f, 10000f)]
    public float beamEmission = 100f;

    private Material material;
    private LineRenderer beamRenderer;
    private Vector3 startPosition;
    private float timeOffset;
    private bool externallyPositioned;
    
    [Header("Magical Particles")]
    public Material particleBeamMaterial; // Assign ParticleBeam.mat in inspector
    public bool useMagicalParticles = true;
    public Color particleStartColor = new Color(0f, 1f, 1f); // Legacy/Secondary
    public Color particleEndColor = new Color(1f, 0f, 1f);   // Legacy/Secondary

    // NEW SETTINGS FROM PROFILE
    public int particleCount = 2000;
    public Color particleEmissionColor = new Color(0f, 0.5f, 1f);
    public float particleSpeed = 5f;
    public float particleChaos = 0.5f;
    public float beamWidth = 1.3f; // Beam width in meters (emitter spacing/diameter)
    public float particleBaseSize = 0.65f; // Individual particle radius in meters

    // PARTICLE DETAIL SETTINGS
    public float particleSizeVariation = 2.5f;
    public float particleSizeOverLifetime = 0.8f;
    public float particleFadeIn = 0.1f;
    public float particleFadeOut = 0.2f;
    public float particleDensity = 1.0f;
    public float particleRotationSpeed = 45f;

    private ParticleSystem magicalParticles;
    private MeshRenderer orbRenderer;
    private GameObject avatarObject;
    private MeshRenderer avatarRenderer;
    private Material avatarMaterial;
    private string currentAvatarUrl;
    private Coroutine avatarRoutine;
    private float avatarShimmerOffset;
    private bool hasAvatarTexture;

    [Header("Hologram Avatar")]
    public float avatarHeight = 1.2f;
    public float avatarSize = 1.1f;
    public float avatarAlpha = 0.85f;
    public float avatarShimmerSpeed = 2.2f;
    public float avatarShimmerAmount = 0.25f;
    public bool hideOrbMesh = true;
    public Texture2D avatarPlaceholderTexture;
    public Color avatarPlaceholderTint = new Color(0.7f, 0.9f, 1f, 0.7f);

    private static readonly Dictionary<string, Texture2D> AvatarCache = new Dictionary<string, Texture2D>();
    private static readonly HashSet<string> AvatarInFlight = new HashSet<string>();

    void Start()
    {
        orbRenderer = GetComponent<MeshRenderer>();
        if (orbRenderer != null)
        {
            material = orbRenderer.material;
            UpdateEmission();
        }
        avatarShimmerOffset = UnityEngine.Random.Range(0f, 10f);
        // Initial build
        RebuildBeamSystem();
        // Ensure we never show the sphere; use avatar or placeholder instead
        EnsureAvatarObject();
        ApplyPlaceholder();
        SetAvatarActive(true);
    }

    // Call this if settings change or to force a rebuild
    public void RebuildBeamSystem()
    {
        // Cleanup existing
        if (magicalParticles != null) Destroy(magicalParticles.gameObject);
        if (beamRenderer != null) Destroy(beamRenderer.gameObject);

        // Create light beam or particles
        if (showBeam)
        {
            if (useMagicalParticles)
            {
                CreateMagicalParticles();
            }
            else
            {
                CreateBeam();
            }
        }
    }

    void CreateMagicalParticles()
    {
        GameObject pObj = new GameObject("MagicalParticles");
        pObj.transform.SetParent(transform);
        pObj.transform.localPosition = Vector3.zero;
        pObj.transform.localRotation = Quaternion.Euler(-90, 0, 0);

        magicalParticles = pObj.AddComponent<ParticleSystem>();
        ParticleSystemRenderer psr = pObj.GetComponent<ParticleSystemRenderer>();

        // Use pre-configured material to prevent shader stripping on mobile
        if (particleBeamMaterial == null)
        {
            particleBeamMaterial = Resources.Load<Material>("Materials/ParticleBeam");
        }
        if (particleBeamMaterial != null)
        {
            psr.material = new Material(particleBeamMaterial); // Instance of the reference material
            Debug.Log($"[BeamAvatar] Using material: {particleBeamMaterial.name} with shader: {particleBeamMaterial.shader.name}");
        }
        else
        {
            // Fallback: Try to find shader (may fail on mobile builds)
            Debug.LogWarning("[BeamAvatar] ParticleBeam material not assigned! Particle beams may not appear on mobile devices.");
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader != null)
            {
                psr.material = new Material(shader);
            }
            else
            {
                Debug.LogError("[BeamAvatar] No particle shader found! Beams will not render.");
                return;
            }
        }
        
        // Generate soft particle texture
        Texture2D softTex = CreateSoftParticleTexture();
        psr.material.mainTexture = softTex;
        psr.material.SetTexture("_BaseMap", softTex);
        
        // Configure for additive blending
        psr.material.SetFloat("_Surface", 1.0f); // Transparent
        psr.material.SetFloat("_Blend", 0.0f);   // Alpha
        psr.material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        psr.material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        psr.material.SetInt("_ZWrite", 0);
        psr.material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        psr.material.EnableKeyword("_EMISSION");
        psr.material.renderQueue = 3000;

        // Apply dynamic settings
        ApplyParticleSettings();

        // Force play — playOnAwake may not trigger if parent was inactive
        magicalParticles.Play();
        Debug.Log($"[BeamAvatar] Particle system created and playing: particleCount={magicalParticles.particleCount}, isPlaying={magicalParticles.isPlaying}, emission={magicalParticles.emission.rateOverTime.constant:F0}");
    }

    /// <summary>
    /// Updates particle properties efficiently without destroying the system.
    /// Call this for live updates (sliders).
    /// </summary>
    public void ApplyParticleSettings()
    {
        if (magicalParticles == null) return;

        ParticleSystemRenderer psr = magicalParticles.GetComponent<ParticleSystemRenderer>();
        if (psr == null || psr.material == null) return;

        // Force Visual Settings
        psr.renderMode = ParticleSystemRenderMode.Billboard;
        psr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        psr.receiveShadows = false;

        // Colors
        Color c1 = particleEmissionColor;
        Color c2 = new Color(c1.r * 0.5f + 0.5f, c1.g * 0.2f, c1.b + 0.2f);
        Color c3 = Color.white;

        psr.material.SetColor("_BaseColor", c1);
        psr.material.SetColor("_Color", c1);
        psr.material.SetColor("_EmissionColor", c1 * 20f);

        // Platform-specific particle count limits
        int maxParticles = particleCount;
        #if UNITY_IOS || UNITY_ANDROID
        maxParticles = Mathf.Min(particleCount, 1000); // Limit to 1000 on mobile
        #endif

        var main = magicalParticles.main;
        float targetHeight = 150f;
        float safeSpeed = Mathf.Max(0.5f, particleSpeed);
        float lifetime = targetHeight / safeSpeed;

        main.startLifetime = lifetime;
        main.startSpeed = safeSpeed;
        main.startSize = new ParticleSystem.MinMaxCurve(particleBaseSize / particleSizeVariation, particleBaseSize * particleSizeVariation);
        main.startColor = new ParticleSystem.MinMaxGradient(c1, c2);
        main.maxParticles = maxParticles + 500;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.gravityModifier = -0.01f;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);

        var emission = magicalParticles.emission;
        if (lifetime > 0)
        {
            emission.rateOverTime = (maxParticles / lifetime) * particleDensity;
        }

        var shape = magicalParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 1.0f + (particleChaos * 2f);
        shape.radius = beamWidth / 2f;
        shape.position = Vector3.zero;
        shape.rotation = Vector3.zero;

        var vel = magicalParticles.velocityOverLifetime;
        vel.enabled = true;
        float wobble = particleChaos * Mathf.Sqrt(safeSpeed / 5f);
        vel.x = new ParticleSystem.MinMaxCurve(-wobble, wobble);
        vel.y = new ParticleSystem.MinMaxCurve(-wobble, wobble);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.space = ParticleSystemSimulationSpace.Local;

        var col = magicalParticles.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        float fadeInEnd = Mathf.Clamp01(particleFadeIn);
        float fadeOutStart = Mathf.Clamp01(1f - particleFadeOut);
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(c1, 0.0f),
                new GradientColorKey(c2, 0.5f),
                new GradientColorKey(c3, 1.0f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, fadeInEnd),
                new GradientAlphaKey(1f, fadeOutStart),
                new GradientAlphaKey(0f, 1f)
            }
        );
        col.color = grad;

        var sizeOverLife = magicalParticles.sizeOverLifetime;
        sizeOverLife.enabled = particleSizeOverLifetime != 1.0f;
        if (sizeOverLife.enabled)
        {
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 1f);
            sizeCurve.AddKey(1f, particleSizeOverLifetime);
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);
        }

        var rotOverLife = magicalParticles.rotationOverLifetime;
        rotOverLife.enabled = particleRotationSpeed > 0f;
        if (rotOverLife.enabled)
        {
            rotOverLife.z = new ParticleSystem.MinMaxCurve(particleRotationSpeed * Mathf.Deg2Rad);
        }

        var lights = magicalParticles.lights;
        if (lights.light != null)
        {
            lights.light.color = c1;
            lights.light.range = beamWidth * 10f;
        }

        if (psr.trailMaterial != null)
        {
            psr.trailMaterial.SetColor("_BaseColor", c1);
            psr.trailMaterial.SetColor("_Color", c1);
            psr.trailMaterial.SetColor("_EmissionColor", c1 * 20f);
            psr.trailMaterial.SetFloat("_Surface", 1.0f);
            psr.trailMaterial.SetFloat("_Blend", 2.0f);
            psr.trailMaterial.EnableKeyword("_EMISSION");
        }
    }


    // Generate a simple soft circle texture
    Texture2D CreateSoftParticleTexture()
    {
        int resolution = 64;
        Texture2D tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
        Color[] colors = new Color[resolution * resolution];
        float center = resolution * 0.5f;
        float radius = resolution * 0.45f;

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float t = Mathf.Clamp01(dist / radius);
                // Soft radial gradient (Inverse square or simple linear)
                float alpha = Mathf.Pow(1f - t, 2f); 
                colors[y * resolution + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        tex.SetPixels(colors);
        tex.Apply();
        return tex;
    }

    void CreateBeam()
    {
        // Cleanup if switching types
        if (beamRenderer != null) Destroy(beamRenderer.gameObject); 
        
        // Create child GameObject for beam
        GameObject beamObj = new GameObject("LightBeam");
        beamObj.transform.SetParent(transform);
        beamObj.transform.localPosition = Vector3.zero;

        // Add LineRenderer
        beamRenderer = beamObj.AddComponent<LineRenderer>();

        // Set up beam material with bright white emission
        Material beamMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        beamMat.EnableKeyword("_EMISSION");

        beamMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

        beamRenderer.material = beamMat;

        // Configure beam appearance - visible from miles away
        beamRenderer.positionCount = 2;
        beamRenderer.useWorldSpace = true; // Use world space for proper positioning

        // Ensure LineRenderer doesn't get culled at distance
        beamRenderer.allowOcclusionWhenDynamic = false;

        // Set beam positions in world space
        // Vector3 orbWorldPos = transform.position; // This will be updated in UpdateBeamAppearance
        // beamRenderer.SetPosition(0, orbWorldPos);
        // beamRenderer.SetPosition(1, orbWorldPos + Vector3.up * beamHeight);

        // Disable shadows for performance
        beamRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        beamRenderer.receiveShadows = false;

        UpdateBeamAppearance();
    }
    /// <summary>
    /// Ensure particle system is playing (called when beam is activated)
    /// </summary>
    public void PlayParticles()
    {
        if (magicalParticles != null && !magicalParticles.isPlaying)
        {
            magicalParticles.Play();
            Debug.Log("[BeamAvatar] Started particle system");
        }
    }

    /// <summary>
    /// Update the start position for float animation. Call this when moving the orb.
    /// </summary>
    public void SetPosition(Vector3 newPosition)
    {
        startPosition = newPosition;
        transform.position = newPosition;
        externallyPositioned = true;
    }

    void Update()
    {
        if (material != null)
        {
            UpdateEmission();
        }

        UpdateAvatarVisual();

        // Float animation — only if not externally positioned (VirtualGridSpawner calls SetPosition every frame)
        if (floatAnimation && !externallyPositioned)
        {
            float yOffset = Mathf.Sin((Time.time + timeOffset) * floatSpeed) * floatHeight;
            transform.position = startPosition + Vector3.up * yOffset;
        }

        // Pulse animation (scale)
        if (pulseAnimation)
        {
            float pulse = 1f + Mathf.Sin((Time.time + timeOffset) * pulseSpeed) * pulseAmount;
            transform.localScale = Vector3.one * orbSize * pulse;
        }

        // Update beam position if it exists and we're animating
        if (beamRenderer != null && beamRenderer.useWorldSpace)
        {
            UpdateBeamAppearance();
            Vector3 orbWorldPos = transform.position;
            beamRenderer.SetPosition(0, orbWorldPos);
            beamRenderer.SetPosition(1, orbWorldPos + Vector3.up * beamHeight);
        }
    }
    
    void UpdateEmission()
    {
        Color emission = glowColor * emissionIntensity;
        material.SetColor("_EmissionColor", emission);
        material.SetColor("_BaseColor", glowColor * 0.5f); // Dimmer base color
    }
    
    void OnValidate()
    {
        // Update in editor and play mode
        if (material != null) UpdateEmission();
        if (beamRenderer != null) UpdateBeamAppearance();
        transform.localScale = Vector3.one * orbSize;
        if (orbRenderer != null && hideOrbMesh)
        {
            orbRenderer.enabled = false;
        }
    }

    void OnDestroy()
    {
        // Clean up material instances
        if (material != null)
        {
            Destroy(material);
        }
        if (avatarMaterial != null)
        {
            Destroy(avatarMaterial);
        }
        if (beamRenderer != null && beamRenderer.material != null)
        {
            Destroy(beamRenderer.material);
        }
    }

    private void UpdateBeamAppearance()
    {
        if (beamRenderer == null || beamRenderer.material == null) return;
        beamRenderer.startWidth = beamWidth;
        beamRenderer.endWidth = beamWidth * 0.5f;
        ApplyBeamMaterial(beamRenderer.material);
    }

    private void ApplyBeamMaterial(Material beamMat)
    {
        Color hdrColor = beamColor * beamEmission;
        beamMat.SetColor("_BaseColor", beamColor);
        beamMat.SetColor("_EmissionColor", hdrColor);
        beamMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
    }

    public void SetAvatarUrl(string url)
    {
        if (string.Equals(currentAvatarUrl, url, StringComparison.OrdinalIgnoreCase)) return;
        currentAvatarUrl = url;

        if (string.IsNullOrEmpty(url))
        {
            EnsureAvatarObject();
            ApplyPlaceholder();
            SetAvatarActive(true);
            return;
        }

        if (AvatarCache.TryGetValue(url, out var cached))
        {
            ApplyAvatarTexture(cached);
            return;
        }

        if (AvatarInFlight.Contains(url)) return;
        AvatarInFlight.Add(url);
        if (avatarRoutine != null) StopCoroutine(avatarRoutine);
        avatarRoutine = StartCoroutine(LoadAvatarTexture(url));
    }

    private IEnumerator LoadAvatarTexture(string url)
    {
        using var req = UnityWebRequestTexture.GetTexture(url);
        yield return req.SendWebRequest();

        AvatarInFlight.Remove(url);
        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[BeamAvatar] Avatar download failed: {req.error}");
            yield break;
        }

        var tex = DownloadHandlerTexture.GetContent(req);
        if (tex == null)
        {
            Debug.LogWarning("[BeamAvatar] Avatar texture missing.");
            yield break;
        }

        AvatarCache[url] = tex;
        if (string.Equals(currentAvatarUrl, url, StringComparison.OrdinalIgnoreCase))
        {
            ApplyAvatarTexture(tex);
        }
    }

    private void ApplyAvatarTexture(Texture2D tex)
    {
        EnsureAvatarObject();
        if (avatarRenderer == null) return;

        hasAvatarTexture = true;
        avatarMaterial.mainTexture = tex;
        avatarMaterial.SetTexture("_BaseMap", tex);
        SetAvatarActive(true);
    }

    private void ApplyPlaceholder()
    {
        EnsureAvatarObject();
        if (avatarRenderer == null) return;

        hasAvatarTexture = false;
        if (avatarPlaceholderTexture != null)
        {
            avatarMaterial.mainTexture = avatarPlaceholderTexture;
            avatarMaterial.SetTexture("_BaseMap", avatarPlaceholderTexture);
        }
        else
        {
            avatarMaterial.mainTexture = null;
            avatarMaterial.SetTexture("_BaseMap", null);
        }
        avatarMaterial.SetColor("_BaseColor", avatarPlaceholderTint);
        avatarMaterial.SetColor("_Color", avatarPlaceholderTint);
    }

    private void EnsureAvatarObject()
    {
        if (avatarObject != null) return;

        avatarObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
        avatarObject.name = "BeamAvatar";
        avatarObject.transform.SetParent(transform, false);
        avatarObject.transform.localPosition = new Vector3(0f, avatarHeight, 0f);
        avatarObject.transform.localRotation = Quaternion.identity;
        avatarObject.transform.localScale = Vector3.one * avatarSize;

        Destroy(avatarObject.GetComponent<Collider>());
        avatarRenderer = avatarObject.GetComponent<MeshRenderer>();

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Transparent");
        avatarMaterial = new Material(shader);
        avatarMaterial.SetColor("_BaseColor", new Color(0.6f, 0.9f, 1f, avatarAlpha));
        avatarMaterial.SetColor("_Color", new Color(0.6f, 0.9f, 1f, avatarAlpha));
        avatarMaterial.renderQueue = 3000;
        avatarRenderer.material = avatarMaterial;
        avatarRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        avatarRenderer.receiveShadows = false;

        ApplyPlaceholder();
    }

    private void SetAvatarActive(bool active)
    {
        if (orbRenderer != null)
        {
            orbRenderer.enabled = hideOrbMesh ? false : !active;
        }
        if (avatarObject != null)
        {
            avatarObject.SetActive(active);
        }
    }

    private void UpdateAvatarVisual()
    {
        if (avatarObject == null || avatarRenderer == null || avatarMaterial == null || !avatarObject.activeSelf)
        {
            return;
        }

        if (Camera.main != null)
        {
            var cam = Camera.main.transform;
            avatarObject.transform.rotation = Quaternion.LookRotation(avatarObject.transform.position - cam.position);
        }

        float shimmer = Mathf.Sin((Time.time + avatarShimmerOffset) * avatarShimmerSpeed) * avatarShimmerAmount;
        float alpha = Mathf.Clamp01((hasAvatarTexture ? avatarAlpha : avatarPlaceholderTint.a) + shimmer);
        var baseColor = hasAvatarTexture
            ? new Color(0.6f, 0.9f, 1f, alpha)
            : new Color(avatarPlaceholderTint.r, avatarPlaceholderTint.g, avatarPlaceholderTint.b, alpha);
        avatarMaterial.SetColor("_BaseColor", baseColor);
        avatarMaterial.SetColor("_Color", baseColor);
    }
}
