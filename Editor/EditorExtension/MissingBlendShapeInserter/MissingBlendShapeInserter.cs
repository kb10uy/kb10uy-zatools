using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using KusakaFactory.Zatools.Localization;

namespace KusakaFactory.Zatools.EditorExtension.MissingBlendShapeInserter
{
    internal sealed class MissingBlendShapeInserter : EditorWindow
    {
        [SerializeField]
        private List<AnimationClipItem> TargetAnimations = new List<AnimationClipItem>();
        [SerializeField]
        private bool OverwriteAnimations = true;
        [SerializeField]
        private string SaveDirectory = "Assets";
        [SerializeField]
        private string TargetAnimationPath = "Body";
        [SerializeField]
        private string FillingValueMode = "All Zero";
        [SerializeField]
        private SkinnedMeshRenderer FillingValueSource = null;
        [SerializeField]
        private List<ModificationPreviewItem> ModificationPreviews = new List<ModificationPreviewItem>();

        private VisualElement _dropArea;
        private ListView _animationList;
        private ListView _modificationList;
        private TextField _targetAnimationPath;
        private DropdownField _fillingValueMode;
        private ObjectField _fillingValueSource;
        private Toggle _overwrite;
        private VisualElement _savePathElement;
        private Button _openSavePath;
        private Button _applyButton;

        [MenuItem("Window/Zatools: kb10uy's Various Tools/Missing BlendShape Inserter")]
        internal static void OpenWindow()
        {
            GetWindow<MissingBlendShapeInserter>();
        }

        internal void CreateGUI()
        {
            var visualTree = ZatoolsResources.LoadVisualTreeByGuid("c798de8d04ea7a94f90be7c714bf91bc");
            var visualTreeModificationItem = ZatoolsResources.LoadVisualTreeByGuid("9d7ee19df1e2ff8438dfbe6cbabde51a");
            visualTree.CloneTree(rootVisualElement);
            rootVisualElement.Bind(new SerializedObject(this));
            ZatoolsLocalization.UILocalizer.ApplyLocalizationFor(rootVisualElement);

            _animationList = rootVisualElement.Q<ListView>("FieldAnimationList");
            _animationList.makeItem = OnAnimationListMakeItem;
            _animationList.bindItem = OnAnimationListBindItem;

            _modificationList = rootVisualElement.Q<ListView>("FieldModificationList");
            _modificationList.makeItem = () =>
            {
                var item = visualTreeModificationItem.CloneTree();
                ZatoolsLocalization.UILocalizer.ApplyLocalizationFor(item);
                return item;
            };

            _targetAnimationPath = rootVisualElement.Q<TextField>("FieldTargetAnimationPath");
            _targetAnimationPath.RegisterValueChangedCallback((e) => RefreshPreview());

            _dropArea = rootVisualElement.Q<VisualElement>("AnimationDropArea");
            _dropArea.RegisterCallback<DragUpdatedEvent>((e) => DragAndDrop.visualMode = DragAndDropVisualMode.Copy);
            _dropArea.RegisterCallback<DragPerformEvent>(OnAnimationDragPerform);

            _fillingValueMode = rootVisualElement.Q<DropdownField>("FieldFillingValueMode");
            _fillingValueSource = rootVisualElement.Q<ObjectField>("FieldFillingValueSource");
            _fillingValueMode.RegisterValueChangedCallback((e) => OnFillingValueSettingsChanged());
            _fillingValueSource.RegisterValueChangedCallback((e) => OnFillingValueSettingsChanged());

            _overwrite = rootVisualElement.Q<Toggle>("FieldOverwrite");
            _savePathElement = rootVisualElement.Q<VisualElement>("SavePathElement");
            _openSavePath = rootVisualElement.Q<Button>("OpenSavePath");
            _overwrite.RegisterValueChangedCallback((e) => OnOverwriteChanged());
            _openSavePath.clicked += OnOpenSavePath;

            _applyButton = rootVisualElement.Q<Button>("ApplyButton");
            _applyButton.clicked += OnApply;

            OnFillingValueSettingsChanged();
            OnOverwriteChanged();
        }

        private VisualElement OnAnimationListMakeItem()
        {
            var visualTree = ZatoolsResources.LoadVisualTreeByGuid("206be82acb6e5494b856612308ba47b9");
            var item = visualTree.CloneTree();
            ZatoolsLocalization.UILocalizer.ApplyLocalizationFor(item);

            var fillingToggle = item.Q<Toggle>();
            var field = item.Q<ObjectField>();
            var removeButton = item.Q<Button>();
            field.SetEnabled(false);
            fillingToggle.RegisterValueChangedCallback((e) => OnItemFillingChanged(item.userData as AnimationClipItem, e.newValue));
            removeButton.clicked += () => OnRemoveItem(item.userData as AnimationClipItem);

            return item;
        }

        private void OnAnimationListBindItem(VisualElement item, int index)
        {
            item.userData = TargetAnimations[index];

            var field = item.Q<ObjectField>();
            var fillingToggle = item.Q<Toggle>();
            field.value = TargetAnimations[index].Clip;
            fillingToggle.value = TargetAnimations[index].ApplyFilling;
        }

        private void OnItemFillingChanged(AnimationClipItem item, bool newValue)
        {
            item.ApplyFilling = newValue;
            RefreshPreview();
        }

        private void OnRemoveItem(AnimationClipItem item)
        {
            TargetAnimations.Remove(item);
            RefreshPreview();
        }

        private void OnAnimationDragPerform(DragPerformEvent e)
        {
            DragAndDrop.AcceptDrag();

            var appendingClips = DragAndDrop.objectReferences
                .Where((o) => o is AnimationClip)
                .Cast<AnimationClip>();
            UnionAppendClips(appendingClips);
            RefreshPreview();
        }

        private void OnFillingValueSettingsChanged()
        {
            _fillingValueSource.SetEnabled(_fillingValueMode.index != 0);
            RefreshPreview();
        }

        private void OnOverwriteChanged()
        {
            _savePathElement.SetEnabled(!OverwriteAnimations);
        }

        private void OnOpenSavePath()
        {
            var path = EditorUtility.OpenFolderPanel("Select directory to save animations...", "", "");
            if (string.IsNullOrWhiteSpace(path)) return;

            var assetsPath = Application.dataPath;
            if (!path.StartsWith(assetsPath)) return;

            // Application.dataPath は Assets/ を含むが SaveDirectory は Assets/**/* である必要がある
            SaveDirectory = $"Assets/{path[assetsPath.Length..]}";
        }

        private void OnApply()
        {
            var process = new MissingBlendShapeInserterProcess(
                TargetAnimations.Select((aci) => (aci.Clip, aci.ApplyFilling)),
                TargetAnimationPath,
                FillingValueMode != "All Zero" ? FillingValueSource : null,
                OverwriteAnimations ? null : SaveDirectory
            );
            process.Apply();

            TargetAnimations.Clear();
            RefreshPreview();
        }

        private void UnionAppendClips(IEnumerable<AnimationClip> newClips)
        {
            var existingClips = new HashSet<AnimationClip>(TargetAnimations.Select((a) => a.Clip));
            foreach (var clip in newClips)
            {
                if (!existingClips.Add(clip)) continue;
                TargetAnimations.Add(new AnimationClipItem { Clip = clip, ApplyFilling = true });
            }
        }

        private void RefreshPreview()
        {
            var process = new MissingBlendShapeInserterProcess(
                TargetAnimations.Select((aci) => (aci.Clip, aci.ApplyFilling)),
                TargetAnimationPath,
                FillingValueMode != "All Zero" ? FillingValueSource : null,
                OverwriteAnimations ? null : SaveDirectory
            );

            ModificationPreviews.Clear();
            foreach (var c in TargetAnimations)
            {
                if (!c.ApplyFilling) continue;
                var missingBlendShapes = process
                    .CalculateMissingBlendShapeNamesFor(c.Clip)
                    .Select((bs) => (bs, process.GetCopyingBlendShapeValue(bs)));
                var previewItem = ModificationPreviewItem.Create(c.Clip.name, missingBlendShapes.ToArray());
                ModificationPreviews.Add(previewItem);
            }

            _applyButton.SetEnabled(TargetAnimations.Count > 0);
        }

        [Serializable]
        internal sealed class AnimationClipItem
        {
            public AnimationClip Clip;
            public bool ApplyFilling;
        }

        [Serializable]
        internal sealed class ModificationPreviewItem
        {
            public string BlendShapeName;
            public int ItemCount;
            public string Modifications;

            internal static ModificationPreviewItem Create(string name, IEnumerable<(string Name, float Value)> blendShapes)
            {
                var values = blendShapes.ToList();
                return new ModificationPreviewItem
                {
                    BlendShapeName = name,
                    ItemCount = values.Count,
                    Modifications = string.Join('\n', values.Select((k) => $"{k.Name}: {k.Value}")),
                };
            }
        }
    }
}
