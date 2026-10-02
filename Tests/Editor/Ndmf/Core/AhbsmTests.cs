using System;
using System.Collections.Immutable;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KusakaFactory.Zatools.Ndmf.Core;

namespace KusakaFactory.Zatools.Tests.Ndmf.Core
{
    public sealed class AhbsmTests : MeshTestBase
    {
        private Mesh CreateShapedMesh()
        {
            var mesh = CreateQuadStrip(1);
            AddShape(mesh, "base", (i) => new Vector3(0, 0, 1), (i) => new Vector3(1, 0, 0), (i) => Vector3.zero, 100.0f);
            AddShape(mesh, "a", (i) => new Vector3(i, 0, 0), (i) => new Vector3(0, 1, 0), (i) => new Vector3(0, 0, 1), 100.0f);
            AddShape(mesh, "b", (i) => new Vector3(0, i, 0), (i) => Vector3.zero, (i) => new Vector3(1, 0, 0), 100.0f);
            AddShape(mesh, "target", (i) => new Vector3(0, 0, -1), (i) => Vector3.zero, (i) => Vector3.zero, 50.0f);
            return mesh;
        }

        private static ImmutableArray<(string SourceName, string TargetName, float Weight)> Definitions(params (string, string, float)[] definitions)
        {
            return definitions.ToImmutableArray();
        }

        [Test]
        public void FetchesBlendShapeIndicesByName()
        {
            var indices = Ahbsm.FetchBlendShapeIndices(CreateShapedMesh());
            Assert.That(indices["base"], Is.EqualTo(0));
            Assert.That(indices["target"], Is.EqualTo(3));
            Assert.That(indices, Has.Count.EqualTo(4));
        }

        [Test]
        public void AggregatesBySourceAndTargetIgnoringUnknownNames()
        {
            var mesh = CreateShapedMesh();
            var aggregated = Ahbsm.AggregateDefinitions(
                Definitions(
                    ("a", "target", 0.5f),
                    ("b", "target", 1.0f),
                    ("a", "target", 0.25f),
                    ("missing", "target", 1.0f),
                    ("a", "missing", 1.0f),
                    ("b", "base", -1.0f)),
                Ahbsm.FetchBlendShapeIndices(mesh));

            Assert.That(aggregated.Select((d) => d.TargetName), Is.EqualTo(new[] { "base", "target" }));
            Assert.That(aggregated.Select((d) => d.TargetIndex), Is.EqualTo(new[] { 0, 3 }));
            Assert.That(aggregated[0].Sources, Is.EqualTo(new[] { (2, "b", -1.0f) }));
            Assert.That(aggregated[1].Sources, Is.EqualTo(new[] { (1, "a", 0.75f), (2, "b", 1.0f) }));
        }

        [Test]
        public void OverwriteAddsSourcesIntoTargetsInPlace()
        {
            var mesh = CreateShapedMesh();
            var aggregated = Ahbsm.AggregateDefinitions(
                Definitions(("a", "target", 2.0f), ("b", "target", 1.0f), ("a", "base", 1.0f)),
                Ahbsm.FetchBlendShapeIndices(mesh));
            var modified = Track(Ahbsm.ProcessOverwrite(mesh, aggregated));

            Assert.That(ShapeNames(modified), Is.EqualTo(ShapeNames(mesh)));
            Assert.That(modified.GetBlendShapeFrameWeight(3, 0), Is.EqualTo(50.0f));

            var target = ShapeDeltas(modified, "target");
            AssertVectors(Enumerable.Range(0, 4).Select((i) => new Vector3(2 * i, i, -1)).ToArray(), target.Vertices);
            AssertVectors(Enumerable.Range(0, 4).Select((_) => new Vector3(0, 2, 0)).ToArray(), target.Normals);
            AssertVectors(Enumerable.Range(0, 4).Select((_) => new Vector3(1, 0, 2)).ToArray(), target.Tangents);

            var baseShape = ShapeDeltas(modified, "base");
            AssertVectors(Enumerable.Range(0, 4).Select((i) => new Vector3(i, 0, 1)).ToArray(), baseShape.Vertices);
            AssertVectors(Enumerable.Range(0, 4).Select((_) => new Vector3(1, 1, 0)).ToArray(), baseShape.Normals);

            AssertVectors(ShapeDeltas(mesh, "a").Vertices, ShapeDeltas(modified, "a").Vertices);
            AssertVectors(ShapeDeltas(mesh, "b").Vertices, ShapeDeltas(modified, "b").Vertices);
        }

        [Test]
        public void OverwriteDoesNotChainMixedTargets()
        {
            var mesh = CreateShapedMesh();
            var aggregated = Ahbsm.AggregateDefinitions(
                Definitions(("a", "b", 1.0f), ("b", "target", 1.0f)),
                Ahbsm.FetchBlendShapeIndices(mesh));
            var modified = Track(Ahbsm.ProcessOverwrite(mesh, aggregated));

            AssertVectors(Enumerable.Range(0, 4).Select((i) => new Vector3(0, i, -1)).ToArray(), ShapeDeltas(modified, "target").Vertices);
        }

        [Test]
        public void AppendKeepsOriginalsAndAddsMixedCopies()
        {
            var mesh = CreateShapedMesh();
            var aggregated = Ahbsm.AggregateDefinitions(
                Definitions(("a", "target", 2.0f), ("b", "target", 1.0f), ("a", "base", 1.0f)),
                Ahbsm.FetchBlendShapeIndices(mesh));
            var modified = Track(Ahbsm.ProcessAppend(mesh, aggregated));

            Assert.That(ShapeNames(modified), Is.EqualTo(new[] { "base", "a", "b", "target", "base_[a]", "target_[a,b]" }));
            for (var bs = 0; bs < mesh.blendShapeCount; ++bs)
            {
                AssertVectors(ShapeDeltas(mesh, mesh.GetBlendShapeName(bs)).Vertices, ShapeDeltas(modified, mesh.GetBlendShapeName(bs)).Vertices);
            }

            Assert.That(modified.GetBlendShapeFrameWeight(5, 0), Is.EqualTo(50.0f));
            AssertVectors(Enumerable.Range(0, 4).Select((i) => new Vector3(2 * i, i, -1)).ToArray(), ShapeDeltas(modified, "target_[a,b]").Vertices);
            AssertVectors(Enumerable.Range(0, 4).Select((i) => new Vector3(i, 0, 1)).ToArray(), ShapeDeltas(modified, "base_[a]").Vertices);
        }

        [Test]
        public void OverwriteRejectsMultiFrameBlendShapes()
        {
            var mesh = CreateShapedMesh();
            AddShape(mesh, "multi", (i) => Vector3.one, 50.0f);
            AddShape(mesh, "multi", (i) => Vector3.one * 2, 100.0f);
            var aggregated = Ahbsm.AggregateDefinitions(Definitions(("a", "target", 1.0f)), Ahbsm.FetchBlendShapeIndices(mesh));

            var modifying = Track(UnityEngine.Object.Instantiate(mesh));
            Assert.That(() => Ahbsm.ProcessOverwrite(mesh, modifying, aggregated), Throws.Exception);
        }
    }
}
