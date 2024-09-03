using UnityEngine;
using VRC.SDKBase;

namespace KusakaFactory.Zatools.Runtime
{
    [AddComponentMenu("KusakaFactory/Enhanced EyePointer Installer")]
    public sealed class EnhancedEyePointerInstaller : MonoBehaviour, IEditorOnly
    {
        public bool VRCConstraint = false;
        public bool DummyEyeBones = false;
        public bool AdaptedFXLayer = false;
    }
}
