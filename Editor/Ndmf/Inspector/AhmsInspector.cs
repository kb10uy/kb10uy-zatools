using UnityEngine;
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
        private HelpBox _warningHelpBox;

        protected override VisualElement CreateInspectorGUIImpl()
        {
            var visualTree = ZatoolsResources.LoadVisualTreeByGuid("f357496904123fa43985fe71a6eb2490");

            var inspector = visualTree.CloneTree();
            ZatoolsLocalization.UILocalizer.ApplyLocalizationFor(inspector);
            inspector.Bind(serializedObject);

            var maskTexture = inspector.Q<ObjectField>("FieldMask");
            maskTexture.RegisterValueChangedCallback((e) => OnMaskChanged(e.newValue as Texture2D));
            _warningHelpBox = inspector.Q<HelpBox>("MaskTextureWarning");

            OnMaskChanged(maskTexture.value as Texture2D);

            return inspector;
        }

        private void OnMaskChanged(Texture2D texture)
        {
            _warningHelpBox.style.display = texture != null && !texture.isReadable ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
