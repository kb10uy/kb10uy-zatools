using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEngine;

namespace KusakaFactory.Zatools.Foundation
{
    internal static class ConvexHull
    {
        private const float Epsilon = 1e-6f;

        /// <summary>
        /// Andrew's Algorithm (Monotone Chain) で凸包を計算し、入力に対する凸包のインデックスリストを返す。
        /// </summary> 
        /// <returns>凸包を形成する点のインデックスリスト。巡回順序。</returns>
        public static ImmutableArray<int> ComputeAndrews(IReadOnlyList<Vector2> points)
        {
            int n = points.Count;
            if (n < 3)
            {
                var result = new List<int>(n);
                for (int i = 0; i < n; i++) result.Add(i);
                return result.ToImmutableArray();
            }

            var sorted = new List<int>(n);
            for (int i = 0; i < n; i++) sorted.Add(i);
            sorted.Sort((a, b) =>
            {
                int cmp = points[a].x.CompareTo(points[b].x);
                return cmp != 0 ? cmp : points[a].y.CompareTo(points[b].y);
            });

            var hull = new List<int>(n + 1);

            for (int i = 0; i < n; i++)
            {
                while (hull.Count >= 2 && Cross(points[hull[hull.Count - 2]], points[hull[hull.Count - 1]], points[sorted[i]]) <= 0f)
                    hull.RemoveAt(hull.Count - 1);
                hull.Add(sorted[i]);
            }

            int lowerCount = hull.Count + 1;
            for (int i = n - 2; i >= 0; i--)
            {
                while (hull.Count >= lowerCount && Cross(points[hull[hull.Count - 2]], points[hull[hull.Count - 1]], points[sorted[i]]) <= 0f)
                    hull.RemoveAt(hull.Count - 1);
                hull.Add(sorted[i]);
            }

            hull.RemoveAt(hull.Count - 1);
            return hull.ToImmutableArray();
        }

        private static float Cross(Vector2 o, Vector2 a, Vector2 b)
        {
            return (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
        }

        /// <summary>
        /// 3D QuickHull で凸包を計算し、三角形インデックス (3つで1面) を返す。
        /// </summary>
        public static ImmutableArray<int> ComputeQuickHull3D(IReadOnlyList<Vector3> points)
        {
            int n = points.Count;
            if (n < 4) return ImmutableArray<int>.Empty;

            if (!TryBuildInitialSimplex(points, out var simplex))
            {
                return ImmutableArray<int>.Empty;
            }

            var interiorPoint = (points[simplex.A] + points[simplex.B] + points[simplex.C] + points[simplex.D]) * 0.25f;
            var faces = new List<Face>(8)
            {
                CreateFace(simplex.A, simplex.B, simplex.C, interiorPoint, points),
                CreateFace(simplex.A, simplex.D, simplex.B, interiorPoint, points),
                CreateFace(simplex.B, simplex.D, simplex.C, interiorPoint, points),
                CreateFace(simplex.C, simplex.D, simplex.A, interiorPoint, points),
            };

            var used = new HashSet<int> { simplex.A, simplex.B, simplex.C, simplex.D };
            for (int i = 0; i < n; i++)
            {
                if (used.Contains(i)) continue;
                AssignPointToFace(i, points, faces);
            }

            while (true)
            {
                if (!TryGetEyePoint(faces, points, out _, out int eyePoint)) break;

                var visible = CollectVisibleFaces(faces, eyePoint, points);

                var orphanPoints = new HashSet<int>();
                foreach (var face in visible)
                {
                    foreach (var p in face.Outside)
                    {
                        if (p != eyePoint) orphanPoints.Add(p);
                    }
                }

                var horizon = BuildHorizon(visible);
                faces.RemoveAll(f => visible.Contains(f));

                var newFaces = new List<Face>(horizon.Count);
                foreach (var edge in horizon)
                {
                    newFaces.Add(CreateFace(edge.From, edge.To, eyePoint, interiorPoint, points));
                }
                faces.AddRange(newFaces);

                foreach (var p in orphanPoints)
                {
                    AssignPointToFace(p, points, newFaces);
                }
            }

            var triangles = new List<int>(faces.Count * 3);
            foreach (var face in faces)
            {
                triangles.Add(face.A);
                triangles.Add(face.B);
                triangles.Add(face.C);
            }
            return triangles.ToImmutableArray();
        }

        private static void AssignPointToFace(int pointIndex, IReadOnlyList<Vector3> points, IReadOnlyList<Face> faces)
        {
            float bestDistance = Epsilon;
            Face bestFace = null;
            foreach (var face in faces)
            {
                float distance = SignedDistance(face, points[pointIndex], points);
                if (distance > bestDistance)
                {
                    bestDistance = distance;
                    bestFace = face;
                }
            }

            bestFace?.Outside.Add(pointIndex);
        }

        private static bool TryGetEyePoint(IReadOnlyList<Face> faces, IReadOnlyList<Vector3> points, out Face face, out int point)
        {
            face = null;
            point = -1;
            float best = Epsilon;

            foreach (var f in faces)
            {
                for (int i = 0; i < f.Outside.Count; i++)
                {
                    int p = f.Outside[i];
                    float distance = SignedDistance(f, points[p], points);
                    if (distance > best)
                    {
                        best = distance;
                        point = p;
                        face = f;
                    }
                }
            }

            return face != null;
        }

        private static HashSet<Face> CollectVisibleFaces(IReadOnlyList<Face> faces, int eyePoint, IReadOnlyList<Vector3> points)
        {
            var visible = new HashSet<Face>();
            foreach (var face in faces)
            {
                if (SignedDistance(face, points[eyePoint], points) > Epsilon)
                {
                    visible.Add(face);
                }
            }

            return visible;
        }

        private static List<Edge> BuildHorizon(HashSet<Face> visible)
        {
            var edgeMap = new Dictionary<(int From, int To), Edge>();

            foreach (var face in visible)
            {
                AddOrRemoveEdge(edgeMap, face.A, face.B);
                AddOrRemoveEdge(edgeMap, face.B, face.C);
                AddOrRemoveEdge(edgeMap, face.C, face.A);
            }

            return edgeMap.Values.ToList();
        }

        private static void AddOrRemoveEdge(Dictionary<(int From, int To), Edge> edges, int from, int to)
        {
            var reverse = (to, from);
            if (edges.ContainsKey(reverse))
            {
                edges.Remove(reverse);
                return;
            }

            edges[(from, to)] = new Edge(from, to);
        }

        private static Face CreateFace(int a, int b, int c, Vector3 interiorPoint, IReadOnlyList<Vector3> points)
        {
            var face = new Face(a, b, c);
            EnsureFaceOrientation(face, interiorPoint, points);
            return face;
        }

        private static void EnsureFaceOrientation(Face face, Vector3 interiorPoint, IReadOnlyList<Vector3> points)
        {
            if (SignedDistance(face, interiorPoint, points) > 0f)
            {
                (face.B, face.C) = (face.C, face.B);
            }
        }

        private static float SignedDistance(Face face, Vector3 point, IReadOnlyList<Vector3> points)
        {
            var a = points[face.A];
            var b = points[face.B];
            var c = points[face.C];
            var normal = Vector3.Cross(b - a, c - a);
            float magnitude = normal.magnitude;
            if (magnitude <= Epsilon) return 0f;
            return Vector3.Dot(normal / magnitude, point - a);
        }

        private static bool TryBuildInitialSimplex(IReadOnlyList<Vector3> points, out InitialSimplex simplex)
        {
            simplex = default;

            int minX = 0;
            int maxX = 0;
            for (int i = 1; i < points.Count; i++)
            {
                if (points[i].x < points[minX].x) minX = i;
                if (points[i].x > points[maxX].x) maxX = i;
            }
            if (minX == maxX) return false;

            int farLine = -1;
            float maxLineDist = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                if (i == minX || i == maxX) continue;
                float d = DistanceFromLine(points[i], points[minX], points[maxX]);
                if (d > maxLineDist)
                {
                    maxLineDist = d;
                    farLine = i;
                }
            }
            if (farLine < 0 || maxLineDist <= Epsilon) return false;

            int farPlane = -1;
            float maxPlaneDist = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                if (i == minX || i == maxX || i == farLine) continue;
                float d = Mathf.Abs(SignedDistanceToPlane(points[i], points[minX], points[maxX], points[farLine]));
                if (d > maxPlaneDist)
                {
                    maxPlaneDist = d;
                    farPlane = i;
                }
            }
            if (farPlane < 0 || maxPlaneDist <= Epsilon) return false;

            simplex = new InitialSimplex(minX, maxX, farLine, farPlane);
            return true;
        }

        private static float DistanceFromLine(Vector3 p, Vector3 a, Vector3 b)
        {
            var ab = b - a;
            float den = ab.magnitude;
            if (den <= Epsilon) return 0f;
            return Vector3.Cross(ab, p - a).magnitude / den;
        }

        private static float SignedDistanceToPlane(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            var normal = Vector3.Cross(b - a, c - a).normalized;
            return Vector3.Dot(normal, p - a);
        }

        private readonly struct InitialSimplex
        {
            public readonly int A;
            public readonly int B;
            public readonly int C;
            public readonly int D;

            public InitialSimplex(int a, int b, int c, int d)
            {
                A = a;
                B = b;
                C = c;
                D = d;
            }
        }

        private readonly struct Edge
        {
            public readonly int From;
            public readonly int To;

            public Edge(int from, int to)
            {
                From = from;
                To = to;
            }
        }

        private sealed class Face
        {
            public int A;
            public int B;
            public int C;
            public List<int> Outside { get; } = new List<int>();

            public Face(int a, int b, int c)
            {
                A = a;
                B = b;
                C = c;
            }
        }
    }
}
