using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;

namespace KusakaFactory.Zatools.Ndmf.Inspector
{
    internal sealed class ZatoolsBlendShapeSelector : PopupWindowContent
    {
        private readonly IList<string> _names;
        private readonly TextField _boundField;

        internal ZatoolsBlendShapeSelector(IList<string> names, TextField boundField)
        {
            _names = names;
            _boundField = boundField;
        }

        internal static void AttachTo(Button openButton, TextField boundField, IList<string> names)
        {
            openButton.clicked += () => UnityEditor.PopupWindow.Show(openButton.worldBound, new ZatoolsBlendShapeSelector(names, boundField));
        }

        internal static List<string> FetchBlendShapeNames(Mesh mesh)
        {
            if (mesh == null) return new List<string>();
            return Enumerable.Range(0, mesh.blendShapeCount)
                .Select((i) => mesh.GetBlendShapeName(i))
                .ToList();
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
            var visualTree = ZatoolsResources.LoadVisualTreeByGuid("7bb58b5cddd547a4088117846bea5180");
            var visualTreeItem = ZatoolsResources.LoadVisualTreeByGuid("3013bdc0f3fd3274db3da3b0709626ff");

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
            var initialSelect = _names.IndexOf(_boundField.text);
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
            _boundField.value = boundItems[selectedIndex];
        }

        private void OnUpdateSearchQuery(ListView listView, string newQuery)
        {
            var trimmedQuery = newQuery.Trim().ToLowerInvariant();
            var filtered = trimmedQuery != "" ? _names.Where((n) => n.ToLowerInvariant().Contains(trimmedQuery)).ToList() : _names;
            listView.ClearSelection();
            listView.itemsSource = (IList)filtered;
        }
    }
}
