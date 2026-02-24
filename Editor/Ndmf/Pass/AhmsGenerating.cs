using UnityEngine;
using nadena.dev.ndmf;
using KusakaFactory.Zatools.Ndmf.Core;
using UnityObject = UnityEngine.Object;
using AhmsComponent = KusakaFactory.Zatools.Runtime.AdHocMeshSplit;

namespace KusakaFactory.Zatools.Ndmf.Pass
{
    internal sealed class AhmsGenerating : Pass<AhmsGenerating>
    {
        protected override void Execute(BuildContext context)
        {
            var components = context.AvatarRootObject.GetComponentsInChildren<AhmsComponent>();
            foreach (var component in components)
            {
                ProcessFor(component, component.GetComponent<SkinnedMeshRenderer>(), context.AvatarRootTransform);
            }
        }

        private void ProcessFor(AhmsComponent component, SkinnedMeshRenderer skinnedMeshRenderer, Transform avatarRoot)
        {
            var originalMesh = skinnedMeshRenderer.sharedMesh;
            if (originalMesh == null)
            {
                UnityObject.DestroyImmediate(component);
                return;
            }

            var fixedParameters = Ahms.FixedParameters.FixFromComponent(component);
            if (fixedParameters.IsUnreadableMask)
            {
                ErrorReport.ReportError(new ZatoolsNdmfError(ErrorSeverity.Error, "ahms.report.unreadable-mask", skinnedMeshRenderer));
                UnityObject.DestroyImmediate(component);
                return;
            }
            if (fixedParameters.SplitMaterial == null)
            {
                UnityObject.DestroyImmediate(component);
                return;
            }

            var modifyingMesh = UnityObject.Instantiate(originalMesh);
            Ahms.Process(skinnedMeshRenderer, modifyingMesh, fixedParameters);

            skinnedMeshRenderer.sharedMesh = modifyingMesh;
            ObjectRegistry.RegisterReplacedObject(originalMesh, modifyingMesh);
            UnityObject.DestroyImmediate(component);
        }
    }
}
