using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using KusakaFactory.Zatools.Localization;

namespace KusakaFactory.Zatools.EditorExtension
{
    internal sealed class MissingBlendShapeInserter : EditorWindow
    {
        [SerializeField]
        private List<AnimationClipItem> TargetAnimations = new List<AnimationClipItem>();
        [SerializeField]
        private bool OverwriteAnimations = true;
        [SerializeField]
        private string FillingValueMode = "All Zero";
        [SerializeField]
        private SkinnedMeshRenderer FillingValueSource = null;
        [SerializeField]
        private List<ModificationPreviewItem> ModificationPreviews = new List<ModificationPreviewItem>();

        private VisualElement _dropArea;
        private DropdownField _fillingValueMode;
        private ObjectField _fillingValueSource;
        private ListView _animationList;
        private ListView _modificationList;

        [MenuItem("Window/kb10uy/Missing BlendShape Inserter")]
        internal static void OpenWindow()
        {
            GetWindow<MissingBlendShapeInserter>();
        }

        internal void CreateGUI()
        {
            var visualTree = Resources.LoadVisualTreeByGuid("c798de8d04ea7a94f90be7c714bf91bc");
            var visualTreeModificationItem = Resources.LoadVisualTreeByGuid("9d7ee19df1e2ff8438dfbe6cbabde51a");
            visualTree.CloneTree(rootVisualElement);
            rootVisualElement.Bind(new SerializedObject(this));
            ZatoolLocalization.UILocalizer.ApplyLocalizationFor(rootVisualElement);

            _dropArea = rootVisualElement.Q<VisualElement>("AnimationDropArea");
            _dropArea.RegisterCallback<DragUpdatedEvent>((e) => DragAndDrop.visualMode = DragAndDropVisualMode.Copy);
            _dropArea.RegisterCallback<DragPerformEvent>(OnAnimationDragPerform);

            _fillingValueMode = rootVisualElement.Q<DropdownField>("FieldFillingValueMode");
            _fillingValueSource = rootVisualElement.Q<ObjectField>("FieldFillingValueSource");
            _fillingValueMode.RegisterValueChangedCallback((e) => UpdateFillingMode());

            _animationList = rootVisualElement.Q<ListView>("FieldAnimationList");
            _animationList.makeItem = OnAnimationListMakeItem;
            _animationList.bindItem = OnAnimationListBindItem;

            _modificationList = rootVisualElement.Q<ListView>("FieldModificationList");
            _modificationList.makeItem = () =>
            {
                var item = visualTreeModificationItem.CloneTree();
                ZatoolLocalization.UILocalizer.ApplyLocalizationFor(item);
                return item;
            };

            UpdateFillingMode();
        }

        private VisualElement OnAnimationListMakeItem()
        {
            var visualTree = Resources.LoadVisualTreeByGuid("206be82acb6e5494b856612308ba47b9");
            var item = visualTree.CloneTree();
            ZatoolLocalization.UILocalizer.ApplyLocalizationFor(item);

            var field = item.Q<ObjectField>();
            var removeButton = item.Q<Button>();
            field.SetEnabled(false);
            removeButton.clicked += () => OnRemoveItem(item.userData as AnimationClipItem);

            return item;
        }

        private void OnAnimationListBindItem(VisualElement item, int index)
        {
            var field = item.Q<ObjectField>();
            field.value = TargetAnimations[index].Clip;
            item.userData = TargetAnimations[index];
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

        private void UpdateFillingMode()
        {
            _fillingValueSource.style.display = _fillingValueMode.index != 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void UnionAppendClips(IEnumerable<AnimationClip> newClips)
        {
            var existingClips = new HashSet<AnimationClip>(TargetAnimations.Select((a) => a.Clip));
            foreach (var clip in newClips)
            {
                if (!existingClips.Add(clip)) continue;
                TargetAnimations.Add(new AnimationClipItem { Clip = clip });
            }
        }

        private void RefreshPreview()
        {
            ModificationPreviews.Clear();
            foreach (var c in TargetAnimations)
            {
                ModificationPreviews.Add(ModificationPreviewItem.Create(c.Clip.name, new[] { "aaa", "bbb", "ccc" }));
            }
        }

        [Serializable]
        internal sealed class AnimationClipItem
        {
            public AnimationClip Clip;
        }

        [Serializable]
        internal sealed class ModificationPreviewItem
        {
            public string BlendShapeName;
            public int ItemCount;
            public string Modifications;

            internal static ModificationPreviewItem Create(string name, string[] keys)
            {
                return new ModificationPreviewItem
                {
                    BlendShapeName = name,
                    ItemCount = keys.Length,
                    Modifications = string.Join('\n', keys.Select((k) => $"{k}: 0.0")),
                };
            }
        }
    }
}
