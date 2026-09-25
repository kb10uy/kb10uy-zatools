using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace KusakaFactory.Zatools.Foundation.Arithmetic
{
    public static unsafe partial class ZaxEvaluator
    {
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
