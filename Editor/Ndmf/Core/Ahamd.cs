using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Mathematics;
using KusakaFactory.Zatools.Foundation;
using KusakaFactory.Zatools.Foundation.Arithmetic;
using KusakaFactory.Zatools.Runtime;

namespace KusakaFactory.Zatools.Ndmf.Core
{
    internal static class Ahamd
    {
        private const int UvChannelCount = 8;
        private const int ScratchCount = 4;
        private const int UInt16VertexLimit = 65535;
        private const float BlendShapeDeltaThreshold = 1.0e-4f;

        internal static readonly ImmutableArray<ZaxVariable> CommonVariables = ImmutableArray.Create(
            new ZaxVariable("position", ZaxValueType.Float3),
            new ZaxVariable("normal", ZaxValueType.Float3),
            new ZaxVariable("tangent", ZaxValueType.Float4),
            new ZaxVariable("color", ZaxValueType.Float4),
            new ZaxVariable("uv0", ZaxValueType.Float4),
            new ZaxVariable("uv1", ZaxValueType.Float4),
            new ZaxVariable("uv2", ZaxValueType.Float4),
            new ZaxVariable("uv3", ZaxValueType.Float4),
            new ZaxVariable("uv4", ZaxValueType.Float4),
            new ZaxVariable("uv5", ZaxValueType.Float4),
            new ZaxVariable("uv6", ZaxValueType.Float4),
            new ZaxVariable("uv7", ZaxValueType.Float4),
            new ZaxVariable("scratch0", ZaxValueType.Float4),
            new ZaxVariable("scratch1", ZaxValueType.Float4),
            new ZaxVariable("scratch2", ZaxValueType.Float4),
            new ZaxVariable("scratch3", ZaxValueType.Float4)
        );

        internal static readonly ImmutableArray<ZaxVariable> SelectionVariables =
            CommonVariables.Add(new ZaxVariable("mask", ZaxValueType.Float4));

        internal static readonly ImmutableArray<ZaxVariable> ModificationVariables =
            CommonVariables.Add(new ZaxVariable("texture", ZaxValueType.Float4));

        internal static ZaxValueType ResultTypeOf(AhamdModificationTarget target)
        {
            switch (target)
            {
                case AhamdModificationTarget.Position:
                case AhamdModificationTarget.Normal:
                    return ZaxValueType.Float3;
                default:
                    return ZaxValueType.Float4;
            }
        }

        internal static void Process(SkinnedMeshRenderer targetRenderer, Mesh modifyingMesh, FixedParameters parameters)
        {
            if (targetRenderer == null || modifyingMesh == null || parameters.Source == null) return;

            var sourceMesh = parameters.Source.sharedMesh;
            if (sourceMesh == null || sourceMesh.vertexCount == 0) return;

            ZaxCompiler.TryCompile(
                parameters.SelectionExpression,
                SelectionVariables,
                ZaxValueType.Float,
                new List<ZaxDiagnostic>(),
                out var selectionProgram);

            var modifications = new List<(FixedModification Modification, ZaxProgram Program)>(parameters.Modifications.Length);
            foreach (var modification in parameters.Modifications)
            {
                var compiled = ZaxCompiler.TryCompile(
                    modification.Expression,
                    ModificationVariables,
                    modification.ResultType,
                    new List<ZaxDiagnostic>(),
                    out var program);
                if (compiled) modifications.Add((modification, program));
            }

            var required = DetermineRequiredAttributes(sourceMesh, parameters, selectionProgram, modifications);
            using var sourceAttributes = VertexAttributes.ReadFrom(sourceMesh, required);

            var selections = SelectVertices(sourceAttributes, selectionProgram, parameters);
            var (subMeshes, newToOld) = ExtractSelectedTriangles(sourceMesh, selections);
            if (newToOld.Length == 0) return;

            using var attributes = sourceAttributes.Compact(newToOld);
            foreach (var (modification, program) in modifications) ApplyModification(attributes, modification, program);

            BuildMesh(modifyingMesh, sourceMesh, attributes, subMeshes, newToOld, parameters);
            targetRenderer.sharedMaterials = BuildMaterials(parameters, subMeshes);
        }

        private static RequiredAttributes DetermineRequiredAttributes(
            Mesh sourceMesh,
            FixedParameters parameters,
            ZaxProgram selectionProgram,
            List<(FixedModification Modification, ZaxProgram Program)> modifications)
        {
            var required = new RequiredAttributes
            {
                Normals = sourceMesh.HasVertexAttribute(VertexAttribute.Normal),
                Tangents = sourceMesh.HasVertexAttribute(VertexAttribute.Tangent),
                Colors = sourceMesh.HasVertexAttribute(VertexAttribute.Color),
            };
            for (var channel = 0; channel < UvChannelCount; ++channel)
            {
                required.Uvs[channel] = sourceMesh.HasVertexAttribute(VertexAttribute.TexCoord0 + channel);
            }

            if (selectionProgram != null)
            {
                MarkReferencedVariables(required, selectionProgram);
                if (parameters.SelectionTexture != null) required.Uvs[(int)parameters.SelectionTextureUv] = true;
            }
            foreach (var (modification, program) in modifications)
            {
                MarkReferencedVariables(required, program);
                MarkTargetAttribute(required, modification.Target);
                if (modification.ExtraTexture != null) required.Uvs[(int)modification.ExtraTextureUv] = true;
            }

            return required;
        }

        private static void MarkReferencedVariables(RequiredAttributes required, ZaxProgram program)
        {
            foreach (var variable in program.Variables)
            {
                switch (variable.Name)
                {
                    case "normal":
                        required.Normals = true;
                        break;
                    case "tangent":
                        required.Tangents = true;
                        break;
                    case "color":
                        required.Colors = true;
                        break;
                    default:
                        if (TryParseIndexedName(variable.Name, "uv", UvChannelCount, out var uvChannel))
                        {
                            required.Uvs[uvChannel] = true;
                        }
                        else if (TryParseIndexedName(variable.Name, "scratch", ScratchCount, out var scratchIndex))
                        {
                            required.Scratches[scratchIndex] = true;
                        }
                        break;
                }
            }
        }

        private static void MarkTargetAttribute(RequiredAttributes required, AhamdModificationTarget target)
        {
            if (target >= AhamdModificationTarget.UV0 && target <= AhamdModificationTarget.UV7)
            {
                required.Uvs[target - AhamdModificationTarget.UV0] = true;
                return;
            }
            if (target >= AhamdModificationTarget.Scratch0 && target <= AhamdModificationTarget.Scratch3)
            {
                required.Scratches[target - AhamdModificationTarget.Scratch0] = true;
                return;
            }

            switch (target)
            {
                case AhamdModificationTarget.Normal:
                    required.Normals = true;
                    break;
                case AhamdModificationTarget.Tangent:
                    required.Tangents = true;
                    break;
                case AhamdModificationTarget.VertexColor:
                    required.Colors = true;
                    break;
                default:
                    break;
            }
        }

        private static bool TryParseIndexedName(string name, string prefix, int count, out int index)
        {
            index = -1;
            if (name.Length != prefix.Length + 1) return false;
            if (!name.StartsWith(prefix, StringComparison.Ordinal)) return false;

            var parsed = name[prefix.Length] - '0';
            if (parsed < 0 || parsed >= count) return false;

            index = parsed;
            return true;
        }

        private static bool[] SelectVertices(VertexAttributes attributes, ZaxProgram selectionProgram, FixedParameters parameters)
        {
            var selections = new bool[attributes.VertexCount];
            if (selectionProgram == null)
            {
                for (var i = 0; i < selections.Length; ++i) selections[i] = true;
                return selections;
            }

            var sampledColors = SampleTexture(attributes, parameters.SelectionTexture, parameters.SelectionTextureUv);
            try
            {
                using var results = Evaluate(attributes, selectionProgram, sampledColors);
                for (var i = 0; i < selections.Length; ++i) selections[i] = results[i].AsFloat >= parameters.SelectionThreshold;
            }
            finally
            {
                if (sampledColors.IsCreated) sampledColors.Dispose();
            }

            return selections;
        }

        private static void ApplyModification(VertexAttributes attributes, FixedModification modification, ZaxProgram program)
        {
            var sampledColors = SampleTexture(attributes, modification.ExtraTexture, modification.ExtraTextureUv);
            try
            {
                using var results = Evaluate(attributes, program, sampledColors);
                attributes.Write(modification.Target, results);
            }
            finally
            {
                if (sampledColors.IsCreated) sampledColors.Dispose();
            }
        }

        private static NativeArray<float4> SampleTexture(VertexAttributes attributes, Texture2D texture, UvChannel channel)
        {
            if (texture == null) return default;

            var samplingUvs = attributes.Uvs[(int)channel];
            var sampledColors = new NativeArray<float4>(attributes.VertexCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            NativeTextureSampler.SampleByComputeShader(texture, ref samplingUvs, ref sampledColors);
            return sampledColors;
        }

        private static NativeArray<ZaxValue> Evaluate(VertexAttributes attributes, ZaxProgram program, NativeArray<float4> sampledColors)
        {
            var results = new NativeArray<ZaxValue>(attributes.VertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            if (program.Variables.Length == 0)
            {
                var uniformResult = ZaxEvaluator.Evaluate(program);
                for (var i = 0; i < results.Length; ++i) results[i] = uniformResult;
                return results;
            }

            var nativeProgram = ZaxNativeProgram.Allocate(program, Allocator.TempJob);
            var bindings = new NativeArray<ZaxVariableBinding>(program.Variables.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            try
            {
                for (var i = 0; i < program.Variables.Length; ++i)
                {
                    bindings[i] = attributes.BindingOf(program.Variables[i].Name, sampledColors);
                }
                ZaxEvaluateJob.Schedule(nativeProgram, bindings, results).Complete();
                return results;
            }
            catch
            {
                results.Dispose();
                throw;
            }
            finally
            {
                nativeProgram.Dispose();
                bindings.Dispose();
            }
        }

        private static (List<SubMeshIndices> SubMeshes, int[] NewToOld) ExtractSelectedTriangles(Mesh sourceMesh, bool[] selections)
        {
            var subMeshes = new List<SubMeshIndices>(sourceMesh.subMeshCount);
            var usedVertices = new bool[selections.Length];
            var sourceIndices = new List<int>();
            for (var sm = 0; sm < sourceMesh.subMeshCount; ++sm)
            {
                if (sourceMesh.GetTopology(sm) != MeshTopology.Triangles) continue;

                sourceMesh.GetTriangles(sourceIndices, sm);
                var selectedIndices = new List<int>();
                for (var i = 0; i + 2 < sourceIndices.Count; i += 3)
                {
                    var i0 = sourceIndices[i];
                    var i1 = sourceIndices[i + 1];
                    var i2 = sourceIndices[i + 2];
                    if (!selections[i0] || !selections[i1] || !selections[i2]) continue;

                    selectedIndices.Add(i0);
                    selectedIndices.Add(i1);
                    selectedIndices.Add(i2);
                    usedVertices[i0] = true;
                    usedVertices[i1] = true;
                    usedVertices[i2] = true;
                }
                if (selectedIndices.Count == 0) continue;

                subMeshes.Add(new SubMeshIndices { SourceSubMesh = sm, Indices = selectedIndices });
            }

            var oldToNew = new int[selections.Length];
            var newToOld = new List<int>();
            for (var i = 0; i < usedVertices.Length; ++i)
            {
                if (!usedVertices[i])
                {
                    oldToNew[i] = -1;
                    continue;
                }
                oldToNew[i] = newToOld.Count;
                newToOld.Add(i);
            }

            foreach (var subMesh in subMeshes)
            {
                for (var i = 0; i < subMesh.Indices.Count; ++i) subMesh.Indices[i] = oldToNew[subMesh.Indices[i]];
            }

            return (subMeshes, newToOld.ToArray());
        }

        private static void BuildMesh(
            Mesh modifyingMesh,
            Mesh sourceMesh,
            VertexAttributes attributes,
            List<SubMeshIndices> subMeshes,
            int[] newToOld,
            FixedParameters parameters)
        {
            modifyingMesh.Clear();
            modifyingMesh.indexFormat = attributes.VertexCount > UInt16VertexLimit ? IndexFormat.UInt32 : IndexFormat.UInt16;

            modifyingMesh.SetVertices(attributes.Positions.Reinterpret<Vector3>());
            if (attributes.HasNormals) modifyingMesh.SetNormals(attributes.Normals.Reinterpret<Vector3>());
            if (attributes.HasTangents) modifyingMesh.SetTangents(attributes.Tangents.Reinterpret<Vector4>());
            // SetColors overload which takes NativeArray<T> requires T size to be 4 or 16 bytes.
            if (attributes.HasColors) modifyingMesh.SetColors(attributes.Colors.Reinterpret<Color>());
            for (var channel = 0; channel < UvChannelCount; ++channel) attributes.WriteUvsTo(modifyingMesh, channel);

            modifyingMesh.bindposes = sourceMesh.bindposes;
            CopyBoneWeights(modifyingMesh, sourceMesh, newToOld);

            modifyingMesh.subMeshCount = subMeshes.Count;
            for (var sm = 0; sm < subMeshes.Count; ++sm) modifyingMesh.SetTriangles(subMeshes[sm].Indices, sm);

            CopyBlendShapes(modifyingMesh, sourceMesh, newToOld);

            if (parameters.RecalculateNormals) modifyingMesh.RecalculateNormals();
            if (parameters.RecalculateTangents) modifyingMesh.RecalculateTangents();
            modifyingMesh.RecalculateBounds();
        }

        private static void CopyBoneWeights(Mesh modifyingMesh, Mesh sourceMesh, int[] newToOld)
        {
            var sourceBonesPerVertex = sourceMesh.GetBonesPerVertex();
            if (!sourceBonesPerVertex.IsCreated || sourceBonesPerVertex.Length == 0) return;

            var sourceBoneWeights = sourceMesh.GetAllBoneWeights();
            var sourceOffsets = new int[sourceBonesPerVertex.Length];
            var sourceOffset = 0;
            for (var i = 0; i < sourceBonesPerVertex.Length; ++i)
            {
                sourceOffsets[i] = sourceOffset;
                sourceOffset += sourceBonesPerVertex[i];
            }

            var boneWeightCount = 0;
            for (var i = 0; i < newToOld.Length; ++i) boneWeightCount += sourceBonesPerVertex[newToOld[i]];

            var bonesPerVertex = new NativeArray<byte>(newToOld.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            var boneWeights = new NativeArray<BoneWeight1>(boneWeightCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            try
            {
                var cursor = 0;
                for (var i = 0; i < newToOld.Length; ++i)
                {
                    var oldIndex = newToOld[i];
                    var influenceCount = sourceBonesPerVertex[oldIndex];
                    bonesPerVertex[i] = influenceCount;
                    for (var w = 0; w < influenceCount; ++w) boneWeights[cursor + w] = sourceBoneWeights[sourceOffsets[oldIndex] + w];
                    cursor += influenceCount;
                }
                modifyingMesh.SetBoneWeights(bonesPerVertex, boneWeights);
            }
            finally
            {
                bonesPerVertex.Dispose();
                boneWeights.Dispose();
            }
        }

        private static void CopyBlendShapes(Mesh modifyingMesh, Mesh sourceMesh, int[] newToOld)
        {
            var blendShapeCount = sourceMesh.blendShapeCount;
            if (blendShapeCount == 0) return;

            var sourceVertexCount = sourceMesh.vertexCount;
            var sourceDeltaVertices = new Vector3[sourceVertexCount];
            var sourceDeltaNormals = new Vector3[sourceVertexCount];
            var sourceDeltaTangents = new Vector3[sourceVertexCount];
            var squaredThreshold = BlendShapeDeltaThreshold * BlendShapeDeltaThreshold;

            var frameWeights = new List<float>();
            var frameVertices = new List<Vector3[]>();
            var frameNormals = new List<Vector3[]>();
            var frameTangents = new List<Vector3[]>();

            for (var shape = 0; shape < blendShapeCount; ++shape)
            {
                var frameCount = sourceMesh.GetBlendShapeFrameCount(shape);
                while (frameVertices.Count < frameCount)
                {
                    frameVertices.Add(new Vector3[newToOld.Length]);
                    frameNormals.Add(new Vector3[newToOld.Length]);
                    frameTangents.Add(new Vector3[newToOld.Length]);
                }
                frameWeights.Clear();

                var hasDelta = false;
                for (var frame = 0; frame < frameCount; ++frame)
                {
                    sourceMesh.GetBlendShapeFrameVertices(shape, frame, sourceDeltaVertices, sourceDeltaNormals, sourceDeltaTangents);
                    frameWeights.Add(sourceMesh.GetBlendShapeFrameWeight(shape, frame));

                    var deltaVertices = frameVertices[frame];
                    var deltaNormals = frameNormals[frame];
                    var deltaTangents = frameTangents[frame];
                    for (var i = 0; i < newToOld.Length; ++i)
                    {
                        var oldIndex = newToOld[i];
                        deltaVertices[i] = sourceDeltaVertices[oldIndex];
                        deltaNormals[i] = sourceDeltaNormals[oldIndex];
                        deltaTangents[i] = sourceDeltaTangents[oldIndex];
                        hasDelta = hasDelta
                            || deltaVertices[i].sqrMagnitude > squaredThreshold
                            || deltaNormals[i].sqrMagnitude > squaredThreshold
                            || deltaTangents[i].sqrMagnitude > squaredThreshold;
                    }
                }
                if (!hasDelta) continue;

                var name = sourceMesh.GetBlendShapeName(shape);
                for (var frame = 0; frame < frameCount; ++frame)
                {
                    modifyingMesh.AddBlendShapeFrame(
                        name,
                        frameWeights[frame],
                        frameVertices[frame],
                        frameNormals[frame],
                        frameTangents[frame]);
                }
            }
        }

        private static Material[] BuildMaterials(FixedParameters parameters, List<SubMeshIndices> subMeshes)
        {
            var materials = new Material[subMeshes.Count];
            var sourceMaterials = parameters.Source.sharedMaterials;
            for (var i = 0; i < materials.Length; ++i)
            {
                var sourceSubMesh = subMeshes[i].SourceSubMesh;
                materials[i] = sourceSubMesh < sourceMaterials.Length ? sourceMaterials[sourceSubMesh] : null;
            }
            return materials;
        }

        private sealed class SubMeshIndices
        {
            internal int SourceSubMesh;
            internal List<int> Indices;
        }

        private sealed class RequiredAttributes
        {
            internal bool Normals;
            internal bool Tangents;
            internal bool Colors;
            internal readonly bool[] Uvs = new bool[UvChannelCount];
            internal readonly bool[] Scratches = new bool[ScratchCount];
        }

        private sealed class VertexAttributes : IDisposable
        {
            internal int VertexCount;
            internal RequiredAttributes Required;
            internal NativeArray<float3> Positions;
            internal NativeArray<float3> Normals;
            internal NativeArray<float4> Tangents;
            internal NativeArray<float4> Colors;
            internal NativeArray<float4>[] Uvs;
            internal NativeArray<float4>[] Scratches;
            internal bool HasNormals;
            internal bool HasTangents;
            internal bool HasColors;
            internal int[] UvDimensions;

            internal static VertexAttributes ReadFrom(Mesh mesh, RequiredAttributes required)
            {
                var vertexCount = mesh.vertexCount;
                var attributes = Allocate(vertexCount, required);
                var vector3Values = new List<Vector3>(vertexCount);
                var vector4Values = new List<Vector4>(vertexCount);
                var colorValues = new List<Color>(vertexCount);

                mesh.GetVertices(vector3Values);
                for (var i = 0; i < vertexCount; ++i) attributes.Positions[i] = vector3Values[i];

                if (required.Normals)
                {
                    mesh.GetNormals(vector3Values);
                    attributes.HasNormals = vector3Values.Count == vertexCount;
                    if (attributes.HasNormals)
                    {
                        for (var i = 0; i < vertexCount; ++i) attributes.Normals[i] = vector3Values[i];
                    }
                }

                if (required.Tangents)
                {
                    mesh.GetTangents(vector4Values);
                    attributes.HasTangents = vector4Values.Count == vertexCount;
                    if (attributes.HasTangents)
                    {
                        for (var i = 0; i < vertexCount; ++i) attributes.Tangents[i] = vector4Values[i];
                    }
                }

                if (required.Colors)
                {
                    mesh.GetColors(colorValues);
                    attributes.HasColors = colorValues.Count == vertexCount;
                    if (attributes.HasColors)
                    {
                        for (var i = 0; i < vertexCount; ++i) attributes.Colors[i] = (Vector4)colorValues[i];
                    }
                    else
                    {
                        for (var i = 0; i < vertexCount; ++i) attributes.Colors[i] = new float4(1.0f);
                    }
                }

                for (var channel = 0; channel < UvChannelCount; ++channel)
                {
                    if (!required.Uvs[channel]) continue;

                    mesh.GetUVs(channel, vector4Values);
                    if (vector4Values.Count != vertexCount) continue;

                    attributes.UvDimensions[channel] = mesh.GetVertexAttributeDimension(VertexAttribute.TexCoord0 + channel);
                    for (var i = 0; i < vertexCount; ++i) attributes.Uvs[channel][i] = vector4Values[i];
                }

                return attributes;
            }

            internal VertexAttributes Compact(int[] newToOld)
            {
                var compacted = Allocate(newToOld.Length, Required);
                compacted.HasNormals = HasNormals;
                compacted.HasTangents = HasTangents;
                compacted.HasColors = HasColors;
                Array.Copy(UvDimensions, compacted.UvDimensions, UvChannelCount);

                CompactInto(Positions, compacted.Positions, newToOld);
                if (Normals.IsCreated) CompactInto(Normals, compacted.Normals, newToOld);
                if (Tangents.IsCreated) CompactInto(Tangents, compacted.Tangents, newToOld);
                if (Colors.IsCreated) CompactInto(Colors, compacted.Colors, newToOld);
                for (var channel = 0; channel < UvChannelCount; ++channel)
                {
                    if (Uvs[channel].IsCreated) CompactInto(Uvs[channel], compacted.Uvs[channel], newToOld);
                }
                for (var scratch = 0; scratch < ScratchCount; ++scratch)
                {
                    if (Scratches[scratch].IsCreated) CompactInto(Scratches[scratch], compacted.Scratches[scratch], newToOld);
                }

                return compacted;
            }

            internal ZaxVariableBinding BindingOf(string name, NativeArray<float4> sampledColors)
            {
                switch (name)
                {
                    case "position": return ZaxVariableBinding.FromArray(Positions, ZaxValueType.Float3);
                    case "normal": return ZaxVariableBinding.FromArray(Normals, ZaxValueType.Float3);
                    case "tangent": return ZaxVariableBinding.FromArray(Tangents, ZaxValueType.Float4);
                    case "color": return ZaxVariableBinding.FromArray(Colors, ZaxValueType.Float4);
                    case "mask":
                    case "texture":
                        // A missing texture behaves as pure white, as Unity's default texture does.
                        return sampledColors.IsCreated
                            ? ZaxVariableBinding.FromArray(sampledColors, ZaxValueType.Float4)
                            : ZaxVariableBinding.Uniform(ZaxValue.FromFloat4(new float4(1.0f)));
                    default:
                        if (TryParseIndexedName(name, "uv", UvChannelCount, out var uvChannel))
                        {
                            return ZaxVariableBinding.FromArray(Uvs[uvChannel], ZaxValueType.Float4);
                        }
                        if (TryParseIndexedName(name, "scratch", ScratchCount, out var scratchIndex))
                        {
                            return ZaxVariableBinding.FromArray(Scratches[scratchIndex], ZaxValueType.Float4);
                        }
                        throw new InvalidOperationException("unbindable variable: " + name);
                }
            }

            internal void Write(AhamdModificationTarget target, NativeArray<ZaxValue> results)
            {
                if (target >= AhamdModificationTarget.UV0 && target <= AhamdModificationTarget.UV7)
                {
                    var channel = target - AhamdModificationTarget.UV0;
                    for (var i = 0; i < VertexCount; ++i) Uvs[channel][i] = results[i].AsFloat4;
                    UvDimensions[channel] = 4;
                    return;
                }
                if (target >= AhamdModificationTarget.Scratch0 && target <= AhamdModificationTarget.Scratch3)
                {
                    var scratch = target - AhamdModificationTarget.Scratch0;
                    for (var i = 0; i < VertexCount; ++i) Scratches[scratch][i] = results[i].AsFloat4;
                    return;
                }

                switch (target)
                {
                    case AhamdModificationTarget.Position:
                        for (var i = 0; i < VertexCount; ++i) Positions[i] = results[i].AsFloat3;
                        break;
                    case AhamdModificationTarget.Normal:
                        for (var i = 0; i < VertexCount; ++i) Normals[i] = results[i].AsFloat3;
                        HasNormals = true;
                        break;
                    case AhamdModificationTarget.Tangent:
                        for (var i = 0; i < VertexCount; ++i) Tangents[i] = results[i].AsFloat4;
                        HasTangents = true;
                        break;
                    case AhamdModificationTarget.VertexColor:
                        for (var i = 0; i < VertexCount; ++i) Colors[i] = results[i].AsFloat4;
                        HasColors = true;
                        break;
                    default:
                        break;
                }
            }

            internal void WriteUvsTo(Mesh mesh, int channel)
            {
                if (UvDimensions[channel] == 0) return;

                var uvs = Uvs[channel];
                switch (UvDimensions[channel])
                {
                    case 2:
                    {
                        var values = new List<Vector2>(VertexCount);
                        for (var i = 0; i < VertexCount; ++i) values.Add(new Vector2(uvs[i].x, uvs[i].y));
                        mesh.SetUVs(channel, values);
                        break;
                    }
                    case 3:
                    {
                        var values = new List<Vector3>(VertexCount);
                        for (var i = 0; i < VertexCount; ++i) values.Add(new Vector3(uvs[i].x, uvs[i].y, uvs[i].z));
                        mesh.SetUVs(channel, values);
                        break;
                    }
                    default:
                        mesh.SetUVs(channel, uvs);
                        break;
                }
            }

            public void Dispose()
            {
                if (Positions.IsCreated) Positions.Dispose();
                if (Normals.IsCreated) Normals.Dispose();
                if (Tangents.IsCreated) Tangents.Dispose();
                if (Colors.IsCreated) Colors.Dispose();
                if (Uvs != null)
                {
                    for (var i = 0; i < Uvs.Length; ++i)
                    {
                        if (Uvs[i].IsCreated) Uvs[i].Dispose();
                    }
                }
                if (Scratches != null)
                {
                    for (var i = 0; i < Scratches.Length; ++i)
                    {
                        if (Scratches[i].IsCreated) Scratches[i].Dispose();
                    }
                }
            }

            private static VertexAttributes Allocate(int vertexCount, RequiredAttributes required)
            {
                var attributes = new VertexAttributes
                {
                    VertexCount = vertexCount,
                    Required = required,
                    Positions = new NativeArray<float3>(vertexCount, Allocator.Persistent),
                    Uvs = new NativeArray<float4>[UvChannelCount],
                    Scratches = new NativeArray<float4>[ScratchCount],
                    UvDimensions = new int[UvChannelCount],
                };
                if (required.Normals) attributes.Normals = new NativeArray<float3>(vertexCount, Allocator.Persistent);
                if (required.Tangents) attributes.Tangents = new NativeArray<float4>(vertexCount, Allocator.Persistent);
                if (required.Colors) attributes.Colors = new NativeArray<float4>(vertexCount, Allocator.Persistent);
                for (var i = 0; i < UvChannelCount; ++i)
                {
                    if (required.Uvs[i]) attributes.Uvs[i] = new NativeArray<float4>(vertexCount, Allocator.Persistent);
                }
                for (var i = 0; i < ScratchCount; ++i)
                {
                    if (required.Scratches[i]) attributes.Scratches[i] = new NativeArray<float4>(vertexCount, Allocator.Persistent);
                }
                return attributes;
            }

            private static void CompactInto<T>(NativeArray<T> source, NativeArray<T> destination, int[] newToOld) where T : unmanaged
            {
                for (var i = 0; i < newToOld.Length; ++i) destination[i] = source[newToOld[i]];
            }
        }

        internal struct FixedParameters : IEquatable<FixedParameters>
        {
            internal SkinnedMeshRenderer Source;
            internal Texture2D SelectionTexture;
            internal UvChannel SelectionTextureUv;
            internal string SelectionExpression;
            internal float SelectionThreshold;
            internal ImmutableArray<FixedModification> Modifications;
            internal bool RecalculateNormals;
            internal bool RecalculateTangents;

            internal static FixedParameters FixFromComponent(AdHocAdvancedMeshDuplication component)
            {
                return new FixedParameters()
                {
                    Source = component.Source,
                    SelectionTexture = component.SelectionTexture,
                    SelectionTextureUv = component.SelectionTextureUv,
                    SelectionExpression = component.SelectionExpression,
                    SelectionThreshold = component.SelectionThreshold,
                    Modifications = component.Modifications
                        .Where((m) => m != null && m.Enabled)
                        .Select(FixedModification.FixFromStep)
                        .ToImmutableArray(),
                    RecalculateNormals = component.RecalculateNormals,
                    RecalculateTangents = component.RecalculateTangents,
                };
            }

            public bool Equals(FixedParameters other)
            {
                return Source == other.Source
                    && SelectionTexture == other.SelectionTexture
                    && SelectionTextureUv == other.SelectionTextureUv
                    && string.Equals(SelectionExpression, other.SelectionExpression, StringComparison.Ordinal)
                    && Mathf.Approximately(SelectionThreshold, other.SelectionThreshold)
                    && Modifications.SequenceEqual(other.Modifications)
                    && RecalculateNormals == other.RecalculateNormals
                    && RecalculateTangents == other.RecalculateTangents;
            }

            public override bool Equals(object obj) => obj is FixedParameters && Equals((FixedParameters)obj);

            public override int GetHashCode() =>
                (Source, SelectionTexture, SelectionExpression, Modifications.Length).GetHashCode();

            public static bool operator ==(FixedParameters lhs, FixedParameters rhs) => lhs.Equals(rhs);

            public static bool operator !=(FixedParameters lhs, FixedParameters rhs) => !(lhs == rhs);
        }

        internal struct FixedModification : IEquatable<FixedModification>
        {
            internal Texture2D ExtraTexture;
            internal UvChannel ExtraTextureUv;
            internal AhamdModificationTarget Target;
            internal string Expression;

            internal ZaxValueType ResultType => ResultTypeOf(Target);

            internal static FixedModification FixFromStep(AhamdModificationStep step)
            {
                return new FixedModification()
                {
                    ExtraTexture = step.ExtraTexture,
                    ExtraTextureUv = step.ExtraTextureUv,
                    Target = step.Target,
                    Expression = step.Expression,
                };
            }

            public bool Equals(FixedModification other)
            {
                return ExtraTexture == other.ExtraTexture
                    && ExtraTextureUv == other.ExtraTextureUv
                    && Target == other.Target
                    && string.Equals(Expression, other.Expression, StringComparison.Ordinal);
            }

            public override bool Equals(object obj) => obj is FixedModification && Equals((FixedModification)obj);

            public override int GetHashCode() => (ExtraTexture, ExtraTextureUv, Target, Expression).GetHashCode();

            public static bool operator ==(FixedModification lhs, FixedModification rhs) => lhs.Equals(rhs);

            public static bool operator !=(FixedModification lhs, FixedModification rhs) => !(lhs == rhs);
        }
    }
}
