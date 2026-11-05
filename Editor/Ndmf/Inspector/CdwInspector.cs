using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using KusakaFactory.Zatools.Localization;
using KusakaFactory.Zatools.Ndmf.Core;
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
            var sourceSection = inspector.Q<VisualElement>("SectionSource");
            var meshAssignedWarning = inspector.Q<HelpBox>("MeshAssignedWarning");
            var missingSourceMeshWarning = inspector.Q<HelpBox>("MissingSourceMeshWarning");
            void RefreshSourceState() => UpdateSourceState(component, targetRenderer, sourceSection, meshAssignedWarning, missingSourceMeshWarning);
            RefreshSourceState();

            ZatoolsObjectReferenceField.Attach<SkinnedMeshRenderer>(
                inspector.Q<ObjectField>("FieldSourceReference"),
                serializedObject.FindProperty(nameof(ConvexDepthWrapper.SourceReference))
            );
            ZatoolsObjectReferenceField.AttachLegacy(
                inspector.Q<VisualElement>("SectionSourceReference"),
                inspector.Q<VisualElement>("SectionLegacySource"),
                inspector.Q<Button>("ButtonConvertSource"),
                serializedObject.FindProperty(nameof(ConvexDepthWrapper.SourceMeshRenderer)),
                () =>
                {
                    ObjectReferenceResolver.Assign(component, component.SourceReference, component.SourceMeshRenderer);
                    component.SourceMeshRenderer = null;
                }
            );
            meshAssignedWarning.TrackSerializedObjectValue(serializedObject, (_) => RefreshSourceState());

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
            var source = ObjectReferenceResolver.Resolve(component, component.SourceReference, component.SourceMeshRenderer).Target;
            var sharedMesh = source != null ? source.sharedMesh : targetSkinnedMesh.sharedMesh;
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
            VisualElement sourceSection,
            HelpBox meshAssignedWarning,
            HelpBox missingSourceMeshWarning)
        {
            if (component == null || targetRenderer == null) return;

            var meshAssigned = targetRenderer.sharedMesh != null;
            var resolved = ObjectReferenceResolver.Resolve(component, component.SourceReference, component.SourceMeshRenderer);
            var separate = resolved.State != ReferenceState.Unset;
            var source = resolved.Target;
            var sourceMeshGeneratedOnBuild = source != null && source.TryGetComponent<AdHocAdvancedMeshDuplication>(out _);

            SetDisplayed(sourceSection, !meshAssigned || separate);
            SetDisplayed(meshAssignedWarning, meshAssigned && separate);
            SetDisplayed(missingSourceMeshWarning, !meshAssigned && source != null && source.sharedMesh == null && !sourceMeshGeneratedOnBuild);
        }
    }
}
