using UnityEngine;
using VRC.SDKBase;
using KusakaFactory.Zatools.Runtime.Utility;

namespace KusakaFactory.Zatools.Runtime
{
    [AddComponentMenu("KusakaFactory/Zatools PB Finger Collider Tarnsfer Target")]
    [Icon("Packages/org.kb10uy.zatools/Resources/Icon.png")]
    public sealed class PBFingerColliderTransferTarget : MonoBehaviour, IEditorOnly
    {
        public float Radius = 0.02f;
        public float Length = 0.08f;

        private void OnDrawGizmosSelected()
        {
            ZatoolGizmos.WithContext(transform.localToWorldMatrix, Color.magenta, () => ZatoolGizmos.DrawPseudoCapsule(Radius, Length));
        }
    }
}
