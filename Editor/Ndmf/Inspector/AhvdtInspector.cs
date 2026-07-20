using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using KusakaFactory.Zatools.Localization;
using KusakaFactory.Zatools.Runtime;
using UnityEngine;

namespace KusakaFactory.Zatools.Ndmf.Inspector
{
    [CustomEditor(typeof(AdHocVertexDataTransfer))]
    internal sealed class AhvdtInspector : ZatoolsInspector
    {
        private Vector3Field _constantVector3;
        private FloatField _constantFloat;

        protected override VisualElement CreateInspectorGUIImpl()
        {
            var visualTree = ZatoolsResources.LoadVisualTreeByGuid("dab24cb0a8936824a88677907edc3538");

            var inspector = visualTree.CloneTree();
            ZatoolsLocalization.UILocalizer.ApplyLocalizationFor(inspector);
            inspector.Bind(serializedObject);

            _constantVector3 = inspector.Q<Vector3Field>("FieldConstantVector3");
            _constantFloat = inspector.Q<FloatField>("FieldConstantFloat");

            var transferMode = inspector.Q<EnumField>("FieldTransferMode");
            transferMode.RegisterValueChangedCallback((e) =>
            {
                if (e.newValue != null) OnTransferModeChanged((VertexDataTransferMode)e.newValue);
            });
            OnTransferModeChanged((serializedObject.targetObject as AdHocVertexDataTransfer).TransferMode);

            return inspector;
        }

        private void OnTransferModeChanged(VertexDataTransferMode newMode)
        {
            var showVector3 = newMode switch
            {
                VertexDataTransferMode.ConstAndLuminance => true,
                VertexDataTransferMode.Const01AndLuminance => true,
                VertexDataTransferMode.LuminanceConstAndConst => true,
                VertexDataTransferMode.LuminanceConst01AndConst => true,
                _ => false,
            };
            var showFloat = newMode switch
            {
                VertexDataTransferMode.LuminanceConstAndConst => true,
                VertexDataTransferMode.LuminanceConst01AndConst => true,
                _ => false,
            };
            _constantVector3.style.display = showVector3 ? DisplayStyle.Flex : DisplayStyle.None;
            _constantFloat.style.display = showFloat ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
