using UnityEngine;
using UnityEditor;
using nadena.dev.ndmf;
using KusakaFactory.Zatools.Ndmf.Core;
using UnityObject = UnityEngine.Object;
using CdwComponent = KusakaFactory.Zatools.Runtime.ConvexDepthWrapper;

namespace KusakaFactory.Zatools.Ndmf.Pass
{
    internal sealed class CdwGenerating : Pass<CdwGenerating>
    {
        internal static readonly string WrapperMaterialGuid = "c40c829946d3b494d807a73fd79af5c5";

        public override string QualifiedName => nameof(CdwGenerating);
        public override string DisplayName => "Generate convex depth wrapper";

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
                var generatedMesh = new Mesh { name = $"Convex Depth Wrapper for {fixedParameters.SourceMeshRenderer.name}" };
                Cdw.ProcessSeparate(skinnedMeshRenderer, generatedMesh, fixedParameters, assigningMaterial);

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
                    UnityObject.DestroyImmediate(component);
                    return;
                }

                var modifyingMesh = UnityObject.Instantiate(originalMesh);
                Cdw.Process(skinnedMeshRenderer, modifyingMesh, fixedParameters, assigningMaterial);

                skinnedMeshRenderer.sharedMesh = modifyingMesh;
                ObjectRegistry.RegisterReplacedObject(originalMesh, modifyingMesh);
            }

            UnityObject.DestroyImmediate(component);
        }
    }
}
