using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using KusakaFactory.Zatools.Localization;
using KusakaFactory.Zatools.Runtime;

namespace KusakaFactory.Zatools.Ndmf.Inspector
{
    [CustomEditor(typeof(AdHocMeshSplit))]
    internal sealed class AhmsInspector : ZatoolsInspector
    {
        protected override VisualElement CreateInspectorGUIImpl()
        {
            var visualTree = ZatoolsResources.LoadVisualTreeByGuid("f357496904123fa43985fe71a6eb2490");

            var inspector = visualTree.CloneTree();
            ZatoolsLocalization.UILocalizer.ApplyLocalizationFor(inspector);
            inspector.Bind(serializedObject);

            var component = target as AdHocMeshSplit;
            var missingSplitMaterialWarning = inspector.Q<HelpBox>("MissingSplitMaterialWarning");
            void RefreshWarning()
            {
                if (component == null) return;
                SetDisplayed(missingSplitMaterialWarning, component.SplitMaterial == null);
            }
            RefreshWarning();

            missingSplitMaterialWarning.TrackPropertyValue(
                serializedObject.FindProperty(nameof(AdHocMeshSplit.SplitMaterial)),
                (_) => RefreshWarning()
            );

            return inspector;
        }
    }
}
