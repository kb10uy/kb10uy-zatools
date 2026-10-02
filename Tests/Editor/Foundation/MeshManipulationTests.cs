using System;
using System.Collections.Immutable;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KusakaFactory.Zatools.Foundation;

namespace KusakaFactory.Zatools.Tests.Foundation
{
    public sealed class MeshManipulationTests : MeshTestBase
    {
        private static readonly ImmutableArray<(string Name, float Value)> NoOverrides = ImmutableArray<(string, float)>.Empty;

        private (Mesh Mesh, SkinnedMeshRenderer Renderer) CreateShapedStrip()
        {
            var mesh = CreateQuadStrip(1);
            AddShape(mesh, "up", (_) => new Vector3(0, 1, 0), 100.0f);
            AddShape(mesh, "right", (i) => new Vector3(i, 0, 0), 50.0f);
            AddShape(mesh, "multi", (_) => new Vector3(0, 0, 1), 50.0f);
            AddShape(mesh, "multi", (_) => new Vector3(0, 0, 2), 100.0f);
            return (mesh, CreateRenderer(mesh));
        }

        private static Vector3[] Expected(Mesh mesh, Func<int, Vector3> offset)
        {
            var vertices = mesh.vertices;
            return Enumerable.Range(0, vertices.Length).Select((i) => vertices[i] + offset(i)).ToArray();
        }

        [Test]
        public void ReturnsBaseVerticesWithoutWeights()
        {
            var (mesh, renderer) = CreateShapedStrip();
            AssertVectors(mesh.vertices, MeshManipulation.ComputeBlendShapeAppliedVertices(mesh, renderer, NoOverrides));
        }

        [Test]
        public void ScalesRendererWeightsByFrameWeight()
        {
            var (mesh, renderer) = CreateShapedStrip();
            renderer.SetBlendShapeWeight(0, 50.0f);
            renderer.SetBlendShapeWeight(1, 25.0f);
            AssertVectors(
                Expected(mesh, (i) => new Vector3(0.5f * i, 0.5f, 0)),
                MeshManipulation.ComputeBlendShapeAppliedVertices(mesh, renderer, NoOverrides));
        }

        [Test]
        public void OverridesAreNormalizedAndReplaceRendererWeights()
        {
            var (mesh, renderer) = CreateShapedStrip();
            renderer.SetBlendShapeWeight(0, 100.0f);
            var overrides = ImmutableArray.Create(("up", 0.0f), ("right", 2.0f), ("missing", 1.0f));
            AssertVectors(
                Expected(mesh, (i) => new Vector3(2 * i, 0, 0)),
                MeshManipulation.ComputeBlendShapeAppliedVertices(mesh, renderer, overrides));
        }

        [Test]
        public void IgnoresMultiFrameBlendShapes()
        {
            var (mesh, renderer) = CreateShapedStrip();
            renderer.SetBlendShapeWeight(2, 100.0f);
            var overrides = ImmutableArray.Create(("multi", 1.0f));
            AssertVectors(mesh.vertices, MeshManipulation.ComputeBlendShapeAppliedVertices(mesh, renderer, overrides));
        }

        [TestCase(1)]
        [TestCase(8)]
        [TestCase(9)]
        [TestCase(20)]
        public void AccumulatesAcrossBatches(int shapeCount)
        {
            var mesh = CreateQuadStrip(1);
            for (var s = 0; s < shapeCount; ++s)
            {
                var shape = s;
                AddShape(mesh, $"s{shape}", (i) => new Vector3(shape + 1, i, 0));
            }
            var renderer = CreateRenderer(mesh);
            for (var s = 0; s < shapeCount; ++s) renderer.SetBlendShapeWeight(s, 10.0f);

            var sum = Enumerable.Range(1, shapeCount).Sum() * 0.1f;
            AssertVectors(
                Expected(mesh, (i) => new Vector3(sum, i * shapeCount * 0.1f, 0)),
                MeshManipulation.ComputeBlendShapeAppliedVertices(mesh, renderer, NoOverrides));
        }

        [Test]
        public void RejectsRendererWithDifferentVertexCount()
        {
            var (mesh, _) = CreateShapedStrip();
            var other = CreateRenderer(CreateQuadStrip(2));
            Assert.That(() => MeshManipulation.ComputeBlendShapeAppliedVertices(mesh, other, NoOverrides), Throws.ArgumentException);
        }

        [Test]
        public void RejectsNullArguments()
        {
            var (mesh, renderer) = CreateShapedStrip();
            Assert.That(() => MeshManipulation.ComputeBlendShapeAppliedVertices(null, renderer, NoOverrides), Throws.ArgumentNullException);
            Assert.That(() => MeshManipulation.ComputeBlendShapeAppliedVertices(mesh, null, NoOverrides), Throws.ArgumentNullException);
        }
    }
}
