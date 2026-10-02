using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KusakaFactory.Zatools.Foundation.Arithmetic;
using KusakaFactory.Zatools.Ndmf.Core;

namespace KusakaFactory.Zatools.Tests.Ndmf.Core
{
    public sealed class AhabssTests : MeshTestBase
    {
        private const int VertexCount = 4;

        private static Ahabss.FixedEntry Entry(string name, string expression, float value = 0.0f)
        {
            return new Ahabss.FixedEntry { Name = name, Expression = expression, Value = value };
        }

        private static Ahabss.FixedParameters Parameters(string[] sources, params Ahabss.FixedEntry[] entries)
        {
            return new Ahabss.FixedParameters { SourceBlendShapes = sources.ToImmutableArray(), Entries = entries.ToImmutableArray() };
        }

        private static Ahabss.CompiledPrograms Compile(Ahabss.FixedParameters parameters)
        {
            Assert.That(Ahabss.TryCompilePrograms(parameters, Ahabss.TryCompileSilently, out var programs), Is.True);
            return programs;
        }

        private (Mesh Original, Mesh Modifying) CreateMeshes()
        {
            var original = CreateQuadStrip(1);
            AddShape(original, "a", (i) => new Vector3(i, 0, 0), (_) => new Vector3(0, 1, 0), (_) => new Vector3(0, 0, 1));
            AddShape(original, "b", (i) => new Vector3(0, 0, i));
            AddShape(original, "multi", (_) => new Vector3(1, 0, 0), 50.0f);
            AddShape(original, "multi", (_) => new Vector3(3, 0, 0), 100.0f);
            return (original, Track(Object.Instantiate(original)));
        }

        private static Vector3[] Each(System.Func<int, Vector3> value) => Enumerable.Range(0, VertexCount).Select(value).ToArray();

        [Test]
        public void DeclaresBaseAndPerSourceVariables()
        {
            var names = Ahabss.VariablesFor(2).Select((v) => v.Name);
            Assert.That(names, Is.EqualTo(new[] { "position", "normal", "tangent", "v0", "n0", "t0", "v1", "n1", "t1" }));
            Assert.That(Ahabss.VariablesFor(0).Select((v) => v.Name), Is.EqualTo(new[] { "position", "normal", "tangent" }));
        }

        [Test]
        public void CompilesEveryEntryAgainstThreeFloat3Results()
        {
            var requested = new List<(int VariableCount, IReadOnlyList<ZaxValueType> ResultTypes)>();
            bool Recording(string source, IReadOnlyList<ZaxVariable> variables, IReadOnlyList<ZaxValueType> resultTypes, out ZaxProgram program)
            {
                requested.Add((variables.Count, resultTypes));
                return Ahabss.TryCompileSilently(source, variables, resultTypes, out program);
            }

            var parameters = Parameters(new[] { "a" }, Entry("x", "@v0 @n0 @t0"), Entry("y", "@position @normal @tangent #xyz"));
            Assert.That(Ahabss.TryCompilePrograms(parameters, Recording, out var programs), Is.True);
            Assert.That(programs.Entries.Select((e) => e.Entry.Name), Is.EqualTo(new[] { "x", "y" }));
            Assert.That(requested.Select((r) => r.VariableCount), Is.EqualTo(new[] { 6, 6 }));
            Assert.That(requested[0].ResultTypes, Is.EqualTo(new[] { ZaxValueType.Float3, ZaxValueType.Float3, ZaxValueType.Float3 }));
        }

        [TestCase("@v0 @n0")]
        [TestCase("@v1 @n0 @t0")]
        [TestCase("@tangent @n0 @t0")]
        public void FailsWhenAnyEntryDoesNotCompile(string expression)
        {
            var parameters = Parameters(new[] { "a" }, Entry("ok", "@v0 @n0 @t0"), Entry("bad", expression));
            Assert.That(Ahabss.TryCompilePrograms(parameters, Ahabss.TryCompileSilently, out var programs), Is.False);
            Assert.That(programs, Is.Null);
        }

        [Test]
        public void SynthesizesShapesFromSourceDeltas()
        {
            var (original, modifying) = CreateMeshes();
            var parameters = Parameters(new[] { "a", "b" }, Entry("mix", "@v0 2 * @v1 + @n0 @t0 @t1 +"));
            var result = Ahabss.Process(original, modifying, parameters, Compile(parameters));

            Assert.That(result.Added.Select((e) => e.Name), Is.EqualTo(new[] { "mix" }));
            Assert.That(result.Skipped, Is.Empty);
            Assert.That(ShapeNames(modifying), Is.EqualTo(new[] { "a", "b", "multi", "mix" }));
            Assert.That(modifying.GetBlendShapeFrameWeight(3, 0), Is.EqualTo(100.0f));

            var mix = ShapeDeltas(modifying, "mix");
            AssertVectors(Each((i) => new Vector3(2 * i, 0, i)), mix.Vertices);
            AssertVectors(Each((_) => new Vector3(0, 1, 0)), mix.Normals);
            AssertVectors(Each((_) => new Vector3(0, 0, 1)), mix.Tangents);
        }

        [Test]
        public void SynthesizesShapesFromVertexAttributes()
        {
            var (original, modifying) = CreateMeshes();
            var parameters = Parameters(new string[0], Entry("attributes", "@position @normal -1 * @tangent #xyz"));
            Ahabss.Process(original, modifying, parameters, Compile(parameters));

            var shape = ShapeDeltas(modifying, "attributes");
            AssertVectors(original.vertices, shape.Vertices);
            AssertVectors(Each((_) => new Vector3(0, 0, 1)), shape.Normals);
            AssertVectors(Each((_) => new Vector3(1, 0, 0)), shape.Tangents);
        }

        [Test]
        public void FillsConstantExpressionsUniformly()
        {
            var (original, modifying) = CreateMeshes();
            var parameters = Parameters(new string[0], Entry("constant", "Y_AXIS X_AXIS 0 0 0 vec3"));
            Ahabss.Process(original, modifying, parameters, Compile(parameters));

            var shape = ShapeDeltas(modifying, "constant");
            AssertVectors(Each((_) => Vector3.up), shape.Vertices);
            AssertVectors(Each((_) => Vector3.right), shape.Normals);
            AssertVectors(Each((_) => Vector3.zero), shape.Tangents);
        }

        [Test]
        public void ReadsLastFrameOfMultiFrameSourcesAndZeroForMissingSources()
        {
            var (original, modifying) = CreateMeshes();
            var parameters = Parameters(new[] { "multi", "missing" }, Entry("x", "@v0 @v1 @n1"));
            Ahabss.Process(original, modifying, parameters, Compile(parameters));

            var shape = ShapeDeltas(modifying, "x");
            AssertVectors(Each((_) => new Vector3(3, 0, 0)), shape.Vertices);
            AssertVectors(Each((_) => Vector3.zero), shape.Normals);
            AssertVectors(Each((_) => Vector3.zero), shape.Tangents);
        }

        [Test]
        public void SkipsDuplicateNamesAndIgnoresEmptyNames()
        {
            var (original, modifying) = CreateMeshes();
            var parameters = Parameters(
                new[] { "a" },
                Entry("a", "@v0 @n0 @t0"),
                Entry("new", "@v0 @n0 @t0", 30.0f),
                Entry("", "@v0 @n0 @t0"),
                Entry("new", "@v0 @n0 @t0", 60.0f));
            var result = Ahabss.Process(original, modifying, parameters, Compile(parameters));

            Assert.That(result.Added.Select((e) => (e.Name, e.Value)), Is.EqualTo(new[] { ("new", 30.0f) }));
            Assert.That(result.Skipped.Select((e) => (e.Name, e.Value)), Is.EqualTo(new[] { ("a", 0.0f), ("new", 60.0f) }));
            Assert.That(ShapeNames(modifying), Is.EqualTo(new[] { "a", "b", "multi", "new" }));
            AssertVectors(ShapeDeltas(original, "a").Vertices, ShapeDeltas(modifying, "a").Vertices);
        }

        [Test]
        public void ResolvesWeightsOfAddedEntries()
        {
            var (original, modifying) = CreateMeshes();
            var parameters = Parameters(new[] { "a" }, Entry("first", "@v0 @n0 @t0", 25.0f), Entry("second", "@v0 @n0 @t0", 75.0f));
            var result = Ahabss.Process(original, modifying, parameters, Compile(parameters));

            var weights = Ahabss.ResolveWeights(modifying, result.Added.Add(Entry("absent", "", 1.0f)));
            Assert.That(weights, Is.EqualTo(new[] { (3, 25.0f), (4, 75.0f) }));
        }

        [Test]
        public void ReturnsEmptyForMissingInputs()
        {
            var (original, modifying) = CreateMeshes();
            var parameters = Parameters(new[] { "a" });
            var programs = Compile(parameters);

            Assert.That(Ahabss.Process(original, modifying, parameters, null).Added, Is.Empty);
            Assert.That(Ahabss.Process(null, modifying, parameters, programs).Added, Is.Empty);
            Assert.That(Ahabss.Process(original, modifying, parameters, programs).Added, Is.Empty);
            Assert.That(modifying.blendShapeCount, Is.EqualTo(original.blendShapeCount));
        }

        [Test]
        public void RejectsVertexCountMismatch()
        {
            var (original, _) = CreateMeshes();
            var parameters = Parameters(new[] { "a" }, Entry("x", "@v0 @n0 @t0"));
            Assert.That(() => Ahabss.Process(original, CreateQuadStrip(2), parameters, Compile(parameters)), Throws.ArgumentException);
        }
    }
}
