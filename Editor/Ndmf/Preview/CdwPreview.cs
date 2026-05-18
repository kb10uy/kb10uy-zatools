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
        private bool _separateMesh = false;
        private SkinnedMeshRenderer _separateSource = null;

        public override RenderAspects WhatChanged => RenderAspects.Mesh | RenderAspects.Material;

        internal override ValueTask Initialize(
            SkinnedMeshRenderer original,
            SkinnedMeshRenderer proxyed,
            ConvexDepthWrapper[] components,
            ComputeContext context
        )
        {
            var wrapperMaterial = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(WrapperPreviewMaterialGuid));
            // ConvexDepthWrapper has DisallowMultipleComponent, a renderer will have at most one.
            var observedParameter = context.Observe(components[0], Cdw.FixedParameters.FixFromComponent, (op, np) => op == np);

            _separateMesh = observedParameter.SeparateSmr;
            _separateSource = observedParameter.SourceMeshRenderer;

            if (_separateMesh)
            {
                if (proxyed == null || proxyed.sharedMesh != null) return default;

                var generatedMesh = new Mesh { name = $"Convex Depth Wrapper for Preview" };
                Cdw.ProcessSeparate(proxyed, generatedMesh, observedParameter, wrapperMaterial);

                _duplicatedMesh = generatedMesh;
                _reassignedMaterials = proxyed.sharedMaterials.ToList();
                proxyed.sharedMesh = generatedMesh;
            }
            else
            {
                if (proxyed == null || proxyed.sharedMesh == null) return default;

                var duplicatedMesh = UnityObject.Instantiate(proxyed.sharedMesh);
                duplicatedMesh.name = $"{duplicatedMesh.name} (Zatools modified)";
                Cdw.Process(proxyed, duplicatedMesh, observedParameter, wrapperMaterial);

                _duplicatedMesh = duplicatedMesh;
                _reassignedMaterials = proxyed.sharedMaterials.ToList();
                proxyed.sharedMesh = duplicatedMesh;
            }

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

                if (_separateMesh)
                {
                    proxyedSkinnedMeshRenderer.bones = _separateSource.bones;
                    proxyedSkinnedMeshRenderer.rootBone = _separateSource.rootBone;
                    proxyedSkinnedMeshRenderer.probeAnchor = _separateSource.probeAnchor;
                    proxyedSkinnedMeshRenderer.localBounds = _separateSource.localBounds;
                }
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
