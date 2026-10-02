using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using KusakaFactory.Zatools.Foundation;
using KusakaFactory.Zatools.Ndmf.Core;
using KusakaFactory.Zatools.Runtime;

namespace KusakaFactory.Zatools.Tests.Ndmf.Core
{
    public sealed class AhvdtTests : MeshTestBase
    {
        private static readonly float4 SampledColor = new float4(0.2f, 0.4f, 0.6f, 0.8f);
        private static readonly float4 Constant = new float4(1.0f, -1.0f, 0.5f, 0.25f);
        private static readonly float SampledLuminance = Luminance.Rec709(SampledColor.xyz);

        private static IEnumerable<TestCaseData> TransferModeCases()
        {
            var l = SampledLuminance;
            yield return new TestCaseData(VertexDataTransferMode.Copy, new Vector4(0.2f, 0.4f, 0.6f, 0.8f));
            yield return new TestCaseData(VertexDataTransferMode.OneMinus, new Vector4(0.8f, 0.6f, 0.4f, 0.2f));
            yield return new TestCaseData(VertexDataTransferMode.ConstAndLuminance, new Vector4(1.0f, -1.0f, 0.5f, l));
            yield return new TestCaseData(VertexDataTransferMode.Const01AndLuminance, new Vector4(1.0f, 0.0f, 0.75f, l));
            yield return new TestCaseData(VertexDataTransferMode.LuminanceConstAndConst, new Vector4(l, -l, 0.5f * l, 0.25f));
            yield return new TestCaseData(VertexDataTransferMode.LuminanceConst01AndConst, new Vector4(l / 2 + 0.5f, -l / 2 + 0.5f, l / 4 + 0.5f, 0.25f));
            yield return new TestCaseData(VertexDataTransferMode.CustomExpression, Vector4.zero);
        }

        [TestCaseSource(nameof(TransferModeCases))]
        public void TransferJobComputesModeValue(VertexDataTransferMode mode, Vector4 expected)
        {
            var colors = new NativeArray<float4>(3, Allocator.TempJob);
            var values = new NativeArray<float4>(3, Allocator.TempJob);
            try
            {
                for (var i = 0; i < colors.Length; ++i) colors[i] = SampledColor;
                var job = new Ahvdt.TransferVertexDataJob
                {
                    TransferValues = values,
                    TransferMode = mode,
                    SampledColors = colors,
                    Constant = Constant,
                };
                job.Schedule(colors.Length, 1).Complete();
                foreach (var value in values) AssertVector(expected, value);
            }
            finally
            {
                colors.Dispose();
                values.Dispose();
            }
        }

        [Test]
        public void NonExpressionModesNeedNoProgram()
        {
            var parameters = new Ahvdt.FixedParameters { TransferMode = VertexDataTransferMode.Copy, Expression = "not an expression" };
            Assert.That(Ahvdt.TryCompileExpression(parameters, out var program), Is.True);
            Assert.That(program, Is.Null);
        }

        [TestCase("@color")]
        [TestCase("@uv @color *")]
        [TestCase("@position ... @tangent #w vec4")]
        [TestCase("1 2 3 4 vec4")]
        public void CompilesExpressionsProducingFloat4(string expression)
        {
            var parameters = new Ahvdt.FixedParameters { TransferMode = VertexDataTransferMode.CustomExpression, Expression = expression };
            Assert.That(Ahvdt.TryCompileExpression(parameters, out var program), Is.True);
            Assert.That(program.ResultType, Is.EqualTo(Ahvdt.ExpressionResultType));
        }

        [TestCase("@position")]
        [TestCase("@color @color")]
        [TestCase("@mask")]
        [TestCase("")]
        public void RejectsExpressionsNotProducingSingleFloat4(string expression)
        {
            var parameters = new Ahvdt.FixedParameters { TransferMode = VertexDataTransferMode.CustomExpression, Expression = expression };
            Assert.That(Ahvdt.TryCompileExpression(parameters, out _), Is.False);
        }

        [Test]
        public void DisabledTargetLeavesMeshUntouched()
        {
            var mesh = CreateQuadStrip(1);
            Ahvdt.Process(mesh, new Ahvdt.FixedParameters
            {
                TransferTarget = VertexDataTransferTarget.Disabled,
                TransferMode = VertexDataTransferMode.Copy,
            }, null);
            Assert.That(mesh.colors, Is.Empty);
        }

        [Test]
        public void ExpressionModeWithoutProgramLeavesMeshUntouched()
        {
            var mesh = CreateQuadStrip(1);
            Ahvdt.Process(mesh, new Ahvdt.FixedParameters
            {
                TransferTarget = VertexDataTransferTarget.VertexColor,
                TransferMode = VertexDataTransferMode.CustomExpression,
            }, null);
            Assert.That(mesh.colors, Is.Empty);
        }

        private Texture2D CreateQuadrantTexture()
        {
            return CreateTexture(
                2, 2,
                new Color(0.1f, 0.2f, 0.3f, 1.0f),
                new Color(0.4f, 0.5f, 0.6f, 0.5f),
                new Color(0.7f, 0.8f, 0.9f, 0.25f),
                new Color(1.0f, 0.0f, 0.5f, 0.0f));
        }

        private Mesh CreateQuadrantSampler(int uvChannel)
        {
            var mesh = Track(new Mesh());
            mesh.vertices = new[] { new Vector3(1, 2, 3), new Vector3(4, 5, 6), new Vector3(7, 8, 9), new Vector3(-1, -2, -3) };
            mesh.SetUVs(uvChannel, new List<Vector2> { new Vector2(0.25f, 0.25f), new Vector2(0.75f, 0.25f), new Vector2(0.25f, 0.75f), new Vector2(0.75f, 0.75f) });
            return mesh;
        }

        [Test]
        [Category("GPU")]
        public void CopiesSampledColorsToVertexColors()
        {
            AssumeComputeShaders();

            var texture = CreateQuadrantTexture();
            var mesh = CreateQuadrantSampler(1);
            Ahvdt.Process(mesh, new Ahvdt.FixedParameters
            {
                SourceTexture = texture,
                SourceUv = UvChannel.UV1,
                TransferTarget = VertexDataTransferTarget.VertexColor,
                TransferMode = VertexDataTransferMode.Copy,
            }, null);

            AssertVectors(texture.GetPixels().Select((c) => (Vector4)c).ToArray(), mesh.colors.Select((c) => (Vector4)c).ToArray());
        }

        [Test]
        [Category("GPU")]
        public void WritesTransferredValuesToUvChannel()
        {
            AssumeComputeShaders();

            var texture = CreateQuadrantTexture();
            var mesh = CreateQuadrantSampler(0);
            Ahvdt.Process(mesh, new Ahvdt.FixedParameters
            {
                SourceTexture = texture,
                SourceUv = UvChannel.UV0,
                TransferTarget = VertexDataTransferTarget.UV5,
                TransferMode = VertexDataTransferMode.OneMinus,
            }, null);

            var uvs = new List<Vector4>();
            mesh.GetUVs(5, uvs);
            AssertVectors(texture.GetPixels().Select((c) => Vector4.one - (Vector4)c).ToArray(), uvs);
            Assert.That(mesh.colors, Is.Empty);
        }

        [Test]
        [Category("GPU")]
        public void EvaluatesCustomExpressionPerVertex()
        {
            AssumeComputeShaders();

            var texture = CreateQuadrantTexture();
            var mesh = CreateQuadrantSampler(0);
            var parameters = new Ahvdt.FixedParameters
            {
                SourceTexture = texture,
                SourceUv = UvChannel.UV0,
                TransferTarget = VertexDataTransferTarget.UV3,
                TransferMode = VertexDataTransferMode.CustomExpression,
                Expression = "@position ... @color #a vec4 @uv #xyxy +",
            };
            Assert.That(Ahvdt.TryCompileExpression(parameters, out var program), Is.True);
            Ahvdt.Process(mesh, parameters, program);

            var uvs = new List<Vector4>();
            mesh.GetUVs(3, uvs);
            var pixels = texture.GetPixels();
            var vertices = mesh.vertices;
            var sourceUvs = mesh.uv;
            var expected = Enumerable.Range(0, 4)
                .Select((i) => new Vector4(vertices[i].x, vertices[i].y, vertices[i].z, pixels[i].a) + new Vector4(sourceUvs[i].x, sourceUvs[i].y, sourceUvs[i].x, sourceUvs[i].y))
                .ToArray();
            AssertVectors(expected, uvs);
        }
    }
}
