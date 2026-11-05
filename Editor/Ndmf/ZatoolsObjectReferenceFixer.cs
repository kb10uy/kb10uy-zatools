using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using nadena.dev.ndmf.preview;
using nadena.dev.ndmf.runtime;
using KusakaFactory.Zatools.Ndmf.Core;
using KusakaFactory.Zatools.Runtime;

namespace KusakaFactory.Zatools.Ndmf
{
    internal static class ZatoolsObjectReferenceFixer
    {
        private static ComputeContext _context;
        private static int? _lastStage;

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            EditorApplication.delayCall += Process;
            EditorApplication.update += () =>
            {
                if (GetCurrentStageRootId(out _) != _lastStage) _context?.Invalidate?.Invoke();
            };
            EditorApplication.playModeStateChanged += (state) =>
            {
                if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += Process;
            };
        }

        private static int? GetCurrentStageRootId(out GameObject contentsRoot)
        {
            contentsRoot = null;

            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null || stage.prefabContentsRoot == null) return null;

            contentsRoot = stage.prefabContentsRoot;
            return contentsRoot.GetInstanceID();
        }

        private static void Process()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                _context = null;
                return;
            }

            _lastStage = GetCurrentStageRootId(out var contentsRoot);

            _context = new ComputeContext(nameof(ZatoolsObjectReferenceFixer));
            _context.InvokeOnInvalidate<object>(typeof(ZatoolsObjectReferenceFixer), (_) => Process());

            IEnumerable<IZatoolsObjectReferenceHolder> holders = _context.GetComponentsByType<IZatoolsObjectReferenceHolder>();
            if (contentsRoot != null)
            {
                holders = holders.Concat(_context.GetComponentsInChildren<IZatoolsObjectReferenceHolder>(contentsRoot, true));
            }

            foreach (var holder in holders)
            {
                var component = holder as Component;
                if (component == null) continue;

                var avatarRoot = _context.GetAvatarRoot(component.gameObject);
                if (avatarRoot == null) continue;

                var references = _context.Observe(
                    component,
                    (c) => ((IZatoolsObjectReferenceHolder)c).GetObjectReferences()
                        .Select((r) => (Reference: r, Target: r.UntypedTarget, r.Path))
                        .ToList(),
                    Enumerable.SequenceEqual
                );

                var recorded = false;
                foreach (var (reference, _, _) in references)
                {
                    var resolved = FindRepairTarget(avatarRoot.transform, reference);
                    if (resolved == null) continue;

                    foreach (var node in _context.ObservePath(resolved.transform))
                    {
                        _context.Observe(node.gameObject, (o) => o.name);
                    }

                    if (!NeedsRepair(avatarRoot.transform, reference, resolved)) continue;
                    if (!recorded) Undo.RecordObject(component, "");
                    recorded = true;
                    Repair(avatarRoot.transform, reference, resolved);
                }

                if (recorded)
                {
                    EditorUtility.SetDirty(component);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                }
            }
        }

        internal static Component FindRepairTarget(Transform avatarRoot, ZatoolsObjectReference reference)
        {
            if (string.IsNullOrEmpty(reference.Path)) return null;

            var (state, resolved) = ObjectReferenceResolver.ResolveUntyped(
                avatarRoot,
                reference.UntypedTarget,
                reference.Path,
                reference.TargetType
            );
            return state == ReferenceState.Resolved ? resolved : null;
        }

        internal static bool NeedsRepair(Transform avatarRoot, ZatoolsObjectReference reference, Component resolved)
        {
            return reference.UntypedTarget != resolved
                || reference.Path != RuntimeUtil.RelativePath(avatarRoot, resolved.transform);
        }

        internal static void Repair(Transform avatarRoot, ZatoolsObjectReference reference, Component resolved)
        {
            reference.UntypedTarget = resolved;
            reference.Path = RuntimeUtil.RelativePath(avatarRoot, resolved.transform);
        }
    }
}
