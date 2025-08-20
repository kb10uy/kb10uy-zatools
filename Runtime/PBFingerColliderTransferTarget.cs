using UnityEngine;
using KusakaFactory.Zatools.Runtime.Utility;

namespace KusakaFactory.Zatools.Runtime
{
    [AddComponentMenu("KusakaFactory/Zatools PB Finger Collider Tarnsfer Target")]
    [Icon("Packages/org.kb10uy.zatools/Resources/Icon.png")]
    [HelpURL("https://zatools.kb10uy.dev/ndmf-plugin/pb-finger-collider-transfer-target/")]
    public sealed class PBFingerColliderTransferTarget : ZatoolsComponent
    {
        public float Radius = 0.02f;
        public float Length = 0.08f;

        private void OnDrawGizmosSelected()
        {
            ZatoolsGizmos.WithContext(transform.localToWorldMatrix, Color.magenta, () => ZatoolsGizmos.DrawPseudoCapsule(Radius, Length));
        }
    }
}
