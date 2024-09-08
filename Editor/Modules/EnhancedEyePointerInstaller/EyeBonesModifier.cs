using UnityEngine;
using UnityEngine.Animations;
using VRC.Dynamics;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.Constraint.Components;
using nadena.dev.ndmf;
using Installer = KusakaFactory.Zatools.Runtime.EnhancedEyePointerInstaller;

namespace KusakaFactory.Zatools.Modules.EnhancedEyePointerInstaller
{
    internal sealed class EyeBonesModifier : Pass<EyeBonesModifier>
    {
        public override string QualifiedName => nameof(EyeBonesModifier);
        public override string DisplayName => "Substitute eye bones and add constraints to them";

        protected override void Execute(BuildContext context)
        {
            var state = context.GetState(InstallerState.Initializer);
            if (state.Installer == null) return;

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

        private static (GameObject LeftEye, GameObject RightEye) LocateEyeBones(GameObject avatarRoot)
        {
            var avatarDescriptor = avatarRoot.GetComponent<VRCAvatarDescriptor>();
            if (avatarDescriptor.enableEyeLook)
            {
                return LocateEyeBonesFromVRCAvatarDescriptor(avatarDescriptor);
            }

            // TODO: もっと intelligent にする
            var leftEyeTransform = avatarRoot.transform.Find("Armature/Hips/Spine/Chest/Neck/Head/LeftEye");
            var rightEyeTransform = avatarRoot.transform.Find("Armature/Hips/Spine/Chest/Neck/Head/RightEye");
            return (leftEyeTransform.gameObject, rightEyeTransform.gameObject);
        }

        private static (GameObject LeftEye, GameObject RightEye) LocateEyeBonesFromVRCAvatarDescriptor(VRCAvatarDescriptor avatarDescriptor)
        {
            var leftEyeTransform = avatarDescriptor.customEyeLookSettings.leftEye;
            var rightEyeTransform = avatarDescriptor.customEyeLookSettings.rightEye;
            return (leftEyeTransform.gameObject, rightEyeTransform.gameObject);
        }

        private static GameObject SubstituteEyeBone(GameObject originalEye)
        {
            // TODO: もっと intelligent にする
            var side = originalEye.name.Contains("L") ? "L" : "R";
            var dummyEye = new GameObject($"DummyEye_{side}");
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

        private static void SetupConstaintsWithVRCVariant(Transform targetTransform, GameObject dummyEye)
        {
            var aimConstraint = dummyEye.AddComponent<VRCAimConstraint>();
            aimConstraint.enabled = false;
            aimConstraint.Sources.Add(new VRCConstraintSource(targetTransform, 1.0f, Vector3.zero, Vector3.zero));
            aimConstraint.AffectsRotationZ = false;
            aimConstraint.Locked = true;
            aimConstraint.IsActive = true;
        }

        private static void SetupConstaintsWithUnityVariant(Transform targetTransform, GameObject dummyEye)
        {
            var aimConstraint = dummyEye.AddComponent<AimConstraint>();
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
            // TODO: 角度の設定もいじるべき
            descriptor.customEyeLookSettings = settings;
        }
    }
}
