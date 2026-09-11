using System;
using System.Linq;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using VRC.SDK3.Avatars.Components;
using UnityObject = UnityEngine.Object;
using GwdoComponent = KusakaFactory.Zatools.Runtime.GlobalWriteDefaultsOverride;
using GwdoMode = KusakaFactory.Zatools.Runtime.WriteDefaultsOverrideMode;

namespace KusakaFactory.Zatools.Ndmf.Pass
{
    /// <remarks>
    /// This component is declared as "Optimizing" although its process is actually "Transforming".
    /// It is intended because NDMF cannot anchor the plugin execution position to the end of a BuildPhase.
    /// </remarks>
    [RunsOnPlatforms(WellKnownPlatforms.VRChatAvatar30)]
    internal sealed class GwdoOptimizing : ZatoolsPass<GwdoOptimizing>
    {
        protected override string ZatoolsPassName => nameof(GwdoOptimizing);
        protected override string ZatoolsPassDescription => "Override Write Defaults value";

        protected override void Execute(BuildContext context)
        {
            var components = context.AvatarRootObject.GetComponentsInChildren<GwdoComponent>();
            if (components.Length == 0) return;
            if (components.Length >= 2 || components[0].gameObject != context.AvatarRootObject)
            {
                ErrorReport.ReportError(new ZatoolsNdmfError(context.AvatarRootObject, ErrorSeverity.NonFatal, "gwdo.report.component-placement"));
            }
            Apply(context, components[0]);
        }

        private void Apply(BuildContext context, GwdoComponent component)
        {
            var wdTargetValue = component.Mode switch
            {
                GwdoMode.ForceOn => true,
                GwdoMode.ForceOff => false,
                _ => throw new ArgumentException($"Unexpected {nameof(GwdoMode)} value: {component.Mode}"),
            };

            var animatorServicesContext = context.Extension<AnimatorServicesContext>();
            var virtualControllerContext = animatorServicesContext.ControllerContext;
            var layerTypes = new[]
            {
                VRCAvatarDescriptor.AnimLayerType.Base,
                VRCAvatarDescriptor.AnimLayerType.Additive,
                VRCAvatarDescriptor.AnimLayerType.Gesture,
                VRCAvatarDescriptor.AnimLayerType.Action,
                VRCAvatarDescriptor.AnimLayerType.FX,
                VRCAvatarDescriptor.AnimLayerType.Sitting,
                VRCAvatarDescriptor.AnimLayerType.TPose,
                VRCAvatarDescriptor.AnimLayerType.IKPose,
            };
            foreach (var layerType in layerTypes)
            {
                if (virtualControllerContext.Controllers.TryGetValue(layerType, out var controller) && controller != null)
                {
                    ApplyForController(controller, wdTargetValue);
                }
            }

            UnityObject.DestroyImmediate(component);
        }

        private void ApplyForController(VirtualAnimatorController controller, bool wdTarget)
        {
            foreach (var virtualState in controller.AllReachableNodes().OfType<VirtualState>())
            {
                // Suppress redundant cache invalidation
                if (virtualState.WriteDefaultValues ^ wdTarget) virtualState.WriteDefaultValues = wdTarget;
            }
        }
    }
}
