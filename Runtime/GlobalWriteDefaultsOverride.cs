using UnityEngine;

namespace KusakaFactory.Zatools.Runtime
{
    [AddComponentMenu("KusakaFactory/Global WD Override")]
    [HelpURL("https://zatools.kb10uy.dev/ndmf-plugin/global-write-defaults-override/")]
    [DisallowMultipleComponent]
    public sealed class GlobalWriteDefaultsOverride : ZatoolsComponent
    {
        public WriteDefaultsOverrideMode Mode = WriteDefaultsOverrideMode.ForceOn;
    }

    public enum WriteDefaultsOverrideMode
    {
        [InspectorName("Force ON")] ForceOn,
        [InspectorName("Force OFF")] ForceOff,
    }
}
