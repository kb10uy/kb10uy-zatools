using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using nadena.dev.ndmf.preview;
using nadena.dev.ndmf.runtime;
using KusakaFactory.Zatools.Runtime;

namespace KusakaFactory.Zatools.Ndmf
{
    internal static class ZatoolsRenderFilter
    {
        /// <summary>
        /// Finds the avatar root of the renderer, or falls back to the hierarchy root when the renderer is not under any avatar.
        /// Preview filters are evaluated for every renderer in the scene, so a fallback is required.
        /// </summary>
        internal static Transform FindAvatarRootOrFallback(Transform rendererTransform)
        {
            var avatarRoot = RuntimeUtil.FindAvatarInParents(rendererTransform);
            return avatarRoot != null ? avatarRoot : rendererTransform.root;
        }
    }

    /// <summary>
    /// Base class for render filters implemented by Zatools.
    /// </summary>
    /// <remarks>
    /// This type is public only so that render filters in external in-house assemblies can inherit from it.
    /// It is not a stable public API and may change or be removed without notice between releases.
    /// </remarks>
    public abstract class ZatoolsRenderFilter<TComponent> : IRenderFilter
    where TComponent : ZatoolsMeshEditingComponent
    {
        public IEnumerable<TogglablePreviewNode> GetPreviewControlNodes() => new[] { PreviewNode };
        public bool IsEnabled(ComputeContext context) => context.Observe(PreviewNode.IsEnabled);

        public ImmutableList<RenderGroup> GetTargetGroups(ComputeContext context) =>
            context.GetComponentsByType<TComponent>()
                .Where((c) => context.ActiveInHierarchy(c.gameObject))
                .Select((c) => (Renderer: context.GetComponent<SkinnedMeshRenderer>(c.gameObject), Component: c))
                .Where((p) => p.Renderer != null)
                .GroupBy((p) => p.Renderer)
                .Select((g) => RenderGroup.For(g.Key).WithData(
                    g.Select((p) => p.Component).ToArray(),
                    (a, b) => a.SequenceEqual(b)
                ))
                .ToImmutableList();

        public async Task<IRenderFilterNode> Instantiate(RenderGroup group, IEnumerable<(Renderer, Renderer)> proxyPairs, ComputeContext context)
        {
            var pp = proxyPairs.Single();
            if (!(pp.Item1 is SkinnedMeshRenderer original)) return null;
            if (!(pp.Item2 is SkinnedMeshRenderer proxyed)) return null;

            var node = CreateNode();
            var components = group.GetData<TComponent[]>();
            await node.Initialize(original, proxyed, components, context);
            return node;
        }

        protected abstract ZatoolsRenderFilterNode<TComponent> CreateNode();
        protected abstract TogglablePreviewNode PreviewNode { get; }

        protected static TogglablePreviewNode CreateTogglablePreviewNode(string name, string qualifiedName, bool initialState = true)
        {
            return TogglablePreviewNode.Create(() => name, $"org.kb10uy.zatools/{qualifiedName}", initialState);
        }
    }

    /// <summary>
    /// Base class for render filter nodes implemented by Zatools.
    /// </summary>
    /// <remarks>
    /// This type is public only so that render filter nodes in external in-house assemblies can inherit from it.
    /// It is not a stable public API and may change or be removed without notice between releases.
    /// </remarks>
    public abstract class ZatoolsRenderFilterNode<TComponent> : IRenderFilterNode
    {
        public abstract RenderAspects WhatChanged { get; }

        protected internal abstract ValueTask Initialize(
            SkinnedMeshRenderer original,
            SkinnedMeshRenderer proxyed,
            TComponent[] components,
            ComputeContext context
        );

        protected virtual ZatoolsRenderFilterNode<TComponent> ZatoolsRefresh(
            IEnumerable<(Renderer, Renderer)> proxyPairs,
            ComputeContext context,
            RenderAspects nonzeroUpdatedAspects
        ) => null;
        protected abstract void ZatoolsOnFrame(Renderer original, Renderer proxy);
        protected abstract void ZatoolsDispose();

        Task<IRenderFilterNode> IRenderFilterNode.Refresh(
            IEnumerable<(Renderer, Renderer)> proxyPairs,
            ComputeContext context,
            RenderAspects updatedAspects
        )
        {
            if (updatedAspects == 0) return Task.FromResult<IRenderFilterNode>(null);
            var refreshed = ZatoolsRefresh(proxyPairs, context, updatedAspects);
            return Task.FromResult<IRenderFilterNode>(refreshed);
        }

        void IRenderFilterNode.OnFrame(Renderer original, Renderer proxy)
        {
            ZatoolsOnFrame(original, proxy);
        }

        void IDisposable.Dispose()
        {
            ZatoolsDispose();
        }
    }
}
