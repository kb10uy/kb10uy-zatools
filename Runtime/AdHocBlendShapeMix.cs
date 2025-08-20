using System;
using UnityEngine;

namespace KusakaFactory.Zatools.Runtime
{
    [AddComponentMenu("KusakaFactory/Zatools Mix BlendShapes on Build")]
    [Icon("Packages/org.kb10uy.zatools/Resources/Icon.png")]
    [HelpURL("https://zatools.kb10uy.dev/ndmf-plugin/adhoc-blendshape-mix/")]
    [RequireComponent(typeof(SkinnedMeshRenderer))]
    public sealed class AdHocBlendShapeMix : ZatoolsMeshEditingComponent
    {
        public bool Replace = true;
        public BlendShapeMixDefinition[] MixDefinitions = new BlendShapeMixDefinition[] { };
    }

    [Serializable]
    public sealed class BlendShapeMixDefinition
    {
        public string FromBlendShape = "";
        public string ToBlendShape = "";
        public float MixWeight = 0.0f;
    }
}
