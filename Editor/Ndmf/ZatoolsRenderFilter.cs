using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using nadena.dev.ndmf.preview;
using KusakaFactory.Zatools.Runtime;
using UnityObject = UnityEngine.Object;

namespace KusakaFactory.Zatools.Ndmf
{
    internal abstract class ZatoolsRenderFilter<TComponent> : IRenderFilter
    where TComponent : ZatoolsMeshEditingComponent
    {
        private readonly TogglablePreviewNode _toggleNode;

        protected ZatoolsRenderFilter(string name, string qualifiedName)
        {
            _toggleNode = TogglablePreviewNode.Create(() => name, $"org.kb10uy.zatools/{qualifiedName}");
        }

        public IEnumerable<TogglablePreviewNode> GetPreviewControlNodes() => new[] { _toggleNode };
        public bool IsEnabled(ComputeContext context) => context.Observe(_toggleNode.IsEnabled);

        public ImmutableList<RenderGroup> GetTargetGroups(ComputeContext context) =>
            context.GetComponentsByType<TComponent>()
                .Select((c) => (Renderer: c.GetComponent<SkinnedMeshRenderer>(), Component: c))
                .Where((p) => p.Renderer != null)
                .GroupBy((p) => p.Renderer)
                .Select((g) => RenderGroup.For(g.Key).WithData(g.Select((p) => p.Component).ToArray()))
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

        internal abstract ZatoolsRenderFilterNode<TComponent> CreateNode();
    }

    internal abstract class ZatoolsRenderFilterNode<TComponent> : IRenderFilterNode
    {
        private Mesh _duplicatedMesh = null;

        public abstract RenderAspects WhatChanged { get; }

        internal abstract ValueTask ProcessEdit(
            SkinnedMeshRenderer original,
            SkinnedMeshRenderer proxyed,
            Mesh duplicatedMesh,
            TComponent[] components,
            ComputeContext context
        );

        internal async ValueTask Initialize(
            SkinnedMeshRenderer original,
            SkinnedMeshRenderer proxyed,
            TComponent[] components,
            ComputeContext context
        )
        {
            var duplicatedMesh = UnityObject.Instantiate(proxyed.sharedMesh);
            duplicatedMesh.name = $"{duplicatedMesh.name} (Zatools modified)";

            await ProcessEdit(original, proxyed, duplicatedMesh, components, context);

            _duplicatedMesh = duplicatedMesh;
            proxyed.sharedMesh = duplicatedMesh;
        }

        void IRenderFilterNode.OnFrame(Renderer original, Renderer proxy)
        {
            if (_duplicatedMesh == null) return;
            if (proxy is SkinnedMeshRenderer proxyedSkinnedMeshRenderer) proxyedSkinnedMeshRenderer.sharedMesh = _duplicatedMesh;
        }

        void IDisposable.Dispose()
        {
            if (_duplicatedMesh == null) return;

            UnityObject.DestroyImmediate(_duplicatedMesh);
            _duplicatedMesh = null;
        }
    }
}
