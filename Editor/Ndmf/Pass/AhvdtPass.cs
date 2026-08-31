using UnityEngine;
using nadena.dev.ndmf;
using KusakaFactory.Zatools.Foundation.Arithmetic;
using KusakaFactory.Zatools.Ndmf.Core;
using KusakaFactory.Zatools.Runtime;
using UnityObject = UnityEngine.Object;
using AhvdtComponent = KusakaFactory.Zatools.Runtime.AdHocVertexDataTransfer;

namespace KusakaFactory.Zatools.Ndmf.Pass
{
    internal sealed class AhvdtGenerating : ZatoolsPass<AhvdtGenerating>
    {
        protected override string ZatoolsPassName => nameof(AhvdtGenerating);

        protected override string ZatoolsPassDescription => "Transfer texture data to vertices";

        protected override void Execute(BuildContext context)
        {
            var components = context.AvatarRootObject.GetComponentsInChildren<AhvdtComponent>();
            foreach (var component in components)
            {
                ProcessFor(component, component.GetComponent<SkinnedMeshRenderer>());
            }
        }

        private void ProcessFor(AhvdtComponent component, SkinnedMeshRenderer skinnedMeshRenderer)
        {
            var originalMesh = skinnedMeshRenderer.sharedMesh;
            var fixedParameters = Ahvdt.FixedParameters.FixFromComponent(component);
            if (originalMesh == null || fixedParameters.SourceTexture == null || fixedParameters.TransferTarget == VertexDataTransferTarget.Disabled)
            {
                UnityObject.DestroyImmediate(component);
                return;
            }

            ZaxProgram expressionProgram = null;
            if (fixedParameters.TransferMode == VertexDataTransferMode.CustomExpression
                && !TryCompileZaxExpression(
                    component,
                    fixedParameters.Expression,
                    Ahvdt.ExpressionVariables,
                    Ahvdt.ExpressionResultType,
                    out expressionProgram))
            {
                UnityObject.DestroyImmediate(component);
                return;
            }

            var modifyingMesh = UnityObject.Instantiate(originalMesh);
            Ahvdt.Process(modifyingMesh, fixedParameters, expressionProgram);

            skinnedMeshRenderer.sharedMesh = modifyingMesh;
            ObjectRegistry.RegisterReplacedObject(originalMesh, modifyingMesh);
            UnityObject.DestroyImmediate(component);
        }
    }
}
