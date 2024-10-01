using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using KusakaFactory.Zatools.Localization;

namespace KusakaFactory.Zatools.Inspector.UVIntegerShift
{
    [CustomEditor(typeof(Runtime.UVIntegerShift))]
    internal sealed class UVIntegerShiftInspector : ZatoolEditorBase
    {
        protected override VisualElement CreateInspectorGUIImpl()
        {
            var visualTree = Resources.LoadVisualTreeByGuid("92a3c71d95afc8a408cb31cdf89304e6");

            var inspector = visualTree.CloneTree();
            ZatoolLocalization.UILocalizer.ApplyLocalizationFor(inspector);
            inspector.Bind(serializedObject);

            return inspector;
        }
    }
}
