using UnityEngine;
using VRC.SDKBase;

namespace KusakaFactory.Zatools.Runtime
{
    [AddComponentMenu("KusakaFactory/UV Integer Shift")]
    [RequireComponent(typeof(SkinnedMeshRenderer))]
    public sealed class UVIntegerShift : MonoBehaviour, IEditorOnly
    {
        public UVIndex DestinationUV;
        public UVIndex SourceUV;
        public int ShiftX;
        public int ShiftY;

        public enum UVIndex
        {
            UV0 = 0,
            UV1 = 1,
            UV2 = 2,
            UV3 = 3,
        }
    }
}
