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
        internal static readonly string WrapperMaterialGuid = "c9f88a305477a2f4ea542f4d5700daaf";

        public override string QualifiedName => nameof(CdwGenerating);
        public override string DisplayName => "Generate convex depth wrapper";

        protected override void Execute(BuildContext context)
        {
            var components = context.AvatarRootObject.GetComponentsInChildren<CdwComponent>();
            foreach (var component in components)
            {
                ProcessFor(component, component.GetComponent<SkinnedMeshRenderer>(), context);
            }
        }

        private void ProcessFor(CdwComponent component, SkinnedMeshRenderer destinationRenderer, BuildContext context)
        {
            if (destinationRenderer == null || component.SourceRenderer == null)
            {
                UnityObject.DestroyImmediate(component);
                return;
            }

            if (component.SourceRenderer.sharedMesh == null)
            {
                UnityObject.DestroyImmediate(component);
                return;
            }

            var wrapperMaterial = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(WrapperMaterialGuid));
            if (wrapperMaterial == null)
            {
                UnityObject.DestroyImmediate(component);
                return;
            }

            var generatedMesh = Cdw.GenerateConvexHullMesh(component.SourceRenderer);
            if (generatedMesh == null)
            {
                UnityObject.DestroyImmediate(component);
                return;
            }

            using (context.OpenSerializationScope())
            {
                context.AssetSaver.SaveAsset(generatedMesh);
            }

            destinationRenderer.sharedMesh = generatedMesh;
            destinationRenderer.rootBone = component.SourceRenderer.rootBone;
            destinationRenderer.bones = component.SourceRenderer.bones;
            destinationRenderer.updateWhenOffscreen = component.SourceRenderer.updateWhenOffscreen;
            destinationRenderer.sharedMaterials = new[] { wrapperMaterial };

            UnityObject.DestroyImmediate(component);
        }
    }
}
