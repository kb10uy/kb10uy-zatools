using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using nadena.dev.ndmf.preview;
using KusakaFactory.Zatools.Runtime;
using KusakaFactory.Zatools.Ndmf.Core;

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
        public override RenderAspects WhatChanged => RenderAspects.Mesh | RenderAspects.Shapes;

        internal override ValueTask ProcessEdit(
            SkinnedMeshRenderer original,
            SkinnedMeshRenderer proxyed,
            Mesh duplicatedMesh,
            AdHocBlendShapeMix[] components,
            ComputeContext context
        )
        {
            // 直前の RenderFilterNode の処理が適用されている方から取る
            var originalMesh = proxyed.sharedMesh;
            var blendShapeIndices = Ahbsm.FetchBlendShapeIndices(originalMesh);
            var observedDefinitions = components.Select((c) => (
                context.Observe(
                    c,
                    (c) => c.MixDefinitions != null ? c.MixDefinitions.FixSources() : ImmutableArray<(string, string, float)>.Empty,
                    (oldList, newList) => oldList.SequenceEqual(newList)
                ),
                c.Replace
            ));
            foreach (var (definitions, replace) in observedDefinitions)
            {
                var aggregatedDefinitions = Ahbsm.AggregateDefinitions(definitions, blendShapeIndices);
                if (replace)
                {
                    Ahbsm.ProcessOverwrite(originalMesh, duplicatedMesh, aggregatedDefinitions);
                }
                else
                {
                    Ahbsm.ProcessAppend(originalMesh, duplicatedMesh, aggregatedDefinitions);
                }
            }

            return default;
        }
    }
}
