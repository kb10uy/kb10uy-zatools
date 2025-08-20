using System;
using UnityEngine;

namespace KusakaFactory.Zatools.Runtime
{
    [AddComponentMenu("KusakaFactory/Zatools Apply Rotation Influence for Bone Array")]
    [Icon("Packages/org.kb10uy.zatools/Resources/Icon.png")]
    [HelpURL("https://zatools.kb10uy.dev/ndmf-plugin/bone-array-rotation-influence/")]
    public sealed class BoneArrayRotationInfluence : ZatoolsComponent
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
