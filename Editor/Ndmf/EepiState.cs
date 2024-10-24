using UnityEngine;
using nadena.dev.ndmf;
using Installer = KusakaFactory.Zatools.Runtime.EnhancedEyePointerInstaller;

namespace KusakaFactory.Zatools.Ndmf
{
    internal sealed class EepiState
    {
        private Installer _installer = null;

        public Installer Installer => _installer;

        private EepiState(BuildContext context)
        {
            _installer = context.AvatarRootObject.GetComponentInChildren<Installer>();
        }

        public static EepiState Initializer(BuildContext context) => new EepiState(context);

        public void Destroy()
        {
            Object.DestroyImmediate(_installer);
            _installer = null;
        }
    }
}
