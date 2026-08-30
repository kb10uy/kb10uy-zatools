using System;

namespace KusakaFactory.Zatools.Foundation.Arithmetic
{
    public enum ZaxDiagnosticCode
    {
        InvalidToken,
        InvalidNumberLiteral,
        InvalidSwizzle,
        EmptyExpression,
        UnknownName,
        UnknownVariable,
        NotEnoughOperands,
        ExtraOperands,
        TypeMismatch,
        SwizzleOnScalar,
        UnpackOnScalar,
        SwizzleOutOfRange,
        ResultTypeMismatch,
    }

    public readonly struct ZaxDiagnostic
    {
        public readonly ZaxDiagnosticCode Code;
        public readonly int Offset;
        public readonly int Length;
        public readonly string[] Arguments;

        public ZaxDiagnostic(ZaxDiagnosticCode code, int offset, int length, params string[] arguments)
        {
            Code = code;
            Offset = offset;
            Length = length;
            Arguments = arguments ?? Array.Empty<string>();
        }

        public ZaxDiagnostic(ZaxDiagnosticCode code, ZaxToken token, params string[] arguments)
            : this(code, token.Offset, token.Length, arguments)
        {
        }

        public string LocalizationKey => Code.LocalizationKey();
    }

    public static class ZaxDiagnosticCodeEx
    {
        public static string LocalizationKey(this ZaxDiagnosticCode code)
        {
            switch (code)
            {
                case ZaxDiagnosticCode.InvalidToken: return "zax.diagnostic.invalid-token";
                case ZaxDiagnosticCode.InvalidNumberLiteral: return "zax.diagnostic.invalid-number-literal";
                case ZaxDiagnosticCode.InvalidSwizzle: return "zax.diagnostic.invalid-swizzle";
                case ZaxDiagnosticCode.EmptyExpression: return "zax.diagnostic.empty-expression";
                case ZaxDiagnosticCode.UnknownName: return "zax.diagnostic.unknown-name";
                case ZaxDiagnosticCode.UnknownVariable: return "zax.diagnostic.unknown-variable";
                case ZaxDiagnosticCode.NotEnoughOperands: return "zax.diagnostic.not-enough-operands";
                case ZaxDiagnosticCode.ExtraOperands: return "zax.diagnostic.extra-operands";
                case ZaxDiagnosticCode.TypeMismatch: return "zax.diagnostic.type-mismatch";
                case ZaxDiagnosticCode.SwizzleOnScalar: return "zax.diagnostic.swizzle-on-scalar";
                case ZaxDiagnosticCode.UnpackOnScalar: return "zax.diagnostic.unpack-on-scalar";
                case ZaxDiagnosticCode.SwizzleOutOfRange: return "zax.diagnostic.swizzle-out-of-range";
                case ZaxDiagnosticCode.ResultTypeMismatch: return "zax.diagnostic.result-type-mismatch";
                default: return "zax.diagnostic.invalid-token";
            }
        }
    }
}
