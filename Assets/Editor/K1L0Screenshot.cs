#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

public static class K1L0Screenshot
{
    [MenuItem("Tools/K1L0 Screenshot")]
    public static void Capture()
    {
        string path = "/tmp/k1l0_screenshot.png";

        // Capture the Game view window
        var gameViewType = System.Type.GetType("UnityEditor.GameView,UnityEditor");
        var gameView = EditorWindow.GetWindow(gameViewType);
        if (gameView != null)
        {
            int w = (int)gameView.position.width;
            int h = (int)gameView.position.height;
            var colors = UnityEditorInternal.InternalEditorUtility.ReadScreenPixel(
                gameView.position.position, w, h);
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.SetPixels(colors);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Debug.Log($"[K1L0Screenshot] Saved {w}x{h} to {path}");
        }
    }
}
#endif
