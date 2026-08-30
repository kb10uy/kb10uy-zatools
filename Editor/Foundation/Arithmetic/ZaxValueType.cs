using Unity.Mathematics;

namespace KusakaFactory.Zatools.Foundation.Arithmetic
{
    public enum ZaxValueType : byte
    {
        Int = 0,
        Float = 1,
        Float2 = 2,
        Float3 = 3,
        Float4 = 4,
    }

    public static class ZaxValueTypeEx
    {
        public static int Dimension(this ZaxValueType type)
        {
            return type <= ZaxValueType.Float ? 1 : (int)type;
        }

        public static bool IsScalar(this ZaxValueType type)
        {
            return type <= ZaxValueType.Float;
        }

        public static bool IsVector(this ZaxValueType type)
        {
            return type >= ZaxValueType.Float2;
        }

        public static ZaxValueType Floatize(this ZaxValueType type)
        {
            return type == ZaxValueType.Int ? ZaxValueType.Float : type;
        }

        public static ZaxValueType OfDimension(int dimension)
        {
            return dimension <= 1 ? ZaxValueType.Float : (ZaxValueType)dimension;
        }

        public static bool TryPromote(ZaxValueType left, ZaxValueType right, out ZaxValueType promoted)
        {
            if (left == right)
            {
                promoted = left;
                return true;
            }
            if (left.IsVector() && right.IsVector())
            {
                promoted = default;
                return false;
            }

            promoted = OfDimension(math.max(left.Dimension(), right.Dimension()));
            return true;
        }

        public static int ByteSize(this ZaxValueType type)
        {
            return type <= ZaxValueType.Float ? 4 : (int)type * 4;
        }

        public static string DisplayName(this ZaxValueType type)
        {
            switch (type)
            {
                case ZaxValueType.Int: return "int";
                case ZaxValueType.Float: return "float";
                case ZaxValueType.Float2: return "float2";
                case ZaxValueType.Float3: return "float3";
                case ZaxValueType.Float4: return "float4";
                default: return "<invalid>";
            }
        }
    }
}
