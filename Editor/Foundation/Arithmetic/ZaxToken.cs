using System;

namespace KusakaFactory.Zatools.Foundation.Arithmetic
{
    public enum ZaxTokenKind : byte
    {
        IntLiteral,
        FloatLiteral,
        Symbol,
        Swizzle,
        Variable,
        Identifier,
    }

    public static class ZaxSwizzleSets
    {
        public const char SwizzlePrefix = '#';
        public const char VariablePrefix = '@';

        private static readonly string[] Sets = { "xyzw", "rgba", "uvst" };

        public static bool TryResolve(string body, Span<int> components)
        {
            if (body.Length < 1 || body.Length > 4) return false;

            var chosenSet = -1;
            for (var i = 0; i < body.Length; ++i)
            {
                var component = -1;
                for (var s = 0; s < Sets.Length; ++s)
                {
                    var index = Sets[s].IndexOf(body[i]);
                    if (index < 0) continue;
                    if (chosenSet >= 0 && chosenSet != s) return false;
                    chosenSet = s;
                    component = index;
                    break;
                }
                if (component < 0) return false;
                components[i] = component;
            }
            return true;
        }
    }

    public readonly struct ZaxToken
    {
        public readonly ZaxTokenKind Kind;
        public readonly int Offset;
        public readonly int Length;
        public readonly string Text;
        public readonly int IntValue;
        public readonly float FloatValue;

        private ZaxToken(ZaxTokenKind kind, int offset, int length, string text, int intValue, float floatValue)
        {
            Kind = kind;
            Offset = offset;
            Length = length;
            Text = text;
            IntValue = intValue;
            FloatValue = floatValue;
        }

        public static ZaxToken Int(int offset, string text, int value) => new ZaxToken(ZaxTokenKind.IntLiteral, offset, text.Length, text, value, value);
        public static ZaxToken Float(int offset, string text, float value) => new ZaxToken(ZaxTokenKind.FloatLiteral, offset, text.Length, text, 0, value);
        public static ZaxToken Symbol(int offset, string text) => new ZaxToken(ZaxTokenKind.Symbol, offset, text.Length, text, 0, 0.0f);
        public static ZaxToken Swizzle(int offset, string text) => new ZaxToken(ZaxTokenKind.Swizzle, offset, text.Length, text, 0, 0.0f);
        public static ZaxToken Variable(int offset, string text) => new ZaxToken(ZaxTokenKind.Variable, offset, text.Length, text, 0, 0.0f);
        public static ZaxToken Identifier(int offset, string text) => new ZaxToken(ZaxTokenKind.Identifier, offset, text.Length, text, 0, 0.0f);

        public string Body => Text.Substring(1);

        public override string ToString() => $"{Kind}({Text})";
    }
}
