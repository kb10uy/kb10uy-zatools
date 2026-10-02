using NUnit.Framework;
using Unity.Mathematics;
using KusakaFactory.Zatools.Foundation.Arithmetic;
using static KusakaFactory.Zatools.Tests.Foundation.Arithmetic.Zax;

namespace KusakaFactory.Zatools.Tests.Foundation.Arithmetic
{
    public sealed class ZaxValueTests
    {
        private const ZaxValueType Int = ZaxValueType.Int;
        private const ZaxValueType Float = ZaxValueType.Float;
        private const ZaxValueType Float2 = ZaxValueType.Float2;
        private const ZaxValueType Float3 = ZaxValueType.Float3;
        private const ZaxValueType Float4 = ZaxValueType.Float4;

        [TestCase(Int, Int, Int)]
        [TestCase(Int, Float, Float)]
        [TestCase(Float, Int, Float)]
        [TestCase(Int, Float2, Float2)]
        [TestCase(Float3, Float, Float3)]
        [TestCase(Float4, Float4, Float4)]
        public void PromotesCompatibleTypes(ZaxValueType left, ZaxValueType right, ZaxValueType expected)
        {
            Assert.That(ZaxValueTypeEx.TryPromote(left, right, out var promoted), Is.True);
            Assert.That(promoted, Is.EqualTo(expected));
        }

        [TestCase(Float2, Float3)]
        [TestCase(Float4, Float2)]
        public void RejectsMixedVectorDimensions(ZaxValueType left, ZaxValueType right)
        {
            Assert.That(ZaxValueTypeEx.TryPromote(left, right, out _), Is.False);
        }

        [TestCase(Int, 1, 4)]
        [TestCase(Float, 1, 4)]
        [TestCase(Float2, 2, 8)]
        [TestCase(Float3, 3, 12)]
        [TestCase(Float4, 4, 16)]
        public void ReportsDimensionAndByteSize(ZaxValueType type, int dimension, int byteSize)
        {
            Assert.That(type.Dimension(), Is.EqualTo(dimension));
            Assert.That(type.ByteSize(), Is.EqualTo(byteSize));
        }

        [TestCase(new ZaxValueType[0], "<none>")]
        [TestCase(new[] { Float3 }, "float3")]
        [TestCase(new[] { Float, Float, Float }, "float x 3")]
        [TestCase(new[] { Int, Float2 }, "int, float2")]
        public void DescribesTypeLists(ZaxValueType[] types, string expected)
        {
            Assert.That(ZaxValueTypeEx.DisplayName(types), Is.EqualTo(expected));
        }

        [Test]
        public void ConvertsBetweenTypes()
        {
            AreEqual(F(3.0f), I(3).ConvertTo(Float));
            AreEqual(I(2), F(2.7f).ConvertTo(Int));
            AreEqual(I(-2), F(-2.7f).ConvertTo(Int));
            AreEqual(F3(2.0f, 2.0f, 2.0f), F(2.0f).ConvertTo(Float3));
            AreEqual(F4(5.0f, 5.0f, 5.0f, 5.0f), I(5).ConvertTo(Float4));
            AreEqual(F(1.0f), F3(1.0f, 2.0f, 3.0f).ConvertTo(Float));
            AreEqual(F2(1.0f, 2.0f), F3(1.0f, 2.0f, 3.0f).ConvertTo(Float2));
            AreEqual(F4(1.0f, 2.0f, 0.0f, 0.0f), F2(1.0f, 2.0f).ConvertTo(Float4));
        }

        [Test]
        public void ComparesByTypeAndBits()
        {
            Assert.That(F(1.0f), Is.EqualTo(F(1.0f)));
            Assert.That(F(1.0f), Is.Not.EqualTo(I(1)));
            Assert.That(F(0.0f), Is.Not.EqualTo(F(-0.0f)));
            Assert.That(F2(1.0f, 2.0f), Is.Not.EqualTo(F3(1.0f, 2.0f, 0.0f)));
            Assert.That(ZaxValue.FromFloat(math.NAN), Is.EqualTo(ZaxValue.FromFloat(math.NAN)));
        }

        [Test]
        public void FormatsInvariantly()
        {
            Assert.That(I(-3).ToString(), Is.EqualTo("-3"));
            Assert.That(F(1.5f).ToString(), Is.EqualTo("1.5"));
            Assert.That(F3(1.0f, -0.25f, 2.0f).ToString(), Is.EqualTo("(1, -0.25, 2)"));
        }
    }
}
