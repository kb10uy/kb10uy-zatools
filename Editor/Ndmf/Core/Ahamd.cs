using System.Collections.Immutable;
using KusakaFactory.Zatools.Foundation.Arithmetic;
using KusakaFactory.Zatools.Runtime;

namespace KusakaFactory.Zatools.Ndmf.Core
{
    internal static class Ahamd
    {
        internal static readonly ImmutableArray<ZaxVariable> CommonVariables = ImmutableArray.Create(
            new ZaxVariable("position", ZaxValueType.Float3),
            new ZaxVariable("normal", ZaxValueType.Float3),
            new ZaxVariable("tangent", ZaxValueType.Float4),
            new ZaxVariable("color", ZaxValueType.Float4),
            new ZaxVariable("uv0", ZaxValueType.Float4),
            new ZaxVariable("uv1", ZaxValueType.Float4),
            new ZaxVariable("uv2", ZaxValueType.Float4),
            new ZaxVariable("uv3", ZaxValueType.Float4),
            new ZaxVariable("uv4", ZaxValueType.Float4),
            new ZaxVariable("uv5", ZaxValueType.Float4),
            new ZaxVariable("uv6", ZaxValueType.Float4),
            new ZaxVariable("uv7", ZaxValueType.Float4),
            new ZaxVariable("scratch0", ZaxValueType.Float4),
            new ZaxVariable("scratch1", ZaxValueType.Float4),
            new ZaxVariable("scratch2", ZaxValueType.Float4),
            new ZaxVariable("scratch3", ZaxValueType.Float4)
        );

        internal static readonly ImmutableArray<ZaxVariable> SelectionVariables =
            CommonVariables.Add(new ZaxVariable("mask", ZaxValueType.Float4));

        internal static readonly ImmutableArray<ZaxVariable> ModificationVariables =
            CommonVariables.Add(new ZaxVariable("texture", ZaxValueType.Float4));

        internal static ZaxValueType ResultTypeOf(AhamdModificationTarget target)
        {
            switch (target)
            {
                case AhamdModificationTarget.Position:
                case AhamdModificationTarget.Normal:
                    return ZaxValueType.Float3;
                default:
                    return ZaxValueType.Float4;
            }
        }
    }
}
