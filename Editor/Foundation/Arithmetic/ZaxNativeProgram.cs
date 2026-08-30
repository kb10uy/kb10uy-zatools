using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace KusakaFactory.Zatools.Foundation.Arithmetic
{
    public unsafe struct ZaxNativeProgram : IDisposable
    {
        public const int MaxStackSize = 64;
        public const int MaxVariableCount = 16;

        public NativeArray<ZaxInstruction> Instructions;
        public NativeArray<ZaxValue> Constants;
        public NativeArray<ZaxValueType> VariableTypes;
        public int VariableCount;
        public int StackSize;
        public ZaxValueType ResultType;

        public bool IsCreated => Instructions.IsCreated;

        public static ZaxNativeProgram Allocate(ZaxProgram program, Allocator allocator)
        {
            if (program == null) throw new ArgumentNullException(nameof(program));
            if (program.StackSize > MaxStackSize)
            {
                throw new ArgumentException(
                    $"expression needs {program.StackSize} stack slots but the job evaluator provides {MaxStackSize}",
                    nameof(program));
            }
            if (program.Variables.Length > MaxVariableCount)
            {
                throw new ArgumentException(
                    $"expression declares {program.Variables.Length} variable(s) but the job evaluator provides {MaxVariableCount}",
                    nameof(program));
            }

            var variableTypes = new NativeArray<ZaxValueType>(program.Variables.Length, allocator);
            for (var i = 0; i < program.Variables.Length; ++i) variableTypes[i] = program.Variables[i].Type;

            return new ZaxNativeProgram
            {
                Instructions = new NativeArray<ZaxInstruction>(program.Instructions, allocator),
                Constants = new NativeArray<ZaxValue>(program.Constants, allocator),
                VariableTypes = variableTypes,
                VariableCount = program.Variables.Length,
                StackSize = program.StackSize,
                ResultType = program.ResultType,
            };
        }

        public ZaxValue Evaluate(ZaxValue* variables, ZaxValue* stack)
        {
            return ZaxEvaluator.Execute(
                (ZaxInstruction*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(Instructions),
                Instructions.Length,
                (ZaxValue*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(Constants),
                variables,
                stack);
        }

        public void Dispose()
        {
            if (Instructions.IsCreated) Instructions.Dispose();
            if (Constants.IsCreated) Constants.Dispose();
            if (VariableTypes.IsCreated) VariableTypes.Dispose();
        }
    }
}
