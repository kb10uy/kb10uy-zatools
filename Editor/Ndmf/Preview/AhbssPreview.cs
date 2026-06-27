using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using nadena.dev.ndmf;
using nadena.dev.ndmf.preview;
using nadena.dev.ndmf.runtime;
using KusakaFactory.Zatools.Runtime;
using KusakaFactory.Zatools.Ndmf.Core;
using UnityObject = UnityEngine.Object;

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
        private Mesh _duplicatedMesh = null;

        public override RenderAspects WhatChanged => RenderAspects.Mesh | RenderAspects.Shapes;

        internal override ValueTask Initialize(
            SkinnedMeshRenderer original,
            SkinnedMeshRenderer proxyed,
            AdHocBlendShapeSplit[] components,
            ComputeContext context
        )
        {
            if (proxyed == null || proxyed.sharedMesh == null) return default;

            var baseMesh = proxyed.sharedMesh;
            var duplicatedMesh = UnityObject.Instantiate(baseMesh);
            duplicatedMesh.name = $"{baseMesh.name} (Zatools modified)";

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

            // プレビュー処理で影響を及ぼすボーンのリストを収集して追加で監視する
            var influentBoneIndices = new HashSet<int>(original.bones.Length);
            foreach (var parameters in observedParameters)
            {
                var influentIndices = Ahbss.AddSplitShapes(proxyed, duplicatedMesh, parameters);
                influentBoneIndices.UnionWith(influentIndices);
            }
            foreach (var bi in influentBoneIndices)
            {
                if (original.bones[bi] != null) context.Observe(original.bones[bi], (t) => t.worldToLocalMatrix);
            }

            _duplicatedMesh = duplicatedMesh;
            proxyed.sharedMesh = duplicatedMesh;
            ObjectRegistry.RegisterReplacedObject(baseMesh, duplicatedMesh);

            return default;
        }

        internal override ZatoolsRenderFilterNode<AdHocBlendShapeSplit> ZatoolsRefresh(
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
