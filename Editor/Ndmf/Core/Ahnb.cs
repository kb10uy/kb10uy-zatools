using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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
        /// <exception cref="InvalidOperationException"></exception>
        internal static void Process(SkinnedMeshRenderer referencingRenderer, Mesh modifyingMesh, FixedParameters parameters)
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
            var nativeBoneDeforms = new NativeArray<float4x4>(bones.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            for (var i = 0; i < modifyingMesh.vertexCount; ++i)
            {
                nativeNormals[i] = new float3(normals[i].x, normals[i].y, normals[i].z);
                nativeMaskValues[i] = mask.Take(uvs[i]);
                nativeBoneWeights[i] = InlinedBoneWeight.FromBoneWeight(boneWeights[i]);
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
                MaskValues = nativeMaskValues,
                BoneWeights = nativeBoneWeights,
                BoneDeforms = nativeBoneDeforms,
                WorldSpaceForward = new float3(parameters.WorldSpaceForward.x, parameters.WorldSpaceForward.y, parameters.WorldSpaceForward.z),
                GlobalWeight = parameters.Weight,
            };
            var jobHandle = job.Schedule(nativeNormals.Length, 4);
            jobHandle.Complete();

            // 書き戻し
            modifyingMesh.SetNormals(nativeNormals);
            modifyingMesh.RecalculateTangents();

            nativeNormals.Dispose();
            nativeMaskValues.Dispose();
            nativeBoneWeights.Dispose();
            nativeBoneDeforms.Dispose();
        }


        [BurstCompile]
        internal struct BendNormalJob : IJobParallelFor
        {
            internal NativeArray<float3> Normals;
            [ReadOnly] internal NativeArray<float> MaskValues;
            [ReadOnly] internal NativeArray<InlinedBoneWeight> BoneWeights;
            [ReadOnly] internal NativeArray<float4x4> BoneDeforms;
            [ReadOnly] internal float3 WorldSpaceForward;
            [ReadOnly] internal float GlobalWeight;


            public void Execute(int index)
            {
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
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct InlinedBoneWeight
        {
            public int4 Indices;
            public float4 Weights;

            internal static InlinedBoneWeight FromBoneWeight(BoneWeight weight)
            {
                return new InlinedBoneWeight
                {
                    Indices = new int4(weight.boneIndex0, weight.boneIndex1, weight.boneIndex2, weight.boneIndex3),
                    Weights = new float4(weight.weight0, weight.weight1, weight.weight2, weight.weight3),
                };
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
