using System.Collections.Generic;
using System.Linq;
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
            var blendShapeNames = FetchBlendShapeNames();
            var visualTree = ZatoolsResources.LoadVisualTreeByGuid("53a4f975226aac74eb22f8c7dddfc60a");
            var visualTreeItem = ZatoolsResources.LoadVisualTreeByGuid("1e565a5a766e846438f28380ddf1cf1c");

            var inspector = visualTree.CloneTree();
            ZatoolsLocalization.UILocalizer.ApplyLocalizationFor(inspector);
            inspector.Bind(serializedObject);

            var definitionsList = inspector.Q<ListView>("FieldOverrides");
            definitionsList.makeItem = () => MakeOverrideItem(visualTreeItem, blendShapeNames);

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

        private List<string> FetchBlendShapeNames()
        {
            var component = target as Runtime.ConvexDepthWrapper;
            var targetSkinnedMesh = component.GetComponent<SkinnedMeshRenderer>();
            var sharedMesh = component.SourceMeshRenderer != null ? component.SourceMeshRenderer.sharedMesh : targetSkinnedMesh.sharedMesh;
            if (sharedMesh == null) return new List<string>();

            return Enumerable.Range(0, sharedMesh.blendShapeCount)
                .Select((i) => sharedMesh.GetBlendShapeName(i))
                .ToList();
        }

        private static VisualElement MakeOverrideItem(VisualTreeAsset visualTreeItem, List<string> blendShapeNames)
        {
            var item = visualTreeItem.CloneTree();
            ZatoolsLocalization.UILocalizer.ApplyLocalizationFor(item);

            var nameField = item.Q<TextField>("FieldName");
            var openFromPanelButton = item.Q<Button>("ButtonOpenBlendShapeNamePanel");

            openFromPanelButton.clicked += () => UnityEditor.PopupWindow.Show(openFromPanelButton.worldBound, new AhbsmInspector.BlendShapeSelector(blendShapeNames, nameField));

            return item;
        }

        private static void UpdateSourceMeshRendererFieldVisibility(ObjectField sourceMeshRendererField, SkinnedMeshRenderer targetRenderer)
        {
            sourceMeshRendererField.style.display = targetRenderer != null && targetRenderer.sharedMesh == null
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }
    }
}
