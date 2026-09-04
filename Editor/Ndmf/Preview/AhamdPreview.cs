using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using nadena.dev.ndmf;
using nadena.dev.ndmf.preview;
using KusakaFactory.Zatools.Runtime;
using KusakaFactory.Zatools.Ndmf.Core;
using UnityObject = UnityEngine.Object;

namespace KusakaFactory.Zatools.Ndmf.Preview
{
    internal sealed class AhamdRenderFilter : ZatoolsRenderFilter<AdHocAdvancedMeshDuplication>
    {
        private static readonly TogglablePreviewNode _previewNode = CreateTogglablePreviewNode("Ad-hoc Advanced Mesh Duplication", "ad-hoc-advanced-mesh-duplication", false);

        protected override ZatoolsRenderFilterNode<AdHocAdvancedMeshDuplication> CreateNode() => new AhamdRenderFilterNode();
        protected override TogglablePreviewNode PreviewNode => _previewNode;
        internal static TogglablePreviewNode SwitchingPreviewNode => _previewNode;
    }

    internal sealed class AhamdRenderFilterNode : ZatoolsRenderFilterNode<AdHocAdvancedMeshDuplication>
    {
        private Mesh _generatedMesh = null;
        private List<Material> _materials = null;
        private SkinnedMeshRenderer _source = null;

        public override RenderAspects WhatChanged => RenderAspects.Mesh;

        protected internal override ValueTask Initialize(
            SkinnedMeshRenderer original,
            SkinnedMeshRenderer proxyed,
            AdHocAdvancedMeshDuplication[] components,
            ComputeContext context
        )
        {
            // AdHocAdvancedMeshDuplication has DisallowMultipleComponent, a renderer will have at most one.
            var observedParameter = context.Observe(components[0], Ahamd.FixedParameters.FixFromComponent, (op, np) => op == np);
            _source = observedParameter.Source;

            if (proxyed == null || proxyed.sharedMesh != null) return default;

            if (!Ahamd.TryCompilePrograms(observedParameter, Ahamd.TryCompileSilently, out var programs)) return default;

            var generatedMesh = new Mesh { name = $"Advanced Mesh Duplication for Preview" };
            Ahamd.Process(proxyed, generatedMesh, observedParameter, programs);

            _generatedMesh = generatedMesh;
            _materials = proxyed.sharedMaterials.ToList();
            proxyed.sharedMesh = generatedMesh;

            return default;
        }

        protected override ZatoolsRenderFilterNode<AdHocAdvancedMeshDuplication> ZatoolsRefresh(
            IEnumerable<(Renderer, Renderer)> proxyPairs,
            ComputeContext context,
            RenderAspects nonzeroUpdatedAspects
        )
        {
            if ((nonzeroUpdatedAspects & (RenderAspects.Mesh | RenderAspects.Shapes)) == 0) return this;
            return null;
        }

        protected override void ZatoolsOnFrame(Renderer original, Renderer proxy)
        {
            if (_generatedMesh == null || _materials == null || _source == null) return;
            if (proxy is SkinnedMeshRenderer proxyedSkinnedMeshRenderer)
            {
                proxyedSkinnedMeshRenderer.sharedMesh = _generatedMesh;
                proxyedSkinnedMeshRenderer.sharedMaterials = _materials.ToArray();
                proxyedSkinnedMeshRenderer.bones = _source.bones;
                proxyedSkinnedMeshRenderer.rootBone = _source.rootBone;
                proxyedSkinnedMeshRenderer.probeAnchor = _source.probeAnchor;
                proxyedSkinnedMeshRenderer.localBounds = _source.localBounds;
            }
        }

        protected override void ZatoolsDispose()
        {
            if (_generatedMesh == null) return;

            UnityObject.DestroyImmediate(_generatedMesh);
            _generatedMesh = null;
            _materials = null;
        }
    }
}
