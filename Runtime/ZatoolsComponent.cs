using UnityEngine;
using nadena.dev.ndmf;

namespace KusakaFactory.Zatools.Runtime
{
    public abstract class ZatoolsComponent : MonoBehaviour, INDMFEditorOnly
    {
    }

    public abstract class ZatoolsMeshEditingComponent : ZatoolsComponent
    {
    }
}
