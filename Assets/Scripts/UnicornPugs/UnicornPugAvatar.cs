using UnityEngine;

[DisallowMultipleComponent]
public class UnicornPugAvatar : MonoBehaviour
{
    [Header("Palette")]
    [SerializeField] private Color pugTan = new(0.84f, 0.69f, 0.47f);
    [SerializeField] private Color pugBrown = new(0.36f, 0.25f, 0.17f);
    [SerializeField] private Color hornPink = new(1f, 0.41f, 0.71f);
    [SerializeField] private Color hornBlue = new(0.44f, 0.84f, 1f);
    [SerializeField] private Color mint = new(0.48f, 1f, 0.84f);

    private const string AvatarRootName = "UnicornPugVisual";

    private void Awake()
    {
        Rebuild();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying && gameObject.scene.IsValid())
        {
            Rebuild();
        }
    }
#endif

    public void Rebuild()
    {
        Transform oldRoot = transform.Find(AvatarRootName);
        if (oldRoot != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(oldRoot.gameObject);
            }
            else
#endif
            {
                Destroy(oldRoot.gameObject);
            }
        }

        GameObject avatarRoot = new(AvatarRootName);
        avatarRoot.transform.SetParent(transform, false);

        Material tanMaterial = CreateMaterial("PugTan", pugTan);
        Material brownMaterial = CreateMaterial("PugBrown", pugBrown);
        Material hornPinkMaterial = CreateMaterial("HornPink", hornPink, true);
        Material hornBlueMaterial = CreateMaterial("HornBlue", hornBlue, true);

        GameObject body = CreatePrimitive("Body", PrimitiveType.Capsule, avatarRoot.transform, new Vector3(0f, 0.95f, 0f), new Vector3(1.05f, 0.75f, 1.45f), tanMaterial);
        body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        GameObject chest = CreatePrimitive("Chest", PrimitiveType.Sphere, avatarRoot.transform, new Vector3(0f, 1.1f, 0.6f), new Vector3(0.95f, 0.8f, 0.8f), tanMaterial);
        GameObject head = CreatePrimitive("Head", PrimitiveType.Sphere, avatarRoot.transform, new Vector3(0f, 1.45f, 0.95f), new Vector3(0.92f, 0.84f, 0.88f), tanMaterial);
        GameObject snout = CreatePrimitive("Snout", PrimitiveType.Sphere, avatarRoot.transform, new Vector3(0f, 1.28f, 1.42f), new Vector3(0.56f, 0.34f, 0.58f), tanMaterial);
        GameObject nose = CreatePrimitive("Nose", PrimitiveType.Sphere, avatarRoot.transform, new Vector3(0f, 1.28f, 1.67f), new Vector3(0.18f, 0.12f, 0.14f), brownMaterial);

        CreatePrimitive("LeftEar", PrimitiveType.Capsule, avatarRoot.transform, new Vector3(-0.28f, 1.78f, 1.02f), new Vector3(0.18f, 0.36f, 0.18f), brownMaterial).transform.localRotation = Quaternion.Euler(0f, 0f, 22f);
        CreatePrimitive("RightEar", PrimitiveType.Capsule, avatarRoot.transform, new Vector3(0.28f, 1.78f, 1.02f), new Vector3(0.18f, 0.36f, 0.18f), brownMaterial).transform.localRotation = Quaternion.Euler(0f, 0f, -22f);

        CreatePrimitive("LeftEyePatch", PrimitiveType.Sphere, avatarRoot.transform, new Vector3(-0.2f, 1.48f, 1.3f), new Vector3(0.24f, 0.2f, 0.12f), brownMaterial);
        CreatePrimitive("RightEyePatch", PrimitiveType.Sphere, avatarRoot.transform, new Vector3(0.2f, 1.48f, 1.3f), new Vector3(0.24f, 0.2f, 0.12f), brownMaterial);

        CreateLeg(avatarRoot.transform, "FrontLeftLeg", new Vector3(-0.32f, 0.45f, 0.62f), tanMaterial, brownMaterial);
        CreateLeg(avatarRoot.transform, "FrontRightLeg", new Vector3(0.32f, 0.45f, 0.62f), tanMaterial, brownMaterial);
        CreateLeg(avatarRoot.transform, "BackLeftLeg", new Vector3(-0.32f, 0.45f, -0.55f), tanMaterial, brownMaterial);
        CreateLeg(avatarRoot.transform, "BackRightLeg", new Vector3(0.32f, 0.45f, -0.55f), tanMaterial, brownMaterial);

        GameObject tail = CreatePrimitive("Tail", PrimitiveType.Cylinder, avatarRoot.transform, new Vector3(0f, 1.18f, -1f), new Vector3(0.14f, 0.28f, 0.14f), brownMaterial);
        tail.transform.localRotation = Quaternion.Euler(-30f, 0f, 70f);

        GameObject hornBaseObject = CreatePrimitive("HornBase", PrimitiveType.Cylinder, avatarRoot.transform, new Vector3(0f, 1.95f, 1.02f), new Vector3(0.1f, 0.24f, 0.1f), hornPinkMaterial);
        hornBaseObject.transform.localRotation = Quaternion.Euler(15f, 0f, 0f);

        GameObject hornTipObject = CreatePrimitive("HornTip", PrimitiveType.Cylinder, avatarRoot.transform, new Vector3(0f, 2.21f, 1.11f), new Vector3(0.06f, 0.18f, 0.06f), hornBlueMaterial);
        hornTipObject.transform.localRotation = Quaternion.Euler(15f, 0f, 0f);

        CreateHornParticles(hornTipObject.transform, hornPink, hornBlue, mint);
        CreateRainbowTrail(head.transform, hornPink, hornBlue, mint);

        // Keep the head visible in first person by disabling shadows only.
        foreach (Renderer renderer in avatarRoot.GetComponentsInChildren<Renderer>())
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }
    }

    private static void CreateLeg(Transform parent, string name, Vector3 localPosition, Material legMaterial, Material pawMaterial)
    {
        GameObject leg = CreatePrimitive(name, PrimitiveType.Cylinder, parent, localPosition, new Vector3(0.16f, 0.45f, 0.16f), legMaterial);
        GameObject paw = CreatePrimitive($"{name}Paw", PrimitiveType.Sphere, parent, localPosition + new Vector3(0f, -0.42f, 0.02f), new Vector3(0.18f, 0.1f, 0.22f), pawMaterial);
        paw.transform.SetParent(leg.transform.parent, true);
    }

    private static GameObject CreatePrimitive(string name, PrimitiveType primitiveType, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject primitive = GameObject.CreatePrimitive(primitiveType);
        primitive.name = name;
        primitive.transform.SetParent(parent, false);
        primitive.transform.localPosition = localPosition;
        primitive.transform.localScale = localScale;

        Collider collider = primitive.GetComponent<Collider>();
        if (collider != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Object.DestroyImmediate(collider);
            }
            else
#endif
            {
                Object.Destroy(collider);
            }
        }

        Renderer renderer = primitive.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        return primitive;
    }

    private static Material CreateMaterial(string label, Color color, bool emissive = false)
    {
        Material material = new(Shader.Find("Universal Render Pipeline/Lit"));
        material.name = label;
        material.color = color;
        if (emissive)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 1.4f);
        }
        return material;
    }

    private static void CreateHornParticles(Transform target, Color start, Color end, Color accent)
    {
        ParticleSystem particles = target.gameObject.AddComponent<ParticleSystem>();
        var main = particles.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 64;

        var emission = particles.emission;
        emission.rateOverTime = 14f;

        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 12f;
        shape.radius = 0.02f;

        var colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(new Gradient
        {
            colorKeys = new[]
            {
                new GradientColorKey(start, 0f),
                new GradientColorKey(end, 0.6f),
                new GradientColorKey(accent, 1f)
            },
            alphaKeys = new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        });
    }

    private static void CreateRainbowTrail(Transform target, Color a, Color b, Color c)
    {
        TrailRenderer trail = target.gameObject.AddComponent<TrailRenderer>();
        trail.time = 0.4f;
        trail.minVertexDistance = 0.03f;
        trail.startWidth = 0.12f;
        trail.endWidth = 0f;
        trail.alignment = LineAlignment.View;
        trail.material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        trail.colorGradient = new Gradient
        {
            colorKeys = new[]
            {
                new GradientColorKey(a, 0f),
                new GradientColorKey(b, 0.45f),
                new GradientColorKey(c, 1f)
            },
            alphaKeys = new[]
            {
                new GradientAlphaKey(0.8f, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        };
    }
}
