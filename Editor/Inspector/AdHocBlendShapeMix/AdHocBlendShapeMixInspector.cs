using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using KusakaFactory.Zatools.Localization;
using KusakaFactory.Zatools.Runtime;

namespace KusakaFactory.Zatools.Inspector.AdHocBlendShapeMix
{
    [CustomEditor(typeof(Runtime.AdHocBlendShapeMix))]
    internal sealed class AdHocBlendShapeMixInspector : ZatoolEditorBase
    {
        protected override VisualElement CreateInspectorGUIImpl()
        {
            var blendShapeNames = FetchBlendShapeNames();
            var visualTree = Resources.LoadInspectorVisualTree("AdHocBlendShapeMix/AdHocBlendShapeMixInspector.uxml");
            var visualTreeItem = Resources.LoadInspectorVisualTree("AdHocBlendShapeMix/AdHocBlendShapeMixDefinition.uxml");

            var inspector = visualTree.CloneTree();
            ZatoolLocalization.UILocalizer.ApplyLocalizationFor(inspector);
            inspector.Bind(serializedObject);

            var definitionsList = inspector.Q<ListView>("FieldMixDefinitions");
            definitionsList.makeItem = () => MakeDefinitionItem(visualTreeItem, blendShapeNames);

            var saveButton = inspector.Q<Button>("ButtonSaveToJson");
            var loadButton = inspector.Q<Button>("ButtonLoadFromJson");
            saveButton.clicked += SaveToJson;
            loadButton.clicked += LoadFromJson;

            var fromRegexField = inspector.Q<TextField>("FieldFromRegex");
            var ToRegexField = inspector.Q<TextField>("FieldToRegex");
            var appendRegexButton = inspector.Q<Button>("ButtonAppendWithRegex");
            appendRegexButton.clicked += () => AppendWithRegex(
                blendShapeNames,
                FetchBlendShapeRelativeWeights(),
                fromRegexField.value,
                ToRegexField.value
            );

            return inspector;
        }

        private void SaveToJson()
        {
            var component = target as Runtime.AdHocBlendShapeMix;
            var pathToSave = EditorUtility.SaveFilePanel(
                "Save Mix Definitions as JSON",
                "",
                $"{component.gameObject.name}-BlendShapeMix.json",
                "json"
            );
            if (pathToSave.Length == 0) return;

            var json = JsonConvert.SerializeObject(component.MixDefinitions, Formatting.Indented);
            File.WriteAllText(pathToSave, json, new UTF8Encoding(false));
        }

        private void LoadFromJson()
        {
            var pathToLoad = EditorUtility.OpenFilePanel("Load Mix Definitions from JSON", "", "json");
            if (pathToLoad.Length == 0) return;

            var jsonText = File.ReadAllText(pathToLoad, new UTF8Encoding(false));
            var loadedArray = JsonConvert.DeserializeObject<BlendShapeMixDefinition[]>(jsonText);

            // defs に objectReference を設定すると undo が効かない
            var definitions = serializedObject.FindProperty(nameof(Runtime.AdHocBlendShapeMix.MixDefinitions));
            serializedObject.Update();
            definitions.ClearArray();
            AppendDefinitionItems(loadedArray);
            serializedObject.ApplyModifiedProperties();
        }

        private void AppendWithRegex(IList<string> names, IList<float> weights, string fromRegex, string toRegex)
        {
            if (fromRegex == "" || toRegex == "") return;

            var fromPattern = new Regex(fromRegex);
            var toPattern = new Regex(toRegex);

            var sources = names
                .Select((n, i) => (Name: n, Index: i))
                .Where((p) => fromPattern.IsMatch(p.Name) && weights[p.Index] > Mathf.Epsilon)
                .Select((p) => (From: p.Name, Weight: -weights[p.Index]))
                .ToArray();

            var definitions = names
                .Where((n) => toPattern.IsMatch(n))
                .SelectMany((n) => sources.Select((sp) => new BlendShapeMixDefinition
                {
                    FromBlendShape = sp.From,
                    ToBlendShape = n,
                    MixWeight = sp.Weight,
                }))
                .ToArray();

            serializedObject.Update();
            AppendDefinitionItems(definitions);
            serializedObject.ApplyModifiedProperties();
        }

        private List<string> FetchBlendShapeNames()
        {
            var component = target as Runtime.AdHocBlendShapeMix;
            var targetSkinnedMesh = component.GetComponent<SkinnedMeshRenderer>();
            var sharedMesh = targetSkinnedMesh.sharedMesh;
            if (sharedMesh == null) return new List<string>();

            return Enumerable.Range(0, sharedMesh.blendShapeCount)
                .Select((i) => sharedMesh.GetBlendShapeName(i))
                .ToList();
        }

        private List<float> FetchBlendShapeRelativeWeights()
        {
            var component = target as Runtime.AdHocBlendShapeMix;
            var targetSkinnedMesh = component.GetComponent<SkinnedMeshRenderer>();
            var sharedMesh = targetSkinnedMesh.sharedMesh;
            if (sharedMesh == null) return new List<float>();

            return Enumerable.Range(0, sharedMesh.blendShapeCount)
                .Select((i) => targetSkinnedMesh.GetBlendShapeWeight(i) / sharedMesh.GetBlendShapeFrameWeight(i, 0))
                .ToList();
        }

        private void AppendDefinitionItems(IEnumerable<BlendShapeMixDefinition> items)
        {
            var definitions = serializedObject.FindProperty(nameof(Runtime.AdHocBlendShapeMix.MixDefinitions));
            var nextIndex = definitions.arraySize;
            foreach (var item in items)
            {
                definitions.InsertArrayElementAtIndex(nextIndex);
                var elem = definitions.GetArrayElementAtIndex(nextIndex);
                elem.FindPropertyRelative(nameof(BlendShapeMixDefinition.FromBlendShape)).stringValue = item.FromBlendShape;
                elem.FindPropertyRelative(nameof(BlendShapeMixDefinition.ToBlendShape)).stringValue = item.ToBlendShape;
                elem.FindPropertyRelative(nameof(BlendShapeMixDefinition.MixWeight)).floatValue = item.MixWeight;
                ++nextIndex;
            }
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
                blendShapeNameList.bindItem = (e, i) => OnBindItem(blendShapeNameList, e, i);
                blendShapeNameList.selectedIndicesChanged += (idxs) => OnSelectionChanged(blendShapeNameList, idxs);
                // ダブルクリックで閉じられるようにする
                var doubleClick = new Clickable(() => editorWindow.Close());
                doubleClick.activators.Clear();
                doubleClick.activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse, clickCount = 2 });
                blendShapeNameList.AddManipulator(doubleClick);
                // 初期選択
                var initialSelect = _names.IndexOf(_boundButton.text);
                if (initialSelect != -1) blendShapeNameList.SetSelection(initialSelect);

                var searchQueryField = editorWindow.rootVisualElement.Q<TextField>("FieldSearchQuery");
                searchQueryField.RegisterCallback<ChangeEvent<string>>((ce) => OnUpdateSearchQuery(blendShapeNameList, ce.newValue));
            }

            private void OnBindItem(ListView listView, VisualElement itemElement, int index)
            {
                itemElement.Q<Label>("LabelName").text = listView.itemsSource[index] as string;
            }

            private void OnSelectionChanged(ListView listView, IEnumerable<int> selectionIndices)
            {
                var selectedIndex = selectionIndices.DefaultIfEmpty(-1).First();
                if (selectedIndex == -1) return;
                var boundItems = listView.itemsSource as IList<string>;
                _boundButton.text = boundItems[selectionIndices.First()];
            }

            private void OnUpdateSearchQuery(ListView listView, string newQuery)
            {
                var trimmedQuery = newQuery.Trim();
                var filtered = trimmedQuery != "" ? _names.Where((n) => n.Contains(trimmedQuery)).ToList() : _names;
                listView.ClearSelection();
                listView.itemsSource = (IList)filtered;
            }
        }
    }
}
