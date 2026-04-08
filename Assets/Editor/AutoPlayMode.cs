using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public static class AutoPlayMode
{
    static AutoPlayMode()
    {
        // Only run once — check if we should auto-play
        if (EditorPrefs.GetBool("AutoPlayOnce", false))
        {
            EditorPrefs.SetBool("AutoPlayOnce", false);
            EditorApplication.delayCall += () =>
            {
                // Load KiloScene if not already loaded
                var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                if (scene.name != "KiloScene")
                {
                    string[] guids = AssetDatabase.FindAssets("t:Scene KiloScene");
                    if (guids.Length > 0)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                        EditorSceneManager.OpenScene(path);
                    }
                }
                EditorApplication.isPlaying = true;
            };
        }
    }
}
