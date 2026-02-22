using UnityEngine;
using UnityEditor;

public class AttachBikerHelmet : EditorWindow
{
    private GameObject helmetModel;
    private GameObject soldierModel;
    private float helmetScale = 0.01f; // Default scale (1% of original size)
    private Vector3 helmetPosition = new Vector3(0, 1.6f, 0); // Approximate head height
    private Vector3 helmetRotation = Vector3.zero;

    [MenuItem("Kiloverse/Attach Biker Helmet to Soldier")]
    public static void ShowWindow()
    {
        GetWindow<AttachBikerHelmet>("Attach Helmet");
    }

    void OnGUI()
    {
        GUILayout.Label("Attach Biker Helmet to Soldier", EditorStyles.boldLabel);

        EditorGUILayout.Space();

        soldierModel = (GameObject)EditorGUILayout.ObjectField("Soldier Model", soldierModel, typeof(GameObject), true);
        helmetModel = (GameObject)EditorGUILayout.ObjectField("Helmet Model", helmetModel, typeof(GameObject), true);

        EditorGUILayout.Space();

        helmetScale = EditorGUILayout.Slider("Helmet Scale", helmetScale, 0.001f, 0.1f);
        helmetPosition = EditorGUILayout.Vector3Field("Position Offset", helmetPosition);
        helmetRotation = EditorGUILayout.Vector3Field("Rotation", helmetRotation);

        EditorGUILayout.Space();

        if (GUILayout.Button("Attach Helmet"))
        {
            AttachHelmet();
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("1. Select the ToonSoldierModel GameObject\n2. Select the Biker_Halmet3 prefab or FBX\n3. Adjust scale and position\n4. Click 'Attach Helmet'", MessageType.Info);
    }

    void AttachHelmet()
    {
        if (soldierModel == null)
        {
            EditorUtility.DisplayDialog("Error", "Please assign the Soldier Model!", "OK");
            return;
        }

        if (helmetModel == null)
        {
            EditorUtility.DisplayDialog("Error", "Please assign the Helmet Model!", "OK");
            return;
        }

        // Find or create helmet container
        Transform soldierTransform = soldierModel.transform;
        Transform helmetContainer = soldierTransform.Find("BikerHelmet");

        if (helmetContainer == null)
        {
            GameObject container = new GameObject("BikerHelmet");
            container.transform.SetParent(soldierTransform);
            helmetContainer = container.transform;
        }

        // Clear existing helmet if any
        while (helmetContainer.childCount > 0)
        {
            DestroyImmediate(helmetContainer.GetChild(0).gameObject);
        }

        // Instantiate helmet
        GameObject helmet = PrefabUtility.InstantiatePrefab(helmetModel) as GameObject;
        if (helmet == null)
        {
            helmet = Instantiate(helmetModel);
        }

        helmet.transform.SetParent(helmetContainer);
        helmet.transform.localPosition = Vector3.zero;
        helmet.transform.localRotation = Quaternion.identity;
        helmet.transform.localScale = Vector3.one;

        // Set container transform
        helmetContainer.localPosition = helmetPosition;
        helmetContainer.localRotation = Quaternion.Euler(helmetRotation);
        helmetContainer.localScale = Vector3.one * helmetScale;

        EditorUtility.SetDirty(soldierModel);

        Debug.Log($"[AttachBikerHelmet] ✓ Attached helmet to {soldierModel.name} at scale {helmetScale}");
        EditorUtility.DisplayDialog("Success", $"Helmet attached at scale {helmetScale}\n\nYou can now adjust the BikerHelmet GameObject's transform to fine-tune position and rotation.", "OK");
    }
}
