using System.Collections.Generic;
using System.Collections.Immutable;
using UnityEngine;

namespace KusakaFactory.Zatools.Foundation
{
    internal static class ConvexHull
    {
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
    }
}
