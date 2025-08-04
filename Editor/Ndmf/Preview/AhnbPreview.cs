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
            // コンポーネント側の値の変更と各ボーンの位置変化を監視
            var observedParameters = components.Select((c) => context.Observe(c, Ahnb.FixedParameters.FixFromComponent, (op, np) => op == np));
            foreach (var bone in original.bones) if (bone != null) context.Observe(bone, (t) => t.worldToLocalMatrix);

            foreach (var parameters in observedParameters) Ahnb.Process(proxyed, duplicatedMesh, parameters);

            return default;
        }
    }
}
