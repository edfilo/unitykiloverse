using Kiloverse.Mapbox;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kiloverse.Mapbox
{
    /// <summary>
    /// Custom road mesh generator that respects Overture Maps width data.
    /// Uses actual road width from properties, falls back to class-based defaults.
    /// </summary>
    public class ZossRoadStack : MeshModifier
    {
        // Default widths by road class (in meters)
        private static readonly System.Collections.Generic.Dictionary<string, float> DefaultWidths = new System.Collections.Generic.Dictionary<string, float>
        {
            { "motorway", 12f },
            { "trunk", 10f },
            { "primary", 8f },
            { "secondary", 7f },
            { "tertiary", 6f },
            { "residential", 5f },
            { "service", 3.5f },
            { "pedestrian", 2f },
            { "footway", 1.5f },
            { "path", 1f }
        };

        private const float FallbackWidth = 5f; // Default if no class/width data

        public override void Run(VectorFeatureUnity feature, MeshData md, IMapInformation map)
        {
            if (feature.Points == null || feature.Points.Count == 0)
            {
                return;
            }

            // Get road width from Overture properties
            float roadWidth = GetRoadWidth(feature);

            // Convert to tile space (accounting for latitude compensation)
            var latComp = GetLatitudeCompensation(map);
            var tileSize = Conversions.TileEdgeSizeInMercator(feature.TileId.Z);
            float metersToTile = 1.0f / ((float)tileSize * latComp);

            float widthInTile = roadWidth * metersToTile;

            // Generate line mesh with proper width
            GenerateLineMesh(feature, md, widthInTile);

            // Debug log for verification (only for large roads)
            if (roadWidth >= 8f && feature.Properties.ContainsKey("class"))
            {
                string roadClass = feature.Properties["class"]?.ToString() ?? "unknown";
                Debug.Log($"[ZossRoadStack] Road class='{roadClass}' width={roadWidth:F1}m (tile: {widthInTile:F4})");
            }
        }

        private float GetRoadWidth(VectorFeatureUnity feature)
        {
            // 1. Try direct 'width' property from Overture
            if (feature.Properties.ContainsKey("width"))
            {
                try
                {
                    float width = Convert.ToSingle(feature.Properties["width"]);
                    if (width > 0 && width < 100) // Sanity check (max 100m wide road)
                    {
                        return width;
                    }
                }
                catch { }
            }

            // 2. Calculate from lanes if available
            if (feature.Properties.ContainsKey("lanes"))
            {
                try
                {
                    int lanes = Convert.ToInt32(feature.Properties["lanes"]);
                    if (lanes > 0 && lanes < 20) // Sanity check
                    {
                        float laneWidth = 3.5f; // Standard lane width in meters
                        return lanes * laneWidth;
                    }
                }
                catch { }
            }

            // 3. Fall back to class-based defaults
            if (feature.Properties.ContainsKey("class"))
            {
                string roadClass = feature.Properties["class"]?.ToString()?.ToLower();
                if (roadClass != null && DefaultWidths.TryGetValue(roadClass, out float defaultWidth))
                {
                    return defaultWidth;
                }
            }

            // 4. Ultimate fallback
            return FallbackWidth;
        }

        private float GetLatitudeCompensation(IMapInformation map)
        {
            // Use interface property directly
            return map.GetLatitudeCompensationForLocation;
        }

        private void GenerateLineMesh(VectorFeatureUnity feature, MeshData md, float width)
        {
            // feature.Points is List<List<Vector3>> - handle all line segments
            if (feature.Points == null || feature.Points.Count == 0) return;

            // Initialize submeshes if needed
            if (md.Triangles.Count == 0) md.Triangles.Add(new List<int>());
            if (md.UV.Count == 0) md.UV.Add(new List<Vector2>());

            Vector3 up = Vector3.up;
            float halfWidth = width * 0.5f;

            // Process each line segment
            foreach (var points in feature.Points)
            {
                if (points == null || points.Count < 2) continue;

                int startVertexIndex = md.Vertices.Count;

                // Generate vertices along the line
                for (int i = 0; i < points.Count; i++)
                {
                    Vector3 point = points[i];
                    Vector3 direction;

                    // Calculate perpendicular direction
                    if (i == 0)
                    {
                        direction = (points[i + 1] - points[i]).normalized;
                    }
                    else if (i == points.Count - 1)
                    {
                        direction = (points[i] - points[i - 1]).normalized;
                    }
                    else
                    {
                        direction = ((points[i + 1] - points[i - 1]) * 0.5f).normalized;
                    }

                    Vector3 perpendicular = Vector3.Cross(up, direction).normalized;

                    // Create two vertices (left and right side of road)
                    md.Vertices.Add(point - perpendicular * halfWidth);
                    md.Vertices.Add(point + perpendicular * halfWidth);

                    md.Normals.Add(up);
                    md.Normals.Add(up);

                    // UV coordinates
                    float t = (float)i / (points.Count - 1);
                    md.UV[0].Add(new Vector2(0, t));
                    md.UV[0].Add(new Vector2(1, t));
                }

                // Generate triangles for this segment
                for (int i = 0; i < points.Count - 1; i++)
                {
                    int baseIndex = startVertexIndex + i * 2;

                    // Triangle 1
                    md.Triangles[0].Add(baseIndex);
                    md.Triangles[0].Add(baseIndex + 2);
                    md.Triangles[0].Add(baseIndex + 1);

                    // Triangle 2
                    md.Triangles[0].Add(baseIndex + 1);
                    md.Triangles[0].Add(baseIndex + 2);
                    md.Triangles[0].Add(baseIndex + 3);
                }
            }
        }
    }
}
