using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using nadena.dev.ndmf;
using nadena.dev.ndmf.preview;
using KusakaFactory.Zatools.Ndmf.Framework;
using UnityObject = UnityEngine.Object;
using NpeComponent = KusakaFactory.Zatools.Runtime.NdmfPreviewExample;

namespace KusakaFactory.Zatools.Ndmf.Pass
{
    internal sealed class NpeTransforming : Pass<NpeTransforming>
    {
        protected override void Execute(BuildContext context)
        {
        }
    }

    internal sealed class NpeRenderFilter : ZatoolRenderFilter<NpeComponent>
    {
        public ImmutableList<RenderGroup> GetTargetGroups(ComputeContext context)
        {
            throw new NotImplementedException();
        }

        public Task<IRenderFilterNode> Instantiate(RenderGroup group, IEnumerable<(Renderer, Renderer)> proxyPairs, ComputeContext context)
        {
            throw new NotImplementedException();
        }
    }
}
