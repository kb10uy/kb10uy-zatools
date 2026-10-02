using NUnit.Framework;
using UnityEngine;
using KusakaFactory.Zatools.Foundation;

namespace KusakaFactory.Zatools.Tests.Foundation
{
    public sealed class ExtraMathTests
    {
        private static void AssertMatrix(Matrix4x4 expected, Matrix4x4 actual)
        {
            for (var i = 0; i < 16; ++i) Assert.That(actual[i], Is.EqualTo(expected[i]).Within(1e-5f), $"element {i}: expected\n{expected}\nbut was\n{actual}");
        }

        private static readonly Matrix4x4[] Sources =
        {
            Matrix4x4.TRS(new Vector3(1, 2, 3), Quaternion.identity, Vector3.one),
            Matrix4x4.TRS(new Vector3(-3, 0, 1), Quaternion.Euler(0, 90, 0), Vector3.one),
            Matrix4x4.Scale(new Vector3(2, 4, 8)),
        };

        [Test]
        public void SingleFullWeightReturnsSource()
        {
            AssertMatrix(Sources[1], ExtraMath.BlendMatrices(Sources, (1, 1.0f)));
        }

        [Test]
        public void BlendsTranslationsLinearly()
        {
            var blended = ExtraMath.BlendMatrices(Sources, (0, 0.5f), (1, 0.5f));
            Assert.That(blended.GetColumn(3), Is.EqualTo(new Vector4(-1, 1, 2, 1)));
        }

        [Test]
        public void WeightsAreNotNormalized()
        {
            var expected = new Matrix4x4(new Vector4(1, 0, 0, 0), new Vector4(0, 2, 0, 0), new Vector4(0, 0, 4, 0), new Vector4(0, 0, 0, 0.5f));
            AssertMatrix(expected, ExtraMath.BlendMatrices(Sources, (2, 0.5f)));
        }

        [Test]
        public void SameIndexAccumulates()
        {
            AssertMatrix(ExtraMath.BlendMatrices(Sources, (0, 1.0f)), ExtraMath.BlendMatrices(Sources, (0, 0.25f), (0, 0.75f)));
        }

        [Test]
        public void NoWeightsYieldsZero()
        {
            AssertMatrix(Matrix4x4.zero, ExtraMath.BlendMatrices(Sources));
        }
    }
}
