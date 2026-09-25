using System;
using UnityEngine;

namespace KusakaFactory.Zatools.Runtime
{
    [AddComponentMenu("KusakaFactory/Zatools Advanced BlendShape Synthesis")]
    [HelpURL("https://zatools.kb10uy.dev/ndmf-plugin/adhoc-advanced-blendshape-synthesis/")]
    [RequireComponent(typeof(SkinnedMeshRenderer))]
    public sealed class AdHocAdvancedBlendShapeSynthesis : ZatoolsMeshEditingComponent
    {
        public string[] SourceBlendShapes = { };
        public AhabssEntry[] Entries = { };
    }

    [Serializable]
    public sealed class AhabssEntry
    {
        public string Name = "Shape";
        public float Value = 0.0f;
        public string Expression = "@v0 @n0 @t0";
    }
}
