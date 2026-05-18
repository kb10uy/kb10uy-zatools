using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using KusakaFactory.Zatools.Localization;
using KusakaFactory.Zatools.Runtime;

namespace KusakaFactory.Zatools.Ndmf.Inspector
{
    [CustomEditor(typeof(ConvexDepthWrapper))]
    internal sealed class CdwInspector : ZatoolsInspector
    {
        protected override VisualElement CreateInspectorGUIImpl()
        {
            var visualTree = ZatoolsResources.LoadVisualTreeByGuid("53a4f975226aac74eb22f8c7dddfc60a");

            var inspector = visualTree.CloneTree();
            ZatoolsLocalization.UILocalizer.ApplyLocalizationFor(inspector);
            inspector.Bind(serializedObject);

            var sourceMeshRendererField = inspector.Q<ObjectField>("FieldSourceMeshRenderer");
            var targetRenderer = (target as ConvexDepthWrapper)?.GetComponent<SkinnedMeshRenderer>();
            UpdateSourceMeshRendererFieldVisibility(sourceMeshRendererField, targetRenderer);

            var smrSerializedObject = new SerializedObject(targetRenderer);
            var sharedMeshProperty = smrSerializedObject.FindProperty("m_Mesh");
            if (sharedMeshProperty != null)
            {
                inspector.TrackPropertyValue(
                    sharedMeshProperty,
                    (_) => UpdateSourceMeshRendererFieldVisibility(sourceMeshRendererField, targetRenderer)
                );
            }

            return inspector;
        }

        private static void UpdateSourceMeshRendererFieldVisibility(ObjectField sourceMeshRendererField, SkinnedMeshRenderer targetRenderer)
        {
            sourceMeshRendererField.style.display = targetRenderer != null && targetRenderer.sharedMesh == null
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }
    }
}
