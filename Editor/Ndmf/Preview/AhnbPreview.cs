using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using nadena.dev.ndmf.preview;
using KusakaFactory.Zatools.Runtime;
using KusakaFactory.Zatools.Ndmf.Core;

namespace KusakaFactory.Zatools.Ndmf.Preview
{
    internal sealed class AhnbRenderFilter : ZatoolsRenderFilter<AdHocNormalBending>
    {
        internal AhnbRenderFilter() : base("Ad-Hoc Normal Bending", "ad-hoc-normal-bending") { }

        internal override ZatoolsRenderFilterNode<AdHocNormalBending> CreateNode() => new AhnbRenderFilterNode();
    }

    internal sealed class AhnbRenderFilterNode : ZatoolsRenderFilterNode<AdHocNormalBending>
    {
        public override RenderAspects WhatChanged => RenderAspects.Mesh;

        internal override ValueTask ProcessEdit(
            SkinnedMeshRenderer original,
            SkinnedMeshRenderer proxyed,
            Mesh duplicatedMesh,
            AdHocNormalBending[] components,
            ComputeContext context
        )
        {
            // コンポーネント側の値の変更
            var observedPairs = components.Select((c) => context.Observe(
                c,
                (c) => (c.Mask, c.Mode, c.Mask != null && c.Mask.isReadable),
                (oldPair, newPair) => oldPair == newPair
            ));

            // ボーンの位置変化
            foreach (var bone in original.bones) context.Observe(bone, (t) => t.worldToLocalMatrix);

            foreach ((var mask, var mode, _) in observedPairs)
            {
                if (mask == null || !mask.isReadable) continue;
                Ahnb.ProcessTest1(proxyed, duplicatedMesh, mask, mode);
            }

            return default;
        }
    }
}
