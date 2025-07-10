using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using VRC.SDK3.Avatars.Components;
using nadena.dev.ndmf;
using KusakaFactory.Zatools.Ndmf.Framework;
using UnityObject = UnityEngine.Object;
using PbfcttComponent = KusakaFactory.Zatools.Runtime.PBFingerColliderTransferTarget;

namespace KusakaFactory.Zatools.Ndmf.Pass
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
                    ErrorReport.ReportError(new ZatoolsNdmfError(component.gameObject, ErrorSeverity.NonFatal, "pbfctt.report.too-many-targets", component.name));
                    UnityObject.DestroyImmediate(component);
                    continue;
                }

                Transfer(context, component, index);
            }
        }

        private void Transfer(BuildContext context, PbfcttComponent transferTarget, int index)
        {
            // Finger Collider は PB の連続ボーンのように parent transform に依存する
            // parent transform から target transform に向かってカプセルが"生える"ように配置される
            // see also: https://note.com/labo405/n/nac5615af9b0e

            //          |
            // S--------T
            // \--------C........
            // PBFCTT で設定されたカプセルの下端 (-Y endpoint) に親となる transform を挿入し、設定されたカプセルと同じ形状になるようにする
            var colliderCenter = new GameObject($"{transferTarget.name}_Center");
            colliderCenter.transform.SetParent(transferTarget.transform, false);

            var colliderStart = new GameObject($"{transferTarget.name}_Start");
            colliderStart.transform.SetParent(colliderCenter.transform, false);
            colliderStart.transform.localPosition = Vector3.down * Math.Max(transferTarget.Length / 2.0f - transferTarget.Radius, 0.0001f);
            colliderStart.transform.SetParent(transferTarget.transform, true);
            colliderCenter.transform.SetParent(colliderStart.transform, true);

            var colliderConfig = new VRCAvatarDescriptor.ColliderConfig
            {
                isMirrored = false,
                state = VRCAvatarDescriptor.ColliderConfig.State.Custom,
                transform = colliderCenter.transform,
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

            UnityObject.DestroyImmediate(transferTarget);
        }
    }
}
