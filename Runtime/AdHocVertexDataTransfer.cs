using UnityEngine;

namespace KusakaFactory.Zatools.Runtime
{
    [AddComponentMenu("KusakaFactory/Zatools Transfer Vertex Data on Build")]
    [Icon("Packages/org.kb10uy.zatools/Resources/Icon.png")]
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
        [InspectorLabel("Disabled")] Disabled = 0x00,
        [InspectorLabel("Vertex Color")] VertexColor = 0x01,
        [InspectorLabel("UV 0")] UV0 = 0x10,
        [InspectorLabel("UV 1")] UV1 = 0x11,
        [InspectorLabel("UV 2")] UV2 = 0x12,
        [InspectorLabel("UV 3")] UV3 = 0x13,
        [InspectorLabel("UV 4")] UV4 = 0x14,
        [InspectorLabel("UV 5")] UV5 = 0x15,
        [InspectorLabel("UV 6")] UV6 = 0x16,
        [InspectorLabel("UV 7")] UV7 = 0x17,
    }

    public enum VertexDataTransferMode
    {
        [InspectorLabel("Copy")] Copy,
        [InspectorLabel("1 - x")] OneMinus,
        [InspectorLabel("Constant, Luminance")] ConstAndLuminance,
        [InspectorLabel("Constant (0 to 1), Luminance")] Const01AndLuminance,
        [InspectorLabel("Constant * Luminance, Constant")] LuminanceConstAndConst,
        [InspectorLabel("Constant * Luminance (0 to 1), Constant")] LuminanceConst01AndConst,
    }
}
