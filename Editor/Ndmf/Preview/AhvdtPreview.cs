using System.Collections.Generic;
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
    internal sealed class AhvdtRenderFilter : ZatoolsRenderFilter<AdHocVertexDataTransfer>
    {
        private static readonly TogglablePreviewNode _previewNode = CreateTogglablePreviewNode("Vertex Data Transfer", "vertex-data-transfer");

        internal override ZatoolsRenderFilterNode<AdHocVertexDataTransfer> CreateNode() => new AhvdtRenderFilterNode();
        internal override TogglablePreviewNode PreviewNode => _previewNode;
        internal static TogglablePreviewNode SwitchingPreviewNode => _previewNode;
    }

    internal sealed class AhvdtRenderFilterNode : ZatoolsRenderFilterNode<AdHocVertexDataTransfer>
    {
        private Mesh _duplicatedMesh = null;

        public override RenderAspects WhatChanged => RenderAspects.Mesh;

        internal override ValueTask Initialize(
            SkinnedMeshRenderer original,
            SkinnedMeshRenderer proxyed,
            AdHocVertexDataTransfer[] components,
            ComputeContext context
        )
        {
            if (proxyed == null || proxyed.sharedMesh == null) return default;

            var baseMesh = proxyed.sharedMesh;
            var duplicatedMesh = UnityObject.Instantiate(baseMesh);
            duplicatedMesh.name = $"{baseMesh.name} (Zatools modified)";

            // テクスチャ内容の変更も Observe 対象にする
            var observedParameters = components.Select((c) => context.Observe(c, Ahvdt.FixedParameters.FixFromComponent, (op, np) => op == np));
            foreach (var c in components) if (c.SourceTexture != null) context.Observe(c.SourceTexture, (tm) => (tm.width, tm.height, tm.imageContentsHash));

            foreach (var parameters in observedParameters)
            {
                if (parameters.SourceTexture != null && parameters.TransferTarget != VertexDataTransferTarget.Disabled) Ahvdt.Process(duplicatedMesh, parameters);
            }

            _duplicatedMesh = duplicatedMesh;
            proxyed.sharedMesh = duplicatedMesh;
            ObjectRegistry.RegisterReplacedObject(baseMesh, duplicatedMesh);

            return default;
        }

        internal override ZatoolsRenderFilterNode<AdHocVertexDataTransfer> ZatoolsRefresh(
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
            if (_duplicatedMesh == null) return;
            if (proxy is SkinnedMeshRenderer proxyed) proxyed.sharedMesh = _duplicatedMesh;
        }

        internal override void ZatoolsDispose()
        {
            if (_duplicatedMesh == null) return;

            UnityObject.DestroyImmediate(_duplicatedMesh);
            _duplicatedMesh = null;
        }
    }
}
