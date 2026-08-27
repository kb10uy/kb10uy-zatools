using UnityEngine;

namespace KusakaFactory.Zatools.Runtime
{
    [AddComponentMenu("KusakaFactory/Zatools Transfer Vertex Data on Build")]
    [HelpURL("https://zatools.kb10uy.dev/ndmf-plugin/adhoc-vertex-data-transfer/")]
    [RequireComponent(typeof(SkinnedMeshRenderer))]
    public class AdHocVertexDataTransfer : ZatoolsMeshEditingComponent
    {
        public Texture2D SourceTexture;
        public UvChannel SourceUv;

        public VertexDataTransferTarget TransferTarget = VertexDataTransferTarget.VertexColor;
        public VertexDataTransferMode TransferMode = VertexDataTransferMode.Copy;
        public Vector3 ConstantVector3 = Vector3.zero;
        public float ConstantFloat = 0.0f;
    }

    public enum VertexDataTransferTarget : int
    {
        [InspectorName("Disabled")] Disabled = 0x00,
        [InspectorName("Vertex Color")] VertexColor = 0x01,
        [InspectorName("UV 0")] UV0 = 0x10,
        [InspectorName("UV 1")] UV1 = 0x11,
        [InspectorName("UV 2")] UV2 = 0x12,
        [InspectorName("UV 3")] UV3 = 0x13,
        [InspectorName("UV 4")] UV4 = 0x14,
        [InspectorName("UV 5")] UV5 = 0x15,
        [InspectorName("UV 6")] UV6 = 0x16,
        [InspectorName("UV 7")] UV7 = 0x17,
    }

    public enum VertexDataTransferMode
    {
        [InspectorName("Copy")] Copy,
        [InspectorName("1 - x")] OneMinus,
        [InspectorName("Constant, Luminance")] ConstAndLuminance,
        [InspectorName("Constant (0 to 1), Luminance")] Const01AndLuminance,
        [InspectorName("Constant * Luminance, Constant")] LuminanceConstAndConst,
        [InspectorName("Constant * Luminance (0 to 1), Constant")] LuminanceConst01AndConst,
    }
}
