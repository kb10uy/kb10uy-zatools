using System;
using UnityEngine;

namespace KusakaFactory.Zatools.Runtime
{
    [AddComponentMenu("KusakaFactory/Zatools Mix BlendShapes on Build")]
    [Icon("Packages/org.kb10uy.zatools/Resources/Icon.png")]
    [RequireComponent(typeof(SkinnedMeshRenderer))]
    public sealed class AdHocBlendShapeMix : ZatoolsMeshEditingComponent
    {
        public bool Replace = true;
        public BlendShapeMixDefinition[] MixDefinitions;
    }

    [Serializable]
    public sealed class BlendShapeMixDefinition : IEquatable<BlendShapeMixDefinition>
    {
        public string FromBlendShape = "";
        public string ToBlendShape = "";
        public float MixWeight = 0.0f;

        public bool Equals(BlendShapeMixDefinition other) =>
            (other != null)
            && FromBlendShape == other.FromBlendShape
            && ToBlendShape == other.ToBlendShape
            && MixWeight == other.MixWeight;

        public override bool Equals(object obj) => Equals(obj as BlendShapeMixDefinition);
        public override int GetHashCode() => (FromBlendShape, ToBlendShape, MixWeight).GetHashCode();
    }
}
