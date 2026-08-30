using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace KusakaFactory.Zatools.Foundation.Arithmetic
{
    [BurstCompile]
    public unsafe struct ZaxEvaluateJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<ZaxInstruction> Instructions;
        [ReadOnly] public NativeArray<ZaxValue> Constants;
        [ReadOnly] public NativeArray<ZaxVariableBinding> Bindings;
        [WriteOnly] public NativeArray<ZaxValue> Results;
        public int VariableCount;

        public void Execute(int index)
        {
            var stack = stackalloc ZaxValue[ZaxNativeProgram.MaxStackSize];
            var variables = stackalloc ZaxValue[ZaxNativeProgram.MaxVariableCount];

            var bindings = (ZaxVariableBinding*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(Bindings);
            for (var i = 0; i < VariableCount; ++i) variables[i] = bindings[i].Read(index);

            Results[index] = ZaxEvaluator.Execute(
                (ZaxInstruction*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(Instructions),
                Instructions.Length,
                (ZaxValue*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(Constants),
                variables,
                stack);
        }

        public static ZaxEvaluateJob Create(
            in ZaxNativeProgram program,
            NativeArray<ZaxVariableBinding> bindings,
            NativeArray<ZaxValue> results)
        {
            if (!program.IsCreated) throw new ArgumentException("program is not allocated", nameof(program));
            if (!results.IsCreated) throw new ArgumentException("results is not allocated", nameof(results));
            if (!bindings.IsCreated) throw new ArgumentException("bindings is not allocated", nameof(bindings));
            if (bindings.Length != program.VariableCount)
            {
                throw new ArgumentException(
                    $"expression declares {program.VariableCount} variable(s) but {bindings.Length} binding(s) were given",
                    nameof(bindings));
            }

            for (var i = 0; i < bindings.Length; ++i)
            {
                var binding = bindings[i];
                var declared = program.VariableTypes[i];
                if (binding.Type != declared)
                {
                    throw new ArgumentException(
                        $"binding {i} is {binding.Type.DisplayName()} but the expression declares {declared.DisplayName()}",
                        nameof(bindings));
                }
                if (!binding.IsUniform && binding.Length < results.Length)
                {
                    throw new ArgumentException(
                        $"binding {i} holds {binding.Length} element(s) but {results.Length} are required",
                        nameof(bindings));
                }
            }

            return new ZaxEvaluateJob
            {
                Instructions = program.Instructions,
                Constants = program.Constants,
                Bindings = bindings,
                Results = results,
                VariableCount = program.VariableCount,
            };
        }

        public static JobHandle Schedule(
            in ZaxNativeProgram program,
            NativeArray<ZaxVariableBinding> bindings,
            NativeArray<ZaxValue> results,
            int innerloopBatchCount = 64,
            JobHandle dependency = default)
        {
            return Create(program, bindings, results).Schedule(results.Length, innerloopBatchCount, dependency);
        }
    }
}
