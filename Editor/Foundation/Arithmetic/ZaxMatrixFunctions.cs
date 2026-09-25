using System;

namespace KusakaFactory.Zatools.Foundation.Arithmetic
{
    public enum ZaxMatrixInference : byte
    {
        Success,
        NotEnoughOperands,
        ShapeMismatch,
    }

    public readonly struct ZaxMatrixEffect
    {
        public readonly int Consumed;
        public readonly int Produced;
        public readonly ZaxValueType ArgumentType;
        public readonly ZaxValueType ResultType;
        public readonly int Dimension;
        public readonly int SecondDimension;

        public ZaxMatrixEffect(int consumed, int produced, ZaxValueType argumentType, ZaxValueType resultType, int dimension, int secondDimension)
        {
            Consumed = consumed;
            Produced = produced;
            ArgumentType = argumentType;
            ResultType = resultType;
            Dimension = dimension;
            SecondDimension = secondDimension;
        }
    }

    public static class ZaxMatrixFunctions
    {
        private const string UnknownMatrix = "floatN x N";

        public static ZaxMatrixInference Infer(
            ZaxFunction function,
            ReadOnlySpan<ZaxValueType> stack,
            out ZaxMatrixEffect effect,
            out int required,
            out string expectedShape)
        {
            effect = default;
            required = 0;
            expectedShape = null;

            switch (function)
            {
                case ZaxFunction.Mmul:
                case ZaxFunction.Madd:
                case ZaxFunction.Msub:
                {
                    if (!TryTopMatrix(stack, out var n, out var element, out required, out expectedShape, $"{UnknownMatrix}, {UnknownMatrix}")) return Fail(required, stack);
                    var matrix = MatrixName(n);
                    expectedShape = $"{matrix}, {matrix}";
                    required = n * 2;
                    if (stack.Length < required) return ZaxMatrixInference.NotEnoughOperands;
                    if (!IsMatrixAt(stack, stack.Length - n, n)) return ZaxMatrixInference.ShapeMismatch;
                    effect = new ZaxMatrixEffect(n * 2, n, element, element, n, n);
                    return ZaxMatrixInference.Success;
                }

                case ZaxFunction.Mmulv:
                {
                    if (!TryTopVector(stack, out var n, out var element, out required, out expectedShape, $"{UnknownMatrix}, floatN")) return Fail(required, stack);
                    expectedShape = $"{MatrixName(n)}, {element.DisplayName()}";
                    required = n + 1;
                    if (stack.Length < required) return ZaxMatrixInference.NotEnoughOperands;
                    if (!IsMatrixAt(stack, stack.Length - 1, n)) return ZaxMatrixInference.ShapeMismatch;
                    effect = new ZaxMatrixEffect(n + 1, 1, element, element, n, n);
                    return ZaxMatrixInference.Success;
                }

                case ZaxFunction.Mscale:
                {
                    expectedShape = $"{UnknownMatrix}, float";
                    required = 2;
                    if (stack.Length < required) return ZaxMatrixInference.NotEnoughOperands;
                    if (!stack[stack.Length - 1].IsScalar()) return ZaxMatrixInference.ShapeMismatch;
                    var rowType = stack[stack.Length - 2];
                    if (!rowType.IsVector()) return ZaxMatrixInference.ShapeMismatch;
                    var n = rowType.Dimension();
                    expectedShape = $"{MatrixName(n)}, float";
                    required = n + 1;
                    if (stack.Length < required) return ZaxMatrixInference.NotEnoughOperands;
                    if (!IsMatrixAt(stack, stack.Length - 1, n)) return ZaxMatrixInference.ShapeMismatch;
                    effect = new ZaxMatrixEffect(n + 1, n, rowType, rowType, n, n);
                    return ZaxMatrixInference.Success;
                }

                case ZaxFunction.Mtfpoint:
                case ZaxFunction.Mtfdir:
                {
                    expectedShape = $"{MatrixName(4)}, float3";
                    required = 5;
                    if (stack.Length < required) return ZaxMatrixInference.NotEnoughOperands;
                    if (stack[stack.Length - 1] != ZaxValueType.Float3) return ZaxMatrixInference.ShapeMismatch;
                    if (!IsMatrixAt(stack, stack.Length - 1, 4)) return ZaxMatrixInference.ShapeMismatch;
                    effect = new ZaxMatrixEffect(5, 1, ZaxValueType.Float4, ZaxValueType.Float3, 4, 4);
                    return ZaxMatrixInference.Success;
                }

                case ZaxFunction.Mtranspose:
                case ZaxFunction.Minverse:
                {
                    if (!TryTopMatrix(stack, out var n, out var element, out required, out expectedShape, UnknownMatrix)) return Fail(required, stack);
                    effect = new ZaxMatrixEffect(n, n, element, element, n, n);
                    return ZaxMatrixInference.Success;
                }

                case ZaxFunction.Mdet:
                case ZaxFunction.Mtrace:
                {
                    if (!TryTopMatrix(stack, out var n, out var element, out required, out expectedShape, UnknownMatrix)) return Fail(required, stack);
                    effect = new ZaxMatrixEffect(n, 1, element, ZaxValueType.Float, n, n);
                    return ZaxMatrixInference.Success;
                }

                case ZaxFunction.Mdiagv:
                {
                    if (!TryTopMatrix(stack, out var n, out var element, out required, out expectedShape, UnknownMatrix)) return Fail(required, stack);
                    effect = new ZaxMatrixEffect(n, 1, element, element, n, n);
                    return ZaxMatrixInference.Success;
                }

                case ZaxFunction.Midentity2:
                case ZaxFunction.Midentity3:
                case ZaxFunction.Midentity4:
                {
                    var n = function == ZaxFunction.Midentity2 ? 2 : (function == ZaxFunction.Midentity3 ? 3 : 4);
                    var element = ZaxValueTypeEx.OfDimension(n);
                    effect = new ZaxMatrixEffect(0, n, element, element, n, n);
                    return ZaxMatrixInference.Success;
                }

                case ZaxFunction.Mdiag:
                {
                    if (!TryTopVector(stack, out var n, out var element, out required, out expectedShape, "floatN")) return Fail(required, stack);
                    effect = new ZaxMatrixEffect(1, n, element, element, n, n);
                    return ZaxMatrixInference.Success;
                }

                case ZaxFunction.Mtranslate:
                case ZaxFunction.Mroteuler:
                {
                    expectedShape = "float3";
                    required = 1;
                    if (stack.Length < required) return ZaxMatrixInference.NotEnoughOperands;
                    if (stack[stack.Length - 1] != ZaxValueType.Float3) return ZaxMatrixInference.ShapeMismatch;
                    var n = function == ZaxFunction.Mtranslate ? 4 : 3;
                    var element = ZaxValueTypeEx.OfDimension(n);
                    effect = new ZaxMatrixEffect(1, n, ZaxValueType.Float3, element, n, n);
                    return ZaxMatrixInference.Success;
                }

                case ZaxFunction.Mrotx:
                case ZaxFunction.Mroty:
                case ZaxFunction.Mrotz:
                {
                    expectedShape = "float";
                    required = 1;
                    if (stack.Length < required) return ZaxMatrixInference.NotEnoughOperands;
                    if (!stack[stack.Length - 1].IsScalar()) return ZaxMatrixInference.ShapeMismatch;
                    effect = new ZaxMatrixEffect(1, 3, ZaxValueType.Float, ZaxValueType.Float3, 3, 3);
                    return ZaxMatrixInference.Success;
                }

                case ZaxFunction.Mrotaxis:
                {
                    expectedShape = "float3, float";
                    required = 2;
                    if (stack.Length < required) return ZaxMatrixInference.NotEnoughOperands;
                    if (!stack[stack.Length - 1].IsScalar()) return ZaxMatrixInference.ShapeMismatch;
                    if (stack[stack.Length - 2] != ZaxValueType.Float3) return ZaxMatrixInference.ShapeMismatch;
                    effect = new ZaxMatrixEffect(2, 3, ZaxValueType.Float3, ZaxValueType.Float3, 3, 3);
                    return ZaxMatrixInference.Success;
                }

                case ZaxFunction.Mlookrot:
                {
                    expectedShape = "float3, float3";
                    required = 2;
                    if (stack.Length < required) return ZaxMatrixInference.NotEnoughOperands;
                    if (stack[stack.Length - 1] != ZaxValueType.Float3) return ZaxMatrixInference.ShapeMismatch;
                    if (stack[stack.Length - 2] != ZaxValueType.Float3) return ZaxMatrixInference.ShapeMismatch;
                    effect = new ZaxMatrixEffect(2, 3, ZaxValueType.Float3, ZaxValueType.Float3, 3, 3);
                    return ZaxMatrixInference.Success;
                }

                case ZaxFunction.Mouter:
                {
                    if (!TryTopVector(stack, out var n, out var element, out required, out expectedShape, "floatN, floatN")) return Fail(required, stack);
                    expectedShape = $"{element.DisplayName()}, {element.DisplayName()}";
                    required = 2;
                    if (stack.Length < required) return ZaxMatrixInference.NotEnoughOperands;
                    if (stack[stack.Length - 2] != element) return ZaxMatrixInference.ShapeMismatch;
                    effect = new ZaxMatrixEffect(2, n, element, element, n, n);
                    return ZaxMatrixInference.Success;
                }

                case ZaxFunction.Mto2:
                case ZaxFunction.Mto3:
                case ZaxFunction.Mto4:
                {
                    if (!TryTopMatrix(stack, out var n, out var element, out required, out expectedShape, UnknownMatrix)) return Fail(required, stack);
                    var k = function == ZaxFunction.Mto2 ? 2 : (function == ZaxFunction.Mto3 ? 3 : 4);
                    effect = new ZaxMatrixEffect(n, k, element, ZaxValueTypeEx.OfDimension(k), n, k);
                    return ZaxMatrixInference.Success;
                }

                case ZaxFunction.Mdup:
                {
                    if (!TryTopMatrix(stack, out var n, out var element, out required, out expectedShape, UnknownMatrix)) return Fail(required, stack);
                    effect = new ZaxMatrixEffect(n, n * 2, element, element, n, n);
                    return ZaxMatrixInference.Success;
                }

                case ZaxFunction.Mdrop:
                {
                    if (!TryTopMatrix(stack, out var n, out var element, out required, out expectedShape, UnknownMatrix)) return Fail(required, stack);
                    effect = new ZaxMatrixEffect(n, 0, element, element, n, n);
                    return ZaxMatrixInference.Success;
                }

                case ZaxFunction.Mswap:
                {
                    if (!TryTopMatrix(stack, out var n, out var element, out required, out expectedShape, $"{UnknownMatrix}, {UnknownMatrix}")) return Fail(required, stack);
                    var below = stack.Slice(0, stack.Length - n);
                    if (!TryTopMatrix(below, out var k, out var secondElement, out var belowRequired, out _, UnknownMatrix))
                    {
                        expectedShape = $"{UnknownMatrix}, {MatrixName(n)}";
                        required = n + belowRequired;
                        return Fail(required, stack);
                    }
                    expectedShape = $"{MatrixName(k)}, {MatrixName(n)}";
                    required = n + k;
                    effect = new ZaxMatrixEffect(n + k, n + k, element, secondElement, n, k);
                    return ZaxMatrixInference.Success;
                }

                default:
                    return ZaxMatrixInference.ShapeMismatch;
            }
        }

        public static string MatrixName(int dimension) => $"{ZaxValueTypeEx.OfDimension(dimension).DisplayName()} x {dimension}";

        private static ZaxMatrixInference Fail(int required, ReadOnlySpan<ZaxValueType> stack)
        {
            return stack.Length < required ? ZaxMatrixInference.NotEnoughOperands : ZaxMatrixInference.ShapeMismatch;
        }

        private static bool TryTopVector(
            ReadOnlySpan<ZaxValueType> stack,
            out int dimension,
            out ZaxValueType element,
            out int required,
            out string expectedShape,
            string unknownShape)
        {
            dimension = 0;
            element = default;
            required = 1;
            expectedShape = unknownShape;
            if (stack.Length < 1) return false;

            element = stack[stack.Length - 1];
            if (!element.IsVector()) return false;
            dimension = element.Dimension();
            return true;
        }

        private static bool TryTopMatrix(
            ReadOnlySpan<ZaxValueType> stack,
            out int dimension,
            out ZaxValueType element,
            out int required,
            out string expectedShape,
            string unknownShape)
        {
            if (!TryTopVector(stack, out dimension, out element, out required, out expectedShape, unknownShape)) return false;

            expectedShape = MatrixName(dimension);
            required = dimension;
            if (stack.Length < required) return false;
            return IsMatrixAt(stack, stack.Length, dimension);
        }

        private static bool IsMatrixAt(ReadOnlySpan<ZaxValueType> stack, int end, int dimension)
        {
            if (end < dimension) return false;
            var element = ZaxValueTypeEx.OfDimension(dimension);
            for (var i = end - dimension; i < end; ++i)
            {
                if (stack[i] != element) return false;
            }
            return true;
        }
    }
}
