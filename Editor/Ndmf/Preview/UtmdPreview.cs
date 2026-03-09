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
    internal sealed class UtmdRenderFilter : ZatoolsRenderFilter<UvTileMapDistribution>
    {
        private static readonly TogglablePreviewNode _previewNode = CreateTogglablePreviewNode("UV Tile Map Distribution", "uv-tile-map-distribution");

        internal override ZatoolsRenderFilterNode<UvTileMapDistribution> CreateNode() => new UtmdRenderFilterNode();
        internal override TogglablePreviewNode PreviewNode => _previewNode;
        internal static TogglablePreviewNode SwitchingPreviewNode => _previewNode;
    }

    internal sealed class UtmdRenderFilterNode : ZatoolsRenderFilterNode<UvTileMapDistribution>
    {
        private Mesh _duplicatedMesh = null;

        public override RenderAspects WhatChanged => RenderAspects.Mesh;

        internal override ValueTask Initialize(
            SkinnedMeshRenderer original,
            SkinnedMeshRenderer proxyed,
            UvTileMapDistribution[] components,
            ComputeContext context
        )
        {
            if (proxyed == null || proxyed.sharedMesh == null) return default;

            var baseMesh = proxyed.sharedMesh;
            var duplicatedMesh = UnityObject.Instantiate(baseMesh);
            duplicatedMesh.name = $"{baseMesh.name} (Zatools modified)";

            // テクスチャ内容の変更も Observe 対象にする
            var observedParameters = components.Select((c) => context.Observe(c, Utmd.FixedParameters.FixFromComponent, (op, np) => op == np));
            foreach (var c in components) if (c.TileMap != null) context.Observe(c.TileMap, (tm) => (tm.width, tm.height, tm.imageContentsHash));

            foreach (var parameters in observedParameters) if (parameters.TileMap != null) Utmd.Process(duplicatedMesh, parameters);

            _duplicatedMesh = duplicatedMesh;
            proxyed.sharedMesh = duplicatedMesh;

            return default;
        }

        internal override ZatoolsRenderFilterNode<UvTileMapDistribution> ZatoolsRefresh(
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
