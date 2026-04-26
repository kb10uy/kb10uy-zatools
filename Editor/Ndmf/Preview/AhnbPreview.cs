using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using nadena.dev.ndmf.preview;
using nadena.dev.ndmf.runtime;
using KusakaFactory.Zatools.Runtime;
using KusakaFactory.Zatools.Ndmf.Core;
using UnityObject = UnityEngine.Object;

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
        private Mesh _duplicatedMesh = null;

        public override RenderAspects WhatChanged => RenderAspects.Mesh;

        internal override ValueTask Initialize(
            SkinnedMeshRenderer original,
            SkinnedMeshRenderer proxyed,
            AdHocNormalBending[] components,
            ComputeContext context
        )
        {
            if (proxyed == null || proxyed.sharedMesh == null) return default;

            var baseMesh = proxyed.sharedMesh;
            var duplicatedMesh = UnityObject.Instantiate(baseMesh);
            duplicatedMesh.name = $"{baseMesh.name} (Zatools modified)";

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
                if (component.Mask != null) context.Observe(component.Mask, (tm) => (tm.width, tm.height, tm.imageContentsHash));
            }

            // プレビュー処理で影響を及ぼすボーンのリストを収集して追加で監視する
            var influentBoneIndices = new HashSet<int>(original.bones.Length);
            foreach (var parameters in observedParameters)
            {
                var influentIndices = Ahnb.Process(proxyed, duplicatedMesh, parameters);
                influentBoneIndices.UnionWith(influentIndices);
            }
            foreach (var bi in influentBoneIndices)
            {
                if (original.bones[bi] != null) context.Observe(original.bones[bi], (t) => t.worldToLocalMatrix);
            }

            _duplicatedMesh = duplicatedMesh;
            proxyed.sharedMesh = duplicatedMesh;

            return default;
        }

        internal override ZatoolsRenderFilterNode<AdHocNormalBending> ZatoolsRefresh(
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
