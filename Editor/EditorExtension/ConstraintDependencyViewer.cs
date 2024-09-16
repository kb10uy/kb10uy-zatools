using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Constraint.Components;
using KusakaFactory.Zatools.Localization;

namespace KusakaFactory.Zatools.EditorExtension
{
    internal sealed class ConstraintDependencyViewer
    {
        [MenuItem("GameObject/kb10uy/Show Constraint Dependency Tree", true)]
        internal static bool CanExecuteFromContextMenu()
        {
            return Selection.activeGameObject != null;
        }

        [MenuItem("GameObject/kb10uy/Show Constraint Dependency Tree", false)]
        internal static void ExecuteFromContextMenu()
        {
            var selectedGameObject = Selection.activeGameObject;
            var resolver = Resolver.ScanAndCreateResolver(selectedGameObject.transform);
        }

        internal sealed class Resolver
        {
            private Transform _root;
            // private Dictionary<string, Transform> _transformMap = new Dictionary<string, Transform>();
            private Dictionary<Transform, string> _reverseMap = new Dictionary<Transform, string>();
            private Dictionary<string, List<IConstraint>> _unityConstraintMap = new Dictionary<string, List<IConstraint>>();
            private Dictionary<string, List<VRCConstraintBase>> _vrcConstraintMap = new Dictionary<string, List<VRCConstraintBase>>();

            private Resolver(Transform root)
            {
                _root = root;
            }

            internal static Resolver ScanAndCreateResolver(Transform root)
            {
                var result = new Resolver(root);
                result.TraverseTransform();
                result.ScanVRCConstraints();
                return result;
            }

            private void TraverseTransform() => TraverseTransform(_root, "");
            private void TraverseTransform(Transform current, string pathPrefix)
            {
                // 同名 GameObject 対策
                var currentIdentifier = $"{current.name}:{current.GetSiblingIndex()}";
                var fullPath = $"{pathPrefix}/{currentIdentifier}";

                // _transformMap.Add(fullPath, current);
                _reverseMap.Add(current, fullPath);

                // IConstraint で取る関係上 Transform を引っ張ってこられない
                // Traverse 中に一緒にやるしかない
                if (!_unityConstraintMap.TryGetValue(fullPath, out var targetList))
                {
                    targetList = new List<IConstraint>();
                    _unityConstraintMap[fullPath] = targetList;
                }
                var constraints = current.GetComponentsInChildren<IConstraint>(true);
                foreach (var constraint in constraints) targetList.Add(constraint);

                for (int i = 0; i < current.childCount; ++i)
                {
                    TraverseTransform(current.GetChild(i), fullPath);
                }
            }

            private void ScanVRCConstraints()
            {
                // Target Transform を別に指定できるので移し替える必要がある
                // ScanUnityConstraints とは対照的に Traverse が完全に終わった後に実行しないといけない
                var vrcConstraintsInRoot = _root.GetComponentsInChildren<VRCConstraintBase>();
                foreach (var vrcConstraint in vrcConstraintsInRoot)
                {
                    var effectiveTargetTransform = vrcConstraint.GetEffectiveTargetTransform();
                    if (!_reverseMap.TryGetValue(effectiveTargetTransform, out var effectiveTargetPath))
                    {
                        Debug.LogWarning($"{effectiveTargetTransform.name} is out of the root GameObject!");
                        continue;
                    }

                    if (!_vrcConstraintMap.TryGetValue(effectiveTargetPath, out var targetList))
                    {
                        targetList = new List<VRCConstraintBase>();
                        _vrcConstraintMap[effectiveTargetPath] = targetList;
                    }
                    targetList.Add(vrcConstraint);
                }
            }
        }
    }
}
