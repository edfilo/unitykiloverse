using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Kiloverse.Mapbox
{
    /// <summary>
    /// Generates a radial gradient texture for light blob decals
    /// </summary>
    public class LightBlobTextureGenerator
    {
#if UNITY_EDITOR
        [MenuItem("KiloWorld/Generate Light Blob Texture")]
        public static void GenerateLightBlobTexture()
        {
            int size = 256;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, true);

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float maxDist = size / 2f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 pos = new Vector2(x, y);
                    float dist = Vector2.Distance(pos, center);

                    // Smooth falloff from center to edges
                    float normalizedDist = Mathf.Clamp01(dist / maxDist);
                    float alpha = 1f - Mathf.SmoothStep(0f, 1f, normalizedDist);

                    // Brighter in center, fades to transparent at edges
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();

            // Save to Assets
            string path = "Assets/KiloverseMapbox/Materials/LightBlobGradient.png";
            byte[] bytes = texture.EncodeToPNG();
            System.IO.File.WriteAllBytes(path, bytes);

            AssetDatabase.Refresh();

            // Import with correct settings
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }

            Debug.Log($"[LightBlobTextureGenerator] Created gradient texture at {path}");
        }
#endif
    }
}
