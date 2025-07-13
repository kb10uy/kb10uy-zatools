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
            Debug.Log("recalculating mesh");
            // 直前の RenderFilterNode の処理が適用されている方から取る
            var originalMesh = proxyed.sharedMesh;
            var blendShapeIndices = Ahbsm.FetchBlendShapeIndices(originalMesh);
            var observedDefinitions = components.Select((c) => (
                context.Observe(
                    c,
                    (c) => c.MixDefinitions.FixSources(),
                    (oldList, newList) =>
                    {
                        var changed = oldList.SequenceEqual(newList);
                        if (!changed) Debug.Log("comparison reported change");
                        return changed;
                    }
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

        /*
        internal override Task<IRenderFilterNode> Refresh(
            SkinnedMeshRenderer original,
            SkinnedMeshRenderer proxyed,
            ComputeContext context,
            RenderAspects updatedAspects
        )
        {
            Debug.Log($"refreshing preview node: {updatedAspects}");
            return updatedAspects == RenderAspects.Shapes ?
                Task.FromResult((IRenderFilterNode)this) :
                Task.FromResult<IRenderFilterNode>(null);
        }
        */
    }
}
