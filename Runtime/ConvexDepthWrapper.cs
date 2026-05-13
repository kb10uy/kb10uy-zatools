using UnityEngine;

namespace KusakaFactory.Zatools.Runtime
{
    [AddComponentMenu("KusakaFactory/Zatools Convex Depth Wrapper")]
    [Icon("Packages/org.kb10uy.zatools/Resources/Icon.png")]
    [RequireComponent(typeof(SkinnedMeshRenderer))]
    public sealed class ConvexDepthWrapper : ZatoolsMeshEditingComponent
    {
        public SkinnedMeshRenderer SourceRenderer;
    }
}
