using UnityEngine;
using nadena.dev.ndmf;
using KusakaFactory.Zatools.Ndmf.Core;
using UnityObject = UnityEngine.Object;
using AhnbComponent = KusakaFactory.Zatools.Runtime.AdHocNormalBending;

namespace KusakaFactory.Zatools.Ndmf.Pass
{
    internal sealed class AhnbTransforming : Pass<AhnbTransforming>
    {
        public override string QualifiedName => nameof(AhnbTransforming);
        public override string DisplayName => "Bend Normal";

        protected override void Execute(BuildContext context)
        {
            var mixComponents = context.AvatarRootObject.GetComponentsInChildren<AhnbComponent>();
            foreach (var mixComponent in mixComponents) ProcessFor(mixComponent, mixComponent.GetComponent<SkinnedMeshRenderer>());
        }

        private void ProcessFor(AhnbComponent bendComponent, SkinnedMeshRenderer skinnedMeshRenderer)
        {
            var originalMesh = skinnedMeshRenderer.sharedMesh;
            if (originalMesh == null) return;

            var fixedParameters = Ahnb.FixedParameters.FixFromComponent(bendComponent);
            var modifyingMesh = UnityObject.Instantiate(originalMesh);
            Ahnb.Process(skinnedMeshRenderer, modifyingMesh, fixedParameters);

            skinnedMeshRenderer.sharedMesh = modifyingMesh;
            ObjectRegistry.RegisterReplacedObject(originalMesh, modifyingMesh);
            UnityObject.DestroyImmediate(bendComponent);
        }
    }
}
