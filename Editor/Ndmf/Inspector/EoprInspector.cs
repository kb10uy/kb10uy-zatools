using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using KusakaFactory.Zatools.Localization;
using KusakaFactory.Zatools.Runtime;

namespace KusakaFactory.Zatools.Ndmf.Inspector
{
    [CustomEditor(typeof(EditorOnlyPropertyReplicator))]
    internal sealed class EoprInspector : ZatoolInspectorBase
    {
        private VisualElement _blendShapeExclusion;

        protected override VisualElement CreateInspectorGUIImpl()
        {
            var visualTree = Resources.LoadVisualTreeByGuid("3a48b3217f1b1aa4aaf9012190ded919");

            var inspector = visualTree.CloneTree();
            ZatoolLocalization.UILocalizer.ApplyLocalizationFor(inspector);
            inspector.Bind(serializedObject);

            _blendShapeExclusion = inspector.Q<VisualElement>("BlendShapeExclusion");

            var enableBlendShape = inspector.Q<Toggle>("FieldEnableBlendShape");
            enableBlendShape.RegisterValueChangedCallback((e) => OnEnableBlendShapeChanged(e.newValue));
            OnEnableBlendShapeChanged(enableBlendShape.value);

            return inspector;
        }

        private void OnEnableBlendShapeChanged(bool newValue)
        {
            _blendShapeExclusion.style.display = newValue ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
