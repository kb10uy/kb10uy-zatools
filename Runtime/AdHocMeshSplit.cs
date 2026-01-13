using UnityEngine;

namespace KusakaFactory.Zatools.Runtime
{
    [AddComponentMenu("KusakaFactory/Zatools Split Mesh on Build")]
    [Icon("Packages/org.kb10uy.zatools/Resources/Icon.png")]
    [HelpURL("https://zatools.kb10uy.dev/ndmf-plugin/adhoc-mesh-split/")]
    [RequireComponent(typeof(SkinnedMeshRenderer))]
    public class AdHocMeshSplit : ZatoolsMeshEditingComponent
    {
        public Texture2D Mask;
        public NormalBendMaskMode Mode = NormalBendMaskMode.White;
        public Material SplitMaterial;
    }

    public enum MeshSplitMaskMode
    {
        White,
        Black,
    }
}
