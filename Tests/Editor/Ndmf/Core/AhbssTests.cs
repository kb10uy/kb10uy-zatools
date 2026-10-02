using System;
using System.Collections.Immutable;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KusakaFactory.Zatools.Ndmf.Core;

namespace KusakaFactory.Zatools.Tests.Ndmf.Core
{
    public sealed class AhbssTests : MeshTestBase
    {
        private const int VertexCount = 8;

        private (SkinnedMeshRenderer Renderer, Mesh Mesh) CreateShapedStrip()
        {
            var mesh = CreateQuadStrip(3);
            SetSingleBoneWeights(mesh, (i) => i / 2, 4);
            AddShape(mesh, "smile", (i) => new Vector3(0, i + 1, 0), (i) => new Vector3(i, 0, 0), (i) => new Vector3(0, 0, i), 80.0f);
            AddShape(mesh, "inner", (i) => i == 3 || i == 4 ? Vector3.forward : Vector3.zero);
            AddShape(mesh, "multi", (i) => Vector3.one, 50.0f);
            AddShape(mesh, "multi", (i) => Vector3.one * 2, 100.0f);

            var renderer = CreateRenderer(mesh);
            renderer.bones = CreateBones(4);
            return (renderer, Track(UnityEngine.Object.Instantiate(mesh)));
        }

        private static Ahbss.FixedParameters Parameters(Transform basis, string[] targets, string left = "_L", string right = "_R")
        {
            return new Ahbss.FixedParameters
            {
                Basis = basis,
                TargetShapes = targets.ToImmutableArray(),
                LeftSuffix = left,
                RightSuffix = right,
            };
        }

        private static Vector3[] Masked(Func<int, Vector3> delta, Func<int, bool> keep)
        {
            return Enumerable.Range(0, VertexCount).Select((i) => keep(i) ? delta(i) : Vector3.zero).ToArray();
        }

        private static bool IsLeft(int vertex) => vertex / 2 < 2;

        [Test]
        public void SplitsByBasisXAndKeepsFrameWeight()
        {
            var (renderer, mesh) = CreateShapedStrip();
            Ahbss.AddSplitShapes(renderer, mesh, Parameters(renderer.transform, new[] { "smile" }));

            Assert.That(ShapeNames(mesh), Is.EqualTo(new[] { "smile", "inner", "multi", "smile_L", "smile_R" }));
            Assert.That(mesh.GetBlendShapeFrameWeight(3, 0), Is.EqualTo(80.0f));
            Assert.That(mesh.GetBlendShapeFrameWeight(4, 0), Is.EqualTo(80.0f));

            var left = ShapeDeltas(mesh, "smile_L");
            AssertVectors(Masked((i) => new Vector3(0, i + 1, 0), IsLeft), left.Vertices);
            AssertVectors(Masked((i) => new Vector3(i, 0, 0), IsLeft), left.Normals);
            AssertVectors(Masked((i) => new Vector3(0, 0, i), IsLeft), left.Tangents);

            var right = ShapeDeltas(mesh, "smile_R");
            AssertVectors(Masked((i) => new Vector3(0, i + 1, 0), (i) => !IsLeft(i)), right.Vertices);
            AssertVectors(Masked((i) => new Vector3(i, 0, 0), (i) => !IsLeft(i)), right.Normals);
        }

        [Test]
        public void RotatedBasisSwapsSides()
        {
            var (renderer, mesh) = CreateShapedStrip();
            var basis = Track(new GameObject("Basis")).transform;
            basis.rotation = Quaternion.Euler(0, 180, 0);
            Ahbss.AddSplitShapes(renderer, mesh, Parameters(basis, new[] { "smile" }));

            AssertVectors(Masked((i) => new Vector3(0, i + 1, 0), (i) => !IsLeft(i)), ShapeDeltas(mesh, "smile_L").Vertices);
            AssertVectors(Masked((i) => new Vector3(0, i + 1, 0), IsLeft), ShapeDeltas(mesh, "smile_R").Vertices);
        }

        [Test]
        public void JudgesSidesOnCurrentlyDeformedVertices()
        {
            var (renderer, mesh) = CreateShapedStrip();
            AddShape(renderer.sharedMesh, "shift", (_) => new Vector3(1, 0, 0));
            AddShape(mesh, "shift", (_) => new Vector3(1, 0, 0));
            renderer.SetBlendShapeWeight(renderer.sharedMesh.GetBlendShapeIndex("shift"), 100.0f);
            Ahbss.AddSplitShapes(renderer, mesh, Parameters(renderer.transform, new[] { "smile" }));

            AssertVectors(Masked((i) => new Vector3(0, i + 1, 0), (i) => i / 2 < 1), ShapeDeltas(mesh, "smile_L").Vertices);
        }

        [Test]
        public void BlankSuffixSkipsThatSide()
        {
            var (renderer, mesh) = CreateShapedStrip();
            Ahbss.AddSplitShapes(renderer, mesh, Parameters(renderer.transform, new[] { "smile" }, left: " "));
            Assert.That(ShapeNames(mesh), Is.EqualTo(new[] { "smile", "inner", "multi", "smile_R" }));
        }

        [Test]
        public void NothingToDoReturnsNoBones()
        {
            var (renderer, mesh) = CreateShapedStrip();
            Assert.That(Ahbss.AddSplitShapes(renderer, mesh, Parameters(renderer.transform, new[] { "smile" }, left: "", right: null)), Is.Empty);
            Assert.That(Ahbss.AddSplitShapes(renderer, mesh, Parameters(renderer.transform, new string[0])), Is.Empty);
            Assert.That(mesh.blendShapeCount, Is.EqualTo(3));
        }

        [Test]
        public void SkipsMissingAndMultiFrameTargets()
        {
            var (renderer, mesh) = CreateShapedStrip();
            var influent = Ahbss.AddSplitShapes(renderer, mesh, Parameters(renderer.transform, new[] { "missing", "multi" }));
            Assert.That(mesh.blendShapeCount, Is.EqualTo(3));
            Assert.That(influent, Is.Empty);
        }

        [Test]
        public void DoesNotOverwriteExistingShapes()
        {
            var (renderer, mesh) = CreateShapedStrip();
            AddShape(mesh, "smile_L", (_) => new Vector3(9, 9, 9));
            Ahbss.AddSplitShapes(renderer, mesh, Parameters(renderer.transform, new[] { "smile" }));

            Assert.That(ShapeNames(mesh), Is.EqualTo(new[] { "smile", "inner", "multi", "smile_L", "smile_R" }));
            AssertVectors(Enumerable.Repeat(new Vector3(9, 9, 9), VertexCount).ToArray(), ShapeDeltas(mesh, "smile_L").Vertices);
        }

        [Test]
        public void ReportsBonesOfVerticesMovedByAnyTarget()
        {
            var (renderer, mesh) = CreateShapedStrip();
            Assert.That(Ahbss.AddSplitShapes(renderer, mesh, Parameters(renderer.transform, new[] { "inner" })), Is.EquivalentTo(new[] { 1, 2 }));

            var (otherRenderer, otherMesh) = CreateShapedStrip();
            Assert.That(Ahbss.AddSplitShapes(otherRenderer, otherMesh, Parameters(otherRenderer.transform, new[] { "inner", "smile" })), Is.EquivalentTo(new[] { 0, 1, 2, 3 }));
        }

        [Test]
        public void SplitsMeshWithoutBoneWeights()
        {
            var mesh = CreateQuadStrip(3);
            AddShape(mesh, "smile", (i) => new Vector3(0, i + 1, 0));
            var renderer = CreateRenderer(mesh);
            var modifying = Track(UnityEngine.Object.Instantiate(mesh));

            var influent = Ahbss.AddSplitShapes(renderer, modifying, Parameters(renderer.transform, new[] { "smile" }));

            Assert.That(influent, Is.Empty);
            AssertVectors(Masked((i) => new Vector3(0, i + 1, 0), IsLeft), ShapeDeltas(modifying, "smile_L").Vertices);
            AssertVectors(Masked((i) => new Vector3(0, i + 1, 0), (i) => !IsLeft(i)), ShapeDeltas(modifying, "smile_R").Vertices);
        }

        [Test]
        public void RejectsVertexCountMismatch()
        {
            var (renderer, _) = CreateShapedStrip();
            var other = CreateQuadStrip(1);
            Assert.That(() => Ahbss.AddSplitShapes(renderer, other, Parameters(renderer.transform, new[] { "smile" })), Throws.ArgumentException);
        }
    }
}
