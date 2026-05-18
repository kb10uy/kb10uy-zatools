using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using UnityEngine;
using KusakaFactory.Zatools.Foundation;
using KusakaFactory.Zatools.Runtime;

namespace KusakaFactory.Zatools.Ndmf.Core
{
    internal static class Cdw
    {
        /// <summary>
        /// メイン処理
        /// </summary>
        /// <param name="referencingRenderer">Mesh の参照元の SkinnedMeshRenderer</param>
        /// <param name="modifyingMesh">対象の Mesh</param>
        /// <param name="parameters">固定されたパラメーター</param>
        /// <param name="wrapperMaterial">割り当てるマテリアル</param>
        /// <exception cref="ArgumentException">頂点数が一致しない場合</exception>
        internal static void Process(SkinnedMeshRenderer referencingRenderer, Mesh modifyingMesh, FixedParameters parameters, Material wrapperMaterial)
        {
            if (referencingRenderer.sharedMesh.vertexCount != modifyingMesh.vertexCount) throw new ArgumentException("different mesh vertex count");

            var blendShapeAppliedVertices = MeshManipulation.ComputeBlendShapeAppliedVertices(modifyingMesh, referencingRenderer);

            ImmutableArray<int> hullTriangles = ConvexHull.ComputeQuickHull3D(blendShapeAppliedVertices);
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
                    extendVertices.Add(blendShapeAppliedVertices[originalIndex]);
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

        internal static void ProcessSeparate(SkinnedMeshRenderer referencingRenderer, Mesh modifyingMesh, FixedParameters parameters, Material wrapperMaterial)
        {
            if (!parameters.SeparateSmr || parameters.SourceMeshRenderer == null || parameters.SourceMeshRenderer.sharedMesh == null) return;

            var sourceMesh = parameters.SourceMeshRenderer.sharedMesh;
            var blendShapeAppliedVertices = MeshManipulation.ComputeBlendShapeAppliedVertices(sourceMesh, parameters.SourceMeshRenderer);

            ImmutableArray<int> hullTriangles = ConvexHull.ComputeQuickHull3D(blendShapeAppliedVertices);
            if (hullTriangles.Length < 12) return;

            var sourceNormals = sourceMesh.normals;
            var sourceTangents = sourceMesh.tangents;
            var sourceBoneWeights = sourceMesh.boneWeights;
            var sourceVertexCount = sourceMesh.vertexCount;
            var validSourceTangents = sourceTangents.Length == sourceVertexCount;
            var sourceHasBoneWeights = sourceBoneWeights != null && sourceBoneWeights.Length == sourceVertexCount;

            var vertexMap = new Dictionary<int, int>();
            var generatedVertices = new List<Vector3>();
            var generatedNormals = new List<Vector3>();
            var generatedUvs = new List<Vector2>();
            var generatedTangents = validSourceTangents ? new List<Vector4>() : null;
            var generatedBoneWeights = sourceHasBoneWeights ? new List<BoneWeight>() : null;
            var generatedTriangles = new int[hullTriangles.Length];

            for (int i = 0; i < hullTriangles.Length; i++)
            {
                var originalIndex = hullTriangles[i];
                if (!vertexMap.TryGetValue(originalIndex, out var newIndex))
                {
                    newIndex = generatedVertices.Count;
                    vertexMap.Add(originalIndex, newIndex);
                    generatedVertices.Add(blendShapeAppliedVertices[originalIndex]);
                    generatedNormals.Add(sourceNormals[originalIndex]);
                    generatedUvs.Add(Vector2.zero);
                    if (validSourceTangents) generatedTangents.Add(sourceTangents[originalIndex]);
                    if (sourceHasBoneWeights) generatedBoneWeights.Add(sourceBoneWeights[originalIndex]);
                }
                generatedTriangles[i] = newIndex;
            }

            modifyingMesh.SetVertices(generatedVertices);
            modifyingMesh.SetNormals(generatedNormals);
            modifyingMesh.SetUVs(0, generatedUvs);
            if (validSourceTangents) modifyingMesh.SetTangents(generatedTangents);
            if (sourceHasBoneWeights) modifyingMesh.boneWeights = generatedBoneWeights.ToArray();
            modifyingMesh.SetTriangles(generatedTriangles, 0);
            modifyingMesh.bindposes = sourceMesh.bindposes;
            modifyingMesh.RecalculateBounds();

            referencingRenderer.sharedMaterials = new Material[] { wrapperMaterial };
        }

        internal struct FixedParameters : IEquatable<FixedParameters>
        {
            internal bool SeparateSmr;
            internal SkinnedMeshRenderer SourceMeshRenderer;

            internal static FixedParameters FixFromComponent(ConvexDepthWrapper component)
            {
                return new FixedParameters
                {
                    SeparateSmr = component.SourceMeshRenderer != null,
                    SourceMeshRenderer = component.SourceMeshRenderer,
                };
            }

            public bool Equals(FixedParameters other)
            {
                return SeparateSmr == other.SeparateSmr && SourceMeshRenderer == other.SourceMeshRenderer;
            }

            public override bool Equals(object obj) => obj is FixedParameters && Equals((FixedParameters)obj);

            public override int GetHashCode() => (SeparateSmr, SourceMeshRenderer).GetHashCode();

            public static bool operator ==(FixedParameters lhs, FixedParameters rhs) => lhs.Equals(rhs);

            public static bool operator !=(FixedParameters lhs, FixedParameters rhs) => !(lhs == rhs);
        }
    }
}
