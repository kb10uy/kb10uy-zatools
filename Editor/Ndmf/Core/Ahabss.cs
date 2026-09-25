using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using KusakaFactory.Zatools.Foundation.Arithmetic;
using KusakaFactory.Zatools.Runtime;

namespace KusakaFactory.Zatools.Ndmf.Core
{
    internal static class Ahabss
    {
        private const float SynthesizedFrameWeight = 100.0f;

        internal static readonly ImmutableArray<ZaxVariable> BaseVariables = ImmutableArray.Create(
            new ZaxVariable("position", ZaxValueType.Float3),
            new ZaxVariable("normal", ZaxValueType.Float3),
            new ZaxVariable("tangent", ZaxValueType.Float4)
        );

        internal static readonly ImmutableArray<ZaxValueType> ResultTypes = ImmutableArray.Create(
            ZaxValueType.Float3,
            ZaxValueType.Float3,
            ZaxValueType.Float3
        );

        internal delegate bool ZaxExpressionCompiler(
            string source,
            IReadOnlyList<ZaxVariable> variables,
            IReadOnlyList<ZaxValueType> expectedResultTypes,
            out ZaxProgram program);

        internal static ImmutableArray<ZaxVariable> VariablesFor(int sourceCount)
        {
            var builder = ImmutableArray.CreateBuilder<ZaxVariable>(BaseVariables.Length + sourceCount * 3);
            builder.AddRange(BaseVariables);
            for (var i = 0; i < sourceCount; ++i)
            {
                builder.Add(new ZaxVariable($"v{i}", ZaxValueType.Float3));
                builder.Add(new ZaxVariable($"n{i}", ZaxValueType.Float3));
                builder.Add(new ZaxVariable($"t{i}", ZaxValueType.Float3));
            }
            return builder.MoveToImmutable();
        }

        internal static bool TryCompilePrograms(FixedParameters parameters, ZaxExpressionCompiler compiler, out CompiledPrograms programs)
        {
            programs = null;
            var variables = VariablesFor(parameters.SourceBlendShapes.Length);

            var entries = ImmutableArray.CreateBuilder<CompiledEntry>(parameters.Entries.Length);
            foreach (var entry in parameters.Entries)
            {
                if (!compiler(entry.Expression, variables, ResultTypes, out var program)) return false;
                entries.Add(new CompiledEntry { Entry = entry, Program = program });
            }

            programs = new CompiledPrograms { Entries = entries.MoveToImmutable() };
            return true;
        }

        internal static bool TryCompileSilently(
            string source,
            IReadOnlyList<ZaxVariable> variables,
            IReadOnlyList<ZaxValueType> expectedResultTypes,
            out ZaxProgram program)
        {
            return ZaxCompiler.TryCompile(source, variables, expectedResultTypes, new List<ZaxDiagnostic>(), out program);
        }

        internal static ProcessResult Process(Mesh originalMesh, Mesh modifyingMesh, FixedParameters parameters, CompiledPrograms programs)
        {
            if (originalMesh == null || modifyingMesh == null || programs == null) return ProcessResult.Empty;
            if (originalMesh.vertexCount != modifyingMesh.vertexCount) throw new ArgumentException("different mesh vertex count");
            if (programs.Entries.IsEmpty) return ProcessResult.Empty;

            var vertexCount = originalMesh.vertexCount;
            var added = ImmutableArray.CreateBuilder<FixedEntry>();
            var skipped = ImmutableArray.CreateBuilder<FixedEntry>();
            var usedNames = new HashSet<string>(Enumerable.Range(0, modifyingMesh.blendShapeCount).Select(modifyingMesh.GetBlendShapeName));
            using (var inputs = new VertexInputs(originalMesh, parameters.SourceBlendShapes))
            {
                foreach (var compiled in programs.Entries)
                {
                    var name = compiled.Entry.Name;
                    if (string.IsNullOrEmpty(name)) continue;
                    if (!usedNames.Add(name))
                    {
                        skipped.Add(compiled.Entry);
                        continue;
                    }

                    var shape = Synthesize(inputs, compiled.Program, vertexCount);
                    modifyingMesh.AddBlendShapeFrame(name, SynthesizedFrameWeight, shape.DeltaVertices, shape.DeltaNormals, shape.DeltaTangents);
                    added.Add(compiled.Entry);
                }
            }

            return new ProcessResult
            {
                Added = added.ToImmutable(),
                Skipped = skipped.ToImmutable(),
            };
        }

        internal static ImmutableArray<(int Index, float Weight)> ResolveWeights(Mesh mesh, IEnumerable<FixedEntry> addedEntries)
        {
            var builder = ImmutableArray.CreateBuilder<(int Index, float Weight)>();
            foreach (var entry in addedEntries)
            {
                var index = mesh.GetBlendShapeIndex(entry.Name);
                if (index < 0) continue;
                builder.Add((index, entry.Value));
            }
            return builder.ToImmutable();
        }

        private static SynthesizedShape Synthesize(VertexInputs inputs, ZaxProgram program, int vertexCount)
        {
            var shape = new SynthesizedShape(vertexCount);
            if (program.Variables.Length == 0)
            {
                Span<ZaxValue> uniform = stackalloc ZaxValue[ResultTypes.Length];
                ZaxEvaluator.Evaluate(program, ReadOnlySpan<ZaxValue>.Empty, uniform);
                shape.Fill(uniform[0].AsFloat3, uniform[1].AsFloat3, uniform[2].AsFloat3);
                return shape;
            }

            var nativeProgram = ZaxNativeProgram.Allocate(program, Allocator.TempJob);
            var bindings = new NativeArray<ZaxVariableBinding>(program.Variables.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var results = new NativeArray<ZaxValue>(vertexCount * ResultTypes.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            try
            {
                for (var i = 0; i < program.Variables.Length; ++i) bindings[i] = inputs.BindingOf(program.Variables[i].Name);
                ZaxEvaluateJob.Schedule(nativeProgram, bindings, results).Complete();

                for (var i = 0; i < vertexCount; ++i)
                {
                    var resultBase = i * ResultTypes.Length;
                    shape.DeltaVertices[i] = results[resultBase].AsFloat3;
                    shape.DeltaNormals[i] = results[resultBase + 1].AsFloat3;
                    shape.DeltaTangents[i] = results[resultBase + 2].AsFloat3;
                }
            }
            finally
            {
                nativeProgram.Dispose();
                bindings.Dispose();
                results.Dispose();
            }

            return shape;
        }

        internal sealed class CompiledPrograms
        {
            internal ImmutableArray<CompiledEntry> Entries;
        }

        internal struct ProcessResult
        {
            internal static readonly ProcessResult Empty = new ProcessResult
            {
                Added = ImmutableArray<FixedEntry>.Empty,
                Skipped = ImmutableArray<FixedEntry>.Empty,
            };

            internal ImmutableArray<FixedEntry> Added;
            internal ImmutableArray<FixedEntry> Skipped;
        }

        internal struct CompiledEntry
        {
            internal FixedEntry Entry;
            internal ZaxProgram Program;
        }

        private sealed class SynthesizedShape
        {
            internal readonly Vector3[] DeltaVertices;
            internal readonly Vector3[] DeltaNormals;
            internal readonly Vector3[] DeltaTangents;

            internal SynthesizedShape(int vertexCount)
            {
                DeltaVertices = new Vector3[vertexCount];
                DeltaNormals = new Vector3[vertexCount];
                DeltaTangents = new Vector3[vertexCount];
            }

            internal void Fill(Vector3 deltaVertex, Vector3 deltaNormal, Vector3 deltaTangent)
            {
                Array.Fill(DeltaVertices, deltaVertex);
                Array.Fill(DeltaNormals, deltaNormal);
                Array.Fill(DeltaTangents, deltaTangent);
            }
        }

        private sealed class VertexInputs : IDisposable
        {
            private readonly Mesh _mesh;
            private readonly ImmutableArray<string> _sourceNames;
            private readonly int _vertexCount;
            private NativeArray<float3> _positions;
            private NativeArray<float3> _normals;
            private NativeArray<float4> _tangents;
            private readonly NativeArray<float3>[] _deltaVertices;
            private readonly NativeArray<float3>[] _deltaNormals;
            private readonly NativeArray<float3>[] _deltaTangents;

            internal VertexInputs(Mesh mesh, ImmutableArray<string> sourceNames)
            {
                _mesh = mesh;
                _sourceNames = sourceNames;
                _vertexCount = mesh.vertexCount;
                _deltaVertices = new NativeArray<float3>[sourceNames.Length];
                _deltaNormals = new NativeArray<float3>[sourceNames.Length];
                _deltaTangents = new NativeArray<float3>[sourceNames.Length];
            }

            internal ZaxVariableBinding BindingOf(string name)
            {
                switch (name)
                {
                    case "position":
                        return ZaxVariableBinding.FromArray(Positions(), ZaxValueType.Float3);
                    case "normal":
                        return ZaxVariableBinding.FromArray(Normals(), ZaxValueType.Float3);
                    case "tangent":
                        return ZaxVariableBinding.FromArray(Tangents(), ZaxValueType.Float4);
                    default:
                        break;
                }

                if (TryParseSourceName(name, out var kind, out var index))
                {
                    EnsureSource(index);
                    switch (kind)
                    {
                        case 'v': return ZaxVariableBinding.FromArray(_deltaVertices[index], ZaxValueType.Float3);
                        case 'n': return ZaxVariableBinding.FromArray(_deltaNormals[index], ZaxValueType.Float3);
                        case 't': return ZaxVariableBinding.FromArray(_deltaTangents[index], ZaxValueType.Float3);
                        default: break;
                    }
                }
                throw new InvalidOperationException("unbindable variable: " + name);
            }

            public void Dispose()
            {
                if (_positions.IsCreated) _positions.Dispose();
                if (_normals.IsCreated) _normals.Dispose();
                if (_tangents.IsCreated) _tangents.Dispose();
                for (var i = 0; i < _sourceNames.Length; ++i)
                {
                    if (_deltaVertices[i].IsCreated) _deltaVertices[i].Dispose();
                    if (_deltaNormals[i].IsCreated) _deltaNormals[i].Dispose();
                    if (_deltaTangents[i].IsCreated) _deltaTangents[i].Dispose();
                }
            }

            private bool TryParseSourceName(string name, out char kind, out int index)
            {
                kind = default;
                index = -1;
                if (name.Length < 2) return false;
                if (name[0] != 'v' && name[0] != 'n' && name[0] != 't') return false;
                if (!int.TryParse(name.Substring(1), out var parsed)) return false;
                if (parsed < 0 || parsed >= _sourceNames.Length) return false;

                kind = name[0];
                index = parsed;
                return true;
            }

            private NativeArray<float3> Positions()
            {
                if (_positions.IsCreated) return _positions;

                var values = new List<Vector3>(_vertexCount);
                _mesh.GetVertices(values);
                _positions = new NativeArray<float3>(_vertexCount, Allocator.Persistent);
                for (var i = 0; i < _vertexCount; ++i) _positions[i] = values[i];
                return _positions;
            }

            private NativeArray<float3> Normals()
            {
                if (_normals.IsCreated) return _normals;

                var values = new List<Vector3>(_vertexCount);
                _mesh.GetNormals(values);
                _normals = new NativeArray<float3>(_vertexCount, Allocator.Persistent);
                if (values.Count == _vertexCount)
                {
                    for (var i = 0; i < _vertexCount; ++i) _normals[i] = values[i];
                }
                return _normals;
            }

            private NativeArray<float4> Tangents()
            {
                if (_tangents.IsCreated) return _tangents;

                var values = new List<Vector4>(_vertexCount);
                _mesh.GetTangents(values);
                _tangents = new NativeArray<float4>(_vertexCount, Allocator.Persistent);
                if (values.Count == _vertexCount)
                {
                    for (var i = 0; i < _vertexCount; ++i) _tangents[i] = values[i];
                }
                return _tangents;
            }

            private void EnsureSource(int index)
            {
                if (_deltaVertices[index].IsCreated) return;

                _deltaVertices[index] = new NativeArray<float3>(_vertexCount, Allocator.Persistent);
                _deltaNormals[index] = new NativeArray<float3>(_vertexCount, Allocator.Persistent);
                _deltaTangents[index] = new NativeArray<float3>(_vertexCount, Allocator.Persistent);

                var shapeIndex = _mesh.GetBlendShapeIndex(_sourceNames[index]);
                if (shapeIndex < 0) return;

                var frameCount = _mesh.GetBlendShapeFrameCount(shapeIndex);
                if (frameCount == 0) return;

                var deltaVertices = new Vector3[_vertexCount];
                var deltaNormals = new Vector3[_vertexCount];
                var deltaTangents = new Vector3[_vertexCount];
                _mesh.GetBlendShapeFrameVertices(shapeIndex, frameCount - 1, deltaVertices, deltaNormals, deltaTangents);
                for (var i = 0; i < _vertexCount; ++i)
                {
                    _deltaVertices[index][i] = deltaVertices[i];
                    _deltaNormals[index][i] = deltaNormals[i];
                    _deltaTangents[index][i] = deltaTangents[i];
                }
            }
        }

        internal struct FixedParameters : IEquatable<FixedParameters>
        {
            internal ImmutableArray<string> SourceBlendShapes;
            internal ImmutableArray<FixedEntry> Entries;

            internal static FixedParameters FixFromComponent(AdHocAdvancedBlendShapeSynthesis component)
            {
                return new FixedParameters()
                {
                    SourceBlendShapes = component.SourceBlendShapes
                        .Select((n) => n ?? string.Empty)
                        .ToImmutableArray(),
                    Entries = component.Entries
                        .Where((e) => e != null)
                        .Select(FixedEntry.FixFromEntry)
                        .ToImmutableArray(),
                };
            }

            public bool Equals(FixedParameters other)
            {
                return SourceBlendShapes.SequenceEqual(other.SourceBlendShapes, StringComparer.Ordinal)
                    && Entries.SequenceEqual(other.Entries);
            }

            public override bool Equals(object obj) => obj is FixedParameters && Equals((FixedParameters)obj);

            public override int GetHashCode() => (SourceBlendShapes.Length, Entries.Length).GetHashCode();

            public static bool operator ==(FixedParameters lhs, FixedParameters rhs) => lhs.Equals(rhs);

            public static bool operator !=(FixedParameters lhs, FixedParameters rhs) => !(lhs == rhs);
        }

        internal struct FixedEntry : IEquatable<FixedEntry>
        {
            internal string Name;
            internal float Value;
            internal string Expression;

            internal static FixedEntry FixFromEntry(AhabssEntry entry)
            {
                return new FixedEntry()
                {
                    Name = entry.Name,
                    Value = entry.Value,
                    Expression = entry.Expression,
                };
            }

            public bool Equals(FixedEntry other)
            {
                return string.Equals(Name, other.Name, StringComparison.Ordinal)
                    && Mathf.Approximately(Value, other.Value)
                    && string.Equals(Expression, other.Expression, StringComparison.Ordinal);
            }

            public override bool Equals(object obj) => obj is FixedEntry && Equals((FixedEntry)obj);

            public override int GetHashCode() => (Name, Expression).GetHashCode();

            public static bool operator ==(FixedEntry lhs, FixedEntry rhs) => lhs.Equals(rhs);

            public static bool operator !=(FixedEntry lhs, FixedEntry rhs) => !(lhs == rhs);
        }
    }
}
