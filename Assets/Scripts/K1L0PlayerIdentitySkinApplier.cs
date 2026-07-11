using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class K1L0PlayerIdentitySkinApplier : MonoBehaviour
{
    private static K1L0PlayerIdentitySkinApplier instance;
    private static GameObject registeredHelmetRoot;

    private string appliedHelmetUrl;
    private string appliedCloakUrl;
    private Coroutine helmetRoutine;
    private Coroutine cloakRoutine;
    private Coroutine helmetReapplyRoutine;
    private Coroutine cloakReapplyRoutine;
    private static readonly Vector2 CloakTextureScale = new Vector2(2.25f, 2.25f);
    private static readonly Dictionary<int, Texture2D> generatedCloakNormals = new Dictionary<int, Texture2D>();

    public static void ApplyFromMetadata(
        string helmetUrl,
        string cloakUrl,
        string avatarUrl,
        string helmetDesign,
        string cloakDesign,
        string helmetTextureUrl = "",
        string cloakTextureUrl = "",
        long skinRevision = 0)
    {
        EnsureInstance().ApplyInternal(
            helmetUrl,
            cloakUrl,
            avatarUrl,
            helmetDesign,
            cloakDesign,
            helmetTextureUrl,
            cloakTextureUrl,
            skinRevision);
    }

    public static void RegisterHelmetRoot(GameObject helmetRoot)
    {
        if (helmetRoot == null) return;
        registeredHelmetRoot = helmetRoot;
        Debug.Log($"[K1L0Skin] Registered runtime helmet root: {helmetRoot.name}");
    }

    private static K1L0PlayerIdentitySkinApplier EnsureInstance()
    {
        if (instance != null) return instance;

        var existing = FindFirstObjectByType<K1L0PlayerIdentitySkinApplier>();
        if (existing != null)
        {
            instance = existing;
            return instance;
        }

        var go = new GameObject("K1L0PlayerIdentitySkinApplier");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<K1L0PlayerIdentitySkinApplier>();
        return instance;
    }

    private void Start()
    {
        cloakRoutine = StartCoroutine(LoadCachedAndApply(true));
        helmetRoutine = StartCoroutine(LoadCachedAndApply(false));
    }

    private IEnumerator LoadCachedAndApply(bool cloak)
    {
        string path = System.IO.Path.Combine(Application.persistentDataPath, cloak ? "cached_cloak.png" : "cached_helmet.png");
        if (!System.IO.File.Exists(path)) yield break;

        var texture = LoadTextureFromFile(path);
        if (texture == null) yield break;

        Renderer[] renderers = null;
        float start = Time.realtimeSinceStartup;
        while ((renderers == null || renderers.Length == 0) && Time.realtimeSinceStartup - start < 8f)
        {
            renderers = FindTargetRenderers(cloak);
            if (renderers == null || renderers.Length == 0) yield return null;
        }

        if (renderers != null && renderers.Length > 0)
        {
            ApplyTexture(renderers, texture, cloak ? "cached cloak" : "cached helmet");
        }

        if (cloak)
        {
            appliedCloakUrl = PlayerPrefs.GetString("K1L0_CachedCloakUrl", "");
            StartReapply(texture, true, appliedCloakUrl);
        }
        else
        {
            appliedHelmetUrl = PlayerPrefs.GetString("K1L0_CachedHelmetUrl", "");
            StartReapply(texture, false, appliedHelmetUrl);
        }
    }

    private Texture2D LoadTextureFromFile(string path)
    {
        try
        {
            byte[] bytes = System.IO.File.ReadAllBytes(path);
            var texture = new Texture2D(2, 2);
            if (texture.LoadImage(bytes))
            {
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Bilinear;
                return texture;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[K1L0Skin] LoadTextureFromFile failed: {ex.Message}");
        }
        return null;
    }

    private void ApplyInternal(
        string helmetUrl,
        string cloakUrl,
        string avatarUrl,
        string helmetDesign,
        string cloakDesign,
        string helmetTextureUrl,
        string cloakTextureUrl,
        long skinRevision)
    {
        var resolvedCloakUrl = FirstNonEmpty(cloakTextureUrl, cloakUrl, avatarUrl);
        var resolvedHelmetUrl = FirstNonEmpty(helmetTextureUrl, helmetUrl);
        var cloakKey = MakeCacheKey(resolvedCloakUrl, skinRevision);
        var helmetKey = MakeCacheKey(resolvedHelmetUrl, skinRevision);
        var cloakIsMaterialTexture = !string.IsNullOrWhiteSpace(cloakTextureUrl);
        var helmetIsMaterialTexture = !string.IsNullOrWhiteSpace(helmetTextureUrl);

        if (string.IsNullOrWhiteSpace(resolvedCloakUrl))
        {
            ApplyProceduralCloakFallback(cloakDesign);
        }
        else if (cloakKey != appliedCloakUrl)
        {
            if (cloakRoutine != null) StopCoroutine(cloakRoutine);
            cloakRoutine = StartCoroutine(DownloadAndApply(resolvedCloakUrl, true, cloakIsMaterialTexture, cloakKey));
        }

        if (string.IsNullOrWhiteSpace(resolvedHelmetUrl))
        {
            ApplyProceduralHelmetFallback(helmetDesign);
        }
        else if (helmetKey != appliedHelmetUrl)
        {
            if (helmetRoutine != null) StopCoroutine(helmetRoutine);
            helmetRoutine = StartCoroutine(DownloadAndApply(resolvedHelmetUrl, false, helmetIsMaterialTexture, helmetKey));
        }
    }

    private IEnumerator DownloadAndApply(string url, bool cloak, bool materialTexture, string cacheKey)
    {
        using var request = UnityWebRequestTexture.GetTexture(url);
        request.timeout = 30;
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[K1L0Skin] Failed to load {(cloak ? "cloak" : "helmet")} texture: {request.error} url={url}");
            yield break;
        }

        var texture = DownloadHandlerTexture.GetContent(request);
        if (texture == null) yield break;

        texture.wrapMode = materialTexture ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        // Save to cache
        try
        {
            byte[] bytes = texture.EncodeToPNG();
            string path = System.IO.Path.Combine(Application.persistentDataPath, cloak ? "cached_cloak.png" : "cached_helmet.png");
            System.IO.File.WriteAllBytes(path, bytes);
            PlayerPrefs.SetString(cloak ? "K1L0_CachedCloakUrl" : "K1L0_CachedHelmetUrl", cacheKey);
            PlayerPrefs.Save();
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[K1L0Skin] Failed to cache texture: {ex.Message}");
        }

        StartReapply(texture, cloak, cacheKey);

        Renderer[] renderers = null;
        float start = Time.realtimeSinceStartup;
        while ((renderers == null || renderers.Length == 0) && Time.realtimeSinceStartup - start < 8f)
        {
            renderers = FindTargetRenderers(cloak);
            if (renderers == null || renderers.Length == 0) yield return null;
        }
        if (renderers == null || renderers.Length == 0)
        {
            Debug.LogWarning($"[K1L0Skin] No {(cloak ? "cloak" : "helmet")} renderers found.");
            yield break;
        }

        if (cloak)
        {
            ApplyTexture(renderers, texture, "cloak");
            appliedCloakUrl = cacheKey;
            StartReapply(texture, true, cacheKey);
        }
        else
        {
            ApplyTexture(renderers, texture, "helmet");
            appliedHelmetUrl = cacheKey;
            StartReapply(texture, false, cacheKey);
        }
    }

    private void StartReapply(Texture2D texture, bool cloak, string cacheKey)
    {
        if (texture == null) return;

        if (cloak)
        {
            if (cloakReapplyRoutine != null) StopCoroutine(cloakReapplyRoutine);
            cloakReapplyRoutine = StartCoroutine(ReapplyForSpawnedRenderers(texture, true, cacheKey));
        }
        else
        {
            if (helmetReapplyRoutine != null) StopCoroutine(helmetReapplyRoutine);
            helmetReapplyRoutine = StartCoroutine(ReapplyForSpawnedRenderers(texture, false, cacheKey));
        }
    }

    private IEnumerator ReapplyForSpawnedRenderers(Texture2D texture, bool cloak, string cacheKey)
    {
        float start = Time.realtimeSinceStartup;
        int lastCount = -1;
        while (Time.realtimeSinceStartup - start < 45f)
        {
            var renderers = FindTargetRenderers(cloak);
            if (renderers != null && renderers.Length > 0)
            {
                ApplyTexture(renderers, texture, cloak ? "cloak retry" : "helmet retry");
                lastCount = renderers.Length;
                if (cloak)
                    appliedCloakUrl = cacheKey;
                else
                    appliedHelmetUrl = cacheKey;
            }
            else if (lastCount != 0)
            {
                Debug.Log($"[K1L0Skin] Waiting for {(cloak ? "cloak" : "helmet")} renderers to spawn.");
                lastCount = 0;
            }

            yield return new WaitForSeconds(0.75f);
        }
    }

    private static GameObject FindCloakRoot()
    {
        return GameObject.Find("cloak") ?? FindObjectByNameContains("cloak");
    }

    private static GameObject FindHelmetRoot()
    {
        if (registeredHelmetRoot != null) return registeredHelmetRoot;
        return GameObject.Find("MotorcycleHelmet_Instance") ?? GameObject.Find("PlayerHelmet") ?? FindObjectByNameContains("helmet") ?? FindObjectByNameContains("halmet");
    }

    private static Renderer[] FindTargetRenderers(bool cloak)
    {
        var renderers = new System.Collections.Generic.List<Renderer>();
        var seen = new System.Collections.Generic.HashSet<int>();
        var all = FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var renderer in all)
        {
            if (renderer == null) continue;
            if (!RendererMatches(renderer, cloak)) continue;
            var id = renderer.GetInstanceID();
            if (seen.Add(id)) renderers.Add(renderer);
        }

        var root = cloak ? FindCloakRoot() : FindHelmetRoot();
        if (root != null)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null) continue;
                var id = renderer.GetInstanceID();
                if (seen.Add(id)) renderers.Add(renderer);
            }
        }

        return renderers.ToArray();
    }

    private static bool RendererMatches(Renderer renderer, bool cloak)
    {
        var transform = renderer.transform;
        while (transform != null)
        {
            var name = transform.name.ToLowerInvariant();
            if (cloak)
            {
                if (name.Contains("cloak")) return true;
            }
            else if (name.Contains("helmet") || name.Contains("halmet"))
            {
                return true;
            }
            transform = transform.parent;
        }

        return false;
    }

    private static void ApplyTexture(Renderer[] renderers, Texture2D texture, string label)
    {
        if (renderers == null || renderers.Length == 0)
        {
            Debug.LogWarning($"[K1L0Skin] No {label} renderers found.");
            return;
        }

        bool isHelmet = label.ToLowerInvariant().Contains("helmet");
        bool isCloak = label.ToLowerInvariant().Contains("cloak");
        if (isCloak)
        {
            texture.wrapMode = TextureWrapMode.Repeat;
        }
        var cloakNormal = isCloak ? GetGeneratedCloakNormal(texture) : null;
        int materialCount = 0;
        foreach (var renderer in renderers)
        {
            if (renderer == null) continue;
            var materials = renderer.materials;
            foreach (var material in materials)
            {
                if (material == null) continue;
                material.mainTexture = texture;
                if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
                if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
                if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
                if (isCloak)
                {
                    if (material.HasProperty("_BaseMap")) material.SetTextureScale("_BaseMap", CloakTextureScale);
                    if (material.HasProperty("_MainTex")) material.SetTextureScale("_MainTex", CloakTextureScale);
                    if (cloakNormal != null && material.HasProperty("_BumpMap"))
                    {
                        material.SetTexture("_BumpMap", cloakNormal);
                        material.SetTextureScale("_BumpMap", CloakTextureScale);
                        material.EnableKeyword("_NORMALMAP");
                    }
                    if (material.HasProperty("_BumpScale")) material.SetFloat("_BumpScale", cloakNormal != null ? 0.65f : 0.85f);
                    if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
                    if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.38f);
                    if (material.HasProperty("_SpecularHighlights")) material.SetFloat("_SpecularHighlights", 0f);
                    if (material.HasProperty("_EnvironmentReflections")) material.SetFloat("_EnvironmentReflections", 0f);
                    material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                    material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                }
                if (isHelmet)
                {
                    material.DisableKeyword("_EMISSION");
                    if (material.HasProperty("_EmissionMap")) material.SetTexture("_EmissionMap", null);
                    if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", Color.black);
                    if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
                    if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.35f);
                }
                materialCount++;
            }
        }

        Debug.Log($"[K1L0Skin] Applied {label} texture to {materialCount} materials.");
    }

    private static Texture2D GetGeneratedCloakNormal(Texture2D source)
    {
        if (source == null) return null;
        int id = source.GetInstanceID();
        if (generatedCloakNormals.TryGetValue(id, out var cached) && cached != null) return cached;

        try
        {
            int width = Mathf.Clamp(source.width, 16, 512);
            int height = Mathf.Clamp(source.height, 16, 512);
            var normal = new Texture2D(width, height, TextureFormat.RGBA32, true, true);
            const float strength = 2.2f;

            for (int y = 0; y < height; y++)
            {
                float v = height <= 1 ? 0f : (float)y / (height - 1);
                float vUp = height <= 1 ? v : (float)Mathf.Min(y + 1, height - 1) / (height - 1);
                float vDown = height <= 1 ? v : (float)Mathf.Max(y - 1, 0) / (height - 1);

                for (int x = 0; x < width; x++)
                {
                    float u = width <= 1 ? 0f : (float)x / (width - 1);
                    float uRight = width <= 1 ? u : (float)Mathf.Min(x + 1, width - 1) / (width - 1);
                    float uLeft = width <= 1 ? u : (float)Mathf.Max(x - 1, 0) / (width - 1);

                    float dx = Luminance(source.GetPixelBilinear(uRight, v)) - Luminance(source.GetPixelBilinear(uLeft, v));
                    float dy = Luminance(source.GetPixelBilinear(u, vUp)) - Luminance(source.GetPixelBilinear(u, vDown));
                    Vector3 n = new Vector3(-dx * strength, -dy * strength, 1f).normalized;
                    normal.SetPixel(x, y, new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f));
                }
            }

            normal.wrapMode = TextureWrapMode.Repeat;
            normal.filterMode = FilterMode.Bilinear;
            normal.Apply(true, false);
            generatedCloakNormals[id] = normal;
            Debug.Log($"[K1L0Skin] Generated cloak normal from albedo {source.width}x{source.height} -> {width}x{height}.");
            return normal;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[K1L0Skin] Failed to generate cloak normal from texture: {ex.Message}");
            return null;
        }
    }

    private static float Luminance(Color color)
    {
        return color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f;
    }

    private static void ApplyProceduralCloakFallback(string design)
    {
        var root = FindCloakRoot();
        if (root == null) return;
        var color = ColorFromDesign(design, new Color(0.88f, 0.88f, 0.82f, 1f));
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            ApplyColor(renderer, color, 0.15f, 0.62f);
    }

    private static void ApplyProceduralHelmetFallback(string design)
    {
        var root = FindHelmetRoot();
        if (root == null) return;
        var color = ColorFromDesign(design, Color.white);
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            ApplyColor(renderer, color, 0.08f, 0.48f);
    }

    private static void ApplyColor(Renderer renderer, Color color, float metallic, float smoothness)
    {
        if (renderer == null) return;
        foreach (var material in renderer.materials)
        {
            if (material == null) continue;
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", null);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", null);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        }
    }

    private static Color ColorFromDesign(string design, Color fallback)
    {
        var text = (design ?? "").ToLowerInvariant();
        if (text.Contains("red")) return new Color(0.55f, 0.03f, 0.035f, 1f);
        if (text.Contains("blue")) return new Color(0.08f, 0.20f, 0.78f, 1f);
        if (text.Contains("green")) return new Color(0.04f, 0.42f, 0.18f, 1f);
        if (text.Contains("purple") || text.Contains("paisley")) return new Color(0.33f, 0.08f, 0.48f, 1f);
        if (text.Contains("gold")) return new Color(0.95f, 0.67f, 0.18f, 1f);
        if (text.Contains("black")) return new Color(0.02f, 0.02f, 0.025f, 1f);
        if (text.Contains("white") || text.Contains("soccer")) return Color.white;
        return fallback;
    }

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }
        return "";
    }

    private static string MakeCacheKey(string url, long skinRevision)
    {
        if (string.IsNullOrWhiteSpace(url)) return "";
        return skinRevision > 0 ? $"{url}#rev={skinRevision}" : url;
    }

    private static GameObject FindObjectByNameContains(string needle)
    {
        var all = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var transform in all)
        {
            if (transform != null && transform.name.ToLowerInvariant().Contains(needle))
                return transform.gameObject;
        }
        return null;
    }
}
