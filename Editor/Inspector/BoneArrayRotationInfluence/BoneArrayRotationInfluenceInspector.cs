using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using KusakaFactory.Zatools.Runtime;
using KusakaFactory.Zatools.Localization;

namespace KusakaFactory.Zatools.Inspector.BoneArrayRotationInfluence
{
    [CustomEditor(typeof(Runtime.BoneArrayRotationInfluence))]
    internal sealed class BoneArrayRotationInfluenceInspector : ZatoolEditorBase
    {
        protected override VisualElement CreateInspectorGUIImpl()
        {
            var visualTree = Resources.LoadInspectorVisualTree("BoneArrayRotationInfluence/BoneArrayRotationInfluenceInspector.uxml");
            var visualTreeItem = Resources.LoadInspectorVisualTree("BoneArrayRotationInfluence/BoneArrayRotationInfluenceSource.uxml");

            var inspector = visualTree.CloneTree();
            ZatoolLocalization.UILocalizer.ApplyLocalizationFor(inspector);
            inspector.Bind(serializedObject);

            var sourcesList = inspector.Q<ListView>("FieldChainRoots");
            sourcesList.makeItem = () =>
            {
                var item = visualTreeItem.CloneTree();
                ZatoolLocalization.UILocalizer.ApplyLocalizationFor(item);
                return item;
            };

            var allInfluencesSlider = inspector.Q<Slider>("SliderUpdateAllInfluences");
            var replaceButton = inspector.Q<Button>("ButtonReplaceWithChildren");
            var updateInfluencesButton = inspector.Q<Button>("ButtonUpdateAllInfluences");
            allInfluencesSlider.value = 1.0f;
            replaceButton.clickable.clicked += () => ReplaceWithChildren(allInfluencesSlider.value);
            updateInfluencesButton.clickable.clicked += () => UpdateAllInfluences(allInfluencesSlider.value);

            return inspector;
        }

        private void ReplaceWithChildren(float influenceValue)
        {
            var attachedObjectTransform = (target as Runtime.BoneArrayRotationInfluence).transform;
            var directChildTransforms = Enumerable.Range(0, attachedObjectTransform.childCount)
                .Select((i) =>
                {
                    var transform = attachedObjectTransform.GetChild(i);
                    var rawAtan2 = Mathf.Atan2(transform.localPosition.z, transform.localPosition.x);
                    var angleFromZ = Mathf.Repeat(rawAtan2 + Mathf.PI * 1.5f, Mathf.PI * 2.0f);
                    return (Transform: transform, Angle: angleFromZ);
                })
                .OrderBy((p) => p.Angle)
                .ToList();

            serializedObject.Update();
            var chainRoots = serializedObject.FindProperty(nameof(Runtime.BoneArrayRotationInfluence.ChainRoots));
            chainRoots.ClearArray();
            for (var i = 0; i < directChildTransforms.Count; ++i)
            {
                chainRoots.InsertArrayElementAtIndex(i);

                var newItem = chainRoots.GetArrayElementAtIndex(i);
                newItem.FindPropertyRelative(nameof(RotationInfluence.Root)).objectReferenceValue = directChildTransforms[i].Transform;
                newItem.FindPropertyRelative(nameof(RotationInfluence.Influence)).floatValue = influenceValue;
            }
            serializedObject.ApplyModifiedProperties();
        }

        private void UpdateAllInfluences(float influenceValue)
        {
            serializedObject.Update();
            var chainRoots = serializedObject.FindProperty(nameof(Runtime.BoneArrayRotationInfluence.ChainRoots));
            for (var i = 0; i < chainRoots.arraySize; ++i)
            {
                var currentItem = chainRoots.GetArrayElementAtIndex(i);
                currentItem.FindPropertyRelative(nameof(RotationInfluence.Influence)).floatValue = influenceValue;
            }
            serializedObject.ApplyModifiedProperties();
        }
    }
}
