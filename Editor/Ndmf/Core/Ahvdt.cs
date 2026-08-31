using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using KusakaFactory.Zatools.Runtime;
using KusakaFactory.Zatools.Foundation;
using KusakaFactory.Zatools.Foundation.Arithmetic;

namespace KusakaFactory.Zatools.Ndmf.Core
{
    internal static class Ahvdt
    {
        internal const ZaxValueType ExpressionResultType = ZaxValueType.Float4;

        internal static readonly ImmutableArray<ZaxVariable> ExpressionVariables = ImmutableArray.Create(
            new ZaxVariable("color", ZaxValueType.Float4),
            new ZaxVariable("uv", ZaxValueType.Float4),
            new ZaxVariable("position", ZaxValueType.Float3),
            new ZaxVariable("normal", ZaxValueType.Float3),
            new ZaxVariable("tangent", ZaxValueType.Float4)
        );

        internal static bool TryCompileExpression(FixedParameters parameters, out ZaxProgram program)
        {
            program = null;
            if (parameters.TransferMode != VertexDataTransferMode.CustomExpression) return true;

            var diagnostics = new List<ZaxDiagnostic>();
            return ZaxCompiler.TryCompile(parameters.Expression, ExpressionVariables, ExpressionResultType, diagnostics, out program);
        }

        internal static void Process(Mesh modifyingMesh, FixedParameters parameters, ZaxProgram expressionProgram)
        {
            if (parameters.TransferTarget == VertexDataTransferTarget.Disabled) return;
            if (parameters.TransferMode == VertexDataTransferMode.CustomExpression && expressionProgram == null) return;

            var vertexCount = modifyingMesh.vertexCount;
            if (vertexCount == 0) return;

            var uvs = new List<Vector4>(vertexCount);
            modifyingMesh.GetUVs((int)parameters.SourceUv, uvs);
            while (uvs.Count < vertexCount) uvs.Add(Vector4.zero);

            var sourceUvs = new NativeArray<float4>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            for (var i = 0; i < vertexCount; ++i) sourceUvs[i] = uvs[i];

            var sampledColors = new NativeArray<float4>(vertexCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            var transferValues = new NativeArray<float4>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            try
            {
                NativeTextureSampler.SampleByComputeShader(parameters.SourceTexture, ref sourceUvs, ref sampledColors);

                if (parameters.TransferMode == VertexDataTransferMode.CustomExpression)
                {
                    EvaluateExpression(modifyingMesh, expressionProgram, sourceUvs, sampledColors, transferValues);
                }
                else
                {
                    var job = new TransferVertexDataJob
                    {
                        TransferValues = transferValues,
                        TransferMode = parameters.TransferMode,
                        SampledColors = sampledColors,
                        Constant = parameters.Constant,
                    };
                    var handle = job.Schedule(vertexCount, 4);
                    handle.Complete();
                }

                switch (parameters.TransferTarget)
                {
                    case VertexDataTransferTarget.VertexColor:
                        // SetColors overload which takes NativeArray<T> requires T size to be 4 or 16 bytes.
                        modifyingMesh.SetColors(transferValues.Reinterpret<Color>());
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
                sourceUvs.Dispose();
                sampledColors.Dispose();
                transferValues.Dispose();
            }
        }

        private static unsafe void EvaluateExpression(
            Mesh modifyingMesh,
            ZaxProgram program,
            NativeArray<float4> sourceUvs,
            NativeArray<float4> sampledColors,
            NativeArray<float4> transferValues)
        {
            var vertexCount = transferValues.Length;
            var vertices = new List<Vector3>(vertexCount);
            var normals = new List<Vector3>(vertexCount);
            var tangents = new List<Vector4>(vertexCount);
            modifyingMesh.GetVertices(vertices);
            modifyingMesh.GetNormals(normals);
            modifyingMesh.GetTangents(tangents);
            while (vertices.Count < vertexCount) vertices.Add(Vector3.zero);
            while (normals.Count < vertexCount) normals.Add(Vector3.zero);
            while (tangents.Count < vertexCount) tangents.Add(Vector4.zero);

            var nativeVertices = new NativeArray<float3>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var nativeNormals = new NativeArray<float3>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var nativeTangents = new NativeArray<float4>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            for (var i = 0; i < vertexCount; ++i)
            {
                nativeVertices[i] = vertices[i];
                nativeNormals[i] = normals[i];
                nativeTangents[i] = tangents[i];
            }

            var nativeProgram = ZaxNativeProgram.Allocate(program, Allocator.TempJob);
            var bindings = new NativeArray<ZaxVariableBinding>(program.Variables.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var results = new NativeArray<ZaxValue>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            try
            {
                for (var i = 0; i < program.Variables.Length; ++i)
                {
                    bindings[i] = program.Variables[i].Name switch
                    {
                        "color" => ZaxVariableBinding.FromArray(sampledColors, ZaxValueType.Float4),
                        "uv" => ZaxVariableBinding.FromArray(sourceUvs, ZaxValueType.Float4),
                        "position" => ZaxVariableBinding.FromArray(nativeVertices, ZaxValueType.Float3),
                        "normal" => ZaxVariableBinding.FromArray(nativeNormals, ZaxValueType.Float3),
                        "tangent" => ZaxVariableBinding.FromArray(nativeTangents, ZaxValueType.Float4),
                        _ => throw new InvalidOperationException("unbindable variable: " + program.Variables[i].Name),
                    };
                }

                ZaxEvaluateJob.Schedule(nativeProgram, bindings, results).Complete();
                for (var i = 0; i < vertexCount; ++i) transferValues[i] = results[i].AsFloat4;
            }
            finally
            {
                nativeVertices.Dispose();
                nativeNormals.Dispose();
                nativeTangents.Dispose();
                nativeProgram.Dispose();
                bindings.Dispose();
                results.Dispose();
            }
        }

        [BurstCompile]
        internal struct TransferVertexDataJob : IJobParallelFor
        {
            private static readonly float3 LuminanceCoefficient = new float3(0.299f, 0.587f, 0.114f);

            internal NativeArray<float4> TransferValues;
            [ReadOnly] internal VertexDataTransferMode TransferMode;
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
            internal string Expression;

            internal static FixedParameters FixFromComponent(AdHocVertexDataTransfer component)
            {
                return new FixedParameters()
                {
                    SourceTexture = component.SourceTexture,
                    SourceUv = component.SourceUv,
                    TransferTarget = component.TransferTarget,
                    TransferMode = component.TransferMode,
                    Constant = new float4(component.ConstantVector3, component.ConstantFloat),
                    Expression = component.Expression,
                };
            }

            public bool Equals(FixedParameters other)
            {
                return SourceTexture == other.SourceTexture
                    && SourceUv == other.SourceUv
                    && TransferTarget == other.TransferTarget
                    && TransferMode == other.TransferMode
                    && Constant.Equals(other.Constant)
                    && string.Equals(Expression, other.Expression, StringComparison.Ordinal);
            }

            public override bool Equals(object obj) => obj is FixedParameters && Equals((FixedParameters)obj);

            public override int GetHashCode() => (SourceTexture, SourceUv, TransferTarget, TransferMode, Constant, Expression).GetHashCode();

            public static bool operator ==(FixedParameters lhs, FixedParameters rhs) => lhs.Equals(rhs);

            public static bool operator !=(FixedParameters lhs, FixedParameters rhs) => !(lhs == rhs);
        }
    }
}
