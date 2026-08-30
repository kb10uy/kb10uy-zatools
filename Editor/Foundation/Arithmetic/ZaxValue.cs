using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace KusakaFactory.Zatools.Foundation.Arithmetic
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct ZaxValue : IEquatable<ZaxValue>
    {
        private readonly int4 _bits;
        private readonly ZaxValueType _type;

        public ZaxValueType Type => _type;

        private ZaxValue(int4 bits, ZaxValueType type)
        {
            _bits = bits;
            _type = type;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ZaxValue FromInt(int value) => new ZaxValue(new int4(value, 0, 0, 0), ZaxValueType.Int);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ZaxValue FromFloat(float value) => new ZaxValue(new int4(math.asint(value), 0, 0, 0), ZaxValueType.Float);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ZaxValue FromFloat2(float2 value) => new ZaxValue(new int4(math.asint(value), 0, 0), ZaxValueType.Float2);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ZaxValue FromFloat3(float3 value) => new ZaxValue(new int4(math.asint(value), 0), ZaxValueType.Float3);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ZaxValue FromFloat4(float4 value) => new ZaxValue(math.asint(value), ZaxValueType.Float4);

        public static ZaxValue FromFloatN(float4 value, ZaxValueType type)
        {
            switch (type)
            {
                case ZaxValueType.Int: return FromInt((int)value.x);
                case ZaxValueType.Float: return FromFloat(value.x);
                case ZaxValueType.Float2: return FromFloat2(value.xy);
                case ZaxValueType.Float3: return FromFloat3(value.xyz);
                case ZaxValueType.Float4: return FromFloat4(value);
                default: throw new ArgumentOutOfRangeException(nameof(type));
            }
        }

        public int AsInt => _bits.x;
        public float AsFloat => math.asfloat(_bits.x);
        public float2 AsFloat2 => math.asfloat(_bits.xy);
        public float3 AsFloat3 => math.asfloat(_bits.xyz);
        public float4 AsFloat4 => math.asfloat(_bits);

        public float4 Broadcast()
        {
            switch (_type)
            {
                case ZaxValueType.Int: return new float4(_bits.x);
                case ZaxValueType.Float: return new float4(math.asfloat(_bits.x));
                case ZaxValueType.Float2: return new float4(math.asfloat(_bits.xy), 0.0f, 0.0f);
                case ZaxValueType.Float3: return new float4(math.asfloat(_bits.xyz), 0.0f);
                default: return math.asfloat(_bits);
            }
        }

        public float4 ToFloat4()
        {
            switch (_type)
            {
                case ZaxValueType.Int: return new float4(_bits.x, 0.0f, 0.0f, 0.0f);
                case ZaxValueType.Float: return new float4(math.asfloat(_bits.x), 0.0f, 0.0f, 0.0f);
                case ZaxValueType.Float2: return new float4(math.asfloat(_bits.xy), 0.0f, 0.0f);
                case ZaxValueType.Float3: return new float4(math.asfloat(_bits.xyz), 0.0f);
                default: return math.asfloat(_bits);
            }
        }

        public ZaxValue ConvertTo(ZaxValueType type)
        {
            if (_type == type) return this;
            return FromFloatN(Broadcast(), type);
        }

        public bool Equals(ZaxValue other)
        {
            return _type == other._type && math.all(_bits == other._bits);
        }

        public override bool Equals(object obj) => obj is ZaxValue other && Equals(other);

        public override int GetHashCode()
        {
            return (int)math.hash(_bits) ^ ((int)_type * 397);
        }

        public override string ToString()
        {
            var culture = CultureInfo.InvariantCulture;
            switch (_type)
            {
                case ZaxValueType.Int:
                    return AsInt.ToString(culture);
                case ZaxValueType.Float:
                    return AsFloat.ToString("R", culture);
                case ZaxValueType.Float2:
                {
                    var v = AsFloat2;
                    return $"({v.x.ToString("R", culture)}, {v.y.ToString("R", culture)})";
                }
                case ZaxValueType.Float3:
                {
                    var v = AsFloat3;
                    return $"({v.x.ToString("R", culture)}, {v.y.ToString("R", culture)}, {v.z.ToString("R", culture)})";
                }
                case ZaxValueType.Float4:
                {
                    var v = AsFloat4;
                    return $"({v.x.ToString("R", culture)}, {v.y.ToString("R", culture)}, {v.z.ToString("R", culture)}, {v.w.ToString("R", culture)})";
                }
                default:
                    return "<invalid>";
            }
        }
    }
}
