using UnityEngine;
using nadena.dev.ndmf;
using KusakaFactory.Zatools.Runtime;

namespace KusakaFactory.Zatools.Ndmf.Pass
{
    internal sealed class NpeTransforming : Pass<NpeTransforming>
    {
        protected override void Execute(BuildContext context)
        {
            var components = context.AvatarRootObject.GetComponentsInChildren<NdmfPreviewExample>();
            foreach (var component in components) Object.DestroyImmediate(component);
        }
    }
}
