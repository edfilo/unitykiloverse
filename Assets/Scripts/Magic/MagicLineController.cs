using UnityEngine;

namespace Kiloverse.Magic
{
    /// <summary>
    /// Generates a looping wavy line that orbits around the viewer so the scene feels alive.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class MagicLineController : MonoBehaviour
    {
        [SerializeField, Min(8)] private int pointCount = 128;
        [SerializeField] private float radius = 1.5f;
        [SerializeField] private float waveAmplitude = 0.2f;
        [SerializeField] private float waveFrequency = 4f;
        [SerializeField] private float waveScrollSpeed = 1.25f;
        [SerializeField] private float rotationSpeed = 18f;
        [SerializeField] private float thickness = 0.035f;
        [SerializeField] private LineRenderer lineRenderer;

        private float timeOffset;
        private Gradient runtimeGradient;

        private void Reset()
        {
            ConfigureRenderer();
            UpdateLine(0f);
        }

        private void Awake()
        {
            ConfigureRenderer();
            UpdateLine(0f);
        }

        private void ConfigureRenderer()
        {
            if (!lineRenderer)
            {
                lineRenderer = GetComponent<LineRenderer>();
                if (!lineRenderer)
                {
                    lineRenderer = gameObject.AddComponent<LineRenderer>();
                }
            }

            lineRenderer.loop = true;
            lineRenderer.useWorldSpace = false;
            lineRenderer.widthMultiplier = thickness;
            lineRenderer.colorGradient = GetGradient();
            lineRenderer.textureMode = LineTextureMode.Stretch;
        }

        private void Update()
        {
            timeOffset += waveScrollSpeed * Time.deltaTime;
            UpdateLine(timeOffset);
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
        }

        private void UpdateLine(float offset)
        {
            var count = Mathf.Max(8, pointCount);
            if (lineRenderer.positionCount != count)
            {
                lineRenderer.positionCount = count;
            }

            for (var i = 0; i < count; i++)
            {
                var t = (float)i / count * Mathf.PI * 2f;
                var wave = Mathf.Sin(t * waveFrequency + offset) * waveAmplitude;
                var r = Mathf.Max(0.05f, radius + wave);
                var pos = new Vector3(Mathf.Cos(t) * r, Mathf.Sin(offset * 0.5f) * 0.05f, Mathf.Sin(t) * r);
                lineRenderer.SetPosition(i, pos);
            }
        }

        private Gradient GetGradient()
        {
            runtimeGradient ??= BuildDefaultGradient();
            return runtimeGradient;
        }

        private static Gradient BuildDefaultGradient()
        {
            return new Gradient
            {
                colorKeys = new[]
                {
                    new GradientColorKey(new Color(0.2f, 0.9f, 1f), 0f),
                    new GradientColorKey(new Color(0.9f, 0.2f, 1f), 0.5f),
                    new GradientColorKey(new Color(0.3f, 0.6f, 1f), 1f)
                },
                alphaKeys = new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                }
            };
        }
    }
}
