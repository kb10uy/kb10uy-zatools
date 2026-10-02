using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using KusakaFactory.Zatools.Foundation.Arithmetic;
using static KusakaFactory.Zatools.Tests.Foundation.Arithmetic.Zax;

namespace KusakaFactory.Zatools.Tests.Foundation.Arithmetic
{
    public sealed class ZaxEvaluateJobTests
    {
        private const int ElementCount = 257;
        private const float JobTolerance = 1e-4f;

        private static readonly ZaxVariable[] Variables =
        {
            new ZaxVariable("s", ZaxValueType.Float),
            new ZaxVariable("p", ZaxValueType.Float3),
            new ZaxVariable("c", ZaxValueType.Float4),
            new ZaxVariable("k", ZaxValueType.Int),
        };

        private bool _previousSynchronousCompilation;

        [OneTimeSetUp]
        public void EnableSynchronousBurstCompilation()
        {
            _previousSynchronousCompilation = BurstCompiler.Options.EnableBurstCompileSynchronously;
            BurstCompiler.Options.EnableBurstCompileSynchronously = true;
        }

        [OneTimeTearDown]
        public void RestoreBurstCompilation()
        {
            BurstCompiler.Options.EnableBurstCompileSynchronously = _previousSynchronousCompilation;
        }

        private static ZaxValue ScalarAt(int i) => F(i * 0.37f - 20.0f);
        private static ZaxValue PointAt(int i) => F3(math.sin(i), math.cos(i * 0.5f), i * 0.01f + 0.1f);
        private static ZaxValue ColorAt(int i) => F4((i % 7) / 7.0f, (i % 11) / 11.0f, (i % 13) / 13.0f, 1.0f);
        private static readonly ZaxValue UniformInt = I(3);

        private static ZaxValue VariableAt(string name, int i)
        {
            switch (name)
            {
                case "s": return ScalarAt(i);
                case "p": return PointAt(i);
                case "c": return ColorAt(i);
                default: return UniformInt;
            }
        }

        private static IEnumerable<string> Expressions()
        {
            yield return "@s";
            yield return "@k 2 * 1 +";
            yield return "@k 2 // @k 2 % +";
            yield return "@p @s *";
            yield return "@s sin @s cos * @s 0.1 * exp +";
            yield return "@s abs sqrt @s abs 1 + log +";
            yield return "@s 0 >= @s floor @s frac lerp";
            yield return "@p normalize @p length *";
            yield return "@p X_AXIS cross @p dot";
            yield return "@p #zyx @p #xxy -";
            yield return "@p ... * +";
            yield return "@c #rgb rgb2yuv yuv2rgb";
            yield return "@c srgb2linear linear2srgb";
            yield return "@s 0.01 * Mrotz @p Mmulv";
            yield return "@p Mtranslate @s 0.1 * Mrotx Mto4 M* @p Mtfpoint";
            yield return "@p normalize 1 Mrotaxis Mdup Minverse Mmul Mtrace";
            yield return "@p Mdiag @p #yzx Mdiag M* Mtranspose Mdet";
            yield return "Midentity2 @p Mdiag Mswap Mdrop Mto2 Mdiagv";
            yield return "Z_AXIS @s Qaxisangle @p Qrotate";
            yield return "@p Qeuler @p Y_AXIS cross normalize Y_AXIS Qlook Qmul";
            yield return "Q_IDENTITY @p normalize 2 Qaxisangle @s 0.01 * saturate Qslerp";
            yield return "@p @s @k";
            yield return "@p ... rot swap over";
        }

        private static ZaxValue[] EvaluateManaged(ZaxProgram program, int index)
        {
            var variables = program.Variables.Select((v) => VariableAt(v.Name, index)).ToArray();
            var results = new ZaxValue[program.ResultCount];
            ZaxEvaluator.Evaluate(program, variables, results);
            return results;
        }

        private static ZaxValue[] EvaluateJob(ZaxProgram program)
        {
            var scalars = new NativeArray<float>(ElementCount, Allocator.TempJob);
            var points = new NativeArray<float3>(ElementCount, Allocator.TempJob);
            var colors = new NativeArray<float4>(ElementCount, Allocator.TempJob);
            var bindings = new NativeArray<ZaxVariableBinding>(program.Variables.Length, Allocator.TempJob);
            var results = new NativeArray<ZaxValue>(ElementCount * program.ResultCount, Allocator.TempJob);
            var native = ZaxNativeProgram.Allocate(program, Allocator.TempJob);
            try
            {
                for (var i = 0; i < ElementCount; ++i)
                {
                    scalars[i] = ScalarAt(i).AsFloat;
                    points[i] = PointAt(i).AsFloat3;
                    colors[i] = ColorAt(i).AsFloat4;
                }

                for (var i = 0; i < program.Variables.Length; ++i)
                {
                    var variable = program.Variables[i];
                    switch (variable.Name)
                    {
                        case "s": bindings[i] = ZaxVariableBinding.FromArray(scalars, variable.Type); break;
                        case "p": bindings[i] = ZaxVariableBinding.FromArray(points, variable.Type); break;
                        case "c": bindings[i] = ZaxVariableBinding.FromArray(colors, variable.Type); break;
                        default: bindings[i] = ZaxVariableBinding.Uniform(UniformInt); break;
                    }
                }

                ZaxEvaluateJob.Schedule(native, bindings, results, 16).Complete();
                return results.ToArray();
            }
            finally
            {
                native.Dispose();
                results.Dispose();
                bindings.Dispose();
                colors.Dispose();
                points.Dispose();
                scalars.Dispose();
            }
        }

        [TestCaseSource(nameof(Expressions))]
        public void JobAgreesWithManagedEvaluator(string source)
        {
            Assume.That(BurstCompiler.IsEnabled, Is.True, "Burst is disabled; the job would run as managed code");

            var program = Compile(source, Variables);
            var jobResults = EvaluateJob(program);

            for (var i = 0; i < ElementCount; ++i)
            {
                var expected = EvaluateManaged(program, i);
                var actual = jobResults.Skip(i * program.ResultCount).Take(program.ResultCount).ToArray();
                AreEqual(expected, actual, JobTolerance);
            }
        }

        [Test]
        public void ReadsStridedBindings()
        {
            var program = Compile("@p", Variables);
            var source = new NativeArray<float4>(4, Allocator.TempJob);
            var bindings = new NativeArray<ZaxVariableBinding>(1, Allocator.TempJob);
            var results = new NativeArray<ZaxValue>(4, Allocator.TempJob);
            var native = ZaxNativeProgram.Allocate(program, Allocator.TempJob);
            try
            {
                for (var i = 0; i < 4; ++i) source[i] = new float4(i, i * 10, i * 100, -1);
                bindings[0] = ZaxVariableBinding.FromArray(source, ZaxValueType.Float3);
                ZaxEvaluateJob.Schedule(native, bindings, results).Complete();
                for (var i = 0; i < 4; ++i) AreEqual(F3(i, i * 10, i * 100), results[i]);
            }
            finally
            {
                native.Dispose();
                results.Dispose();
                bindings.Dispose();
                source.Dispose();
            }
        }

        [Test]
        public void RejectsBindingCountMismatch()
        {
            var program = Compile("@s @p *", Variables);
            using (var native = ZaxNativeProgram.Allocate(program, Allocator.Temp))
            using (var bindings = new NativeArray<ZaxVariableBinding>(1, Allocator.Temp))
            using (var results = new NativeArray<ZaxValue>(4, Allocator.Temp))
            {
                Assert.That(() => ZaxEvaluateJob.Create(native, bindings, results), Throws.ArgumentException);
            }
        }

        [Test]
        public void RejectsBindingTypeMismatch()
        {
            var program = Compile("@s", Variables);
            using (var native = ZaxNativeProgram.Allocate(program, Allocator.Temp))
            using (var bindings = new NativeArray<ZaxVariableBinding>(1, Allocator.Temp))
            using (var results = new NativeArray<ZaxValue>(4, Allocator.Temp))
            {
                var tmp = bindings;
                tmp[0] = ZaxVariableBinding.Uniform(I(1));
                Assert.That(() => ZaxEvaluateJob.Create(native, bindings, results), Throws.ArgumentException);
            }
        }

        [Test]
        public void RejectsShortBinding()
        {
            var program = Compile("@s", Variables);
            using (var native = ZaxNativeProgram.Allocate(program, Allocator.Temp))
            using (var source = new NativeArray<float>(3, Allocator.Temp))
            using (var bindings = new NativeArray<ZaxVariableBinding>(1, Allocator.Temp))
            using (var results = new NativeArray<ZaxValue>(4, Allocator.Temp))
            {
                var tmp = bindings;
                tmp[0] = ZaxVariableBinding.FromArray(source, ZaxValueType.Float);
                Assert.That(() => ZaxEvaluateJob.Create(native, bindings, results), Throws.ArgumentException);
            }
        }

        [Test]
        public void RejectsResultBufferNotMultipleOfResultCount()
        {
            var program = Compile("1 2");
            using (var native = ZaxNativeProgram.Allocate(program, Allocator.Temp))
            using (var bindings = new NativeArray<ZaxVariableBinding>(0, Allocator.Temp))
            using (var results = new NativeArray<ZaxValue>(5, Allocator.Temp))
            {
                Assert.That(() => ZaxEvaluateJob.Create(native, bindings, results), Throws.ArgumentException);
            }
        }

        [Test]
        public void RejectsBindingNarrowerThanType()
        {
            using (var source = new NativeArray<float2>(4, Allocator.Temp))
            {
                Assert.That(() => ZaxVariableBinding.FromArray(source, ZaxValueType.Float3), Throws.ArgumentException);
            }
        }

        [Test]
        public void RejectsProgramExceedingStackLimit()
        {
            var source = string.Join(" ", Enumerable.Repeat("1", ZaxNativeProgram.MaxStackSize + 1));
            var program = Compile(source);
            Assert.That(() => ZaxNativeProgram.Allocate(program, Allocator.Temp), Throws.ArgumentException);
        }

        [Test]
        public void RejectsProgramExceedingVariableLimit()
        {
            var variables = Enumerable.Range(0, ZaxNativeProgram.MaxVariableCount + 1)
                .Select((i) => new ZaxVariable($"v{i}", ZaxValueType.Float))
                .ToArray();
            var source = string.Join(" ", variables.Select((v) => $"@{v.Name}")) + string.Concat(Enumerable.Repeat(" +", variables.Length - 1));
            var program = Compile(source, variables);
            Assert.That(() => ZaxNativeProgram.Allocate(program, Allocator.Temp), Throws.ArgumentException);
        }
    }
}
