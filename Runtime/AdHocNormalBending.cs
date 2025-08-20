using UnityEngine;

namespace KusakaFactory.Zatools.Runtime
{
    [AddComponentMenu("KusakaFactory/Zatools Bend Normal on Build")]
    [Icon("Packages/org.kb10uy.zatools/Resources/Icon.png")]
    [HelpURL("https://zatools.kb10uy.dev/ndmf-plugin/adhoc-normal-bending/")]
    [RequireComponent(typeof(SkinnedMeshRenderer))]
    public sealed class AdHocNormalBending : ZatoolsMeshEditingComponent
    {
        public Transform Direction;
        public float Weight = 1.0f;
        public Texture2D Mask;
        public NormalBendMaskMode Mode = NormalBendMaskMode.White;
    }

    public enum NormalBendMaskMode
    {
        White,
        Black,
    }
}
