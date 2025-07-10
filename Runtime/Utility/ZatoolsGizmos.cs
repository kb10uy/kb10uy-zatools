using System;
using UnityEngine;

namespace KusakaFactory.Zatools.Runtime.Utility
{
    internal static class ZatoolsGizmos
    {
        public static void WithContext(Matrix4x4 matrix, Color color, Action draw)
        {
            var currentMatrix = Gizmos.matrix;
            var currentColor = Gizmos.color;

            Gizmos.matrix = matrix;
            Gizmos.color = color;
            draw();

            Gizmos.color = currentColor;
            Gizmos.matrix = currentMatrix;
        }

        public static void DrawPseudoCapsule(float radius, float length)
        {
            var midHalfLength = length / 2.0f - radius;
            Gizmos.DrawWireSphere(Vector3.up * midHalfLength, radius);
            Gizmos.DrawWireSphere(Vector3.down * midHalfLength, radius);
            Gizmos.DrawLineList(new[] {
                new Vector3(-radius, -midHalfLength, 0.0f),
                new Vector3(-radius, midHalfLength, 0.0f),
                new Vector3(radius, -midHalfLength, 0.0f),
                new Vector3(radius, midHalfLength, 0.0f),
                new Vector3(0.0f, -midHalfLength, -radius),
                new Vector3(0.0f, midHalfLength, -radius),
                new Vector3(0.0f, -midHalfLength, radius),
                new Vector3(0.0f, midHalfLength, radius),
            });
        }
    }
}
