using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using nadena.dev.ndmf;
using nadena.dev.ndmf.preview;
using KusakaFactory.Zatools.Runtime;
using KusakaFactory.Zatools.Ndmf.Core;
using UnityObject = UnityEngine.Object;

namespace KusakaFactory.Zatools.Ndmf.Preview
{
    internal sealed class AhabssRenderFilter : ZatoolsRenderFilter<AdHocAdvancedBlendShapeSynthesis>
    {
        private static readonly TogglablePreviewNode _previewNode = CreateTogglablePreviewNode("Ad-hoc Advanced BlendShape Synthesis", "ad-hoc-advanced-blendshape-synthesis", false);

        protected override ZatoolsRenderFilterNode<AdHocAdvancedBlendShapeSynthesis> CreateNode() => new AhabssRenderFilterNode();
        protected override TogglablePreviewNode PreviewNode => _previewNode;
        internal static TogglablePreviewNode SwitchingPreviewNode => _previewNode;
    }

    internal sealed class AhabssRenderFilterNode : ZatoolsRenderFilterNode<AdHocAdvancedBlendShapeSynthesis>
    {
        private Mesh _synthesizedMesh = null;
        private ImmutableArray<(int Index, float Weight)> _weights = ImmutableArray<(int Index, float Weight)>.Empty;
        private AdHocAdvancedBlendShapeSynthesis[] _components = null;
        private List<Ahabss.FixedParameters> _observedParameters = null;

        public override RenderAspects WhatChanged => RenderAspects.Mesh | RenderAspects.Shapes;

        protected internal override ValueTask Initialize(
            SkinnedMeshRenderer original,
            SkinnedMeshRenderer proxyed,
            AdHocAdvancedBlendShapeSynthesis[] components,
            ComputeContext context
        )
        {
            _components = components;
            _observedParameters = ObserveParameters(context, components);
            var observedParameters = _observedParameters;

            if (proxyed == null || proxyed.sharedMesh == null) return default;

            var baseMesh = proxyed.sharedMesh;
            var currentMesh = baseMesh;
            var addedEntries = new List<Ahabss.FixedEntry>();
            foreach (var parameters in observedParameters)
            {
                if (!Ahabss.TryCompilePrograms(parameters, Ahabss.TryCompileSilently, out var programs)) continue;

                var nextMesh = UnityObject.Instantiate(currentMesh);
                nextMesh.name = $"{baseMesh.name} (Zatools modified)";
                addedEntries.AddRange(Ahabss.Process(currentMesh, nextMesh, parameters, programs).Added);
                if (currentMesh != baseMesh) UnityObject.DestroyImmediate(currentMesh);
                currentMesh = nextMesh;
            }
            if (currentMesh == baseMesh) return default;

            var weights = ImmutableArray.CreateBuilder<(int Index, float Weight)>();
            weights.AddRange(Ahabss.ResolveWeights(currentMesh, addedEntries));

            _synthesizedMesh = currentMesh;
            _weights = weights.ToImmutable();
            proxyed.sharedMesh = currentMesh;
            ApplyWeights(proxyed);
            ObjectRegistry.RegisterReplacedObject(baseMesh, currentMesh);

            return default;
        }

        protected override ZatoolsRenderFilterNode<AdHocAdvancedBlendShapeSynthesis> ZatoolsRefresh(
            IEnumerable<(Renderer, Renderer)> proxyPairs,
            ComputeContext context,
            RenderAspects nonzeroUpdatedAspects
        )
        {
            if ((nonzeroUpdatedAspects & RenderAspects.Mesh) != 0) return null;

            // Refresh には新しい ComputeContext が渡されるので、this を返すなら監視を登録し直す必要がある
            if (_components == null || _components.Any((c) => c == null)) return null;
            var currentParameters = ObserveParameters(context, _components);
            if (!currentParameters.SequenceEqual(_observedParameters)) return null;
            return this;
        }

        private static List<Ahabss.FixedParameters> ObserveParameters(ComputeContext context, AdHocAdvancedBlendShapeSynthesis[] components)
        {
            return components
                .Select((c) => context.Observe(c, Ahabss.FixedParameters.FixFromComponent, (op, np) => op == np))
                .ToList();
        }

        protected override void ZatoolsOnFrame(Renderer original, Renderer proxy)
        {
            if (_synthesizedMesh == null) return;
            if (proxy is SkinnedMeshRenderer proxyed)
            {
                proxyed.sharedMesh = _synthesizedMesh;
                ApplyWeights(proxyed);
            }
        }

        protected override void ZatoolsDispose()
        {
            if (_synthesizedMesh == null) return;

            UnityObject.DestroyImmediate(_synthesizedMesh);
            _synthesizedMesh = null;
            _weights = ImmutableArray<(int Index, float Weight)>.Empty;
        }

        private void ApplyWeights(SkinnedMeshRenderer renderer)
        {
            foreach (var (index, weight) in _weights) renderer.SetBlendShapeWeight(index, weight);
        }
    }
}
