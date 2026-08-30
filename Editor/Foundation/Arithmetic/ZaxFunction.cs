using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace KusakaFactory.Zatools.Foundation.Arithmetic
{
    public enum ZaxFunction : byte
    {
        Add, Sub, Mul, Div, IntDiv, Mod, Neg,
        Abs, Sign, Min, Max, Clamp, Saturate,
        Floor, Ceil, Round, Frac,
        Sqrt, Rsqrt, Pow, Exp, Exp2, Log, Log2, Log10,
        Sin, Cos, Tan, Asin, Acos, Atan, Atan2,
        Degrees, Radians,
        Lerp, Step, Smoothstep,
        Dot, Cross, Length, LengthSq, Distance, Normalize, Reflect,
        Vec2, Vec3, Vec4,
    }

    public enum ZaxSignature : byte
    {
        ElementwisePreserveInt,
        ElementwiseFloat,
        IntegerOnly,
        ReduceToScalar,
        Cross,
        Construct,
    }

    public readonly struct ZaxFunctionInfo
    {
        public readonly ZaxFunction Function;
        public readonly byte Arity;
        public readonly ZaxSignature Signature;

        public ZaxFunctionInfo(ZaxFunction function, byte arity, ZaxSignature signature)
        {
            Function = function;
            Arity = arity;
            Signature = signature;
        }
    }

    public static class ZaxFunctions
    {
        private const ZaxSignature Ei = ZaxSignature.ElementwisePreserveInt;
        private const ZaxSignature Ef = ZaxSignature.ElementwiseFloat;
        private const ZaxSignature Io = ZaxSignature.IntegerOnly;
        private const ZaxSignature Rs = ZaxSignature.ReduceToScalar;
        private const ZaxSignature Cr = ZaxSignature.Cross;
        private const ZaxSignature Cn = ZaxSignature.Construct;

        private static readonly ZaxFunctionInfo[] Table = BuildTable();

        private static readonly Dictionary<string, ZaxFunction> NameTable = new Dictionary<string, ZaxFunction>
        {
            ["+"] = ZaxFunction.Add, ["add"] = ZaxFunction.Add,
            ["-"] = ZaxFunction.Sub, ["sub"] = ZaxFunction.Sub,
            ["*"] = ZaxFunction.Mul, ["mul"] = ZaxFunction.Mul,
            ["/"] = ZaxFunction.Div, ["div"] = ZaxFunction.Div,
            ["//"] = ZaxFunction.IntDiv, ["idiv"] = ZaxFunction.IntDiv,
            ["%"] = ZaxFunction.Mod, ["mod"] = ZaxFunction.Mod,
            ["neg"] = ZaxFunction.Neg,
            ["abs"] = ZaxFunction.Abs,
            ["sign"] = ZaxFunction.Sign,
            ["min"] = ZaxFunction.Min,
            ["max"] = ZaxFunction.Max,
            ["clamp"] = ZaxFunction.Clamp,
            ["saturate"] = ZaxFunction.Saturate,
            ["floor"] = ZaxFunction.Floor,
            ["ceil"] = ZaxFunction.Ceil,
            ["round"] = ZaxFunction.Round,
            ["frac"] = ZaxFunction.Frac,
            ["sqrt"] = ZaxFunction.Sqrt,
            ["rsqrt"] = ZaxFunction.Rsqrt,
            ["pow"] = ZaxFunction.Pow,
            ["exp"] = ZaxFunction.Exp,
            ["exp2"] = ZaxFunction.Exp2,
            ["log"] = ZaxFunction.Log,
            ["log2"] = ZaxFunction.Log2,
            ["log10"] = ZaxFunction.Log10,
            ["sin"] = ZaxFunction.Sin,
            ["cos"] = ZaxFunction.Cos,
            ["tan"] = ZaxFunction.Tan,
            ["asin"] = ZaxFunction.Asin,
            ["acos"] = ZaxFunction.Acos,
            ["atan"] = ZaxFunction.Atan,
            ["atan2"] = ZaxFunction.Atan2,
            ["degrees"] = ZaxFunction.Degrees,
            ["radians"] = ZaxFunction.Radians,
            ["lerp"] = ZaxFunction.Lerp,
            ["step"] = ZaxFunction.Step,
            ["smoothstep"] = ZaxFunction.Smoothstep,
            ["dot"] = ZaxFunction.Dot,
            ["cross"] = ZaxFunction.Cross,
            ["length"] = ZaxFunction.Length,
            ["lengthsq"] = ZaxFunction.LengthSq,
            ["distance"] = ZaxFunction.Distance,
            ["normalize"] = ZaxFunction.Normalize,
            ["reflect"] = ZaxFunction.Reflect,
            ["vec2"] = ZaxFunction.Vec2,
            ["vec3"] = ZaxFunction.Vec3,
            ["vec4"] = ZaxFunction.Vec4,
        };

        private static readonly Dictionary<string, ZaxValue> ConstantTable = new Dictionary<string, ZaxValue>
        {
            ["PI"] = ZaxValue.FromFloat(math.PI),
            ["TAU"] = ZaxValue.FromFloat(2.0f * math.PI),
            ["E"] = ZaxValue.FromFloat(math.E),
            ["EPSILON"] = ZaxValue.FromFloat(math.EPSILON),
            ["INF"] = ZaxValue.FromFloat(math.INFINITY),
        };

        private static ZaxFunctionInfo[] BuildTable()
        {
            var entries = new ZaxFunctionInfo[]
            {
                new ZaxFunctionInfo(ZaxFunction.Add, 2, Ei),
                new ZaxFunctionInfo(ZaxFunction.Sub, 2, Ei),
                new ZaxFunctionInfo(ZaxFunction.Mul, 2, Ei),
                new ZaxFunctionInfo(ZaxFunction.Div, 2, Ef),
                new ZaxFunctionInfo(ZaxFunction.IntDiv, 2, Io),
                new ZaxFunctionInfo(ZaxFunction.Mod, 2, Ei),
                new ZaxFunctionInfo(ZaxFunction.Neg, 1, Ei),
                new ZaxFunctionInfo(ZaxFunction.Abs, 1, Ei),
                new ZaxFunctionInfo(ZaxFunction.Sign, 1, Ei),
                new ZaxFunctionInfo(ZaxFunction.Min, 2, Ei),
                new ZaxFunctionInfo(ZaxFunction.Max, 2, Ei),
                new ZaxFunctionInfo(ZaxFunction.Clamp, 3, Ei),
                new ZaxFunctionInfo(ZaxFunction.Saturate, 1, Ef),
                new ZaxFunctionInfo(ZaxFunction.Floor, 1, Ef),
                new ZaxFunctionInfo(ZaxFunction.Ceil, 1, Ef),
                new ZaxFunctionInfo(ZaxFunction.Round, 1, Ef),
                new ZaxFunctionInfo(ZaxFunction.Frac, 1, Ef),
                new ZaxFunctionInfo(ZaxFunction.Sqrt, 1, Ef),
                new ZaxFunctionInfo(ZaxFunction.Rsqrt, 1, Ef),
                new ZaxFunctionInfo(ZaxFunction.Pow, 2, Ef),
                new ZaxFunctionInfo(ZaxFunction.Exp, 1, Ef),
                new ZaxFunctionInfo(ZaxFunction.Exp2, 1, Ef),
                new ZaxFunctionInfo(ZaxFunction.Log, 1, Ef),
                new ZaxFunctionInfo(ZaxFunction.Log2, 1, Ef),
                new ZaxFunctionInfo(ZaxFunction.Log10, 1, Ef),
                new ZaxFunctionInfo(ZaxFunction.Sin, 1, Ef),
                new ZaxFunctionInfo(ZaxFunction.Cos, 1, Ef),
                new ZaxFunctionInfo(ZaxFunction.Tan, 1, Ef),
                new ZaxFunctionInfo(ZaxFunction.Asin, 1, Ef),
                new ZaxFunctionInfo(ZaxFunction.Acos, 1, Ef),
                new ZaxFunctionInfo(ZaxFunction.Atan, 1, Ef),
                new ZaxFunctionInfo(ZaxFunction.Atan2, 2, Ef),
                new ZaxFunctionInfo(ZaxFunction.Degrees, 1, Ef),
                new ZaxFunctionInfo(ZaxFunction.Radians, 1, Ef),
                new ZaxFunctionInfo(ZaxFunction.Lerp, 3, Ef),
                new ZaxFunctionInfo(ZaxFunction.Step, 2, Ef),
                new ZaxFunctionInfo(ZaxFunction.Smoothstep, 3, Ef),
                new ZaxFunctionInfo(ZaxFunction.Dot, 2, Rs),
                new ZaxFunctionInfo(ZaxFunction.Cross, 2, Cr),
                new ZaxFunctionInfo(ZaxFunction.Length, 1, Rs),
                new ZaxFunctionInfo(ZaxFunction.LengthSq, 1, Rs),
                new ZaxFunctionInfo(ZaxFunction.Distance, 2, Rs),
                new ZaxFunctionInfo(ZaxFunction.Normalize, 1, Ef),
                new ZaxFunctionInfo(ZaxFunction.Reflect, 2, Ef),
                new ZaxFunctionInfo(ZaxFunction.Vec2, 2, Cn),
                new ZaxFunctionInfo(ZaxFunction.Vec3, 3, Cn),
                new ZaxFunctionInfo(ZaxFunction.Vec4, 4, Cn),
            };

            var table = new ZaxFunctionInfo[entries.Length];
            foreach (var entry in entries) table[(int)entry.Function] = entry;
            return table;
        }

        public static ZaxFunctionInfo Info(ZaxFunction function) => Table[(int)function];

        public static bool TryLookup(string name, out ZaxFunction function) => NameTable.TryGetValue(name, out function);

        public static bool TryLookupConstant(string name, out ZaxValue value) => ConstantTable.TryGetValue(name, out value);

        public static bool TryInferResult(
            ZaxFunction function,
            ReadOnlySpan<ZaxValueType> arguments,
            out ZaxValueType argumentType,
            out ZaxValueType resultType)
        {
            argumentType = default;
            resultType = default;

            var info = Table[(int)function];
            switch (info.Signature)
            {
                case ZaxSignature.ElementwisePreserveInt:
                    if (!TryPromoteAll(arguments, out var preserved)) return false;
                    argumentType = preserved;
                    resultType = preserved;
                    return true;

                case ZaxSignature.ElementwiseFloat:
                    if (!TryPromoteAll(arguments, out var floated)) return false;
                    argumentType = floated.Floatize();
                    resultType = argumentType;
                    return true;

                case ZaxSignature.ReduceToScalar:
                    if (!TryPromoteAll(arguments, out var reduced)) return false;
                    argumentType = reduced.Floatize();
                    resultType = ZaxValueType.Float;
                    return true;

                case ZaxSignature.IntegerOnly:
                    foreach (var argument in arguments)
                    {
                        if (argument != ZaxValueType.Int) return false;
                    }
                    argumentType = ZaxValueType.Int;
                    resultType = ZaxValueType.Int;
                    return true;

                case ZaxSignature.Cross:
                    foreach (var argument in arguments)
                    {
                        if (argument != ZaxValueType.Float3) return false;
                    }
                    argumentType = ZaxValueType.Float3;
                    resultType = ZaxValueType.Float3;
                    return true;

                case ZaxSignature.Construct:
                    foreach (var argument in arguments)
                    {
                        if (!argument.IsScalar()) return false;
                    }
                    argumentType = ZaxValueType.Float;
                    resultType = ZaxValueTypeEx.OfDimension(info.Arity);
                    return true;

                default:
                    return false;
            }
        }

        private static bool TryPromoteAll(ReadOnlySpan<ZaxValueType> arguments, out ZaxValueType promoted)
        {
            promoted = arguments.Length > 0 ? arguments[0] : ZaxValueType.Float;
            for (var i = 1; i < arguments.Length; ++i)
            {
                if (!ZaxValueTypeEx.TryPromote(promoted, arguments[i], out promoted)) return false;
            }
            return true;
        }
    }
}
