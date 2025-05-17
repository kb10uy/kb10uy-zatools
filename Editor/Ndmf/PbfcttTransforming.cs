using UnityEngine;
using UnityEngine.Animations;
using UnityEditor;
using VRC.Dynamics;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Dynamics.Constraint.Components;
using nadena.dev.ndmf;
using KusakaFactory.Zatools.Localization;
using UnityObject = UnityEngine.Object;
using PbfcttComponent = KusakaFactory.Zatools.Runtime.PBFingerColliderTransferTarget;
using System.Linq;

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
            UnityObject.DestroyImmediate(transferTarget);
        }
    }
}