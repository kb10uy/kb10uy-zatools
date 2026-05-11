using UnityEngine;

namespace KusakaFactory.Zatools.Runtime
{
    [AddComponentMenu("KusakaFactory/Zatools Eyehole Depth Wrapper")]
    [Icon("Packages/org.kb10uy.zatools/Resources/Icon.png")]
    [HelpURL("https://zatools.kb10uy.dev/ndmf-plugin/eyehole-depth-wrapper/")]
    public sealed class EyeholeDepthWrapper : ZatoolsMeshEditingComponent
    {
        public string BlinkBlendShapeName = "vrc.blink";
        public float Threshold = 0.0001f;
        public float WithdrawalLimit = 0.025f;
        public Transform Basis = null;
        public float CentroidPush = 0.005f;
    }
}
