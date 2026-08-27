using UnityEngine;

namespace KusakaFactory.Zatools.Runtime
{
    [AddComponentMenu("KusakaFactory/Zatools Enhanced EyePointer Installer")]
    [HelpURL("https://zatools.kb10uy.dev/ndmf-plugin/eye-pointer-installer/")]
    public sealed class EnhancedEyePointerInstaller : ZatoolsComponent
    {
        public bool VRCConstraint = false;
        public bool DummyEyeBones = false;
        public bool FixTargetAxis = false;
        public bool AdaptedFXLayer = false;
        public bool OverrideGlobalWeight = false;
        public float InitialGlobalWeight = 1.0f;
        public bool AddGlobalWeightControl = false;
        public GameObject SeparateHeadAvatarRoot = null;
    }
}
