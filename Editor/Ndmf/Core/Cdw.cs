using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using UnityEngine;
using KusakaFactory.Zatools.Foundation;
using KusakaFactory.Zatools.Runtime;
using UnityObject = UnityEngine.Object;

namespace KusakaFactory.Zatools.Ndmf.Core
{
    internal static class Cdw
    {
        /// <summary>
        /// メイン処理
        /// </summary>
        /// <param name="referenceRenderer">Mesh の参照元の SkinnedMeshRenderer</param>
        /// <param name="modifyingMesh">対象の Mesh</param>
        /// <param name="parameters">固定されたパラメーター</param>
        /// <param name="wrapperMaterial">割り当てるマテリアル</param>
        /// <exception cref="ArgumentException">頂点数が一致しない場合</exception>
        internal static void Process(SkinnedMeshRenderer referencingRenderer, Mesh modifyingMesh, FixedParameters parameters, Material wrapperMaterial)
        {
            if (referencingRenderer.sharedMesh.vertexCount != modifyingMesh.vertexCount) throw new ArgumentException("different mesh vertex count");

            var bakedMesh = new Mesh();
            referencingRenderer.BakeMesh(bakedMesh);
            var bakedVertices = bakedMesh.vertices;
            if (bakedVertices == null || bakedVertices.Length < 4) return;
            UnityObject.DestroyImmediate(bakedMesh);

            ImmutableArray<int> hullTriangles = ConvexHull.ComputeQuickHull3D(bakedVertices);
            if (hullTriangles.Length < 12) return;

            var vertices = modifyingMesh.vertices;
            var normals = modifyingMesh.normals;
            var tangents = modifyingMesh.tangents;
            var uvs = modifyingMesh.uv;
            var boneWeights = modifyingMesh.boneWeights;
            var vertexCount = modifyingMesh.vertexCount;
            var validTangents = tangents.Length == vertexCount;
            var hasBoneWeights = boneWeights != null && boneWeights.Length == vertexCount;

            var vertexMap = new Dictionary<int, int>();
            var extendVertices = new List<Vector3>();
            var extendNormals = new List<Vector3>();
            var extendTangents = validTangents ? new List<Vector4>() : null;
            var extendBoneWeights = hasBoneWeights ? new List<BoneWeight>() : null;
            var remappedTriangles = new int[hullTriangles.Length];

            for (int i = 0; i < hullTriangles.Length; i++)
            {
                var originalIndex = hullTriangles[i];
                if (!vertexMap.TryGetValue(originalIndex, out var newIndex))
                {
                    newIndex = vertexCount + extendVertices.Count;
                    vertexMap.Add(originalIndex, newIndex);
                    extendVertices.Add(vertices[originalIndex]);
                    extendNormals.Add(normals[originalIndex]);
                    if (validTangents) extendTangents.Add(tangents[originalIndex]);
                    if (hasBoneWeights) extendBoneWeights.Add(boneWeights[originalIndex]);
                }
                remappedTriangles[i] = newIndex;
            }

            var newVertexCount = vertexCount + extendVertices.Count;
            Array.Resize(ref vertices, newVertexCount);
            Array.Resize(ref normals, newVertexCount);
            Array.Resize(ref uvs, newVertexCount);
            if (validTangents) Array.Resize(ref tangents, newVertexCount);
            if (hasBoneWeights) Array.Resize(ref boneWeights, newVertexCount);
            for (var i = 0; i < extendVertices.Count; ++i)
            {
                vertices[vertexCount + i] = extendVertices[i];
                normals[vertexCount + i] = extendNormals[i];
                uvs[vertexCount + i] = Vector2.zero;
                if (validTangents) tangents[vertexCount + i] = extendTangents[i];
                if (hasBoneWeights) boneWeights[vertexCount + i] = extendBoneWeights[i];
            }

            modifyingMesh.vertices = vertices;
            modifyingMesh.normals = normals;
            modifyingMesh.uv = uvs;
            if (validTangents) modifyingMesh.tangents = tangents;
            if (hasBoneWeights) modifyingMesh.boneWeights = boneWeights;

            var originalSubMeshCount = modifyingMesh.subMeshCount;
            var savedTriangles = new int[originalSubMeshCount][];
            for (int i = 0; i < originalSubMeshCount; i++) savedTriangles[i] = modifyingMesh.GetTriangles(i);
            modifyingMesh.subMeshCount = originalSubMeshCount + 1;
            for (int i = 0; i < originalSubMeshCount; i++) modifyingMesh.SetTriangles(savedTriangles[i], i);
            modifyingMesh.SetTriangles(remappedTriangles, originalSubMeshCount);
            modifyingMesh.RecalculateBounds();

            var originalMaterials = referencingRenderer.sharedMaterials;
            var newMaterials = new Material[originalMaterials.Length + 1];
            originalMaterials.CopyTo(newMaterials, 0);
            newMaterials[originalMaterials.Length] = wrapperMaterial;
            referencingRenderer.sharedMaterials = newMaterials;
        }

        internal struct FixedParameters : IEquatable<FixedParameters>
        {
            internal static FixedParameters FixFromComponent(ConvexDepthWrapper component)
            {
                return new FixedParameters();
            }

            public bool Equals(FixedParameters other)
            {
                return true;
            }

            public override bool Equals(object obj) => obj is FixedParameters && Equals((FixedParameters)obj);

            public override int GetHashCode() => 0.GetHashCode();

            public static bool operator ==(FixedParameters lhs, FixedParameters rhs) => lhs.Equals(rhs);

            public static bool operator !=(FixedParameters lhs, FixedParameters rhs) => !(lhs == rhs);
        }
    }
}
