using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KusakaFactory.Zatools.Ndmf.Core;
using KusakaFactory.Zatools.Runtime;

namespace KusakaFactory.Zatools.Tests.Ndmf.Core
{
    public sealed class AhnbTests : MeshTestBase
    {
        private (SkinnedMeshRenderer Renderer, Mesh Mesh, Transform[] Bones) CreateSkinnedStrip(Vector3 normal)
        {
            var mesh = CreateQuadStrip(1);
            mesh.normals = Enumerable.Repeat(normal, mesh.vertexCount).ToArray();
            mesh.boneWeights = new[]
            {
                new BoneWeight { boneIndex0 = 0, weight0 = 1.0f },
                new BoneWeight { boneIndex0 = 0, weight0 = 0.7f, boneIndex1 = 1, weight1 = 0.3f },
                new BoneWeight { boneIndex0 = 0, weight0 = 0.995f, boneIndex1 = 2, weight1 = 0.005f },
                new BoneWeight { boneIndex0 = 1, weight0 = 1.0f },
            };
            mesh.bindposes = Enumerable.Repeat(Matrix4x4.identity, 3).ToArray();

            var bones = CreateBones(3);
            var renderer = CreateRenderer(mesh);
            renderer.bones = bones;
            return (renderer, mesh, bones);
        }

        private static Ahnb.FixedParameters Parameters(Vector3 forward, float weight, NormalBendMaskMode mode = NormalBendMaskMode.White)
        {
            return new Ahnb.FixedParameters { WorldSpaceForward = forward, Weight = weight, MaskMode = mode };
        }

        [Test]
        public void FullWeightPointsNormalsAlongForward()
        {
            var (renderer, mesh, _) = CreateSkinnedStrip(new Vector3(0, 0, -1));
            var influent = Ahnb.Process(renderer, mesh, Parameters(new Vector3(1, 0, 0), 1.0f));

            foreach (var normal in mesh.normals) AssertVector(new Vector3(1, 0, 0), normal);
            Assert.That(influent, Is.EquivalentTo(new[] { 0, 1 }));
        }

        [Test]
        public void ZeroWeightKeepsNormals()
        {
            var (renderer, mesh, _) = CreateSkinnedStrip(new Vector3(0, 0, -1));
            Ahnb.Process(renderer, mesh, Parameters(new Vector3(1, 0, 0), 0.0f));

            foreach (var normal in mesh.normals) AssertVector(new Vector3(0, 0, -1), normal);
        }

        [Test]
        public void PartialWeightSlerpsTowardForward()
        {
            var (renderer, mesh, _) = CreateSkinnedStrip(new Vector3(0, 0, 1));
            Ahnb.Process(renderer, mesh, Parameters(new Vector3(1, 0, 0), 0.5f));

            var half = Mathf.Sqrt(0.5f);
            foreach (var normal in mesh.normals) AssertVector(new Vector3(half, 0, half), normal);
        }

        [Test]
        public void ForwardIsConvertedIntoBoneSpace()
        {
            var (renderer, mesh, bones) = CreateSkinnedStrip(new Vector3(0, 0, -1));
            foreach (var bone in bones) bone.rotation = Quaternion.Euler(0, 90, 0);
            Ahnb.Process(renderer, mesh, Parameters(new Vector3(0, 0, 1), 1.0f));

            foreach (var normal in mesh.normals) AssertVector(new Vector3(-1, 0, 0), normal);
        }

        [Test]
        public void BlackModeWithoutMaskLeavesNormalsAndReportsNoBones()
        {
            var (renderer, mesh, _) = CreateSkinnedStrip(new Vector3(0, 0, -1));
            var influent = Ahnb.Process(renderer, mesh, Parameters(new Vector3(1, 0, 0), 1.0f, NormalBendMaskMode.Black));

            foreach (var normal in mesh.normals) AssertVector(new Vector3(0, 0, -1), normal);
            Assert.That(influent, Is.Empty);
        }

        [Test]
        public void BendsMeshWithoutBoneWeightsInRendererSpace()
        {
            var mesh = CreateQuadStrip(1);
            mesh.normals = Enumerable.Repeat(new Vector3(0, 0, -1), mesh.vertexCount).ToArray();
            var renderer = CreateRenderer(mesh);
            renderer.transform.rotation = Quaternion.Euler(0, 90, 0);

            var influent = Ahnb.Process(renderer, mesh, Parameters(new Vector3(0, 0, 1), 1.0f));

            foreach (var normal in mesh.normals) AssertVector(new Vector3(-1, 0, 0), normal);
            Assert.That(influent, Is.Empty);
        }

        [Test]
        [Category("GPU")]
        public void MaskLimitsAffectedVertices()
        {
            AssumeComputeShaders();

            var (renderer, mesh, _) = CreateSkinnedStrip(new Vector3(0, 0, -1));
            mesh.uv = new[] { new Vector2(0.25f, 0.5f), new Vector2(0.25f, 0.5f), new Vector2(0.75f, 0.5f), new Vector2(0.75f, 0.5f) };
            var parameters = Parameters(new Vector3(1, 0, 0), 1.0f);
            parameters.MaskTexture = CreateTexture(2, 1, Color.black, Color.white);
            var influent = Ahnb.Process(renderer, mesh, parameters);

            var normals = mesh.normals;
            AssertVector(new Vector3(0, 0, -1), normals[0]);
            AssertVector(new Vector3(0, 0, -1), normals[1]);
            AssertVector(new Vector3(1, 0, 0), normals[2]);
            AssertVector(new Vector3(1, 0, 0), normals[3]);
            Assert.That(influent, Is.EquivalentTo(new[] { 0, 1 }));
        }
    }
}
