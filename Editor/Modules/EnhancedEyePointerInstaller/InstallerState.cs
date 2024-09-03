using UnityEngine;
using nadena.dev.ndmf;
using Installer = KusakaFactory.Zatools.Runtime.EnhancedEyePointerInstaller;

namespace KusakaFactory.Zatools.Modules.EnhancedEyePointerInstaller
{
    internal sealed class InstallerState
    {
        private Installer _installer = null;

        public Installer Installer => _installer;

        private InstallerState(BuildContext context)
        {
            _installer = context.AvatarRootObject.GetComponentInChildren<Installer>();
        }

        public static InstallerState Initializer(BuildContext context) => new InstallerState(context);

        public void Destroy()
        {
            Object.DestroyImmediate(_installer);
            _installer = null;
        }
    }
}
