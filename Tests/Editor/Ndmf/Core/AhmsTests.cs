using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KusakaFactory.Zatools.Ndmf.Core;
using KusakaFactory.Zatools.Runtime;

namespace KusakaFactory.Zatools.Tests.Ndmf.Core
{
    public sealed class AhmsTests : MeshTestBase
    {
        private Material _first;
        private Material _second;
        private Material _split;

        [SetUp]
        public void CreateMaterials()
        {
            _first = CreateMaterial("First");
            _second = CreateMaterial("Second");
            _split = CreateMaterial("Split");
        }

        private (SkinnedMeshRenderer Renderer, Mesh Mesh) CreateTwoSubMeshStrip()
        {
            var mesh = CreateQuadStrip(4, 2);
            return (CreateRenderer(mesh, _first, _second), mesh);
        }

        [Test]
        public void WhiteWithoutMaskSplitsEveryTriangle()
        {
            var (renderer, mesh) = CreateTwoSubMeshStrip();
            Ahms.Process(renderer, mesh, new Ahms.FixedParameters { MaskMode = MeshSplitMaskMode.White, SplitMaterial = _split });

            Assert.That(mesh.subMeshCount, Is.EqualTo(3));
            Assert.That(mesh.GetTriangles(0), Is.Empty);
            Assert.That(mesh.GetTriangles(1), Is.Empty);
            Assert.That(mesh.GetTriangles(2), Is.EqualTo(Enumerable.Range(0, 4).SelectMany(QuadTriangles)));
            Assert.That(renderer.sharedMaterials, Is.EqualTo(new[] { _first, _second, _split }));
        }

        [Test]
        public void BlackWithoutMaskSplitsNothing()
        {
            var (renderer, mesh) = CreateTwoSubMeshStrip();
            Ahms.Process(renderer, mesh, new Ahms.FixedParameters { MaskMode = MeshSplitMaskMode.Black, SplitMaterial = _split });

            Assert.That(mesh.subMeshCount, Is.EqualTo(3));
            Assert.That(mesh.GetTriangles(0), Is.EqualTo(QuadTriangles(0).Concat(QuadTriangles(1))));
            Assert.That(mesh.GetTriangles(1), Is.EqualTo(QuadTriangles(2).Concat(QuadTriangles(3))));
            Assert.That(mesh.GetTriangles(2), Is.Empty);
            Assert.That(renderer.sharedMaterials, Is.EqualTo(new[] { _first, _second, _split }));
        }

        [Test]
        public void FilteringMaterialRestrictsSplitToMatchingSubMeshes()
        {
            var (renderer, mesh) = CreateTwoSubMeshStrip();
            Ahms.Process(renderer, mesh, new Ahms.FixedParameters
            {
                MaskMode = MeshSplitMaskMode.White,
                FilteringMaterial = _second,
                SplitMaterial = _split,
            });

            Assert.That(mesh.GetTriangles(0), Is.EqualTo(QuadTriangles(0).Concat(QuadTriangles(1))));
            Assert.That(mesh.GetTriangles(1), Is.Empty);
            Assert.That(mesh.GetTriangles(2), Is.EqualTo(QuadTriangles(2).Concat(QuadTriangles(3))));
        }

        [Test]
        public void MissingMaterialSlotsBecomeNull()
        {
            var mesh = CreateQuadStrip(4, 2);
            var renderer = CreateRenderer(mesh, _first);
            Ahms.Process(renderer, mesh, new Ahms.FixedParameters { MaskMode = MeshSplitMaskMode.White, SplitMaterial = _split });

            Assert.That(renderer.sharedMaterials, Is.EqualTo(new[] { _first, null, _split }));
        }

        [TestCase(MeshSplitMaskMode.White, new[] { 2, 3 }, new[] { 0, 1 })]
        [TestCase(MeshSplitMaskMode.Black, new[] { 0 }, new[] { 1, 2, 3 })]
        [Category("GPU")]
        public void SplitsTrianglesWhoseVerticesAreAllMasked(MeshSplitMaskMode mode, int[] splitQuads, int[] remainingQuads)
        {
            AssumeComputeShaders();

            var mask = CreateTexture(2, 1, Color.black, Color.white);
            var mesh = CreateQuadStrip(4);
            mesh.uv = Enumerable.Range(0, mesh.vertexCount).Select((i) => new Vector2(i / 2 < 2 ? 0.25f : 0.75f, 0.5f)).ToArray();
            var renderer = CreateRenderer(mesh, _first);

            Ahms.Process(renderer, mesh, new Ahms.FixedParameters { MaskTexture = mask, MaskMode = mode, SplitMaterial = _split });

            Assert.That(mesh.GetTriangles(0), Is.EqualTo(remainingQuads.SelectMany(QuadTriangles)));
            Assert.That(mesh.GetTriangles(1), Is.EqualTo(splitQuads.SelectMany(QuadTriangles)));
        }
    }
}
