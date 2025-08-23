using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using nadena.dev.ndmf.preview;
using nadena.dev.ndmf.runtime;
using KusakaFactory.Zatools.Runtime;
using KusakaFactory.Zatools.Ndmf.Core;

namespace KusakaFactory.Zatools.Ndmf.Preview
{
    internal sealed class AhbssRenderFilter : ZatoolsRenderFilter<AdHocBlendShapeSplit>
    {
        private static readonly TogglablePreviewNode _previewNode = CreateTogglablePreviewNode("Ad-Hoc BlendShape Split", "ad-hoc-blendshape-split", false);

        internal override ZatoolsRenderFilterNode<AdHocBlendShapeSplit> CreateNode() => new AhbssRenderFilterNode();
        internal override TogglablePreviewNode PreviewNode => _previewNode;
        internal static TogglablePreviewNode SwitchingPreviewNode => _previewNode;
    }

    internal sealed class AhbssRenderFilterNode : ZatoolsRenderFilterNode<AdHocBlendShapeSplit>
    {
        public override RenderAspects WhatChanged => RenderAspects.Mesh | RenderAspects.Shapes;

        internal override ValueTask ProcessEdit(
            SkinnedMeshRenderer original,
            SkinnedMeshRenderer proxyed,
            Mesh duplicatedMesh,
            AdHocBlendShapeSplit[] components,
            ComputeContext context
        )
        {
            // 直前の RenderFilterNode の処理が適用されている方から取る
            var avatarRoot = RuntimeUtil.FindAvatarInParents(original.transform);
            var observedParameters = components.Select((c) => context.Observe(
                c,
                (c) => Ahbss.FixedParameters.FixFromComponent(avatarRoot, c),
                (op, np) => op == np
            ));
            foreach (var component in components)
            {
                if (component.Basis != null) context.Observe(component.Basis, (t) => t.worldToLocalMatrix);
            }
            foreach (var bone in original.bones) if (bone != null) context.Observe(bone, (t) => t.worldToLocalMatrix);

            foreach (var parameters in observedParameters) Ahbss.AddSplitShapes(proxyed, duplicatedMesh, parameters);

            return default;
        }
    }
}
