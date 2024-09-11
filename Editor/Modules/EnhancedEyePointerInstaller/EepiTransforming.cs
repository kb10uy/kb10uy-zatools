using UnityEngine;
using UnityEngine.Animations;
using VRC.Dynamics;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.Constraint.Components;
using nadena.dev.ndmf;
using KusakaFactory.Zatools.Localization;
using Installer = KusakaFactory.Zatools.Runtime.EnhancedEyePointerInstaller;

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

            EnsureAvatarRootPlacement(context.AvatarRootObject, state.Installer);

            var (constrainedLeftEye, constrainedRightEye) = LocateEyeBones(context.AvatarRootObject);
            if (state.Installer.DummyEyeBones)
            {
                constrainedLeftEye = SubstituteEyeBone(constrainedLeftEye);
                constrainedRightEye = SubstituteEyeBone(constrainedRightEye);
            }

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

            ReplaceAvatarDescriptorEyeBones(context.AvatarDescriptor, constrainedLeftEye, constrainedRightEye);

            state.Destroy();
        }

        private static void EnsureAvatarRootPlacement(GameObject avatarRoot, Installer installer)
        {
            var avatarTransform = avatarRoot.transform;
            var installerTransform = installer.transform;
            if (installerTransform != avatarTransform)
            {
                // アバタールートに移動する
                installerTransform.parent = avatarTransform;
                ErrorReport.ReportError(ZatoolLocalization.NdmfLocalizer, ErrorSeverity.Information, "eepi.report.prefab-moved");
            }
        }

        private static (GameObject LeftEye, GameObject RightEye) LocateEyeBones(GameObject avatarRoot)
        {
            var avatarDescriptor = avatarRoot.GetComponent<VRCAvatarDescriptor>();
            if (avatarDescriptor.enableEyeLook)
            {
                return LocateEyeBonesFromVRCAvatarDescriptor(avatarDescriptor);
            }

            return LocateEyeBonesUnityAvatarAsset(avatarRoot);
        }

        private static (GameObject LeftEye, GameObject RightEye) LocateEyeBonesFromVRCAvatarDescriptor(VRCAvatarDescriptor avatarDescriptor)
        {
            var leftEyeTransform = avatarDescriptor.customEyeLookSettings.leftEye;
            var rightEyeTransform = avatarDescriptor.customEyeLookSettings.rightEye;
            return (leftEyeTransform.gameObject, rightEyeTransform.gameObject);
        }

        private static (GameObject LeftEye, GameObject RightEye) LocateEyeBonesUnityAvatarAsset(GameObject avatarRoot)
        {
            var animator = avatarRoot.GetComponent<Animator>();
            var leftEyeTransform = animator.GetBoneTransform(HumanBodyBones.LeftEye);
            var rightEyeTransform = animator.GetBoneTransform(HumanBodyBones.RightEye);
            return (leftEyeTransform.gameObject, rightEyeTransform.gameObject);
        }

        private static GameObject SubstituteEyeBone(GameObject originalEye)
        {
            if (originalEye == null) return null;

            // originalEye があった場所に originalEye と同じ名前で作ってあげないとどこかで「見失って」誤動作する可能性が高い
            var dummyEye = new GameObject(originalEye.name);
            dummyEye.transform.SetParent(originalEye.transform.parent, true);
            dummyEye.transform.position = originalEye.transform.position;
            dummyEye.transform.localRotation = Quaternion.identity;
            originalEye.transform.SetParent(dummyEye.transform, true);
            return dummyEye;
        }

        private static GameObject LocateEyePointerTarget(Installer installer)
        {
            var eyePointerTargetTransform = installer.transform.Find("Target");
            return eyePointerTargetTransform.gameObject;
        }

        private static void SetupConstaintsWithVRCVariant(Transform targetTransform, GameObject constrainedEye)
        {
            if (constrainedEye == null) return;

            var aimConstraint = constrainedEye.AddComponent<VRCAimConstraint>();
            aimConstraint.enabled = false;
            aimConstraint.Sources.Add(new VRCConstraintSource(targetTransform, 1.0f, Vector3.zero, Vector3.zero));
            aimConstraint.AffectsRotationZ = false;
            aimConstraint.Locked = true;
            aimConstraint.IsActive = true;
        }

        private static void SetupConstaintsWithUnityVariant(Transform targetTransform, GameObject constrainedEye)
        {
            if (constrainedEye == null) return;

            var aimConstraint = constrainedEye.AddComponent<AimConstraint>();
            aimConstraint.enabled = false;
            aimConstraint.AddSource(new ConstraintSource { sourceTransform = targetTransform, weight = 1.0f });
            aimConstraint.rotationAxis = Axis.X | Axis.Y;
            aimConstraint.locked = true;
            aimConstraint.constraintActive = true;
        }

        private static void ReplaceAvatarDescriptorEyeBones(VRCAvatarDescriptor descriptor, GameObject leftEye, GameObject rightEye)
        {
            if (!descriptor.enableEyeLook) return;

            var settings = descriptor.customEyeLookSettings;
            settings.leftEye = leftEye.transform;
            settings.rightEye = rightEye.transform;
            descriptor.customEyeLookSettings = settings;
        }
    }
}
