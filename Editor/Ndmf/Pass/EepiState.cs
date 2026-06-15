using UnityEngine;
using nadena.dev.ndmf;
using nadena.dev.modular_avatar.core;
using Installer = KusakaFactory.Zatools.Runtime.EnhancedEyePointerInstaller;

namespace KusakaFactory.Zatools.Ndmf.Pass
{
    internal sealed class EepiState
    {
        private Installer _installer = null;
        private ModularAvatarMergeAnimator _mergeAnimator = null;
        private bool _installed = false;

        public Installer Installer => _installer;
        public ModularAvatarMergeAnimator MergeAnimator => _mergeAnimator;
        public bool Installed => _installed;

        public (Transform Left, Transform Right, Transform Head) TargetAnchors = (null, null, null);
        public (Transform Left, Transform Right, Transform Head) AnchorProxies = (null, null, null);

        private EepiState(BuildContext context)
        {
            _installer = context.AvatarRootObject.GetComponentInChildren<Installer>();
            if (_installer != null) _mergeAnimator = _installer.GetComponent<ModularAvatarMergeAnimator>();

            TargetAnchors = (
                _installer.transform.Find("WorldAnchor/TargetHand_L"),
                _installer.transform.Find("WorldAnchor/TargetHand_R"),
                _installer.transform.Find("WorldAnchor/TargetHead")
            );
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
