using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using nadena.dev.ndmf.preview;
using KusakaFactory.Zatools.Runtime;
using KusakaFactory.Zatools.Ndmf.Core;
using UnityObject = UnityEngine.Object;

namespace KusakaFactory.Zatools.Ndmf.Preview
{
    internal sealed class AhmsRenderFilter : ZatoolsRenderFilter<AdHocMeshSplit>
    {
        private static readonly TogglablePreviewNode _previewNode = CreateTogglablePreviewNode("Ad-Hoc Mesh Split", "ad-hoc-mesh-split");

        internal override ZatoolsRenderFilterNode<AdHocMeshSplit> CreateNode() => new AhmsRenderFilterNode();
        internal override TogglablePreviewNode PreviewNode => _previewNode;
        internal static TogglablePreviewNode SwitchingPreviewNode => _previewNode;
    }

    internal sealed class AhmsRenderFilterNode : ZatoolsRenderFilterNode<AdHocMeshSplit>
    {
        private Mesh _duplicatedMesh = null;
        private List<Material> _reassignedMaterials = null;

        public override RenderAspects WhatChanged => RenderAspects.Mesh | RenderAspects.Material;

        internal override async ValueTask Initialize(
            SkinnedMeshRenderer original,
            SkinnedMeshRenderer proxyed,
            AdHocMeshSplit[] components,
            ComputeContext context
        )
        {
            if (proxyed == null || proxyed.sharedMesh == null) return;

            var duplicatedMesh = UnityObject.Instantiate(proxyed.sharedMesh);
            duplicatedMesh.name = $"{duplicatedMesh.name} (Zatools modified)";

            // ProcessEdit --------
            var observedParameters = components.Select((c) => context.Observe(c, Ahms.FixedParameters.FixFromComponent, (op, np) => op == np));
            foreach (var c in components) if (c.Mask != null) context.Observe(c.Mask, (tm) => (tm.width, tm.height, tm.imageContentsHash));

            foreach (var parameters in observedParameters)
            {
                if (parameters.IsUnreadableMask) continue;
                if (parameters.SplitMaterial == null) continue;
                Ahms.Process(proxyed, duplicatedMesh, parameters);
            }
            foreach (var m in proxyed.sharedMaterials)
            {
                Debug.LogWarning(m.name);
            }
            // ProcessEdit --------

            _duplicatedMesh = duplicatedMesh;
            _reassignedMaterials = proxyed.sharedMaterials.ToList();
            proxyed.sharedMesh = duplicatedMesh;
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
