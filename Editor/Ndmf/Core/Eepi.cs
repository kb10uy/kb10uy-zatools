using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using nadena.dev.modular_avatar.core;
using CustomEyeLookSettings = VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.CustomEyeLookSettings;
using Installer = KusakaFactory.Zatools.Runtime.EnhancedEyePointerInstaller;

#if ZATOOLS_HAS_VRCSDK
using VRC.Dynamics;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Dynamics.Constraint.Components;
using nadena.dev.ndmf.runtime;
using UnityEngine.Animations;
using UnityEditor.Animations;
using UnityObject = UnityEngine.Object;
#endif

namespace KusakaFactory.Zatools.Ndmf.Core
{
    internal static class Eepi
    {
        internal const string ApsVersionFor420StyleHeadProxy = "000000000400000000020000000000";
        internal readonly static string ToggleParameterName = "SEP/Toggle";
        internal readonly static string GlobalWeightParameterName = "SEP/GlobalWeight";

        internal static void EnsureAvatarRootPlacement(GameObject avatarRoot, Installer installer, ModularAvatarMergeAnimator mergeAnimator)
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
                ErrorReport.ReportError(new ZatoolsNdmfError(ErrorSeverity.Information, "eepi.report.prefab-moved"));
            }
        }

#if ZATOOLS_HAS_VRCSDK
        internal static (Transform LeftEye, Transform RightEye) LocateEyeBones(VRCAvatarDescriptor avatarDescriptor)
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
#endif

        internal static (Transform, Quaternion) SubstituteEyeBone(Transform avatarRoot, Transform originalEye)
        {
            if (originalEye == null) return (null, Quaternion.identity);

            // TODO: もっと intelligent にする
            var side = originalEye.name.Contains("L") || originalEye.name.Contains("left") ? "L" : "R";
            var dummyEye = new GameObject($"DummyEye_{side}");
            dummyEye.transform.SetParent(originalEye.parent, true);
            dummyEye.transform.position = originalEye.position;
            dummyEye.transform.rotation = avatarRoot.rotation;
            originalEye.transform.SetParent(dummyEye.transform, true);
            return (dummyEye.transform, Quaternion.Inverse(originalEye.localRotation));
        }

        internal static Transform GenerateHandAnchorProxy(Transform avatarRoot, HumanBodyBones handBone)
        {
            var animator = avatarRoot.GetComponent<Animator>();
            var hand = animator.GetBoneTransform(handBone);
            var handParent = hand.parent;
            var armDirectionInHandSpace = hand.InverseTransformDirection(handParent.TransformDirection(hand.localPosition));
            var proxyRotation = Quaternion.FromToRotation(Vector3.up, armDirectionInHandSpace.normalized);

            var anchorProxy = new GameObject("TargetAnchorProxy");
            anchorProxy.transform.SetParent(hand, false);
            anchorProxy.transform.localRotation = proxyRotation;

            return anchorProxy.transform;
        }

        internal static Transform GenerateHeadAnchorProxy(Transform avatarRoot)
        {
            var animator = avatarRoot.GetComponent<Animator>();
            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            var forwardInHeadSpace = head.InverseTransformDirection(avatarRoot.transform.forward);
            var upwardInHeadSpace = head.InverseTransformDirection(avatarRoot.transform.up);
            var proxyRotation = Quaternion.LookRotation(forwardInHeadSpace, upwardInHeadSpace);

            var anchorProxy = new GameObject("TargetAnchorProxy");
            anchorProxy.transform.SetParent(head, false);
            anchorProxy.transform.localRotation = proxyRotation;

            return anchorProxy.transform;
        }

        internal static void SetupApsProperties(Component apsComponent, params Transform[] unfixAnchors)
        {
            var soAps = new SerializedObject(apsComponent);
            var unhandleEyesProperty = soAps.FindProperty("UnhandleEyes");
            var unfixObjectsProperty = soAps.FindProperty("UnfixObjects");

            unhandleEyesProperty.boolValue = true;
            var nextIndex = unfixObjectsProperty.arraySize;
            foreach (var unfixAnchor in unfixAnchors)
            {
                unfixObjectsProperty.InsertArrayElementAtIndex(nextIndex);
                var unfixElement = unfixObjectsProperty.GetArrayElementAtIndex(nextIndex);
                unfixElement.objectReferenceValue = unfixAnchor.gameObject;
                ++nextIndex;
            }
            soAps.ApplyModifiedProperties();
        }

        internal static void ReplaceAnchorParent(Transform anchor, Transform anchorProxy)
        {
            var boneProxy = anchor.GetComponent<ModularAvatarBoneProxy>();
            if (boneProxy == null) return;
            boneProxy.target = anchorProxy;
        }

        internal static GameObject LocateEyePointerTarget(Installer installer)
        {
            var eyePointerTargetTransform = installer.transform.Find("Target");
            return eyePointerTargetTransform.gameObject;
        }

#if ZATOOLS_HAS_VRCSDK
        internal static VRCAimConstraint SetupConstaintsWithVRCVariant(Transform targetTransform, Transform constrainedEye)
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

        internal static AimConstraint SetupConstaintsWithUnityVariant(Transform targetTransform, Transform constrainedEye)
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
#endif

        internal static void AdaptBundledAnimationController(VirtualAnimatorController controller, bool useDummyBones, string leftPath, string rightPath)
        {
            // 収録されている各種パスパターンのうち 1 種類だけ残して置き換える
            var preservedEyeComponent = useDummyBones ? "DummyEye_" : "Eye_";
            var fromLeftPath = $"Armature/Hips/Spine/Chest/Neck/Head/{preservedEyeComponent}L";
            var fromRightPath = $"Armature/Hips/Spine/Chest/Neck/Head/{preservedEyeComponent}R";
            Func<string, string> pathRewriter = (path) =>
            {
                if (path.StartsWith("EyePointer") || path == leftPath || path == rightPath) return path;
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

#if ZATOOLS_HAS_VRCSDK
        internal static void AdjustEyeLookSettings(
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
                ErrorReport.ReportError(new ZatoolsNdmfError(descriptor, ErrorSeverity.Information, "eepi.report.placeholder-inserted"));

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
#endif

        private static void AdjustEyeRotations(
            CustomEyeLookSettings.EyeRotations original,
            Quaternion leftAdjustment,
            Quaternion rightAdjustment
        )
        {
            original.left = original.left * leftAdjustment;
            original.right = original.right * rightAdjustment;
        }

#if ZATOOLS_HAS_VRCSDK
        internal static void GenerateGlobalWeightOverride(BuildContext context, Installer installer, Component leftAim, Component rightAim)
        {
            var virtualControllerContext = context.Extension<VirtualControllerContext>();
            var fxController = VirtualAnimatorController.Create(
                virtualControllerContext.CloneContext,
                "EnhancedEyePointerInstaller"
            );

            // Controller Parameters
            fxController.Parameters = fxController.Parameters
                .SetItem(ToggleParameterName, new AnimatorControllerParameter
                {
                    name = ToggleParameterName,
                    type = AnimatorControllerParameterType.Bool,
                    defaultBool = false,
                })
                .SetItem(GlobalWeightParameterName, new AnimatorControllerParameter
                {
                    name = GlobalWeightParameterName,
                    type = AnimatorControllerParameterType.Float,
                    defaultFloat = installer.InitialGlobalWeight,
                });

            // Layer
            // NDMF normalize pass forces the first layer's default weight to 1.0,
            // so keep a no-op first layer and put our controllable layer second.
            _ = fxController.AddLayer(new LayerPriority(0), "Base");
            var controlLayer = fxController.AddLayer(new LayerPriority(1), "SEP_GlobalWeightOverride");
            controlLayer.DefaultWeight = 0.0f;
            var controlLayerStateMachine = controlLayer.StateMachine;

            // Weight Animation
            var weightPropertyName = installer.VRCConstraint ? "GlobalWeight" : "m_Weight";
            var controlAnimation = VirtualClip.Create("Zatools-EEPI-GlobalWeightControl");
            var curve = new AnimationCurve(
                new Keyframe { time = 0.0f, value = 0.0f },
                new Keyframe { time = 100.0f, value = 1.0f }
            );
            AnimationUtility.SetKeyLeftTangentMode(curve, 0, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyRightTangentMode(curve, 0, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyLeftTangentMode(curve, 1, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyRightTangentMode(curve, 1, AnimationUtility.TangentMode.Linear);
            controlAnimation.SetFloatCurve(EditorCurveBinding.FloatCurve(
                RuntimeUtil.RelativePath(context.AvatarRootObject, leftAim.gameObject),
                leftAim.GetType(),
                weightPropertyName
            ), curve);
            controlAnimation.SetFloatCurve(EditorCurveBinding.FloatCurve(
                RuntimeUtil.RelativePath(context.AvatarRootObject, rightAim.gameObject),
                rightAim.GetType(),
                weightPropertyName
            ), curve);

            // State
            var disabledState = controlLayerStateMachine.AddState("Disabled", controlAnimation);
            disabledState.TimeParameter = GlobalWeightParameterName;
            var enabledState = controlLayerStateMachine.AddState("Enabled", controlAnimation);
            enabledState.TimeParameter = GlobalWeightParameterName;
            controlLayerStateMachine.DefaultState = disabledState;

            // Transition
            var toEnabledTransition = VirtualStateTransition.Create();
            toEnabledTransition.SetDestination(enabledState);
            toEnabledTransition.ExitTime = null;
            toEnabledTransition.Duration = 0.0f;
            toEnabledTransition.Conditions = toEnabledTransition.Conditions.Add(new AnimatorCondition
            {
                mode = AnimatorConditionMode.If,
                parameter = ToggleParameterName,
            });
            disabledState.Transitions = disabledState.Transitions.Add(toEnabledTransition);

            var toDisabledTransition = VirtualStateTransition.Create();
            toDisabledTransition.SetDestination(disabledState);
            toDisabledTransition.ExitTime = null;
            toDisabledTransition.Duration = 0.0f;
            toDisabledTransition.Conditions = toDisabledTransition.Conditions.Add(new AnimatorCondition
            {
                mode = AnimatorConditionMode.IfNot,
                parameter = ToggleParameterName,
            });
            enabledState.Transitions = enabledState.Transitions.Add(toDisabledTransition);

            // AnimatorLayerControl StateBehaviour
            var disableLayer = new VRCAnimatorLayerControl
            {
                playable = VRC.SDKBase.VRC_AnimatorLayerControl.BlendableLayer.FX,
                layer = controlLayer.VirtualLayerIndex,
                goalWeight = 0.0f,
                blendDuration = 0.1f,
            };
            disabledState.Behaviours = disabledState.Behaviours.Add(disableLayer);

            var enableLayer = new VRCAnimatorLayerControl
            {
                playable = VRC.SDKBase.VRC_AnimatorLayerControl.BlendableLayer.FX,
                layer = controlLayer.VirtualLayerIndex,
                goalWeight = 1.0f,
                blendDuration = 0.1f,
            };
            enabledState.Behaviours = enabledState.Behaviours.Add(enableLayer);

            // MA Merge Animator
            var globalWeightControl = new GameObject("GlobalWeightControl");
            globalWeightControl.transform.parent = installer.transform;
            var mergeAnimator = globalWeightControl.AddComponent<ModularAvatarMergeAnimator>();
            virtualControllerContext.Controllers[mergeAnimator] = fxController;
            mergeAnimator.layerPriority = 1000; // EyePointer のそれより後ならなんでもいい
            mergeAnimator.layerType = VRCAvatarDescriptor.AnimLayerType.FX;
            mergeAnimator.deleteAttachedAnimator = false;
            mergeAnimator.matchAvatarWriteDefaults = true;
            mergeAnimator.pathMode = MergeAnimatorPathMode.Absolute;

            // MA Parameters
            var parameters = globalWeightControl.AddComponent<ModularAvatarParameters>();
            var globalWeightConfig = new ParameterConfig
            {
                nameOrPrefix = GlobalWeightParameterName,
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
                    subParameters = new[] { new VRCExpressionsMenu.Control.Parameter { name = GlobalWeightParameterName } },
                    value = installer.InitialGlobalWeight,
                });

                copiedRootMenuAsset.controls[0].subMenu = copiedMenuAsset;
                defaultMenuInstaller.menuToAppend = copiedRootMenuAsset;
            }
        }
#endif

        internal static ((Transform OriginalLeftEye, Transform OriginalRightEye), (Transform LeftEye, Transform RightEye)) FindApsProxyedEyeBones(Transform avatarRoot, string apsVersion)
        {
            var animator = avatarRoot.GetComponent<Animator>();
            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            var originalLeftEye = animator.GetBoneTransform(HumanBodyBones.LeftEye);
            var originalRightEye = animator.GetBoneTransform(HumanBodyBones.RightEye);
            if (head == null || originalLeftEye == null || originalRightEye == null) return ((null, null), (null, null));

            Transform proxyedHead;
            if (apsVersion.CompareTo(ApsVersionFor420StyleHeadProxy) >= 0)
            {
                // APS >=4.2.0: Head の子要素として Head_Const/Head/ がある
                proxyedHead = head.Find($"{head.name}_Const/{head.name}");
            }
            else
            {
                // APS <4.2.0: Head の兄弟要素として Head_Track/Head_Chop/Head_Const/Head/ がある
                proxyedHead = head.parent.Find($"{head.name}_Track/{head.name}_Chop/{head.name}_Const/{head.name}");
            }
            if (proxyedHead == null) return ((originalLeftEye, originalRightEye), (null, null));

            var proxyedLeftEye = proxyedHead.Find(originalLeftEye.name);
            var proxyedRightEye = proxyedHead.Find(originalRightEye.name);
            return ((originalLeftEye, originalRightEye), (proxyedLeftEye, proxyedRightEye));
        }

#if ZATOOLS_HAS_VRCSDK
        internal static void DisableApsRotationConstraint(Transform left, Transform right)
        {
            var leftRotationConstraint = left.GetComponent<VRCRotationConstraint>();
            var rightRotationConstraint = right.GetComponent<VRCRotationConstraint>();
            leftRotationConstraint.enabled = false;
            leftRotationConstraint.IsActive = false;
            leftRotationConstraint.GlobalWeight = 0.0f;
            rightRotationConstraint.enabled = false;
            rightRotationConstraint.IsActive = false;
            rightRotationConstraint.GlobalWeight = 0.0f;
        }

        internal static void MoveApsAimConstraint(Transform source, Transform dest)
        {
            var original = source.GetComponent<VRCAimConstraint>();
            var copied = dest.gameObject.AddComponent<VRCAimConstraint>();
            copied.enabled = false;
            copied.Sources.Add(original.Sources[0]);
            copied.AffectsRotationZ = false;
            copied.Locked = true;
            copied.IsActive = true;

            UnityObject.DestroyImmediate(original);
        }

        internal static void FixApsEyeLookSettings(VRCAvatarDescriptor descriptor, Transform leftEye, Transform rightEye)
        {
            descriptor.enableEyeLook = true;

            var zeroedLooking = new CustomEyeLookSettings.EyeRotations
            {
                linked = true,
                left = Quaternion.identity,
                right = Quaternion.identity
            };
            var adjustedSettings = descriptor.customEyeLookSettings;
            adjustedSettings.leftEye = leftEye;
            adjustedSettings.rightEye = rightEye;
            adjustedSettings.eyesLookingStraight = zeroedLooking;
            adjustedSettings.eyesLookingUp = zeroedLooking;
            adjustedSettings.eyesLookingDown = zeroedLooking;
            adjustedSettings.eyesLookingLeft = zeroedLooking;
            adjustedSettings.eyesLookingRight = zeroedLooking;
            descriptor.customEyeLookSettings = adjustedSettings;
        }
#endif

        internal static (Component ApsComponent, string ApsVersion) DetectApsInstallation(GameObject avatarRoot)
        {
            Type apsComponentType = null, apsPluginType = null;
            foreach (var typeInDomain in AppDomain.CurrentDomain.GetAssemblies().SelectMany((a) => a.GetTypes()))
            {
                switch (typeInDomain.FullName)
                {
                    case "ZeroFactory.AvatarPoseSystem.NDMF.AvatarPoseSystem":
                        apsComponentType = typeInDomain;
                        break;
                    case "ZeroFactory.AvatarPoseSystem.NDMF.Editor.AvatarPoseSystemPlugin":
                        apsPluginType = typeInDomain;
                        break;
                }
            }
            if (apsComponentType == null || apsPluginType == null) return (null, "");

            return (avatarRoot.GetComponentInChildren(apsComponentType), DetectApsVersionNormalized(apsPluginType));
        }

        private static string DetectApsVersionNormalized(Type apsPluginType)
        {
            var apsPluginConstructor = apsPluginType.GetConstructor(Type.EmptyTypes);
            var apsPluginVersionField = apsPluginType.GetField("Version", BindingFlags.NonPublic | BindingFlags.Instance);
            var apsPluginInstance = apsPluginConstructor?.Invoke(null);
            var versionString = apsPluginVersionField?.GetValue(apsPluginInstance) as string;
            if (versionString == null) return "??????????";

            var normalizedVersionNumbers = versionString
                .Split('.')
                .Take(3)
                .Select((p) => int.TryParse(p, out var number) ? number.ToString("D10") : "??????????");
            return string.Concat(normalizedVersionNumbers);
        }
    }
}
