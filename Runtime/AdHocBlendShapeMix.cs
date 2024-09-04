using System;
using UnityEngine;
using VRC.SDKBase;

namespace KusakaFactory.Zatools.Runtime
{
    [AddComponentMenu("KusakaFactory/Mix BlendShapes on Build")]
    [RequireComponent(typeof(SkinnedMeshRenderer))]
    public sealed class AdHocBlendShapeMix : MonoBehaviour, IEditorOnly
    {
        public bool Replace = true;
        public BlendShapeMixDefinition[] MixDefinitions;
    }

    [Serializable]
    public sealed class BlendShapeMixDefinition
    {
        public string FromBlendShape = "";
        public string ToBlendShape = "";
        public float MixWeight = 0.0f;
    }
}
