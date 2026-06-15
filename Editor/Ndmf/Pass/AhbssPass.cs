using UnityEngine;
using nadena.dev.ndmf;
using KusakaFactory.Zatools.Ndmf.Core;
using UnityObject = UnityEngine.Object;
using AhbssComponent = KusakaFactory.Zatools.Runtime.AdHocBlendShapeSplit;

namespace KusakaFactory.Zatools.Ndmf.Pass
{
    internal sealed class AhbssGenerating : Pass<AhbssGenerating>
    {
        protected override void Execute(BuildContext context)
        {
            var components = context.AvatarRootObject.GetComponentsInChildren<AhbssComponent>();
            foreach (var component in components)
            {
                ProcessFor(component, component.GetComponent<SkinnedMeshRenderer>(), context.AvatarRootTransform);
            }
        }

        private void ProcessFor(AhbssComponent component, SkinnedMeshRenderer skinnedMeshRenderer, Transform avatarRoot)
        {
            var originalMesh = skinnedMeshRenderer.sharedMesh;
            if (originalMesh == null)
            {
                UnityObject.DestroyImmediate(component);
                return;
            }

            var fixedParameters = Ahbss.FixedParameters.FixFromComponent(avatarRoot, component);
            var modifyingMesh = UnityObject.Instantiate(originalMesh);
            Ahbss.AddSplitShapes(skinnedMeshRenderer, modifyingMesh, fixedParameters);

            skinnedMeshRenderer.sharedMesh = modifyingMesh;
            ObjectRegistry.RegisterReplacedObject(originalMesh, modifyingMesh);
            UnityObject.DestroyImmediate(component);
        }
    }
}
