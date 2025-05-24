using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using UnityEditor;
using VRC.Dynamics;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Dynamics.Constraint.Components;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using nadena.dev.ndmf.runtime;
using nadena.dev.modular_avatar.core;
using AnimatorAsCode.V1;
using KusakaFactory.Zatools.Ndmf.Framework;
using UnityObject = UnityEngine.Object;
using CustomEyeLookSettings = VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.CustomEyeLookSettings;
using Installer = KusakaFactory.Zatools.Runtime.EnhancedEyePointerInstaller;

namespace KusakaFactory.Zatools.Ndmf
{
    internal sealed class EepiTransforming : Pass<EepiTransforming>
    {
        public override string QualifiedName => nameof(EepiTransforming);
        public override string DisplayName => "Substitute eye bones and add constraints to them";

        protected override void Execute(BuildContext context)
        {
            var virtualControllerContext = context.Extension<VirtualControllerContext>();
            var state = context.GetState(EepiState.Initializer);
            if (state.Installer == null) return;

            // アバタールート
            EnsureAvatarRootPlacement(context.AvatarRootObject, state.Installer, state.MergeAnimator);

            // 対象の Eye ボーンと Eye Look 補正値の取得
            var (constrainedLeftEye, constrainedRightEye) = LocateEyeBones(context.AvatarDescriptor);
            var (lookAdjustLeft, lookAdjustRight) = (Quaternion.identity, Quaternion.identity);
            if (state.Installer.DummyEyeBones)
            {
                (constrainedLeftEye, lookAdjustLeft) = SubstituteEyeBone(constrainedLeftEye);
                (constrainedRightEye, lookAdjustRight) = SubstituteEyeBone(constrainedRightEye);
            }

            // Aim Constraint の設定
            var target = LocateEyePointerTarget(state.Installer);
            Component leftAim, rightAim;
            if (state.Installer.VRCConstraint)
            {
                leftAim = SetupConstaintsWithVRCVariant(target.transform, constrainedLeftEye);
                rightAim = SetupConstaintsWithVRCVariant(target.transform, constrainedRightEye);
            }
            else
            {
                leftAim = SetupConstaintsWithUnityVariant(target.transform, constrainedLeftEye);
                rightAim = SetupConstaintsWithUnityVariant(target.transform, constrainedRightEye);
            }

            // AnimatorController/AnimationClip の修正
            if (state.Installer.AdaptedFXLayer)
            {
                var virtualController = virtualControllerContext.Controllers[state.MergeAnimator];
                var leftPath = RuntimeUtil.RelativePath(context.AvatarRootObject, constrainedLeftEye.gameObject);
                var rightPath = RuntimeUtil.RelativePath(context.AvatarRootObject, constrainedRightEye.gameObject);
                AdaptBundledAnimationController(virtualController, state.Installer.DummyEyeBones, leftPath, rightPath);
            }

            // Eye Look の設定・修正
            AdjustEyeLookSettings(
                context.AvatarDescriptor,
                constrainedLeftEye,
                lookAdjustLeft,
                constrainedRightEye,
                lookAdjustRight
            );

            // Global Weight Override
            if (state.Installer.OverrideGlobalWeight)
            {
                GenerateGlobalWeightOverride(context, state.Installer, leftAim, rightAim);
            }

            state.Destroy();
        }

        private static void EnsureAvatarRootPlacement(GameObject avatarRoot, Installer installer, ModularAvatarMergeAnimator mergeAnimator)
        {
            // SeparateHeadAvatarRoot が設定されている場合、付属している(はずの)MergeAnimator を相対モードにしてルートを設定する
            if (installer.SeparateHeadAvatarRoot != null)
            {
                mergeAnimator.pathMode = MergeAnimatorPathMode.Relative;
                mergeAnimator.relativePathRoot.Set(installer.SeparateHeadAvatarRoot);
            }

            var installerTransform = installer.transform;
            var installRootTransform = installer.SeparateHeadAvatarRoot != null
                ? installer.SeparateHeadAvatarRoot.transform
                : avatarRoot.transform;
            if (installerTransform.parent != installRootTransform)
            {
                // installRootTransform に移動する
                installerTransform.parent = installRootTransform;
                ErrorReport.ReportError(new ZatoolNdmfError(ErrorSeverity.Information, "eepi.report.prefab-moved"));
            }
        }

        private static (Transform LeftEye, Transform RightEye) LocateEyeBones(VRCAvatarDescriptor avatarDescriptor)
        {
            if (avatarDescriptor.enableEyeLook)
            {
                return LocateEyeBonesFromVRCAvatarDescriptor(avatarDescriptor);
            }

            return LocateEyeBonesUnityAvatarAsset(avatarDescriptor.gameObject);
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

        private static VRCAimConstraint SetupConstaintsWithVRCVariant(Transform targetTransform, Transform constrainedEye)
        {
            if (constrainedEye == null) return null;

            var aimConstraint = constrainedEye.gameObject.AddComponent<VRCAimConstraint>();
            aimConstraint.enabled = false;
            aimConstraint.Sources.Add(new VRCConstraintSource(targetTransform, 1.0f, Vector3.zero, Vector3.zero));
            aimConstraint.AffectsRotationZ = false;
            aimConstraint.Locked = true;
            aimConstraint.IsActive = true;

            return aimConstraint;
        }

        private static AimConstraint SetupConstaintsWithUnityVariant(Transform targetTransform, Transform constrainedEye)
        {
            if (constrainedEye == null) return null;

            var aimConstraint = constrainedEye.gameObject.AddComponent<AimConstraint>();
            aimConstraint.enabled = false;
            aimConstraint.AddSource(new ConstraintSource { sourceTransform = targetTransform, weight = 1.0f });
            aimConstraint.rotationAxis = Axis.X | Axis.Y;
            aimConstraint.locked = true;
            aimConstraint.constraintActive = true;

            return aimConstraint;
        }

        private static void AdaptBundledAnimationController(VirtualAnimatorController controller, bool useDummyBones, string leftPath, string rightPath)
        {
            // 収録されている各種パスパターンのうち 1 種類だけ残して置き換える
            var preservedEyeComponent = useDummyBones ? "DummyEye_" : "Eye_";
            var fromLeftPath = $"Armature/Hips/Spine/Chest/Neck/Head/{preservedEyeComponent}L";
            var fromRightPath = $"Armature/Hips/Spine/Chest/Neck/Head/{preservedEyeComponent}R";
            Func<string, string> pathRewriter = (path) =>
            {
                if (path.StartsWith("EyePointer")) return path;
                if (path == fromLeftPath) return leftPath;
                if (path == fromRightPath) return rightPath;
                return null;
            };

            var virtualMotions = controller.Layers.SelectMany((l) => l.StateMachine.States.Select((st) => st.State.Motion));
            foreach (var virtualMotion in virtualMotions) RewriteVirtualMotion(virtualMotion, pathRewriter);
        }

        private static void RewriteVirtualMotion(VirtualMotion motion, Func<string, string> pathRewriter)
        {
            switch (motion)
            {
                case VirtualClip clip:
                    clip.EditPaths(pathRewriter);
                    break;
                case VirtualBlendTree blendTree:
                    foreach (var childMotion in blendTree.Children) RewriteVirtualMotion(childMotion.Motion, pathRewriter);
                    break;
            }
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
                ErrorReport.ReportError(new ZatoolNdmfError(descriptor, ErrorSeverity.Information, "eepi.report.placeholder-inserted"));

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

        private void GenerateGlobalWeightOverride(BuildContext context, Installer installer, Component leftAim, Component rightAim)
        {
            var aac = AacV1.Create(new AacConfiguration
            {
                SystemName = "EnhancedEyePointerInstaller",
                AnimatorRoot = context.AvatarRootObject.transform,
                DefaultValueRoot = context.AvatarRootObject.transform,
                AssetKey = GUID.Generate().ToString(),
                AssetContainer = context.AssetContainer,
                ContainerMode = AacConfiguration.Container.OnlyWhenPersistenceRequired,
                DefaultsProvider = new AacDefaultsProvider(false),
            });
            var fxController = aac.NewAnimatorController();
            var layer = fxController.NewLayer("GlobalWeightOverride").WithWeight(0.0f);
            var sepToggle = layer.BoolParameter("SEP/Toggle");
            var sepGlobalWeight = layer.FloatParameter("SEP/GlobalWeight");

            // Weight Animation
            var weightProperty = installer.VRCConstraint ? "GlobalWeight" : "m_Weight";
            var controlAnimation = aac.NewClip("GlobalWeightControl").Animating((ec) =>
            {
                var left = ec.BindingFromComponent(leftAim, weightProperty);
                var right = ec.BindingFromComponent(rightAim, weightProperty);
                var curve = new AnimationCurve(
                    new Keyframe { time = 0.0f, value = 0.0f },
                    new Keyframe { time = 100.0f, value = 1.0f }
                );
                AnimationUtility.SetKeyLeftTangentMode(curve, 0, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(curve, 0, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyLeftTangentMode(curve, 1, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(curve, 1, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetEditorCurve(ec.Clip, left, curve);
                AnimationUtility.SetEditorCurve(ec.Clip, right, curve);
            });

            // Transition
            var disabled = layer.NewState("Disabled").WithAnimation(controlAnimation).WithMotionTime(sepGlobalWeight);
            var enabled = layer.NewState("Enabled").WithAnimation(controlAnimation).WithMotionTime(sepGlobalWeight);
            disabled.TransitionsTo(enabled).When(sepToggle.IsTrue());
            enabled.TransitionsTo(disabled).When(sepToggle.IsFalse());

            // AnimatorLayerControl StateBehaviour
            var disableLayer = disabled.CreateNewBehaviour<VRCAnimatorLayerControl>();
            disableLayer.playable = VRC.SDKBase.VRC_AnimatorLayerControl.BlendableLayer.FX;
            disableLayer.layer = 0;
            disableLayer.goalWeight = 0.0f;
            disableLayer.blendDuration = 0.1f;
            var enableLayer = enabled.CreateNewBehaviour<VRCAnimatorLayerControl>();
            enableLayer.playable = VRC.SDKBase.VRC_AnimatorLayerControl.BlendableLayer.FX;
            enableLayer.layer = 0;
            enableLayer.goalWeight = 1.0f;
            enableLayer.blendDuration = 0.1f;

            // MergeAnimator
            var globalWeightControl = new GameObject("GlobalWeightControl");
            globalWeightControl.transform.parent = installer.transform;
            var mergeAnimator = globalWeightControl.AddComponent<ModularAvatarMergeAnimator>();
            mergeAnimator.animator = fxController.AnimatorController;
            mergeAnimator.layerPriority = 1000; // EyePointer のそれより後ならなんでもいい
            mergeAnimator.layerType = VRCAvatarDescriptor.AnimLayerType.FX;
            mergeAnimator.deleteAttachedAnimator = false;
            mergeAnimator.matchAvatarWriteDefaults = true;
            mergeAnimator.pathMode = MergeAnimatorPathMode.Absolute;

            // Parameter
            var parameters = globalWeightControl.AddComponent<ModularAvatarParameters>();
            var globalWeightConfig = new ParameterConfig
            {
                nameOrPrefix = "SEP/GlobalWeight",
                syncType = ParameterSyncType.Float,
                saved = false,
                localOnly = false,
                hasExplicitDefaultValue = true,
                defaultValue = installer.InitialGlobalWeight,
            };
            parameters.parameters.Add(globalWeightConfig);

            // Control Puppet
            if (installer.AddGlobalWeightControl)
            {
                // デフォルトのメニューが 7 項目しかない前提
                // VRCExpressionsMenu をコピーして Radial Puppet を差し込む
                var defaultMenuInstaller = installer.GetComponent<ModularAvatarMenuInstaller>();
                var originalRootMenuAsset = defaultMenuInstaller.menuToAppend;
                var copiedRootMenuAsset = UnityObject.Instantiate(originalRootMenuAsset);
                var originalMenuAsset = copiedRootMenuAsset.controls[0].subMenu;
                var copiedMenuAsset = UnityObject.Instantiate(originalMenuAsset);

                copiedMenuAsset.controls.Add(new VRCExpressionsMenu.Control
                {
                    type = VRCExpressionsMenu.Control.ControlType.RadialPuppet,
                    name = "EyePointer Weight",
                    subParameters = new[] { new VRCExpressionsMenu.Control.Parameter { name = "SEP/GlobalWeight" } },
                    value = installer.InitialGlobalWeight,
                });

                copiedRootMenuAsset.controls[0].subMenu = copiedMenuAsset;
                defaultMenuInstaller.menuToAppend = copiedRootMenuAsset;
            }
        }
    }
}
