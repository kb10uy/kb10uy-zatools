using System.Collections.Generic;
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

            var component = target as ConvexDepthWrapper;
            var targetRenderer = component.GetComponent<SkinnedMeshRenderer>();
            var sourceMeshRendererField = inspector.Q<ObjectField>("FieldSourceMeshRenderer");
            var meshAssignedWarning = inspector.Q<HelpBox>("MeshAssignedWarning");
            var missingSourceMeshWarning = inspector.Q<HelpBox>("MissingSourceMeshWarning");
            void RefreshSourceState() => UpdateSourceState(component, targetRenderer, sourceMeshRendererField, meshAssignedWarning, missingSourceMeshWarning);
            RefreshSourceState();

            meshAssignedWarning.TrackPropertyValue(
                serializedObject.FindProperty(nameof(ConvexDepthWrapper.SourceMeshRenderer)),
                (_) => RefreshSourceState()
            );

            var smrSerializedObject = new SerializedObject(targetRenderer);
            var sharedMeshProperty = smrSerializedObject.FindProperty("m_Mesh");
            if (sharedMeshProperty != null)
            {
                inspector.TrackPropertyValue(sharedMeshProperty, (_) => RefreshSourceState());
            }

            return inspector;
        }

        private List<string> FetchBlendShapeNames()
        {
            var component = target as Runtime.ConvexDepthWrapper;
            var targetSkinnedMesh = component.GetComponent<SkinnedMeshRenderer>();
            var sharedMesh = component.SourceMeshRenderer != null ? component.SourceMeshRenderer.sharedMesh : targetSkinnedMesh.sharedMesh;
            return ZatoolsBlendShapeSelector.FetchBlendShapeNames(sharedMesh);
        }

        private static VisualElement MakeOverrideItem(VisualTreeAsset visualTreeItem, List<string> blendShapeNames)
        {
            var item = visualTreeItem.CloneTree();
            ZatoolsLocalization.UILocalizer.ApplyLocalizationFor(item);

            ZatoolsBlendShapeSelector.AttachTo(item.Q<Button>("ButtonOpenBlendShapeNamePanel"), item.Q<TextField>("FieldName"), blendShapeNames);

            return item;
        }

        private static void UpdateSourceState(
            ConvexDepthWrapper component,
            SkinnedMeshRenderer targetRenderer,
            ObjectField sourceMeshRendererField,
            HelpBox meshAssignedWarning,
            HelpBox missingSourceMeshWarning)
        {
            if (component == null || targetRenderer == null) return;

            var meshAssigned = targetRenderer.sharedMesh != null;
            var source = component.SourceMeshRenderer;
            var sourceMeshGeneratedOnBuild = source != null && source.TryGetComponent<AdHocAdvancedMeshDuplication>(out _);

            SetDisplayed(sourceMeshRendererField, !meshAssigned || source != null);
            SetDisplayed(meshAssignedWarning, meshAssigned && source != null);
            SetDisplayed(missingSourceMeshWarning, !meshAssigned && source != null && source.sharedMesh == null && !sourceMeshGeneratedOnBuild);
        }
    }
}
