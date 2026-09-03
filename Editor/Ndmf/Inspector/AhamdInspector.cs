using System.Collections.Generic;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using KusakaFactory.Zatools.Localization;
using KusakaFactory.Zatools.Runtime;

namespace KusakaFactory.Zatools.Ndmf.Inspector
{
    [CustomEditor(typeof(AdHocAdvancedMeshDuplication))]
    internal sealed class AhamdInspector : ZatoolsInspector
    {
        protected override VisualElement CreateInspectorGUIImpl()
        {
            var visualTree = ZatoolsResources.LoadVisualTreeByGuid("ad33420e2dd38ae4387ec5cf5c1cf2e1");
            var visualTreeItem = ZatoolsResources.LoadVisualTreeByGuid("0b75f8a0021b770e5ac6bbaad0d2665b");

            var inspector = visualTree.CloneTree();
            ZatoolsLocalization.UILocalizer.ApplyLocalizationFor(inspector);
            inspector.Bind(serializedObject);

            var modificationsList = inspector.Q<ListView>("FieldModifications");
            modificationsList.makeItem = () => MakeModificationItem(visualTreeItem);
            modificationsList.itemsAdded += ResetAddedModifications;

            return inspector;
        }

        private VisualElement MakeModificationItem(VisualTreeAsset visualTreeItem)
        {
            var item = visualTreeItem.CloneTree();
            ZatoolsLocalization.UILocalizer.ApplyLocalizationFor(item);
            return item;
        }

        private void ResetAddedModifications(IEnumerable<int> indices)
        {
            var modifications = serializedObject.FindProperty(nameof(AdHocAdvancedMeshDuplication.Modifications));
            foreach (var index in indices)
            {
                modifications.GetArrayElementAtIndex(index).boxedValue = new AhamdModificationStep();
            }
            serializedObject.ApplyModifiedProperties();
        }
    }
}
