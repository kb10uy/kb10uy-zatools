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

            var inspector = visualTree.CloneTree();
            ZatoolsLocalization.UILocalizer.ApplyLocalizationFor(inspector);
            inspector.Bind(serializedObject);

            return inspector;
        }
    }
}
