using System.Collections.Generic;
using UnityEngine;
using nadena.dev.ndmf;
using KusakaFactory.Zatools.Runtime;
using UnityObject = UnityEngine.Object;

namespace KusakaFactory.Zatools.Modules
{
    internal sealed class UVIntegerShifter : Pass<UVIntegerShifter>
    {
        public override string QualifiedName => nameof(UVIntegerShifter);
        public override string DisplayName => "Shift integer part(s) of mesh UV(s)";

        protected override void Execute(BuildContext context)
        {
            var components = context.AvatarRootObject.GetComponentsInChildren<UVIntegerShift>();
            foreach (var component in components) Apply(context, component, component.GetComponent<SkinnedMeshRenderer>());
        }

        private void Apply(BuildContext context, UVIntegerShift shiftComponent, SkinnedMeshRenderer skinnedMeshRenderer)
        {
            var originalMesh = skinnedMeshRenderer.sharedMesh;
            var modifiedMesh = UnityObject.Instantiate(originalMesh);

            var shiftValue = new Vector2(shiftComponent.ShiftX, shiftComponent.ShiftY);
            var modifyingUVs = new List<Vector2>();
            modifiedMesh.GetUVs((int)shiftComponent.SourceUV, modifyingUVs);
            for (int i = 0; i < modifyingUVs.Count; ++i) modifyingUVs[i] += shiftValue;
            modifiedMesh.SetUVs((int)shiftComponent.DestinationUV, modifyingUVs);

            skinnedMeshRenderer.sharedMesh = modifiedMesh;
            ObjectRegistry.RegisterReplacedObject(originalMesh, modifiedMesh);
            UnityObject.DestroyImmediate(shiftComponent);
        }
    }
}
