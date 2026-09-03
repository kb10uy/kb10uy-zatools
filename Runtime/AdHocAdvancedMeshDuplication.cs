using System;
using UnityEngine;

namespace KusakaFactory.Zatools.Runtime
{
    [AddComponentMenu("KusakaFactory/Zatools Advanced Duplicate Mesh on Build")]
    [HelpURL("https://zatools.kb10uy.dev/ndmf-plugin/adhoc-advanced-mesh-duplication/")]
    [RequireComponent(typeof(SkinnedMeshRenderer))]
    public sealed class AdHocAdvancedMeshDuplication : ZatoolsMeshEditingComponent
    {
        public SkinnedMeshRenderer Source;
        public Material OverrideMaterial;

        // Preprocess
        public Texture2D SelectionTexture = null;
        public UvChannel SelectionTextureUv = UvChannel.UV0;
        public string SelectionExpression = "@mask #r";
        public float SelectionThreshold = 0.5f;

        // Main process
        public AhamdModificationStep[] Modifications = { };

        // Postprocess
        public bool RecalculateNormals = false;
        public bool RecalculateTangents = false;
    }

    [Serializable]
    public sealed class AhamdModificationStep
    {
        public bool Enabled = true;
        public Texture2D ExtraTexture = null;
        public UvChannel ExtraTextureUv = UvChannel.UV0;
        public AhamdModificationTarget Target = AhamdModificationTarget.UV0;
        public string Expression = "@uv0";
    }

    public enum AhamdModificationTarget
    {
        [InspectorName("Position")] Position = 0x01,
        [InspectorName("Normal")] Normal = 0x02,
        [InspectorName("Tangent")] Tangent = 0x03,
        [InspectorName("Vertex Color")] VertexColor = 0x04,

        [InspectorName("UV 0")] UV0 = 0x10,
        [InspectorName("UV 1")] UV1 = 0x11,
        [InspectorName("UV 2")] UV2 = 0x12,
        [InspectorName("UV 3")] UV3 = 0x13,
        [InspectorName("UV 4")] UV4 = 0x14,
        [InspectorName("UV 5")] UV5 = 0x15,
        [InspectorName("UV 6")] UV6 = 0x16,
        [InspectorName("UV 7")] UV7 = 0x17,

        [InspectorName("Scratch 0")] Scratch0 = 0x20,
        [InspectorName("Scratch 1")] Scratch1 = 0x21,
        [InspectorName("Scratch 2")] Scratch2 = 0x22,
        [InspectorName("Scratch 3")] Scratch3 = 0x23,
    }
}
