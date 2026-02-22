using UnityEngine;
using UnityEditor;
using System.IO;

public class WindowPaneTextureGenerator : EditorWindow
{
    private int textureSize = 512;
    private int columns = 2; // Reduced from 4 - larger panes
    private int rows = 3; // Reduced from 6 - larger panes
    private int frameThickness = 6; // Increased from 4 - thicker frames
    private Color glowColor = Color.white;
    private Color frameColor = new Color(0.1f, 0.08f, 0.05f);

    [MenuItem("Tools/Generate Window Pane Texture")]
    static void Init()
    {
        WindowPaneTextureGenerator window = (WindowPaneTextureGenerator)EditorWindow.GetWindow(typeof(WindowPaneTextureGenerator));
        window.titleContent = new GUIContent("Window Pane Generator");
        window.Show();
    }

    [MenuItem("Tools/Generate Window Pane Texture Now")]
    static void GenerateNow()
    {
        GenerateWindowTextureStatic(512, 2, 3, 6, Color.white, new Color(0.1f, 0.08f, 0.05f));
    }

    static void GenerateWindowTextureStatic(int textureSize, int columns, int rows, int frameThickness, Color glowColor, Color frameColor)
    {
        Texture2D texture = new Texture2D(textureSize, textureSize);

        // Calculate pane dimensions
        float paneWidth = (float)textureSize / columns;
        float paneHeight = (float)textureSize / rows;

        // Fill texture
        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                // Determine which pane we're in
                int paneX = Mathf.FloorToInt(x / paneWidth);
                int paneY = Mathf.FloorToInt(y / paneHeight);

                // Calculate position within pane
                float localX = x - (paneX * paneWidth);
                float localY = y - (paneY * paneHeight);

                // Check if we're in a frame area
                bool isFrame = localX < frameThickness ||
                               localX >= paneWidth - frameThickness ||
                               localY < frameThickness ||
                               localY >= paneHeight - frameThickness;

                // Set pixel color
                texture.SetPixel(x, y, isFrame ? frameColor : glowColor);
            }
        }

        texture.Apply();

        // Save texture
        string path = "Assets/Textures/WindowPaneGrid.png";

        // Create directory if it doesn't exist
        string directory = Path.GetDirectoryName(path);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Save as PNG
        byte[] bytes = texture.EncodeToPNG();
        File.WriteAllBytes(path, bytes);

        AssetDatabase.Refresh();

        // Set texture import settings
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.SaveAndReimport();
        }

        Debug.Log($"Window pane texture generated at: {path}");
    }

    void OnGUI()
    {
        GUILayout.Label("Window Pane Texture Generator", EditorStyles.boldLabel);
        
        textureSize = EditorGUILayout.IntField("Texture Size", textureSize);
        columns = EditorGUILayout.IntField("Columns (horizontal panes)", columns);
        rows = EditorGUILayout.IntField("Rows (vertical panes)", rows);
        frameThickness = EditorGUILayout.IntField("Frame Thickness (pixels)", frameThickness);
        glowColor = EditorGUILayout.ColorField("Glow Color", glowColor);
        frameColor = EditorGUILayout.ColorField("Frame Color", frameColor);

        GUILayout.Space(10);
        
        if (GUILayout.Button("Generate Window Texture"))
        {
            GenerateWindowTexture();
        }
    }

    void GenerateWindowTexture()
    {
        Texture2D texture = new Texture2D(textureSize, textureSize);
        
        // Calculate pane dimensions
        float paneWidth = (float)textureSize / columns;
        float paneHeight = (float)textureSize / rows;
        
        // Fill texture
        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                // Determine which pane we're in
                int paneX = Mathf.FloorToInt(x / paneWidth);
                int paneY = Mathf.FloorToInt(y / paneHeight);
                
                // Calculate position within pane
                float localX = x - (paneX * paneWidth);
                float localY = y - (paneY * paneHeight);
                
                // Check if we're in a frame area
                bool isFrame = localX < frameThickness || 
                               localX >= paneWidth - frameThickness ||
                               localY < frameThickness || 
                               localY >= paneHeight - frameThickness;
                
                // Set pixel color
                texture.SetPixel(x, y, isFrame ? frameColor : glowColor);
            }
        }
        
        texture.Apply();
        
        // Save texture
        string path = "Assets/Textures/WindowPaneGrid.png";
        
        // Create directory if it doesn't exist
        string directory = Path.GetDirectoryName(path);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        // Save as PNG
        byte[] bytes = texture.EncodeToPNG();
        File.WriteAllBytes(path, bytes);
        
        AssetDatabase.Refresh();
        
        // Set texture import settings
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.SaveAndReimport();
        }
        
        Debug.Log($"Window pane texture generated at: {path}");
        EditorUtility.DisplayDialog("Success", $"Window pane texture created at:\n{path}", "OK");
    }
}
