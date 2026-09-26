using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using KusakaFactory.Zatools.Localization;
using KusakaFactory.Zatools.Runtime;

namespace KusakaFactory.Zatools.Ndmf.Inspector
{
    [CustomEditor(typeof(UvTileMapDistribution))]
    internal sealed class UtmdInspector : ZatoolsInspector
    {
        protected override VisualElement CreateInspectorGUIImpl()
        {
            var visualTree = ZatoolsResources.LoadVisualTreeByGuid("fc07b878ef0b41b4e9f5926de7593592");

            var inspector = visualTree.CloneTree();
            ZatoolsLocalization.UILocalizer.ApplyLocalizationFor(inspector);
            inspector.Bind(serializedObject);

            var component = target as UvTileMapDistribution;
            var missingTileMapWarning = inspector.Q<HelpBox>("MissingTileMapWarning");
            void RefreshWarning()
            {
                if (component == null) return;
                SetDisplayed(missingTileMapWarning, component.TileMap == null);
            }
            RefreshWarning();

            missingTileMapWarning.TrackPropertyValue(
                serializedObject.FindProperty(nameof(UvTileMapDistribution.TileMap)),
                (_) => RefreshWarning()
            );

            return inspector;
        }
    }
}
