using System;
using System.Runtime.InteropServices;

namespace KusakaFactory.Zatools.Foundation.Arithmetic
{
    public enum ZaxOpCode : byte
    {
        Constant,
        Variable,
        Call,
        Swizzle,
        Convert,
        Unpack,
        Dup,
        Drop,
        Swap,
        Over,
        Rot,
        Matrix,
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct ZaxInstruction
    {
        public readonly ZaxOpCode OpCode;
        public readonly ZaxFunction Function;
        public readonly ZaxValueType ResultType;
        public readonly ZaxValueType ArgumentType;
        public readonly ushort Operand;

        private ZaxInstruction(ZaxOpCode opCode, ZaxFunction function, ZaxValueType resultType, ZaxValueType argumentType, ushort operand)
        {
            OpCode = opCode;
            Function = function;
            ResultType = resultType;
            ArgumentType = argumentType;
            Operand = operand;
        }

        public static ZaxInstruction Constant(int index, ZaxValueType type) => new ZaxInstruction(ZaxOpCode.Constant, default, type, type, (ushort)index);
        public static ZaxInstruction Variable(int index, ZaxValueType type) => new ZaxInstruction(ZaxOpCode.Variable, default, type, type, (ushort)index);
        public static ZaxInstruction Call(ZaxFunction function, ZaxValueType argumentType, ZaxValueType resultType, int arity) => new ZaxInstruction(ZaxOpCode.Call, function, resultType, argumentType, (ushort)arity);
        public static ZaxInstruction Swizzle(ushort packed, ZaxValueType argumentType, ZaxValueType resultType) => new ZaxInstruction(ZaxOpCode.Swizzle, default, resultType, argumentType, packed);
        public static ZaxInstruction Convert(ZaxValueType argumentType, ZaxValueType resultType, int slot) => new ZaxInstruction(ZaxOpCode.Convert, default, resultType, argumentType, (ushort)slot);
        public static ZaxInstruction Unpack(ZaxValueType argumentType) => new ZaxInstruction(ZaxOpCode.Unpack, default, ZaxValueType.Float, argumentType, 0);
        public static ZaxInstruction Stack(ZaxOpCode opCode, ZaxValueType resultType) => new ZaxInstruction(opCode, default, resultType, resultType, 0);
        public static ZaxInstruction Matrix(ZaxFunction function, ZaxValueType argumentType, ZaxValueType resultType, int dimension, int secondDimension) => new ZaxInstruction(ZaxOpCode.Matrix, function, resultType, argumentType, PackDimensions(dimension, secondDimension));

        public static ushort PackSwizzle(ReadOnlySpan<int> components)
        {
            var packed = 0;
            for (var i = 0; i < components.Length; ++i) packed |= (components[i] & 3) << (i * 2);
            return (ushort)(packed | (components.Length << 8));
        }

        public static int SwizzleLength(ushort packed) => (packed >> 8) & 7;

        public static int SwizzleComponent(ushort packed, int index) => (packed >> (index * 2)) & 3;

        public static ushort PackDimensions(int dimension, int secondDimension) => (ushort)((dimension & 0xFF) | ((secondDimension & 0xFF) << 8));

        public static int Dimension(ushort packed) => packed & 0xFF;

        public static int SecondDimension(ushort packed) => (packed >> 8) & 0xFF;

        public override string ToString()
        {
            switch (OpCode)
            {
                case ZaxOpCode.Constant: return $"Constant {Operand} -> {ResultType.DisplayName()}";
                case ZaxOpCode.Variable: return $"Variable {Operand} -> {ResultType.DisplayName()}";
                case ZaxOpCode.Call: return $"Call {Function}/{Operand} ({ArgumentType.DisplayName()}) -> {ResultType.DisplayName()}";
                case ZaxOpCode.Swizzle: return $"Swizzle {Operand:X4} -> {ResultType.DisplayName()}";
                case ZaxOpCode.Convert: return $"Convert [{Operand}] -> {ResultType.DisplayName()}";
                case ZaxOpCode.Unpack: return $"Unpack {ArgumentType.DisplayName()} -> {ArgumentType.Dimension()} x float";
                case ZaxOpCode.Matrix: return $"Matrix {Function} N={Dimension(Operand)} K={SecondDimension(Operand)} ({ArgumentType.DisplayName()}) -> {ResultType.DisplayName()}";
                default: return $"{OpCode} -> {ResultType.DisplayName()}";
            }
        }
    }
}
