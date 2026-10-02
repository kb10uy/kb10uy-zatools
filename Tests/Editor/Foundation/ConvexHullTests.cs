using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KusakaFactory.Zatools.Foundation;

namespace KusakaFactory.Zatools.Tests.Foundation
{
    public sealed class ConvexHullTests
    {
        private const float Tolerance = 1e-4f;

        private static float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);

        private static void AssertConvexHull2D(IReadOnlyList<Vector2> points, IReadOnlyList<int> hull)
        {
            Assert.That(hull, Is.Unique);
            Assert.That(hull.Count, Is.GreaterThanOrEqualTo(3));

            for (var i = 0; i < hull.Count; ++i)
            {
                var a = points[hull[i]];
                var b = points[hull[(i + 1) % hull.Count]];
                var c = points[hull[(i + 2) % hull.Count]];
                Assert.That(Cross(a, b, c), Is.GreaterThan(0.0f), $"hull turns clockwise or straight at {hull[(i + 1) % hull.Count]}");

                for (var p = 0; p < points.Count; ++p)
                {
                    Assert.That(Cross(a, b, points[p]), Is.GreaterThanOrEqualTo(-Tolerance), $"point {p} lies outside edge {hull[i]}-{hull[(i + 1) % hull.Count]}");
                }
            }
        }

        private static void AssertConvexHull3D(IReadOnlyList<Vector3> points, IReadOnlyList<int> triangles)
        {
            Assert.That(triangles.Count % 3, Is.EqualTo(0));
            Assert.That(triangles.Count, Is.GreaterThanOrEqualTo(12));

            var edges = new HashSet<(int, int)>();
            for (var t = 0; t < triangles.Count; t += 3)
            {
                var a = triangles[t];
                var b = triangles[t + 1];
                var c = triangles[t + 2];
                Assert.That(edges.Add((a, b)) && edges.Add((b, c)) && edges.Add((c, a)), Is.True, $"triangle {t / 3} repeats a directed edge");

                var normal = Vector3.Cross(points[b] - points[a], points[c] - points[a]).normalized;
                for (var p = 0; p < points.Count; ++p)
                {
                    Assert.That(Vector3.Dot(normal, points[p] - points[a]), Is.LessThanOrEqualTo(Tolerance), $"point {p} lies outside triangle {t / 3}");
                }
            }

            foreach (var (from, to) in edges)
            {
                Assert.That(edges.Contains((to, from)), Is.True, $"edge {from}-{to} has no opposite edge; the hull is not closed");
            }

            var vertexCount = triangles.Distinct().Count();
            var faceCount = triangles.Count / 3;
            Assert.That(vertexCount - edges.Count / 2 + faceCount, Is.EqualTo(2), "Euler characteristic of the hull is not 2");
        }

        [Test]
        public void Andrews_SquareWithInteriorPoint()
        {
            var points = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0.5f), new Vector2(1, 1), new Vector2(0, 1) };
            var hull = ConvexHull.ComputeAndrews(points);
            Assert.That(hull, Is.EquivalentTo(new[] { 0, 1, 3, 4 }));
            AssertConvexHull2D(points, hull);
        }

        [Test]
        public void Andrews_ExcludesCollinearPoints()
        {
            var points = new[] { new Vector2(0, 0), new Vector2(0.5f, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1), new Vector2(0, 0.5f) };
            var hull = ConvexHull.ComputeAndrews(points);
            Assert.That(hull, Is.EquivalentTo(new[] { 0, 2, 3, 4 }));
            AssertConvexHull2D(points, hull);
        }

        [Test]
        public void Andrews_ToleratesDuplicatePoints()
        {
            var points = new[] { new Vector2(0, 0), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1) };
            var hull = ConvexHull.ComputeAndrews(points);
            Assert.That(hull, Has.Length.EqualTo(3));
            AssertConvexHull2D(points, hull);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void Andrews_ReturnsAllIndicesForFewerThanThreePoints(int count)
        {
            var points = Enumerable.Range(0, count).Select((i) => new Vector2(i, i * 2)).ToArray();
            Assert.That(ConvexHull.ComputeAndrews(points), Is.EqualTo(Enumerable.Range(0, count)));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void Andrews_RandomPointCloud(int seed)
        {
            var random = new System.Random(seed);
            var points = Enumerable.Range(0, 200).Select((_) => new Vector2((float)random.NextDouble() * 10 - 5, (float)random.NextDouble() * 4 - 2)).ToArray();
            AssertConvexHull2D(points, ConvexHull.ComputeAndrews(points));
        }

        [Test]
        public void QuickHull3D_CubeWithInteriorPoints()
        {
            var points = new List<Vector3>();
            for (var i = 0; i < 8; ++i) points.Add(new Vector3(i & 1, (i >> 1) & 1, (i >> 2) & 1));
            points.Add(new Vector3(0.5f, 0.5f, 0.5f));
            points.Add(new Vector3(0.25f, 0.75f, 0.5f));
            points.Add(new Vector3(0.9f, 0.1f, 0.2f));

            var triangles = ConvexHull.ComputeQuickHull3D(points);
            Assert.That(triangles, Has.Length.EqualTo(12 * 3));
            Assert.That(triangles.Distinct(), Is.EquivalentTo(Enumerable.Range(0, 8)));
            AssertConvexHull3D(points, triangles);
        }

        [Test]
        public void QuickHull3D_Tetrahedron()
        {
            var points = new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0), new Vector3(0, 0, 1) };
            var triangles = ConvexHull.ComputeQuickHull3D(points);
            Assert.That(triangles, Has.Length.EqualTo(4 * 3));
            AssertConvexHull3D(points, triangles);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void QuickHull3D_RandomPointCloud(int seed)
        {
            var random = new System.Random(seed);
            var points = Enumerable.Range(0, 300)
                .Select((_) => new Vector3((float)random.NextDouble() * 2 - 1, (float)random.NextDouble() * 3 - 1.5f, (float)random.NextDouble() - 0.5f))
                .ToArray();
            AssertConvexHull3D(points, ConvexHull.ComputeQuickHull3D(points));
        }

        [Test]
        public void QuickHull3D_PointsOnSphere()
        {
            var random = new System.Random(42);
            var points = Enumerable.Range(0, 100)
                .Select((_) => new Vector3((float)random.NextDouble() * 2 - 1, (float)random.NextDouble() * 2 - 1, (float)random.NextDouble() * 2 - 1).normalized)
                .ToArray();
            var triangles = ConvexHull.ComputeQuickHull3D(points);
            Assert.That(triangles.Distinct().Count(), Is.EqualTo(points.Length));
            AssertConvexHull3D(points, triangles);
        }

        [Test]
        public void QuickHull3D_ReturnsEmptyForFewerThanFourPoints()
        {
            var points = new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0) };
            Assert.That(ConvexHull.ComputeQuickHull3D(points), Is.Empty);
        }

        [Test]
        public void QuickHull3D_ReturnsEmptyForCoplanarPoints()
        {
            var points = new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0), new Vector3(1, 1, 0), new Vector3(0.5f, 0.5f, 0) };
            Assert.That(ConvexHull.ComputeQuickHull3D(points), Is.Empty);
        }

        [Test]
        public void QuickHull3D_ReturnsEmptyForCollinearPoints()
        {
            var points = Enumerable.Range(0, 5).Select((i) => new Vector3(i, i * 2, i * 3)).ToArray();
            Assert.That(ConvexHull.ComputeQuickHull3D(points), Is.Empty);
        }
    }
}
