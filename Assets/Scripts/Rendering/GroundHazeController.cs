using UnityEngine;

/// Low-cost, camera-following smoke sheets for radioactive daytime atmosphere.
/// They render after opaque world geometry but before beam item thumbnails.
public sealed class GroundHazeController : MonoBehaviour
{
    private const int LayerCount = 3;
    private const int CloudBankCount = 4;
    private static GroundHazeController instance;
    private readonly MeshRenderer[] renderers = new MeshRenderer[LayerCount];
    private readonly Material[] materials = new Material[LayerCount];
    private readonly MeshRenderer[] cloudBankRenderers = new MeshRenderer[CloudBankCount];
    private readonly Material[] cloudBankMaterials = new Material[CloudBankCount];
    private MeshRenderer horizonRenderer;
    private Material horizonMaterial;
    private Camera mapCamera;
    private Transform player;
    private float liveDensity;
    private float liveHorizonDensity;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        // Retired: VF2 is now the sole ground-atmosphere renderer. Do not
        // create the three horizontal planes, four vertical cloud banks, or
        // the horizon-curtain quad. Keep this class as a compatibility stub
        // for existing live-setting calls and older serialized presets.
    }

    private void Start()
    {
        var shader = Shader.Find("K1L0/GroundHaze");
        if (shader == null)
        {
            Debug.LogError("[GroundHaze] K1L0/GroundHaze shader missing");
            enabled = false;
            return;
        }

        for (int i = 0; i < LayerCount; i++)
        {
            var layer = GameObject.CreatePrimitive(PrimitiveType.Plane);
            layer.name = $"RadioactiveSmoke_{i}";
            layer.transform.SetParent(transform, false);
            layer.transform.localScale = Vector3.one * (i == 0 ? 95f : 82f);
            var collider = layer.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            var renderer = layer.GetComponent<MeshRenderer>();
            var material = new Material(shader) { renderQueue = 3020 + i };
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderers[i] = renderer;
            materials[i] = material;
        }

        // Soft camera-facing banks turn the same inexpensive procedural haze
        // into visible dust-cloud bodies. They overlap the low horizon curtain
        // but retain feathered edges, avoiding a solid horizontal fog band.
        for (int i = 0; i < CloudBankCount; i++)
        {
            var bank = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bank.name = $"RadioactiveDustBank_{i}";
            bank.transform.SetParent(transform, false);
            var collider = bank.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            var renderer = bank.GetComponent<MeshRenderer>();
            var material = new Material(shader) { renderQueue = 3020 + i };
            material.SetFloat("_Vertical", 1f);
            // The layered sky is a horizon plane with depth, so ordinary
            // LEqual-tested atmospheric cards disappear behind it. Dust is an
            // overlay medium: draw through scene depth at low alpha, before
            // beam thumbnails and HUD labels.
            material.SetFloat("_ZTestMode", (float)UnityEngine.Rendering.CompareFunction.Always);
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            cloudBankRenderers[i] = renderer;
            cloudBankMaterials[i] = material;
        }

        var curtain = GameObject.CreatePrimitive(PrimitiveType.Quad);
        curtain.name = "RadioactiveHorizonCurtain";
        curtain.transform.SetParent(transform, false);
        var curtainCollider = curtain.GetComponent<Collider>();
        if (curtainCollider != null) Destroy(curtainCollider);
        horizonRenderer = curtain.GetComponent<MeshRenderer>();
        horizonMaterial = new Material(shader) { renderQueue = 3024 };
        horizonMaterial.SetFloat("_Vertical", 1f);
        horizonMaterial.SetFloat("_HorizonCurtain", 1f);
        // The shader confines depth-independent coverage to a narrow feather at
        // world height; this is what hides the depth-writing ground's top row.
        horizonMaterial.SetFloat("_ZTestMode", (float)UnityEngine.Rendering.CompareFunction.Always);
        horizonRenderer.sharedMaterial = horizonMaterial;
        horizonRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        horizonRenderer.receiveShadows = false;
        ApplySettings();
    }

    private void LateUpdate()
    {
        if (mapCamera == null || !mapCamera.isActiveAndEnabled) mapCamera = Camera.main;
        if (player == null)
        {
            var controller = FindFirstObjectByType<KiloFirstPersonController>();
            if (controller != null) player = controller.transform;
        }
        if (mapCamera == null) return;

        Vector3 anchor = player != null ? player.position : mapCamera.transform.position;
        float baseHeight = PlayerPrefs.GetFloat("k1lo_groundHazeHeight", 1.1f);
        float spacing = PlayerPrefs.GetFloat("k1lo_groundHazeSpacing", 0.72f);
        transform.position = new Vector3(
            Mathf.Round(anchor.x / 40f) * 40f,
            anchor.y + baseHeight,
            Mathf.Round(anchor.z / 40f) * 40f);

        for (int i = 0; i < LayerCount; i++)
        {
            if (renderers[i] == null) continue;
            float phase = i * 2.173f;
            float slowRise = Mathf.Sin(Time.time * (.11f + i * .018f) + phase);
            float billow = Mathf.Max(0f, Mathf.Sin(Time.time * (.047f + i * .009f) - phase));
            float riseMeters = slowRise * .42f + billow * (1.1f + i * .24f);
            renderers[i].transform.localPosition = new Vector3(0f, i * spacing + riseMeters, 0f);
            materials[i]?.SetFloat("_LayerPhase", i * 17.31f);
            // Independent breathing makes banks alternately thicken and clear.
            float densityPulse = Mathf.Lerp(.62f, 1.30f,
                .5f + .5f * Mathf.Sin(Time.time * (.16f + i * .027f) + phase));
            materials[i]?.SetFloat("_Density", liveDensity * (1f - i * .16f) * densityPulse);
        }

        Vector3 cameraForward = Vector3.ProjectOnPlane(mapCamera.transform.forward, Vector3.up).normalized;
        if (cameraForward.sqrMagnitude < .01f) cameraForward = Vector3.forward;
        Vector3 cameraRight = Vector3.Cross(Vector3.up, cameraForward).normalized;
        for (int i = 0; i < CloudBankCount; i++)
        {
            var renderer = cloudBankRenderers[i];
            if (renderer == null) continue;
            float phase = i * 1.91f;
            float distance = 105f + i * 43f;
            float lateral = Mathf.Sin(phase + Time.time * .018f) * (32f + i * 9f);
            float rise = 7f + i * 3.2f + Mathf.Sin(Time.time * (.055f + i * .008f) + phase) * 2.8f;
            renderer.transform.position = anchor + cameraForward * distance + cameraRight * lateral + Vector3.up * rise;
            renderer.transform.rotation = Quaternion.LookRotation(-cameraForward, Vector3.up);
            float width = 92f + i * 34f;
            float height = 22f + i * 6f;
            renderer.transform.localScale = new Vector3(width, height, 1f);
            float pulse = Mathf.Lerp(.58f, 1.08f,
                .5f + .5f * Mathf.Sin(Time.time * (.105f + i * .014f) + phase));
            cloudBankMaterials[i]?.SetFloat("_Density", liveDensity * .38f * pulse);
            cloudBankMaterials[i]?.SetFloat("_LayerPhase", 70f + i * 13.7f);
        }

        if (horizonRenderer != null)
        {
            Vector3 forward = cameraForward;
            if (forward.sqrMagnitude < .01f) forward = Vector3.forward;
            float distance = PlayerPrefs.GetFloat("k1lo_groundHazeHorizonDistance", 520f);
            float height = PlayerPrefs.GetFloat("k1lo_groundHazeHorizonHeight", 52f);
            float horizonRise = Mathf.Sin(Time.time * .063f + 1.7f) * 4.5f;
            horizonRenderer.transform.position = anchor + forward * distance + Vector3.up * (height + horizonRise);
            horizonRenderer.transform.rotation = Quaternion.LookRotation(-forward, Vector3.up);
            // Bury the curtain deeply below world height. At 2.2x its bottom
            // sat only 10% of `height` below the ground and projected as a
            // one-pixel horizontal seam. The visible top remains the shader's
            // animated/noisy ridge; the lower edge is now safely offscreen or
            // behind foreground geometry.
            horizonRenderer.transform.localScale = new Vector3(distance * 2.35f, height * 3.4f, 1f);
            horizonMaterial?.SetFloat("_Density", liveHorizonDensity *
                Mathf.Lerp(.72f, 1.18f, .5f + .5f * Mathf.Sin(Time.time * .091f)));
        }
    }

    public static void ApplySettings()
    {
        if (instance == null) return;
        float enabledValue = PlayerPrefs.GetInt("k1lo_groundHazeEnabled", 1);
        float density = PlayerPrefs.GetFloat("k1lo_groundHazeDensity", .34f);
        float detail = PlayerPrefs.GetFloat("k1lo_groundHazeDetail", 1.35f);
        float speed = PlayerPrefs.GetFloat("k1lo_groundHazeSpeed", .055f);
        float hue = PlayerPrefs.GetFloat("k1lo_groundHazeHue", .055f);
        float saturation = PlayerPrefs.GetFloat("k1lo_groundHazeSaturation", .82f);
        float brightness = PlayerPrefs.GetFloat("k1lo_groundHazeBrightness", 1.08f);
        float extent = PlayerPrefs.GetFloat("k1lo_groundHazeExtent", 135f);
        float pinkAmount = PlayerPrefs.GetFloat("k1lo_groundHazePinkAmount", .34f);
        float whiteAmount = PlayerPrefs.GetFloat("k1lo_groundHazeWhiteAmount", .22f);
        float blueAmount = PlayerPrefs.GetFloat("k1lo_groundHazeBlueAmount", .24f);
        float orangeAmount = PlayerPrefs.GetFloat("k1lo_groundHazeOrangeAmount", .18f);
        float horizonDensity = PlayerPrefs.GetFloat("k1lo_groundHazeHorizonDensity", .24f);
        instance.liveDensity = density;
        instance.liveHorizonDensity = Mathf.Clamp01(horizonDensity);
        Color color = Color.HSVToRGB(Mathf.Repeat(hue, 1f), Mathf.Clamp01(saturation), Mathf.Max(0f, brightness), true);

        for (int i = 0; i < LayerCount; i++)
        {
            if (instance.renderers[i] != null) instance.renderers[i].enabled = enabledValue > .5f;
            if (instance.renderers[i] != null)
                instance.renderers[i].transform.localScale = Vector3.one * extent * (i == 0 ? 1f : .86f);
            var material = instance.materials[i];
            if (material == null) continue;
            material.SetColor("_SmokeColor", color);
            material.SetFloat("_Density", density * (1f - i * .16f));
            material.SetFloat("_Detail", detail * (1f + i * .21f));
            material.SetFloat("_Speed", speed * (1f + i * .37f));
            material.SetFloat("_PinkAmount", Mathf.Clamp01(pinkAmount));
            material.SetFloat("_WhiteAmount", Mathf.Clamp01(whiteAmount));
            material.SetFloat("_BlueAmount", Mathf.Clamp01(blueAmount));
            material.SetFloat("_OrangeAmount", Mathf.Clamp01(orangeAmount));
        }
        for (int i = 0; i < CloudBankCount; i++)
        {
            if (instance.cloudBankRenderers[i] != null)
                instance.cloudBankRenderers[i].enabled = enabledValue > .5f;
            var material = instance.cloudBankMaterials[i];
            if (material == null) continue;
            material.SetColor("_SmokeColor", color);
            material.SetFloat("_Density", density * .38f);
            material.SetFloat("_Detail", detail * (.76f + i * .08f));
            material.SetFloat("_Speed", speed * (.72f + i * .11f));
            material.SetFloat("_PinkAmount", Mathf.Clamp01(pinkAmount));
            material.SetFloat("_WhiteAmount", Mathf.Clamp01(whiteAmount));
            material.SetFloat("_BlueAmount", Mathf.Clamp01(blueAmount));
            material.SetFloat("_OrangeAmount", Mathf.Clamp01(orangeAmount));
        }
        if (instance.horizonRenderer != null) instance.horizonRenderer.enabled = enabledValue > .5f;
        if (instance.horizonMaterial != null)
        {
            instance.horizonMaterial.SetColor("_SmokeColor", color);
            instance.horizonMaterial.SetFloat("_Density", Mathf.Clamp01(horizonDensity));
            instance.horizonMaterial.SetFloat("_Detail", detail * .72f);
            instance.horizonMaterial.SetFloat("_Speed", speed * .55f);
            instance.horizonMaterial.SetFloat("_LayerPhase", 41.7f);
            instance.horizonMaterial.SetFloat("_PinkAmount", Mathf.Clamp01(pinkAmount));
            instance.horizonMaterial.SetFloat("_WhiteAmount", Mathf.Clamp01(whiteAmount));
            instance.horizonMaterial.SetFloat("_BlueAmount", Mathf.Clamp01(blueAmount));
            instance.horizonMaterial.SetFloat("_OrangeAmount", Mathf.Clamp01(orangeAmount));
        }
    }
}
