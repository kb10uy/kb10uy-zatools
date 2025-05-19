using UnityEngine;
using VRC.SDKBase;

namespace KusakaFactory.Zatools.Runtime
{
    [AddComponentMenu("KusakaFactory/Zatools Enhanced EyePointer Installer")]
    [Icon("Packages/org.kb10uy.zatools/Resources/Icon.png")]
    public sealed class EnhancedEyePointerInstaller : MonoBehaviour, IEditorOnly
    {
        public bool VRCConstraint = false;
        public bool DummyEyeBones = false;
        public bool AdaptedFXLayer = false;
        public bool OverrideGlobalWeight = false;
        public float InitialGlobalWeight = 1.0f;
        public bool AddGlobalWeightControl = false;
        public GameObject SeparateHeadAvatarRoot = null;
    }
}
