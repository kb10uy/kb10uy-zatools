using System.Collections.Generic;
using UnityEngine;
using nadena.dev.ndmf;
using KusakaFactory.Zatools.Foundation.Arithmetic;
using KusakaFactory.Zatools.Ndmf.Core;
using UnityObject = UnityEngine.Object;
using AhabssComponent = KusakaFactory.Zatools.Runtime.AdHocAdvancedBlendShapeSynthesis;

namespace KusakaFactory.Zatools.Ndmf.Pass
{
    internal sealed class AhabssGenerating : ZatoolsPass<AhabssGenerating>
    {
        protected override string ZatoolsPassName => nameof(AhabssGenerating);
        protected override string ZatoolsPassDescription => "Advanced BlendShape synthesis";

        protected override void Execute(BuildContext context)
        {
            var components = context.AvatarRootObject.GetComponentsInChildren<AhabssComponent>();
            foreach (var component in components)
            {
                ProcessFor(component, component.GetComponent<SkinnedMeshRenderer>());
            }
        }

        private void ProcessFor(AhabssComponent component, SkinnedMeshRenderer skinnedMeshRenderer)
        {
            var originalMesh = skinnedMeshRenderer.sharedMesh;
            if (originalMesh == null)
            {
                UnityObject.DestroyImmediate(component);
                return;
            }

            var fixedParameters = Ahabss.FixedParameters.FixFromComponent(component);

            bool CompileReporting(
                string source,
                IReadOnlyList<ZaxVariable> variables,
                IReadOnlyList<ZaxValueType> expectedResultTypes,
                out ZaxProgram program)
            {
                return TryCompileZaxExpression(component.gameObject, source, variables, expectedResultTypes, out program);
            }

            if (!Ahabss.TryCompilePrograms(fixedParameters, CompileReporting, out var programs))
            {
                UnityObject.DestroyImmediate(component);
                return;
            }

            var modifyingMesh = UnityObject.Instantiate(originalMesh);
            var result = Ahabss.Process(originalMesh, modifyingMesh, fixedParameters, programs);
            foreach (var entry in result.Skipped)
            {
                ErrorReport.ReportError(new ZatoolsNdmfError(
                    component.gameObject,
                    ErrorSeverity.NonFatal,
                    "ahabss.report.duplicate-name",
                    entry.Name));
            }

            skinnedMeshRenderer.sharedMesh = modifyingMesh;
            foreach (var (index, weight) in Ahabss.ResolveWeights(modifyingMesh, result.Added))
            {
                skinnedMeshRenderer.SetBlendShapeWeight(index, weight);
            }

            ObjectRegistry.RegisterReplacedObject(originalMesh, modifyingMesh);
            UnityObject.DestroyImmediate(component);
        }
    }
}
