using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using KusakaFactory.Zatools.Localization;

namespace KusakaFactory.Zatools.Inspector.EnhancedEyePointerInstaller
{
    [CustomEditor(typeof(Runtime.EnhancedEyePointerInstaller))]
    internal sealed class EnhancedEyePointerInstallerInspector : ZatoolEditorBase
    {
        protected override VisualElement CreateInspectorGUIImpl()
        {
            var visualTree = Resources.LoadVisualTreeByGuid("67cccac4d1dc9ee438b9d25aad817391");

            var inspector = visualTree.CloneTree();
            ZatoolLocalization.UILocalizer.ApplyLocalizationFor(inspector);
            inspector.Bind(serializedObject);

            // TODO: 実装したら AaC の存在チェックで制御する
            var adaptedFXLayer = inspector.Q<PropertyField>("FieldAdaptedFXLayer");
            adaptedFXLayer.SetEnabled(false);

            return inspector;
        }
    }
}
