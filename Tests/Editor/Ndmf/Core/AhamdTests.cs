using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using KusakaFactory.Zatools.Foundation.Arithmetic;
using KusakaFactory.Zatools.Ndmf.Core;
using KusakaFactory.Zatools.Runtime;

namespace KusakaFactory.Zatools.Tests.Ndmf.Core
{
    public sealed class AhamdTests : MeshTestBase
    {
        private const string SelectRightmostQuad = "@position #x 1 >=";

        private Material _first;
        private Material _second;

        [SetUp]
        public void CreateMaterials()
        {
            _first = CreateMaterial("First");
            _second = CreateMaterial("Second");
        }

        /// <summary>
        /// Four quads spanning X = -2 to 2, quads 0-1 in submesh 0 and quads 2-3 in submesh 1.
        /// Vertex i is weighted to bone i, "left" moves quad 0 and "right" moves quad 3.
        /// </summary>
        private SkinnedMeshRenderer CreateSource()
        {
            var mesh = CreateQuadStrip(4, 2);
            SetSingleBoneWeights(mesh, (i) => i, 10);
            mesh.colors = Enumerable.Range(0, mesh.vertexCount).Select((i) => new Color(i / 10.0f, 0, 0, 1)).ToArray();
            AddShape(mesh, "left", (i) => i < 4 ? new Vector3(0, 0, 1) : Vector3.zero);
            AddShape(mesh, "right", (i) => i >= 6 ? new Vector3(0, i, 0) : Vector3.zero);
            AddShape(mesh, "right", (i) => i >= 6 ? new Vector3(0, 2 * i, 0) : Vector3.zero, 200.0f);
            return CreateRenderer(mesh, _first, _second);
        }

        private static Ahamd.FixedParameters Parameters(SkinnedMeshRenderer source, string selection, params (AhamdModificationTarget, string)[] modifications)
        {
            return new Ahamd.FixedParameters
            {
                Source = source,
                SelectionExpression = selection,
                SelectionThreshold = 0.5f,
                Modifications = modifications.Select((m) => new Ahamd.FixedModification { Target = m.Item1, Expression = m.Item2 }).ToImmutableArray(),
            };
        }

        private static Ahamd.CompiledPrograms Compile(Ahamd.FixedParameters parameters)
        {
            Assert.That(Ahamd.TryCompilePrograms(parameters, Ahamd.TryCompileSilently, out var programs), Is.True);
            return programs;
        }

        private (SkinnedMeshRenderer Target, Mesh Generated) Run(Ahamd.FixedParameters parameters)
        {
            var target = CreateRenderer(null);
            var generated = Track(new Mesh());
            Ahamd.Process(target, generated, parameters, Compile(parameters));
            return (target, generated);
        }

        [TestCase(AhamdModificationTarget.Position, ZaxValueType.Float3)]
        [TestCase(AhamdModificationTarget.Normal, ZaxValueType.Float3)]
        [TestCase(AhamdModificationTarget.Tangent, ZaxValueType.Float4)]
        [TestCase(AhamdModificationTarget.VertexColor, ZaxValueType.Float4)]
        [TestCase(AhamdModificationTarget.UV3, ZaxValueType.Float4)]
        [TestCase(AhamdModificationTarget.Scratch2, ZaxValueType.Float4)]
        public void ResultTypeFollowsTarget(AhamdModificationTarget target, ZaxValueType expected)
        {
            Assert.That(Ahamd.ResultTypeOf(target), Is.EqualTo(expected));
        }

        [TestCase("@mask #r", AhamdModificationTarget.UV0, "@uv0 @texture *", true)]
        [TestCase("@scratch3 #w @uv7 #x +", AhamdModificationTarget.Position, "@position @normal +", true)]
        [TestCase("@texture #r", AhamdModificationTarget.UV0, "@uv0", false)]
        [TestCase("@mask", AhamdModificationTarget.UV0, "@uv0", false)]
        [TestCase("1", AhamdModificationTarget.UV0, "@mask", false)]
        [TestCase("1", AhamdModificationTarget.Normal, "@tangent", false)]
        [TestCase("1", AhamdModificationTarget.UV0, "@uv8", false)]
        public void CompilesSelectionAndModificationsWithTheirOwnVariables(string selection, AhamdModificationTarget target, string modification, bool expected)
        {
            var parameters = Parameters(null, selection, (target, modification));
            Assert.That(Ahamd.TryCompilePrograms(parameters, Ahamd.TryCompileSilently, out var programs), Is.EqualTo(expected));
            if (expected) Assert.That(programs.Modifications, Has.Length.EqualTo(1));
        }

        [Test]
        public void SelectingEverythingCopiesTheMesh()
        {
            var source = CreateSource();
            var (target, generated) = Run(Parameters(source, "1"));
            var sourceMesh = source.sharedMesh;

            AssertVectors(sourceMesh.vertices, generated.vertices);
            AssertVectors(sourceMesh.normals, generated.normals);
            Assert.That(generated.uv, Is.EqualTo(sourceMesh.uv));
            Assert.That(generated.colors, Is.EqualTo(sourceMesh.colors));
            Assert.That(generated.subMeshCount, Is.EqualTo(2));
            Assert.That(generated.GetTriangles(0), Is.EqualTo(sourceMesh.GetTriangles(0)));
            Assert.That(generated.GetTriangles(1), Is.EqualTo(sourceMesh.GetTriangles(1)));
            Assert.That(ShapeNames(generated), Is.EqualTo(new[] { "left", "right" }));
            Assert.That(generated.bindposes, Has.Length.EqualTo(10));
            Assert.That(generated.indexFormat, Is.EqualTo(IndexFormat.UInt16));
            Assert.That(target.sharedMaterials, Is.EqualTo(new[] { _first, _second }));
        }

        [Test]
        public void KeepsOnlyTrianglesWithAllVerticesSelected()
        {
            var source = CreateSource();
            var (target, generated) = Run(Parameters(source, SelectRightmostQuad));

            Assert.That(generated.vertexCount, Is.EqualTo(4));
            AssertVectors(new[] { new Vector3(1, 0, 0), new Vector3(1, 1, 0), new Vector3(2, 0, 0), new Vector3(2, 1, 0) }, generated.vertices);
            Assert.That(generated.subMeshCount, Is.EqualTo(1));
            Assert.That(generated.GetTriangles(0), Is.EqualTo(QuadTriangles(0)));
            Assert.That(target.sharedMaterials, Is.EqualTo(new[] { _second }));
            Assert.That(generated.boneWeights.Select((w) => w.boneIndex0), Is.EqualTo(new[] { 6, 7, 8, 9 }));
            Assert.That(generated.colors.Select((c) => c.r), Is.EqualTo(new[] { 0.6f, 0.7f, 0.8f, 0.9f }));
        }

        [Test]
        public void CopiesOnlyBlendShapesMovingSelectedVertices()
        {
            var source = CreateSource();
            var (_, generated) = Run(Parameters(source, SelectRightmostQuad));

            Assert.That(ShapeNames(generated), Is.EqualTo(new[] { "right" }));
            Assert.That(generated.GetBlendShapeFrameCount(0), Is.EqualTo(2));
            Assert.That(generated.GetBlendShapeFrameWeight(0, 1), Is.EqualTo(200.0f));
            AssertVectors(new[] { new Vector3(0, 6, 0), new Vector3(0, 7, 0), new Vector3(0, 8, 0), new Vector3(0, 9, 0) }, ShapeDeltas(generated, "right").Vertices);
            AssertVectors(new[] { new Vector3(0, 12, 0), new Vector3(0, 14, 0), new Vector3(0, 16, 0), new Vector3(0, 18, 0) }, ShapeDeltas(generated, "right", 1).Vertices);
        }

        [Test]
        public void SelectingNothingLeavesTargetUntouched()
        {
            var source = CreateSource();
            var (target, generated) = Run(Parameters(source, "0"));

            Assert.That(generated.vertexCount, Is.EqualTo(0));
            Assert.That(target.sharedMaterials, Is.Empty);
        }

        [Test]
        public void MissingSourceDoesNothing()
        {
            var (target, generated) = Run(Parameters(null, "1"));
            Assert.That(generated.vertexCount, Is.EqualTo(0));
            Assert.That(target.sharedMaterials, Is.Empty);
        }

        [Test]
        public void SelectionThresholdIsInclusive()
        {
            var source = CreateSource();
            var parameters = Parameters(source, "@position #x 2 + 4 /");
            parameters.SelectionThreshold = 0.75f;
            var (_, generated) = Run(parameters);
            Assert.That(generated.vertexCount, Is.EqualTo(4));
        }

        [Test]
        public void MissingMaskTextureActsAsWhite()
        {
            var source = CreateSource();
            var (_, generated) = Run(Parameters(source, "@mask #r @mask #g * @mask #b * @mask #a *"));
            Assert.That(generated.vertexCount, Is.EqualTo(source.sharedMesh.vertexCount));
        }

        [Test]
        public void AppliesModificationsInOrder()
        {
            var source = CreateSource();
            var (_, generated) = Run(Parameters(
                source,
                SelectRightmostQuad,
                (AhamdModificationTarget.Scratch1, "@position ... 1 vec4"),
                (AhamdModificationTarget.Position, "@position 0 0 1 vec3 +"),
                (AhamdModificationTarget.UV2, "@scratch1 @texture *"),
                (AhamdModificationTarget.VertexColor, "@color #a @color #r 0 1 vec4"),
                (AhamdModificationTarget.Normal, "Y_AXIS")));

            var original = new[] { new Vector3(1, 0, 0), new Vector3(1, 1, 0), new Vector3(2, 0, 0), new Vector3(2, 1, 0) };
            AssertVectors(original.Select((v) => v + Vector3.forward).ToArray(), generated.vertices);
            AssertVectors(Enumerable.Repeat(Vector3.up, 4).ToArray(), generated.normals);

            var uv2 = new List<Vector4>();
            generated.GetUVs(2, uv2);
            AssertVectors(original.Select((v) => new Vector4(v.x, v.y, v.z, 1)).ToArray(), uv2);
            Assert.That(generated.GetVertexAttributeDimension(VertexAttribute.TexCoord2), Is.EqualTo(4));
            Assert.That(generated.GetVertexAttributeDimension(VertexAttribute.TexCoord0), Is.EqualTo(2));

            AssertVectors(new[] { 0.6f, 0.7f, 0.8f, 0.9f }.Select((r) => new Vector4(1, r, 0, 1)).ToArray(), generated.colors.Select((c) => (Vector4)c).ToArray());
        }

        [Test]
        public void MissingVertexColorsReadAsWhite()
        {
            var source = CreateSource();
            source.sharedMesh.colors = new Color[0];
            var (_, generated) = Run(Parameters(source, "1", (AhamdModificationTarget.UV1, "@color")));

            var uv1 = new List<Vector4>();
            generated.GetUVs(1, uv1);
            AssertVectors(Enumerable.Repeat(Vector4.one, source.sharedMesh.vertexCount).ToArray(), uv1);
            Assert.That(generated.HasVertexAttribute(VertexAttribute.Color), Is.False);
        }

        [Test]
        public void RecalculatesNormalsWhenRequested()
        {
            var source = CreateSource();
            var parameters = Parameters(source, "1", (AhamdModificationTarget.Normal, "X_AXIS"));
            parameters.RecalculateNormals = true;
            var (_, generated) = Run(parameters);

            foreach (var normal in generated.normals) AssertVector(new Vector3(0, 0, -1), normal);
        }

        [Test]
        [Category("GPU")]
        public void SamplesSelectionAndModificationTextures()
        {
            AssumeComputeShaders();

            var source = CreateSource();
            var parameters = Parameters(source, "@mask #g", (AhamdModificationTarget.UV1, "@texture"));
            parameters.SelectionTexture = CreateTexture(4, 1, Color.black, Color.black, Color.black, Color.green);
            parameters.SelectionTextureUv = UvChannel.UV0;
            var modifications = parameters.Modifications.ToBuilder();
            modifications[0] = new Ahamd.FixedModification
            {
                Target = AhamdModificationTarget.UV1,
                Expression = "@texture",
                ExtraTexture = CreateTexture(1, 1, new Color(0.1f, 0.2f, 0.3f, 0.4f)),
                ExtraTextureUv = UvChannel.UV0,
            };
            parameters.Modifications = modifications.ToImmutable();

            var mesh = source.sharedMesh;
            mesh.uv = Enumerable.Range(0, mesh.vertexCount).Select((i) => new Vector2(i / 2 >= 3 ? 0.875f : 0.125f, 0.5f)).ToArray();
            var (_, generated) = Run(parameters);

            Assert.That(generated.vertexCount, Is.EqualTo(4));
            var uv1 = new List<Vector4>();
            generated.GetUVs(1, uv1);
            AssertVectors(Enumerable.Repeat(new Vector4(0.1f, 0.2f, 0.3f, 0.4f), 4).ToArray(), uv1);
        }
    }
}
