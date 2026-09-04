using System;
using System.Collections.Immutable;
using System.Linq;
using UnityEngine;
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

        internal struct FixedParameters : IEquatable<FixedParameters>
        {
            internal SkinnedMeshRenderer Source;
            internal Material OverrideMaterial;
            internal Texture2D SelectionTexture;
            internal UvChannel SelectionTextureUv;
            internal string SelectionExpression;
            internal float SelectionThreshold;
            internal ImmutableArray<FixedModification> Modifications;
            internal bool RecalculateNormals;
            internal bool RecalculateTangents;

            internal static FixedParameters FixFromComponent(AdHocAdvancedMeshDuplication component)
            {
                return new FixedParameters()
                {
                    Source = component.Source,
                    OverrideMaterial = component.OverrideMaterial,
                    SelectionTexture = component.SelectionTexture,
                    SelectionTextureUv = component.SelectionTextureUv,
                    SelectionExpression = component.SelectionExpression,
                    SelectionThreshold = component.SelectionThreshold,
                    Modifications = component.Modifications
                        .Where((m) => m != null && m.Enabled)
                        .Select(FixedModification.FixFromStep)
                        .ToImmutableArray(),
                    RecalculateNormals = component.RecalculateNormals,
                    RecalculateTangents = component.RecalculateTangents,
                };
            }

            public bool Equals(FixedParameters other)
            {
                return Source == other.Source
                    && OverrideMaterial == other.OverrideMaterial
                    && SelectionTexture == other.SelectionTexture
                    && SelectionTextureUv == other.SelectionTextureUv
                    && string.Equals(SelectionExpression, other.SelectionExpression, StringComparison.Ordinal)
                    && Mathf.Approximately(SelectionThreshold, other.SelectionThreshold)
                    && Modifications.SequenceEqual(other.Modifications)
                    && RecalculateNormals == other.RecalculateNormals
                    && RecalculateTangents == other.RecalculateTangents;
            }

            public override bool Equals(object obj) => obj is FixedParameters && Equals((FixedParameters)obj);

            public override int GetHashCode() =>
                (Source, OverrideMaterial, SelectionTexture, SelectionExpression, Modifications.Length).GetHashCode();

            public static bool operator ==(FixedParameters lhs, FixedParameters rhs) => lhs.Equals(rhs);

            public static bool operator !=(FixedParameters lhs, FixedParameters rhs) => !(lhs == rhs);
        }

        internal struct FixedModification : IEquatable<FixedModification>
        {
            internal Texture2D ExtraTexture;
            internal UvChannel ExtraTextureUv;
            internal AhamdModificationTarget Target;
            internal string Expression;

            internal ZaxValueType ResultType => ResultTypeOf(Target);

            internal static FixedModification FixFromStep(AhamdModificationStep step)
            {
                return new FixedModification()
                {
                    ExtraTexture = step.ExtraTexture,
                    ExtraTextureUv = step.ExtraTextureUv,
                    Target = step.Target,
                    Expression = step.Expression,
                };
            }

            public bool Equals(FixedModification other)
            {
                return ExtraTexture == other.ExtraTexture
                    && ExtraTextureUv == other.ExtraTextureUv
                    && Target == other.Target
                    && string.Equals(Expression, other.Expression, StringComparison.Ordinal);
            }

            public override bool Equals(object obj) => obj is FixedModification && Equals((FixedModification)obj);

            public override int GetHashCode() => (ExtraTexture, ExtraTextureUv, Target, Expression).GetHashCode();

            public static bool operator ==(FixedModification lhs, FixedModification rhs) => lhs.Equals(rhs);

            public static bool operator !=(FixedModification lhs, FixedModification rhs) => !(lhs == rhs);
        }
    }
}
