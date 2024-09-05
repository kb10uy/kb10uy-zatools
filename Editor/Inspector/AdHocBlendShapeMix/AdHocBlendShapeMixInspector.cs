using System.Collections;
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

            var fromButton = item.Q<Button>("FieldFromBlendShape");
            var toButton = item.Q<Button>("FieldToBlendShape");
            fromButton.clicked += () => UnityEditor.PopupWindow.Show(fromButton.worldBound, new BlendShapeSelector(blendShapeNames, fromButton));
            toButton.clicked += () => UnityEditor.PopupWindow.Show(toButton.worldBound, new BlendShapeSelector(blendShapeNames, toButton));

            return item;
        }

        internal sealed class BlendShapeSelector : PopupWindowContent
        {
            private IList<string> _names;
            private Button _boundButton;

            internal BlendShapeSelector(IList<string> names, Button boundButton)
            {
                _names = names;
                _boundButton = boundButton;
            }

            public override void OnGUI(Rect rect)
            {
                // Keep empty
            }

            public override Vector2 GetWindowSize()
            {
                return new Vector2(200.0f, 320.0f);
            }

            public override void OnOpen()
            {
                var visualTree = Resources.LoadInspectorVisualTree("AdHocBlendShapeMix/AdHocBlendShapeMixBlendShapeSelector.uxml");
                var visualTreeItem = Resources.LoadInspectorVisualTree("AdHocBlendShapeMix/AdHocBlendShapeMixBlendShapeItem.uxml");

                visualTree.CloneTree(editorWindow.rootVisualElement);

                var blendShapeNameList = editorWindow.rootVisualElement.Q<ListView>("FieldBlendShapeNames");
                blendShapeNameList.itemsSource = (IList)_names;
                blendShapeNameList.makeItem = visualTreeItem.CloneTree;
                blendShapeNameList.bindItem = (e, i) => e.Q<Label>("LabelName").text = blendShapeNameList.itemsSource[i] as string;
                blendShapeNameList.selectedIndicesChanged += (idxs) => _boundButton.text = _names[idxs.First()];

                var doubleClick = new Clickable(() => editorWindow.Close());
                doubleClick.activators.Clear();
                doubleClick.activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse, clickCount = 2 });
                blendShapeNameList.AddManipulator(doubleClick);

                var initialSelect = _names.IndexOf(_boundButton.text);
                if (initialSelect != -1) blendShapeNameList.SetSelection(initialSelect);
            }
        }
    }
}
