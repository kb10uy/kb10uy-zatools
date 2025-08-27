using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using nadena.dev.ndmf.preview;
using KusakaFactory.Zatools.Runtime;
using KusakaFactory.Zatools.Ndmf.Core;

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
        public override RenderAspects WhatChanged => RenderAspects.Mesh;

        internal override ValueTask ProcessEdit(
            SkinnedMeshRenderer original,
            SkinnedMeshRenderer proxyed,
            Mesh duplicatedMesh,
            UvTileMapDistribution[] components,
            ComputeContext context
        )
        {
            // テクスチャ内容の変更も Observe 対象にする
            var observedParameters = components.Select((c) => context.Observe(c, Utmd.FixedParameters.FixFromComponent, (op, np) => op == np));
            foreach (var c in components) if (c.TileMap != null) context.Observe(c.TileMap, (tm) => (tm.width, tm.height, tm.imageContentsHash));

            foreach (var parameters in observedParameters) if (parameters.TileMap != null) Utmd.Process(duplicatedMesh, parameters);

            return default;
        }
    }
}
