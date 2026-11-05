using System;
using System.Collections.Generic;
using UnityEngine;

namespace KusakaFactory.Zatools.Runtime
{
    [AddComponentMenu("KusakaFactory/Zatools Convex Depth Wrapper")]
    [HelpURL("https://zatools.kb10uy.dev/ndmf-plugin/convex-depth-wrapper/")]
    [RequireComponent(typeof(SkinnedMeshRenderer))]
    [DisallowMultipleComponent]
    public sealed class ConvexDepthWrapper : ZatoolsMeshEditingComponent, IZatoolsObjectReferenceHolder
    {
        public Material MaterialOverride = null;
        public SkinnedMeshRenderer SourceMeshRenderer = null;
        public SkinnedMeshRendererReference SourceReference = new SkinnedMeshRendererReference();
        public ConvexDepthWrapperBlendShapeOverride[] Overrides = new ConvexDepthWrapperBlendShapeOverride[] { };

        IEnumerable<ZatoolsObjectReference> IZatoolsObjectReferenceHolder.GetObjectReferences() =>
            new ZatoolsObjectReference[] { SourceReference };
    }

    [Serializable]
    public sealed class ConvexDepthWrapperBlendShapeOverride
    {
        public string Name = "";
        public float Value = 0.0f;
    }
}
