using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using KusakaFactory.Zatools.Foundation.Arithmetic;
using static KusakaFactory.Zatools.Tests.Foundation.Arithmetic.Zax;

namespace KusakaFactory.Zatools.Tests.Foundation.Arithmetic
{
    public sealed class ZaxEvaluatorTests
    {
        private static IEnumerable<TestCaseData> IntegerCases()
        {
            yield return new TestCaseData("1 2 +", I(3));
            yield return new TestCaseData("5 7 -", I(-2));
            yield return new TestCaseData("6 7 *", I(42));
            yield return new TestCaseData("7 2 //", I(3));
            yield return new TestCaseData("-7 2 //", I(-3));
            yield return new TestCaseData("7 0 //", I(0));
            yield return new TestCaseData("7 3 %", I(1));
            yield return new TestCaseData("-7 3 %", I(-1));
            yield return new TestCaseData("7 0 %", I(0));
            yield return new TestCaseData("4 neg", I(-4));
            yield return new TestCaseData("-3 abs", I(3));
            yield return new TestCaseData("-5 sign", I(-1));
            yield return new TestCaseData("0 sign", I(0));
            yield return new TestCaseData("3 sign", I(1));
            yield return new TestCaseData("3 8 min", I(3));
            yield return new TestCaseData("3 8 max", I(8));
            yield return new TestCaseData("5 1 3 clamp", I(3));
            yield return new TestCaseData("-5 1 3 clamp", I(1));
            yield return new TestCaseData("1 2 add 3 mul", I(9));
        }

        private static IEnumerable<TestCaseData> ScalarCases()
        {
            yield return new TestCaseData("1 2.5 +", F(3.5f));
            yield return new TestCaseData("7 2 /", F(3.5f));
            yield return new TestCaseData("7.5 2 %", F(1.5f));
            yield return new TestCaseData("-2.5 abs", F(2.5f));
            yield return new TestCaseData("-0.5 sign", F(-1.0f));
            yield return new TestCaseData("1.5 saturate", F(1.0f));
            yield return new TestCaseData("-0.5 saturate", F(0.0f));
            yield return new TestCaseData("2.7 floor", F(2.0f));
            yield return new TestCaseData("-2.2 floor", F(-3.0f));
            yield return new TestCaseData("2.2 ceil", F(3.0f));
            yield return new TestCaseData("2.6 round", F(3.0f));
            yield return new TestCaseData("-1.25 frac", F(0.75f));
            yield return new TestCaseData("4 sqrt", F(2.0f));
            yield return new TestCaseData("4 rsqrt", F(0.5f));
            yield return new TestCaseData("2 10 pow", F(1024.0f));
            yield return new TestCaseData("0 exp", F(1.0f));
            yield return new TestCaseData("3 exp2", F(8.0f));
            yield return new TestCaseData("E log", F(1.0f));
            yield return new TestCaseData("8 log2", F(3.0f));
            yield return new TestCaseData("100 log10", F(2.0f));
            yield return new TestCaseData("HALF_PI sin", F(1.0f));
            yield return new TestCaseData("PI cos", F(-1.0f));
            yield return new TestCaseData("PI 4 / tan", F(1.0f));
            yield return new TestCaseData("1 asin", F(math.PI / 2));
            yield return new TestCaseData("-1 acos", F(math.PI));
            yield return new TestCaseData("1 atan", F(math.PI / 4));
            yield return new TestCaseData("1 -1 atan2", F(math.PI * 3 / 4));
            yield return new TestCaseData("PI degrees", F(180.0f));
            yield return new TestCaseData("180 radians", F(math.PI));
            yield return new TestCaseData("0 10 0.25 lerp", F(2.5f));
            yield return new TestCaseData("0.5 0.3 step", F(0.0f));
            yield return new TestCaseData("0.5 0.7 step", F(1.0f));
            yield return new TestCaseData("0 1 0.5 smoothstep", F(0.5f));
            yield return new TestCaseData("0 1 0.25 smoothstep", F(0.15625f));
            yield return new TestCaseData("TAU", F(math.PI * 2));
            yield return new TestCaseData("TRUE FALSE +", F(1.0f));
        }

        private static IEnumerable<TestCaseData> ComparisonCases()
        {
            yield return new TestCaseData("1 2 <", F(1.0f));
            yield return new TestCaseData("1 2 >", F(0.0f));
            yield return new TestCaseData("2 2 <=", F(1.0f));
            yield return new TestCaseData("2 2 >=", F(1.0f));
            yield return new TestCaseData("2 2.0 ==", F(1.0f));
            yield return new TestCaseData("2 3 !=", F(1.0f));
            yield return new TestCaseData("0 !", F(1.0f));
            yield return new TestCaseData("3 not", F(0.0f));
            yield return new TestCaseData("1 5 vec2 3 3 vec2 lt", F2(1.0f, 0.0f));
            yield return new TestCaseData("1 0 2 vec3 not", F3(0.0f, 1.0f, 0.0f));
        }

        private static IEnumerable<TestCaseData> VectorCases()
        {
            yield return new TestCaseData("1 2 3 vec3 2 *", F3(2.0f, 4.0f, 6.0f));
            yield return new TestCaseData("2 1 2 3 vec3 -", F3(1.0f, 0.0f, -1.0f));
            yield return new TestCaseData("1 2 vec2 3 4 vec2 +", F2(4.0f, 6.0f));
            yield return new TestCaseData("1 2 3 4 vec4 4 3 2 1 vec4 *", F4(4.0f, 6.0f, 6.0f, 4.0f));
            yield return new TestCaseData("1 4 vec2 2 /", F2(0.5f, 2.0f));
            yield return new TestCaseData("1 5 vec2 2 3 vec2 min", F2(1.0f, 3.0f));
            yield return new TestCaseData("-1 0.5 2 vec3 0 1 clamp", F3(0.0f, 0.5f, 1.0f));
            yield return new TestCaseData("1 2 3 vec3 4 5 6 vec3 dot", F(32.0f));
            yield return new TestCaseData("X_AXIS Y_AXIS cross", F3(0.0f, 0.0f, 1.0f));
            yield return new TestCaseData("3 4 vec2 length", F(5.0f));
            yield return new TestCaseData("3 4 vec2 lengthsq", F(25.0f));
            yield return new TestCaseData("1 1 vec2 4 5 vec2 distance", F(5.0f));
            yield return new TestCaseData("3 0 0 vec3 normalize", F3(1.0f, 0.0f, 0.0f));
            yield return new TestCaseData("1 -1 vec2 0 1 vec2 reflect", F2(1.0f, 1.0f));
            yield return new TestCaseData("0 0 vec2 2 4 vec2 0.5 lerp", F2(1.0f, 2.0f));
            yield return new TestCaseData("1 2 3 4 vec4", F4(1.0f, 2.0f, 3.0f, 4.0f));
        }

        private static IEnumerable<TestCaseData> SwizzleAndStackCases()
        {
            yield return new TestCaseData("1 2 3 4 vec4 #wzyx", F4(4.0f, 3.0f, 2.0f, 1.0f));
            yield return new TestCaseData("1 2 3 4 vec4 #ba", F2(3.0f, 4.0f));
            yield return new TestCaseData("1 2 3 vec3 #s", F(3.0f));
            yield return new TestCaseData("1 2 vec2 #xxx", F3(1.0f, 1.0f, 1.0f));
            yield return new TestCaseData("1 2 3 vec3 ... + +", F(6.0f));
            yield return new TestCaseData("1 2 vec2 unpack -", F(-1.0f));
            yield return new TestCaseData("1 dup +", I(2));
            yield return new TestCaseData("1 2 drop", I(1));
            yield return new TestCaseData("1 2 swap -", I(1));
        }

        [TestCaseSource(nameof(IntegerCases))]
        [TestCaseSource(nameof(ScalarCases))]
        [TestCaseSource(nameof(ComparisonCases))]
        [TestCaseSource(nameof(VectorCases))]
        [TestCaseSource(nameof(SwizzleAndStackCases))]
        public void EvaluatesSingleResult(string source, ZaxValue expected)
        {
            AreEqual(expected, Evaluate(source));
        }

        private static IEnumerable<TestCaseData> MultipleResultCases()
        {
            yield return new TestCaseData("1 2.5 1 2 vec2", new[] { I(1), F(2.5f), F2(1.0f, 2.0f) });
            yield return new TestCaseData("1 2 over", new[] { I(1), I(2), I(1) });
            yield return new TestCaseData("1 2 3 rot", new[] { I(2), I(3), I(1) });
            yield return new TestCaseData("1 2 3 vec3 ...", new[] { F(1.0f), F(2.0f), F(3.0f) });
        }

        [TestCaseSource(nameof(MultipleResultCases))]
        public void EvaluatesMultipleResults(string source, ZaxValue[] expected)
        {
            AreEqual(expected, EvaluateAll(source));
        }

        [Test]
        public void ConvertsColorSpaces()
        {
            AreEqual(F3(1.0f, 0.0f, 0.0f), Evaluate("1 1 1 vec3 rgb2yuv"), 1e-4f);
            AreEqual(F3(0.2f, 0.5f, 0.8f), Evaluate("0.2 0.5 0.8 vec3 rgb2yuv yuv2rgb"), 1e-3f);
            AreEqual(F(0.0f), Evaluate("0 srgb2linear"));
            AreEqual(F(1.0f), Evaluate("1 srgb2linear"));
            AreEqual(F(0.21404f), Evaluate("0.5 srgb2linear"), 1e-4f);
            AreEqual(F(0.02f / 12.92f), Evaluate("0.02 srgb2linear"));
            AreEqual(F4(0.1f, 0.3f, 0.5f, 0.9f), Evaluate("0.1 0.3 0.5 0.9 vec4 srgb2linear linear2srgb"), 1e-5f);
        }

        [Test]
        public void ReadsVariablesInProgramOrder()
        {
            var program = Compile("@b @a *", new ZaxVariable("a", ZaxValueType.Float), new ZaxVariable("b", ZaxValueType.Float3));
            Assert.That(program.Variables[0].Name, Is.EqualTo("b"));

            var variables = new[] { F3(1.0f, 2.0f, 3.0f), F(2.0f) };
            AreEqual(F3(2.0f, 4.0f, 6.0f), ZaxEvaluator.Evaluate(program, variables));
        }

        [Test]
        public void SingleResultOverloadRejectsMultipleResults()
        {
            var program = Compile("1 2");
            Assert.That(() => ZaxEvaluator.Evaluate(program), Throws.ArgumentException);
        }

        [Test]
        public void MultipleResultOverloadRejectsShortBuffer()
        {
            var program = Compile("1 2");
            Assert.That(() => ZaxEvaluator.Evaluate(program, ReadOnlySpan<ZaxValue>.Empty, new ZaxValue[1]), Throws.ArgumentException);
        }
    }
}
