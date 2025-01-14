using System;
using UnityEngine;
using VRC.SDKBase;

namespace KusakaFactory.Zatools.Runtime
{
    [AddComponentMenu("KusakaFactory/Zatools Apply Rotation Influence for Bone Array")]
    public sealed class BoneArrayRotationInfluence : MonoBehaviour, IEditorOnly
    {
        public RotationInfluence[] ChainRoots = new[] { new RotationInfluence() };
        public bool CloseLoop = true;
        public float ParentOffsetDistance = 0.01f;
    }

    [Serializable]
    public sealed class RotationInfluence
    {
        public Transform Root;
        public float Influence = 1.0f;
    }
}
