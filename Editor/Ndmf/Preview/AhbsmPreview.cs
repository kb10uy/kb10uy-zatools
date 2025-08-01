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
        internal AhbsmRenderFilter() : base("Ad-Hoc BlendShape Mix", "ad-hoc-blendshape-mix") { }

        internal override ZatoolsRenderFilterNode<AdHocBlendShapeMix> CreateNode() => new AhbsmRenderFilterNode();
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
                    (c) => c.MixDefinitions.FixSources(),
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
