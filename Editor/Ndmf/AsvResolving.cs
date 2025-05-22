using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using nadena.dev.ndmf;
using nadena.dev.modular_avatar.core;

namespace KusakaFactory.Zatools.Ndmf
{
    internal sealed class AsvResolving : Pass<AsvResolving>
    {
        private static readonly ImmutableArray<ArmatureLikeDetector> ArmatureLikeDetectors = ImmutableArray.CreateRange(new[] {
            new ArmatureLikeDetector("Armature|アーマチュア|ｱｰﾏﾁｭｱ", (t) => t),
            new ArmatureLikeDetector("^hips?$", (t) => t.parent),
        });

        protected override void Execute(BuildContext context)
        {
            ScanUnmergedArmature(context);
        }

        private void ScanUnmergedArmature(BuildContext context)
        {
            var avatarGameObjects = context.AvatarRootObject.GetComponentsInChildren<Transform>(true);
            var armatureLikeTransforms = avatarGameObjects
                .SelectMany((t) => ArmatureLikeDetectors.Select((d) => d.SearchRootFor(t)))
                .Where((t) => t != null)
                .Distinct();

            foreach (var armatureLike in armatureLikeTransforms)
            {
                if (armatureLike.TryGetComponent<ModularAvatarMergeArmature>(out var mergeArmature)) continue;
                if (armatureLike.parent == context.AvatarRootTransform) continue;
                ErrorReport.ReportError(new ZatoolNdmfError(armatureLike.gameObject, ErrorSeverity.Error, "asv.report.suspicious-unmerged-armature"));
            }
        }

        private sealed class ArmatureLikeDetector
        {
            private Regex _pattern;
            private Func<Transform, Transform> _rootFilter;

            public ArmatureLikeDetector(string pattern, Func<Transform, Transform> filter)
            {
                _pattern = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                _rootFilter = filter;
            }

            public Transform SearchRootFor(Transform current)
            {
                return _pattern.IsMatch(current.name) ? _rootFilter(current) : null;
            }
        }
    }
}
