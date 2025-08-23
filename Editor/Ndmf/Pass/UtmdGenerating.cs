using UnityEngine;
using nadena.dev.ndmf;
using KusakaFactory.Zatools.Ndmf.Core;
using UnityObject = UnityEngine.Object;
using UtmdComponent = KusakaFactory.Zatools.Runtime.UvTileMapDistribution;

namespace KusakaFactory.Zatools.Ndmf.Pass
{
    internal sealed class UtmdGenerating : Pass<UtmdGenerating>
    {
        public override string QualifiedName => nameof(UtmdGenerating);
        public override string DisplayName => "UV Tile Map Distribution";

        protected override void Execute(BuildContext context)
        {
            var components = context.AvatarRootObject.GetComponentsInChildren<UtmdComponent>();
            foreach (var component in components)
            {
                ProcessFor(component, component.GetComponent<SkinnedMeshRenderer>());
            }
        }

        private void ProcessFor(UtmdComponent component, SkinnedMeshRenderer skinnedMeshRenderer)
        {
            var originalMesh = skinnedMeshRenderer.sharedMesh;
            if (originalMesh == null || component.TileMap == null)
            {
                UnityObject.DestroyImmediate(component);
                return;
            }

            var fixedParameters = Utmd.FixedParameters.FixFromComponent(component);
            var modifyingMesh = UnityObject.Instantiate(originalMesh);
            Utmd.Process(modifyingMesh, fixedParameters);

            skinnedMeshRenderer.sharedMesh = modifyingMesh;
            ObjectRegistry.RegisterReplacedObject(originalMesh, modifyingMesh);
            UnityObject.DestroyImmediate(component);
        }
    }
}
