using UnityEngine;

namespace KusakaFactory.Zatools.Runtime
{
    [AddComponentMenu("KusakaFactory/Zatools Split BlendShape on Build")]
    [HelpURL("https://zatools.kb10uy.dev/ndmf-plugin/adhoc-blendshape-split/")]
    [RequireComponent(typeof(SkinnedMeshRenderer))]
    public class AdHocBlendShapeSplit : ZatoolsMeshEditingComponent
    {
        public string[] TargetShapes = new string[] { };
        public Transform Basis = null;
        public string LeftSuffix = "_sL";
        public string RightSuffix = "_sR";
    }
}
