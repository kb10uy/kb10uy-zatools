using UnityEngine;
using nadena.dev.ndmf;
using KusakaFactory.Zatools.Ndmf.Core;
using UnityObject = UnityEngine.Object;
using AhamdComponent = KusakaFactory.Zatools.Runtime.AdHocAdvancedMeshDuplication;

namespace KusakaFactory.Zatools.Ndmf.Pass
{
    internal sealed class AhamdGenerating : ZatoolsPass<AhamdGenerating>
    {
        protected override string ZatoolsPassName => nameof(AhamdGenerating);
        protected override string ZatoolsPassDescription => "Advanced duplicate mesh";

        protected override void Execute(BuildContext context)
        {
            var components = context.AvatarRootObject.GetComponentsInChildren<AhamdComponent>();
            foreach (var component in components)
            {
                ProcessFor(component, component.GetComponent<SkinnedMeshRenderer>(), context.AvatarRootTransform);
            }
        }

        private void ProcessFor(AhamdComponent component, SkinnedMeshRenderer skinnedMeshRenderer, Transform avatarRoot)
        {
            var originalMesh = skinnedMeshRenderer.sharedMesh;
            if (originalMesh != null)
            {
                UnityObject.DestroyImmediate(component);
                return;
            }

            var fixedParameters = Ahamd.FixedParameters.FixFromComponent(component);

            var generatedMesh = new Mesh { name = $"Advanced Mesh Duplication from {fixedParameters.Source.name}" };
            Ahamd.Process(skinnedMeshRenderer, generatedMesh, fixedParameters);

            skinnedMeshRenderer.sharedMesh = generatedMesh;
            skinnedMeshRenderer.bones = fixedParameters.Source.bones;
            skinnedMeshRenderer.rootBone = fixedParameters.Source.rootBone;
            skinnedMeshRenderer.probeAnchor = fixedParameters.Source.probeAnchor;
            skinnedMeshRenderer.localBounds = fixedParameters.Source.localBounds;

            UnityObject.DestroyImmediate(component);
        }
    }
}
