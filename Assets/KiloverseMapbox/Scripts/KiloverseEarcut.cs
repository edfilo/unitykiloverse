using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kiloverse.Mapbox
{
    public static class EarcutLibrary
    {
        public static List<int> Earcut(List<float> data, List<int> holeIndices, int dim)
        {
            dim = Math.Max(dim, 2);
            var hasHoles = holeIndices.Count;
            var outerLen = hasHoles > 0 ? holeIndices[0] * dim : data.Count;
            var outerNode = LinkedList(data, 0, outerLen, dim, true);
            var triangles = new List<int>((int)(outerNode.i * 1.5));
            if (outerNode == null) return triangles;
            var minX = 0f; var minY = 0f; var maxX = 0f; var maxY = 0f;
            var size = 0f;
            if (hasHoles > 0) outerNode = EliminateHoles(data, holeIndices, outerNode, dim);
            if (data.Count > 80 * dim)
            {
                minX = maxX = data[0]; minY = maxY = data[1];
                for (var i = dim; i < outerLen; i += dim)
                {
                    var x = data[i]; var y = data[i + 1];
                    if (x < minX) minX = x; if (y < minY) minY = y;
                    if (x > maxX) maxX = x; if (y > maxY) maxY = y;
                }
                size = Math.Max(maxX - minX, maxY - minY);
            }
            EarcutLinked(outerNode, triangles, dim, minX, minY, size);
            return triangles;
        }

        private static void EarcutLinked(EarcutNode ear, List<int> triangles, int dim, float minX, float minY, float size, int pass = 0)
        {
            if (ear == null) return;
            if (pass == 0 && size > 0) IndexCurve(ear, minX, minY, size);
            var stop = ear;
            while (ear.prev != ear.next)
            {
                var prev = ear.prev; var next = ear.next;
                if (size > 0 ? IsEarHashed(ear, minX, minY, size) : IsEar(ear))
                {
                    triangles.Add(prev.i / dim); triangles.Add(next.i / dim); triangles.Add(ear.i / dim);
                    RemoveNode(ear); ear = next.next; stop = next.next; continue;
                }
                ear = next;
                if (ear == stop)
                {
                    if (pass == 0) EarcutLinked(FilterPoints(ear, null), triangles, dim, minX, minY, size, 1);
                    else if (pass == 1) { ear = CureLocalIntersections(ear, triangles, dim); EarcutLinked(ear, triangles, dim, minX, minY, size, 2); }
                    else if (pass == 2) SplitEarcut(ear, triangles, dim, minX, minY, size);
                    break;
                }
            }
        }

        private static bool IsEarHashed(EarcutNode ear, float minX, float minY, float size)
        {
            var a = ear.prev; var b = ear; var c = ear.next;
            if (Area(a, b, c) >= 0) return false;
            var minTX = a.x < b.x ? (a.x < c.x ? a.x : c.x) : (b.x < c.x ? b.x : c.x);
            var minTY = a.y < b.y ? (a.y < c.y ? a.y : c.y) : (b.y < c.y ? b.y : c.y);
            var maxTX = a.x > b.x ? (a.x > c.x ? a.x : c.x) : (b.x > c.x ? b.x : c.x);
            var maxTY = a.y > b.y ? (a.y > c.y ? a.y : c.y) : (b.y > c.y ? b.y : c.y);
            var minZ = ZOrder(minTX, minTY, minX, minY, size);
            var maxZ = ZOrder(maxTX, maxTY, minX, minY, size);
            var p = ear.nextZ;
            while (p != null && p.mZOrder <= maxZ)
            {
                if (p != ear.prev && p != ear.next && PointInTriangle(a.x, a.y, b.x, b.y, c.x, c.y, p.x, p.y) && Area(p.prev, p, p.next) >= 0) return false;
                p = p.nextZ;
            }
            p = ear.prevZ;
            while (p != null && p.mZOrder >= minZ)
            {
                if (p != ear.prev && p != ear.next && PointInTriangle(a.x, a.y, b.x, b.y, c.x, c.y, p.x, p.y) && Area(p.prev, p, p.next) >= 0) return false;
                p = p.prevZ;
            }
            return true;
        }

        private static int ZOrder(float x, float y, float minX, float minY, float size)
        {
            x = 32767 * (x - minX) / size; y = 32767 * (y - minY) / size;
            x = ((int)x | ((int)x << 8)) & 0x00FF00FF; x = ((int)x | ((int)x << 4)) & 0x0F0F0F0F;
            x = ((int)x | ((int)x << 2)) & 0x33333333; x = ((int)x | ((int)x << 1)) & 0x55555555;
            y = ((int)y | ((int)y << 8)) & 0x00FF00FF; y = ((int)y | ((int)y << 4)) & 0x0F0F0F0F;
            y = ((int)y | ((int)y << 2)) & 0x33333333; y = ((int)y | ((int)y << 1)) & 0x55555555;
            return (int)x | ((int)y << 1);
        }

        private static void SplitEarcut(EarcutNode start, List<int> triangles, int dim, float minX, float minY, float size)
        {
            var a = start;
            do { var b = a.next.next;
                while (b != a.prev) { if (a.i != b.i && IsValidDiagonal(a, b)) {
                    var c = SplitPolygon(a, b); a = FilterPoints(a, a.next); c = FilterPoints(c, c.next);
                    EarcutLinked(a, triangles, dim, minX, minY, size); EarcutLinked(c, triangles, dim, minX, minY, size); return;
                } b = b.next; } a = a.next;
            } while (a != start);
        }

        private static bool IsValidDiagonal(EarcutNode a, EarcutNode b)
        { return a.next.i != b.i && a.prev.i != b.i && !IntersectsPolygon(a, b) && LocallyInside(a, b) && LocallyInside(b, a) && MiddleInside(a, b); }

        private static bool MiddleInside(EarcutNode a, EarcutNode b)
        {
            var p = a; var inside = false; var px = (a.x + b.x) / 2; var py = (a.y + b.y) / 2;
            do { if (((p.y > py) != (p.next.y > py)) && p.next.y != p.y && (px < (p.next.x - p.x) * (py - p.y) / (p.next.y - p.y) + p.x)) inside = !inside; p = p.next; } while (p != a);
            return inside;
        }

        private static bool IntersectsPolygon(EarcutNode a, EarcutNode b)
        { var p = a; do { if (p.i != a.i && p.next.i != a.i && p.i != b.i && p.next.i != b.i && Intersects(p, p.next, a, b)) return true; p = p.next; } while (p != a); return false; }

        private static EarcutNode CureLocalIntersections(EarcutNode start, List<int> triangles, int dim)
        {
            var p = start;
            do { var a = p.prev; var b = p.next.next;
                if (!Equals(a, b) && Intersects(a, p, p.next, b) && LocallyInside(a, b) && LocallyInside(b, a))
                { triangles.Add(a.i / dim); triangles.Add(p.i / dim); triangles.Add(b.i / dim); RemoveNode(p); RemoveNode(p.next); p = start = b; }
                p = p.next;
            } while (p != start);
            return p;
        }

        private static bool Intersects(EarcutNode p1, EarcutNode q1, EarcutNode p2, EarcutNode q2)
        {
            if ((Equals(p1, q1) && Equals(p2, q2)) || (Equals(p1, q2) && Equals(p2, q1))) return true;
            return Area(p1, q1, p2) > 0 != Area(p1, q1, q2) > 0 && Area(p2, q2, p1) > 0 != Area(p2, q2, q1) > 0;
        }

        private static bool IsEar(EarcutNode ear)
        {
            var a = ear.prev; var b = ear; var c = ear.next;
            if (Area(a, b, c) >= 0) return false;
            var p = ear.next.next;
            while (p != ear.prev) { if (PointInTriangle(a.x, a.y, b.x, b.y, c.x, c.y, p.x, p.y) && Area(p.prev, p, p.next) >= 0) return false; p = p.next; }
            return true;
        }

        private static void IndexCurve(EarcutNode start, float minX, float minY, float size)
        {
            var p = start;
            do { if (p.mZOrder == 0) p.mZOrder = ZOrder(p.x, p.y, minX, minY, size); p.prevZ = p.prev; p.nextZ = p.next; p = p.next; } while (p != start);
            p.prevZ.nextZ = null; p.prevZ = null; SortLinked(p);
        }

        private static EarcutNode SortLinked(EarcutNode list)
        {
            int inSize = 1; int numMerges;
            do {
                var p = list; list = null; EarcutNode tail = null; numMerges = 0;
                while (p != null) {
                    numMerges++; var q = p; var pSize = 0;
                    for (int i = 0; i < inSize; i++) { pSize++; q = q.nextZ; if (q == null) break; }
                    var qSize = inSize;
                    while (pSize > 0 || (qSize > 0 && q != null)) {
                        EarcutNode e;
                        if (pSize != 0 && (qSize == 0 || q == null || p.mZOrder <= q.mZOrder)) { e = p; p = p.nextZ; pSize--; }
                        else { e = q; q = q.nextZ; qSize--; }
                        if (tail != null) tail.nextZ = e; else list = e;
                        e.prevZ = tail; tail = e;
                    }
                    p = q;
                }
                if (tail != null) tail.nextZ = null;
                inSize *= 2;
            } while (numMerges > 1);
            return list;
        }

        private static EarcutNode EliminateHoles(List<float> data, List<int> holeIndices, EarcutNode outerNode, int dim)
        {
            var len = holeIndices.Count;
            var queue = new List<EarcutNode>(len);
            for (int i = 0; i < len; i++)
            {
                var start = holeIndices[i] * dim;
                var end = i < len - 1 ? holeIndices[i + 1] * dim : data.Count;
                var list = LinkedList(data, start, end, dim, false);
                if (list == list.next) list.steiner = true;
                queue.Add(GetLeftmost(list));
            }
            queue.Sort((a, b) => (int)Math.Ceiling(a.x - b.x));
            for (int i = 0; i < queue.Count; i++)
            {
                EliminateHole(queue[i], outerNode);
                outerNode = FilterPoints(outerNode, outerNode.next);
            }
            return outerNode;
        }

        private static void EliminateHole(EarcutNode hole, EarcutNode outerNode)
        {
            outerNode = FindHoleBridge(hole, outerNode);
            if (outerNode != null) { var b = SplitPolygon(outerNode, hole); FilterPoints(b, b.next); }
        }

        private static EarcutNode FilterPoints(EarcutNode start, EarcutNode end)
        {
            if (start == null) return start;
            if (end == null) end = start;
            var p = start; bool again = true;
            do { again = false;
                if (!p.steiner && (Equals(p, p.next) || Area(p.prev, p, p.next) == 0)) { RemoveNode(p); p = end = p.prev; if (p == p.next) return null; again = true; }
                else p = p.next;
            } while (again || p != end);
            return end;
        }

        private static EarcutNode SplitPolygon(EarcutNode a, EarcutNode b)
        {
            var a2 = new EarcutNode(a.i, a.x, a.y); var b2 = new EarcutNode(b.i, b.x, b.y);
            var an = a.next; var bp = b.prev;
            a.next = b; b.prev = a; a2.next = an; an.prev = a2; b2.next = a2; a2.prev = b2; bp.next = b2; b2.prev = bp;
            return b2;
        }

        private static EarcutNode FindHoleBridge(EarcutNode hole, EarcutNode outerNode)
        {
            var p = outerNode; var hx = hole.x; var hy = hole.y; var qx = float.MinValue; EarcutNode m = null;
            do {
                if (hy <= p.y && hy >= p.next.y && p.next.y != p.y) {
                    var x = p.x + (hy - p.y) * (p.next.x - p.x) / (p.next.y - p.y);
                    if (x <= hx && x > qx) { qx = x; if (x == hx) { if (hy == p.y) return p; if (hy == p.next.y) return p.next; } m = p.x < p.next.x ? p : p.next; }
                }
                p = p.next;
            } while (p != outerNode);
            if (m == null) return null;
            if (hx == qx) return m.prev;
            var stop = m; var mx = m.x; var my = m.y; var tanMin = float.MaxValue;
            p = m.next;
            while (p != stop) {
                if (hx >= p.x && p.x >= mx && hx != p.x && PointInTriangle(hy < my ? hx : qx, hy, mx, my, hy < my ? qx : hx, hy, p.x, p.y)) {
                    var tan = Math.Abs(hy - p.y) / (hx - p.x);
                    if ((tan < tanMin || (tan == tanMin && p.x > m.x)) && LocallyInside(p, hole)) { m = p; tanMin = tan; }
                }
                p = p.next;
            }
            return m;
        }

        private static bool LocallyInside(EarcutNode a, EarcutNode b)
        { return Area(a.prev, a, a.next) < 0 ? Area(a, b, a.next) >= 0 && Area(a, a.prev, b) >= 0 : Area(a, b, a.prev) < 0 || Area(a, a.next, b) < 0; }

        private static float Area(EarcutNode p, EarcutNode q, EarcutNode r)
        { return (q.y - p.y) * (r.x - q.x) - (q.x - p.x) * (r.y - q.y); }

        private static bool PointInTriangle(float ax, float ay, float bx, float by, float cx, float cy, float px, float py)
        { return (cx - px) * (ay - py) - (ax - px) * (cy - py) >= 0 && (ax - px) * (by - py) - (bx - px) * (ay - py) >= 0 && (bx - px) * (cy - py) - (cx - px) * (by - py) >= 0; }

        private static EarcutNode GetLeftmost(EarcutNode start)
        { var p = start; var leftmost = start; do { if (p.x < leftmost.x) leftmost = p; p = p.next; } while (p != start); return leftmost; }

        private static bool Equals(EarcutNode p1, EarcutNode p2) => p1.x == p2.x && p1.y == p2.y;

        private static void RemoveNode(EarcutNode p)
        { p.next.prev = p.prev; p.prev.next = p.next; if (p.prevZ != null) p.prevZ.nextZ = p.nextZ; if (p.nextZ != null) p.nextZ.prevZ = p.prevZ; }

        private static float SignedArea(List<float> data, int start, int end, int dim)
        { var sum = 0f; var j = end - dim; for (var i = start; i < end; i += dim) { sum += (data[j] - data[i]) * (data[i + 1] + data[j + 1]); j = i; } return sum; }

        private static EarcutNode LinkedList(List<float> data, int start, int end, int dim, bool clockwise)
        {
            EarcutNode last = null;
            if (clockwise == (SignedArea(data, start, end, dim) > 0))
                for (int i = start; i < end; i += dim) last = InsertNode(i, data[i], data[i + 1], last);
            else
                for (int i = end - dim; i >= start; i -= dim) last = InsertNode(i, data[i], data[i + 1], last);
            if (last != null && Equals(last, last.next)) { RemoveNode(last); last = last.next; }
            return last;
        }

        private static EarcutNode InsertNode(int i, float x, float y, EarcutNode last)
        {
            var p = new EarcutNode(i, x, y);
            if (last == null) { p.prev = p; p.next = p; }
            else { p.next = last.next; p.prev = last; last.next.prev = p; last.next = p; }
            return p;
        }

        public static EarcutData Flatten(List<List<Vector3>> data)
        {
            var dataCount = data.Count;
            var totalVertCount = 0;
            for (int i = 0; i < dataCount; i++) totalVertCount += data[i].Count;
            var result = new EarcutData() { Dim = 2 };
            result.Vertices = new List<float>(totalVertCount * 2);
            var holeIndex = 0;
            for (var i = 0; i < dataCount; i++)
            {
                var subCount = data[i].Count;
                for (var j = 0; j < subCount; j++) { result.Vertices.Add(data[i][j][0]); result.Vertices.Add(data[i][j][2]); }
                if (i > 0) { holeIndex += data[i - 1].Count; result.Holes.Add(holeIndex); }
            }
            return result;
        }
    }

    public class EarcutData
    {
        public List<float> Vertices;
        public List<int> Holes;
        public int Dim;
        public EarcutData() { Holes = new List<int>(); Dim = 2; }
    }

    public class EarcutNode
    {
        public int i;
        public float x, y;
        public int mZOrder;
        public EarcutNode prev, next, prevZ, nextZ;
        public bool steiner;
        public EarcutNode(int ind, float pX, float pY) { i = ind; x = pX; y = pY; }
    }
}

