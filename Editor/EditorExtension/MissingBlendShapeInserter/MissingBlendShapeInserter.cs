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
    internal sealed class AnimationPropertyUnionFiller : EditorWindow
    {
        [SerializeField]
        private List<AnimationClipItem> TargetAnimations = new List<AnimationClipItem>();
        [SerializeField]
        private bool OverwriteAnimations = true;
        [SerializeField]
        private string FillingValueMode = "All Zero";
        [SerializeField]
        private SkinnedMeshRenderer FillingValueSource = null;

        private VisualElement _dropArea;
        private DropdownField _fillingValueMode;
        private ObjectField _fillingValueSource;
        private ListView _animationList;

        [MenuItem("Window/kb10uy/Animation Property Union Filler")]
        internal static void OpenWindow()
        {
            GetWindow<AnimationPropertyUnionFiller>();
        }

        internal void CreateGUI()
        {
            var visualTree = Resources.LoadVisualTreeByGuid("c798de8d04ea7a94f90be7c714bf91bc");
            var visualTreeItem = Resources.LoadVisualTreeByGuid("206be82acb6e5494b856612308ba47b9");
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
            _animationList.makeItem = () =>
            {
                var item = visualTreeItem.CloneTree();
                ZatoolLocalization.UILocalizer.ApplyLocalizationFor(item);
                return item;
            };

            UpdateFillingMode();
        }

        private void OnAnimationDragPerform(DragPerformEvent e)
        {
            DragAndDrop.AcceptDrag();

            var appendingClips = DragAndDrop.objectReferences
                .Where((o) => o is AnimationClip)
                .Cast<AnimationClip>();
            AppendAndRefresh(appendingClips);
        }

        private void UpdateFillingMode()
        {
            _fillingValueSource.visible = _fillingValueMode.index != 0;
        }

        private void AppendAndRefresh(IEnumerable<AnimationClip> newClips)
        {
            var existingClips = new HashSet<AnimationClip>(TargetAnimations.Select((a) => a.Clip));
            foreach (var clip in newClips)
            {
                if (!existingClips.Add(clip)) continue;
                TargetAnimations.Add(new AnimationClipItem { Clip = clip });
            }
        }

        [Serializable]
        internal sealed class AnimationClipItem
        {
            public AnimationClip Clip;
        }
    }
}
