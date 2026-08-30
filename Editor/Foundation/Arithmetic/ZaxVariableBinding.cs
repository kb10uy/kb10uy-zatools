using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace KusakaFactory.Zatools.Foundation.Arithmetic
{
    public unsafe struct ZaxVariableBinding
    {
        public void* Data;
        public ZaxValue Constant;
        public ZaxValueType Type;
        public int Stride;
        public int Length;

        public bool IsUniform => Data == null;

        public static ZaxVariableBinding FromArray<T>(NativeArray<T> source, ZaxValueType type) where T : unmanaged
        {
            if (!source.IsCreated) throw new ArgumentException("source is not allocated", nameof(source));

            var stride = UnsafeUtility.SizeOf<T>();
            if (stride < type.ByteSize())
            {
                throw new ArgumentException(
                    $"{typeof(T).Name} is {stride} byte(s) but {type.DisplayName()} needs {type.ByteSize()}",
                    nameof(type));
            }

            return new ZaxVariableBinding
            {
                Data = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(source),
                Constant = default,
                Type = type,
                Stride = stride,
                Length = source.Length,
            };
        }

        public static ZaxVariableBinding Uniform(ZaxValue value)
        {
            return new ZaxVariableBinding
            {
                Data = null,
                Constant = value,
                Type = value.Type,
                Stride = 0,
                Length = 0,
            };
        }

        public ZaxValue Read(int index)
        {
            if (Data == null) return Constant;

            switch (Type)
            {
                case ZaxValueType.Int:
                    return ZaxValue.FromInt(UnsafeUtility.ReadArrayElementWithStride<int>(Data, index, Stride));
                case ZaxValueType.Float:
                    return ZaxValue.FromFloat(UnsafeUtility.ReadArrayElementWithStride<float>(Data, index, Stride));
                case ZaxValueType.Float2:
                    return ZaxValue.FromFloat2(UnsafeUtility.ReadArrayElementWithStride<float2>(Data, index, Stride));
                case ZaxValueType.Float3:
                    return ZaxValue.FromFloat3(UnsafeUtility.ReadArrayElementWithStride<float3>(Data, index, Stride));
                default:
                    return ZaxValue.FromFloat4(UnsafeUtility.ReadArrayElementWithStride<float4>(Data, index, Stride));
            }
        }
    }
}
