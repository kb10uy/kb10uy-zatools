using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using nadena.dev.ndmf.preview;
using nadena.dev.ndmf.runtime;
using KusakaFactory.Zatools.Runtime;

namespace KusakaFactory.Zatools.Ndmf.Core
{
    internal enum ReferenceState
    {
        Unset,
        Resolved,
        Unresolved,
    }

    internal readonly struct ResolvedReference<T> where T : Component
    {
        internal readonly ReferenceState State;
        internal readonly T Target;
        internal readonly string Path;

        internal ResolvedReference(ReferenceState state, T target, string path)
        {
            State = state;
            Target = target;
            Path = path;
        }
    }

    internal static class ObjectReferenceResolver
    {
        internal static ResolvedReference<T> Resolve<T>(Component owner, ZatoolsObjectReference<T> reference, T legacy)
            where T : Component
        {
            return Resolve(RuntimeUtil.FindAvatarInParents(owner.transform), legacy, reference.Target, reference.Path);
        }

        internal static List<ResolvedReference<T>> ResolveAll<T>(
            Component owner,
            IEnumerable<ZatoolsObjectReference<T>> references,
            IEnumerable<T> legacy
        )
            where T : Component
        {
            var avatarRoot = RuntimeUtil.FindAvatarInParents(owner.transform);
            return legacy
                .Where((target) => target != null)
                .Select((target) => Resolve(avatarRoot, target, null, null))
                .Concat(references.Select((reference) => Resolve(avatarRoot, null, reference.Target, reference.Path)))
                .Where((resolved) => resolved.State != ReferenceState.Unset)
                .ToList();
        }

        internal static ResolvedReference<T> Observe<TOwner, T>(
            ComputeContext context,
            TOwner owner,
            Func<TOwner, ZatoolsObjectReference<T>> reference,
            Func<TOwner, T> legacy
        )
            where TOwner : Component
            where T : Component
        {
            var (legacyTarget, target, path) = context.Observe(owner, (o) =>
            {
                var observed = reference(o);
                return (legacy(o), observed.Target, observed.Path);
            });
            var avatarRoot = context.GetAvatarRoot(owner.gameObject);
            var resolved = Resolve(avatarRoot != null ? avatarRoot.transform : null, legacyTarget, target, path);

            if (resolved.State == ReferenceState.Resolved)
            {
                foreach (var node in context.ObservePath(resolved.Target.transform))
                {
                    context.Observe(node.gameObject, (o) => o.name);
                }
            }
            else if (resolved.State == ReferenceState.Unresolved && avatarRoot != null)
            {
                context.GetComponentsInChildren<T>(avatarRoot, true);
            }

            return resolved;
        }

        internal static (ReferenceState State, Component Target) ResolveUntyped(
            Transform avatarRoot,
            Component target,
            string path,
            Type targetType
        )
        {
            var hasPath = !string.IsNullOrEmpty(path);
            if (target != null && (!hasPath || avatarRoot == null || target.transform.IsChildOf(avatarRoot)))
            {
                return (ReferenceState.Resolved, target);
            }

            if (!hasPath) return (ReferenceState.Unset, null);
            if (avatarRoot == null) return (ReferenceState.Unresolved, null);

            var found = avatarRoot.Find(path);
            var component = found != null ? found.GetComponent(targetType) : null;
            return component != null ? (ReferenceState.Resolved, component) : (ReferenceState.Unresolved, null);
        }

        internal static string PathFor(Component owner, Component target)
        {
            if (target == null) return "";
            var avatarRoot = RuntimeUtil.FindAvatarInParents(owner.transform);
            if (avatarRoot == null) return "";
            return RuntimeUtil.RelativePath(avatarRoot, target.transform) ?? "";
        }

        internal static void Assign<T>(Component owner, ZatoolsObjectReference<T> reference, T target)
            where T : Component
        {
            reference.Target = target;
            reference.Path = PathFor(owner, target);
        }

        private static ResolvedReference<T> Resolve<T>(Transform avatarRoot, T legacy, T target, string path)
            where T : Component
        {
            if (legacy != null) return new ResolvedReference<T>(ReferenceState.Resolved, legacy, "");

            var (state, resolved) = ResolveUntyped(avatarRoot, target, path, typeof(T));
            return new ResolvedReference<T>(state, (T)resolved, path);
        }
    }
}
