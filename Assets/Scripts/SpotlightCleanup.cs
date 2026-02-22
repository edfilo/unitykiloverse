using UnityEngine;

namespace KiloWorld
{
    /// <summary>
    /// Ensures only one PlayerSpotlight exists in the scene.
    /// Runs once on scene load and destroys duplicates.
    /// </summary>
    public class SpotlightCleanup : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CleanupDuplicateSpotlights()
        {
            GameObject[] spotlights = GameObject.FindGameObjectsWithTag("Untagged");
            int count = 0;
            GameObject keeper = null;

            foreach (GameObject obj in spotlights)
            {
                if (obj.name == "PlayerSpotlight")
                {
                    count++;
                    if (keeper == null)
                    {
                        // Keep the first one we find
                        keeper = obj;
                        Debug.Log($"[SpotlightCleanup] Keeping PlayerSpotlight: {obj.GetInstanceID()}");
                    }
                    else
                    {
                        // Destroy duplicates
                        Debug.Log($"[SpotlightCleanup] Destroying duplicate PlayerSpotlight: {obj.GetInstanceID()}");
                        Destroy(obj);
                    }
                }
            }

            if (count > 1)
            {
                Debug.LogWarning($"[SpotlightCleanup] Found and cleaned up {count - 1} duplicate PlayerSpotlight(s). Only 1 remains.");
            }
            else if (count == 1)
            {
                Debug.Log("[SpotlightCleanup] Found exactly 1 PlayerSpotlight. No cleanup needed.");
            }
            else
            {
                Debug.LogWarning("[SpotlightCleanup] No PlayerSpotlight found in scene.");
            }
        }
    }
}
