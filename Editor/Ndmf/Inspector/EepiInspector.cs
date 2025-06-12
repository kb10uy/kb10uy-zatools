using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using KusakaFactory.Zatools.Localization;
using KusakaFactory.Zatools.Runtime;

namespace KusakaFactory.Zatools.Ndmf.Inspector
{
    [CustomEditor(typeof(EnhancedEyePointerInstaller))]
    internal sealed class EepiInspector : ZatoolInspectorBase
    {
        protected override VisualElement CreateInspectorGUIImpl()
        {
            var visualTree = ZatoolsResources.LoadVisualTreeByGuid("67cccac4d1dc9ee438b9d25aad817391");

            var inspector = visualTree.CloneTree();
            ZatoolsLocalization.UILocalizer.ApplyLocalizationFor(inspector);
            inspector.Bind(serializedObject);

            var overrideGlobalWeight = inspector.Q<Toggle>("FieldOverrideGlobalWeight");
            var globalWeightGroup = inspector.Q<VisualElement>("GlobalWeightGroup");
            overrideGlobalWeight.RegisterValueChangedCallback((e) => globalWeightGroup.SetEnabled(e.newValue));
            globalWeightGroup.SetEnabled(overrideGlobalWeight.value);

            return inspector;
        }
    }
}
