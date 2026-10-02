using NUnit.Framework;
using KusakaFactory.Zatools.Foundation.Arithmetic;

namespace KusakaFactory.Zatools.Tests.Foundation.Arithmetic
{
    public sealed class ZaxCompilerTests
    {
        private const ZaxValueType Int = ZaxValueType.Int;
        private const ZaxValueType Float = ZaxValueType.Float;
        private const ZaxValueType Float2 = ZaxValueType.Float2;
        private const ZaxValueType Float3 = ZaxValueType.Float3;
        private const ZaxValueType Float4 = ZaxValueType.Float4;

        [TestCase("1", Int)]
        [TestCase("1.0", Float)]
        [TestCase("1 2 +", Int)]
        [TestCase("1 2.0 +", Float)]
        [TestCase("1 2 /", Float)]
        [TestCase("7 2 //", Int)]
        [TestCase("7 2 %", Int)]
        [TestCase("-3 abs", Int)]
        [TestCase("4 sqrt", Float)]
        [TestCase("1 2 <", Float)]
        [TestCase("1 2 vec2", Float2)]
        [TestCase("1 2 vec2 3 *", Float2)]
        [TestCase("3 1 2 vec2 *", Float2)]
        [TestCase("1 2 3 vec3 dup dot", Float)]
        [TestCase("1 2 3 vec3 length", Float)]
        [TestCase("1 2 3 vec3 dup cross", Float3)]
        [TestCase("1 2 3 4 vec4 #x", Float)]
        [TestCase("1 2 3 4 vec4 #zy", Float2)]
        [TestCase("1 2 vec2 #xxyy", Float4)]
        [TestCase("PI", Float)]
        [TestCase("X_AXIS", Float3)]
        [TestCase("Q_IDENTITY", Float4)]
        [TestCase("Midentity3 Mdet", Float)]
        [TestCase("Midentity4 1 2 3 vec3 Mtfpoint", Float3)]
        [TestCase("Midentity3 Qfromm", Float4)]
        [TestCase("Q_IDENTITY X_AXIS Qrotate", Float3)]
        [TestCase("X_AXIS 1 Qaxisangle", Float4)]
        public void InfersSingleResultType(string source, ZaxValueType expected)
        {
            var program = Zax.Compile(source);
            Assert.That(program.ResultTypes, Is.EqualTo(new[] { expected }));
            Assert.That(program.ResultType, Is.EqualTo(expected));
        }

        [TestCase("1 2.0", new[] { Int, Float })]
        [TestCase("1 2 vec2 ...", new[] { Float, Float })]
        [TestCase("1 2 3 vec3 unpack", new[] { Float, Float, Float })]
        [TestCase("1 2.0 swap", new[] { Float, Int })]
        [TestCase("1 2.0 over", new[] { Int, Float, Int })]
        [TestCase("1 2.0 X_AXIS rot", new[] { Float, Float3, Int })]
        [TestCase("Midentity2", new[] { Float2, Float2 })]
        [TestCase("Midentity3 Mto2", new[] { Float2, Float2 })]
        [TestCase("Midentity2 Mto4", new[] { Float4, Float4, Float4, Float4 })]
        [TestCase("Midentity2 Mdup", new[] { Float2, Float2, Float2, Float2 })]
        [TestCase("1 Midentity3 Mdrop", new[] { Int })]
        [TestCase("Midentity2 Midentity3 Mswap", new[] { Float3, Float3, Float3, Float2, Float2 })]
        [TestCase("Q_IDENTITY Mfromq", new[] { Float3, Float3, Float3 })]
        [TestCase("X_AXIS 1 Mrotaxis", new[] { Float3, Float3, Float3 })]
        [TestCase("X_AXIS Mtranslate", new[] { Float4, Float4, Float4, Float4 })]
        public void InfersMultipleResultTypes(string source, ZaxValueType[] expected)
        {
            Assert.That(Zax.Compile(source).ResultTypes, Is.EqualTo(expected));
        }

        [TestCase("", ZaxDiagnosticCode.EmptyExpression)]
        [TestCase("; only a comment", ZaxDiagnosticCode.EmptyExpression)]
        [TestCase("1 drop", ZaxDiagnosticCode.EmptyExpression)]
        [TestCase("#q", ZaxDiagnosticCode.InvalidSwizzle)]
        [TestCase("foo", ZaxDiagnosticCode.UnknownName)]
        [TestCase("@x", ZaxDiagnosticCode.UnknownVariable)]
        [TestCase("+", ZaxDiagnosticCode.NotEnoughOperands)]
        [TestCase("1 +", ZaxDiagnosticCode.NotEnoughOperands)]
        [TestCase("dup", ZaxDiagnosticCode.NotEnoughOperands)]
        [TestCase("1 2 rot", ZaxDiagnosticCode.NotEnoughOperands)]
        [TestCase("#x", ZaxDiagnosticCode.NotEnoughOperands)]
        [TestCase("...", ZaxDiagnosticCode.NotEnoughOperands)]
        [TestCase("7.0 2 //", ZaxDiagnosticCode.TypeMismatch)]
        [TestCase("1 2 vec2 1 2 3 vec3 +", ZaxDiagnosticCode.TypeMismatch)]
        [TestCase("1 2 vec2 dup cross", ZaxDiagnosticCode.TypeMismatch)]
        [TestCase("1 2 vec2 3 vec2", ZaxDiagnosticCode.TypeMismatch)]
        [TestCase("Q_IDENTITY 1 Qrotate", ZaxDiagnosticCode.TypeMismatch)]
        [TestCase("1 #x", ZaxDiagnosticCode.SwizzleOnScalar)]
        [TestCase("1 2 vec2 #z", ZaxDiagnosticCode.SwizzleOutOfRange)]
        [TestCase("1 unpack", ZaxDiagnosticCode.UnpackOnScalar)]
        [TestCase("1 2 vec2 Mmul", ZaxDiagnosticCode.NotEnoughOperands)]
        [TestCase("1 Mtranspose", ZaxDiagnosticCode.MatrixShapeMismatch)]
        [TestCase("Midentity3 Midentity2 Mmul", ZaxDiagnosticCode.MatrixShapeMismatch)]
        [TestCase("Midentity3 1 2 vec2 Mmulv", ZaxDiagnosticCode.MatrixShapeMismatch)]
        [TestCase("Midentity4 1 2 vec2 Mtfpoint", ZaxDiagnosticCode.MatrixShapeMismatch)]
        [TestCase("Midentity4 Qfromm", ZaxDiagnosticCode.MatrixShapeMismatch)]
        [TestCase("1 2 vec2 Mtranslate", ZaxDiagnosticCode.MatrixShapeMismatch)]
        [TestCase("1 2 vec2 X_AXIS Mouter", ZaxDiagnosticCode.MatrixShapeMismatch)]
        public void RejectsInvalidExpressions(string source, ZaxDiagnosticCode code)
        {
            Assert.That(Zax.CompileError(source).Code, Is.EqualTo(code));
        }

        [Test]
        public void PointsDiagnosticAtOffendingToken()
        {
            var diagnostic = Zax.CompileError("1 2 foo +");
            Assert.That(diagnostic.Code, Is.EqualTo(ZaxDiagnosticCode.UnknownName));
            Assert.That(diagnostic.Offset, Is.EqualTo(4));
            Assert.That(diagnostic.Length, Is.EqualTo(3));
            Assert.That(diagnostic.Arguments, Is.EqualTo(new[] { "foo" }));
        }

        [Test]
        public void ReportsRequiredAndAvailableOperandCounts()
        {
            var diagnostic = Zax.CompileError("1 clamp");
            Assert.That(diagnostic.Code, Is.EqualTo(ZaxDiagnosticCode.NotEnoughOperands));
            Assert.That(diagnostic.Arguments, Is.EqualTo(new[] { "clamp", "3", "1" }));
        }

        [Test]
        public void ImplicitlyConvertsIntResultToExpectedFloat()
        {
            var program = Zax.Compile("1 2 +", new[] { Float });
            Assert.That(program.ResultTypes, Is.EqualTo(new[] { Float }));
            Zax.AreEqual(3.0f, ZaxEvaluator.Evaluate(program));
        }

        [Test]
        public void ConvertsOnlyMismatchedResultSlots()
        {
            var program = Zax.Compile("1 2.5 3", new[] { Float, Float, Int });
            var results = new ZaxValue[3];
            ZaxEvaluator.Evaluate(program, default, results);
            Zax.AreEqual(new[] { Zax.F(1.0f), Zax.F(2.5f), Zax.I(3) }, results);
        }

        [Test]
        public void AcceptsMatchingExpectedResultTypes()
        {
            var program = Zax.Compile("1.0 1 2 vec2", new[] { Float, Float2 });
            Assert.That(program.ResultTypes, Is.EqualTo(new[] { Float, Float2 }));
        }

        [TestCase("1.5", new[] { Int })]
        [TestCase("1", new[] { Float2 })]
        [TestCase("1 2 vec2", new[] { Float3 })]
        [TestCase("1.0 2", new[] { Int, Int })]
        public void RejectsIncompatibleResultTypes(string source, ZaxValueType[] expected)
        {
            Assert.That(Zax.CompileError(source, expected).Code, Is.EqualTo(ZaxDiagnosticCode.ResultTypeMismatch));
        }

        [TestCase("1 2", new[] { Float })]
        [TestCase("1", new[] { Float, Float })]
        public void RejectsResultCountMismatch(string source, ZaxValueType[] expected)
        {
            var diagnostic = Zax.CompileError(source, expected);
            Assert.That(diagnostic.Code, Is.EqualTo(ZaxDiagnosticCode.ResultCountMismatch));
            Assert.That(diagnostic.Arguments, Is.EqualTo(new[] { expected.Length.ToString(), (expected.Length == 1 ? 2 : 1).ToString() }));
        }

        [Test]
        public void SingleResultOverloadChecksResultType()
        {
            var diagnostics = new System.Collections.Generic.List<ZaxDiagnostic>();
            Assert.That(ZaxCompiler.TryCompile("1 2 vec2", null, ZaxValueType.Float, diagnostics, out _), Is.False);
            Assert.That(diagnostics[0].Code, Is.EqualTo(ZaxDiagnosticCode.ResultTypeMismatch));

            diagnostics.Clear();
            Assert.That(ZaxCompiler.TryCompile("1 2", null, (ZaxValueType?)null, diagnostics, out var program), Is.True);
            Assert.That(program.ResultCount, Is.EqualTo(2));
        }

        [Test]
        public void RecordsOnlyReferencedVariablesInFirstUseOrder()
        {
            var program = Zax.Compile(
                "@c @a + @c *",
                new ZaxVariable("a", Float),
                new ZaxVariable("b", Float2),
                new ZaxVariable("c", Float));
            Assert.That(program.Variables, Has.Length.EqualTo(2));
            Assert.That(program.Variables[0].Name, Is.EqualTo("c"));
            Assert.That(program.Variables[1].Name, Is.EqualTo("a"));
            Assert.That(program.IndexOfVariable("c"), Is.EqualTo(0));
            Assert.That(program.IndexOfVariable("a"), Is.EqualTo(1));
            Assert.That(program.IndexOfVariable("b"), Is.EqualTo(-1));
        }

        [Test]
        public void TypesVariablesByDeclaration()
        {
            var program = Zax.Compile("@p @s *", new ZaxVariable("p", Float3), new ZaxVariable("s", Int));
            Assert.That(program.ResultTypes, Is.EqualTo(new[] { Float3 }));
        }

        [Test]
        public void VariableNamesAreCaseSensitive()
        {
            var diagnostic = Zax.CompileError("@A", new ZaxVariable("a", Float));
            Assert.That(diagnostic.Code, Is.EqualTo(ZaxDiagnosticCode.UnknownVariable));
            Assert.That(diagnostic.Arguments, Is.EqualTo(new[] { "A" }));
        }

        [TestCase("1", 1)]
        [TestCase("1 2 +", 2)]
        [TestCase("1 2 3 + +", 3)]
        [TestCase("1 2 + 3 + 4 +", 2)]
        [TestCase("1 2 3 vec3 ...", 3)]
        [TestCase("Midentity4 Mdup Mmul", 8)]
        public void ComputesMaximumStackDepth(string source, int expected)
        {
            Assert.That(Zax.Compile(source).StackSize, Is.EqualTo(expected));
        }

        [Test]
        public void ResultTypeThrowsForMultipleResults()
        {
            var program = Zax.Compile("1 2");
            Assert.That(() => program.ResultType, Throws.InvalidOperationException);
        }
    }
}
