using System.Linq;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using KusakaFactory.Zatools.Localization;
using KusakaFactory.Zatools.Runtime;
using nadena.dev.ndmf.runtime;

namespace KusakaFactory.Zatools.Ndmf.Inspector
{
    [CustomEditor(typeof(GlobalWriteDefaultsOverride))]
    internal sealed class GwdoInspector : ZatoolsInspector
    {
        protected override VisualElement CreateInspectorGUIImpl()
        {
            var visualTree = ZatoolsResources.LoadVisualTreeByGuid("799012ca8b393e74d8561f49f6c0e9f5");

            var inspector = visualTree.CloneTree();
            ZatoolsLocalization.UILocalizer.ApplyLocalizationFor(inspector);
            inspector.Bind(serializedObject);

            var component = target as GlobalWriteDefaultsOverride;
            var placementWarning = inspector.Q<HelpBox>("PlacementWarning");
            void RefreshPlacementWarning()
            {
                if (component == null) return;
                SetDisplayed(placementWarning, !IsPlacedProperly(component));
            }
            RefreshPlacementWarning();

            placementWarning.RegisterCallback<AttachToPanelEvent>((_) => EditorApplication.hierarchyChanged += RefreshPlacementWarning);
            placementWarning.RegisterCallback<DetachFromPanelEvent>((_) => EditorApplication.hierarchyChanged -= RefreshPlacementWarning);

            return inspector;
        }

        private static bool IsPlacedProperly(GlobalWriteDefaultsOverride component)
        {
            var avatarRoot = RuntimeUtil.FindAvatarInParents(component.transform);
            if (avatarRoot != component.transform) return false;
            return !avatarRoot.GetComponentsInChildren<GlobalWriteDefaultsOverride>().Any((c) => c != component);
        }
    }
}
