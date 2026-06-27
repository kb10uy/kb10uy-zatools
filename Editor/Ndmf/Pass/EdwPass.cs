using UnityEngine;
using UnityEditor;
using nadena.dev.ndmf;
using KusakaFactory.Zatools.Ndmf.Core;
using UnityObject = UnityEngine.Object;
using EdwComponent = KusakaFactory.Zatools.Runtime.EyeholeDepthWrapper;

namespace KusakaFactory.Zatools.Ndmf.Pass
{
    internal sealed class EdwGenerating : ZatoolsPass<EdwGenerating>
    {
        internal static readonly string WrapperMaterialGuid = "c40c829946d3b494d807a73fd79af5c5";

        internal override string ZatoolsPassName => nameof(EdwGenerating);
        internal override string ZatoolsPassDescription => "Generate overlapping mesh for depth override";

        protected override void Execute(BuildContext context)
        {
            var components = context.AvatarRootObject.GetComponentsInChildren<EdwComponent>();
            foreach (var component in components)
            {
                ErrorReport.ReportError(new ZatoolsNdmfError(component.gameObject, ErrorSeverity.NonFatal, "edw.report.deprecated", component.name));
                ProcessFor(component, component.GetComponent<SkinnedMeshRenderer>(), context.AvatarRootTransform);
            }
        }

        private void ProcessFor(EdwComponent component, SkinnedMeshRenderer skinnedMeshRenderer, Transform avatarRoot)
        {
            var originalMesh = skinnedMeshRenderer.sharedMesh;
            if (originalMesh == null)
            {
                UnityObject.DestroyImmediate(component);
                return;
            }

            var assigningMaterial = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(WrapperMaterialGuid));
            var fixedParameters = Edw.FixedParameters.FixFromComponent(avatarRoot, component);
            var modifyingMesh = UnityObject.Instantiate(originalMesh);
            Edw.Process(skinnedMeshRenderer, modifyingMesh, fixedParameters, assigningMaterial);

            skinnedMeshRenderer.sharedMesh = modifyingMesh;
            ObjectRegistry.RegisterReplacedObject(originalMesh, modifyingMesh);
            UnityObject.DestroyImmediate(component);
        }
    }
}
