using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using KusakaFactory.Zatools.Runtime;
using KusakaFactory.Zatools.Foundation;
using UnityObject = UnityEngine.Object;

namespace KusakaFactory.Zatools.Ndmf.Core
{
    internal static class Ahbss
    {
        /// <summary>
        /// メイン処理
        /// </summary>
        /// <param name="referenceRenderer">Mesh の参照元の SkinnedMeshRenderer</param>
        /// <param name="modifyingMesh">対象の Mesh</param>
        /// <param name="parameters">固定されたパラメーター</param>
        /// <returns>このマスクによって影響を受ける頂点にウェイトがかかっているボーンのインデックス(順不同)</returns>
        /// <exception cref="ArgumentException">頂点数が一致しない場合</exception>
        internal static HashSet<int> AddSplitShapes(SkinnedMeshRenderer referenceRenderer, Mesh modifyingMesh, FixedParameters parameters)
        {
            if (referenceRenderer.sharedMesh.vertexCount != modifyingMesh.vertexCount) throw new ArgumentException("different mesh vertex count");

            var addLeft = !string.IsNullOrWhiteSpace(parameters.LeftSuffix);
            var addRight = !string.IsNullOrWhiteSpace(parameters.RightSuffix);
            if (parameters.TargetShapes.IsEmpty || (!addLeft && !addRight)) return new HashSet<int>();

            // 現在の変形状態を固定して左右判定をする
            // BlendShape = 0 状態を取ったほうがいい気もするがまあ速そうだし……
            var smrRelativeDeformedMesh = new Mesh();
            var smrRelativeVertices = new List<Vector3>(smrRelativeDeformedMesh.vertexCount);
            referenceRenderer.BakeMesh(smrRelativeDeformedMesh);
            smrRelativeDeformedMesh.GetVertices(smrRelativeVertices);
            UnityObject.DestroyImmediate(smrRelativeDeformedMesh);

            var vertexCount = modifyingMesh.vertexCount;
            var originalVertices = new Vector3[vertexCount];
            var originalNormals = new Vector3[vertexCount];
            var originalTangents = new Vector3[vertexCount];
            var originalWeight = 0.0f;
            var leftVertices = new Vector3[vertexCount];
            var leftNormals = new Vector3[vertexCount];
            var leftTangents = new Vector3[vertexCount];
            var rightVertices = new Vector3[vertexCount];
            var rightNormals = new Vector3[vertexCount];
            var rightTangents = new Vector3[vertexCount];

            // 影響ボーン抽出用
            var influentBones = new HashSet<int>();
            var boneWeights = new List<BoneWeight>(modifyingMesh.vertexCount);
            modifyingMesh.GetBoneWeights(boneWeights);
            var nativeDeltaVertices = new NativeArray<bool>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var nativeBoneWeights = new NativeArray<InlinedBoneWeight>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var nativeInfluentBones = new NativeArray<int4>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            for (var i = 0; i < vertexCount; ++i)
            {
                nativeDeltaVertices[i] = false;
                nativeBoneWeights[i] = InlinedBoneWeight.FromBoneWeight(boneWeights[i]);
                nativeInfluentBones[i] = -1;
            }

            // BakeMesh は SMR の座標系で生成するので Basis は SMR からの相対とする
            var inverseRelativeBasis = (referenceRenderer.transform.worldToLocalMatrix * parameters.Basis.localToWorldMatrix).inverse;
            var nameToIndex = Enumerable.Range(0, modifyingMesh.blendShapeCount).ToDictionary((i) => modifyingMesh.GetBlendShapeName(i));
            var sw = new System.Diagnostics.Stopwatch();
            foreach (var targetShape in parameters.TargetShapes)
            {
                if (!nameToIndex.TryGetValue(targetShape, out var shapeIndex)) continue;
                if (modifyingMesh.GetBlendShapeFrameCount(shapeIndex) != 1) continue;

                modifyingMesh.GetBlendShapeFrameVertices(shapeIndex, 0, originalVertices, originalNormals, originalTangents);
                originalWeight = modifyingMesh.GetBlendShapeFrameWeight(shapeIndex, 0);

                // ウェイトの有無の union
                for (var i = 0; i < vertexCount; ++i) nativeDeltaVertices[i] |= originalVertices[i] != Vector3.zero;

                // 分割
                for (var i = 0; i < vertexCount; ++i)
                {
                    // left-handed, forwarding
                    var deformedVertexInBasis = inverseRelativeBasis.MultiplyPoint(smrRelativeVertices[i]);
                    var isRightSide = deformedVertexInBasis.x >= 0;
                    leftVertices[i] = !isRightSide ? originalVertices[i] : Vector3.zero;
                    leftNormals[i] = !isRightSide ? originalNormals[i] : Vector3.zero;
                    leftTangents[i] = !isRightSide ? originalTangents[i] : Vector3.zero;
                    rightVertices[i] = isRightSide ? originalVertices[i] : Vector3.zero;
                    rightNormals[i] = isRightSide ? originalNormals[i] : Vector3.zero;
                    rightTangents[i] = isRightSide ? originalTangents[i] : Vector3.zero;
                }

                // シェイプキー追加
                // Distinct() しているので この処理中に追加された BlendShape 同士で被ることはない
                var leftName = $"{targetShape}{parameters.LeftSuffix}";
                var rightName = $"{targetShape}{parameters.RightSuffix}";
                if (addLeft && !nameToIndex.ContainsKey(leftName)) modifyingMesh.AddBlendShapeFrame(leftName, originalWeight, leftVertices, leftNormals, leftTangents);
                if (addRight && !nameToIndex.ContainsKey(rightName)) modifyingMesh.AddBlendShapeFrame(rightName, originalWeight, rightVertices, rightNormals, rightTangents);
            }

            // 影響ボーン抽出
            var job = new SelectInfluentBoneJob
            {
                InfluentBones = nativeInfluentBones,
                DeltaVertices = nativeDeltaVertices,
                BoneWeights = nativeBoneWeights,
                CommonThreshold = 0.01f,
            };
            var jobHandle = job.Schedule(vertexCount, 4);
            jobHandle.Complete();

            foreach (var ib in nativeInfluentBones)
            {
                // Add が重いので弾く
                if (ib.x != -1) influentBones.Add(ib.x);
                if (ib.y != -1) influentBones.Add(ib.y);
                if (ib.z != -1) influentBones.Add(ib.z);
                if (ib.w != -1) influentBones.Add(ib.w);
            }

            return influentBones;
        }

        [BurstCompile]
        internal struct SelectInfluentBoneJob : IJobParallelFor
        {
            internal NativeArray<int4> InfluentBones;
            [ReadOnly] internal NativeArray<bool> DeltaVertices;
            [ReadOnly] internal NativeArray<InlinedBoneWeight> BoneWeights;
            [ReadOnly] internal float CommonThreshold;

            public void Execute(int index)
            {
                if (!DeltaVertices[index]) return;

                // 影響を受けるボーンを抽出
                var boneWeight = BoneWeights[index];
                var influence = boneWeight.Weights >= CommonThreshold;
                InfluentBones[index] = math.select((int4)(-1), boneWeight.Indices, influence);
            }
        }

        internal struct FixedParameters : IEquatable<FixedParameters>
        {
            internal Transform Basis;
            internal ImmutableArray<string> TargetShapes;
            internal string LeftSuffix;
            internal string RightSuffix;

            internal static FixedParameters FixFromComponent(Transform defaultBasis, AdHocBlendShapeSplit component)
            {
                var basisSource = component.Basis != null ? component.Basis : defaultBasis;
                return new FixedParameters()
                {
                    Basis = basisSource,
                    TargetShapes = component.TargetShapes.Distinct().ToImmutableArray(),
                    LeftSuffix = component.LeftSuffix,
                    RightSuffix = component.RightSuffix,
                };
            }

            public bool Equals(FixedParameters other)
            {
                return Basis.worldToLocalMatrix == other.Basis.worldToLocalMatrix &&
                    TargetShapes.SequenceEqual(other.TargetShapes) &&
                    LeftSuffix == other.LeftSuffix &&
                    RightSuffix == other.RightSuffix;
            }

            public override bool Equals(object obj) => obj is FixedParameters && Equals((FixedParameters)obj);

            public override int GetHashCode() => (Basis, TargetShapes).GetHashCode();

            public static bool operator ==(FixedParameters lhs, FixedParameters rhs) => lhs.Equals(rhs);

            public static bool operator !=(FixedParameters lhs, FixedParameters rhs) => !(lhs == rhs);
        }
    }
}
