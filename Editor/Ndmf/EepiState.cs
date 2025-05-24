using UnityEngine;
using nadena.dev.ndmf;
using nadena.dev.modular_avatar.core;
using Installer = KusakaFactory.Zatools.Runtime.EnhancedEyePointerInstaller;

namespace KusakaFactory.Zatools.Ndmf
{
    internal sealed class EepiState
    {
        private Installer _installer = null;
        private ModularAvatarMergeAnimator _mergeAnimator = null;

        public Installer Installer => _installer;
        public ModularAvatarMergeAnimator MergeAnimator => _mergeAnimator;

        private EepiState(BuildContext context)
        {
            _installer = context.AvatarRootObject.GetComponentInChildren<Installer>();
            _mergeAnimator = _installer.GetComponent<ModularAvatarMergeAnimator>();
        }

        public static EepiState Initializer(BuildContext context) => new EepiState(context);

        public void Destroy()
        {
            Object.DestroyImmediate(_installer);
            _installer = null;
        }
    }
}
