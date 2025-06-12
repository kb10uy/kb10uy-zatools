using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using KusakaFactory.Zatools.Localization;
using KusakaFactory.Zatools.Runtime;

namespace KusakaFactory.Zatools.Ndmf.Inspector
{
    [CustomEditor(typeof(AvatarStatusValidatorIgnoredArmature))]
    internal sealed class AsvIgnoredArmatureInspector : ZatoolInspectorBase
    {
        protected override VisualElement CreateInspectorGUIImpl()
        {
            var visualTree = ZatoolsResources.LoadVisualTreeByGuid("3c4f3df91aae55345ae01e667d8a056f");

            var inspector = visualTree.CloneTree();
            ZatoolsLocalization.UILocalizer.ApplyLocalizationFor(inspector);
            inspector.Bind(serializedObject);

            return inspector;
        }
    }
}
