using UnityEngine;
using VRC.SDKBase;

namespace KusakaFactory.Zatools.Runtime
{
    [AddComponentMenu("KusakaFactory/Zatools Enhanced EyePointer Installer")]
    public sealed class EnhancedEyePointerInstaller : MonoBehaviour, IEditorOnly
    {
        public bool VRCConstraint = false;
        public bool DummyEyeBones = false;
        public bool AdaptedFXLayer = false;
        public bool OverrideGlobalWeight = false;
        public float InitialGlobalWeight = 1.0f;
        public bool AddGlobalWeightControl = false;
    }
}
