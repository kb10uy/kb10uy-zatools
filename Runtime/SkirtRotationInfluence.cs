#if KZT_NDMF

using UnityEngine;
using VRC.SDKBase;

namespace KusakaFactory.Zatools.Runtime
{
    [AddComponentMenu("KusakaFactory/Apply Rotation Influence for Skirt Bones")]
    public class SkirtRotationInfluence : MonoBehaviour, IEditorOnly
    {
        [Tooltip("各スカートボーン鎖の一番上の Transform のリスト。")]
        public Transform[] SkirtChains;

        [Tooltip("SkirtChains の最初と最後を繋げるかどうか。")]
        public bool CloseLoop = true;

        [Tooltip("親ボーンを生成する位置の Y 軸方向の距離。")]
        public float ParentOffsetDistance = 0.01f;
    }
}

#endif
