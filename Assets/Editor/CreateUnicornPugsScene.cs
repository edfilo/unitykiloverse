using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class CreateUnicornPugsScene
{
    private const string ScenePath = "Assets/Scenes/UnicornPugs.unity";

    [MenuItem("Tools/Unicorn Pugs/Create Demo Scene")]
    public static void CreateDemoSceneMenu()
    {
        CreateDemoScene();
    }

    public static void CreateDemoScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "UnicornPugs";

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.83f, 0.88f, 1f);
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.74f, 0.88f, 1f);
        RenderSettings.fogDensity = 0.01f;

        CreateLight();
        CreateGround();
        CreatePathsAndPads();
        CreatePond();
        CreateTrees();
        CreateCrystals();
        CreatePlayer();
        CreateTitle();

        EditorSceneManager.SaveScene(scene, ScenePath);
        EnsureSceneInBuildSettings();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        AssetDatabase.SaveAssets();

        Debug.Log("[UnicornPugs] Demo scene created at " + ScenePath);
    }

    private static void EnsureSceneInBuildSettings()
    {
        string[] currentScenePaths = new string[EditorBuildSettings.scenes.Length + 1];
        bool found = false;

        for (int i = 0; i < EditorBuildSettings.scenes.Length; i++)
        {
            currentScenePaths[i] = EditorBuildSettings.scenes[i].path;
            if (currentScenePaths[i] == ScenePath)
            {
                found = true;
            }
        }

        if (found)
        {
            return;
        }

        EditorBuildSettingsScene[] scenes = new EditorBuildSettingsScene[EditorBuildSettings.scenes.Length + 1];
        for (int i = 0; i < EditorBuildSettings.scenes.Length; i++)
        {
            scenes[i] = EditorBuildSettings.scenes[i];
        }

        scenes[^1] = new EditorBuildSettingsScene(ScenePath, true);
        EditorBuildSettings.scenes = scenes;
    }

    private static void CreateLight()
    {
        GameObject lightObject = new("Directional Light");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        light.color = new Color(1f, 0.97f, 0.92f);
        lightObject.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
    }

    private static void CreateGround()
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "ParkGround";
        ground.transform.localScale = new Vector3(3f, 1f, 3f);
        ground.GetComponent<Renderer>().sharedMaterial = CreateMaterial("ParkGroundMat", new Color(0.55f, 0.82f, 0.54f));
    }

    private static void CreatePathsAndPads()
    {
        CreateBox("MainPath", new Vector3(0f, 0.02f, 0f), new Vector3(3.6f, 0.05f, 18f), new Color(0.91f, 0.85f, 0.68f));
        CreateBox("CrossPath", new Vector3(0f, 0.021f, 3f), new Vector3(18f, 0.05f, 3.2f), new Color(0.91f, 0.85f, 0.68f));
        CreateBox("SpawnPad", new Vector3(0f, 0.05f, 12f), new Vector3(3.2f, 0.15f, 3.2f), new Color(1f, 0.95f, 0.48f), true);
        CreateBox("SellPad", new Vector3(5.5f, 0.05f, 12f), new Vector3(3.2f, 0.15f, 3.2f), new Color(0.56f, 0.9f, 0.6f), true);
    }

    private static void CreatePond()
    {
        CreateBox("Pond", new Vector3(-10f, 0.01f, -6f), new Vector3(8f, 0.03f, 5f), new Color(0.38f, 0.78f, 1f), true);
    }

    private static void CreateTrees()
    {
        Vector3[] positions =
        {
            new(-12f, 0f, 12f),
            new(-16f, 0f, 3f),
            new(13f, 0f, 11f),
            new(15f, 0f, -5f),
            new(8f, 0f, -12f),
            new(-4f, 0f, -14f),
        };

        foreach (Vector3 position in positions)
        {
            GameObject trunk = CreatePrimitive("TreeTrunk", PrimitiveType.Cylinder, position + new Vector3(0f, 1.5f, 0f), new Vector3(0.45f, 1.6f, 0.45f), new Color(0.44f, 0.28f, 0.17f));
            GameObject canopy = CreatePrimitive("TreeCanopy", PrimitiveType.Sphere, position + new Vector3(0f, 3.7f, 0f), new Vector3(2.4f, 2.2f, 2.4f), new Color(0.35f, 0.73f, 0.36f));
            canopy.transform.SetParent(trunk.transform.parent);
        }
    }

    private static void CreateCrystals()
    {
        (Vector3 position, Vector3 scale, Color color)[] crystals =
        {
            (new Vector3(-3f, 0.8f, 8f), new Vector3(0.6f, 1.6f, 0.6f), new Color(1f, 0.35f, 0.45f)),
            (new Vector3(3f, 0.8f, 8f), new Vector3(0.6f, 1.6f, 0.6f), new Color(0.39f, 0.77f, 1f)),
            (new Vector3(-7f, 0.95f, -1f), new Vector3(0.8f, 1.9f, 0.8f), new Color(0.54f, 0.86f, 0.52f)),
            (new Vector3(8f, 1f, 2f), new Vector3(0.85f, 2f, 0.85f), new Color(0.81f, 0.5f, 0.94f)),
            (new Vector3(0f, 1.25f, -9f), new Vector3(1.1f, 2.4f, 1.1f), new Color(1f, 0.95f, 0.52f)),
        };

        foreach (var crystal in crystals)
        {
            GameObject crystalObject = CreatePrimitive("Crystal", PrimitiveType.Cube, crystal.position, crystal.scale, crystal.color, true);
            crystalObject.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
        }
    }

    private static void CreatePlayer()
    {
        GameObject player = new("Player");
        player.transform.position = new Vector3(0f, 1.15f, 12f);

        CharacterController controller = player.AddComponent<CharacterController>();
        controller.center = new Vector3(0f, 1f, 0f);
        controller.height = 2f;
        controller.radius = 0.45f;

        player.AddComponent<UnicornPugsPlayerController>();
        player.AddComponent<UnicornPugAvatar>();

        GameObject cameraPivot = new("CameraPivot");
        cameraPivot.transform.SetParent(player.transform, false);
        cameraPivot.transform.localPosition = new Vector3(0f, 1.9f, 0.1f);

        GameObject cameraObject = new("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.SetParent(cameraPivot.transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, 0.1f, -3.8f);
        cameraObject.transform.localRotation = Quaternion.Euler(10f, 0f, 0f);
        Camera cameraComponent = cameraObject.AddComponent<Camera>();
        cameraComponent.fieldOfView = 70f;

        UnicornPugsPlayerController playerController = player.GetComponent<UnicornPugsPlayerController>();
        SerializedObject serializedObject = new(playerController);
        serializedObject.FindProperty("cameraPivot").objectReferenceValue = cameraPivot.transform;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreateTitle()
    {
        GameObject title = new("UnicornPugsTitle");
        TextMesh textMesh = title.AddComponent<TextMesh>();
        textMesh.text = "Unicorn Pugs";
        textMesh.fontSize = 64;
        textMesh.characterSize = 0.18f;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.color = new Color(0.89f, 0.23f, 0.56f);
        title.transform.position = new Vector3(0f, 4.6f, 10.5f);

        GameObject subtitle = new("UnicornPugsSubtitle");
        TextMesh subtitleMesh = subtitle.AddComponent<TextMesh>();
        subtitleMesh.text = "Pug + unicorn horn + crystal park";
        subtitleMesh.fontSize = 32;
        subtitleMesh.characterSize = 0.1f;
        subtitleMesh.anchor = TextAnchor.MiddleCenter;
        subtitleMesh.color = new Color(1f, 1f, 1f);
        subtitle.transform.position = new Vector3(0f, 3.8f, 10.5f);
    }

    private static GameObject CreateBox(string name, Vector3 position, Vector3 scale, Color color, bool emissive = false)
    {
        return CreatePrimitive(name, PrimitiveType.Cube, position, scale, color, emissive);
    }

    private static GameObject CreatePrimitive(string name, PrimitiveType type, Vector3 position, Vector3 scale, Color color, bool emissive = false)
    {
        GameObject gameObject = GameObject.CreatePrimitive(type);
        gameObject.name = name;
        gameObject.transform.position = position;
        gameObject.transform.localScale = scale;
        gameObject.GetComponent<Renderer>().sharedMaterial = CreateMaterial(name + "Mat", color, emissive);
        return gameObject;
    }

    private static Material CreateMaterial(string label, Color color, bool emissive = false)
    {
        Material material = new(Shader.Find("Universal Render Pipeline/Lit"));
        material.name = label;
        material.color = color;
        if (emissive)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 1.25f);
        }

        return material;
    }
}
