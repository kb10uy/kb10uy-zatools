using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using nadena.dev.ndmf;
using nadena.dev.modular_avatar.core;
using KusakaFactory.Zatools.Runtime;
using KusakaFactory.Zatools.Ndmf.Framework;
using UnityObject = UnityEngine.Object;

namespace KusakaFactory.Zatools.Ndmf
{
    internal sealed class AsvResolving : Pass<AsvResolving>
    {
        // セフィラちゃんの armature root が "Sonia" だったりするので子に Hips があるかどうかでも判定する必要が多分ある
        private static readonly ImmutableArray<Func<Transform, bool>> ArmatureLikeDetectors = ImmutableArray.CreateRange(new Func<Transform, bool>[] {
            (t) => Regex.IsMatch(t.name, "Armature|アーマチュア|ｱｰﾏﾁｭｱ" , RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
            (t) => t.EnumerateDirectChildren().Any((c) => Regex.IsMatch(c.name, "^Hips?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)),
        });

        protected override void Execute(BuildContext context)
        {
            ScanUnmergedArmaturesIn(context, context.AvatarRootTransform, ArmatureLikeStatus.Unrelated);
        }

        private void ScanUnmergedArmaturesIn(BuildContext context, Transform root, ArmatureLikeStatus status)
        {
            // strict mode (Indirect を警告・エラーにする) の実装のために直接枝刈りしない

            if (root.TryGetComponent<AvatarStatusValidatorIgnoredArmature>(out var ignoredArmature))
            {
                status = ArmatureLikeStatus.Ignored;
                UnityObject.DestroyImmediate(ignoredArmature);
            }

            // status の更新
            switch (status)
            {
                case ArmatureLikeStatus.Ignored:
                case ArmatureLikeStatus.MergeTarget:
                    break;

                case ArmatureLikeStatus.Unrelated:
                    if (ArmatureLikeDetectors.Any((detector) => detector(root)) && root.parent == context.AvatarRootTransform) status = ArmatureLikeStatus.MergeTarget;
                    if (root.TryGetComponent<ModularAvatarMergeArmature>(out var _)) status = ArmatureLikeStatus.DirectlyMerged;
                    if (root.TryGetComponent<ModularAvatarBoneProxy>(out var _)) status = ArmatureLikeStatus.Proxyed;
                    break;

                case ArmatureLikeStatus.DirectlyMerged:
                    // 今見ているのが armature-like でないか、Merge Armature が付いていればそのまま
                    if (!ArmatureLikeDetectors.Any((detector) => detector(root))) break;
                    if (root.TryGetComponent<ModularAvatarMergeArmature>(out var _)) break;
                    status = root.TryGetComponent<ModularAvatarBoneProxy>(out var _) ? ArmatureLikeStatus.Proxyed : ArmatureLikeStatus.IndirectlyMerged;
                    break;

                case ArmatureLikeStatus.IndirectlyMerged:
                    // 今見ているのが armature-like でないか、Merge Armature が付いていなければそのまま
                    if (!ArmatureLikeDetectors.Any((detector) => detector(root))) break;
                    if (!root.TryGetComponent<ModularAvatarMergeArmature>(out var _)) break;
                    status = root.TryGetComponent<ModularAvatarBoneProxy>(out var _) ? ArmatureLikeStatus.Proxyed : ArmatureLikeStatus.DirectlyMerged;
                    break;

                case ArmatureLikeStatus.Proxyed:
                    // armature-like で Merge Armature が付いていれば DireclyMerged にする
                    if (!ArmatureLikeDetectors.Any((detector) => detector(root))) break;
                    if (root.TryGetComponent<ModularAvatarMergeArmature>(out var _)) status = ArmatureLikeStatus.DirectlyMerged;
                    break;

                default:
                    throw new InvalidOperationException("unexpected state");
            }

            // Unreleated な armature-like をエラー対象とする
            if (status == ArmatureLikeStatus.Unrelated && ArmatureLikeDetectors.Any((detector) => detector(root)))
            {
                ErrorReport.ReportError(new ZatoolNdmfError(root.gameObject, ErrorSeverity.Error, "asv.report.suspicious-unmerged-armature"));
            }

            // 子の走査
            foreach (var child in root.EnumerateDirectChildren()) ScanUnmergedArmaturesIn(context, child, status);
        }

        internal enum ArmatureLikeStatus
        {
            /// <summary>
            /// Armature と関係がない。
            /// </summary>
            Unrelated,

            /// <summary>
            /// Zatools Ignored Armature によって無視される範囲にある。
            /// </summary>
            Ignored,

            /// <summary>
            /// 他の armature-like のマージ先になる。
            /// </summary>
            MergeTarget,

            /// <summary>
            /// 最も近い armature-like がマージされる。
            /// </summary>
            DirectlyMerged,

            /// <summary>
            /// アバタールートまでのどこかで armature-like がマージされるが、最も近いものはマージされない。
            /// </summary>
            IndirectlyMerged,

            /// <summary>
            /// 最も近い armature-like よりも近くに Bone Proxy がある。
            /// </summary>
            Proxyed,
        }
    }
}
