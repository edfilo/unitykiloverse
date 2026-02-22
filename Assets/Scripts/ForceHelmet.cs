using UnityEngine;
using System.Collections.Generic;

public class ForceHelmet : MonoBehaviour
{
    public GameObject motorcycleHelmetPrefab;
    public Vector3 helmetScale = Vector3.one;
    public Vector3 helmetPositionOffset = Vector3.zero;

    private GameObject createdHelmet;
    public GameObject GetCreatedHelmet() => createdHelmet;

    void Awake()
    {
        Debug.Log("[ForceHelmet] Awake() called on " + gameObject.name);
    }

    void Start()
    {
        Debug.Log("[ForceHelmet] Start() executing.");
        ReplaceHelmet();
    }

    void ReplaceHelmet()
    {
        Debug.Log($"[ForceHelmet] Prefab assigned? {(motorcycleHelmetPrefab != null)}");

        if (motorcycleHelmetPrefab == null)
        {
            Debug.LogWarning("[ForceHelmet] Prefab field is null. Attempting to load 'Biker_Halmet3' from Resources...");
            motorcycleHelmetPrefab = Resources.Load<GameObject>("Biker_Halmet3");

            if (motorcycleHelmetPrefab == null)
            {
                Debug.LogError("[ForceHelmet] FAILED to load helmet from Resources! Make sure 'Biker_Halmet3.prefab' is in a Resources folder.");
                return;
            }
            else
            {
                Debug.Log("[ForceHelmet] Successfully loaded helmet from Resources.");
            }
        }

        Transform headBone = FindDeepChild(transform, "Bip001 Head");
        if (headBone == null) headBone = FindDeepChild(transform, "Head");

        Transform neckBone = FindDeepChild(transform, "Bip001 Neck");
        if (neckBone == null) neckBone = FindDeepChild(transform, "Neck");

        Debug.Log($"[ForceHelmet] Head: {(headBone?.name ?? "null")}, Neck: {(neckBone?.name ?? "null")}");

        if (headBone != null && neckBone != null)
        {
            // Load settings from profile
            var profile = KiloWorld.Rendering.Systems.RenderManager.Instance?.profile;
            Vector3 scale = helmetScale;

            if (profile != null)
            {
                scale = Vector3.one * profile.helmet.scale;
                Debug.Log($"[ForceHelmet] Using profile settings - Scale: {profile.helmet.scale}");
            }

            createdHelmet = Instantiate(motorcycleHelmetPrefab);
            createdHelmet.name = "PlayerHelmet"; // Give it a findable name

            // Parent directly to head bone and use its coordinate system
            createdHelmet.transform.SetParent(headBone);
            createdHelmet.transform.localPosition = Vector3.zero; // At head bone position
            createdHelmet.transform.localRotation = Quaternion.identity; // No rotation offset - inherit bone rotation
            createdHelmet.transform.localScale = scale;

            // Hide the original head bone
            headBone.localScale = Vector3.one * 0.0001f;
            Debug.Log($"[ForceHelmet] SUCCESS: Helmet parented to head bone with scale {scale}");
        }
    }

    Transform FindDeepChild(Transform aParent, string aName)
    {
        Queue<Transform> queue = new Queue<Transform>();
        queue.Enqueue(aParent);
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            if (c.name == aName) return c;
            foreach (Transform t in c) queue.Enqueue(t);
        }
        return null;
    }
}
