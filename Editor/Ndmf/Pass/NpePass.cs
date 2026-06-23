using UnityEngine;
using nadena.dev.ndmf;
using KusakaFactory.Zatools.Runtime;

namespace KusakaFactory.Zatools.Ndmf.Pass
{
    internal sealed class NpeTransforming : ZatoolsPass<NpeTransforming>
    {
        internal override string ZatoolsPassName => nameof(NpeTransforming);
        internal override string ZatoolsPassDescription => "Example pass";

        protected override void Execute(BuildContext context)
        {
            var components = context.AvatarRootObject.GetComponentsInChildren<NdmfPreviewExample>();
            foreach (var component in components) Object.DestroyImmediate(component);
        }
    }
}
