using UnityEngine;
using UnityEditor;
using nadena.dev.ndmf;
using KusakaFactory.Zatools.Ndmf.Core;
using UnityObject = UnityEngine.Object;
using CdwComponent = KusakaFactory.Zatools.Runtime.ConvexDepthWrapper;

namespace KusakaFactory.Zatools.Ndmf.Pass
{
    internal sealed class CdwGenerating : ZatoolsPass<CdwGenerating>
    {
        internal static readonly string WrapperMaterialGuid = "c40c829946d3b494d807a73fd79af5c5";

        protected override string ZatoolsPassName => nameof(CdwGenerating);
        protected override string ZatoolsPassDescription => "Generate convex mesh for depth override";

        protected override void Execute(BuildContext context)
        {
            var components = context.AvatarRootObject.GetComponentsInChildren<CdwComponent>();
            foreach (var component in components)
            {
                ProcessFor(component, component.GetComponent<SkinnedMeshRenderer>(), context.AvatarRootTransform);
            }
        }

        private void ProcessFor(CdwComponent component, SkinnedMeshRenderer skinnedMeshRenderer, Transform avatarRoot)
        {
            var fixedParameters = Cdw.FixedParameters.FixFromComponent(component);
            var assigningMaterial = component.MaterialOverride != null ?
                component.MaterialOverride :
                AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(WrapperMaterialGuid));

            if (fixedParameters.SeparateSmr)
            {
                if (skinnedMeshRenderer.sharedMesh != null)
                {
                    ErrorReport.ReportError(new ZatoolsNdmfError(component.gameObject, ErrorSeverity.NonFatal, "cdw.report.mesh-assigned"));
                    UnityObject.DestroyImmediate(component);
                    return;
                }

                if (fixedParameters.SourceMeshRenderer.sharedMesh == null)
                {
                    ErrorReport.ReportError(new ZatoolsNdmfError(component.gameObject, ErrorSeverity.NonFatal, "cdw.report.missing-source-mesh"));
                    UnityObject.DestroyImmediate(component);
                    return;
                }

                var generatedMesh = new Mesh { name = $"Convex Depth Wrapper for {fixedParameters.SourceMeshRenderer.name}" };
                if (!Cdw.ProcessSeparate(skinnedMeshRenderer, generatedMesh, fixedParameters, assigningMaterial))
                {
                    ErrorReport.ReportError(new ZatoolsNdmfError(component.gameObject, ErrorSeverity.NonFatal, "cdw.report.degenerate-hull"));
                    UnityObject.DestroyImmediate(generatedMesh);
                    UnityObject.DestroyImmediate(component);
                    return;
                }

                skinnedMeshRenderer.sharedMesh = generatedMesh;
                skinnedMeshRenderer.bones = fixedParameters.SourceMeshRenderer.bones;
                skinnedMeshRenderer.rootBone = fixedParameters.SourceMeshRenderer.rootBone;
                skinnedMeshRenderer.probeAnchor = fixedParameters.SourceMeshRenderer.probeAnchor;
                skinnedMeshRenderer.localBounds = fixedParameters.SourceMeshRenderer.localBounds;
            }
            else
            {
                var originalMesh = skinnedMeshRenderer.sharedMesh;
                if (originalMesh == null)
                {
                    ErrorReport.ReportError(new ZatoolsNdmfError(component.gameObject, ErrorSeverity.NonFatal, "cdw.report.missing-mesh"));
                    UnityObject.DestroyImmediate(component);
                    return;
                }

                var modifyingMesh = UnityObject.Instantiate(originalMesh);
                if (!Cdw.Process(skinnedMeshRenderer, modifyingMesh, fixedParameters, assigningMaterial))
                {
                    ErrorReport.ReportError(new ZatoolsNdmfError(component.gameObject, ErrorSeverity.NonFatal, "cdw.report.degenerate-hull"));
                    UnityObject.DestroyImmediate(modifyingMesh);
                    UnityObject.DestroyImmediate(component);
                    return;
                }

                skinnedMeshRenderer.sharedMesh = modifyingMesh;
                ObjectRegistry.RegisterReplacedObject(originalMesh, modifyingMesh);
            }

            UnityObject.DestroyImmediate(component);
        }
    }
}
