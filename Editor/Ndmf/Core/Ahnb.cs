using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using KusakaFactory.Zatools.Foundation;
using KusakaFactory.Zatools.Runtime;

namespace KusakaFactory.Zatools.Ndmf.Core
{
    internal static class Ahnb
    {
        /// <summary>
        /// メイン処理
        /// </summary>
        /// <param name="referencingRenderer">Mesh の参照元の SkinnedMeshRenderer</param>
        /// <param name="modifyingMesh">対象の Mesh</param>
        /// <param name="parameters">固定されたパラメーター</param>
        /// <returns>このマスクによって影響を受ける頂点にウェイトがかかっているボーンのインデックス(順不同)</returns>
        /// <exception cref="InvalidOperationException"></exception>
        internal static HashSet<int> Process(SkinnedMeshRenderer referencingRenderer, Mesh modifyingMesh, FixedParameters parameters)
        {
            // TODO: BoneWeight1 を使う
            var normals = new List<Vector3>(modifyingMesh.vertexCount);
            var uvs = new List<Vector2>(modifyingMesh.vertexCount);
            var boneWeights = new List<BoneWeight>(modifyingMesh.vertexCount);
            var bindposes = new List<Matrix4x4>(modifyingMesh.bindposeCount);
            modifyingMesh.GetNormals(normals);
            modifyingMesh.GetUVs(0, uvs);
            modifyingMesh.GetBoneWeights(boneWeights);
            modifyingMesh.GetBindposes(bindposes);
            var bones = referencingRenderer.bones;

            var mask = new TextureMask(parameters.MaskTexture, parameters.MaskMode switch
            {
                NormalBendMaskMode.White => TextureMask.Mode.TakeWhite,
                NormalBendMaskMode.Black => TextureMask.Mode.TakeBlack,
                _ => throw new InvalidOperationException("unknown mode"),
            });

            // 移し替え
            var nativeNormals = new NativeArray<float3>(normals.Count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var nativeMaskValues = new NativeArray<float>(normals.Count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var nativeBoneWeights = new NativeArray<InlinedBoneWeight>(normals.Count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var nativeInfluentBones = new NativeArray<int4>(normals.Count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var nativeBoneDeforms = new NativeArray<float4x4>(bones.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            for (var i = 0; i < modifyingMesh.vertexCount; ++i)
            {
                nativeNormals[i] = new float3(normals[i].x, normals[i].y, normals[i].z);
                nativeMaskValues[i] = mask.Take(uvs[i]);
                nativeBoneWeights[i] = InlinedBoneWeight.FromBoneWeight(boneWeights[i]);
                nativeInfluentBones[i] = -1;
            }
            for (var i = 0; i < bones.Length; ++i)
            {
                var bd = bones[i] != null ? bones[i].localToWorldMatrix * bindposes[i] : Matrix4x4.identity;
                nativeBoneDeforms[i] = bd;
            }

            // 実行
            var job = new BendNormalJob
            {
                Normals = nativeNormals,
                InfluentBones = nativeInfluentBones,
                MaskValues = nativeMaskValues,
                BoneWeights = nativeBoneWeights,
                BoneDeforms = nativeBoneDeforms,
                WorldSpaceForward = new float3(parameters.WorldSpaceForward.x, parameters.WorldSpaceForward.y, parameters.WorldSpaceForward.z),
                GlobalWeight = parameters.Weight,
                CommonThreshold = 0.01f,
            };
            var jobHandle = job.Schedule(nativeNormals.Length, 4);
            jobHandle.Complete();

            // 書き戻し
            modifyingMesh.SetNormals(nativeNormals);
            modifyingMesh.RecalculateTangents();

            var influentBones = new HashSet<int>();
            foreach (var ib in nativeInfluentBones)
            {
                // Add が重いので弾く
                if (ib.x != -1) influentBones.Add(ib.x);
                if (ib.y != -1) influentBones.Add(ib.y);
                if (ib.z != -1) influentBones.Add(ib.z);
                if (ib.w != -1) influentBones.Add(ib.w);
            }

            // 破棄
            nativeNormals.Dispose();
            nativeMaskValues.Dispose();
            nativeBoneWeights.Dispose();
            nativeBoneDeforms.Dispose();
            nativeInfluentBones.Dispose();

            return influentBones;
        }

        [BurstCompile]
        internal struct BendNormalJob : IJobParallelFor
        {
            internal NativeArray<float3> Normals;
            internal NativeArray<int4> InfluentBones;
            [ReadOnly] internal NativeArray<float> MaskValues;
            [ReadOnly] internal NativeArray<InlinedBoneWeight> BoneWeights;
            [ReadOnly] internal NativeArray<float4x4> BoneDeforms;
            [ReadOnly] internal float3 WorldSpaceForward;
            [ReadOnly] internal float GlobalWeight;
            [ReadOnly] internal float CommonThreshold;

            public void Execute(int index)
            {
                if (MaskValues[index] < CommonThreshold) return;

                var up = new float3(0.0f, 1.0f, 0.0f);
                var targetForward = new float4(WorldSpaceForward, 0.0f);

                var boneWeight = BoneWeights[index];
                var originalNormal = quaternion.LookRotation(Normals[index], up);

                // ボーン変形を受ける行列を計算
                // BlendShape は線形にしか移動しないのでこの場合は無視してよいものとする
                var matrix = float4x4.zero;
                matrix += BoneDeforms[boneWeight.Indices.x] * boneWeight.Weights.x;
                matrix += BoneDeforms[boneWeight.Indices.y] * boneWeight.Weights.y;
                matrix += BoneDeforms[boneWeight.Indices.z] * boneWeight.Weights.z;
                matrix += BoneDeforms[boneWeight.Indices.w] * boneWeight.Weights.w;
                matrix = math.inverse(matrix);
                var directed = math.mul(matrix, targetForward);
                var directedNormal = quaternion.LookRotation(new float3(directed.x, directed.y, directed.z), up);

                var bentNormal = math.slerp(originalNormal, directedNormal, GlobalWeight * MaskValues[index]);
                Normals[index] = math.mul(bentNormal, new float3(0.0f, 0.0f, 1.0f));

                // 影響を受けるボーンを抽出
                var influence = boneWeight.Weights >= CommonThreshold;
                InfluentBones[index] = math.select((int4)(-1), boneWeight.Indices, influence);
            }
        }

        internal struct FixedParameters : IEquatable<FixedParameters>
        {
            internal Vector3 WorldSpaceForward;
            internal float Weight;
            internal Texture2D MaskTexture;
            internal bool CanReadMask;
            internal NormalBendMaskMode MaskMode;

            internal static FixedParameters FixFromComponent(Transform defaultDirection, AdHocNormalBending component)
            {
                var directionSource = component.Direction != null ? component.Direction : defaultDirection;
                return new FixedParameters()
                {
                    WorldSpaceForward = directionSource.forward,
                    Weight = component.Weight,
                    MaskTexture = component.Mask,
                    MaskMode = component.Mode,
                    CanReadMask = component.Mask != null && component.Mask.isReadable,
                };
            }

            internal bool IsUnreadableMask => MaskTexture != null && !CanReadMask;

            public bool Equals(FixedParameters other)
            {
                return (WorldSpaceForward - other.WorldSpaceForward).magnitude < 0.0001f
                    && Mathf.Approximately(Weight, other.Weight)
                    && MaskTexture == other.MaskTexture
                    && CanReadMask == other.CanReadMask
                    && MaskMode == other.MaskMode;
            }

            public override bool Equals(object obj) => obj is FixedParameters && Equals((FixedParameters)obj);

            public override int GetHashCode() => (WorldSpaceForward, MaskTexture, MaskMode).GetHashCode();

            public static bool operator ==(FixedParameters lhs, FixedParameters rhs) => lhs.Equals(rhs);

            public static bool operator !=(FixedParameters lhs, FixedParameters rhs) => !(lhs == rhs);
        }
    }
}
