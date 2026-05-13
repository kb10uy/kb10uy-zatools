using UnityEngine;

namespace KusakaFactory.Zatools.Runtime
{
    [AddComponentMenu("KusakaFactory/Zatools Convex Depth Wrapper")]
    [Icon("Packages/org.kb10uy.zatools/Resources/Icon.png")]
    [HelpURL("https://zatools.kb10uy.dev/ndmf-plugin/convex-depth-wrapper/")]
    [RequireComponent(typeof(SkinnedMeshRenderer))]
    public sealed class ConvexDepthWrapper : ZatoolsMeshEditingComponent
    {
        public SkinnedMeshRenderer SourceRenderer = null;
    }
}
