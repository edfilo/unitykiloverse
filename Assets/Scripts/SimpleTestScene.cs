using UnityEngine;
using UnityEngine.UI;


/// <summary>
/// Simple test to verify Unity renders on iOS
/// Creates a bright red cube that rotates - should be impossible to miss
/// </summary>
public class SimpleTestScene : MonoBehaviour
{
    private GameObject testCube;
    private Camera testCamera;

void Start()
    {
        Debug.Log("[SimpleTest] ========== CREATING SIMPLE TEST SCENE ==========");
        
        // AGGRESSIVELY CLEAR EVERYTHING
        // Clear ALL render settings
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = Color.black;
        RenderSettings.fog = false;
        RenderSettings.fogColor = Color.black;
        
        // Destroy ALL existing cameras first
        Camera[] allCams = FindObjectsOfType<Camera>();
        Debug.Log($"[SimpleTest] Found {allCams.Length} existing cameras - destroying them all");
        foreach (Camera cam in allCams)
        {
            Debug.Log($"[SimpleTest] Destroying camera: {cam.name}");
            DestroyImmediate(cam.gameObject);
        }
        
        // Destroy any existing player or world objects
        GameObject player = GameObject.Find("Player");
        if (player != null) 
        {
            Debug.Log("[SimpleTest] Found and destroying Player");
            DestroyImmediate(player);
        }
        
        // Destroy any terrain or world objects
        Terrain[] terrains = FindObjectsOfType<Terrain>();
        foreach (Terrain t in terrains)
        {
            Debug.Log($"[SimpleTest] Destroying terrain: {t.name}");
            DestroyImmediate(t.gameObject);
        }
        
        // Create our test camera with BLUE background
        GameObject camObj = new GameObject("TestCamera");
        testCamera = camObj.AddComponent<Camera>();
        testCamera.clearFlags = CameraClearFlags.SolidColor;
        testCamera.backgroundColor = Color.blue;
        testCamera.depth = 999;  // Highest depth to render last
        testCamera.cullingMask = -1;  // See everything
        testCamera.fieldOfView = 60;
        testCamera.nearClipPlane = 0.3f;
        testCamera.farClipPlane = 1000f;
        Debug.Log($"[SimpleTest] Created TestCamera with BLUE background at {camObj.transform.position}");
        
        // Create a RED rotating cube
        testCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        testCube.name = "TestCube";
        testCube.transform.position = new Vector3(0, 0, 3);
        testCube.transform.localScale = Vector3.one;
        
        // Use the simplest possible material
        Renderer renderer = testCube.GetComponent<Renderer>();
        if (renderer != null && renderer.sharedMaterial != null)
        {
            renderer.material.color = Color.red;
            renderer.material.shader = Shader.Find("Unlit/Color") ?? Shader.Find("UI/Default");
            Debug.Log($"[SimpleTest] Created RED cube with shader: {renderer.material.shader?.name}");
        }
        
        // Create a YELLOW UI panel using ScreenSpaceOverlay (no camera needed)
        GameObject canvasObj = new GameObject("TestCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;  // On top of everything
        
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        GraphicRaycaster raycaster = canvasObj.AddComponent<GraphicRaycaster>();
        
        GameObject panelObj = new GameObject("TestPanel");
        panelObj.transform.SetParent(canvasObj.transform, false);
        
        RectTransform rect = panelObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.25f, 0.25f);
        rect.anchorMax = new Vector2(0.75f, 0.75f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        
        UnityEngine.UI.Image image = panelObj.AddComponent<UnityEngine.UI.Image>();
        image.color = Color.yellow;
        
        Debug.Log("[SimpleTest] ===========================================");
        Debug.Log("[SimpleTest] You should ONLY see:");
        Debug.Log("[SimpleTest] - BLUE background (camera clear color)");
        Debug.Log("[SimpleTest] - RED cube (in center)");
        Debug.Log("[SimpleTest] - YELLOW panel (UI overlay)");
        Debug.Log("[SimpleTest] ===========================================");
        Debug.Log("[SimpleTest] If you see grass/skybox/player, they weren't cleared properly");
    }

    void Update()
    {
        if (testCube != null)
        {
            testCube.transform.Rotate(0, 30 * Time.deltaTime, 0);
        }
    }
}