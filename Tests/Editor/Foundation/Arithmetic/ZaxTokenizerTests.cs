using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using KusakaFactory.Zatools.Foundation.Arithmetic;

namespace KusakaFactory.Zatools.Tests.Foundation.Arithmetic
{
    public sealed class ZaxTokenizerTests
    {
        private static (bool Succeeded, List<ZaxToken> Tokens, List<ZaxDiagnostic> Diagnostics) Tokenize(string source)
        {
            var tokens = new List<ZaxToken>();
            var diagnostics = new List<ZaxDiagnostic>();
            var succeeded = ZaxTokenizer.TryTokenize(source, tokens, diagnostics);
            return (succeeded, tokens, diagnostics);
        }

        private static ZaxToken Single(string source)
        {
            var (succeeded, tokens, diagnostics) = Tokenize(source);
            Assert.That(succeeded, Is.True, () => string.Join(", ", diagnostics.Select((d) => d.Code)));
            Assert.That(tokens, Has.Count.EqualTo(1));
            return tokens[0];
        }

        [TestCase("+")]
        [TestCase("-")]
        [TestCase("//")]
        [TestCase("...")]
        [TestCase(">=")]
        [TestCase("!=")]
        [TestCase("!")]
        [TestCase("M*")]
        [TestCase("M+")]
        [TestCase("M-")]
        public void RecognizesSymbols(string source)
        {
            var token = Single(source);
            Assert.That(token.Kind, Is.EqualTo(ZaxTokenKind.Symbol));
            Assert.That(token.Text, Is.EqualTo(source));
        }

        [TestCase("0", 0)]
        [TestCase("42", 42)]
        [TestCase("-3", -3)]
        [TestCase("+7", 7)]
        public void RecognizesIntLiterals(string source, int expected)
        {
            var token = Single(source);
            Assert.That(token.Kind, Is.EqualTo(ZaxTokenKind.IntLiteral));
            Assert.That(token.IntValue, Is.EqualTo(expected));
        }

        [TestCase("1.5", 1.5f)]
        [TestCase(".5", 0.5f)]
        [TestCase("-.25", -0.25f)]
        [TestCase("2f", 2.0f)]
        [TestCase("2F", 2.0f)]
        [TestCase("1e3", 1000.0f)]
        [TestCase("1.5E-1", 0.15f)]
        public void RecognizesFloatLiterals(string source, float expected)
        {
            var token = Single(source);
            Assert.That(token.Kind, Is.EqualTo(ZaxTokenKind.FloatLiteral));
            Assert.That(token.FloatValue, Is.EqualTo(expected));
        }

        [TestCase("#x")]
        [TestCase("#wzyx")]
        [TestCase("#rgb")]
        [TestCase("#uv")]
        [TestCase("#xxxx")]
        public void RecognizesSwizzles(string source)
        {
            Assert.That(Single(source).Kind, Is.EqualTo(ZaxTokenKind.Swizzle));
        }

        [TestCase("@a", "a")]
        [TestCase("@_value1", "_value1")]
        public void RecognizesVariables(string source, string body)
        {
            var token = Single(source);
            Assert.That(token.Kind, Is.EqualTo(ZaxTokenKind.Variable));
            Assert.That(token.Body, Is.EqualTo(body));
        }

        [TestCase("sin")]
        [TestCase("_a1")]
        [TestCase("Mmul")]
        [TestCase("f")]
        public void RecognizesIdentifiers(string source)
        {
            Assert.That(Single(source).Kind, Is.EqualTo(ZaxTokenKind.Identifier));
        }

        [TestCase("#", ZaxDiagnosticCode.InvalidSwizzle)]
        [TestCase("#xg", ZaxDiagnosticCode.InvalidSwizzle)]
        [TestCase("#xyzwx", ZaxDiagnosticCode.InvalidSwizzle)]
        [TestCase("#q", ZaxDiagnosticCode.InvalidSwizzle)]
        [TestCase("@", ZaxDiagnosticCode.InvalidToken)]
        [TestCase("@1", ZaxDiagnosticCode.InvalidToken)]
        [TestCase("$", ZaxDiagnosticCode.InvalidToken)]
        [TestCase("a-b", ZaxDiagnosticCode.InvalidToken)]
        [TestCase("1.2.3", ZaxDiagnosticCode.InvalidNumberLiteral)]
        [TestCase("12abc", ZaxDiagnosticCode.InvalidNumberLiteral)]
        [TestCase("1e", ZaxDiagnosticCode.InvalidNumberLiteral)]
        [TestCase("99999999999", ZaxDiagnosticCode.InvalidNumberLiteral)]
        public void RejectsInvalidTokens(string source, ZaxDiagnosticCode code)
        {
            var (succeeded, tokens, diagnostics) = Tokenize(source);
            Assert.That(succeeded, Is.False);
            Assert.That(tokens, Is.Empty);
            Assert.That(diagnostics.Select((d) => d.Code), Is.EqualTo(new[] { code }));
            Assert.That(diagnostics[0].Offset, Is.EqualTo(0));
            Assert.That(diagnostics[0].Length, Is.EqualTo(source.Length));
        }

        [Test]
        public void SplitsOnWhitespaceAndRecordsOffsets()
        {
            var (succeeded, tokens, _) = Tokenize("  1\t2.0\n  add");
            Assert.That(succeeded, Is.True);
            Assert.That(tokens.Select((t) => t.Kind), Is.EqualTo(new[] { ZaxTokenKind.IntLiteral, ZaxTokenKind.FloatLiteral, ZaxTokenKind.Identifier }));
            Assert.That(tokens.Select((t) => t.Offset), Is.EqualTo(new[] { 2, 4, 10 }));
            Assert.That(tokens.Select((t) => t.Length), Is.EqualTo(new[] { 1, 3, 3 }));
        }

        [Test]
        public void SkipsCommentsUntilEndOfLine()
        {
            var (succeeded, tokens, _) = Tokenize("1 ; 2 3\n4;5\n; 6");
            Assert.That(succeeded, Is.True);
            Assert.That(tokens.Select((t) => t.Text), Is.EqualTo(new[] { "1", "4" }));
        }

        [Test]
        public void ReportsEveryInvalidToken()
        {
            var (succeeded, tokens, diagnostics) = Tokenize("1 #q 2 @1");
            Assert.That(succeeded, Is.False);
            Assert.That(tokens.Select((t) => t.Text), Is.EqualTo(new[] { "1", "2" }));
            Assert.That(diagnostics.Select((d) => d.Code), Is.EqualTo(new[] { ZaxDiagnosticCode.InvalidSwizzle, ZaxDiagnosticCode.InvalidToken }));
            Assert.That(diagnostics.Select((d) => d.Offset), Is.EqualTo(new[] { 2, 7 }));
        }

        [Test]
        public void KeepsPreexistingDiagnostics()
        {
            var tokens = new List<ZaxToken>();
            var diagnostics = new List<ZaxDiagnostic> { new ZaxDiagnostic(ZaxDiagnosticCode.EmptyExpression, 0, 0) };
            Assert.That(ZaxTokenizer.TryTokenize("1 2", tokens, diagnostics), Is.True);
            Assert.That(diagnostics, Has.Count.EqualTo(1));
        }

        [Test]
        public void ClearsTokenListBeforeTokenizing()
        {
            var tokens = new List<ZaxToken> { ZaxToken.Int(0, "9", 9) };
            Assert.That(ZaxTokenizer.TryTokenize("1", tokens, new List<ZaxDiagnostic>()), Is.True);
            Assert.That(tokens.Select((t) => t.Text), Is.EqualTo(new[] { "1" }));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   \n\t")]
        public void AcceptsEmptySource(string source)
        {
            var (succeeded, tokens, diagnostics) = Tokenize(source);
            Assert.That(succeeded, Is.True);
            Assert.That(tokens, Is.Empty);
            Assert.That(diagnostics, Is.Empty);
        }
    }
}
