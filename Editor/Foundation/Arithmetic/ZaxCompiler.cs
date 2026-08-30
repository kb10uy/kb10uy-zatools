using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace KusakaFactory.Zatools.Foundation.Arithmetic
{
    public static class ZaxCompiler
    {
        private static readonly ImmutableDictionary<string, ZaxOpCode> StackOperations = ImmutableDictionary<string, ZaxOpCode>.Empty.AddRange(new Dictionary<string, ZaxOpCode>
        {
            ["dup"] = ZaxOpCode.Dup,
            ["drop"] = ZaxOpCode.Drop,
            ["swap"] = ZaxOpCode.Swap,
            ["over"] = ZaxOpCode.Over,
            ["rot"] = ZaxOpCode.Rot,
        });

        private static readonly ImmutableDictionary<ZaxOpCode, int> StackOperationDepths = ImmutableDictionary<ZaxOpCode, int>.Empty.AddRange(new Dictionary<ZaxOpCode, int>
        {
            [ZaxOpCode.Dup] = 1,
            [ZaxOpCode.Drop] = 1,
            [ZaxOpCode.Swap] = 2,
            [ZaxOpCode.Over] = 2,
            [ZaxOpCode.Rot] = 3,
        });

        public static bool TryCompile(
            string source,
            IReadOnlyList<ZaxVariable> variables,
            ZaxValueType? expectedResultType,
            List<ZaxDiagnostic> diagnostics,
            out ZaxProgram program)
        {
            program = null;
            var declaredVariables = variables != null ? variables.ToArray() : Array.Empty<ZaxVariable>();

            var tokens = new List<ZaxToken>();
            if (!ZaxTokenizer.TryTokenize(source, tokens, diagnostics)) return false;
            if (tokens.Count == 0)
            {
                diagnostics.Add(new ZaxDiagnostic(ZaxDiagnosticCode.EmptyExpression, 0, 0));
                return false;
            }

            var instructions = new List<ZaxInstruction>();
            var constants = new List<ZaxValue>();
            var constantIndices = new Dictionary<ZaxValue, int>();
            var typeStack = new List<ZaxValueType>();
            var argumentBuffer = new ZaxValueType[4];
            var componentBuffer = new int[4];
            var maxStackSize = 0;

            void Push(ZaxValueType type)
            {
                typeStack.Add(type);
                if (typeStack.Count > maxStackSize) maxStackSize = typeStack.Count;
            }

            void EmitConstant(ZaxValue value)
            {
                if (!constantIndices.TryGetValue(value, out var index))
                {
                    index = constants.Count;
                    constants.Add(value);
                    constantIndices.Add(value, index);
                }
                instructions.Add(ZaxInstruction.Constant(index, value.Type));
                Push(value.Type);
            }

            bool EmitCall(ZaxFunction function, ZaxToken token)
            {
                var info = ZaxFunctions.Info(function);
                if (typeStack.Count < info.Arity)
                {
                    diagnostics.Add(new ZaxDiagnostic(
                        ZaxDiagnosticCode.NotEnoughOperands, token,
                        token.Text, info.Arity.ToString(), typeStack.Count.ToString()));
                    return false;
                }

                var baseIndex = typeStack.Count - info.Arity;
                for (var i = 0; i < info.Arity; ++i) argumentBuffer[i] = typeStack[baseIndex + i];
                var arguments = new ReadOnlySpan<ZaxValueType>(argumentBuffer, 0, info.Arity);

                if (!ZaxFunctions.TryInferResult(function, arguments, out var argumentType, out var resultType))
                {
                    diagnostics.Add(new ZaxDiagnostic(
                        ZaxDiagnosticCode.TypeMismatch, token,
                        token.Text, DescribeTypes(argumentBuffer, info.Arity)));
                    return false;
                }

                typeStack.RemoveRange(baseIndex, info.Arity);
                instructions.Add(ZaxInstruction.Call(function, argumentType, resultType, info.Arity));
                Push(resultType);
                return true;
            }

            bool EmitSwizzle(ZaxToken token)
            {
                if (typeStack.Count < 1)
                {
                    diagnostics.Add(new ZaxDiagnostic(
                        ZaxDiagnosticCode.NotEnoughOperands, token, token.Text, "1", "0"));
                    return false;
                }

                var operandType = typeStack[typeStack.Count - 1];
                if (!operandType.IsVector())
                {
                    diagnostics.Add(new ZaxDiagnostic(
                        ZaxDiagnosticCode.SwizzleOnScalar, token, token.Text, operandType.DisplayName()));
                    return false;
                }

                var dimension = operandType.Dimension();
                var length = token.Text.Length - 1;
                ZaxSwizzleSets.TryResolve(token.Body, componentBuffer);
                for (var i = 0; i < length; ++i)
                {
                    if (componentBuffer[i] >= dimension)
                    {
                        diagnostics.Add(new ZaxDiagnostic(
                            ZaxDiagnosticCode.SwizzleOutOfRange, token,
                            token.Text, operandType.DisplayName()));
                        return false;
                    }
                }

                var resultType = ZaxValueTypeEx.OfDimension(length);
                var packed = ZaxInstruction.PackSwizzle(new ReadOnlySpan<int>(componentBuffer, 0, length));
                typeStack[typeStack.Count - 1] = resultType;
                instructions.Add(ZaxInstruction.Swizzle(packed, operandType, resultType));
                return true;
            }

            bool EmitStackOperation(ZaxOpCode opCode, ZaxToken token)
            {
                var required = StackOperationDepths[opCode];
                if (typeStack.Count < required)
                {
                    diagnostics.Add(new ZaxDiagnostic(
                        ZaxDiagnosticCode.NotEnoughOperands, token,
                        token.Text, required.ToString(), typeStack.Count.ToString()));
                    return false;
                }

                var top = typeStack.Count - 1;
                switch (opCode)
                {
                    case ZaxOpCode.Dup:
                        instructions.Add(ZaxInstruction.Stack(opCode, typeStack[top]));
                        Push(typeStack[top]);
                        return true;
                    case ZaxOpCode.Drop:
                        instructions.Add(ZaxInstruction.Stack(opCode, typeStack[top]));
                        typeStack.RemoveAt(top);
                        return true;
                    case ZaxOpCode.Swap:
                        {
                            var swapped = typeStack[top];
                            typeStack[top] = typeStack[top - 1];
                            typeStack[top - 1] = swapped;
                            instructions.Add(ZaxInstruction.Stack(opCode, typeStack[top]));
                            return true;
                        }
                    case ZaxOpCode.Over:
                        instructions.Add(ZaxInstruction.Stack(opCode, typeStack[top - 1]));
                        Push(typeStack[top - 1]);
                        return true;
                    case ZaxOpCode.Rot:
                        {
                            var rotated = typeStack[top - 2];
                            typeStack[top - 2] = typeStack[top - 1];
                            typeStack[top - 1] = typeStack[top];
                            typeStack[top] = rotated;
                            instructions.Add(ZaxInstruction.Stack(opCode, typeStack[top]));
                            return true;
                        }
                    default:
                        return false;
                }
            }

            foreach (var token in tokens)
            {
                switch (token.Kind)
                {
                    case ZaxTokenKind.IntLiteral:
                        EmitConstant(ZaxValue.FromInt(token.IntValue));
                        break;

                    case ZaxTokenKind.FloatLiteral:
                        EmitConstant(ZaxValue.FromFloat(token.FloatValue));
                        break;

                    case ZaxTokenKind.Swizzle:
                        if (!EmitSwizzle(token)) return false;
                        break;

                    case ZaxTokenKind.Symbol:
                        if (!ZaxFunctions.TryLookup(token.Text, out var symbolFunction))
                        {
                            diagnostics.Add(new ZaxDiagnostic(ZaxDiagnosticCode.UnknownName, token, token.Text));
                            return false;
                        }
                        if (!EmitCall(symbolFunction, token)) return false;
                        break;

                    case ZaxTokenKind.Variable:
                        {
                            var variableIndex = IndexOfVariable(declaredVariables, token.Body);
                            if (variableIndex < 0)
                            {
                                diagnostics.Add(new ZaxDiagnostic(ZaxDiagnosticCode.UnknownVariable, token, token.Body));
                                return false;
                            }
                            var variableType = declaredVariables[variableIndex].Type;
                            instructions.Add(ZaxInstruction.Variable(variableIndex, variableType));
                            Push(variableType);
                            break;
                        }

                    case ZaxTokenKind.Identifier:
                        {
                            if (StackOperations.TryGetValue(token.Text, out var stackOperation))
                            {
                                if (!EmitStackOperation(stackOperation, token)) return false;
                                break;
                            }
                            if (ZaxFunctions.TryLookupConstant(token.Text, out var constantValue))
                            {
                                EmitConstant(constantValue);
                                break;
                            }
                            if (ZaxFunctions.TryLookup(token.Text, out var namedFunction))
                            {
                                if (!EmitCall(namedFunction, token)) return false;
                                break;
                            }
                            diagnostics.Add(new ZaxDiagnostic(ZaxDiagnosticCode.UnknownName, token, token.Text));
                            return false;
                        }
                }
            }

            if (typeStack.Count == 0)
            {
                diagnostics.Add(new ZaxDiagnostic(ZaxDiagnosticCode.EmptyExpression, 0, source?.Length ?? 0));
                return false;
            }
            if (typeStack.Count > 1)
            {
                var last = tokens[tokens.Count - 1];
                diagnostics.Add(new ZaxDiagnostic(
                    ZaxDiagnosticCode.ExtraOperands, last, last.Text, typeStack.Count.ToString()));
                return false;
            }

            var producedType = typeStack[0];
            if (expectedResultType.HasValue && expectedResultType.Value != producedType)
            {
                var expected = expectedResultType.Value;
                if (!IsImplicitlyConvertible(producedType, expected))
                {
                    var last = tokens[tokens.Count - 1];
                    diagnostics.Add(new ZaxDiagnostic(
                        ZaxDiagnosticCode.ResultTypeMismatch, last.Offset, last.Length,
                        expected.DisplayName(), producedType.DisplayName()));
                    return false;
                }
                instructions.Add(ZaxInstruction.Convert(producedType, expected));
                producedType = expected;
            }

            program = new ZaxProgram(
                source,
                instructions.ToArray(),
                constants.ToArray(),
                declaredVariables,
                maxStackSize,
                producedType);
            return true;
        }

        private static bool IsImplicitlyConvertible(ZaxValueType from, ZaxValueType to)
        {
            if (from == to) return true;
            if (!from.IsScalar()) return false;
            return to != ZaxValueType.Int;
        }

        private static int IndexOfVariable(ZaxVariable[] variables, string name)
        {
            for (var i = 0; i < variables.Length; ++i)
            {
                if (string.Equals(variables[i].Name, name, StringComparison.Ordinal)) return i;
            }
            return -1;
        }

        private static string DescribeTypes(ZaxValueType[] types, int count)
        {
            return string.Join(", ", Enumerable.Range(0, count).Select((i) => types[i].DisplayName()));
        }
    }
}
