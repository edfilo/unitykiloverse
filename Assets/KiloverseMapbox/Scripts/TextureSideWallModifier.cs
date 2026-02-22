// Force recompile v2
using System;
using System.Collections.Generic;
using Mapbox.BaseModule.Data;
using Mapbox.BaseModule.Map;
using Mapbox.BaseModule.Utilities;
using Mapbox.BaseModule.Data.Interfaces;
using Mapbox.VectorModule.MeshGeneration.MeshModifiers;
using UnityEngine;

namespace Kiloverse.Mapbox
{
    public class TextureSideWallModifier : MeshModifier, IPolygonMeshModifier
    {
        private struct WallSegmentData
        {
            public Vector3 firstVertex;
            public Vector3 secondVertex;
            public float length;
            public Vector3 normal;
            public Vector3 direction;
            public float columnScaleRatio;
            public float rightOfEdgeUv;
            public float currentY1;
            public float currentY2;
        }

        private AtlasInfo _atlasInfo;
        private bool _useRandomHeights;
        private bool _useRandomColors;

        private AtlasEntity _currentFacade;
        private Rect _currentTextureRect;
        private float _singleFloorHeight;
        private float _scaledFirstFloorHeight;
        private float _scaledTopFloorHeight;
        private float _scaledPreferredWallLength;
        private float _singleColumnLength;

        private float finalFirstHeight;
        private float finalTopHeight;
        private float finalMidHeight;
        private float finalLeftOverRowHeight;

        private float columnScaleRatio;
        private float rightOfEdgeUv;
        private Vector3 wallNormal;
        private Vector3 wallDirection;

        private float currentY1;
        private float currentY2;
        private float _wallSizeEpsilon = 0.99f;
        private float _narrowWallWidthDelta = 0.01f;
        private float _shortRowHeightDelta = 0.015f;
        private float _minWallLength;

        private Vector3 wallSegmentFirstVertex;
        private Vector3 wallSegmentSecondVertex;
        private float wallSegmentLength;

        private const bool EnableVerboseLogging = true;

        public TextureSideWallModifier(AtlasInfo atlasInfo, bool useRandomHeights = false, bool useRandomColors = false)
        {
            _atlasInfo = atlasInfo;
            _useRandomHeights = useRandomHeights;
            _useRandomColors = useRandomColors;
            if (_atlasInfo != null && _atlasInfo.Textures != null)
            {
                foreach (var atlas in _atlasInfo.Textures)
                {
                    atlas?.CalculateParameters();
                }
            }
        }

        public override void Initialize()
        {
            base.Initialize();
        }

public override void Run(VectorFeatureUnity feature, MeshData md, IMapInformation mapInfo)
        {
            if (md.Vertices.Count == 0 || feature == null)
                return;

            // 1. Select a random facade from the atlas
            if (_atlasInfo == null || _atlasInfo.Textures == null || _atlasInfo.Textures.Count == 0)
            {
                UnityEngine.Debug.LogWarning("[KILOVERSE] No AtlasInfo or Textures found!");
                return;
            }

            int seed = feature.Data.Id.GetHashCode();
            var random = new System.Random(seed);
            _currentFacade = _atlasInfo.Textures[random.Next(_atlasInfo.Textures.Count)];
            _currentTextureRect = _currentFacade.TextureRect;

            // 2. Calculate building height
            float buildingHeightMeters = 10f;
            if (feature.Properties.ContainsKey("height"))
            {
                buildingHeightMeters = System.Convert.ToSingle(feature.Properties["height"]);
                
                // DEBUG: Log buildings >= 200m to find USX/BNY
                if (buildingHeightMeters >= 200f)
                {
                    UnityEngine.Debug.Log($"[TextureSideWallModifier] TALL BUILDING: height={buildingHeightMeters:F1}m from feature.Properties[\"height\"]");
                }
            }
            else if (feature.Properties.ContainsKey("render_height"))
            {
                buildingHeightMeters = System.Convert.ToSingle(feature.Properties["render_height"]);
            }

            // Custom Logic: Boost small "suburb" buildings (approx 3m) to 9-14m
            // We use the seed to ensure consistent random height for the same building
            if (buildingHeightMeters > 2.0f && buildingHeightMeters < 4.0f)
            {
                // Use the existing random generator initialized with feature ID
                // Range: 9m to 14m
                buildingHeightMeters = 9f + (float)random.NextDouble() * 5f;
            }
            
            // Disable global random height override unless explicitly requested (and even then, maybe we want to keep the logic above?)
            // For now, we'll assume the user wants to DISABLE the old "randomize everything" logic if it was active.
            if (_useRandomHeights)
            {
                 // Optional: Keep this if you want to randomize EVERYTHING else too. 
                 // But user said "disable it", so let's comment it out or make it only apply if it wasn't already modified.
                 // buildingHeightMeters = 10f + (float)random.NextDouble() * 40f;
            }

            // 3. Convert heights to tile space
            var tileSize = Conversions.TileEdgeSizeInMercator(feature.TileId.Z);
            float latComp = mapInfo.GetLatitudeCompensationForLocation;
            float metersToTile = 1.0f / ((float)tileSize * latComp);

            float buildingHeightTile = buildingHeightMeters * metersToTile;
            
            // 4. Calculate floor segments (First, Top, Mid)
            float groundH = _currentFacade.GroundFloorHeightMeters * metersToTile;
            float topH = _currentFacade.TopFloorHeightMeters * metersToTile;
            float midH = _currentFacade.MidFloorHeightMeters * metersToTile;

            // Safety checks
            if (buildingHeightTile < groundH)
            {
                finalFirstHeight = buildingHeightTile;
                finalTopHeight = 0;
                finalMidHeight = 0;
                finalLeftOverRowHeight = 0;
            }
            else
            {
                finalFirstHeight = groundH;
                float remaining = buildingHeightTile - groundH;

                if (remaining < topH)
                {
                    finalTopHeight = remaining;
                    finalMidHeight = 0;
                    finalLeftOverRowHeight = 0;
                }
                else
                {
                    finalTopHeight = topH;
                    remaining -= topH;
                    
                    // Calculate mid floors
                    finalMidHeight = remaining; // For now, use all remaining for mid section
                    finalLeftOverRowHeight = 0; // Simplify for now
                }
            }

            _singleFloorHeight = midH;
            _scaledPreferredWallLength = _currentFacade.PreferredEdgeSectionLength * metersToTile;

            // 5. Move roof vertices up
            int vertexCount = md.Vertices.Count;
            for (int i = 0; i < vertexCount; i++)
            {
                md.Vertices[i] = new Vector3(md.Vertices[i].x, md.Vertices[i].y + buildingHeightTile, md.Vertices[i].z);
            }

            // 6. Duplicate roof triangles (double-sided) - DISABLED for indoor visibility
            /*
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
            */

            // 7. Initialize UVs
            if (md.UV.Count == 0) md.UV.Add(new List<Vector2>());
            while (md.UV.Count < 2) md.UV.Add(new List<Vector2>());

            // Roof UVs
            for (int i = 0; i < vertexCount; i++)
            {
                md.UV[1].Add(Vector2.zero); // No color/data for roof
            }

            // 8. Build Walls
            List<int> wallTriangles = new List<int>();
            int triIndex = md.Vertices.Count;

            if (md.Edges.Count > 0)
            {
                for (int i = 0; i < md.Edges.Count; i++)
                {
                    var v1 = md.Vertices[md.Edges[i]];
                    var v2 = md.Vertices[md.Edges[(i + 1) % md.Edges.Count]];
                    
                    // Use the proper atlas-aware method
                    triIndex = BuildWallForEdge(md, v1, v2, wallTriangles, triIndex);
                }
            }

            if (wallTriangles.Count > 0)
            {
                md.Triangles[0].AddRange(wallTriangles);
            }
        }

        private int BuildWallForEdgeSimple(MeshData md, Vector3 v1, Vector3 v2, List<int> wallTriangles, int triIndex, List<float> floorHeights, int buildingSeed, float tileSize, float latComp)
        {
            var horizontalFirst = new Vector3(v1.x, 0f, v1.z);
            var horizontalSecond = new Vector3(v2.x, 0f, v2.z);
            float wallLength = Vector3.Distance(horizontalFirst, horizontalSecond);
            if (wallLength <= Mathf.Epsilon)
                return triIndex;

            Vector3 direction = (v2 - v1).normalized;
            if (direction == Vector3.zero)
                direction = Vector3.forward;

            Vector3 normal = Vector3.Cross(direction, Vector3.down).normalized;
            if (normal == Vector3.zero)
                normal = Vector3.forward;

            // Build walls from bottom up, one quad per floor
            float currentY = v1.y - ((floorHeights[0] + floorHeights[floorHeights.Count - 1]) / 2f / tileSize / latComp); // Start at ground (roof - totalHeight)
            
            // Calculate total building height in tile-space for ground level
            float totalHeightMeters = 0f;
            foreach (var fh in floorHeights)
                totalHeightMeters += fh;
            float totalHeightTileSpace = (totalHeightMeters / tileSize) / latComp;
            currentY = v1.y - totalHeightTileSpace;

            for (int floorIdx = 0; floorIdx < floorHeights.Count; floorIdx++)
            {
                float floorHeightMeters = floorHeights[floorIdx];
                float floorHeightTileSpace = (floorHeightMeters / tileSize) / latComp;

                // Generate random color for this floor using (floor * building) seed
                int floorSeed = buildingSeed * (floorIdx + 1);
                var colorRandom = new System.Random(floorSeed);
                Color floorColor = new Color(
                    (float)colorRandom.NextDouble(),
                    (float)colorRandom.NextDouble(),
                    (float)colorRandom.NextDouble(),
                    1f
                );

                if (floorIdx < 5) // Log first 5 floors to see color variation
                    UnityEngine.Debug.Log($"[KILOVERSE] Building {buildingSeed} Floor {floorIdx}/{floorHeights.Count}: RGB=({floorColor.r:F3}, {floorColor.g:F3}, {floorColor.b:F3})");

                // Create quad vertices (bottom-left, bottom-right, top-left, top-right)
                md.Vertices.Add(new Vector3(v1.x, currentY, v1.z));
                md.Vertices.Add(new Vector3(v2.x, currentY, v2.z));
                md.Vertices.Add(new Vector3(v1.x, currentY + floorHeightTileSpace, v1.z));
                md.Vertices.Add(new Vector3(v2.x, currentY + floorHeightTileSpace, v2.z));

                // Simple UVs (0-1)
                md.UV[0].Add(new Vector2(0, 0));
                md.UV[0].Add(new Vector2(1, 0));
                md.UV[0].Add(new Vector2(0, 1));
                md.UV[0].Add(new Vector2(1, 1));

                // Store color in UV2 (R in x, G in y, B will be derived in shader)
                Vector2 colorUV = new Vector2(floorColor.r, floorColor.g);
                md.UV[1].Add(colorUV);
                md.UV[1].Add(colorUV);
                md.UV[1].Add(colorUV);
                md.UV[1].Add(colorUV);

                if (floorIdx == 0 && EnableVerboseLogging)
                    UnityEngine.Debug.Log($"[KILOVERSE] UV2 set for floor 0: ({colorUV.x:F3}, {colorUV.y:F3}), UV[1] count={md.UV[1].Count}");

                // Normals and tangents
                for (int i = 0; i < 4; i++)
                {
                    md.Normals.Add(normal);
                    md.Tangents.Add(direction);
                }

                // Triangles (two tris per quad)
                wallTriangles.Add(triIndex);
                wallTriangles.Add(triIndex + 1);
                wallTriangles.Add(triIndex + 2);
                wallTriangles.Add(triIndex + 1);
                wallTriangles.Add(triIndex + 3);
                wallTriangles.Add(triIndex + 2);

                currentY += floorHeightTileSpace;
                triIndex += 4;
            }

            return triIndex;
        }

        private int BuildWallForEdge(MeshData md, Vector3 v1, Vector3 v2, List<int> wallTriangles, int triIndex)
        {
            // Create local wall segment data (thread-safe)
            WallSegmentData wallData = new WallSegmentData();
            wallData.firstVertex = v1;
            wallData.secondVertex = v2;

            var horizontalFirst = new Vector3(v1.x, 0f, v1.z);
            var horizontalSecond = new Vector3(v2.x, 0f, v2.z);
            wallData.length = Vector3.Distance(horizontalFirst, horizontalSecond);
            if (wallData.length <= Mathf.Epsilon)
                return triIndex;

            wallData.direction = (v2 - v1).normalized;
            if (wallData.direction == Vector3.zero)
                wallData.direction = Vector3.forward;

            wallData.normal = Vector3.Cross(wallData.direction, Vector3.down).normalized;
            if (wallData.normal == Vector3.zero)
                wallData.normal = Vector3.forward;

            wallData.columnScaleRatio = wallData.length / Mathf.Max(_scaledPreferredWallLength, 0.0001f);
            float widthRatio = Mathf.Clamp01(wallData.columnScaleRatio);
            wallData.rightOfEdgeUv = Mathf.Lerp(_currentTextureRect.xMin, _currentTextureRect.xMax, widthRatio);
            if (Mathf.Abs(wallData.rightOfEdgeUv - _currentTextureRect.xMin) <= Mathf.Epsilon)
            {
                wallData.rightOfEdgeUv = _currentTextureRect.xMax;
            }

            wallData.currentY1 = v1.y;
            wallData.currentY2 = v2.y;

            triIndex = LeftOverRow(md, wallTriangles, triIndex, ref wallData);
            triIndex = TopFloor(md, wallTriangles, triIndex, ref wallData);

            // Only process mid floors if there's meaningful height (at least 1mm in tile space)
            if (finalMidHeight > 0.001f)
            {
                triIndex = MidFloors(md, wallTriangles, triIndex, ref wallData);
            }

            triIndex = FirstFloor(md, wallData.currentY1, wallTriangles, triIndex, ref wallData);

            return triIndex;
        }
        
private int LeftOverRow(MeshData md, List<int> wallTriangles, int triIndex, ref WallSegmentData wallData)
        {
            if (finalLeftOverRowHeight <= 0) return triIndex;

            float uvTop = wallData.currentY1 / (finalFirstHeight + finalMidHeight + finalTopHeight + finalLeftOverRowHeight);
            float uvBottom = (wallData.currentY1 - finalLeftOverRowHeight) / (finalFirstHeight + finalMidHeight + finalTopHeight + finalLeftOverRowHeight);

            triIndex = AddWallSegment(md, wallTriangles, triIndex,
                wallData.currentY1, wallData.currentY2,
                wallData.currentY1 - finalLeftOverRowHeight, wallData.currentY2 - finalLeftOverRowHeight,
                uvTop, uvBottom, 0, ref wallData);

            wallData.currentY1 -= finalLeftOverRowHeight;
            wallData.currentY2 -= finalLeftOverRowHeight;

            return triIndex;
        }

private int TopFloor(MeshData md, List<int> wallTriangles, int triIndex, ref WallSegmentData wallData)
        {
            if (finalTopHeight <= 0) return triIndex;

            float uvTop = wallData.currentY1 / (finalFirstHeight + finalMidHeight + finalTopHeight + finalLeftOverRowHeight);
            float uvBottom = (wallData.currentY1 - finalTopHeight) / (finalFirstHeight + finalMidHeight + finalTopHeight + finalLeftOverRowHeight);

            triIndex = AddWallSegment(md, wallTriangles, triIndex,
                wallData.currentY1, wallData.currentY2,
                wallData.currentY1 - finalTopHeight, wallData.currentY2 - finalTopHeight,
                uvTop, uvBottom, 3, ref wallData);

            wallData.currentY1 -= finalTopHeight;
            wallData.currentY2 -= finalTopHeight;

            return triIndex;
        }

private int MidFloors(MeshData md, List<int> wallTriangles, int triIndex, ref WallSegmentData wallData)
        {
            float remainingHeight = finalMidHeight;

            while (remainingHeight >= _singleFloorHeight - 0.01f)
            {
                float midUvStep = ((float)Math.Min(_currentFacade.MidFloorCount,
                    Math.Round(remainingHeight / _singleFloorHeight))) / _currentFacade.MidFloorCount;

                float floorHeight = _singleFloorHeight * _currentFacade.MidFloorCount * midUvStep;

                // SAFETY: Prevent infinite loop if floorHeight is too small
                if (floorHeight < 0.001f)
                {
                    UnityEngine.Debug.LogWarning($"[KILOVERSE] MidFloors: floorHeight too small ({floorHeight:F6}), breaking loop");
                    break;
                }

                float uvTop = wallData.currentY1 / (finalFirstHeight + finalMidHeight + finalTopHeight + finalLeftOverRowHeight);
                float uvBottom = (wallData.currentY1 - floorHeight) / (finalFirstHeight + finalMidHeight + finalTopHeight + finalLeftOverRowHeight);

                triIndex = AddWallSegment(md, wallTriangles, triIndex,
                    wallData.currentY1, wallData.currentY2,
                    wallData.currentY1 - floorHeight, wallData.currentY2 - floorHeight,
                    uvTop, uvBottom, 2, ref wallData);

                wallData.currentY1 -= floorHeight;
                wallData.currentY2 -= floorHeight;
                remainingHeight -= floorHeight;
            }

            return triIndex;
        }

private int FirstFloor(MeshData md, float totalHeight, List<int> wallTriangles, int triIndex, ref WallSegmentData wallData)
        {
            if (finalFirstHeight <= 0) return triIndex;

            float uvTop = wallData.currentY1 / (finalFirstHeight + finalMidHeight + finalTopHeight + finalLeftOverRowHeight);
            float uvBottom = 0.0f; // Ground level

            triIndex = AddWallSegment(md, wallTriangles, triIndex,
                wallData.currentY1, wallData.currentY2,
                wallData.currentY1 - finalFirstHeight, wallData.currentY2 - finalFirstHeight,
                uvTop, uvBottom, 1, ref wallData);

            return triIndex;
        }
        private int AddWallSegment(MeshData md, List<int> wallTriangles, int triIndex, float y1Top, float y2Top, float y1Bottom, float y2Bottom, float uvTop, float uvBottom, int floorType, ref WallSegmentData wallData)
        {
            if (EnableVerboseLogging)
                UnityEngine.Debug.Log($"[KILOVERSE] AddWallSegment: yTop={y1Top:F2}, yBottom={y1Bottom:F2}, uvTop={uvTop:F3}, uvBottom={uvBottom:F3}, floorType={floorType}");

            // Validate wall vertices are reasonable (within 1 unit of wall segment endpoints)
            float maxDist = 1.0f;
            float dist1 = Vector3.Distance(new Vector3(wallData.firstVertex.x, 0, wallData.firstVertex.z),
                                           new Vector3(wallData.secondVertex.x, 0, wallData.secondVertex.z));
            if (dist1 > maxDist)
            {
                UnityEngine.Debug.LogWarning($"[KILOVERSE] Wall segment too long: {dist1} units! v1=({wallData.firstVertex.x},{wallData.firstVertex.z}) v2=({wallData.secondVertex.x},{wallData.secondVertex.z})");
            }

            // Vertices
            md.Vertices.Add(new Vector3(wallData.firstVertex.x, y1Top, wallData.firstVertex.z));
            md.Vertices.Add(new Vector3(wallData.secondVertex.x, y2Top, wallData.secondVertex.z));
            md.Vertices.Add(new Vector3(wallData.firstVertex.x, y1Bottom, wallData.firstVertex.z));
            md.Vertices.Add(new Vector3(wallData.secondVertex.x, y2Bottom, wallData.secondVertex.z));

            // UV0: Standard texture coordinates
            if (wallData.length >= _minWallLength)
            {
                md.UV[0].Add(new Vector2(_currentTextureRect.xMin, uvTop));
                md.UV[0].Add(new Vector2(wallData.rightOfEdgeUv, uvTop));
                md.UV[0].Add(new Vector2(_currentTextureRect.xMin, uvBottom));
                md.UV[0].Add(new Vector2(wallData.rightOfEdgeUv, uvBottom));
            }
            else
            {
                md.UV[0].Add(new Vector2(_currentTextureRect.xMin, uvTop));
                md.UV[0].Add(new Vector2(_currentTextureRect.xMin + _narrowWallWidthDelta, uvTop));
                md.UV[0].Add(new Vector2(_currentTextureRect.xMin, uvBottom));
                md.UV[0].Add(new Vector2(_currentTextureRect.xMin + _narrowWallWidthDelta, uvBottom));
            }

            // Ensure UV channels exist
            while (md.UV.Count < 3)
            {
                md.UV.Add(new List<Vector2>());
                // Fill new channels with zeros for existing vertices to avoid count mismatch
                int currentVertCount = md.Vertices.Count - 4; // Exclude the 4 we just added
                for (int i = 0; i < currentVertCount; i++)
                {
                    md.UV[md.UV.Count - 1].Add(Vector2.zero);
                }
            }

            // UV1: Random color data (matching shader expectation: 0-1 range)
            if (_useRandomColors)
            {
                var random = new System.Random(triIndex + floorType * 1000);
                float r = (float)random.NextDouble();
                float g = (float)random.NextDouble();
                
                Vector2 colorData = new Vector2(r, g);
                for (int i = 0; i < 4; i++) md.UV[1].Add(colorData);
            }
            else
            {
                for (int i = 0; i < 4; i++) md.UV[1].Add(Vector2.zero);
            }

            // UV2 (Unity UV3): Floor Type for Debug Shader
            // 1=Bottom, 2=Mid, 3=Top
            Vector2 debugData = new Vector2((float)floorType, 0);
            for (int i = 0; i < 4; i++) md.UV[2].Add(debugData);

            // Normals and Tangents
            for (int i = 0; i < 4; i++)
            {
                md.Normals.Add(wallData.normal);
                md.Tangents.Add(wallData.direction);
            }

            // Triangles
            wallTriangles.Add(triIndex);
            wallTriangles.Add(triIndex + 1);
            wallTriangles.Add(triIndex + 2);
            wallTriangles.Add(triIndex + 1);
            wallTriangles.Add(triIndex + 3);
            wallTriangles.Add(triIndex + 2);

            return triIndex + 4;
        }
    }
}
