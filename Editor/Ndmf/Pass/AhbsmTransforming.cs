using UnityEngine;
using nadena.dev.ndmf;
using KusakaFactory.Zatools.Ndmf.Core;
using UnityObject = UnityEngine.Object;
using AhbsmComponent = KusakaFactory.Zatools.Runtime.AdHocBlendShapeMix;

namespace KusakaFactory.Zatools.Ndmf.Pass
{
    internal sealed class AhbsmTransforming : Pass<AhbsmTransforming>
    {
        public override string QualifiedName => nameof(AhbsmTransforming);
        public override string DisplayName => "Mix BlendShapes";

        protected override void Execute(BuildContext context)
        {
            var mixComponents = context.AvatarRootObject.GetComponentsInChildren<AhbsmComponent>();
            foreach (var mixComponent in mixComponents) ProcessFor(mixComponent, mixComponent.GetComponent<SkinnedMeshRenderer>());
        }

        private void ProcessFor(AhbsmComponent mixComponent, SkinnedMeshRenderer skinnedMeshRenderer)
        {
            var originalMesh = skinnedMeshRenderer.sharedMesh;
            if (originalMesh == null) return;
            var blendShapeIndices = Ahbsm.FetchBlendShapeIndices(originalMesh);

            var resolvedMixDefinitions = Ahbsm.AggregateDefinitions(mixComponent.MixDefinitions.FixSources(), blendShapeIndices);
            if (resolvedMixDefinitions.Count == 0) return;

            var modifiedMesh = mixComponent.Replace ?
                Ahbsm.ProcessOverwrite(originalMesh, resolvedMixDefinitions) :
                Ahbsm.ProcessAppend(originalMesh, resolvedMixDefinitions);

            skinnedMeshRenderer.sharedMesh = modifiedMesh;
            ObjectRegistry.RegisterReplacedObject(originalMesh, modifiedMesh);
            UnityObject.DestroyImmediate(mixComponent);
        }
    }
}
