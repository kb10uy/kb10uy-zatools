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
            Debug.Log("Starting Ahvdt");

            if (parameters.TransferTarget == VertexDataTransferTarget.Disabled) return;

            var vertexCount = modifyingMesh.vertexCount;
            if (vertexCount == 0) return;

            var vertices = new List<Vector3>(vertexCount);
            var normals = new List<Vector3>(vertexCount);
            var uvs = new List<Vector4>(vertexCount);
            modifyingMesh.GetVertices(vertices);
            modifyingMesh.GetNormals(normals);
            modifyingMesh.GetUVs((int)parameters.SourceUv, uvs);
            while (vertices.Count < vertexCount) vertices.Add(Vector3.zero);
            while (normals.Count < vertexCount) normals.Add(Vector3.zero);
            while (uvs.Count < vertexCount) uvs.Add(Vector4.zero);

            var verticesInput = new NativeArray<float3>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var normalsInput = new NativeArray<float3>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var sourceUvs = new NativeArray<float4>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            for (var i = 0; i < vertexCount; ++i)
            {
                verticesInput[i] = vertices[i];
                normalsInput[i] = normals[i];
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
                    SourceUvs = sourceUvs,
                    SampledColors = sampledColors,
                };
                var handle = job.Schedule(vertexCount, 4);
                handle.Complete();

                switch (parameters.TransferTarget)
                {
                    case VertexDataTransferTarget.VertexColor:
                        // SetColors overload which takes NativeArray<T> requires T size to be 4 or 16 bytes.
                        Debug.Log("Setting to vertex color");
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
                sourceUvs.Dispose();
                sampledColors.Dispose();
                transferValues.Dispose();
            }
        }

        [BurstCompile]
        internal struct TransferVertexDataJob : IJobParallelFor
        {
            internal NativeArray<float4> TransferValues;
            [ReadOnly] internal VertexDataTransferMode TransferMode;
            [ReadOnly] internal NativeArray<float3> Vertices;
            [ReadOnly] internal NativeArray<float3> Normals;
            [ReadOnly] internal NativeArray<float4> SourceUvs;
            [ReadOnly] internal NativeArray<float4> SampledColors;

            public void Execute(int index)
            {
                var vertex = Vertices[index];
                var normal = Normals[index];
                var sourceUv = SourceUvs[index];
                var sampledColor = SampledColors[index];
                TransferValues[index] = TransferMode switch
                {
                    VertexDataTransferMode.Normal => sampledColor,
                    _ => float4.zero,
                };
            }
        }

        internal struct FixedParameters : IEquatable<FixedParameters>
        {
            internal Texture2D SourceTexture;
            internal UvChannel SourceUv;
            internal VertexDataTransferTarget TransferTarget;
            internal VertexDataTransferMode TransferMode;

            internal static FixedParameters FixFromComponent(AdHocVertexDataTransfer component)
            {
                return new FixedParameters()
                {
                    SourceTexture = component.SourceTexture,
                    SourceUv = component.SourceUv,
                    TransferTarget = component.TransferTarget,
                    TransferMode = component.TransferMode,
                };
            }

            public bool Equals(FixedParameters other)
            {
                return SourceTexture == other.SourceTexture
                    && SourceUv == other.SourceUv
                    && TransferTarget == other.TransferTarget
                    && TransferMode == other.TransferMode;
            }

            public override bool Equals(object obj) => obj is FixedParameters && Equals((FixedParameters)obj);

            public override int GetHashCode() => (SourceTexture, SourceUv, TransferTarget, TransferMode).GetHashCode();

            public static bool operator ==(FixedParameters lhs, FixedParameters rhs) => lhs.Equals(rhs);

            public static bool operator !=(FixedParameters lhs, FixedParameters rhs) => !(lhs == rhs);
        }
    }
}
