using System.Collections.Generic;
using UnityEngine;

namespace KusakaFactory.Zatools.Runtime
{
    [AddComponentMenu("KusakaFactory/Zatools Face Tracking Data Installer")]
    [HelpURL("https://zatools.kb10uy.dev/ndmf-plugin/face-tracking-data-installer/")]
    public sealed class FaceTrackingDataInstaller : ZatoolsComponent
    {
        public TextAsset Definition = null;
        public List<SkinnedMeshRenderer> Meshes = new List<SkinnedMeshRenderer>();
    }
}
