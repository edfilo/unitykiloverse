using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kiloverse.Mapbox
{
    // ── Constants & Extensions ──────────────────────────────────────

    public static class MapConstants
    {
        public static readonly Vector3 Vector3Up = new Vector3(0, 1, 0);
        public static readonly Vector3 Vector3Forward = new Vector3(0, 0, 1);
        public static readonly Vector3 Vector3Unused = new Vector3(float.MinValue, float.MinValue, float.MinValue);
    }

    public static class Vector3Extensions
    {
        public static Vector3 Perpendicular(this Vector3 v) => new Vector3(-v.z, v.y, v.x);
    }

    // ── JoinType Enum ──────────────────────────────────────────────

    public enum JoinType { Miter, Bevel, Round, Fakeround, Flipbevel, Butt, Square }

    // ── LineMeshParameters ─────────────────────────────────────────

    [Serializable]
    public class LineMeshParameters
    {
        public float Width = 5f;
        public JoinType JoinType = JoinType.Round;
        public JoinType CapType = JoinType.Round;
        public float MiterLimit = 0.2f;
        public float RoundLimit = 1.05f;
    }

    // ── Polygon Mesh Modifier ──────────────────────────────────────

    [Serializable]
    public class KiloversePolygonMeshModifier : MeshModifier
    {
        private float _height;

        public KiloversePolygonMeshModifier(float height = 0f) { _height = height; }

        public override void Run(VectorFeatureUnity feature, MeshData md, IMapInformation mapInfo)
        {
            var pushUp = new Vector3(0, _height, 0);
            var counter = feature.Points.Count;
            var subset = new List<List<Vector3>>(counter);
            var currentIndex = 0;
            List<int> triList = null;

            for (int i = 0; i < counter; i++)
            {
                var sub = feature.Points[i];
                var vertCount = md.Vertices.Count;

                if (IsClockwise(sub) && vertCount > 0)
                {
                    var flatData = EarcutLibrary.Flatten(subset);
                    var result = EarcutLibrary.Earcut(flatData.Vertices, flatData.Holes, flatData.Dim);
                    var polygonVertexCount = result.Count;
                    if (triList == null) triList = new List<int>(polygonVertexCount);
                    else triList.Capacity = triList.Count + polygonVertexCount;
                    for (int j = 0; j < polygonVertexCount; j++) triList.Add(result[j] + currentIndex);
                    currentIndex = vertCount;
                    subset.Clear();
                }

                subset.Add(sub);
                var subCount = sub.Count;
                md.Vertices.Capacity = md.Vertices.Count + subCount;
                md.Normals.Capacity = md.Normals.Count + subCount;
                md.Edges.Capacity = md.Edges.Count + subCount * 2;

                for (int j = 0; j < subCount; j++)
                {
                    md.Edges.Add(vertCount + ((j + 1) % subCount));
                    md.Edges.Add(vertCount + j);
                    md.Vertices.Add(sub[j] + pushUp);
                    md.Tangents.Add(MapConstants.Vector3Forward);
                    md.Normals.Add(MapConstants.Vector3Up);
                    md.UV[0].Add(new Vector2(sub[j].x, sub[j].z));
                }
            }

            var finalFlatData = EarcutLibrary.Flatten(subset);
            var finalResult = EarcutLibrary.Earcut(finalFlatData.Vertices, finalFlatData.Holes, finalFlatData.Dim);
            var finalCount = finalResult.Count;
            if (triList == null) triList = new List<int>(finalCount);
            else triList.Capacity = triList.Count + finalCount;
            for (int i = 0; i < finalCount; i++) triList.Add(finalResult[i] + currentIndex);
            md.Triangles.Add(triList);
        }

        private bool IsClockwise(IList<Vector3> vertices)
        {
            double sum = 0.0;
            var count = vertices.Count;
            for (int i = 0; i < count; i++)
            {
                var v1 = vertices[i];
                var v2 = vertices[(i + 1) % count];
                sum += (v2.x - v1.x) * (v2.z + v1.z);
            }
            return sum > 0.0;
        }
    }

    // ── Line Mesh Modifier ─────────────────────────────────────────

    [Serializable]
    public class KiloverseLineMeshModifier : MeshModifier
    {
        private LineMeshParameters _params;

        public KiloverseLineMeshModifier(LineMeshParameters parameters = null)
        {
            _params = parameters ?? new LineMeshParameters();
        }

        public override void Run(VectorFeatureUnity feature, MeshData md, IMapInformation mapInfo)
        {
            var tileScale = Conversions.TileEdgeSizeInMercator(feature.TileId) / mapInfo.GetLatitudeCompensationForLocation;
            var scaledWidth = _params.Width / (float)tileScale;

            if (feature.Points.Count < 1) return;

            var allPoints = new List<List<Vector3>>();
            foreach (var segment in feature.Points)
            {
                var filtered = new List<Vector3>();
                var tolerance = 0.001f;
                for (int i = 0; i < segment.Count - 1; i++)
                {
                    var p1 = segment[i]; var p2 = segment[i + 1];
                    bool outside = (p1.x < 0 && p2.x < 0) || (p1.x > 1 && p2.x > 1) ||
                                   (p1.z < -1 && p2.z < -1) || (p1.z > 0 && p2.z > 0);
                    if (!outside && !IsOnEdge(p1, p2, tolerance))
                    {
                        filtered.Add(p1);
                        if (i == segment.Count - 2) filtered.Add(p2);
                    }
                    else { filtered.Add(p1); allPoints.Add(filtered); filtered = new List<Vector3>(); }
                }
                allPoints.Add(filtered);
            }

            foreach (var seg in allPoints)
            {
                if (seg.Count < 2) continue;
                ExtrudeSegment(seg, md, scaledWidth);
            }
        }

        private void ExtrudeSegment(List<Vector3> roadSegment, MeshData md, float scaledWidth)
        {
            var vertexList = new List<Vector3>();
            var normalList = new List<Vector3>();
            var triangleList = new List<int>();
            var uvList = new List<Vector2>();
            var tangentList = new List<Vector4>();
            int index1 = -1, index2 = -1, index3 = -1;
            bool startOfLine = true;
            float cornerOffsetA = 0, cornerOffsetB = 0;
            var prevVertex = MapConstants.Vector3Unused;
            var currentVertex = MapConstants.Vector3Unused;
            var prevNormal = MapConstants.Vector3Unused;
            var nextNormal = MapConstants.Vector3Unused;
            float distance = 0f;
            float cosHalfSharpCorner = Mathf.Cos(75f / 2f * (Mathf.PI / 180f));
            float sharpCornerOffset = 15f;

            var count = roadSegment.Count;
            for (int i = 0; i < count; i++)
            {
                var nextVertex = i != (count - 1) ? roadSegment[i + 1] : MapConstants.Vector3Unused;
                if (nextNormal != MapConstants.Vector3Unused) prevNormal = nextNormal;
                if (currentVertex != MapConstants.Vector3Unused) prevVertex = currentVertex;
                currentVertex = roadSegment[i];
                nextNormal = (nextVertex != MapConstants.Vector3Unused) ? (nextVertex - currentVertex).normalized.Perpendicular() : prevNormal;
                if (prevNormal == MapConstants.Vector3Unused) prevNormal = nextNormal;

                var joinNormal = (prevNormal + nextNormal).normalized;
                var cosHalfAngle = joinNormal.x * nextNormal.x + joinNormal.z * nextNormal.z;
                var miterLength = cosHalfAngle != 0 ? 1 / cosHalfAngle : float.PositiveInfinity;
                var isSharpCorner = cosHalfAngle < cosHalfSharpCorner && prevVertex != MapConstants.Vector3Unused && nextVertex != MapConstants.Vector3Unused;

                if (isSharpCorner && i > 0)
                {
                    var prevSegLen = Vector3.Distance(currentVertex, prevVertex);
                    if (prevSegLen > 2 * sharpCornerOffset)
                    {
                        var dir = currentVertex - prevVertex;
                        var newPrevVertex = currentVertex - dir * (sharpCornerOffset / prevSegLen);
                        distance += Vector3.Distance(newPrevVertex, prevVertex);
                        AddCurrentVertex(newPrevVertex, distance, prevNormal, scaledWidth, vertexList, normalList, triangleList, uvList, tangentList, ref index1, ref index2, md);
                        prevVertex = newPrevVertex;
                    }
                }

                var middleVertex = prevVertex != MapConstants.Vector3Unused && nextVertex != MapConstants.Vector3Unused;
                var currentJoin = middleVertex ? _params.JoinType : _params.CapType;

                if (middleVertex && currentJoin == JoinType.Round)
                {
                    if (miterLength < _params.RoundLimit) currentJoin = JoinType.Miter;
                    else if (miterLength <= 2) currentJoin = JoinType.Fakeround;
                }
                if (currentJoin == JoinType.Miter && miterLength > _params.MiterLimit) currentJoin = JoinType.Bevel;
                if (currentJoin == JoinType.Bevel)
                {
                    if (miterLength > 2) currentJoin = JoinType.Flipbevel;
                    if (miterLength < _params.MiterLimit) currentJoin = JoinType.Miter;
                }

                if (prevVertex != MapConstants.Vector3Unused) distance += Vector3.Distance(currentVertex, prevVertex);

                if (currentJoin == JoinType.Miter)
                {
                    joinNormal *= miterLength;
                    AddCurrentVertex(currentVertex, distance, joinNormal, scaledWidth, vertexList, normalList, triangleList, uvList, tangentList, ref index1, ref index2, md);
                }
                else if (currentJoin == JoinType.Flipbevel)
                {
                    if (miterLength > 100) joinNormal = nextNormal * -1;
                    else
                    {
                        var direction = (prevNormal.x * nextNormal.z - prevNormal.z * nextNormal.x) > 0 ? -1 : 1;
                        var bevelLength = miterLength * (prevNormal + nextNormal).magnitude / (prevNormal - nextNormal).magnitude;
                        joinNormal = joinNormal.Perpendicular() * (bevelLength * direction);
                    }
                    AddCurrentVertex(currentVertex, distance, joinNormal, scaledWidth, vertexList, normalList, triangleList, uvList, tangentList, ref index1, ref index2, md, 0, 0);
                    AddCurrentVertex(currentVertex, distance, joinNormal * -1, scaledWidth, vertexList, normalList, triangleList, uvList, tangentList, ref index1, ref index2, md, 0, 0);
                }
                else if (currentJoin == JoinType.Bevel || currentJoin == JoinType.Fakeround)
                {
                    var lineTurnsLeft = (prevNormal.x * nextNormal.z - prevNormal.z * nextNormal.x) > 0;
                    var offset = (float)-Math.Sqrt(miterLength * miterLength - 1);
                    cornerOffsetA = lineTurnsLeft ? offset : 0;
                    cornerOffsetB = lineTurnsLeft ? 0 : offset;

                    if (!startOfLine)
                        AddCurrentVertex(currentVertex, distance, prevNormal, scaledWidth, vertexList, normalList, triangleList, uvList, tangentList, ref index1, ref index2, md, cornerOffsetA, cornerOffsetB);

                    if (currentJoin == JoinType.Fakeround)
                    {
                        var n = Mathf.Floor((0.5f - (cosHalfAngle - 0.5f)) * 8);
                        for (var m = 0f; m < n; m++)
                        {
                            var approx = (nextNormal * ((m + 1f) / (n + 1f)) + prevNormal).normalized;
                            AddPieSliceVertex(currentVertex, distance, approx, lineTurnsLeft, scaledWidth, vertexList, normalList, triangleList, uvList, tangentList, ref index1, ref index2, ref index3, md);
                        }
                        var jn = joinNormal;
                        index3 = -1;
                        AddPieSliceVertex(currentVertex, distance, jn, lineTurnsLeft, scaledWidth, vertexList, normalList, triangleList, uvList, tangentList, ref index1, ref index2, ref index3, md);
                        for (var k = n - 1; k >= -1; k--)
                        {
                            var approx = (prevNormal * ((k + 1) / (n + 1)) + nextNormal).normalized;
                            AddPieSliceVertex(currentVertex, distance, approx, lineTurnsLeft, scaledWidth, vertexList, normalList, triangleList, uvList, tangentList, ref index1, ref index2, ref index3, md);
                        }
                        index1 = -1; index2 = -1;
                    }

                    if (nextVertex != MapConstants.Vector3Unused)
                        AddCurrentVertex(currentVertex, distance, nextNormal, scaledWidth, vertexList, normalList, triangleList, uvList, tangentList, ref index1, ref index2, md, -cornerOffsetA, -cornerOffsetB);
                }
                else if (currentJoin == JoinType.Butt)
                {
                    if (!startOfLine)
                        AddCurrentVertex(currentVertex, distance, prevNormal, scaledWidth, vertexList, normalList, triangleList, uvList, tangentList, ref index1, ref index2, md, 0, 0);
                    if (nextVertex != MapConstants.Vector3Unused)
                        AddCurrentVertex(currentVertex, distance, nextNormal, scaledWidth, vertexList, normalList, triangleList, uvList, tangentList, ref index1, ref index2, md, 0, 0);
                }
                else if (currentJoin == JoinType.Square)
                {
                    if (!startOfLine) { AddCurrentVertex(currentVertex, distance, prevNormal, scaledWidth, vertexList, normalList, triangleList, uvList, tangentList, ref index1, ref index2, md, 1, 1); index1 = index2 = -1; }
                    if (nextVertex != MapConstants.Vector3Unused) AddCurrentVertex(currentVertex, distance, nextNormal, scaledWidth, vertexList, normalList, triangleList, uvList, tangentList, ref index1, ref index2, md, -1, -1);
                }
                else if (currentJoin == JoinType.Round)
                {
                    if (startOfLine)
                    {
                        AddCurrentVertex(currentVertex, distance, prevNormal * 0.33f, scaledWidth, vertexList, normalList, triangleList, uvList, tangentList, ref index1, ref index2, md, -2f, -2f);
                        AddCurrentVertex(currentVertex, distance, prevNormal * 0.66f, scaledWidth, vertexList, normalList, triangleList, uvList, tangentList, ref index1, ref index2, md, -.7f, -.7f);
                        AddCurrentVertex(currentVertex, distance, prevNormal, scaledWidth, vertexList, normalList, triangleList, uvList, tangentList, ref index1, ref index2, md, 0, 0);
                    }
                    else if (nextVertex == MapConstants.Vector3Unused)
                    {
                        AddCurrentVertex(currentVertex, distance, prevNormal, scaledWidth, vertexList, normalList, triangleList, uvList, tangentList, ref index1, ref index2, md, 0, 0);
                        AddCurrentVertex(currentVertex, distance, prevNormal * 0.66f, scaledWidth, vertexList, normalList, triangleList, uvList, tangentList, ref index1, ref index2, md, .7f, .7f);
                        AddCurrentVertex(currentVertex, distance, prevNormal * 0.33f, scaledWidth, vertexList, normalList, triangleList, uvList, tangentList, ref index1, ref index2, md, 2f, 2f);
                        index1 = -1; index2 = -1;
                    }
                    else
                    {
                        AddCurrentVertex(currentVertex, distance, prevNormal, scaledWidth, vertexList, normalList, triangleList, uvList, tangentList, ref index1, ref index2, md, 0, 0);
                        AddCurrentVertex(currentVertex, distance, nextNormal, scaledWidth, vertexList, normalList, triangleList, uvList, tangentList, ref index1, ref index2, md, 0, 0);
                    }
                }

                if (isSharpCorner && i < count - 1)
                {
                    var nextSegLen = Vector3.Distance(currentVertex, nextVertex);
                    if (nextSegLen > 2 * sharpCornerOffset)
                    {
                        var newCurrent = currentVertex + (nextVertex - currentVertex) * (sharpCornerOffset / nextSegLen);
                        distance += Vector3.Distance(newCurrent, currentVertex);
                        AddCurrentVertex(newCurrent, distance, nextNormal, scaledWidth, vertexList, normalList, triangleList, uvList, tangentList, ref index1, ref index2, md);
                        currentVertex = newCurrent;
                    }
                }
                startOfLine = false;
            }

            md.Edges.Add(md.Vertices.Count);
            md.Edges.Add(md.Vertices.Count + 1);
            md.Edges.Add(md.Vertices.Count + vertexList.Count - 1);
            md.Edges.Add(md.Vertices.Count + vertexList.Count - 2);
            md.Vertices.AddRange(vertexList);
            md.Normals.AddRange(normalList);
            if (md.Triangles.Count == 0) md.Triangles.Add(new List<int>());
            md.Triangles[0].AddRange(triangleList);
            md.Tangents.AddRange(tangentList);
            md.UV[0].AddRange(uvList);
        }

        private static void AddCurrentVertex(Vector3 pos, float dist, Vector3 normal, float width,
            List<Vector3> verts, List<Vector3> norms, List<int> tris, List<Vector2> uvs, List<Vector4> tangs,
            ref int idx1, ref int idx2, MeshData md, float endLeft = 0, float endRight = 0)
        {
            var triStart = md.Vertices.Count;
            var extrude = normal;
            if (endLeft != 0) extrude -= normal.Perpendicular() * endLeft;
            var vert = pos + extrude * width;
            if (float.IsNaN(vert.x) || float.IsNaN(vert.z) || float.IsInfinity(vert.x) || float.IsInfinity(vert.z)) vert = pos;

            verts.Add(vert); norms.Add(MapConstants.Vector3Up); uvs.Add(new Vector2(1, dist)); tangs.Add((Vector4)extrude);
            var idx3 = triStart + verts.Count - 1;
            if (idx1 >= triStart && idx2 >= triStart)
            {
                tris.Add(idx1); tris.Add(idx3); tris.Add(idx2);
                md.Edges.Add(triStart + verts.Count - 1); md.Edges.Add(triStart + verts.Count - 3);
            }
            idx1 = idx2; idx2 = idx3;

            extrude = normal * -1;
            if (endRight != 0) extrude -= normal.Perpendicular() * endRight;
            vert = pos + extrude * width;
            if (float.IsNaN(vert.x) || float.IsNaN(vert.z) || float.IsInfinity(vert.x) || float.IsInfinity(vert.z)) vert = pos;

            verts.Add(vert); norms.Add(MapConstants.Vector3Up); uvs.Add(new Vector2(0, dist)); tangs.Add((Vector4)extrude);
            idx3 = triStart + verts.Count - 1;
            if (idx1 >= triStart && idx2 >= triStart)
            {
                tris.Add(idx1); tris.Add(idx2); tris.Add(idx3);
                md.Edges.Add(triStart + verts.Count - 3); md.Edges.Add(triStart + verts.Count - 1);
            }
            idx1 = idx2; idx2 = idx3;
        }

        private static void AddPieSliceVertex(Vector3 pos, float dist, Vector3 normal, bool lineTurnsLeft, float width,
            List<Vector3> verts, List<Vector3> norms, List<int> tris, List<Vector2> uvs, List<Vector4> tangs,
            ref int idx1, ref int idx2, ref int idx3, MeshData md)
        {
            var triStart = md.Vertices.Count;
            var extrude = normal * (lineTurnsLeft ? -1 : 1);
            verts.Add(pos + extrude * width); norms.Add(MapConstants.Vector3Up); uvs.Add(new Vector2(1, dist));
            tangs.Add((Vector4)(lineTurnsLeft ? normal * -1 : normal));
            idx3 = triStart + verts.Count - 1;
            if (idx1 >= 0 && idx2 >= 0)
            {
                tris.Add(idx1); tris.Add(idx3); tris.Add(idx2);
                if (!lineTurnsLeft) { md.Edges.Add(idx3); md.Edges.Add(idx1); }
                else { md.Edges.Add(idx2); md.Edges.Add(idx3); }
            }
            if (lineTurnsLeft) idx2 = idx3; else idx1 = idx3;
        }

        private static bool IsOnEdge(Vector3 a, Vector3 b, float tol)
        {
            bool Close(float v, float t, float tolerance) => Math.Abs(v - t) <= tolerance;
            if (Close(a.x, 0, tol) && Close(b.x, 0, tol)) return true;
            if (Close(a.x, 1, tol) && Close(b.x, 1, tol)) return true;
            if (Close(a.z, -1, tol) && Close(b.z, -1, tol)) return true;
            if (Close(a.z, 0, tol) && Close(b.z, 0, tol)) return true;
            return false;
        }
    }

    // ── Snap Terrain Modifier ──────────────────────────────────────

    [Serializable]
    public class KiloverseSnapTerrainModifier : MeshModifier
    {
        public override void Run(VectorFeatureUnity feature, MeshData md, IMapInformation mapInfo)
        {
            if (mapInfo.QueryElevation == null) return;

            // Compute scale factor: scale * tile-edge-in-Unity-space
            var tileEdge = Conversions.TileEdgeSizeInMercator(feature.TileId);
            float scaleFactor = (float)(mapInfo.Scale * tileEdge * mapInfo.Scale);
            if (scaleFactor == 0) scaleFactor = 1f;

            var vertCount = md.Vertices.Count;
            if (vertCount > 0)
            {
                for (int i = 0; i < vertCount; i++)
                {
                    float x = md.Vertices[i].x;
                    float z = md.Vertices[i].z;
                    float xQuery = Mathf.Clamp01(x);
                    float zQuery = z < 0 ? Mathf.Clamp01(-z) : Mathf.Clamp01(z);

                    var h = (float)(mapInfo.QueryElevation(feature.TileId, xQuery, zQuery) / scaleFactor);
                    if (float.IsNaN(h) || float.IsInfinity(h)) h = 0;
                    md.Vertices[i] += new Vector3(0, h, 0);
                }
            }
            else
            {
                foreach (var sub in feature.Points)
                {
                    var subCount = sub.Count;
                    for (int i = 0; i < subCount; i++)
                    {
                        float x = sub[i].x; float z = sub[i].z;
                        float xQuery = Mathf.Clamp01(x);
                        float zQuery = z < 0 ? Mathf.Clamp01(-z) : Mathf.Clamp01(z);
                        var h = (float)(mapInfo.QueryElevation(feature.TileId, xQuery, zQuery) / scaleFactor);
                        if (float.IsNaN(h) || float.IsInfinity(h)) h = 0;
                        sub[i] += new Vector3(0, h, 0);
                    }
                }
            }
        }
    }

    // ── Material Modifier ──────────────────────────────────────────

    [Serializable]
    public class KiloverseMaterialModifier : GameObjectModifier
    {
        private Material[] _materials;

        public KiloverseMaterialModifier(params Material[] materials) { _materials = materials; }

        public override void Run(VectorEntity ve, IMapInformation mapInformation)
        {
            if (ve.MeshRenderer != null)
                ve.MeshRenderer.materials = _materials;
            else
            {
                ve.MeshRenderer = ve.GameObject.AddComponent<MeshRenderer>();
                if (ve.MeshRenderer != null) ve.MeshRenderer.materials = _materials;
            }
        }
    }
}
