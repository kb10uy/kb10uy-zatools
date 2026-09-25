using System;
using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace KusakaFactory.Zatools.Foundation.Arithmetic
{
    public static unsafe class ZaxEvaluator
    {
        public static ZaxValue Evaluate(ZaxProgram program)
        {
            return Evaluate(program, ReadOnlySpan<ZaxValue>.Empty);
        }

        public static ZaxValue Evaluate(ZaxProgram program, ReadOnlySpan<ZaxValue> variables)
        {
            if (program.ResultCount != 1)
            {
                throw new ArgumentException($"program produces {program.ResultCount} values; use the overload that takes a results span", nameof(program));
            }

            Span<ZaxValue> stack = stackalloc ZaxValue[math.max(program.StackSize, 1)];
            Execute(program.Instructions, program.Constants, variables, stack);
            return stack[0];
        }

        public static void Evaluate(ZaxProgram program, ReadOnlySpan<ZaxValue> variables, Span<ZaxValue> results)
        {
            if (results.Length < program.ResultCount)
            {
                throw new ArgumentException($"program produces {program.ResultCount} values but only {results.Length} slot(s) were given", nameof(results));
            }

            Span<ZaxValue> stack = stackalloc ZaxValue[math.max(program.StackSize, 1)];
            Execute(program.Instructions, program.Constants, variables, stack);
            stack.Slice(0, program.ResultCount).CopyTo(results);
        }

        public static void Execute(
            ReadOnlySpan<ZaxInstruction> instructions,
            ReadOnlySpan<ZaxValue> constants,
            ReadOnlySpan<ZaxValue> variables,
            Span<ZaxValue> stack)
        {
            fixed (ZaxInstruction* instructionsPointer = instructions)
            fixed (ZaxValue* constantsPointer = constants)
            fixed (ZaxValue* variablesPointer = variables)
            fixed (ZaxValue* stackPointer = stack)
            {
                Execute(instructionsPointer, instructions.Length, constantsPointer, variablesPointer, stackPointer);
            }
        }

        public static void Execute(
            ZaxInstruction* instructions,
            int instructionCount,
            ZaxValue* constants,
            ZaxValue* variables,
            ZaxValue* stack)
        {
            var pointer = 0;
            for (var i = 0; i < instructionCount; ++i)
            {
                var instruction = instructions[i];
                switch (instruction.OpCode)
                {
                    case ZaxOpCode.Constant:
                        stack[pointer++] = constants[instruction.Operand];
                        break;

                    case ZaxOpCode.Variable:
                        stack[pointer++] = variables[instruction.Operand];
                        break;

                    case ZaxOpCode.Convert:
                        stack[instruction.Operand] = stack[instruction.Operand].ConvertTo(instruction.ResultType);
                        break;

                    case ZaxOpCode.Swizzle:
                        stack[pointer - 1] = ApplySwizzle(stack[pointer - 1], instruction.Operand, instruction.ResultType);
                        break;

                    case ZaxOpCode.Unpack:
                    {
                        var packed = stack[pointer - 1].ToFloat4();
                        var dimension = instruction.ArgumentType.Dimension();
                        for (var c = 0; c < dimension; ++c) stack[pointer - 1 + c] = ZaxValue.FromFloat(packed[c]);
                        pointer += dimension - 1;
                        break;
                    }

                    case ZaxOpCode.Dup:
                        stack[pointer] = stack[pointer - 1];
                        pointer += 1;
                        break;

                    case ZaxOpCode.Drop:
                        pointer -= 1;
                        break;

                    case ZaxOpCode.Swap:
                    {
                        var swapped = stack[pointer - 1];
                        stack[pointer - 1] = stack[pointer - 2];
                        stack[pointer - 2] = swapped;
                        break;
                    }

                    case ZaxOpCode.Over:
                        stack[pointer] = stack[pointer - 2];
                        pointer += 1;
                        break;

                    case ZaxOpCode.Rot:
                    {
                        var rotated = stack[pointer - 3];
                        stack[pointer - 3] = stack[pointer - 2];
                        stack[pointer - 2] = stack[pointer - 1];
                        stack[pointer - 1] = rotated;
                        break;
                    }

                    case ZaxOpCode.Call:
                    {
                        var arity = instruction.Operand;
                        var baseIndex = pointer - arity;
                        var result = Apply(
                            instruction.Function,
                            instruction.ArgumentType,
                            instruction.ResultType,
                            stack + baseIndex,
                            arity);
                        stack[baseIndex] = result;
                        pointer = baseIndex + 1;
                        break;
                    }

                    case ZaxOpCode.Matrix:
                        pointer = ApplyMatrix(instruction, stack, pointer);
                        break;
                }
            }
        }

        private static float4 Boolean(bool4 mask) => math.select(float4.zero, new float4(1.0f), mask);

        private static float3 RgbToYuv(float3 rgb)
        {
            return new float3(
                math.dot(rgb, new float3(0.2126f, 0.7152f, 0.0722f)),
                math.dot(rgb, new float3(-0.09991f, -0.33609f, 0.436f)),
                math.dot(rgb, new float3(0.615f, -0.55861f, -0.05639f)));
        }

        private static float3 YuvToRgb(float3 yuv)
        {
            return new float3(
                math.dot(yuv, new float3(1.0f, 0.0f, 1.28033f)),
                math.dot(yuv, new float3(1.0f, -0.21482f, -0.38059f)),
                math.dot(yuv, new float3(1.0f, 2.12798f, 0.0f)));
        }

        private static float4 SrgbToLinear(float4 srgb)
        {
            var low = srgb / 12.92f;
            var high = math.pow((srgb + 0.055f) / 1.055f, 2.4f);
            return math.select(high, low, srgb <= new float4(0.04045f));
        }

        private static float4 LinearToSrgb(float4 linear)
        {
            var low = linear * 12.92f;
            var high = 1.055f * math.pow(linear, 1.0f / 2.4f) - 0.055f;
            return math.select(high, low, linear <= new float4(0.0031308f));
        }

        private static ZaxValue ApplySwizzle(ZaxValue value, ushort packed, ZaxValueType resultType)
        {
            var source = value.ToFloat4();
            var length = ZaxInstruction.SwizzleLength(packed);
            var swizzled = float4.zero;
            for (var i = 0; i < length; ++i) swizzled[i] = source[ZaxInstruction.SwizzleComponent(packed, i)];
            return ZaxValue.FromFloatN(swizzled, resultType);
        }

        private static ZaxValue Apply(
            ZaxFunction function,
            ZaxValueType argumentType,
            ZaxValueType resultType,
            ZaxValue* arguments,
            int count)
        {
            if (argumentType == ZaxValueType.Int) return ApplyInteger(function, arguments, count);

            var x = arguments[0].ConvertTo(argumentType).ToFloat4();
            var y = count > 1 ? arguments[1].ConvertTo(argumentType).ToFloat4() : float4.zero;
            var z = count > 2 ? arguments[2].ConvertTo(argumentType).ToFloat4() : float4.zero;
            var w = count > 3 ? arguments[3].ConvertTo(argumentType).ToFloat4() : float4.zero;

            switch (function)
            {
                case ZaxFunction.Add: return ZaxValue.FromFloatN(x + y, resultType);
                case ZaxFunction.Sub: return ZaxValue.FromFloatN(x - y, resultType);
                case ZaxFunction.Mul: return ZaxValue.FromFloatN(x * y, resultType);
                case ZaxFunction.Div: return ZaxValue.FromFloatN(x / y, resultType);
                case ZaxFunction.Mod: return ZaxValue.FromFloatN(math.fmod(x, y), resultType);
                case ZaxFunction.Neg: return ZaxValue.FromFloatN(-x, resultType);
                case ZaxFunction.Abs: return ZaxValue.FromFloatN(math.abs(x), resultType);
                case ZaxFunction.Sign: return ZaxValue.FromFloatN(math.sign(x), resultType);
                case ZaxFunction.Min: return ZaxValue.FromFloatN(math.min(x, y), resultType);
                case ZaxFunction.Max: return ZaxValue.FromFloatN(math.max(x, y), resultType);
                case ZaxFunction.Clamp: return ZaxValue.FromFloatN(math.clamp(x, y, z), resultType);
                case ZaxFunction.Saturate: return ZaxValue.FromFloatN(math.saturate(x), resultType);
                case ZaxFunction.Floor: return ZaxValue.FromFloatN(math.floor(x), resultType);
                case ZaxFunction.Ceil: return ZaxValue.FromFloatN(math.ceil(x), resultType);
                case ZaxFunction.Round: return ZaxValue.FromFloatN(math.round(x), resultType);
                case ZaxFunction.Frac: return ZaxValue.FromFloatN(math.frac(x), resultType);
                case ZaxFunction.Sqrt: return ZaxValue.FromFloatN(math.sqrt(x), resultType);
                case ZaxFunction.Rsqrt: return ZaxValue.FromFloatN(math.rsqrt(x), resultType);
                case ZaxFunction.Pow: return ZaxValue.FromFloatN(math.pow(x, y), resultType);
                case ZaxFunction.Exp: return ZaxValue.FromFloatN(math.exp(x), resultType);
                case ZaxFunction.Exp2: return ZaxValue.FromFloatN(math.exp2(x), resultType);
                case ZaxFunction.Log: return ZaxValue.FromFloatN(math.log(x), resultType);
                case ZaxFunction.Log2: return ZaxValue.FromFloatN(math.log2(x), resultType);
                case ZaxFunction.Log10: return ZaxValue.FromFloatN(math.log10(x), resultType);
                case ZaxFunction.Sin: return ZaxValue.FromFloatN(math.sin(x), resultType);
                case ZaxFunction.Cos: return ZaxValue.FromFloatN(math.cos(x), resultType);
                case ZaxFunction.Tan: return ZaxValue.FromFloatN(math.tan(x), resultType);
                case ZaxFunction.Asin: return ZaxValue.FromFloatN(math.asin(x), resultType);
                case ZaxFunction.Acos: return ZaxValue.FromFloatN(math.acos(x), resultType);
                case ZaxFunction.Atan: return ZaxValue.FromFloatN(math.atan(x), resultType);
                case ZaxFunction.Atan2: return ZaxValue.FromFloatN(math.atan2(x, y), resultType);
                case ZaxFunction.Degrees: return ZaxValue.FromFloatN(math.degrees(x), resultType);
                case ZaxFunction.Radians: return ZaxValue.FromFloatN(math.radians(x), resultType);
                case ZaxFunction.Lerp: return ZaxValue.FromFloatN(math.lerp(x, y, z), resultType);
                case ZaxFunction.Step: return ZaxValue.FromFloatN(math.step(x, y), resultType);
                case ZaxFunction.Smoothstep: return ZaxValue.FromFloatN(math.smoothstep(x, y, z), resultType);
                case ZaxFunction.Normalize: return ZaxValue.FromFloatN(math.normalize(x), resultType);
                case ZaxFunction.Reflect: return ZaxValue.FromFloatN(math.reflect(x, y), resultType);
                case ZaxFunction.Dot: return ZaxValue.FromFloat(math.dot(x, y));
                case ZaxFunction.Length: return ZaxValue.FromFloat(math.length(x));
                case ZaxFunction.LengthSq: return ZaxValue.FromFloat(math.lengthsq(x));
                case ZaxFunction.Distance: return ZaxValue.FromFloat(math.distance(x, y));
                case ZaxFunction.Cross: return ZaxValue.FromFloat3(math.cross(x.xyz, y.xyz));
                case ZaxFunction.Vec2: return ZaxValue.FromFloat2(new float2(x.x, y.x));
                case ZaxFunction.Vec3: return ZaxValue.FromFloat3(new float3(x.x, y.x, z.x));
                case ZaxFunction.Vec4: return ZaxValue.FromFloat4(new float4(x.x, y.x, z.x, w.x));
                case ZaxFunction.Gt: return ZaxValue.FromFloatN(Boolean(x > y), resultType);
                case ZaxFunction.Lt: return ZaxValue.FromFloatN(Boolean(x < y), resultType);
                case ZaxFunction.Geq: return ZaxValue.FromFloatN(Boolean(x >= y), resultType);
                case ZaxFunction.Leq: return ZaxValue.FromFloatN(Boolean(x <= y), resultType);
                case ZaxFunction.Eq: return ZaxValue.FromFloatN(Boolean(x == y), resultType);
                case ZaxFunction.Neq: return ZaxValue.FromFloatN(Boolean(x != y), resultType);
                case ZaxFunction.Not: return ZaxValue.FromFloatN(Boolean(x == float4.zero), resultType);
                case ZaxFunction.RgbToYuv: return ZaxValue.FromFloat3(RgbToYuv(x.xyz));
                case ZaxFunction.YuvToRgb: return ZaxValue.FromFloat3(YuvToRgb(x.xyz));
                case ZaxFunction.SrgbToLinear: return ZaxValue.FromFloatN(SrgbToLinear(x), resultType);
                case ZaxFunction.LinearToSrgb: return ZaxValue.FromFloatN(LinearToSrgb(x), resultType);
                default: return ZaxValue.FromFloatN(x, resultType);
            }
        }

        private static ZaxValue ApplyInteger(ZaxFunction function, ZaxValue* arguments, int count)
        {
            var a = arguments[0].AsInt;
            var b = count > 1 ? arguments[1].AsInt : 0;
            var c = count > 2 ? arguments[2].AsInt : 0;

            switch (function)
            {
                case ZaxFunction.Add: return ZaxValue.FromInt(a + b);
                case ZaxFunction.Sub: return ZaxValue.FromInt(a - b);
                case ZaxFunction.Mul: return ZaxValue.FromInt(a * b);
                case ZaxFunction.IntDiv: return ZaxValue.FromInt(b != 0 ? a / b : 0);
                case ZaxFunction.Mod: return ZaxValue.FromInt(b != 0 ? a % b : 0);
                case ZaxFunction.Neg: return ZaxValue.FromInt(-a);
                case ZaxFunction.Abs: return ZaxValue.FromInt(math.abs(a));
                case ZaxFunction.Sign: return ZaxValue.FromInt(a > 0 ? 1 : (a < 0 ? -1 : 0));
                case ZaxFunction.Min: return ZaxValue.FromInt(math.min(a, b));
                case ZaxFunction.Max: return ZaxValue.FromInt(math.max(a, b));
                case ZaxFunction.Clamp: return ZaxValue.FromInt(math.clamp(a, b, c));
                default: return ZaxValue.FromInt(a);
            }
        }

        private static float4x4 LoadRows(ZaxValue* rows, int dimension)
        {
            var transposed = float4x4.identity;
            for (var i = 0; i < dimension; ++i) transposed[i] = rows[i].ToFloat4();
            return transposed;
        }

        private static void StoreRows(ZaxValue* rows, float4x4 transposed, int dimension, ZaxValueType rowType)
        {
            for (var i = 0; i < dimension; ++i) rows[i] = ZaxValue.FromFloatN(transposed[i], rowType);
        }

        private static float Scalar(ZaxValue value) => value.ToFloat4().x;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int ApplyMatrix(in ZaxInstruction instruction, ZaxValue* stack, int pointer)
        {
            var n = ZaxInstruction.Dimension(instruction.Operand);
            var k = ZaxInstruction.SecondDimension(instruction.Operand);
            var rowType = instruction.ArgumentType;
            var resultType = instruction.ResultType;

            switch (instruction.Function)
            {
                case ZaxFunction.Mmul:
                {
                    var baseIndex = pointer - n * 2;
                    var a = LoadRows(stack + baseIndex, n);
                    var b = LoadRows(stack + baseIndex + n, n);
                    StoreRows(stack + baseIndex, math.mul(b, a), n, rowType);
                    return baseIndex + n;
                }

                case ZaxFunction.Madd:
                {
                    var baseIndex = pointer - n * 2;
                    var a = LoadRows(stack + baseIndex, n);
                    var b = LoadRows(stack + baseIndex + n, n);
                    StoreRows(stack + baseIndex, a + b, n, rowType);
                    return baseIndex + n;
                }

                case ZaxFunction.Msub:
                {
                    var baseIndex = pointer - n * 2;
                    var a = LoadRows(stack + baseIndex, n);
                    var b = LoadRows(stack + baseIndex + n, n);
                    StoreRows(stack + baseIndex, a - b, n, rowType);
                    return baseIndex + n;
                }

                case ZaxFunction.Mmulv:
                {
                    var baseIndex = pointer - n - 1;
                    var m = LoadRows(stack + baseIndex, n);
                    var v = stack[baseIndex + n].ToFloat4();
                    stack[baseIndex] = ZaxValue.FromFloatN(math.mul(v, m), resultType);
                    return baseIndex + 1;
                }

                case ZaxFunction.Mscale:
                {
                    var baseIndex = pointer - n - 1;
                    var m = LoadRows(stack + baseIndex, n);
                    var s = Scalar(stack[baseIndex + n]);
                    StoreRows(stack + baseIndex, m * s, n, rowType);
                    return baseIndex + n;
                }

                case ZaxFunction.Mtfpoint:
                {
                    var baseIndex = pointer - 5;
                    var m = LoadRows(stack + baseIndex, 4);
                    var p = stack[baseIndex + 4].AsFloat3;
                    stack[baseIndex] = ZaxValue.FromFloat3(math.mul(new float4(p, 1.0f), m).xyz);
                    return baseIndex + 1;
                }

                case ZaxFunction.Mtfdir:
                {
                    var baseIndex = pointer - 5;
                    var m = LoadRows(stack + baseIndex, 4);
                    var d = stack[baseIndex + 4].AsFloat3;
                    stack[baseIndex] = ZaxValue.FromFloat3(math.mul(new float4(d, 0.0f), m).xyz);
                    return baseIndex + 1;
                }

                case ZaxFunction.Mtranspose:
                {
                    var baseIndex = pointer - n;
                    var m = LoadRows(stack + baseIndex, n);
                    StoreRows(stack + baseIndex, math.transpose(m), n, rowType);
                    return pointer;
                }

                case ZaxFunction.Minverse:
                {
                    var baseIndex = pointer - n;
                    var m = LoadRows(stack + baseIndex, n);
                    StoreRows(stack + baseIndex, math.inverse(m), n, rowType);
                    return pointer;
                }

                case ZaxFunction.Mdet:
                {
                    var baseIndex = pointer - n;
                    var m = LoadRows(stack + baseIndex, n);
                    stack[baseIndex] = ZaxValue.FromFloat(math.determinant(m));
                    return baseIndex + 1;
                }

                case ZaxFunction.Mtrace:
                {
                    var baseIndex = pointer - n;
                    var m = LoadRows(stack + baseIndex, n);
                    var trace = 0.0f;
                    for (var i = 0; i < n; ++i) trace += m[i][i];
                    stack[baseIndex] = ZaxValue.FromFloat(trace);
                    return baseIndex + 1;
                }

                case ZaxFunction.Mdiagv:
                {
                    var baseIndex = pointer - n;
                    var m = LoadRows(stack + baseIndex, n);
                    stack[baseIndex] = ZaxValue.FromFloatN(new float4(m.c0.x, m.c1.y, m.c2.z, m.c3.w), resultType);
                    return baseIndex + 1;
                }

                case ZaxFunction.Midentity2:
                case ZaxFunction.Midentity3:
                case ZaxFunction.Midentity4:
                    StoreRows(stack + pointer, float4x4.identity, n, resultType);
                    return pointer + n;

                case ZaxFunction.Mdiag:
                {
                    var baseIndex = pointer - 1;
                    var v = stack[baseIndex].ToFloat4();
                    var m = float4x4.identity;
                    m.c0.x = v.x;
                    m.c1.y = v.y;
                    m.c2.z = v.z;
                    m.c3.w = v.w;
                    StoreRows(stack + baseIndex, m, n, resultType);
                    return baseIndex + n;
                }

                case ZaxFunction.Mtranslate:
                {
                    var baseIndex = pointer - 1;
                    var t = stack[baseIndex].AsFloat3;
                    StoreRows(stack + baseIndex, math.transpose(float4x4.Translate(t)), 4, resultType);
                    return baseIndex + 4;
                }

                case ZaxFunction.Mrotx:
                {
                    var baseIndex = pointer - 1;
                    var angle = Scalar(stack[baseIndex]);
                    StoreRows(stack + baseIndex, math.transpose(float4x4.RotateX(angle)), 3, resultType);
                    return baseIndex + 3;
                }

                case ZaxFunction.Mroty:
                {
                    var baseIndex = pointer - 1;
                    var angle = Scalar(stack[baseIndex]);
                    StoreRows(stack + baseIndex, math.transpose(float4x4.RotateY(angle)), 3, resultType);
                    return baseIndex + 3;
                }

                case ZaxFunction.Mrotz:
                {
                    var baseIndex = pointer - 1;
                    var angle = Scalar(stack[baseIndex]);
                    StoreRows(stack + baseIndex, math.transpose(float4x4.RotateZ(angle)), 3, resultType);
                    return baseIndex + 3;
                }

                case ZaxFunction.Mrotaxis:
                {
                    var baseIndex = pointer - 2;
                    var axis = stack[baseIndex].AsFloat3;
                    var angle = Scalar(stack[baseIndex + 1]);
                    StoreRows(stack + baseIndex, math.transpose(float4x4.AxisAngle(axis, angle)), 3, resultType);
                    return baseIndex + 3;
                }

                case ZaxFunction.Mroteuler:
                {
                    var baseIndex = pointer - 1;
                    var euler = stack[baseIndex].AsFloat3;
                    StoreRows(stack + baseIndex, math.transpose(float4x4.Euler(euler)), 3, resultType);
                    return baseIndex + 3;
                }

                case ZaxFunction.Mlookrot:
                {
                    var baseIndex = pointer - 2;
                    var forward = stack[baseIndex].AsFloat3;
                    var up = stack[baseIndex + 1].AsFloat3;
                    var rotation = new float4x4(float3x3.LookRotation(forward, up), float3.zero);
                    StoreRows(stack + baseIndex, math.transpose(rotation), 3, resultType);
                    return baseIndex + 3;
                }

                case ZaxFunction.Mouter:
                {
                    var baseIndex = pointer - 2;
                    var a = stack[baseIndex].ToFloat4();
                    var b = stack[baseIndex + 1].ToFloat4();
                    for (var i = 0; i < n; ++i) stack[baseIndex + i] = ZaxValue.FromFloatN(a[i] * b, resultType);
                    return baseIndex + n;
                }

                case ZaxFunction.Mto2:
                case ZaxFunction.Mto3:
                case ZaxFunction.Mto4:
                {
                    var baseIndex = pointer - n;
                    var m = LoadRows(stack + baseIndex, n);
                    StoreRows(stack + baseIndex, m, k, resultType);
                    return baseIndex + k;
                }

                case ZaxFunction.Mdup:
                {
                    var baseIndex = pointer - n;
                    for (var i = 0; i < n; ++i) stack[pointer + i] = stack[baseIndex + i];
                    return pointer + n;
                }

                case ZaxFunction.Mdrop:
                    return pointer - n;

                case ZaxFunction.Mswap:
                {
                    var baseIndex = pointer - n - k;
                    var temporary = stackalloc ZaxValue[4];
                    for (var i = 0; i < n; ++i) temporary[i] = stack[baseIndex + k + i];
                    for (var i = k - 1; i >= 0; --i) stack[baseIndex + n + i] = stack[baseIndex + i];
                    for (var i = 0; i < n; ++i) stack[baseIndex + i] = temporary[i];
                    return pointer;
                }

                default:
                    return pointer;
            }
        }
    }
}
