using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Mathematics;
using KusakaFactory.Zatools.Foundation.Arithmetic;

namespace KusakaFactory.Zatools.Tests.Foundation.Arithmetic
{
    internal static class Zax
    {
        public const float Tolerance = 1e-5f;

        public static ZaxProgram Compile(string source, params ZaxVariable[] variables)
        {
            var diagnostics = new List<ZaxDiagnostic>();
            var succeeded = ZaxCompiler.TryCompile(source, variables, (IReadOnlyList<ZaxValueType>)null, diagnostics, out var program);
            Assert.That(succeeded, Is.True, () => $"`{source}` failed to compile: {Describe(diagnostics)}");
            return program;
        }

        public static ZaxProgram Compile(string source, IReadOnlyList<ZaxValueType> expectedResultTypes, params ZaxVariable[] variables)
        {
            var diagnostics = new List<ZaxDiagnostic>();
            var succeeded = ZaxCompiler.TryCompile(source, variables, expectedResultTypes, diagnostics, out var program);
            Assert.That(succeeded, Is.True, () => $"`{source}` failed to compile: {Describe(diagnostics)}");
            return program;
        }

        public static ZaxDiagnostic CompileError(string source, params ZaxVariable[] variables)
        {
            return CompileError(source, null, variables);
        }

        public static ZaxDiagnostic CompileError(string source, IReadOnlyList<ZaxValueType> expectedResultTypes, params ZaxVariable[] variables)
        {
            var diagnostics = new List<ZaxDiagnostic>();
            var succeeded = ZaxCompiler.TryCompile(source, variables, expectedResultTypes, diagnostics, out var program);
            Assert.That(succeeded, Is.False, () => $"`{source}` compiled unexpectedly:\n{program.Disassemble()}");
            Assert.That(program, Is.Null);
            Assert.That(diagnostics, Is.Not.Empty);
            return diagnostics[0];
        }

        public static ZaxValue Evaluate(string source)
        {
            return ZaxEvaluator.Evaluate(Compile(source));
        }

        public static ZaxValue[] EvaluateAll(string source)
        {
            var program = Compile(source);
            var results = new ZaxValue[program.ResultCount];
            ZaxEvaluator.Evaluate(program, ReadOnlySpan<ZaxValue>.Empty, results);
            return results;
        }

        public static void AreEqual(ZaxValue expected, ZaxValue actual, float tolerance = Tolerance)
        {
            Assert.That(actual.Type, Is.EqualTo(expected.Type), () => $"expected {expected} but was {actual}");
            if (expected.Type == ZaxValueType.Int)
            {
                Assert.That(actual.AsInt, Is.EqualTo(expected.AsInt));
                return;
            }

            var e = expected.ToFloat4();
            var a = actual.ToFloat4();
            for (var i = 0; i < expected.Type.Dimension(); ++i)
            {
                Assert.That(a[i], Is.EqualTo(e[i]).Within(tolerance * math.max(1.0f, math.abs(e[i]))), () => $"component {i}: expected {expected} but was {actual}");
            }
        }

        public static void AreEqual(float expected, ZaxValue actual, float tolerance = Tolerance) => AreEqual(ZaxValue.FromFloat(expected), actual, tolerance);
        public static void AreEqual(float2 expected, ZaxValue actual, float tolerance = Tolerance) => AreEqual(ZaxValue.FromFloat2(expected), actual, tolerance);
        public static void AreEqual(float3 expected, ZaxValue actual, float tolerance = Tolerance) => AreEqual(ZaxValue.FromFloat3(expected), actual, tolerance);
        public static void AreEqual(float4 expected, ZaxValue actual, float tolerance = Tolerance) => AreEqual(ZaxValue.FromFloat4(expected), actual, tolerance);

        public static void AreEqual(IReadOnlyList<ZaxValue> expected, IReadOnlyList<ZaxValue> actual, float tolerance = Tolerance)
        {
            Assert.That(actual.Count, Is.EqualTo(expected.Count), () => $"expected [{string.Join(", ", expected)}] but was [{string.Join(", ", actual)}]");
            for (var i = 0; i < expected.Count; ++i) AreEqual(expected[i], actual[i], tolerance);
        }

        public static ZaxValue I(int value) => ZaxValue.FromInt(value);
        public static ZaxValue F(float value) => ZaxValue.FromFloat(value);
        public static ZaxValue F2(float x, float y) => ZaxValue.FromFloat2(new float2(x, y));
        public static ZaxValue F3(float x, float y, float z) => ZaxValue.FromFloat3(new float3(x, y, z));
        public static ZaxValue F4(float x, float y, float z, float w) => ZaxValue.FromFloat4(new float4(x, y, z, w));

        private static string Describe(IEnumerable<ZaxDiagnostic> diagnostics)
        {
            return string.Join(", ", diagnostics.Select((d) => $"{d.Code}@{d.Offset}[{string.Join(", ", d.Arguments)}]"));
        }
    }
}
