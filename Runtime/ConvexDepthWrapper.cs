using System;
using UnityEngine;

namespace KusakaFactory.Zatools.Runtime
{
    [AddComponentMenu("KusakaFactory/Zatools Convex Depth Wrapper")]
    [Icon("Packages/org.kb10uy.zatools/Resources/Icon.png")]
    [HelpURL("https://zatools.kb10uy.dev/ndmf-plugin/convex-depth-wrapper/")]
    [RequireComponent(typeof(SkinnedMeshRenderer))]
    [DisallowMultipleComponent]
    public sealed class ConvexDepthWrapper : ZatoolsMeshEditingComponent
    {
        public Material MaterialOverride = null;
        public SkinnedMeshRenderer SourceMeshRenderer = null;
        public ConvexDepthWrapperBlendShapeOverride[] Overrides = new ConvexDepthWrapperBlendShapeOverride[] { };
    }

    [Serializable]
    public sealed class ConvexDepthWrapperBlendShapeOverride
    {
        public string Name = "";
        public float Value = 0.0f;
    }
}
