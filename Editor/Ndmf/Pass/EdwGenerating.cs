using UnityEngine;
using UnityEditor;
using nadena.dev.ndmf;
using KusakaFactory.Zatools.Ndmf.Core;
using UnityObject = UnityEngine.Object;
using EdwComponent = KusakaFactory.Zatools.Runtime.EyeholeDepthWrapper;

namespace KusakaFactory.Zatools.Ndmf.Pass
{
    internal sealed class EdwGenerating : Pass<EdwGenerating>
    {
        internal static readonly string WrapperMaterialGuid = "c40c829946d3b494d807a73fd79af5c5";

        public override string QualifiedName => nameof(EdwGenerating);
        public override string DisplayName => "Generate wrapping polygons for eyeholes";

        protected override void Execute(BuildContext context)
        {
            var components = context.AvatarRootObject.GetComponentsInChildren<EdwComponent>();
            foreach (var component in components)
            {
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
