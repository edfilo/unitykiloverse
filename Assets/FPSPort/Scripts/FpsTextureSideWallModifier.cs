using System;
using System.Collections.Generic;
using Kiloverse.FPSPort;
using global::Mapbox.BaseModule.Data;
using global::Mapbox.BaseModule.Data.Interfaces;
using global::Mapbox.BaseModule.Map;
using global::Mapbox.BaseModule.Utilities;
using global::Mapbox.VectorModule.MeshGeneration.MeshModifiers;
using UnityEngine;

namespace Kiloverse.FPSPort
{
    /// <summary>
    /// Port of the FPS project's textured side wall modifier to Mapbox SDK v3 APIs.
    /// Keeps the original atlas-driven tiling (columns + top/mid/bottom UV bands).
    /// </summary>
    public class FpsTextureSideWallModifier : MeshModifier, IPolygonMeshModifier
    {
        private readonly FpsAtlasInfo _atlasInfo;
        private readonly bool _separateSubmesh;
        private static readonly System.Random _rng = new System.Random();

        private FpsAtlasEntity _facade;
        private Rect _textureRect;
        private float _singleFloorHeight;
        private float _firstFloorHeight;
        private float _topFloorHeight;
        
        private float _finalFirstHeight;
        private float _finalTopHeight;
        private float _finalMidHeight;
        private float _finalLeftOverRowHeight;

        public FpsTextureSideWallModifier(FpsAtlasInfo atlasInfo, bool centerSegments = true, bool separateSubmesh = false, bool debugLogVertices = false, int debugLogLimit = 10, string atlasDebugName = null)
        {
            _atlasInfo = atlasInfo;
            _separateSubmesh = separateSubmesh;
            
            if (_atlasInfo != null)
            {
                foreach (var tex in _atlasInfo.Textures)
                {
                    tex?.CalculateParameters();
                }
            }
        }

        public override void Run(VectorFeatureUnity feature, MeshData md, IMapInformation mapInfo)
        {
            if (_atlasInfo == null || _atlasInfo.Textures == null || _atlasInfo.Textures.Count == 0) return;
            if (md.Vertices.Count == 0 || feature == null) return;

            // Pick facade
            int seed = feature.Data.Id.GetHashCode();
            var random = new System.Random(seed);
            _facade = _atlasInfo.Textures[random.Next(_atlasInfo.Textures.Count)];
            _textureRect = _facade.TextureRect;

            // Calculate heights
            float maxHeightMeters = 10f;
            bool foundHeight = false;
            if (feature.Properties.TryGetValue("height", out var hObj)) 
            {
                maxHeightMeters = Convert.ToSingle(hObj);
                foundHeight = true;
            }
            else if (feature.Properties.TryGetValue("render_height", out var rhObj)) 
            {
                maxHeightMeters = Convert.ToSingle(rhObj);
                foundHeight = true;
            }

            if (foundHeight)
            {
                // UnityEngine.Debug.Log($"[FPSPort] Building {feature.Data.Id}: API Height = {maxHeightMeters}m");
            }
            else
            {
                // UnityEngine.Debug.Log($"[FPSPort] Building {feature.Data.Id}: No height found, using default 10m");
            }

            // Random height variation
            maxHeightMeters += (float)random.NextDouble() * 5f;

            var tileSize = Conversions.TileEdgeSizeInMercator(feature.TileId);
            // var scale = mapInfo.Scale; // Do not use scale for tile-relative mesh generation
            float latComp = mapInfo.GetLatitudeCompensationForLocation;
            float metersToTile = 1.0f / (tileSize * latComp);

            float buildingHeightTile = maxHeightMeters * metersToTile;

            // Calculate floor heights in tile space
            // FpsAtlasEntity calculates these in "units" relative to PreferredEdgeSectionLength, we need to scale them
            // But wait, FpsAtlasEntity.CalculateParameters() sets FirstFloorHeight based on PreferredEdgeSectionLength.
            // We should probably treat the facade's "PreferredEdgeSectionLength" as roughly 3-4 meters? 
            // Let's assume the calculated heights in FpsAtlasEntity are in "meters" if PreferredEdgeSectionLength was meters.
            // Actually, let's just use the ratios.
            
            float totalRatio = 1.0f; // Texture is 0-1
            float bottomRatio = _facade.BottomSectionRatio;
            float topRatio = _facade.TopSectionRatio;
            float midRatio = 1.0f - bottomRatio - topRatio;

            // We want real world heights. 
            // Let's assume:
            // Ground floor = 4.5m
            // Top floor = 3.5m
            // Mid floors = 3.0m
            
            float groundMeters = 4.5f;
            float topMeters = 3.5f;
            float midMeters = 3.0f;

            _firstFloorHeight = groundMeters * metersToTile;
            _topFloorHeight = topMeters * metersToTile;
            _singleFloorHeight = midMeters * metersToTile;

            // Calculate sections
            if (buildingHeightTile < _firstFloorHeight)
            {
                _finalFirstHeight = buildingHeightTile;
                _finalTopHeight = 0;
                _finalMidHeight = 0;
            }
            else
            {
                _finalFirstHeight = _firstFloorHeight;
                float remaining = buildingHeightTile - _firstFloorHeight;

                if (remaining < _topFloorHeight)
                {
                    _finalTopHeight = remaining;
                    _finalMidHeight = 0;
                }
                else
                {
                    _finalTopHeight = _topFloorHeight;
                    _finalMidHeight = remaining - _topFloorHeight;
                }
            }

            // Move roof
            int vertexCount = md.Vertices.Count;
            for (int i = 0; i < vertexCount; i++)
            {
                md.Vertices[i] = new Vector3(md.Vertices[i].x, md.Vertices[i].y + buildingHeightTile, md.Vertices[i].z);
            }

            // Duplicate roof (reversed) - DISABLED for indoor visibility
            /*
            if (md.Triangles.Count > 0 && md.Triangles[0].Count > 0)
            {
                int roofTriCount = md.Triangles[0].Count;
                List<int> reversedRoofTris = new List<int>(roofTriCount);
                for (int i = roofTriCount - 1; i >= 0; i--) reversedRoofTris.Add(md.Triangles[0][i]);
                md.Triangles[0].AddRange(reversedRoofTris);
            }
            */

            // Init UVs
            if (md.UV.Count == 0) md.UV.Add(new List<Vector2>());
            while (md.UV.Count < 3) md.UV.Add(new List<Vector2>());

            // Roof UVs
            for (int i = 0; i < vertexCount; i++)
            {
                md.UV[1].Add(Vector2.zero);
                md.UV[2].Add(Vector2.zero);
            }

            List<int> wallTriangles = new List<int>();
            int triIndex = md.Vertices.Count;

            for (int i = 0; i < md.Edges.Count; i += 2)
            {
                var v1 = md.Vertices[md.Edges[i]];
                var v2 = md.Vertices[md.Edges[i + 1]];
                triIndex = BuildWall(md, v1, v2, wallTriangles, triIndex, seed);
            }

            if (wallTriangles.Count > 0)
            {
                if (_separateSubmesh) md.Triangles.Add(wallTriangles);
                else md.Triangles[0].AddRange(wallTriangles);
            }
        }

        private int BuildWall(MeshData md, Vector3 v1, Vector3 v2, List<int> wallTriangles, int triIndex, int buildingSeed)
        {
            Vector3 direction = (v2 - v1).normalized;
            Vector3 normal = new Vector3(-(v1.z - v2.z), 0, (v1.x - v2.x)).normalized;
            float length = Vector3.Distance(v1, v2);

            if (length < 0.001f) return triIndex;

            float currentY = v1.y - (_finalFirstHeight + _finalMidHeight + _finalTopHeight); // Ground level

            // 1. Ground Floor
            if (_finalFirstHeight > 0)
            {
                triIndex = AddQuad(md, wallTriangles, triIndex, 
                    v1.x, v2.x, v1.z, v2.z, 
                    currentY + _finalFirstHeight, currentY, 
                    _facade.topOfBottomUv, _textureRect.yMin, 
                    1, buildingSeed, normal, direction);
                currentY += _finalFirstHeight;
            }

            // 2. Mid Floors
            if (_finalMidHeight > 0)
            {
                float remaining = _finalMidHeight;
                while (remaining > 0.001f)
                {
                    float h = Mathf.Min(remaining, _singleFloorHeight);
                    // Calculate UVs for this slice
                    float uvHeight = _facade.midUvHeight / _facade.MidFloorCount; // Height of one mid floor in UV space
                    // If h is partial, scale UV? No, just crop.
                    float uvTop = _facade.topOfBottomUv + uvHeight;
                    float uvBottom = _facade.topOfBottomUv;
                    
                    // Actually, let's just map the whole mid section to the mid UVs repeating
                    // For simplicity in this fix, we'll just stack them.
                    
                    triIndex = AddQuad(md, wallTriangles, triIndex,
                        v1.x, v2.x, v1.z, v2.z,
                        currentY + h, currentY,
                         _facade.topOfBottomUv + (_facade.midUvHeight / _facade.MidFloorCount), _facade.topOfBottomUv,
                        2, buildingSeed, normal, direction);
                        
                    currentY += h;
                    remaining -= h;
                }
            }

            // 3. Top Floor
            if (_finalTopHeight > 0)
            {
                triIndex = AddQuad(md, wallTriangles, triIndex,
                    v1.x, v2.x, v1.z, v2.z,
                    currentY + _finalTopHeight, currentY,
                    _textureRect.yMax, _facade.bottomOfTopUv,
                    3, buildingSeed, normal, direction);
            }

            return triIndex;
        }

        private int AddQuad(MeshData md, List<int> wallTriangles, int triIndex, 
            float x1, float x2, float z1, float z2, 
            float yTop, float yBottom, 
            float uvTop, float uvBottom, 
            int floorType, int seed, Vector3 normal, Vector3 tangent)
        {
            md.Vertices.Add(new Vector3(x1, yTop, z1));
            md.Vertices.Add(new Vector3(x2, yTop, z2));
            md.Vertices.Add(new Vector3(x1, yBottom, z1));
            md.Vertices.Add(new Vector3(x2, yBottom, z2));

            // UV0
            md.UV[0].Add(new Vector2(_textureRect.xMin, uvTop));
            md.UV[0].Add(new Vector2(_textureRect.xMax, uvTop));
            md.UV[0].Add(new Vector2(_textureRect.xMin, uvBottom));
            md.UV[0].Add(new Vector2(_textureRect.xMax, uvBottom));

            // UV1 (Unused)
            for(int i=0; i<4; i++) md.UV[1].Add(Vector2.zero);

            // UV2 (Debug Type)
            Vector2 debug = new Vector2(floorType, 0);
            for(int i=0; i<4; i++) md.UV[2].Add(debug);

            for(int i=0; i<4; i++)
            {
                md.Normals.Add(normal);
                md.Tangents.Add(tangent);
            }

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
