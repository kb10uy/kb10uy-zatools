using UnityEngine;
using nadena.dev.ndmf;
using nadena.dev.modular_avatar.core;
using Installer = KusakaFactory.Zatools.Runtime.EnhancedEyePointerInstaller;

namespace KusakaFactory.Zatools.Ndmf.Pass
{
    internal sealed class EepiState
    {
        private Installer _installer = null;
        private bool _installed = false;

        public Installer Installer => _installer;
        public bool Installed => _installed;

        public ModularAvatarMergeAnimator MergeAnimator = null;
        public (Transform Left, Transform Right, Transform Head) TargetAnchors = (null, null, null);
        public (Transform Left, Transform Right, Transform Head) AnchorProxies = (null, null, null);
        public (Component Component, string Version) ApsInstallation = (null, "");

        private EepiState(BuildContext context)
        {
            _installer = context.AvatarRootObject.GetComponentInChildren<Installer>();
        }

        public static EepiState Initializer(BuildContext context) => new EepiState(context);

        public void Destroy()
        {
            Object.DestroyImmediate(_installer);
            _installer = null;
            _installed = true;
        }
    }
}
