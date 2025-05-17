using UnityEngine;
using VRC.SDKBase;
using KusakaFactory.Zatools.Runtime.Utility;

namespace KusakaFactory.Zatools.Runtime
{
    [AddComponentMenu("KusakaFactory/Zatools PB Finger Collider Tarnsfer Target")]
    public sealed class PBFingerColliderTransferTarget : MonoBehaviour, IEditorOnly
    {
        public Vector3 Position = Vector3.zero;
        public Vector3 Rotation = Vector3.zero;
        public float Radius = 0.02f;
        public float Length = 0.08f;

        private void OnDrawGizmosSelected()
        {
            ZatoolGizmos.WithContext(
                transform.localToWorldMatrix * Matrix4x4.Translate(Position) * Matrix4x4.Rotate(Quaternion.Euler(Rotation)),
                Color.magenta,
                () => ZatoolGizmos.DrawPseudoCapsule(Radius, Length)
            );
        }
    }
}
