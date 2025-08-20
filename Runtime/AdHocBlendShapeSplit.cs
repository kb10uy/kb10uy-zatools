using UnityEngine;

namespace KusakaFactory.Zatools.Runtime
{
    [AddComponentMenu("KusakaFactory/Zatools Split BlendShape on Build")]
    [Icon("Packages/org.kb10uy.zatools/Resources/Icon.png")]
    [HelpURL("https://zatools.kb10uy.dev/ndmf-plugin/adhoc-blendshape-split/")]
    [RequireComponent(typeof(SkinnedMeshRenderer))]
    public class AdHocBlendShapeSplit : ZatoolsMeshEditingComponent
    {
        public string[] TargetShapes = new string[] { };
        public Transform Basis = null;
    }
}
