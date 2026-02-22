using UnityEngine;

namespace Kiloverse.Magic
{
    /// <summary>
    /// Builds and spins a pottery-wheel style disc that we can paint onto.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class MagicWheelSurface : MonoBehaviour
    {
        [SerializeField] private float radius = 1.1f;
        [SerializeField, Range(12, 256)] private int segments = 128;
        [SerializeField] private float rotationSpeed = 50f;
        [SerializeField] private Material surfaceMaterial;

        private Mesh generatedMesh;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private MeshCollider meshCollider;

        public Collider SurfaceCollider => meshCollider;
        public float Radius => radius;

        private void Awake()
        {
            CacheComponents();
            BuildSurface();
            ApplyMaterial();
        }

        private void OnValidate()
        {
            CacheComponents();
            BuildSurface();
            ApplyMaterial();
        }

        private void CacheComponents()
        {
            if (!meshFilter)
            {
                meshFilter = GetComponent<MeshFilter>();
            }

            if (!meshRenderer)
            {
                meshRenderer = GetComponent<MeshRenderer>();
            }

            if (!meshCollider)
            {
                meshCollider = GetComponent<MeshCollider>();
                if (!meshCollider)
                {
                    meshCollider = gameObject.AddComponent<MeshCollider>();
                }
            }
        }

        private void BuildSurface()
        {
            segments = Mathf.Clamp(segments, 12, 256);
            var vertexCount = segments + 1;
            if (generatedMesh == null)
            {
                generatedMesh = new Mesh { name = "MagicWheelSurface" };
            }
            else
            {
                generatedMesh.Clear();
            }

            var vertices = new Vector3[vertexCount];
            var normals = new Vector3[vertexCount];
            var uvs = new Vector2[vertexCount];
            var triangles = new int[segments * 3];

            vertices[0] = Vector3.zero;
            normals[0] = Vector3.up;
            uvs[0] = new Vector2(0.5f, 0.5f);

            for (var i = 0; i < segments; i++)
            {
                var angle = (float)i / segments * Mathf.PI * 2f;
                var x = Mathf.Cos(angle) * radius;
                var z = Mathf.Sin(angle) * radius;
                var idx = i + 1;
                vertices[idx] = new Vector3(x, 0f, z);
                normals[idx] = Vector3.up;
                uvs[idx] = new Vector2((x / radius + 1f) * 0.5f, (z / radius + 1f) * 0.5f);

                var triIndex = i * 3;
                triangles[triIndex] = 0;
                triangles[triIndex + 1] = idx;
                triangles[triIndex + 2] = i == segments - 1 ? 1 : idx + 1;
            }

            generatedMesh.vertices = vertices;
            generatedMesh.normals = normals;
            generatedMesh.uv = uvs;
            generatedMesh.triangles = triangles;
            generatedMesh.RecalculateBounds();

            meshFilter.sharedMesh = generatedMesh;
            meshCollider.sharedMesh = generatedMesh;
        }

        private void ApplyMaterial()
        {
            if (surfaceMaterial)
            {
                meshRenderer.sharedMaterial = surfaceMaterial;
            }
        }

        private void Update()
        {
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
        }
    }
}
