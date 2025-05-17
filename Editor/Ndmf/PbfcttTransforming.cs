using UnityEngine;
using UnityEditor;
using VRC.SDK3.Avatars.Components;
using nadena.dev.ndmf;
using KusakaFactory.Zatools.Localization;
using UnityObject = UnityEngine.Object;
using PbfcttComponent = KusakaFactory.Zatools.Runtime.PBFingerColliderTransferTarget;
using System.Linq;
using System;

#if ZATOOLS_AAO_EXISTS
using Anatawa12.AvatarOptimizer.API;
#endif

namespace KusakaFactory.Zatools.Ndmf
{
    internal sealed class PbfcttTransforming : Pass<PbfcttTransforming>
    {
        private const int MaxTargets = 6;

        protected override void Execute(BuildContext context)
        {
            var targetComponents = context.AvatarRootObject
                .GetComponentsInChildren<PbfcttComponent>()
                .Select((c, i) => (c, i));

            foreach ((var component, var index) in targetComponents)
            {
                if (index >= MaxTargets)
                {
                    ErrorReport.ReportError(ZatoolLocalization.NdmfLocalizer, ErrorSeverity.NonFatal, "pbfctt.report.too-many-targets", component.name);
                    UnityObject.DestroyImmediate(component);
                    continue;
                }

                Transfer(context, component, index);
            }
        }

        private void Transfer(BuildContext context, PbfcttComponent transferTarget, int index)
        {
            // VRChat クライアントで実行される際、Finger Collider は*極めて*非直感的な方法で配置される
            // 中間オブジェクトを挟むことで設定時の見た目通りに配置されるようにする必要がある
            // see also: https://note.com/labo405/n/nac5615af9b0e
            // カプセルの"下端"の位置に仮想的な親を作って transferTarget をその子にする
            var halfLength = Math.Max(transferTarget.Length / 2.0f - transferTarget.Radius, 0.0001f);
            var targetParentTransform = transferTarget.transform.parent;

            var intermediateParent = new GameObject($"{transferTarget.name}_Start");
            intermediateParent.transform.SetParent(transferTarget.transform, false);
            intermediateParent.transform.localPosition = Vector3.down * halfLength;
            intermediateParent.transform.SetParent(targetParentTransform, true);
            transferTarget.transform.SetParent(intermediateParent.transform, true);

            var colliderConfig = new VRCAvatarDescriptor.ColliderConfig
            {
                isMirrored = false,
                state = VRCAvatarDescriptor.ColliderConfig.State.Custom,
                transform = transferTarget.transform,
                position = Vector3.zero,
                rotation = Quaternion.identity,
                radius = transferTarget.Radius,
                height = transferTarget.Length - transferTarget.Radius * 2.0f,
            };

            switch (index)
            {
                case 0:
                    context.AvatarDescriptor.collider_fingerLittleL = colliderConfig;
                    break;
                case 1:
                    context.AvatarDescriptor.collider_fingerLittleR = colliderConfig;
                    break;
                case 2:
                    context.AvatarDescriptor.collider_fingerRingL = colliderConfig;
                    break;
                case 3:
                    context.AvatarDescriptor.collider_fingerRingR = colliderConfig;
                    break;
                case 4:
                    context.AvatarDescriptor.collider_fingerMiddleL = colliderConfig;
                    break;
                case 5:
                    context.AvatarDescriptor.collider_fingerMiddleR = colliderConfig;
                    break;
                default: throw new ArgumentException($"prohibited index: {index}");
            }
        }

        internal sealed class OptimizingDeletion : Pass<OptimizingDeletion>
        {
            protected override void Execute(BuildContext context)
            {
                var targetComponents = context.AvatarRootObject.GetComponentsInChildren<PbfcttComponent>();
                foreach (var targetComponent in targetComponents) UnityObject.DestroyImmediate(targetComponent);
            }
        }
    }

#if ZATOOLS_AAO_EXISTS
    [ComponentInformation(typeof(PbfcttComponent))]
    internal sealed class PbfcttComponentInformation : ComponentInformation<PbfcttComponent>
    {
        protected override void CollectDependency(PbfcttComponent component, ComponentDependencyCollector collector)
        {
            // Trace and Optimize で仮の親が削除されてしまうので登録する
            // もしかしたら AAO 側の考慮もれかも (cf. https://misskey.niri.la/notes/a7vplgv5wa)
            Debug.LogWarning($"fixing {component.transform.name} / {component.transform.parent.name}");
            collector.AddDependency(component.transform, component.transform.parent);
        }
    }
#endif
}
