using UnityEngine;
using UnityEngine.Animations;
using VRC.Dynamics;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.Constraint.Components;
using nadena.dev.ndmf;
using KusakaFactory.Zatools.Localization;
using Installer = KusakaFactory.Zatools.Runtime.EnhancedEyePointerInstaller;
using CustomEyeLookSettings = VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.CustomEyeLookSettings;

namespace KusakaFactory.Zatools.Modules.EnhancedEyePointerInstaller
{
    internal sealed class EepiTransforming : Pass<EepiTransforming>
    {
        public override string QualifiedName => nameof(EepiTransforming);
        public override string DisplayName => "Substitute eye bones and add constraints to them";

        protected override void Execute(BuildContext context)
        {
            var state = context.GetState(InstallerState.Initializer);
            if (state.Installer == null) return;

            // アバタールート
            EnsureAvatarRootPlacement(context.AvatarRootObject, state.Installer);

            // 対象の Eye ボーンと Eye Look 補正値の取得
            var (constrainedLeftEye, constrainedRightEye) = LocateEyeBones(context.AvatarRootObject);
            var (lookAdjustLeft, lookAdjustRight) = (Quaternion.identity, Quaternion.identity);
            if (state.Installer.DummyEyeBones)
            {
                (constrainedLeftEye, lookAdjustLeft) = SubstituteEyeBone(constrainedLeftEye);
                (constrainedRightEye, lookAdjustRight) = SubstituteEyeBone(constrainedRightEye);
            }

            // Aim Constraint の設定
            var target = LocateEyePointerTarget(state.Installer);
            if (state.Installer.VRCConstraint)
            {
                SetupConstaintsWithVRCVariant(target.transform, constrainedLeftEye);
                SetupConstaintsWithVRCVariant(target.transform, constrainedRightEye);
            }
            else
            {
                SetupConstaintsWithUnityVariant(target.transform, constrainedLeftEye);
                SetupConstaintsWithUnityVariant(target.transform, constrainedRightEye);
            }

            // Eye Look の設定・修正
            AdjustEyeLookSettings(
                context.AvatarDescriptor,
                constrainedLeftEye,
                lookAdjustLeft,
                constrainedRightEye,
                lookAdjustRight
            );

            state.Destroy();
        }

        private static void EnsureAvatarRootPlacement(GameObject avatarRoot, Installer installer)
        {
            var avatarTransform = avatarRoot.transform;
            var installerTransform = installer.transform;
            if (installerTransform.parent != avatarTransform)
            {
                // アバタールートに移動する
                installerTransform.parent = avatarTransform;
                ErrorReport.ReportError(ZatoolLocalization.NdmfLocalizer, ErrorSeverity.Information, "eepi.report.prefab-moved");
            }
        }

        private static (Transform LeftEye, Transform RightEye) LocateEyeBones(GameObject avatarRoot)
        {
            var avatarDescriptor = avatarRoot.GetComponent<VRCAvatarDescriptor>();
            if (avatarDescriptor.enableEyeLook)
            {
                return LocateEyeBonesFromVRCAvatarDescriptor(avatarDescriptor);
            }

            return LocateEyeBonesUnityAvatarAsset(avatarRoot);
        }

        private static (Transform LeftEye, Transform RightEye) LocateEyeBonesFromVRCAvatarDescriptor(VRCAvatarDescriptor avatarDescriptor)
        {
            var leftEyeTransform = avatarDescriptor.customEyeLookSettings.leftEye;
            var rightEyeTransform = avatarDescriptor.customEyeLookSettings.rightEye;
            return (leftEyeTransform, rightEyeTransform);
        }

        private static (Transform LeftEye, Transform RightEye) LocateEyeBonesUnityAvatarAsset(GameObject avatarRoot)
        {
            var animator = avatarRoot.GetComponent<Animator>();
            var leftEyeTransform = animator.GetBoneTransform(HumanBodyBones.LeftEye);
            var rightEyeTransform = animator.GetBoneTransform(HumanBodyBones.RightEye);
            return (leftEyeTransform, rightEyeTransform);
        }

        private static (Transform, Quaternion) SubstituteEyeBone(Transform originalEye)
        {
            if (originalEye == null) return (null, Quaternion.identity);

            // TODO: もっと intelligent にする
            var side = originalEye.name.Contains("L") || originalEye.name.Contains("left") ? "L" : "R";
            var dummyEye = new GameObject($"DummyEye_{side}");
            dummyEye.transform.SetParent(originalEye.parent, true);
            dummyEye.transform.position = originalEye.position;
            dummyEye.transform.localRotation = Quaternion.identity;
            originalEye.transform.SetParent(dummyEye.transform, true);
            return (dummyEye.transform, Quaternion.Inverse(originalEye.localRotation));
        }

        private static GameObject LocateEyePointerTarget(Installer installer)
        {
            var eyePointerTargetTransform = installer.transform.Find("Target");
            return eyePointerTargetTransform.gameObject;
        }

        private static void SetupConstaintsWithVRCVariant(Transform targetTransform, Transform constrainedEye)
        {
            if (constrainedEye == null) return;

            var aimConstraint = constrainedEye.gameObject.AddComponent<VRCAimConstraint>();
            aimConstraint.enabled = false;
            aimConstraint.Sources.Add(new VRCConstraintSource(targetTransform, 1.0f, Vector3.zero, Vector3.zero));
            aimConstraint.AffectsRotationZ = false;
            aimConstraint.Locked = true;
            aimConstraint.IsActive = true;
        }

        private static void SetupConstaintsWithUnityVariant(Transform targetTransform, Transform constrainedEye)
        {
            if (constrainedEye == null) return;

            var aimConstraint = constrainedEye.gameObject.AddComponent<AimConstraint>();
            aimConstraint.enabled = false;
            aimConstraint.AddSource(new ConstraintSource { sourceTransform = targetTransform, weight = 1.0f });
            aimConstraint.rotationAxis = Axis.X | Axis.Y;
            aimConstraint.locked = true;
            aimConstraint.constraintActive = true;
        }

        private static void AdjustEyeLookSettings(
            VRCAvatarDescriptor descriptor,
            Transform leftEye,
            Quaternion leftEyeAdjustment,
            Transform rightEye,
            Quaternion rightEyeAdjustment
        )
        {
            var adjustedSettings = descriptor.customEyeLookSettings;
            adjustedSettings.leftEye = leftEye.transform;
            adjustedSettings.rightEye = rightEye.transform;

            if (descriptor.enableEyeLook)
            {
                AdjustEyeRotations(adjustedSettings.eyesLookingStraight, leftEyeAdjustment, rightEyeAdjustment);
                AdjustEyeRotations(adjustedSettings.eyesLookingUp, leftEyeAdjustment, rightEyeAdjustment);
                AdjustEyeRotations(adjustedSettings.eyesLookingDown, leftEyeAdjustment, rightEyeAdjustment);
                AdjustEyeRotations(adjustedSettings.eyesLookingLeft, leftEyeAdjustment, rightEyeAdjustment);
                AdjustEyeRotations(adjustedSettings.eyesLookingRight, leftEyeAdjustment, rightEyeAdjustment);
            }
            else
            {
                // Eye Look は必ず有効にしなければならない
                // さもないとキャリブレーション中やリモートアバターとして最初にロードされた時などにウェイトがかかってた状態に戻ってしまう
                // see: https://github.com/kb10uy/kb10uy-zatools/issues/16
                descriptor.enableEyeLook = true;
                ErrorReport.ReportError(ZatoolLocalization.NdmfLocalizer, ErrorSeverity.Information, "eepi.report.placeholder-inserted");

                var zeroedLooking = new CustomEyeLookSettings.EyeRotations
                {
                    linked = true,
                    left = Quaternion.identity,
                    right = Quaternion.identity
                };
                adjustedSettings.eyesLookingStraight = zeroedLooking;
                adjustedSettings.eyesLookingUp = zeroedLooking;
                adjustedSettings.eyesLookingDown = zeroedLooking;
                adjustedSettings.eyesLookingLeft = zeroedLooking;
                adjustedSettings.eyesLookingRight = zeroedLooking;
            }

            descriptor.customEyeLookSettings = adjustedSettings;
        }

        private static void AdjustEyeRotations(
            CustomEyeLookSettings.EyeRotations original,
            Quaternion leftAdjustment,
            Quaternion rightAdjustment
        )
        {
            original.left = leftAdjustment * original.left;
            original.right = rightAdjustment * original.right;
        }
    }
}
