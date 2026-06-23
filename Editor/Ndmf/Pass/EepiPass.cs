using System.Collections.Generic;
using UnityEngine;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using nadena.dev.ndmf.runtime;
using nadena.dev.ndmf.vrchat;
using nadena.dev.modular_avatar.core;
using KusakaFactory.Zatools.Ndmf.Core;
using Installer = KusakaFactory.Zatools.Runtime.EnhancedEyePointerInstaller;

namespace KusakaFactory.Zatools.Ndmf.Pass
{
    internal sealed class EepiResolving : ZatoolsPass<EepiResolving>
    {
        internal override string ZatoolsPassName => nameof(EepiResolving);
        internal override string ZatoolsPassDescription => "Set up GameObjects and constraints for EyePointer";

        protected override void Execute(BuildContext context)
        {
            var state = context.GetState(EepiState.Initializer);
            if (state.Installer == null) return;

            state.MergeAnimator = state.Installer.GetComponent<ModularAvatarMergeAnimator>();
            state.TargetAnchors = (
                state.Installer.transform.Find("WorldAnchor/TargetHand_L"),
                state.Installer.transform.Find("WorldAnchor/TargetHand_R"),
                state.Installer.transform.Find("WorldAnchor/TargetHead")
            );
            state.ApsInstallation = Eepi.DetectApsInstallation(context.AvatarRootObject);
        }
    }

    internal sealed class EepiGeneratingBeforeAps : ZatoolsPass<EepiGeneratingBeforeAps>
    {
        internal override string ZatoolsPassName => nameof(EepiGeneratingBeforeAps);
        internal override string ZatoolsPassDescription => "Generate target proxy if needed";

        protected override void Execute(BuildContext context)
        {
            var state = context.GetState(EepiState.Initializer);
            if (state.Installer == null) return;

            Transform left, right, head;
            if (state.Installer.FixTargetAxis)
            {
                left = Eepi.GenerateHandAnchorProxy(context.AvatarRootTransform, HumanBodyBones.LeftHand);
                right = Eepi.GenerateHandAnchorProxy(context.AvatarRootTransform, HumanBodyBones.RightHand);
                head = Eepi.GenerateHeadAnchorProxy(context.AvatarRootTransform);
                state.AnchorProxies = (left, right, head);
            }
            else
            {
                (left, right, head) = state.TargetAnchors;
            }

            if (state.ApsInstallation.Component != null)
            {
                state.Installer.VRCConstraint = true;
                state.Installer.DummyEyeBones = false;
                state.Installer.AdaptedFXLayer = true;
                Eepi.SetupApsProperties(state.ApsInstallation.Component, left, right, head);
                ErrorReport.ReportError(new ZatoolsNdmfError(context.VRChatAvatarDescriptor(), ErrorSeverity.Information, "eepi.report.aps-detected"));
            }
        }
    }

    internal sealed class EepiTransforming : ZatoolsPass<EepiTransforming>
    {
        internal override string ZatoolsPassName => nameof(EepiTransforming);
        internal override string ZatoolsPassDescription => "Substitute eye bones and add constraints to them";

        protected override void Execute(BuildContext context)
        {
            // MEMO: NDMF Portable Component で eye が取れるようになったら移行できるかも
            var avatarDescriptor = context.VRChatAvatarDescriptor();
            var virtualControllerContext = context.Extension<VirtualControllerContext>();
            var state = context.GetState(EepiState.Initializer);
            if (state.Installer == null) return;

            // アバタールート
            Eepi.EnsureAvatarRootPlacement(context.AvatarRootObject, state.Installer, state.MergeAnimator);

            // 対象の Eye ボーンと Eye Look 補正値の取得
            var (constrainedLeftEye, constrainedRightEye) = Eepi.LocateEyeBones(avatarDescriptor);
            var (lookAdjustLeft, lookAdjustRight) = (Quaternion.identity, Quaternion.identity);
            if (state.Installer.DummyEyeBones)
            {
                (constrainedLeftEye, lookAdjustLeft) = Eepi.SubstituteEyeBone(context.AvatarRootTransform, constrainedLeftEye);
                (constrainedRightEye, lookAdjustRight) = Eepi.SubstituteEyeBone(context.AvatarRootTransform, constrainedRightEye);
            }
            if (state.Installer.FixTargetAxis)
            {
                Eepi.ReplaceAnchorParent(state.TargetAnchors.Left, state.AnchorProxies.Left);
                Eepi.ReplaceAnchorParent(state.TargetAnchors.Right, state.AnchorProxies.Right);
                Eepi.ReplaceAnchorParent(state.TargetAnchors.Head, state.AnchorProxies.Head);
            }

            // Aim Constraint の設定
            var target = Eepi.LocateEyePointerTarget(state.Installer);
            Component leftAim, rightAim;
            if (state.Installer.VRCConstraint)
            {
                leftAim = Eepi.SetupConstaintsWithVRCVariant(target.transform, constrainedLeftEye);
                rightAim = Eepi.SetupConstaintsWithVRCVariant(target.transform, constrainedRightEye);
            }
            else
            {
                leftAim = Eepi.SetupConstaintsWithUnityVariant(target.transform, constrainedLeftEye);
                rightAim = Eepi.SetupConstaintsWithUnityVariant(target.transform, constrainedRightEye);
            }

            // AnimatorController/AnimationClip の修正
            if (state.Installer.AdaptedFXLayer)
            {
                var virtualController = virtualControllerContext.Controllers[state.MergeAnimator];
                var leftPath = RuntimeUtil.RelativePath(context.AvatarRootObject, constrainedLeftEye.gameObject);
                var rightPath = RuntimeUtil.RelativePath(context.AvatarRootObject, constrainedRightEye.gameObject);
                Eepi.AdaptBundledAnimationController(virtualController, state.Installer.DummyEyeBones, leftPath, rightPath);
            }

            // Eye Look の設定・修正
            Eepi.AdjustEyeLookSettings(
                avatarDescriptor,
                constrainedLeftEye,
                lookAdjustLeft,
                constrainedRightEye,
                lookAdjustRight
            );

            // Global Weight Override
            if (state.Installer.OverrideGlobalWeight) Eepi.GenerateGlobalWeightOverride(context, state.Installer, leftAim, rightAim);

            state.Destroy();
        }
    }

    internal sealed class EepiTransformingAfterMA : ZatoolsPass<EepiTransformingAfterMA>
    {
        internal override string ZatoolsPassName => nameof(EepiTransformingAfterMA);
        internal override string ZatoolsPassDescription => "Additional process for Enhanced Eye Pointer Installer";

        protected override void Execute(BuildContext context)
        {
            var state = context.GetState(EepiState.Initializer);
            if (!state.Installed || state.ApsInstallation.Version == "") return;
            var descriptor = context.VRChatAvatarDescriptor();
            var ((originalLeftEye, originalRightEye), (proxyedLeftEye, proxyedRightEye)) = Eepi.FindApsProxyedEyeBones(context.AvatarRootTransform, state.ApsInstallation.Version);
            if (proxyedLeftEye == null || proxyedRightEye == null) return;

            Eepi.DisableApsRotationConstraint(proxyedLeftEye, proxyedRightEye);
            Eepi.MoveApsAimConstraint(originalLeftEye, proxyedLeftEye);
            Eepi.MoveApsAimConstraint(originalRightEye, proxyedRightEye);
            Eepi.FixApsEyeLookSettings(descriptor, proxyedLeftEye, proxyedRightEye);
        }
    }

    [ParameterProviderFor(typeof(Installer))]
    internal sealed class EepiExtendedParameters : IParameterProvider
    {
        private readonly Installer _installer;

        public EepiExtendedParameters(Installer installer)
        {
            _installer = installer;
        }

        public IEnumerable<ProvidedParameter> GetSuppliedParameters(BuildContext context)
        {
            if (_installer.OverrideGlobalWeight && _installer.AddGlobalWeightControl)
            {
                yield return new ProvidedParameter(
                    Eepi.GlobalWeightParameterName,
                    ParameterNamespace.Animator,
                    _installer,
                    ZatoolsNdmfPlugin.Instance,
                    AnimatorControllerParameterType.Float
                )
                {
                    WantSynced = true,
                    IsHidden = false,
                    DefaultValue = _installer.InitialGlobalWeight,
                };
            }
        }
    }
}
