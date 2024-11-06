using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using KusakaFactory.Zatools.Localization;

namespace KusakaFactory.Zatools.EditorExtension
{
    internal sealed class AssetGuidList : EditorWindow
    {
        private static readonly int EntriesPerPage = 50;

        private ObjectField _asset;
        private TextField _rootPath;
        private TextField _query;
        private TextField _list;
        private Button _buttonPrev;
        private Button _buttonNext;
        private Label _pages;
        private Button _buttonSave;

        // 全部一度に表示すると頂点数がどうとか言われて例外を吐かれるのでペジネーションする
        private List<(string Guid, string Path)> _results = new List<(string Guid, string Path)>();
        private int _maxPagesCount = 1;
        private int _currentPage = 0;

        [MenuItem("Window/kb10uy/Asset GUID List")]
        internal static void OpenWindow()
        {
            GetWindow<AssetGuidList>();
        }

        internal void CreateGUI()
        {
            var visualTree = Resources.LoadVisualTreeByGuid("5ded7ab7b7f943448bcd01783bb476dd");
            visualTree.CloneTree(rootVisualElement);
            ZatoolLocalization.UILocalizer.ApplyLocalizationFor(rootVisualElement);

            _asset = rootVisualElement.Q<ObjectField>("FieldBaseAsset");
            _rootPath = rootVisualElement.Q<TextField>("FieldRootPath");
            _query = rootVisualElement.Q<TextField>("FieldQuery");
            _list = rootVisualElement.Q<TextField>("FieldListText");
            _buttonPrev = rootVisualElement.Q<Button>("ButtonPrevious");
            _buttonNext = rootVisualElement.Q<Button>("ButtonNext");
            _pages = rootVisualElement.Q<Label>("LabelPages");
            _buttonSave = rootVisualElement.Q<Button>("ButtonSave");

            _asset.RegisterValueChangedCallback(OnUpdateBaseAsset);
            _rootPath.RegisterValueChangedCallback((e) => OnUpdateQueriedAssets());
            _query.RegisterValueChangedCallback((e) => OnUpdateQueriedAssets());
            _list.SetVerticalScrollerVisibility(ScrollerVisibility.Auto);

            _buttonSave.clicked += SaveAsFile;
            _buttonPrev.clicked += MovePrevPage;
            _buttonNext.clicked += MoveNextPage;
            UpdatePaginatedView();
        }

        private void SaveAsFile()
        {
            var pathToSave = EditorUtility.SaveFilePanel("Save GUIDs to File", "", "Asset-GUIDs.txt", "txt");
            if (pathToSave.Length == 0) return;

            var saveText = string.Join('\n', _results.Select((p) => $"{p.Guid} | {p.Path}"));
            File.WriteAllText(pathToSave, saveText, new UTF8Encoding(false));
        }

        private void OnUpdateBaseAsset(ChangeEvent<UnityEngine.Object> changeEvent)
        {
            if (changeEvent.newValue == null) return;

#pragma warning disable CS0436
            // この Application は UnityEntgine.CoreModule.dll ではなく kb10uy-zatools のものを指す
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
#pragma warning restore CS0436

            var assetProjectPath = AssetDatabase.GetAssetPath(changeEvent.newValue);
            var assetFullPath = Path.Combine(projectRoot, assetProjectPath);
            if (!Directory.Exists(assetFullPath)) assetProjectPath = Path.GetDirectoryName(assetProjectPath);

            _query.SetValueWithoutNotify("");
            _rootPath.value = assetProjectPath;
        }

        private void OnUpdateQueriedAssets()
        {
            if (string.IsNullOrEmpty(_rootPath.value)) return;

            var assetGuids = AssetDatabase.FindAssets("", new[] { _rootPath.value });
            _results = assetGuids
                .Select((guid) => (guid, AssetDatabase.GUIDToAssetPath(guid)))
                .Where((p) => p.Item2.Contains(_query.value))
                .ToList();

            _maxPagesCount = Math.Max(1, (_results.Count + (EntriesPerPage - 1)) / EntriesPerPage);
            _currentPage = 0;
            UpdatePaginatedView();
        }

        private void MoveNextPage()
        {
            _currentPage = Math.Clamp(_currentPage + 1, 0, _maxPagesCount - 1);
            UpdatePaginatedView();
        }

        private void MovePrevPage()
        {
            _currentPage = Math.Clamp(_currentPage - 1, 0, _maxPagesCount - 1);
            UpdatePaginatedView();
        }

        private void UpdatePaginatedView()
        {
            _currentPage = Math.Clamp(_currentPage, 0, _maxPagesCount - 1);

            _list.value = string.Join(
                '\n',
                _results
                    .Skip(_currentPage * EntriesPerPage)
                    .Take(EntriesPerPage)
                    .Select((p) => $"{p.Guid} | {p.Path}")
            );
            _pages.text = $"{_currentPage + 1} / {_maxPagesCount}";
            _buttonPrev.SetEnabled(_currentPage > 0);
            _buttonNext.SetEnabled(_currentPage < _maxPagesCount - 1);
        }
    }
}
