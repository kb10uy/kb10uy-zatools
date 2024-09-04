using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using KusakaFactory.Zatools.Localization;

namespace KusakaFactory.Zatools.Inspector.AdHocBlendShapeMix
{
    [CustomEditor(typeof(Runtime.AdHocBlendShapeMix))]
    internal sealed class AdHocBlendShapeMixInspector : ZatoolEditorBase
    {
        protected override VisualElement CreateInspectorGUIImpl()
        {
            var blendShapeNames = FetchBlendShapeNames().ToList();
            var visualTree = Resources.LoadInspectorVisualTree("AdHocBlendShapeMix/AdHocBlendShapeMixInspector.uxml");
            var visualTreeItem = Resources.LoadInspectorVisualTree("AdHocBlendShapeMix/AdHocBlendShapeMixDefinition.uxml");

            var inspector = visualTree.CloneTree();
            ZatoolLocalization.UILocalizer.ApplyLocalizationFor(inspector);
            inspector.Bind(serializedObject);

            var definitionsList = inspector.Q<ListView>("FieldMixDefinitions");
            definitionsList.makeItem = () => MakeDefinitionItem(visualTreeItem, blendShapeNames);

            return inspector;
        }

        private string[] FetchBlendShapeNames()
        {
            var component = target as Runtime.AdHocBlendShapeMix;
            var targetSkinnedMesh = component.GetComponent<SkinnedMeshRenderer>();
            var sharedMesh = targetSkinnedMesh.sharedMesh;
            if (sharedMesh == null) return new string[] { };

            return Enumerable.Range(0, sharedMesh.blendShapeCount)
                .Select((i) => sharedMesh.GetBlendShapeName(i))
                .ToArray();
        }

        private static VisualElement MakeDefinitionItem(VisualTreeAsset visualTreeItem, List<string> blendShapeNames)
        {
            var item = visualTreeItem.CloneTree();
            ZatoolLocalization.UILocalizer.ApplyLocalizationFor(item);

            var fromDropdown = item.Q<DropdownField>("FieldFromBlendShape");
            var toDropdown = item.Q<DropdownField>("FieldToBlendShape");
            fromDropdown.choices = blendShapeNames;
            toDropdown.choices = blendShapeNames;

            return item;
        }
    }
}
