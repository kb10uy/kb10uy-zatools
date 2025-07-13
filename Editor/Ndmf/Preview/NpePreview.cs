using System.Threading.Tasks;
using UnityEngine;
using nadena.dev.ndmf.preview;
using KusakaFactory.Zatools.Runtime;

namespace KusakaFactory.Zatools.Ndmf.Preview
{
    internal sealed class NpeRenderFilter : ZatoolsRenderFilter<NdmfPreviewExample>
    {
        internal NpeRenderFilter() : base("NDMF Preview Example", "ndmf-preview-example") { }

        internal override ZatoolsRenderFilterNode<NdmfPreviewExample> CreateNode() => new NpeRenderFilterNode();
    }

    internal sealed class NpeRenderFilterNode : ZatoolsRenderFilterNode<NdmfPreviewExample>
    {
        public override RenderAspects WhatChanged => RenderAspects.Mesh | RenderAspects.Shapes;

        internal override async ValueTask ProcessEdit(
            SkinnedMeshRenderer original,
            SkinnedMeshRenderer proxyed,
            Mesh duplicatedMesh,
            NdmfPreviewExample[] components,
            ComputeContext context
        )
        {
            var newVertices = duplicatedMesh.vertices;
            await Task.Run(() =>
            {
                for (int i = 0; i < newVertices.Length; ++i)
                {
                    newVertices[i] = newVertices[i] + Vector3.forward * 0.05f;
                }
            });
            duplicatedMesh.SetVertices(newVertices);
            Debug.Log($"Zatools NDMF Preview Example Processed: {proxyed.name}");
        }
    }
}
