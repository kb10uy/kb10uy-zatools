using UnityEngine;
using KusakaFactory.Zatools.Runtime;

namespace KusakaFactory.Zatools.Ndmf.Core
{
    internal static class Gmpo
    {
        internal struct FixedOverride
        {
            internal string Name;
            internal MaterialPropertyOverrideType TargetType;
            internal float FloatValue;
            internal int IntValue;
            internal Vector4 VectorValue;

            internal static FixedOverride Fix(MaterialPropertyOverride propertyOverride)
            {
                return new FixedOverride
                {
                    Name = propertyOverride.Name,
                    TargetType = propertyOverride.TargetType,
                    FloatValue = propertyOverride.FloatValue,
                    IntValue = propertyOverride.IntValue,
                    VectorValue = propertyOverride.VectorValue,
                };
            }
        }
    }
}