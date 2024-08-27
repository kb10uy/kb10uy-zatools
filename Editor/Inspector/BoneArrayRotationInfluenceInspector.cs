#if KZT_NDMF

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

            var replaceButton = inspector.Q<Button>("ButtonReplaceWithChildren");
            replaceButton.clickable.clicked += ReplaceWithChildren;

            return inspector;
        }

        private void ReplaceWithChildren()
        {
            var attachedObjectTransform = (target as BoneArrayRotationInfluence).transform;
            serializedObject.Update();

            var chainRoots = serializedObject.FindProperty(nameof(BoneArrayRotationInfluence.ChainRoots));
            chainRoots.ClearArray();
            for (var i = 0; i < attachedObjectTransform.childCount; ++i)
            {
                var root = attachedObjectTransform.GetChild(i);
                chainRoots.InsertArrayElementAtIndex(i);

                var newItem = chainRoots.GetArrayElementAtIndex(i);
                newItem.FindPropertyRelative(nameof(RotationInfluence.Root)).objectReferenceValue = root;
                newItem.FindPropertyRelative(nameof(RotationInfluence.Influence)).floatValue = 1.0f;
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}

#endif
