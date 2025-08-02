using UnityEngine;

namespace KusakaFactory.Zatools.Runtime
{
    [AddComponentMenu("KusakaFactory/Zatools Bend Normal on Build")]
    [Icon("Packages/org.kb10uy.zatools/Resources/Icon.png")]
    [RequireComponent(typeof(SkinnedMeshRenderer))]
    public sealed class AdHocNormalBending : ZatoolsMeshEditingComponent
    {
        public Texture2D Mask;
        public NormalBendMaskMode Mode;
    }

    public enum NormalBendMaskMode
    {
        TakeWhite,
        TakeBlack,
    }
}
