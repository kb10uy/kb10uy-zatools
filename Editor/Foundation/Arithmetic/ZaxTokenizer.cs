using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;

namespace KusakaFactory.Zatools.Foundation.Arithmetic
{
    public static class ZaxTokenizer
    {
        private static readonly ImmutableHashSet<string> Symbols = ImmutableHashSet<string>.Empty.Union(new[]
        {
            "+", "-", "*", "/", "//", "%",
        });

        public static bool TryTokenize(string source, List<ZaxToken> tokens, List<ZaxDiagnostic> diagnostics)
        {
            tokens.Clear();
            var initialDiagnosticCount = diagnostics.Count;
            if (source == null) return true;

            var position = 0;
            while (position < source.Length)
            {
                if (char.IsWhiteSpace(source[position]))
                {
                    position += 1;
                    continue;
                }

                var start = position;
                while (position < source.Length && !char.IsWhiteSpace(source[position])) position += 1;
                var text = source.Substring(start, position - start);

                if (TryClassify(text, start, diagnostics, out var token)) tokens.Add(token);
            }

            return diagnostics.Count == initialDiagnosticCount;
        }

        private static bool TryClassify(string text, int offset, List<ZaxDiagnostic> diagnostics, out ZaxToken token)
        {
            token = default;

            if (Symbols.Contains(text))
            {
                token = ZaxToken.Symbol(offset, text);
                return true;
            }

            if (text[0] == ZaxSwizzleSets.SwizzlePrefix)
            {
                Span<int> components = stackalloc int[4];
                if (text.Length > 1 && ZaxSwizzleSets.TryResolve(text.Substring(1), components))
                {
                    token = ZaxToken.Swizzle(offset, text);
                    return true;
                }
                diagnostics.Add(new ZaxDiagnostic(ZaxDiagnosticCode.InvalidSwizzle, offset, text.Length, text));
                return false;
            }

            if (text[0] == ZaxSwizzleSets.VariablePrefix)
            {
                if (text.Length > 1 && IsIdentifier(text.Substring(1)))
                {
                    token = ZaxToken.Variable(offset, text);
                    return true;
                }
                diagnostics.Add(new ZaxDiagnostic(ZaxDiagnosticCode.InvalidToken, offset, text.Length, text));
                return false;
            }

            if (IsNumberStart(text)) return TryClassifyNumber(text, offset, diagnostics, out token);

            if (IsIdentifier(text))
            {
                token = ZaxToken.Identifier(offset, text);
                return true;
            }

            diagnostics.Add(new ZaxDiagnostic(ZaxDiagnosticCode.InvalidToken, offset, text.Length, text));
            return false;
        }

        private static bool TryClassifyNumber(string text, int offset, List<ZaxDiagnostic> diagnostics, out ZaxToken token)
        {
            token = default;

            var body = text;
            var forcedFloat = false;
            if (body.Length > 1 && (body[body.Length - 1] == 'f' || body[body.Length - 1] == 'F'))
            {
                body = body.Substring(0, body.Length - 1);
                forcedFloat = true;
            }

            var isFloat = forcedFloat || body.IndexOf('.') >= 0 || body.IndexOf('e') >= 0 || body.IndexOf('E') >= 0;
            if (isFloat)
            {
                if (float.TryParse(body, NumberStyles.Float, CultureInfo.InvariantCulture, out var floatValue))
                {
                    token = ZaxToken.Float(offset, text, floatValue);
                    return true;
                }
            }
            else
            {
                if (int.TryParse(body, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var intValue))
                {
                    token = ZaxToken.Int(offset, text, intValue);
                    return true;
                }
            }

            diagnostics.Add(new ZaxDiagnostic(ZaxDiagnosticCode.InvalidNumberLiteral, offset, text.Length, text));
            return false;
        }

        private static bool IsNumberStart(string text)
        {
            var head = text[0];
            if (char.IsDigit(head) || head == '.') return true;
            if ((head == '-' || head == '+') && text.Length > 1) return char.IsDigit(text[1]) || text[1] == '.';
            return false;
        }

        private static bool IsIdentifier(string text)
        {
            if (text.Length == 0) return false;
            if (!char.IsLetter(text[0]) && text[0] != '_') return false;
            for (var i = 1; i < text.Length; ++i)
            {
                if (!char.IsLetterOrDigit(text[i]) && text[i] != '_') return false;
            }
            return true;
        }
    }
}
