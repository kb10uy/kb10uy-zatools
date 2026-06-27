using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using nadena.dev.ndmf;
using nadena.dev.ndmf.preview;
using nadena.dev.ndmf.runtime;
using KusakaFactory.Zatools.Runtime;
using KusakaFactory.Zatools.Ndmf.Core;
using UnityObject = UnityEngine.Object;

namespace KusakaFactory.Zatools.Ndmf.Preview
{
    internal sealed class EdwRenderFilter : ZatoolsRenderFilter<EyeholeDepthWrapper>
    {
        private static readonly TogglablePreviewNode _previewNode = CreateTogglablePreviewNode("Eyehole Depth Wrapper", "eyehole-depth-wrapper", false);

        internal override ZatoolsRenderFilterNode<EyeholeDepthWrapper> CreateNode() => new EdwRenderFilterNode();
        internal override TogglablePreviewNode PreviewNode => _previewNode;
        internal static TogglablePreviewNode SwitchingPreviewNode => _previewNode;
    }

    internal sealed class EdwRenderFilterNode : ZatoolsRenderFilterNode<EyeholeDepthWrapper>
    {
        internal static readonly string WrapperPreviewMaterialGuid = "c9f88a305477a2f4ea542f4d5700daaf";

        private Mesh _duplicatedMesh = null;
        private List<Material> _reassignedMaterials = null;

        public override RenderAspects WhatChanged => RenderAspects.Mesh | RenderAspects.Material;

        internal override ValueTask Initialize(
            SkinnedMeshRenderer original,
            SkinnedMeshRenderer proxyed,
            EyeholeDepthWrapper[] components,
            ComputeContext context
        )
        {
            if (proxyed == null || proxyed.sharedMesh == null) return default;

            var baseMesh = proxyed.sharedMesh;
            var duplicatedMesh = UnityObject.Instantiate(baseMesh);
            duplicatedMesh.name = $"{duplicatedMesh.name} (Zatools modified)";

            var avatarRoot = RuntimeUtil.FindAvatarInParents(original.transform);
            var observedParameters = components.Select((c) => context.Observe(
                c,
                (c) => Edw.FixedParameters.FixFromComponent(avatarRoot, c),
                (op, np) => op == np
            ));
            foreach (var component in components)
            {
                if (component.Basis != null) context.Observe(component.Basis, (t) => t.worldToLocalMatrix);
            }

            var wrapperMaterial = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(WrapperPreviewMaterialGuid));
            foreach (var parameters in observedParameters) Edw.Process(proxyed, duplicatedMesh, parameters, wrapperMaterial);

            _duplicatedMesh = duplicatedMesh;
            _reassignedMaterials = proxyed.sharedMaterials.ToList();
            proxyed.sharedMesh = duplicatedMesh;
            ObjectRegistry.RegisterReplacedObject(baseMesh, duplicatedMesh);

            return default;
        }

        internal override ZatoolsRenderFilterNode<EyeholeDepthWrapper> ZatoolsRefresh(
            IEnumerable<(Renderer, Renderer)> proxyPairs,
            ComputeContext context,
            RenderAspects nonzeroUpdatedAspects
        )
        {
            if ((nonzeroUpdatedAspects & RenderAspects.Mesh) == 0) return this;
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
