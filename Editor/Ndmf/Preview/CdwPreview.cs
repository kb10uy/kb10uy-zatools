using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using nadena.dev.ndmf.preview;
using KusakaFactory.Zatools.Runtime;
using KusakaFactory.Zatools.Ndmf.Core;
using UnityObject = UnityEngine.Object;

namespace KusakaFactory.Zatools.Ndmf.Preview
{
    internal sealed class CdwRenderFilter : ZatoolsRenderFilter<ConvexDepthWrapper>
    {
        private static readonly TogglablePreviewNode _previewNode = CreateTogglablePreviewNode("Convex Depth Wrapper", "convex-depth-wrapper", false);

        internal override ZatoolsRenderFilterNode<ConvexDepthWrapper> CreateNode() => new CdwRenderFilterNode();
        internal override TogglablePreviewNode PreviewNode => _previewNode;
        internal static TogglablePreviewNode SwitchingPreviewNode => _previewNode;
    }

    internal sealed class CdwRenderFilterNode : ZatoolsRenderFilterNode<ConvexDepthWrapper>
    {
        internal static readonly string WrapperPreviewMaterialGuid = "c9f88a305477a2f4ea542f4d5700daaf";

        private Mesh _duplicatedMesh = null;
        private List<Material> _reassignedMaterials = null;

        public override RenderAspects WhatChanged => RenderAspects.Mesh | RenderAspects.Material;

        internal override ValueTask Initialize(
            SkinnedMeshRenderer original,
            SkinnedMeshRenderer proxyed,
            ConvexDepthWrapper[] components,
            ComputeContext context
        )
        {
            if (proxyed == null || proxyed.sharedMesh == null) return default;

            var duplicatedMesh = UnityObject.Instantiate(proxyed.sharedMesh);
            duplicatedMesh.name = $"{duplicatedMesh.name} (Zatools modified)";

            var observedParameters = components.Select((c) => context.Observe(
                c,
                Cdw.FixedParameters.FixFromComponent,
                (op, np) => op == np
            ));

            var wrapperMaterial = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(WrapperPreviewMaterialGuid));
            foreach (var parameters in observedParameters) Cdw.Process(proxyed, duplicatedMesh, parameters, wrapperMaterial);

            _duplicatedMesh = duplicatedMesh;
            _reassignedMaterials = proxyed.sharedMaterials.ToList();
            proxyed.sharedMesh = duplicatedMesh;

            return default;
        }

        internal override ZatoolsRenderFilterNode<ConvexDepthWrapper> ZatoolsRefresh(
            IEnumerable<(Renderer, Renderer)> proxyPairs,
            ComputeContext context,
            RenderAspects nonzeroUpdatedAspects
        )
        {
            if ((nonzeroUpdatedAspects & (RenderAspects.Mesh | RenderAspects.Shapes)) == 0) return this;
            return null;
        }

        internal override void ZatoolsOnFrame(Renderer original, Renderer proxy)
        {
            if (_duplicatedMesh == null || _reassignedMaterials == null) return;
            if (proxy is SkinnedMeshRenderer proxyedSkinnedMeshRenderer)
            {
                proxyedSkinnedMeshRenderer.sharedMesh = _duplicatedMesh;
                proxyedSkinnedMeshRenderer.sharedMaterials = _reassignedMaterials.ToArray();
            }
        }

        internal override void ZatoolsDispose()
        {
            if (_duplicatedMesh == null) return;

            UnityObject.DestroyImmediate(_duplicatedMesh);
            _duplicatedMesh = null;
            _reassignedMaterials = null;
        }
    }
}
