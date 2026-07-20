using UnityEngine;

namespace KusakaFactory.Zatools.Runtime
{
    [AddComponentMenu("KusakaFactory/Zatools Transfer Vertex Data on Build")]
    [Icon("Packages/org.kb10uy.zatools/Resources/Icon.png")]
    [HelpURL("https://zatools.kb10uy.dev/ndmf-plugin/adhoc-vertex-data-transfer/")]
    [RequireComponent(typeof(SkinnedMeshRenderer))]
    public class AdHocVertexDataTransfer : MonoBehaviour
    {
        public Texture2D SourceTexture;
        public UvChannel SourceUv;

        public VertexDataTransferTarget TransferTarget = VertexDataTransferTarget.VertexColor;
        public VertexDataTransferMode TransferMode = VertexDataTransferMode.Normal;
    }

    public enum VertexDataTransferTarget : int
    {
        Disabled = 0x00,
        VertexColor = 0x01,
        UV0 = 0x10,
        UV1 = 0x11,
        UV2 = 0x12,
        UV3 = 0x13,
        UV4 = 0x14,
        UV5 = 0x15,
        UV6 = 0x16,
        UV7 = 0x17,
    }

    public enum VertexDataTransferMode
    {
        Normal,
    }
}
