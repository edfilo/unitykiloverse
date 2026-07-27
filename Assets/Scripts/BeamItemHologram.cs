using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

/// Masked, glitching item thumbnail displayed at the top of a signal beam.
/// The beam keeps its own particles; the item visual itself uses no particles.
public sealed class BeamItemHologram : MonoBehaviour
{
    private const float TopOffsetMeters = 6f;
    // Natural perspective is preserved until an item becomes very small. Farther
    // items are then enlarged just enough to maintain this fraction of viewport
    // height; close items continue using their authored world size unchanged.
    private static float targetViewportHeight = 0.045f;
    private static float baseWorldSize = 10f;
    private static float maxWorldSize = 180f;
    private const float CloseEngagementMeters = 77f; // ≈ 100 walking steps at 1.3 steps/metre.
    private static float glitchAmount = 0.62f;
    private static float insectCruiseY = 20f;
    private static float insectCeilingY = 40f;
    private static float insectVisitInterval = 20f;
    private static float insectApproachSeconds = 6.5f;
    private static float insectHoverSeconds = 4f;
    private static float insectReturnSeconds = 4.5f;
    private static float insectCameraClearance = 24f;
    private static float insectCuriosityRadius = 5f;
    private static float insectCuriositySpeed = .18f;
    private static float insectApproachMeander = 6f;
    private static float insectInvestigationLift = 7.5f;
    private static float nextScheduledVisitAt = -1f;
    private static bool tuningLoaded;
    private static readonly Dictionary<string, Texture2D> TextureCache = new();
    private static readonly HashSet<BeamItemHologram> LiveItems = new();
    private static readonly HashSet<BeamItemHologram> CameraAttackers = new();
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
    private float noiseSeedX;
    private float noiseSeedY;
    private float noiseSeedZ;
    private float floatSpeedSeedY;
    private float approachEndsAt;
    private float approachStartedAt;
    private float approachWorldSpeed;
    private Vector3 approachStartPosition;
    private Vector3 approachCurveOffset;
    private float returnStartedAt;
    private float returnEndsAt;
    private Vector3 returnStartPosition;
    private float investigationEndsAt;
    private Vector3 flightPosition;
    private bool hasFlightPosition;
    private FlightMode flightMode;

    private enum FlightMode
    {
        Roaming,
        Approaching,
        Investigating,
        Returning
    }

    private void Awake()
    {
        LoadTuning();
        floatPhase = Mathf.Repeat(GetInstanceID() * .6180339f, 100f);
        noiseSeedX = Mathf.Repeat(Mathf.Abs(GetInstanceID()) * .4142136f, 1000f);
        noiseSeedY = Mathf.Repeat(Mathf.Abs(GetInstanceID()) * .7320508f, 1000f);
        noiseSeedZ = Mathf.Repeat(Mathf.Abs(GetInstanceID()) * .7548777f, 1000f);
        floatSpeedSeedY = Mathf.Repeat(Mathf.Abs(GetInstanceID()) * .5698403f, 1f);
        if (nextScheduledVisitAt < 0f) nextScheduledVisitAt = Time.time + insectVisitInterval;
    }

    private void OnEnable()
    {
        LiveItems.Add(this);
    }

    private void OnDisable()
    {
        LiveItems.Remove(this);
        CameraAttackers.Remove(this);
        flightMode = FlightMode.Roaming;
    }

    private void OnDestroy()
    {
        LiveItems.Remove(this);
        CameraAttackers.Remove(this);
    }

    private static void LoadTuning()
    {
        if (tuningLoaded) return;
        tuningLoaded = true;
        insectCruiseY = PlayerPrefs.GetFloat("itemInsectCruiseY", insectCruiseY);
        insectCeilingY = PlayerPrefs.GetFloat("itemInsectCeilingY", insectCeilingY);
        insectVisitInterval = PlayerPrefs.GetFloat("itemInsectVisitInterval", insectVisitInterval);
        insectApproachSeconds = PlayerPrefs.GetFloat("itemInsectApproachSeconds", insectApproachSeconds);
        insectHoverSeconds = PlayerPrefs.GetFloat("itemInsectHoverSeconds", insectHoverSeconds);
        insectReturnSeconds = PlayerPrefs.GetFloat("itemInsectReturnSeconds", insectReturnSeconds);
        insectCameraClearance = PlayerPrefs.GetFloat("itemInsectCameraClearance", insectCameraClearance);
        insectCuriosityRadius = PlayerPrefs.GetFloat("itemInsectCuriosityRadius", insectCuriosityRadius);
        insectCuriositySpeed = PlayerPrefs.GetFloat("itemInsectCuriositySpeed", insectCuriositySpeed);
        insectApproachMeander = PlayerPrefs.GetFloat("itemInsectApproachMeander", insectApproachMeander);
        insectInvestigationLift = PlayerPrefs.GetFloat("itemInsectInvestigationLift", insectInvestigationLift);
        baseWorldSize = PlayerPrefs.GetFloat("itemBaseSize", baseWorldSize);
        targetViewportHeight = PlayerPrefs.GetFloat("itemViewportHeight", targetViewportHeight);
        maxWorldSize = PlayerPrefs.GetFloat("itemMaxWorldSize", maxWorldSize);
        NormalizeFloatTuning();
    }

    private static void NormalizeFloatTuning()
    {
        insectCruiseY = Mathf.Clamp(insectCruiseY, 2f, 100f);
        insectCeilingY = Mathf.Clamp(insectCeilingY, insectCruiseY, 120f);
        insectVisitInterval = Mathf.Clamp(insectVisitInterval, 5f, 120f);
        insectApproachSeconds = Mathf.Clamp(insectApproachSeconds, 1f, 30f);
        insectHoverSeconds = Mathf.Clamp(insectHoverSeconds, .5f, 20f);
        insectReturnSeconds = Mathf.Clamp(insectReturnSeconds, .5f, 20f);
        insectCameraClearance = Mathf.Clamp(insectCameraClearance, 6f, 80f);
        insectCuriosityRadius = Mathf.Clamp(insectCuriosityRadius, 1f, 16f);
        insectCuriositySpeed = Mathf.Clamp(insectCuriositySpeed, .03f, .5f);
        insectApproachMeander = Mathf.Clamp(insectApproachMeander, 0f, 18f);
        insectInvestigationLift = Mathf.Clamp(insectInvestigationLift, 0f, 24f);
        baseWorldSize = Mathf.Clamp(baseWorldSize, 2f, 40f);
        targetViewportHeight = Mathf.Clamp(targetViewportHeight, .015f, .12f);
        maxWorldSize = Mathf.Max(baseWorldSize, Mathf.Clamp(maxWorldSize, 16f, 180f));
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
        EndCameraAttack();
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
            material.SetFloat("_UseMainAlpha", 0f);
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
        visual.transform.localScale = Vector3.one * baseWorldSize;
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
        bool hasEmbeddedAlpha = HasTransparentBackground(color);
        material.SetFloat("_UseMainAlpha", hasEmbeddedAlpha ? 1f : 0f);
        material.SetFloat("_DebugSolid", 0f);
        visual.SetActive(true);
        artworkReady = true;

        bool hasMask = false;
        // The legacy depth URL was also used as an opacity mask. A semantic
        // alpha-bearing PNG no longer needs that request, and depth luminance
        // must never override its silhouette.
        if (!hasEmbeddedAlpha && !string.IsNullOrWhiteSpace(depthUrl))
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
        Debug.Log($"[BeamItemGlitch] ready beam={name} image={color.width}x{color.height} embeddedAlpha={hasEmbeddedAlpha} mask={hasMask} localY={visual.transform.localPosition.y:F1} scale={visual.transform.localScale.x:F1}");
        loadRoutine = null;
    }

    private static bool HasTransparentBackground(Texture2D texture)
    {
        if (texture == null || !texture.isReadable) return false;
        try
        {
            int lastX = Mathf.Max(0, texture.width - 1);
            int lastY = Mathf.Max(0, texture.height - 1);
            // Spawn artwork is generated with generous padding. Transparent
            // corners reliably distinguish new RGBA cutouts from legacy opaque
            // black-matte PNGs without scanning or allocating the full texture.
            return texture.GetPixel(0, 0).a < .98f ||
                   texture.GetPixel(lastX, 0).a < .98f ||
                   texture.GetPixel(0, lastY).a < .98f ||
                   texture.GetPixel(lastX, lastY).a < .98f;
        }
        catch
        {
            return false;
        }
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

        var cam = Camera.main;
        Vector3 normalFlightPosition = CalculateInsectFlightPosition(beamTop, cam);
        Vector3 worldTop = ResolveInsectFlightPosition(normalFlightPosition, cam);
        // Preserve natural world-space perspective up close. Once ordinary
        // perspective would make a distant item smaller than the authored
        // screen floor, increase only its world size. This is one distance/FOV
        // calculation per visible quad and adds no renderer or texture cost.
        float worldSize = baseWorldSize;
        if (cam != null && !cam.orthographic)
        {
            float cameraDistance = Vector3.Distance(cam.transform.position, worldTop);
            float readableFarSize = 2f * cameraDistance *
                Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad * .5f) * targetViewportHeight;
            worldSize = Mathf.Clamp(Mathf.Max(baseWorldSize, readableFarSize), baseWorldSize, maxWorldSize);
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

    private Vector3 CalculateInsectFlightPosition(Vector3 beamAnchor, Camera cam)
    {
        // Spend most of the cycle cruising in the sky. Short takeoff/landing
        // shoulders still let the item visit its home anchor without making the
        // whole collection repeatedly fall to the ground.
        float speed = Mathf.Lerp(.055f, .095f, floatSpeedSeedY);
        float cycle = Mathf.Repeat(Time.time * speed + floatPhase, 1f);
        float airborne;
        if (cycle < .08f) airborne = 0f;
        else if (cycle < .20f) airborne = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.08f, .20f, cycle));
        else if (cycle < .88f) airborne = 1f;
        else airborne = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(.88f, 1f, cycle));

        float noiseTime = Time.time * speed;
        float brownianX = (Mathf.PerlinNoise(noiseSeedX, noiseTime) - .5f) * 2f;
        float brownianY = (Mathf.PerlinNoise(noiseSeedY, noiseTime * 1.13f) - .5f) * 2f;
        float brownianZ = (Mathf.PerlinNoise(noiseSeedZ, noiseTime * .91f) - .5f) * 2f;
        GetRoamingHeightRange(cam, transform.position, out float effectiveCruiseY, out float effectiveCeilingY);
        float landingCenterY = transform.position.y + baseWorldSize * .5f;
        float cruiseWave = .5f + .5f * brownianY;
        float cruiseY = transform.position.y + Mathf.Lerp(effectiveCruiseY, effectiveCeilingY, cruiseWave);

        Vector3 position = beamAnchor;
        position.y = Mathf.Lerp(landingCenterY, cruiseY, airborne);
        position.x += brownianX * 2f * airborne;
        position.z += brownianZ * 2f * airborne;
        return position;
    }

    // Close markers should read as objects sitting just off the street in
    // front of their building — it is fine for them to be below the horizon.
    // From ~200 steps (150m) out the range curves up so distant markers keep
    // the tuned high sky float on the horizon by roughly half a mile (800m).
    // The near center keeps the quad bottom about a meter above the ground.
    private static void GetRoamingHeightRange(Camera cam, Vector3 groundPosition, out float cruiseY, out float ceilingY)
    {
        const float NearBlendStartMeters = 150f;
        const float FarBlendEndMeters = 800f;
        if (cam == null)
        {
            cruiseY = insectCruiseY;
            ceilingY = insectCeilingY;
            return;
        }

        Vector3 cameraGround = cam.transform.position;
        cameraGround.y = groundPosition.y;
        float horizontalDistance = Vector3.Distance(cameraGround, groundPosition);
        float distanceBlend = Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(NearBlendStartMeters, FarBlendEndMeters, horizontalDistance));
        float closeCruise = baseWorldSize * .5f + 1f;
        float closeCeiling = closeCruise + 2f;
        cruiseY = Mathf.Lerp(closeCruise, insectCruiseY, distanceBlend);
        ceilingY = Mathf.Max(cruiseY, Mathf.Lerp(closeCeiling, insectCeilingY, distanceBlend));
    }

    private Vector3 ResolveInsectFlightPosition(Vector3 normalPosition, Camera cam)
    {
        if (cam == null)
        {
            EndCameraAttack();
            return normalPosition;
        }

        if (!hasFlightPosition)
        {
            flightPosition = normalPosition;
            hasFlightPosition = true;
        }

        if (flightMode == FlightMode.Roaming)
        {
            flightPosition = normalPosition;
            TryBeginCameraAttack(normalPosition, cam);
        }

        float effectiveClearance = Mathf.Max(insectCameraClearance, baseWorldSize * 2.2f);
        Vector3 cameraTarget = cam.transform.position + cam.transform.forward * effectiveClearance;
        float buzzTime = Time.time * insectCuriositySpeed;
        float buzzX = (Mathf.PerlinNoise(noiseSeedX + 73f, buzzTime) - .5f) * 2f;
        float buzzY = (Mathf.PerlinNoise(noiseSeedY + 41f, buzzTime * 1.21f) - .5f) * 2f;
        float buzzZ = (Mathf.PerlinNoise(noiseSeedZ + 29f, buzzTime * .83f) - .5f) * 2f;
        float curiousPulseX = Mathf.Sin(Time.time * 1.37f + floatPhase) * .42f;
        float curiousPulseY = Mathf.Sin(Time.time * 1.09f + floatPhase * .73f) * .36f;
        float curiousPulseZ = Mathf.Sin(Time.time * .83f + floatPhase * 1.17f) * .28f;

        if (flightMode == FlightMode.Approaching)
        {
            // Follow a broad quadratic arc instead of snapping down a straight
            // sightline. Total travel time remains distance-derived and exact.
            float progress = Mathf.Clamp01(Mathf.InverseLerp(approachStartedAt, approachEndsAt, Time.time));
            float eased = progress * progress * (3f - 2f * progress);
            float inverse = 1f - eased;
            Vector3 control = Vector3.Lerp(approachStartPosition, cameraTarget, .5f) + approachCurveOffset;
            flightPosition = inverse * inverse * approachStartPosition
                + 2f * inverse * eased * control
                + eased * eased * cameraTarget;
            float arcEnvelope = Mathf.Sin(progress * Mathf.PI);
            float meanderX = buzzX + curiousPulseX;
            float meanderY = buzzY + curiousPulseY;
            float meanderZ = buzzZ + curiousPulseZ;
            flightPosition += cam.transform.right * meanderX * insectApproachMeander * arcEnvelope;
            flightPosition += cam.transform.up * meanderY * insectApproachMeander * .62f * arcEnvelope;
            flightPosition += cam.transform.forward * meanderZ * insectApproachMeander * .35f * arcEnvelope;
            if (progress >= 1f)
            {
                flightMode = FlightMode.Investigating;
                investigationEndsAt = Time.time + insectHoverSeconds;
            }
            return flightPosition;
        }

        if (flightMode == FlightMode.Investigating)
        {
            // A three-dimensional camera-relative curiosity volume keeps the
            // visitor alive: it peers side-to-side, rises/falls, and probes depth.
            Vector3 curiousTarget = cameraTarget
                + cam.transform.up * insectInvestigationLift
                + cam.transform.right * (buzzX + curiousPulseX) * insectCuriosityRadius
                + cam.transform.up * (buzzY + curiousPulseY) * insectCuriosityRadius * .7f
                + cam.transform.forward * (buzzZ + curiousPulseZ) * insectCuriosityRadius * .45f;
            float responsiveness = 1f - Mathf.Exp(-2.4f * Time.deltaTime);
            flightPosition = Vector3.Lerp(flightPosition, curiousTarget, responsiveness);
            if (Time.time >= investigationEndsAt)
            {
                BeginReturn(normalPosition);
            }
            return flightPosition;
        }

        if (flightMode == FlightMode.Returning)
        {
            float progress = Mathf.Clamp01(Mathf.InverseLerp(returnStartedAt, returnEndsAt, Time.time));
            float eased = progress * progress * (3f - 2f * progress);
            flightPosition = Vector3.Lerp(returnStartPosition, normalPosition, eased);
            if (progress >= 1f)
            {
                flightMode = FlightMode.Roaming;
                approachWorldSpeed = 0f;
            }
            return flightPosition;
        }

        return normalPosition;
    }

    private void TryBeginCameraAttack(Vector3 normalPosition, Camera cam)
    {
        if (Time.time < nextScheduledVisitAt || CameraAttackers.Count > 0) return;

        // Every interval, select one visible item. Anything within roughly 100
        // walking steps outranks every farther beam; beyond that, distance is
        // strongly weighted with a small per-cycle random factor for variety.
        bool thisItemIsVisible = IsInsideInvestigationViewport(cam, normalPosition);
        if (!thisItemIsVisible || !IsPreferredVisitCandidate(cam, normalPosition)) return;

        flightPosition = normalPosition;
        hasFlightPosition = true;
        flightMode = FlightMode.Approaching;
        float effectiveClearance = Mathf.Max(insectCameraClearance, baseWorldSize * 2.2f);
        Vector3 cameraTarget = cam.transform.position + cam.transform.forward * effectiveClearance;
        float travelDistance = Vector3.Distance(flightPosition, cameraTarget);
        approachWorldSpeed = travelDistance / Mathf.Max(.1f, insectApproachSeconds);
        approachStartedAt = Time.time;
        approachEndsAt = approachStartedAt + insectApproachSeconds;
        approachStartPosition = flightPosition;
        float sideSign = (GetInstanceID() & 1) == 0 ? 1f : -1f;
        float curveWidth = Mathf.Clamp(travelDistance * .08f, 4f, 18f);
        float curveLift = Mathf.Clamp(travelDistance * .03f, 2f, 8f);
        approachCurveOffset = cam.transform.right * curveWidth * sideSign + cam.transform.up * curveLift;
        nextScheduledVisitAt = Time.time + insectVisitInterval;
        CameraAttackers.Add(this);
        Debug.Log($"[BeamItemFlight] visit beam={name} distance={travelDistance:F1}m closePriority={HorizontalDistance(cam.transform.position, normalPosition) <= CloseEngagementMeters} inbound={insectApproachSeconds:F1}s avgSpeed={approachWorldSpeed:F2}m/s viewDistance={effectiveClearance:F1}m curiosity={insectCuriosityRadius:F1}m hover={insectHoverSeconds:F1}s return={insectReturnSeconds:F1}s");
    }

    private static bool IsInsideInvestigationViewport(Camera cam, Vector3 worldPosition)
    {
        if (cam == null) return false;
        Vector3 viewport = cam.WorldToViewportPoint(worldPosition);
        return viewport.z > 0f && viewport.x >= .04f && viewport.x <= .96f &&
               viewport.y >= .06f && viewport.y <= .94f;
    }

    private bool IsPreferredVisitCandidate(Camera cam, Vector3 candidatePosition)
    {
        float candidateDistance = HorizontalDistance(cam.transform.position, candidatePosition);
        bool candidateIsClose = candidateDistance <= CloseEngagementMeters;
        float candidateScore = VisitSelectionScore(this, candidateDistance);
        foreach (var item in LiveItems)
        {
            if (item == null || item == this || item.flightMode != FlightMode.Roaming ||
                !item.isActiveAndEnabled || item.visual == null ||
                !item.visual.activeInHierarchy) continue;
            Vector3 otherPosition = item.visual.transform.position;
            if (!IsInsideInvestigationViewport(cam, otherPosition)) continue;
            float otherDistance = HorizontalDistance(cam.transform.position, otherPosition);
            bool otherIsClose = otherDistance <= CloseEngagementMeters;
            if (otherIsClose != candidateIsClose)
            {
                if (otherIsClose) return false;
                continue;
            }

            // At close range, engagement should feel intentional: the nearest
            // item wins. Outside it, the weighted score usually favors closer
            // beams but occasionally allows a neighboring one to investigate.
            float otherScore = candidateIsClose ? otherDistance : VisitSelectionScore(item, otherDistance);
            float ownScore = candidateIsClose ? candidateDistance : candidateScore;
            if (otherScore < ownScore - .01f) return false;
            if (Mathf.Abs(otherScore - ownScore) <= .01f && item.GetInstanceID() < GetInstanceID()) return false;
        }
        return true;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    private static float VisitSelectionScore(BeamItemHologram item, float distance)
    {
        int cycle = Mathf.FloorToInt(Time.time / Mathf.Max(1f, insectVisitInterval));
        float noise = Mathf.Sin(item.GetInstanceID() * 12.9898f + cycle * 78.233f) * 43758.5453f;
        float randomFactor = Mathf.Lerp(.72f, 1.28f, noise - Mathf.Floor(noise));
        return distance / randomFactor;
    }

    private void BeginReturn(Vector3 normalPosition)
    {
        flightMode = FlightMode.Returning;
        CameraAttackers.Remove(this);
        returnStartPosition = flightPosition;
        returnStartedAt = Time.time;
        returnEndsAt = returnStartedAt + insectReturnSeconds;
    }

    private void EndCameraAttack()
    {
        CameraAttackers.Remove(this);
        approachWorldSpeed = 0f;
        returnStartedAt = 0f;
        returnEndsAt = 0f;
        approachEndsAt = 0f;
        if (flightMode == FlightMode.Approaching || flightMode == FlightMode.Investigating)
            flightMode = FlightMode.Roaming;
    }

    public static void SetViewportHeight(float value)
    {
        targetViewportHeight = Mathf.Clamp(value, .015f, .12f);
    }

    public static void SetMaxWorldSize(float value)
    {
        maxWorldSize = Mathf.Max(baseWorldSize, Mathf.Clamp(value, 16f, 180f));
    }

    public static void SetBaseWorldSize(float value)
    {
        baseWorldSize = Mathf.Clamp(value, 2f, 40f);
        if (maxWorldSize < baseWorldSize) maxWorldSize = baseWorldSize;
    }

    public static void SetGlitchAmount(float value)
    {
        glitchAmount = Mathf.Clamp01(value);
        foreach (var item in FindObjectsByType<BeamItemHologram>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            item.material?.SetFloat("_GlitchAmount", glitchAmount);
    }

    public static void SetFloatMinY(float value)
    {
        // Retained for compatibility with older native overlays. Insect flight
        // now lands on the ground anchor instead of using an elevated minimum.
    }

    public static void SetFloatMaxY(float value)
    {
        SetInsectCeilingY(value);
    }

    public static void SetFloatWobbleX(float value) { }
    public static void SetFloatWobbleZ(float value) { }

    public static void SetInsectCameraClearance(float value)
    {
        insectCameraClearance = Mathf.Clamp(value, 6f, 80f);
    }

    public static void SetInsectCuriosityRadius(float value)
    {
        insectCuriosityRadius = Mathf.Clamp(value, 1f, 16f);
    }

    public static void SetInsectCuriositySpeed(float value)
    {
        insectCuriositySpeed = Mathf.Clamp(value, .03f, .5f);
    }

    public static void SetInsectApproachMeander(float value)
    {
        insectApproachMeander = Mathf.Clamp(value, 0f, 18f);
    }

    public static void SetInsectInvestigationLift(float value)
    {
        insectInvestigationLift = Mathf.Clamp(value, 0f, 24f);
    }

    public static void SetInsectAggressiveness(float value)
    {
        // Retired compatibility key. Visits now use a fixed global interval.
    }

    public static void SetFloatSpeedMin(float value)
    {
        SetInsectSpeedMin(value);
    }

    public static void SetFloatSpeedMax(float value)
    {
        SetInsectSpeedMax(value);
    }

    public static void SetInsectCruiseY(float value)
    {
        insectCruiseY = Mathf.Clamp(value, 2f, 100f);
        if (insectCeilingY < insectCruiseY) insectCeilingY = insectCruiseY;
    }

    public static void SetInsectCeilingY(float value)
    {
        insectCeilingY = Mathf.Clamp(value, insectCruiseY, 120f);
    }

    public static void SetInsectWanderRadius(float value)
    {
        // Retired compatibility key. Roaming uses a restrained fixed drift.
    }

    public static void SetInsectSpeedMin(float value)
    {
        // Retired compatibility key. Inbound speed is distance / travel time.
    }

    public static void SetInsectSpeedMax(float value)
    {
        // Retired compatibility key. Inbound speed is distance / travel time.
    }

    public static void SetInsectInvestigateSeconds(float value)
    {
        SetInsectHoverSeconds(value);
    }

    public static void SetInsectVisitInterval(float value)
    {
        insectVisitInterval = Mathf.Clamp(value, 5f, 120f);
        float requestedVisitAt = Time.time + insectVisitInterval;
        nextScheduledVisitAt = nextScheduledVisitAt < Time.time
            ? requestedVisitAt
            : Mathf.Min(nextScheduledVisitAt, requestedVisitAt);
    }

    public static void SetInsectApproachSeconds(float value)
    {
        insectApproachSeconds = Mathf.Clamp(value, 1f, 30f);
    }

    public static void SetInsectHoverSeconds(float value)
    {
        insectHoverSeconds = Mathf.Clamp(value, .5f, 20f);
    }

    public static void SetInsectReturnSeconds(float value)
    {
        insectReturnSeconds = Mathf.Clamp(value, .5f, 20f);
    }

    /// A label anchor that never follows the animated bob. It sits at the
    /// authored maximum Y plus half of the authored base item height. It never
    /// follows an item's independent flight or camera-attack path.
    public bool TryGetStableLabelScreenPoint(Camera cam, out Vector3 screenPoint)
    {
        screenPoint = default;
        if (cam == null) return false;
        Vector3 anchor = hasWorldAnchor
            ? worldAnchor
            : transform.position + Vector3.up * (beamHeight + TopOffsetMeters);
        GetRoamingHeightRange(cam, transform.position, out _, out float effectiveCeilingY);
        anchor.y = transform.position.y + effectiveCeilingY;
        anchor.y += baseWorldSize * .5f;
        screenPoint = cam.WorldToScreenPoint(anchor);
        return screenPoint.z > 0f;
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

    public bool TryGetCurrentWorldPosition(out Vector3 position)
    {
        position = visual != null ? visual.transform.position : transform.position;
        return visual != null && visual.activeInHierarchy;
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
