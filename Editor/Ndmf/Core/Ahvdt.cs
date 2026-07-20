using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using KusakaFactory.Zatools.Runtime;
using KusakaFactory.Zatools.Foundation;

namespace KusakaFactory.Zatools.Ndmf.Core
{
    internal static class Ahvdt
    {
        internal static void Process(Mesh modifyingMesh, FixedParameters parameters)
        {
            if (parameters.TransferTarget == VertexDataTransferTarget.Disabled) return;

            var vertexCount = modifyingMesh.vertexCount;
            if (vertexCount == 0) return;

            var vertices = new List<Vector3>(vertexCount);
            var normals = new List<Vector3>(vertexCount);
            var tangents = new List<Vector4>(vertexCount);
            var uvs = new List<Vector4>(vertexCount);
            modifyingMesh.GetVertices(vertices);
            modifyingMesh.GetNormals(normals);
            modifyingMesh.GetTangents(tangents);
            modifyingMesh.GetUVs((int)parameters.SourceUv, uvs);
            while (vertices.Count < vertexCount) vertices.Add(Vector3.zero);
            while (normals.Count < vertexCount) normals.Add(Vector3.zero);
            while (tangents.Count < vertexCount) uvs.Add(Vector4.zero);
            while (uvs.Count < vertexCount) uvs.Add(Vector4.zero);

            var verticesInput = new NativeArray<float3>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var normalsInput = new NativeArray<float3>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var tangentsInput = new NativeArray<float4>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var sourceUvs = new NativeArray<float4>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            for (var i = 0; i < vertexCount; ++i)
            {
                verticesInput[i] = vertices[i];
                normalsInput[i] = normals[i];
                tangentsInput[i] = tangents[i];
                sourceUvs[i] = uvs[i];
            }

            var sampledColors = new NativeArray<float4>(vertexCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            var transferValues = new NativeArray<float4>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            try
            {
                NativeTextureSampler.SampleByComputeShader(parameters.SourceTexture, ref sourceUvs, ref sampledColors);

                var job = new TransferVertexDataJob
                {
                    TransferValues = transferValues,
                    TransferMode = parameters.TransferMode,
                    Vertices = verticesInput,
                    Normals = normalsInput,
                    Tangents = tangentsInput,
                    SourceUvs = sourceUvs,
                    SampledColors = sampledColors,
                    Constant = parameters.Constant,
                };
                var handle = job.Schedule(vertexCount, 4);
                handle.Complete();

                switch (parameters.TransferTarget)
                {
                    case VertexDataTransferTarget.VertexColor:
                        // SetColors overload which takes NativeArray<T> requires T size to be 4 or 16 bytes.
                        modifyingMesh.SetColors(transferValues);
                        break;
                    case VertexDataTransferTarget.UV0:
                    case VertexDataTransferTarget.UV1:
                    case VertexDataTransferTarget.UV2:
                    case VertexDataTransferTarget.UV3:
                    case VertexDataTransferTarget.UV4:
                    case VertexDataTransferTarget.UV5:
                    case VertexDataTransferTarget.UV6:
                    case VertexDataTransferTarget.UV7:
                        var targetChannel = parameters.TransferTarget - VertexDataTransferTarget.UV0;
                        modifyingMesh.SetUVs(targetChannel, transferValues);
                        break;
                    default:
                        break;
                }
            }
            finally
            {
                verticesInput.Dispose();
                normalsInput.Dispose();
                tangentsInput.Dispose();
                sourceUvs.Dispose();
                sampledColors.Dispose();
                transferValues.Dispose();
            }
        }

        [BurstCompile]
        internal struct TransferVertexDataJob : IJobParallelFor
        {
            private static readonly float3 LuminanceCoefficient = new float3(0.299f, 0.587f, 0.114f);

            internal NativeArray<float4> TransferValues;
            [ReadOnly] internal VertexDataTransferMode TransferMode;
            [ReadOnly] internal NativeArray<float3> Vertices;
            [ReadOnly] internal NativeArray<float3> Normals;
            [ReadOnly] internal NativeArray<float4> Tangents;
            [ReadOnly] internal NativeArray<float4> SourceUvs;
            [ReadOnly] internal NativeArray<float4> SampledColors;
            [ReadOnly] internal float4 Constant;

            public void Execute(int index)
            {
                var sampledColor = SampledColors[index];
                switch (TransferMode)
                {
                    case VertexDataTransferMode.Copy:
                        TransferValues[index] = sampledColor;
                        break;
                    case VertexDataTransferMode.OneMinus:
                        TransferValues[index] = new float4(1.0f) - sampledColor;
                        break;
                    case VertexDataTransferMode.ConstAndLuminance:
                        TransferValues[index] = new float4(Constant.xyz, math.dot(sampledColor.xyz, LuminanceCoefficient));
                        break;
                    case VertexDataTransferMode.Const01AndLuminance:
                        TransferValues[index] = new float4(Constant.xyz / 2.0f + 0.5f, math.dot(sampledColor.xyz, LuminanceCoefficient));
                        break;
                    case VertexDataTransferMode.LuminanceConstAndConst:
                        TransferValues[index] = new float4(
                            Constant.xyz * math.dot(sampledColor.xyz, LuminanceCoefficient),
                            Constant.w
                        );
                        break;
                    case VertexDataTransferMode.LuminanceConst01AndConst:
                        TransferValues[index] = new float4(
                            Constant.xyz * math.dot(sampledColor.xyz, LuminanceCoefficient) / 2.0f + 0.5f,
                            Constant.w
                        );
                        break;
                    default:
                        TransferValues[index] = float4.zero;
                        break;
                }
            }
        }

        internal struct FixedParameters : IEquatable<FixedParameters>
        {
            internal Texture2D SourceTexture;
            internal UvChannel SourceUv;
            internal VertexDataTransferTarget TransferTarget;
            internal VertexDataTransferMode TransferMode;
            internal float4 Constant;

            internal static FixedParameters FixFromComponent(AdHocVertexDataTransfer component)
            {
                return new FixedParameters()
                {
                    SourceTexture = component.SourceTexture,
                    SourceUv = component.SourceUv,
                    TransferTarget = component.TransferTarget,
                    TransferMode = component.TransferMode,
                    Constant = new float4(component.ConstantVector3, component.ConstantFloat),
                };
            }

            public bool Equals(FixedParameters other)
            {
                return SourceTexture == other.SourceTexture
                    && SourceUv == other.SourceUv
                    && TransferTarget == other.TransferTarget
                    && TransferMode == other.TransferMode
                    && Constant.Equals(other.Constant);
            }

            public override bool Equals(object obj) => obj is FixedParameters && Equals((FixedParameters)obj);

            public override int GetHashCode() => (SourceTexture, SourceUv, TransferTarget, TransferMode, Constant).GetHashCode();

            public static bool operator ==(FixedParameters lhs, FixedParameters rhs) => lhs.Equals(rhs);

            public static bool operator !=(FixedParameters lhs, FixedParameters rhs) => !(lhs == rhs);
        }
    }
}
