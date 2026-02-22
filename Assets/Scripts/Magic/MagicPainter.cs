using System.Collections.Generic;
using UnityEngine;

namespace Kiloverse.Magic
{
    /// <summary>
    /// Lets the user paint strokes onto the spinning wheel using touch or mouse input.
    /// </summary>
    [DisallowMultipleComponent]
    public class MagicPainter : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private MagicWheelSurface wheelSurface;
        [SerializeField] private Material strokeMaterial;
        [SerializeField] private float strokeSize = 0.06f;
        [SerializeField] private float minDistanceBetweenStrokes = 0.03f;
        [SerializeField] private float strokeDepthOffset = 0.0025f;
        [SerializeField] private int maxStrokeCount = 400;
        [SerializeField] private Transform strokeContainer;
        [SerializeField] private MagicSynthController synthController;

        private readonly Queue<GameObject> spawnedStrokes = new();
        private Vector3 lastStrokePosition;
        private bool hasLastStroke;
        private static Mesh strokeMesh;
        private static MaterialPropertyBlock propertyBlock;
        private Gradient strokeGradient;
        private bool isTouchingWheel;

        private void Awake()
        {
            if (!targetCamera)
            {
                targetCamera = Camera.main;
            }

            if (!wheelSurface)
            {
                wheelSurface = GetComponent<MagicWheelSurface>();
            }

            if (!strokeContainer)
            {
                var container = new GameObject("MagicStrokeContainer");
                container.transform.SetParent(wheelSurface ? wheelSurface.transform : transform, false);
                strokeContainer = container.transform;
            }

            EnsureDefaultGradient();
            EnsureStrokeMesh();
        }

        private void OnDisable()
        {
            EndInteraction();
        }

        private void EnsureDefaultGradient()
        {
            if (strokeGradient != null)
            {
                return;
            }

            strokeGradient = new Gradient();
            var colorKeys = new[]
            {
                new GradientColorKey(new Color(1f, 0.45f, 0.4f), 0f),
                new GradientColorKey(new Color(0.6f, 0.8f, 1f), 0.5f),
                new GradientColorKey(new Color(0.7f, 0.3f, 1f), 1f)
            };
            var alphaKeys = new[]
            {
                new GradientAlphaKey(0.95f, 0f),
                new GradientAlphaKey(0.95f, 1f)
            };
            strokeGradient.SetKeys(colorKeys, alphaKeys);
        }

        private void EnsureStrokeMesh()
        {
            if (strokeMesh != null)
            {
                return;
            }

            strokeMesh = new Mesh { name = "MagicStrokeBlob" };
            
            // Create a simple octagon approximation for a blob
            var vertices = new List<Vector3> { Vector3.zero }; // Center
            var uvs = new List<Vector2> { new Vector2(0.5f, 0.5f) };
            var tris = new List<int>();
            var colors = new List<Color> { new Color(1, 1, 1, 1) };

            int segments = 16;
            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                float r = 0.5f;
                // Add some random variation for "blob" look if desired, or keep perfect circle
                // r *= Random.Range(0.9f, 1.1f); 
                
                float x = Mathf.Cos(angle) * r;
                float y = Mathf.Sin(angle) * r;
                
                vertices.Add(new Vector3(x, 0f, y));
                uvs.Add(new Vector2(x + 0.5f, y + 0.5f));
                
                // Soft edges using vertex colors (alpha fade at edges)
                colors.Add(new Color(1, 1, 1, 0)); 

                if (i > 0)
                {
                    tris.Add(0);
                    tris.Add(i + 1);
                    tris.Add(i);
                }
            }

            strokeMesh.SetVertices(vertices);
            strokeMesh.SetUVs(0, uvs);
            strokeMesh.SetTriangles(tris, 0);
            strokeMesh.SetColors(colors);
            strokeMesh.RecalculateNormals();
            strokeMesh.RecalculateBounds();
        }

        private void Update()
        {
            if (!targetCamera || !wheelSurface || !wheelSurface.SurfaceCollider)
            {
                EndInteraction();
                return;
            }

            if (!TryGetPointerPosition(out var pointerPosition))
            {
                EndInteraction();
                return;
            }

            var ray = targetCamera.ScreenPointToRay(pointerPosition);
            
            // Create a mathematical plane for the wheel surface
            // The plane passes through the wheel's position and faces its up vector
            var plane = new Plane(wheelSurface.transform.up, wheelSurface.transform.position);

            if (!plane.Raycast(ray, out var enter))
            {
                Debug.Log("Raycast missed the infinite plane.");
                EndInteraction();
                return;
            }

            var hitPoint = ray.GetPoint(enter);
            var hitNormal = wheelSurface.transform.up;
            Debug.Log($"Raycast hit plane at {hitPoint}");

            var localPoint = wheelSurface.transform.InverseTransformPoint(hitPoint);
            var radial = new Vector2(localPoint.x, localPoint.z);
            
            // Allow painting outside the physical radius (deform logic will handle visuals)
            var normalizedRadius = wheelSurface.Radius <= 0f ? 0f : radial.magnitude / wheelSurface.Radius;
            
            UpdateSynth(normalizedRadius);

            var point = hitPoint + hitNormal * strokeDepthOffset;
            if (hasLastStroke && Vector3.Distance(point, lastStrokePosition) < minDistanceBetweenStrokes)
            {
                return;
            }

            SpawnStroke(point, hitNormal, wheelSurface.transform);
            lastStrokePosition = point;
            hasLastStroke = true;
        }

        private bool TryGetPointerPosition(out Vector2 position)
        {
            if (Input.touchCount > 0)
            {
                var touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    position = default;
                    return false;
                }

                position = touch.position;
                Debug.Log($"Touch detected at {position}");
                return true;
            }

            if (Input.GetMouseButton(0))
            {
                position = Input.mousePosition;
                Debug.Log($"Mouse detected at {position}");
                return true;
            }

            position = default;
            return false;
        }

        private void UpdateSynth(float normalizedRadius)
        {
            if (!synthController)
            {
                return;
            }

            if (!isTouchingWheel)
            {
                synthController.BeginStroke(normalizedRadius);
                isTouchingWheel = true;
            }
            else
            {
                synthController.UpdateStroke(normalizedRadius);
            }
        }

        private void EndInteraction()
        {
            if (isTouchingWheel)
            {
                synthController?.EndStroke();
            }

            isTouchingWheel = false;
            hasLastStroke = false;
        }

        private void SpawnStroke(Vector3 position, Vector3 normal, Transform parent)
        {
            var stroke = new GameObject("Stroke");
            stroke.transform.SetParent(strokeContainer ? strokeContainer : parent, true);
            stroke.transform.position = position;
            var forward = targetCamera ? targetCamera.transform.forward : Vector3.forward;
            stroke.transform.rotation = Quaternion.LookRotation(forward, normal);

            var sizeJitter = Random.Range(0.85f, 1.15f);
            var scale = strokeSize * sizeJitter;
            stroke.transform.localScale = new Vector3(scale, scale, scale);

            var filter = stroke.AddComponent<MeshFilter>();
            filter.sharedMesh = strokeMesh;

            var renderer = stroke.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = strokeMaterial;

            if (propertyBlock == null)
            {
                propertyBlock = new MaterialPropertyBlock();
            }

            var t = Mathf.Repeat(Time.time * 0.15f + Random.value * 0.25f, 1f);
            var color = strokeGradient.Evaluate(t);
            propertyBlock.SetColor("_BaseColor", color);
            propertyBlock.SetColor("_Color", color);
            renderer.SetPropertyBlock(propertyBlock);

            spawnedStrokes.Enqueue(stroke);
            while (spawnedStrokes.Count > maxStrokeCount)
            {
                var oldest = spawnedStrokes.Dequeue();
                if (oldest)
                {
                    Destroy(oldest);
                }
            }
        }
    }
}
