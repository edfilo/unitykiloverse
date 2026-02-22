#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

/// <summary>
/// Editor utility to clean up old Meeder/Hernandez teleport buttons from the scene
/// Menu: Tools > Cleanup Old Teleport Buttons
/// </summary>
public class CleanupOldTeleportButtons
{
    [MenuItem("Tools/Cleanup Old Teleport Buttons")]
    static void CleanupButtons()
    {
        int count = 0;

        // Search by GameObject name
        var oldButtonNames = new[] {
            "HernandezButton", "MeederButton", "SlotAButton", "SlotBButton", "GPSButton",
            "Hernandez", "Meeder", "hernandez", "meeder", "Hernandez Park", "Meeder Green"
        };

        foreach (var buttonName in oldButtonNames)
        {
            var obj = GameObject.Find(buttonName);
            if (obj != null)
            {
                Debug.Log($"[Cleanup] Destroying: {buttonName}");
                Object.DestroyImmediate(obj);
                count++;
            }
        }

        // Search all buttons by text content
        var allButtons = Object.FindObjectsOfType<Button>();
        foreach (var btn in allButtons)
        {
            var text = btn.GetComponentInChildren<Text>();
            if (text != null)
            {
                string textLower = text.text.ToLower();
                if (textLower.Contains("hernandez") ||
                    textLower.Contains("meeder") ||
                    (textLower.Contains("green") && textLower.Contains("park")))
                {
                    Debug.Log($"[Cleanup] Destroying button by text: {btn.name} (text: \"{text.text}\")");
                    Object.DestroyImmediate(btn.gameObject);
                    count++;
                }
            }
        }

        if (count > 0)
        {
            EditorUtility.DisplayDialog("Cleanup Complete",
                $"Removed {count} old teleport button(s).\n\nDon't forget to save your scene!",
                "OK");
            EditorUtility.SetDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().GetRootGameObjects()[0]);
        }
        else
        {
            EditorUtility.DisplayDialog("Cleanup Complete",
                "No old teleport buttons found in the scene.",
                "OK");
        }
    }
}
#endif
