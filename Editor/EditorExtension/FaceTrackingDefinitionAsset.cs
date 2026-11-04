#if ZATOOLS_HAS_VRCSDK_FACE_TRACKING
using UnityEditor;
using KusakaFactory.Zatools.Ndmf.Core;

namespace KusakaFactory.Zatools.EditorExtension
{
    internal static class FaceTrackingDefinitionAsset
    {
        [MenuItem("Assets/Create/Zatools: kb10uy's Various Tools/Face Tracking Definition JSON", false, 0)]
        private static void Create()
        {
            ProjectWindowUtil.CreateAssetWithContent("FaceTrackingDefinition.json", FtdiJson.Write(new Ftdi.Definition()));
        }
    }
}
#endif
