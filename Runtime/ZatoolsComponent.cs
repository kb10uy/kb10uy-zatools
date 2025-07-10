using UnityEngine;
using VRC.SDKBase;

namespace KusakaFactory.Zatools.Runtime
{
    public abstract class ZatoolsComponent : MonoBehaviour, IEditorOnly
    {
    }

    public abstract class ZatoolsMeshEditingComponent : ZatoolsComponent
    {
    }
}
