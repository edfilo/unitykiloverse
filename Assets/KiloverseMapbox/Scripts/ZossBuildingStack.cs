using Kiloverse.Mapbox;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kiloverse.Mapbox
{
    public class ZossBuildingStack : MeshModifier
    {
        /// <summary>Set by tile loader before processing each tile. True = distant tile, simplified geometry.</summary>
        public static bool SimpleLOD;

        /// <summary>Building height/floor corrections keyed by Overture building ID.</summary>
        public static Dictionary<string, float[]> Corrections;

        private static int buildingCount = 0;
        private readonly float firstFloorHeight;
        private readonly float upperFloorHeight;
        private readonly float minDoorWidth;
        private readonly float maxDoorWidth;
        private readonly float minDoorHeightRatio;
        private readonly float maxDoorHeightRatio;
        private readonly float minWindowWidth;
        private readonly float maxWindowWidth;
        private readonly float minWindowSpacing;
        private readonly float maxWindowSpacing;
        private readonly float windowHeightRatio;
        private readonly Color[] wallColors;
        private readonly float windowOffPercentage;
        private readonly float windowBrightnessVariance;
        private readonly float bottomFloorDarkening;
        private readonly int windowVarianceSeed;

        // Conversion factor from tile-space to meters (set per building in Run())
        private float tileToMeters;


public ZossBuildingStack(
            float firstFloorHeight,
            float upperFloorHeight,
            float minDoorWidth,
            float maxDoorWidth,
            float minDoorHeightRatio,
            float maxDoorHeightRatio,
            float minWindowWidth,
            float maxWindowWidth,
            float minWindowSpacing,
            float maxWindowSpacing,
            float windowHeightRatio,
            Color[] wallColors,
            float windowOffPercentage,
            float windowBrightnessVariance,
            float bottomFloorDarkening,
            int windowVarianceSeed)
        {
            this.firstFloorHeight = firstFloorHeight;
            this.upperFloorHeight = upperFloorHeight;
            this.minDoorWidth = minDoorWidth;
            this.maxDoorWidth = maxDoorWidth;
            this.minDoorHeightRatio = minDoorHeightRatio;
            this.maxDoorHeightRatio = maxDoorHeightRatio;
            this.minWindowWidth = minWindowWidth;
            this.maxWindowWidth = maxWindowWidth;
            this.minWindowSpacing = minWindowSpacing;
            this.maxWindowSpacing = maxWindowSpacing;
            this.windowHeightRatio = windowHeightRatio;
            this.wallColors = wallColors;
            this.windowOffPercentage = windowOffPercentage;
            this.windowBrightnessVariance = windowBrightnessVariance;
            this.bottomFloorDarkening = bottomFloorDarkening;
            this.windowVarianceSeed = windowVarianceSeed;
        }



public override void Run(VectorFeatureUnity feature, MeshData md, IMapInformation mapInfo)
        {
            if (md.Vertices.Count == 0 || feature == null) return;

            bool isFirstBuilding = (buildingCount == 0);
            buildingCount++;

            // 1. Calculate Height — check corrections first
            float buildingHeightMeters = 10f;
            bool corrected = false;
            if (Corrections != null && feature.Properties.ContainsKey("id"))
            {
                string overtureId = feature.Properties["id"].ToString();
                if (Corrections.TryGetValue(overtureId, out float[] fix))
                {
                    buildingHeightMeters = fix[0]; // [0]=height, [1]=floors
                    corrected = true;
                }
            }

            if (!corrected && feature.Properties.ContainsKey("height"))
            {
                buildingHeightMeters = Convert.ToSingle(feature.Properties["height"]);

                if (buildingHeightMeters <= 0f && feature.Properties.ContainsKey("num_floors"))
                {
                    int numFloors = Convert.ToInt32(feature.Properties["num_floors"]);
                    buildingHeightMeters = numFloors * 3.5f;
                }
            }
            else if (!corrected && feature.Properties.ContainsKey("num_floors"))
            {
                int numFloors = Convert.ToInt32(feature.Properties["num_floors"]);
                buildingHeightMeters = numFloors * 3.5f;
            }
            else if (!corrected && feature.Properties.ContainsKey("render_height"))
            {
                buildingHeightMeters = Convert.ToSingle(feature.Properties["render_height"]);
            }

            // Boost small buildings logic (optional, keeping consistent with previous logic)
            int seed = feature.FeatureId.GetHashCode();
            System.Random rng = new System.Random(seed);

            if (buildingHeightMeters > 2.0f && buildingHeightMeters < 4.0f)
            {
                buildingHeightMeters = 9f + (float)(rng.NextDouble() * 5.0);
            }

            // Calculate actual floor heights from num_floors data if available
            float actualFirstFloorHeight = firstFloorHeight;
            float actualUpperFloorHeight = upperFloorHeight;

            if (feature.Properties.ContainsKey("num_floors"))
            {
                int numFloors = Convert.ToInt32(feature.Properties["num_floors"]);
                if (numFloors > 0)
                {
                    // Divide building height by actual floor count
                    float avgFloorHeight = buildingHeightMeters / numFloors;

                    // First floor slightly taller (architectural realism: 115%)
                    actualFirstFloorHeight = avgFloorHeight * 1.15f;
                    actualUpperFloorHeight = avgFloorHeight;

                    // DEBUG: Log when using actual floor data
                    if (buildingHeightMeters >= 200f)
                    {
                        UnityEngine.Debug.Log($"[ZossBuildingStack] Using num_floors={numFloors}: avgFloor={avgFloorHeight:F2}m, first={actualFirstFloorHeight:F2}m, upper={actualUpperFloorHeight:F2}m");
                    }
                }
            }

            // 2. Convert to Tile Space
            // BUG FIX: Latitude compensation should NOT be applied to vertical (Y-axis) heights!
            // Mercator projection only distorts horizontal distances, not vertical.
            // Pittsburgh: latComp=0.761, which was incorrectly scaling 256m → 336m (1.314x too tall)
            var tileSize = Conversions.TileEdgeSizeInMercator(feature.TileId.Z);
            float latComp = mapInfo.GetLatitudeCompensationForLocation;
            float metersToTileHorizontal = 1.0f / ((float)tileSize * latComp); // For X/Z
            float metersToTileVertical = 1.0f / (float)tileSize;                // For Y (no latComp!)

            // Store conversion factor for UV calculation in AddQuad (uses horizontal)
            tileToMeters = 1.0f / metersToTileHorizontal;

            float totalHeightTile = buildingHeightMeters * metersToTileVertical;
            float firstFloorHeightTile = actualFirstFloorHeight * metersToTileVertical;
            float upperFloorHeightTile = actualUpperFloorHeight * metersToTileVertical;

            // 3. Move Roof Up
            int vertexCount = md.Vertices.Count;
            for (int i = 0; i < vertexCount; i++)
            {
                md.Vertices[i] = new Vector3(md.Vertices[i].x, md.Vertices[i].y + totalHeightTile, md.Vertices[i].z);
            }


            // 4. Duplicate Roof (Double Sided)
            if (md.Triangles.Count > 0 && md.Triangles[0].Count > 0)
            {
                int roofTriCount = md.Triangles[0].Count;
                List<int> reversedRoofTris = new List<int>(roofTriCount);
                for (int i = roofTriCount - 1; i >= 0; i--)
                {
                    reversedRoofTris.Add(md.Triangles[0][i]);
                }
                md.Triangles[0].AddRange(reversedRoofTris);
            }

            // 5. Initialize Submeshes
            // Submesh 0: Walls (Roof is already here)
            // Submesh 1: Windows/Doors (Lit)
            while (md.Triangles.Count < 2) md.Triangles.Add(new List<int>());

            // Initialize UVs/Colors
            if (md.UV.Count == 0) md.UV.Add(new List<Vector2>()); // UV0
            while (md.UV.Count < 2) md.UV.Add(new List<Vector2>()); // UV1 (Colors)

            // Fill existing vertices (roof) with default data
            int currentVertCount = md.Vertices.Count;
            while (md.UV[0].Count < currentVertCount) md.UV[0].Add(Vector2.zero);
            while (md.UV[1].Count < currentVertCount) md.UV[1].Add(Vector2.zero);
            
            // Fill tangents for roof vertices
            Vector4 defaultTangent = new Vector4(1, 0, 0, 1);
            while (md.Tangents.Count < currentVertCount) md.Tangents.Add(defaultTangent);

            // Pick a random wall color for this building
            Color wallColor = wallColors[rng.Next(0, wallColors.Length)];

            // 6. Build Walls
            // Simple approach: walk through vertices in order, connect each to the next
            // Last vertex connects back to first (closing the loop)
            int wallsCreated = 0;
            for (int i = 0; i < vertexCount; i++)
            {
                var v1 = md.Vertices[i];
                var v2 = md.Vertices[(i + 1) % vertexCount];

                // Reset Y to ground (roof is at totalHeightTile)
                Vector3 groundV1 = new Vector3(v1.x, v1.y - totalHeightTile, v1.z);
                Vector3 groundV2 = new Vector3(v2.x, v2.y - totalHeightTile, v2.z);

                float dist = Vector3.Distance(groundV1, groundV2);
                if (dist >= 0.001f) wallsCreated++;

                BuildWallSegment(md, groundV1, groundV2, totalHeightTile, firstFloorHeightTile, upperFloorHeightTile, wallColor, seed + i);
            }

            // DEBUG: Log tall building FINAL mesh details (after walls built)
            if (buildingHeightMeters >= 200f)
            {
                float maxY = float.MinValue;
                float minY = float.MaxValue;
                foreach (var v in md.Vertices)
                {
                    if (v.y > maxY) maxY = v.y;
                    if (v.y < minY) minY = v.y;
                }
                float meshHeightTile = maxY - minY;
                float tileToMetersVertical = (float)tileSize;  // Vertical conversion (no latComp)
                float meshHeightMeters = meshHeightTile * tileToMetersVertical;
                UnityEngine.Debug.Log($"[ZossBuildingStack] FINAL MESH: height={buildingHeightMeters:F1}m → meshHeight={meshHeightMeters:F1}m (tile={meshHeightTile:F6}, tileToMetersVertical={tileToMetersVertical:F1}) | vertexCount={md.Vertices.Count}, wallsCreated={wallsCreated}");
            }
        }

        private void BuildWallSegment(MeshData md, Vector3 v1, Vector3 v2, float totalHeight, float firstH, float upperH, Color wallColor, int seed)
        {
            System.Random rng = new System.Random(seed);

            Vector3 direction = (v2 - v1).normalized;
            Vector3 normal = Vector3.Cross(direction, Vector3.up).normalized;

            float wallLength = Vector3.Distance(v1, v2);
            if (wallLength < 0.001f) return;

            // Create full base wall (submesh 0)
            AddQuad(md, v1, v1.y, v2, v1.y + totalHeight, normal, wallColor, 0);

            Vector3 offset = normal * 0.00001f;

            if (SimpleLOD)
            {
                // Distant LOD: single emissive quad per wall (~60% coverage)
                // Looks like one big glowing window from afar
                float insetH = wallLength * 0.15f;
                float insetV = totalHeight * 0.12f;
                float brightness = 0.5f + (float)(rng.NextDouble() * 0.5);
                Color glowColor = Color.white * brightness;
                AddQuad(md,
                    v1 + offset + direction * insetH, v1.y + insetV,
                    v2 + offset - direction * insetH, v1.y + totalHeight - insetV,
                    normal, glowColor, 1);
                return;
            }

            // Full detail: per-floor windows and doors
            float currentY = v1.y;
            float remainingHeight = totalHeight;
            int floorIndex = 0;

            while (remainingHeight > 0.0001f)
            {
                float currentFloorH = (floorIndex == 0) ? firstH : upperH;
                if (remainingHeight < currentFloorH) currentFloorH = remainingHeight;

                if (floorIndex == 0)
                {
                    BuildFirstFloorLights(md, v1 + offset, v2 + offset, currentY, currentFloorH, wallLength, direction, normal, rng);
                }
                else
                {
                    BuildUpperFloorLights(md, v1 + offset, v2 + offset, currentY, currentFloorH, wallLength, direction, normal, rng);
                }

                currentY += currentFloorH;
                remainingHeight -= currentFloorH;
                floorIndex++;
            }
        }

private void BuildFirstFloorLights(MeshData md, Vector3 v1, Vector3 v2, float yBottom, float height, float length, Vector3 dir, Vector3 normal, System.Random rng)
        {
            float doorRatio = (float)(rng.NextDouble() * 0.2 + 0.2);
            float doorWidth = length * doorRatio;

            float doorHeightRatio = (float)(rng.NextDouble() * (maxDoorHeightRatio - minDoorHeightRatio) + minDoorHeightRatio);
            float doorHeight = height * doorHeightRatio;

            // Position door
            float doorStart = (float)(rng.NextDouble() * (0.9 - doorRatio - 0.1) + 0.1) * length;
            float doorEnd = doorStart + doorWidth;

            // Apply bottom floor darkening (light spill physics)
            float darkeningMultiplier = 1.0f - bottomFloorDarkening;
            Color doorColor = Color.white * darkeningMultiplier;

            // Create emissive door quad (submesh 1) with darkening applied
            AddQuad(md,
                v1 + dir * doorStart, yBottom,
                v1 + dir * doorEnd, yBottom + doorHeight,
                normal, doorColor, 1);
        }

private void BuildUpperFloorLights(MeshData md, Vector3 v1, Vector3 v2, float yBottom, float height, float length, Vector3 dir, Vector3 normal, System.Random rng)
        {
            int windowCount = rng.Next(1, 5);

            float windowH = height * windowHeightRatio;
            float windowY = yBottom + (height - windowH) / 2.0f;

            float segmentLength = length / windowCount;
            float windowW = segmentLength * 0.6f;
            float padding = (segmentLength - windowW) / 2.0f;

            for (int i = 0; i < windowCount; i++)
            {
                // Use variance seed + window index for deterministic randomization
                System.Random windowRng = new System.Random(rng.Next() + windowVarianceSeed + i);

                // Randomly turn off windows (dead rooms)
                bool windowOff = windowRng.NextDouble() < windowOffPercentage;
                if (windowOff) continue; // Skip this window

                // Apply random brightness variance (±windowBrightnessVariance)
                float brightnessMultiplier = 1.0f + ((float)windowRng.NextDouble() * 2.0f - 1.0f) * windowBrightnessVariance;
                brightnessMultiplier = Mathf.Clamp(brightnessMultiplier, 0.1f, 2.0f); // Clamp to reasonable range

                Color windowColor = Color.white * brightnessMultiplier;

                float segStart = i * segmentLength;
                float wStart = segStart + padding;
                float wEnd = wStart + windowW;

                // Create window grid with panes (submesh 1) with varied brightness
                // PERFORMANCE FIX: 1×1 = single quad per window (6x reduction from 2×3 grid)
                // This reduces vertices from 1M+ to ~170K per urban tile
                AddWindowGrid(md,
                    v1 + dir * wStart, windowY,
                    v1 + dir * wEnd, windowY + windowH,
                    normal, windowColor, 1, 1, 1); // Single solid window (was 2×3 = 6 panes)
            }
        }

private void AddQuad(MeshData md, Vector3 bottomLeft, float yBottom, Vector3 bottomRight, float yTop, Vector3 normal, Color color, int submeshIndex)
        {
            // DEBUG: Log first wall quad for tall buildings
            if (submeshIndex == 0 && md.Vertices.Count < 100 && (yTop - yBottom) > 0.1f)
            {
                UnityEngine.Debug.Log($"[AddQuad] Creating wall: yBottom={yBottom:F6}, yTop={yTop:F6}, height={yTop - yBottom:F6}");
            }

            // Create proper vertical quad (bottomLeft/bottomRight are at same Y initially)
            Vector3 bl = new Vector3(bottomLeft.x, yBottom, bottomLeft.z);
            Vector3 br = new Vector3(bottomRight.x, yBottom, bottomRight.z);
            Vector3 tl = new Vector3(bottomLeft.x, yTop, bottomLeft.z);
            Vector3 tr = new Vector3(bottomRight.x, yTop, bottomRight.z);

            int startIndex = md.Vertices.Count;

            md.Vertices.Add(bl);
            md.Vertices.Add(br);
            md.Vertices.Add(tl);
            md.Vertices.Add(tr);

            // Normals
            md.Normals.Add(normal);
            md.Normals.Add(normal);
            md.Normals.Add(normal);
            md.Normals.Add(normal);

            // Tangents (required for URP lighting)
            Vector3 tangent = Vector3.Cross(normal, Vector3.up);
            if (tangent.sqrMagnitude < 0.001f) tangent = Vector3.Cross(normal, Vector3.forward);
            tangent.Normalize();
            Vector4 tangent4 = new Vector4(tangent.x, tangent.y, tangent.z, 1.0f);
            md.Tangents.Add(tangent4);
            md.Tangents.Add(tangent4);
            md.Tangents.Add(tangent4);
            md.Tangents.Add(tangent4);

            // UVs (World-space scaled in meters to prevent stretching)
            // Convert tile-space dimensions to meters
            float quadWidthTile = Vector3.Distance(bl, br);
            float quadHeightTile = yTop - yBottom;

            float quadWidthMeters = quadWidthTile * tileToMeters;
            float quadHeightMeters = quadHeightTile * tileToMeters;

            // Scale UVs by meter dimensions so texture tiles correctly
            // Material tiling setting will control repetition density
            md.UV[0].Add(new Vector2(0, 0));
            md.UV[0].Add(new Vector2(quadWidthMeters, 0));
            md.UV[0].Add(new Vector2(0, quadHeightMeters));
            md.UV[0].Add(new Vector2(quadWidthMeters, quadHeightMeters));

            // Colors (UV1 for vertex colors - used by custom shaders, ignored by URP Lit)
            md.UV[1].Add(new Vector2(color.r, color.g));
            md.UV[1].Add(new Vector2(color.r, color.g));
            md.UV[1].Add(new Vector2(color.r, color.g));
            md.UV[1].Add(new Vector2(color.r, color.g));

            // Triangles
            md.Triangles[submeshIndex].Add(startIndex);
            md.Triangles[submeshIndex].Add(startIndex + 1);
            md.Triangles[submeshIndex].Add(startIndex + 2);
            
            md.Triangles[submeshIndex].Add(startIndex + 2);
            md.Triangles[submeshIndex].Add(startIndex + 1);
            md.Triangles[submeshIndex].Add(startIndex + 3);
        }
    

private void AddWindowGrid(MeshData md, Vector3 bottomLeft, float yBottom, Vector3 bottomRight, float yTop, Vector3 normal, Color color, int submeshIndex, int columns = 4, int rows = 6)
        {
            // PERFORMANCE NOTE: columns=1, rows=1 creates single solid window (4 vertices)
            // vs columns=2, rows=3 creates 6-pane grid (24 vertices) = 6x memory cost
            // For a 64-floor building: 1×1 = 768 verts, 2×3 = 4608 verts per window

            // Calculate total dimensions
            Vector3 dirHorizontal = (bottomRight - bottomLeft).normalized;
            float totalWidth = Vector3.Distance(bottomLeft, bottomRight);
            float totalHeight = yTop - yBottom;

            // Frame thickness - bigger gaps for bloom visibility (10% of pane width)
            float frameThickness = totalWidth * 0.10f / columns;
            
            // Calculate pane dimensions
            float paneWidth = (totalWidth - frameThickness * (columns - 1)) / columns;
            float paneHeight = (totalHeight - frameThickness * (rows - 1)) / rows;
            
            // Create each pane
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < columns; col++)
                {
                    // Calculate pane position
                    float xOffset = col * (paneWidth + frameThickness);
                    float yOffset = row * (paneHeight + frameThickness);
                    
                    Vector3 paneBottomLeft = bottomLeft + dirHorizontal * xOffset;
                    Vector3 paneBottomRight = paneBottomLeft + dirHorizontal * paneWidth;
                    float paneYBottom = yBottom + yOffset;
                    float paneYTop = paneYBottom + paneHeight;
                    
                    // Add single pane quad
                    AddQuad(md, paneBottomLeft, paneYBottom, paneBottomRight, paneYTop, normal, color, submeshIndex);
                }
            }
        }
}
}
