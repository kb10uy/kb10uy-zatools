using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using nadena.dev.ndmf.preview;
using nadena.dev.ndmf.runtime;
using KusakaFactory.Zatools.Runtime;
using KusakaFactory.Zatools.Ndmf.Core;

namespace KusakaFactory.Zatools.Ndmf.Preview
{
    internal sealed class AhnbRenderFilter : ZatoolsRenderFilter<AdHocNormalBending>
    {
        private static readonly TogglablePreviewNode _previewNode = CreateTogglablePreviewNode("Ad-Hoc Normal Bending", "ad-hoc-normal-bending");

        internal override ZatoolsRenderFilterNode<AdHocNormalBending> CreateNode() => new AhnbRenderFilterNode();
        internal override TogglablePreviewNode PreviewNode => _previewNode;
        internal static TogglablePreviewNode SwitchingPreviewNode => _previewNode;
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
            var avatarRoot = RuntimeUtil.FindAvatarInParents(original.transform);
            var observedParameters = components.Select((c) => context.Observe(
                c,
                (c) => Ahnb.FixedParameters.FixFromComponent(avatarRoot, c),
                (op, np) => op == np)
            );
            foreach (var component in components)
            {
                if (component.Direction != null) context.Observe(component.Direction, (t) => t.worldToLocalMatrix);
            }
            foreach (var bone in original.bones) if (bone != null) context.Observe(bone, (t) => t.worldToLocalMatrix);

            foreach (var parameters in observedParameters) Ahnb.Process(proxyed, duplicatedMesh, parameters);

            return default;
        }
    }
}
