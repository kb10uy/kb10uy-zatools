using UnityEngine;
using nadena.dev.ndmf;
using JetBrains.Annotations;

namespace KusakaFactory.Zatools.Runtime
{
    /// <summary>
    /// Base class for components processed by Zatools during a NDMF build.
    /// </summary>
    /// <remarks>
    /// This type is part of the stable public API and may be inherited by components in external assemblies.
    /// </remarks>
    [PublicAPI]
    public abstract class ZatoolsComponent : MonoBehaviour, INDMFEditorOnly
    {
    }

    /// <summary>
    /// Base class for Zatools components which modify meshes during a NDMF build.
    /// Those which supports NDMF preview should inherit this class.
    /// </summary>
    /// <remarks>
    /// This type is part of the stable public API and may be inherited by components in external assemblies.
    /// </remarks>
    [PublicAPI]
    public abstract class ZatoolsMeshEditingComponent : ZatoolsComponent
    {
    }
}
