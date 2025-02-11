using UnityEngine;
using VRC.SDKBase;

namespace KusakaFactory.Zatools.Runtime
{
    [AddComponentMenu("KusakaFactory/Zatools Replicate Property from EditorOnly")]
    public class EditorOnlyPropertyReplicator : MonoBehaviour, IEditorOnly
    {
        public GameObject[] Targets = new GameObject[] { };

        public bool EnableSmrBlendShapes = false;
        public string[] SmrBlendShapesExclusion = new string[] { };
    }
}
