using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class SimpleGroundPlane : MonoBehaviour
{
    [Header("Ground Size")]
    [SerializeField] private float size = 10000f; // Large enough to cover the map
    
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Material groundMaterial;
    private Camera mainCamera;
    private float fixedHeight = 0.01f; // Set to 0.01f as requested

private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        
        GenerateGroundMesh();
        CreateGroundMaterial();
    }

    private void Start()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogWarning("[SimpleGroundPlane] Main camera not found. Ground won't follow camera.");
        }
    }
    private void Update()
    {
        /* TEMPORARILY DISABLED FOR STATIC TEST
        if (mainCamera != null)
        {
            // Follow camera XZ position
            Vector3 cameraPos = mainCamera.transform.position;
            float currentY = transform.position.y;
            transform.position = new Vector3(cameraPos.x, currentY, cameraPos.z);
            
            // Update material UV offset to create scrolling effect based on world position
            if (groundMaterial != null)
            {
                // Get texture scale/tiling
                Vector2 tiling = groundMaterial.GetTextureScale("_BaseMap");
                
                // Calculate offset: world position * tiling / quad size
                Vector2 offset = new Vector2(
                    (cameraPos.x * tiling.x) / size,
                    (cameraPos.z * tiling.y) / size
                );

                // Apply to ALL relevant maps to ensure they slide together
                groundMaterial.SetTextureOffset("_BaseMap", offset);
                groundMaterial.SetTextureOffset("_BumpMap", offset);
                groundMaterial.SetTextureOffset("_EmissionMap", offset);
                // Note: URP usually shares UVs via _BaseMap_ST, but explicit setting ensures checks
            }
        }
        */
    }


    private void GenerateGroundMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "SimpleGroundPlane";

        // Create a simple quad slightly below 0 to avoid Z-fighting
        Vector3[] vertices = new Vector3[4]
        {
            new Vector3(-size/2, -0.05f, -size/2),
            new Vector3(size/2, -0.05f, -size/2),
            new Vector3(-size/2, -0.05f, size/2),
            new Vector3(size/2, -0.05f, size/2)
        };

        Vector2[] uvs = new Vector2[4]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(0, 1),
            new Vector2(1, 1)
        };

        int[] triangles = new int[6]
        {
            0, 2, 1,
            2, 3, 1
        };

        /*
        // Explicitly set normals pointing UP
        Vector3[] normals = new Vector3[4]
        {
            Vector3.up,
            Vector3.up,
            Vector3.up,
            Vector3.up
        };
        */

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        // mesh.normals = normals;
        mesh.RecalculateNormals();
        mesh.RecalculateTangents(); // Ensure tangent space is correct

        meshFilter.mesh = mesh;
    }

private void CreateGroundMaterial()
    {
        // Always create a fresh URP Lit material for the ground
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null)
        {
            Debug.LogError("[SimpleGroundPlane] Could not find URP Lit shader!");
            return;
        }

        groundMaterial = new Material(urpLit);
        groundMaterial.name = "SimpleGroundMaterial (Runtime Instance)";
        groundMaterial.SetColor("_BaseColor", new Color(0.2f, 0.2f, 0.2f));
        groundMaterial.SetFloat("_Smoothness", 0.3f);
        groundMaterial.SetFloat("_Metallic", 0f);

        // Force opaque rendering with depth write
        groundMaterial.SetFloat("_Surface", 0.0f);
        groundMaterial.SetFloat("_Blend", 0.0f);
        groundMaterial.SetFloat("_ZWrite", 1.0f);
        groundMaterial.renderQueue = 2000;
        groundMaterial.SetOverrideTag("RenderType", "Opaque");
        
        // Disable Specular Highlights and Environment Reflections
        groundMaterial.SetFloat("_EnvironmentReflections", 0.0f);
        groundMaterial.SetFloat("_SpecularHighlights", 0.0f);

        // Force enable the "OFF" keywords to disable these effects in URP Lit
        groundMaterial.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        groundMaterial.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");

        // Ensure smoothness is zeroed for the PBR pass
        groundMaterial.SetFloat("_Smoothness", 0.0f);
        groundMaterial.SetFloat("_Metallic", 0.0f);

        // Receive shadows
        groundMaterial.SetFloat("_ReceiveShadows", 1.0f);

        meshRenderer.material = groundMaterial;
    }

    public Material GetGroundMaterial()
    {
        return groundMaterial;
    }

public void UpdateMaterial(
        Color color,
        float smoothness,
        float brightness,
        float metallic = 0f,
        Texture2D albedo = null,
        Texture2D normal = null,
        float normalStrength = 1f,
        Color emission = default,
        Texture2D emissionMap = null,
        float emissionIntensity = 0f,
        Vector2 tiling = default)
    {
        // Default tiling if not specified
        if (tiling == default) tiling = new Vector2(100, 100);

        // Ensure material is created
        if (groundMaterial == null)
        {
            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer != null && meshRenderer.sharedMaterial != null)
            {
                groundMaterial = meshRenderer.sharedMaterial;
            }
            else
            {
                CreateGroundMaterial();
            }
        }

        if (groundMaterial != null)
        {
            Color finalColor = color * brightness;
            groundMaterial.SetColor("_BaseColor", finalColor);
            
            // Force zero specular properties
            groundMaterial.SetFloat("_Smoothness", 0.0f);
            groundMaterial.SetFloat("_Metallic", 0.0f);
            
            groundMaterial.SetFloat("_EnvironmentReflections", 0.0f);
            groundMaterial.SetFloat("_SpecularHighlights", 0.0f);
            groundMaterial.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            groundMaterial.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");

            // Albedo
            groundMaterial.SetTexture("_BaseMap", albedo);
            groundMaterial.SetTextureScale("_BaseMap", tiling);

            // Normal Map
            if (normal != null)
            {
                groundMaterial.SetTexture("_BumpMap", normal);
                groundMaterial.EnableKeyword("_NORMALMAP");
                // Negative bump scale flips the normal map (fixes reversed directional lighting)
                groundMaterial.SetFloat("_BumpScale", -normalStrength);
                groundMaterial.SetTextureScale("_BumpMap", tiling);
            }
            else
            {
                groundMaterial.SetTexture("_BumpMap", null);
                groundMaterial.DisableKeyword("_NORMALMAP");
            }

            // Emission
            Color hdrEmission = emission * emissionIntensity;
            groundMaterial.SetColor("_EmissionColor", hdrEmission);
            groundMaterial.SetTexture("_EmissionMap", emissionMap);
            groundMaterial.SetTextureScale("_EmissionMap", tiling);

            if (emissionIntensity > 0 || emissionMap != null)
            {
                groundMaterial.EnableKeyword("_EMISSION");
                groundMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                groundMaterial.DisableKeyword("_EMISSION");
            }
        }
        else
        {
            Debug.LogError("[SimpleGroundPlane] Failed to create or find ground material!");
        }
    }
}
