using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using nadena.dev.ndmf.preview;
using KusakaFactory.Zatools.Runtime;
using KusakaFactory.Zatools.Ndmf.Core;
using UnityObject = UnityEngine.Object;

namespace KusakaFactory.Zatools.Ndmf.Preview
{
    internal sealed class AhbsmRenderFilter : ZatoolsRenderFilter<AdHocBlendShapeMix>
    {
        private static readonly TogglablePreviewNode _previewNode = CreateTogglablePreviewNode("Ad-Hoc BlendShape Mix", "ad-hoc-blendshape-mix", false);

        internal override ZatoolsRenderFilterNode<AdHocBlendShapeMix> CreateNode() => new AhbsmRenderFilterNode();
        internal override TogglablePreviewNode PreviewNode => _previewNode;
        internal static TogglablePreviewNode SwitchingPreviewNode => _previewNode;
    }

    internal sealed class AhbsmRenderFilterNode : ZatoolsRenderFilterNode<AdHocBlendShapeMix>
    {
        private Mesh _duplicatedMesh = null;

        public override RenderAspects WhatChanged => RenderAspects.Mesh | RenderAspects.Shapes;

        internal override ValueTask Initialize(
            SkinnedMeshRenderer original,
            SkinnedMeshRenderer proxyed,
            AdHocBlendShapeMix[] components,
            ComputeContext context
        )
        {
            if (proxyed == null || proxyed.sharedMesh == null) return default;

            // 直前の RenderFilterNode の処理が適用されている方から取る
            var baseMesh = proxyed.sharedMesh;
            var blendShapeIndices = Ahbsm.FetchBlendShapeIndices(baseMesh);

            var observedDefinitions = components.Select((c) => (
                context.Observe(
                    c,
                    (c) => c.MixDefinitions != null ? c.MixDefinitions.FixSources() : ImmutableArray<(string, string, float)>.Empty,
                    (oldList, newList) => oldList.SequenceEqual(newList)
                ),
                c.Replace
            ));

            var duplicatedMesh = UnityObject.Instantiate(baseMesh);
            duplicatedMesh.name = $"{baseMesh.name} (Zatools modified)";

            foreach (var (definitions, replace) in observedDefinitions)
            {
                var aggregatedDefinitions = Ahbsm.AggregateDefinitions(definitions, blendShapeIndices);
                if (replace)
                {
                    Ahbsm.ProcessOverwrite(baseMesh, duplicatedMesh, aggregatedDefinitions);
                }
                else
                {
                    Ahbsm.ProcessAppend(baseMesh, duplicatedMesh, aggregatedDefinitions);
                }
            }

            _duplicatedMesh = duplicatedMesh;
            proxyed.sharedMesh = duplicatedMesh;

            return default;
        }

        internal override ZatoolsRenderFilterNode<AdHocBlendShapeMix> ZatoolsRefresh(
            IEnumerable<(Renderer, Renderer)> proxyPairs,
            ComputeContext context,
            RenderAspects nonzeroUpdatedAspects
        )
        {
            // Renderer BlendShapes の値の変更など、上流で Mesh が変更されていない場合は現在のノードを再利用する
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
