using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using KusakaFactory.Zatools.Foundation.Arithmetic;
using static KusakaFactory.Zatools.Tests.Foundation.Arithmetic.Zax;

namespace KusakaFactory.Zatools.Tests.Foundation.Arithmetic
{
    public sealed class ZaxEvaluatorMatrixTests
    {
        private const string A = "1 2 vec2 3 4 vec2";
        private const string Swap = "0 1 vec2 1 0 vec2";

        private static ZaxValue[] Rows2(float m00, float m01, float m10, float m11) => new[] { F2(m00, m01), F2(m10, m11) };

        private static ZaxValue[] Identity(int n)
        {
            var rows = new ZaxValue[n];
            for (var i = 0; i < n; ++i)
            {
                var row = float4.zero;
                row[i] = 1.0f;
                rows[i] = ZaxValue.FromFloatN(row, ZaxValueTypeEx.OfDimension(n));
            }
            return rows;
        }

        private static IEnumerable<TestCaseData> MatrixResultCases()
        {
            yield return new TestCaseData("Midentity2", Identity(2));
            yield return new TestCaseData("Midentity3", Identity(3));
            yield return new TestCaseData("Midentity4", Identity(4));
            yield return new TestCaseData($"{A} {Swap} Mmul", Rows2(2, 1, 4, 3));
            yield return new TestCaseData($"{Swap} {A} M*", Rows2(3, 4, 1, 2));
            yield return new TestCaseData($"{A} {Swap} M+", Rows2(1, 3, 4, 4));
            yield return new TestCaseData($"{A} {Swap} Msub", Rows2(1, 1, 2, 4));
            yield return new TestCaseData($"{A} 2 Mscale", Rows2(2, 4, 6, 8));
            yield return new TestCaseData($"{A} Mtranspose", Rows2(1, 3, 2, 4));
            yield return new TestCaseData("2 0 vec2 0 4 vec2 Minverse", Rows2(0.5f, 0, 0, 0.25f));
            yield return new TestCaseData("4 7 vec2 2 6 vec2 Mdup Minverse Mmul", Identity(2));
            yield return new TestCaseData("1 2 3 vec3 Mdiag", new[] { F3(1, 0, 0), F3(0, 2, 0), F3(0, 0, 3) });
            yield return new TestCaseData("1 2 vec2 3 4 vec2 Mouter", Rows2(3, 4, 6, 8));
            yield return new TestCaseData($"{A} Mto3", new[] { F3(1, 2, 0), F3(3, 4, 0), F3(0, 0, 1) });
            yield return new TestCaseData("Midentity2 Mto4", Identity(4));
            yield return new TestCaseData("1 2 3 vec3 Mtranslate", new[] { F4(1, 0, 0, 1), F4(0, 1, 0, 2), F4(0, 0, 1, 3), F4(0, 0, 0, 1) });
            yield return new TestCaseData("1 2 3 vec3 Mtranslate Mto3", Identity(3));
            yield return new TestCaseData("Z_AXIS Y_AXIS Mlookrot", Identity(3));
            yield return new TestCaseData($"{A} Mdup", new[] { F2(1, 2), F2(3, 4), F2(1, 2), F2(3, 4) });
            yield return new TestCaseData($"1 {A} Mdrop", new[] { I(1) });
            yield return new TestCaseData(
                "Midentity2 1 2 3 vec3 4 5 6 vec3 7 8 9 vec3 Mswap",
                new[] { F3(1, 2, 3), F3(4, 5, 6), F3(7, 8, 9), F2(1, 0), F2(0, 1) });
            yield return new TestCaseData(
                "1 2 3 vec3 4 5 6 vec3 7 8 9 vec3 Midentity2 Mswap",
                new[] { F2(1, 0), F2(0, 1), F3(1, 2, 3), F3(4, 5, 6), F3(7, 8, 9) });
        }

        [TestCaseSource(nameof(MatrixResultCases))]
        public void EvaluatesMatrixResults(string source, ZaxValue[] expected)
        {
            AreEqual(expected, EvaluateAll(source));
        }

        private static IEnumerable<TestCaseData> ReducingCases()
        {
            yield return new TestCaseData($"{A} Mdet", F(-2));
            yield return new TestCaseData("Midentity4 Mdet", F(1));
            yield return new TestCaseData("2 3 4 vec3 Mdiag Mdet", F(24));
            yield return new TestCaseData($"{A} Mtrace", F(5));
            yield return new TestCaseData("Midentity3 Mtrace", F(3));
            yield return new TestCaseData($"{A} Mdiagv", F2(1, 4));
            yield return new TestCaseData($"{A} 1 1 vec2 Mmulv", F2(3, 7));
            yield return new TestCaseData($"{A} Mtranspose 1 1 vec2 Mmulv", F2(4, 6));
            yield return new TestCaseData("1 2 3 vec3 Mtranslate 10 20 30 vec3 Mtfpoint", F3(11, 22, 33));
            yield return new TestCaseData("1 2 3 vec3 Mtranslate 10 20 30 vec3 Mtfdir", F3(10, 20, 30));
            yield return new TestCaseData("1 0 0 vec3 Mtranslate 0 2 0 vec3 Mtranslate M* 0 0 0 vec3 Mtfpoint", F3(1, 2, 0));
            yield return new TestCaseData("2 2 2 vec3 Mdiag Mto4 1 1 1 vec3 Mtranslate M* 1 0 0 vec3 Mtfpoint", F3(4, 2, 2));
            yield return new TestCaseData("1 1 1 vec3 Mtranslate 2 2 2 vec3 Mdiag Mto4 M* 1 0 0 vec3 Mtfpoint", F3(3, 1, 1));
        }

        [TestCaseSource(nameof(ReducingCases))]
        public void EvaluatesReducingFunctions(string source, ZaxValue expected)
        {
            AreEqual(expected, Evaluate(source));
        }

        private static IEnumerable<TestCaseData> RotationCases()
        {
            yield return new TestCaseData("HALF_PI Mrotx Y_AXIS Mmulv", F3(0, 0, 1));
            yield return new TestCaseData("HALF_PI Mroty Z_AXIS Mmulv", F3(1, 0, 0));
            yield return new TestCaseData("HALF_PI Mrotz X_AXIS Mmulv", F3(0, 1, 0));
            yield return new TestCaseData("Z_AXIS HALF_PI Mrotaxis X_AXIS Mmulv", F3(0, 1, 0));
            yield return new TestCaseData("0 0 HALF_PI vec3 Mroteuler X_AXIS Mmulv", F3(0, 1, 0));
            yield return new TestCaseData("Z_AXIS HALF_PI Qaxisangle Mfromq X_AXIS Mmulv", F3(0, 1, 0));
            yield return new TestCaseData("X_AXIS Y_AXIS Mlookrot Z_AXIS Mmulv", F3(1, 0, 0));
            yield return new TestCaseData("HALF_PI Mrotz Mtranspose X_AXIS Mmulv", F3(0, -1, 0));
            yield return new TestCaseData("HALF_PI Mrotz Minverse X_AXIS Mmulv", F3(0, -1, 0));
            yield return new TestCaseData("HALF_PI Mrotz HALF_PI Mrotx M* Y_AXIS Mmulv", F3(0, 0, 1));
            yield return new TestCaseData("HALF_PI Mrotx HALF_PI Mrotz M* Y_AXIS Mmulv", F3(-1, 0, 0));
        }

        [TestCaseSource(nameof(RotationCases))]
        public void RotatesColumnVectors(string source, ZaxValue expected)
        {
            AreEqual(expected, Evaluate(source));
        }

        [Test]
        public void RoundTripsQuaternionThroughMatrix()
        {
            var expected = quaternion.AxisAngle(math.normalize(new float3(1, 2, 3)), 0.7f).value;
            var actual = Evaluate("1 2 3 vec3 normalize 0.7 Qaxisangle Mfromq Qfromm").AsFloat4;
            Assert.That(math.abs(math.dot(expected, actual)), Is.EqualTo(1.0f).Within(Tolerance));
        }

        [Test]
        public void MatrixFromRotationAgreesWithQuaternion()
        {
            var fromMatrix = Evaluate("0.3 -0.4 1.1 vec3 Mroteuler 1 2 3 vec3 Mmulv");
            var fromQuaternion = Evaluate("0.3 -0.4 1.1 vec3 Qeuler 1 2 3 vec3 Qrotate");
            AreEqual(fromQuaternion, fromMatrix);
        }
    }
}
