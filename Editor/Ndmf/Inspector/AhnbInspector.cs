using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using KusakaFactory.Zatools.Localization;
using KusakaFactory.Zatools.Runtime;

namespace KusakaFactory.Zatools.Ndmf.Inspector
{
    [CustomEditor(typeof(AdHocNormalBending))]
    internal sealed class AhnbInspector : ZatoolsInspector
    {
        private HelpBox _warningHelpBox;

        protected override VisualElement CreateInspectorGUIImpl()
        {
            var visualTree = ZatoolsResources.LoadVisualTreeByGuid("795ec2b0279d5e246a72591ba48a4cef");

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
