using System.Collections.Immutable;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KusakaFactory.Zatools.Ndmf.Core;

namespace KusakaFactory.Zatools.Tests.Ndmf.Core
{
    public sealed class CdwTests : MeshTestBase
    {
        private static readonly Vector3 Interior = new Vector3(0.5f, 0.5f, 0.5f);

        private Material _original;
        private Material _wrapper;

        [SetUp]
        public void CreateMaterials()
        {
            _original = CreateMaterial("Original");
            _wrapper = CreateMaterial("Wrapper");
        }

        private static Cdw.FixedParameters Parameters(params (string, float)[] overrides)
        {
            return new Cdw.FixedParameters { Overrides = overrides.ToImmutableArray() };
        }

        private static void AssertClosedHull(Mesh mesh, int[] triangles)
        {
            Assert.That(triangles.Length, Is.EqualTo(12 * 3));
            var vertices = mesh.vertices;
            var centroid = triangles.Distinct().Aggregate(Vector3.zero, (sum, i) => sum + vertices[i]) / triangles.Distinct().Count();
            for (var t = 0; t < triangles.Length; t += 3)
            {
                var a = vertices[triangles[t]];
                var normal = Vector3.Cross(vertices[triangles[t + 1]] - a, vertices[triangles[t + 2]] - a);
                Assert.That(Vector3.Dot(normal, a - centroid), Is.GreaterThan(0.0f), $"triangle {t / 3} faces inward");
            }
        }

        [Test]
        public void AppendsHullAsNewSubMeshWithWrapperMaterial()
        {
            var mesh = CreateCubeCloud(Interior);
            SetSingleBoneWeights(mesh, (i) => i, 9);
            var renderer = CreateRenderer(mesh, _original);
            var modifying = Track(Object.Instantiate(mesh));

            Assert.That(Cdw.Process(renderer, modifying, Parameters(), _wrapper), Is.True);

            Assert.That(modifying.vertexCount, Is.EqualTo(9 + 8));
            Assert.That(modifying.subMeshCount, Is.EqualTo(2));
            Assert.That(modifying.GetTriangles(0), Is.EqualTo(mesh.GetTriangles(0)));
            Assert.That(renderer.sharedMaterials, Is.EqualTo(new[] { _original, _wrapper }));

            var hull = modifying.GetTriangles(1);
            Assert.That(hull.Distinct(), Is.EquivalentTo(Enumerable.Range(9, 8)));
            AssertClosedHull(modifying, hull);

            var vertices = modifying.vertices;
            var boneWeights = modifying.boneWeights;
            var uvs = modifying.uv;
            AssertVectors(mesh.vertices, vertices.Take(9).ToArray());
            foreach (var index in hull.Distinct())
            {
                var corner = Enumerable.Range(0, 8).Single((c) => (mesh.vertices[c] - vertices[index]).sqrMagnitude < 1e-8f);
                Assert.That(boneWeights[index].boneIndex0, Is.EqualTo(corner));
                Assert.That(uvs[index], Is.EqualTo(Vector2.zero));
            }
        }

        [Test]
        public void HullFollowsAppliedBlendShapes()
        {
            var mesh = CreateCubeCloud(Interior);
            AddShape(mesh, "push", (i) => i == 8 ? new Vector3(0, 0, 2) : Vector3.zero);
            AddShape(mesh, "pull", (i) => i == 7 ? new Vector3(-0.5f, -0.5f, -0.5f) : Vector3.zero);
            var renderer = CreateRenderer(mesh, _original);
            renderer.SetBlendShapeWeight(0, 100.0f);
            var modifying = Track(Object.Instantiate(mesh));

            Assert.That(Cdw.Process(renderer, modifying, Parameters(("pull", 1.0f)), _wrapper), Is.True);

            var hullVertices = modifying.GetTriangles(1).Distinct().Select((i) => modifying.vertices[i]).ToArray();
            Assert.That(hullVertices.Any((v) => (v - new Vector3(0.5f, 0.5f, 2.5f)).sqrMagnitude < 1e-8f), Is.True, "pushed interior point is missing");
            Assert.That(hullVertices.Any((v) => (v - Vector3.one).sqrMagnitude < 1e-8f), Is.False, "pulled corner is still on the hull");
        }

        [Test]
        public void DegenerateHullLeavesMeshUntouched()
        {
            var mesh = CreateQuadStrip(2);
            var renderer = CreateRenderer(mesh, _original);
            var modifying = Track(Object.Instantiate(mesh));

            Assert.That(Cdw.Process(renderer, modifying, Parameters(), _wrapper), Is.False);
            Assert.That(modifying.vertexCount, Is.EqualTo(mesh.vertexCount));
            Assert.That(modifying.subMeshCount, Is.EqualTo(1));
            Assert.That(renderer.sharedMaterials, Is.EqualTo(new[] { _original }));
        }

        [Test]
        public void RejectsVertexCountMismatch()
        {
            var renderer = CreateRenderer(CreateCubeCloud(), _original);
            Assert.That(() => Cdw.Process(renderer, CreateCubeCloud(Interior), Parameters(), _wrapper), Throws.ArgumentException);
        }

        [Test]
        public void SeparateBuildsHullOnlyMeshFromSource()
        {
            var sourceMesh = CreateCubeCloud(Interior);
            SetSingleBoneWeights(sourceMesh, (i) => i, 9);
            var source = CreateRenderer(sourceMesh, _original);
            var target = CreateRenderer(null);
            var generated = Track(new Mesh());
            var parameters = Parameters();
            parameters.SeparateSmr = true;
            parameters.SourceMeshRenderer = source;

            Assert.That(Cdw.ProcessSeparate(target, generated, parameters, _wrapper), Is.True);

            Assert.That(generated.vertexCount, Is.EqualTo(8));
            Assert.That(generated.subMeshCount, Is.EqualTo(1));
            AssertClosedHull(generated, generated.GetTriangles(0));
            Assert.That(generated.bindposes, Has.Length.EqualTo(9));
            Assert.That(generated.boneWeights.Select((w) => w.boneIndex0), Is.EquivalentTo(Enumerable.Range(0, 8)));
            Assert.That(target.sharedMaterials, Is.EqualTo(new[] { _wrapper }));
            Assert.That(source.sharedMaterials, Is.EqualTo(new[] { _original }));
        }

        [Test]
        public void SeparateRequiresSourceRenderer()
        {
            var target = CreateRenderer(null);
            var generated = Track(new Mesh());
            Assert.That(Cdw.ProcessSeparate(target, generated, Parameters(), _wrapper), Is.False);

            var parameters = Parameters();
            parameters.SeparateSmr = true;
            parameters.SourceMeshRenderer = CreateRenderer(null);
            Assert.That(Cdw.ProcessSeparate(target, generated, parameters, _wrapper), Is.False);
            Assert.That(generated.vertexCount, Is.EqualTo(0));
        }
    }
}
