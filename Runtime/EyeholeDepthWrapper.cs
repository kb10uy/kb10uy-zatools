using UnityEngine;

#if UNITY_EDITOR
using nadena.dev.ndmf.runtime;
using VRC.SDK3.Avatars.Components;
#endif

namespace KusakaFactory.Zatools.Runtime
{
    [AddComponentMenu("KusakaFactory/Zatools Eyehole Depth Wrapper")]
    [Icon("Packages/org.kb10uy.zatools/Resources/Icon.png")]
    [HelpURL("https://zatools.kb10uy.dev/ndmf-plugin/eyehole-depth-wrapper/")]
    public sealed class EyeholeDepthWrapper : ZatoolsMeshEditingComponent
    {
        public Transform Basis = null;

        #region Selection
        public string BlinkBlendShapeName = "vrc.blink";
        public float Threshold = 0.0001f;
        public float EyelashCut = 0.002f;
        public float WithdrawalLimit = 0.025f;
        #endregion

        #region Generation
        public DomeGeneratorKind GeneratorKind = DomeGeneratorKind.Hermite;
        public float CentroidPush = 0.005f;
        public int Subdivisions = 4;
        public float TangentScale = 0.5f;
        #endregion

#if UNITY_EDITOR
        private void Reset()
        {
            var descriptor = RuntimeUtil.FindAvatarInParents(transform)?.GetComponent<VRCAvatarDescriptor>();
            if (descriptor == null || !descriptor.enableEyeLook) return;
            var blinkMesh = descriptor.customEyeLookSettings.eyelidsSkinnedMesh;
            var blinkBlendShapeIndex = descriptor.customEyeLookSettings.eyelidsBlendshapes[0];
            if (blinkMesh == null || blinkBlendShapeIndex == -1) return;
            var blinkName = blinkMesh.sharedMesh.GetBlendShapeName(blinkBlendShapeIndex);
            BlinkBlendShapeName = blinkName;
        }
#endif
    }

    public enum DomeGeneratorKind
    {
        Hermite = 0,
        Fan = 1,
    }
}
