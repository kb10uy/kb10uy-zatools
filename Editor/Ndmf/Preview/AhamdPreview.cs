using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using nadena.dev.ndmf;
using nadena.dev.ndmf.preview;
using KusakaFactory.Zatools.Runtime;
using KusakaFactory.Zatools.Ndmf.Core;
using UnityObject = UnityEngine.Object;

namespace KusakaFactory.Zatools.Ndmf.Preview
{
    internal sealed class AhamdRenderFilter : ZatoolsRenderFilter<AdHocAdvancedMeshDuplication>
    {
        private static readonly TogglablePreviewNode _previewNode = CreateTogglablePreviewNode("Ad-hoc Advanced Mesh Duplication", "ad-hoc-advanced-mesh-duplication", false);

        protected override ZatoolsRenderFilterNode<AdHocAdvancedMeshDuplication> CreateNode() => new AhamdRenderFilterNode();
        protected override TogglablePreviewNode PreviewNode => _previewNode;
        internal static TogglablePreviewNode SwitchingPreviewNode => _previewNode;
    }

    internal sealed class AhamdRenderFilterNode : ZatoolsRenderFilterNode<AdHocAdvancedMeshDuplication>
    {
        public override RenderAspects WhatChanged => RenderAspects.Mesh;

        protected internal override ValueTask Initialize(SkinnedMeshRenderer original, SkinnedMeshRenderer proxyed, AdHocAdvancedMeshDuplication[] components, ComputeContext context)
        {
            return default;
        }

        protected override void ZatoolsOnFrame(Renderer original, Renderer proxy)
        {

        }

        protected override void ZatoolsDispose()
        {

        }
    }
}
