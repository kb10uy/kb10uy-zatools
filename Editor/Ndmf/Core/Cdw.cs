using System.Collections.Generic;
using System.Collections.Immutable;
using UnityEngine;
using KusakaFactory.Zatools.Foundation;
using UnityObject = UnityEngine.Object;

namespace KusakaFactory.Zatools.Ndmf.Core
{
    internal static class Cdw
    {
        internal static Mesh GenerateConvexHullMesh(SkinnedMeshRenderer sourceRenderer)
        {
            if (sourceRenderer == null || sourceRenderer.sharedMesh == null) return null;

            var source = sourceRenderer.sharedMesh;
            var baked = new Mesh();
            try
            {
                sourceRenderer.BakeMesh(baked);
                var vertices = baked.vertices;
                if (vertices == null || vertices.Length < 4) return null;

                var sourceBoneWeights = source.boneWeights;
                var hasBoneWeights = sourceBoneWeights != null && sourceBoneWeights.Length == vertices.Length;

                ImmutableArray<int> hullTriangles = ConvexHull.ComputeQuickHull3D(vertices);
                if (hullTriangles.Length < 12) return null;

                var vertexMap = new Dictionary<int, int>();
                var remappedVertices = new List<Vector3>();
                var remappedBoneWeights = hasBoneWeights ? new List<BoneWeight>() : null;
                var remappedTriangles = new int[hullTriangles.Length];

                for (int i = 0; i < hullTriangles.Length; i++)
                {
                    int originalIndex = hullTriangles[i];
                    if (!vertexMap.TryGetValue(originalIndex, out int newIndex))
                    {
                        newIndex = remappedVertices.Count;
                        vertexMap.Add(originalIndex, newIndex);
                        remappedVertices.Add(vertices[originalIndex]);
                        if (hasBoneWeights) remappedBoneWeights.Add(sourceBoneWeights[originalIndex]);
                    }

                    remappedTriangles[i] = newIndex;
                }

                var hullMesh = new Mesh
                {
                    name = $"{source.name}_ConvexHull"
                };
                hullMesh.SetVertices(remappedVertices);
                hullMesh.SetTriangles(remappedTriangles, 0);
                if (hasBoneWeights)
                {
                    hullMesh.boneWeights = remappedBoneWeights.ToArray();
                    hullMesh.bindposes = source.bindposes;
                }
                hullMesh.RecalculateNormals();
                hullMesh.RecalculateBounds();
                return hullMesh;
            }
            finally
            {
                UnityObject.DestroyImmediate(baked);
            }
        }
    }
}
