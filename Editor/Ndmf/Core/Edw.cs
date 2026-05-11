using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEngine;
using KusakaFactory.Zatools.Foundation;
using KusakaFactory.Zatools.Runtime;

namespace KusakaFactory.Zatools.Ndmf.Core
{
    internal static class Edw
    {
        /// <summary>
        /// メイン処理
        /// </summary>
        /// <param name="referenceRenderer">Mesh の参照元の SkinnedMeshRenderer</param>
        /// <param name="modifyingMesh">対象の Mesh</param>
        /// <param name="parameters">固定されたパラメーター</param>
        /// <returns>このマスクによって影響を受ける頂点にウェイトがかかっているボーンのインデックス(順不同)</returns>
        /// <exception cref="ArgumentException">頂点数が一致しない場合</exception>
        internal static void Process(SkinnedMeshRenderer referencingRenderer, Mesh modifyingMesh, FixedParameters parameters, Material wrapperMaterial)
        {
            if (referencingRenderer.sharedMesh.vertexCount != modifyingMesh.vertexCount) throw new ArgumentException("different mesh vertex count");

            var blendShapeIndex = modifyingMesh.GetBlendShapeIndex(parameters.BlinkBlendShapeName);
            if (blendShapeIndex == -1 || parameters.Threshold < 0.0f || parameters.WithdrawalLimit < 0.0f) return;

            var vertices = modifyingMesh.vertices;
            var normals = modifyingMesh.normals;
            var tangents = modifyingMesh.tangents;
            var uvs = modifyingMesh.uv;
            var boneWeights = modifyingMesh.boneWeights;
            var vertexCount = modifyingMesh.vertexCount;
            var deltaVertices = new Vector3[vertexCount];
            var _deltaNormals = new Vector3[vertexCount];
            var _deltaTangents = new Vector3[vertexCount];
            modifyingMesh.GetBlendShapeFrameVertices(
                blendShapeIndex,
                modifyingMesh.GetBlendShapeFrameCount(blendShapeIndex) - 1,
                deltaVertices,
                _deltaNormals,
                _deltaTangents
            );

            var inverseRelativeBasis = (referencingRenderer.transform.worldToLocalMatrix * parameters.Basis.localToWorldMatrix).inverse;
            var verticesInBasis = vertices.Select(v => inverseRelativeBasis.MultiplyPoint(v)).ToImmutableArray();
            var blinkMovingIndices = deltaVertices
                .Select((d, i) => (Delta: inverseRelativeBasis.MultiplyVector(d), Index: i))
                .Where(t => t.Delta.sqrMagnitude > Mathf.Pow(parameters.Threshold, 2.0f))
                .Select(t => t.Index)
                .ToHashSet();
            var blinkMaxZ = blinkMovingIndices.Select((i) => verticesInBasis[i]).Max((v) => v.z);
            var blinkHullFilteredIndices = blinkMovingIndices.Where((i) => blinkMaxZ - verticesInBasis[i].z < parameters.WithdrawalLimit).ToImmutableArray();
            var (leftConvexHull, rightConvexHull) = ComputeConvexHulls(verticesInBasis, blinkHullFilteredIndices);

            var extendVertices = new List<Vector3>(2);
            var extendNormals = new List<Vector3>(2);
            var extendTangents = new List<Vector2>(2);
            var extendUvs = new List<Vector2>(2);
            var extendBoneWeights = new List<BoneWeight>(2);
            var triangles = new List<int>();
            if (leftConvexHull.Length >= 3)
            {
                var centroidIndex = vertices.Length + extendVertices.Count;
                extendVertices.Add(leftConvexHull.Aggregate(Vector3.zero, (s, i) => s + vertices[i]) / leftConvexHull.Length);
                extendNormals.Add(leftConvexHull.Aggregate(Vector3.zero, (s, i) => s + normals[i]).normalized);
                extendTangents.Add(leftConvexHull.Aggregate(Vector4.zero, (s, i) => s + tangents[i]).normalized);
                extendUvs.Add(leftConvexHull.Aggregate(Vector2.zero, (s, i) => s + uvs[i]) / leftConvexHull.Length);
                if (boneWeights != null) extendBoneWeights.Add(boneWeights[leftConvexHull[0]]);
                for (int i = 0; i < leftConvexHull.Length; i++)
                {
                    int next = (i + 1) % leftConvexHull.Length;
                    triangles.AddRange(ImmutableArray.Create(centroidIndex, leftConvexHull[i], leftConvexHull[next]));
                }
            }
            if (rightConvexHull.Length >= 3)
            {
                var centroidIndex = vertices.Length + extendVertices.Count;
                extendVertices.Add(rightConvexHull.Aggregate(Vector3.zero, (s, i) => s + vertices[i]) / rightConvexHull.Length);
                extendNormals.Add(rightConvexHull.Aggregate(Vector3.zero, (s, i) => s + normals[i]).normalized);
                extendTangents.Add(rightConvexHull.Aggregate(Vector4.zero, (s, i) => s + tangents[i]).normalized);
                extendUvs.Add(rightConvexHull.Aggregate(Vector2.zero, (s, i) => s + uvs[i]) / rightConvexHull.Length);
                if (boneWeights != null) extendBoneWeights.Add(boneWeights[rightConvexHull[0]]);
                for (int i = 0; i < rightConvexHull.Length; i++)
                {
                    int next = (i + 1) % rightConvexHull.Length;
                    triangles.AddRange(ImmutableArray.Create(centroidIndex, rightConvexHull[i], rightConvexHull[next]));
                }
            }

            Array.Resize(ref vertices, vertexCount + extendVertices.Count);
            Array.Resize(ref normals, vertexCount + extendVertices.Count);
            Array.Resize(ref tangents, vertexCount + extendVertices.Count);
            Array.Resize(ref uvs, vertexCount + extendVertices.Count);
            if (boneWeights != null) Array.Resize(ref boneWeights, vertexCount + extendVertices.Count);
            for (var i = 0; i < extendVertices.Count; ++i)
            {
                vertices[vertexCount + i] = extendVertices[i];
                normals[vertexCount + i] = extendNormals[i];
                tangents[vertexCount + i] = extendTangents[i];
                uvs[vertexCount + i] = extendUvs[i];
                if (boneWeights != null) boneWeights[vertexCount + i] = extendBoneWeights[i];
            }

            modifyingMesh.vertices = vertices;
            modifyingMesh.normals = normals;
            modifyingMesh.tangents = tangents;
            modifyingMesh.uv = uvs;
            if (boneWeights != null) modifyingMesh.boneWeights = boneWeights;

            var originalSubMeshCount = modifyingMesh.subMeshCount;
            var savedTriangles = new int[originalSubMeshCount][];
            for (int i = 0; i < originalSubMeshCount; i++) savedTriangles[i] = modifyingMesh.GetTriangles(i);
            modifyingMesh.subMeshCount = originalSubMeshCount + 1;
            for (int i = 0; i < originalSubMeshCount; i++) modifyingMesh.SetTriangles(savedTriangles[i], i);
            modifyingMesh.SetTriangles(triangles, originalSubMeshCount);
            modifyingMesh.RecalculateBounds();

            var originalMaterials = referencingRenderer.sharedMaterials;
            var newMaterials = new Material[originalMaterials.Length + 1];
            originalMaterials.CopyTo(newMaterials, 0);
            newMaterials[originalMaterials.Length] = wrapperMaterial;
            referencingRenderer.sharedMaterials = newMaterials;
        }

        private static (ImmutableArray<int> Left, ImmutableArray<int> Right) ComputeConvexHulls(ImmutableArray<Vector3> vertices, ImmutableArray<int> movingIndices)
        {
            var leftMapping = new List<int>(movingIndices.Length / 2);
            var leftPoints = new List<Vector2>(movingIndices.Length / 2);
            var rightMapping = new List<int>(movingIndices.Length / 2);
            var rightPoints = new List<Vector2>(movingIndices.Length / 2);

            foreach (int i in movingIndices)
            {
                if (vertices[i].x < 0f)
                {
                    leftMapping.Add(i);
                    leftPoints.Add(vertices[i]);
                }
                else
                {
                    rightMapping.Add(i);
                    rightPoints.Add(vertices[i]);
                }
            }

            var leftHullIndices = ConvexHull.ComputeAndrews(leftPoints);
            var rightHullIndices = ConvexHull.ComputeAndrews(rightPoints);
            var leftHullVertexIndices = leftHullIndices.Select(li => leftMapping[li]).ToImmutableArray();
            var rightHullVertexIndices = rightHullIndices.Select(ri => rightMapping[ri]).ToImmutableArray();
            return (leftHullVertexIndices, rightHullVertexIndices);
        }

        internal struct FixedParameters : IEquatable<FixedParameters>
        {
            internal string BlinkBlendShapeName;
            internal float Threshold;
            internal float WithdrawalLimit;
            internal Transform Basis;

            internal static FixedParameters FixFromComponent(Transform defaultBasis, EyeholeDepthWrapper component)
            {
                var basisSource = component.Basis != null ? component.Basis : defaultBasis;
                return new FixedParameters()
                {
                    BlinkBlendShapeName = component.BlinkBlendShapeName,
                    Threshold = component.Threshold,
                    WithdrawalLimit = component.WithdrawalLimit,
                    Basis = basisSource,
                };
            }

            public bool Equals(FixedParameters other)
            {
                return Basis.worldToLocalMatrix == other.Basis.worldToLocalMatrix &&
                    BlinkBlendShapeName == other.BlinkBlendShapeName &&
                    Mathf.Approximately(Threshold, other.Threshold) &&
                    Mathf.Approximately(WithdrawalLimit, other.WithdrawalLimit);
            }

            public override bool Equals(object obj) => obj is FixedParameters && Equals((FixedParameters)obj);

            public override int GetHashCode() => (Basis, BlinkBlendShapeName).GetHashCode();

            public static bool operator ==(FixedParameters lhs, FixedParameters rhs) => lhs.Equals(rhs);

            public static bool operator !=(FixedParameters lhs, FixedParameters rhs) => !(lhs == rhs);
        }
    }
}
