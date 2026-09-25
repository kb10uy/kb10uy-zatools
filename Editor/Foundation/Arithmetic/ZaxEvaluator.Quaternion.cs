using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace KusakaFactory.Zatools.Foundation.Arithmetic
{
    public static unsafe partial class ZaxEvaluator
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int ApplyQuaternion(in ZaxInstruction instruction, ZaxValue* stack, int pointer)
        {
            var arity = instruction.Operand;
            var baseIndex = pointer - arity;
            var x = stack[baseIndex].ToFloat4();
            var y = arity > 1 ? stack[baseIndex + 1].ToFloat4() : float4.zero;
            var z = arity > 2 ? stack[baseIndex + 2].ToFloat4() : float4.zero;

            ZaxValue result;
            switch (instruction.Function)
            {
                case ZaxFunction.Qmul: result = ZaxValue.FromFloat4(math.mul(new quaternion(x), new quaternion(y)).value); break;
                case ZaxFunction.Qrotate: result = ZaxValue.FromFloat3(math.rotate(new quaternion(x), y.xyz)); break;
                case ZaxFunction.Qinverse: result = ZaxValue.FromFloat4(math.inverse(new quaternion(x)).value); break;
                case ZaxFunction.Qconjugate: result = ZaxValue.FromFloat4(math.conjugate(new quaternion(x)).value); break;
                case ZaxFunction.Qaxisangle: result = ZaxValue.FromFloat4(quaternion.AxisAngle(x.xyz, y.x).value); break;
                case ZaxFunction.Qeuler: result = ZaxValue.FromFloat4(quaternion.Euler(x.xyz).value); break;
                case ZaxFunction.Qlook: result = ZaxValue.FromFloat4(quaternion.LookRotation(x.xyz, y.xyz).value); break;
                case ZaxFunction.Qslerp: result = ZaxValue.FromFloat4(math.slerp(new quaternion(x), new quaternion(y), z.x).value); break;
                case ZaxFunction.Qnlerp: result = ZaxValue.FromFloat4(math.nlerp(new quaternion(x), new quaternion(y), z.x).value); break;
                default: result = ZaxValue.FromFloatN(x, instruction.ResultType); break;
            }

            stack[baseIndex] = result;
            return baseIndex + 1;
        }
    }
}
