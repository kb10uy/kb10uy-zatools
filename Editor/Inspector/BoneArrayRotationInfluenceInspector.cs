using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using KusakaFactory.Zatools.Runtime;

namespace KusakaFactory.Zatools.Inspector
{
    [CustomEditor(typeof(BoneArrayRotationInfluence))]
    internal sealed class BoneArrayRotationInfluenceInspector : Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var visualTree = InspectorUtil.LoadInspectorVisualTree("BoneArrayRotationInfluenceInspector");
            var visualTreeItem = InspectorUtil.LoadInspectorVisualTree("BoneArrayRotationInfluenceSource");

            var inspector = new VisualElement();
            visualTree.CloneTree(inspector);

            var sourcesList = inspector.Q<ListView>("FieldChainRoots");
            sourcesList.makeItem = visualTreeItem.CloneTree;

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
            var attachedObjectTransform = (target as BoneArrayRotationInfluence).transform;
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
            var chainRoots = serializedObject.FindProperty(nameof(BoneArrayRotationInfluence.ChainRoots));
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
            var chainRoots = serializedObject.FindProperty(nameof(BoneArrayRotationInfluence.ChainRoots));
            for (var i = 0; i < chainRoots.arraySize; ++i)
            {
                var currentItem = chainRoots.GetArrayElementAtIndex(i);
                currentItem.FindPropertyRelative(nameof(RotationInfluence.Influence)).floatValue = influenceValue;
            }
            serializedObject.ApplyModifiedProperties();
        }
    }
}
