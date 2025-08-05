using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using KusakaFactory.Zatools.Localization;
using KusakaFactory.Zatools.Runtime;
using System;

namespace KusakaFactory.Zatools.Ndmf.Inspector
{
    [CustomEditor(typeof(GlobalMaterialPropertyOverride))]
    internal sealed class GmpoInspector : ZatoolInspector
    {
        private SerializedProperty _overridesProperty;

        protected override VisualElement CreateInspectorGUIImpl()
        {
            _overridesProperty = serializedObject.FindProperty("Overrides");

            var visualTree = ZatoolsResources.LoadVisualTreeByGuid("7e9d8840577c98149b99010fe631649b");
            var visualTreeItem = ZatoolsResources.LoadVisualTreeByGuid("10d4d6385c636c045996bb170d8f02bf");

            var inspector = visualTree.CloneTree();
            ZatoolsLocalization.UILocalizer.ApplyLocalizationFor(inspector);
            inspector.Bind(serializedObject);

            var overridesList = inspector.Q<ListView>("FieldOverrides");
            overridesList.makeItem = () => MakeOverrideItem(visualTreeItem);
            overridesList.bindItem = OnItemBound;
            overridesList.unbindItem = OnItemUnbound;

            return inspector;
        }

        private static VisualElement MakeOverrideItem(VisualTreeAsset visualTreeItem)
        {
            var item = visualTreeItem.CloneTree();
            ZatoolsLocalization.UILocalizer.ApplyLocalizationFor(item);
            return item;
        }

        private void OnItemBound(VisualElement itemElement, int index)
        {
            var nameField = itemElement.Q<TextField>("FieldName");
            var typeField = itemElement.Q<EnumField>("FieldTargetType");
            var floatField = itemElement.Q<FloatField>("FieldFloatValue");
            var intField = itemElement.Q<IntegerField>("FieldIntValue");
            var vectorField = itemElement.Q<Vector4Field>("FieldVectorValue");

            var overrideProperty = _overridesProperty.GetArrayElementAtIndex(index);
            var nameProperty = overrideProperty.FindPropertyRelative("Name");
            var targetTypeProperty = overrideProperty.FindPropertyRelative("TargetType");
            var floatValueProperty = overrideProperty.FindPropertyRelative("FloatValue");
            var intValueProperty = overrideProperty.FindPropertyRelative("IntValue");
            var vectorValueProperty = overrideProperty.FindPropertyRelative("VectorValue");

            nameField.BindProperty(nameProperty);
            typeField.BindProperty(targetTypeProperty);
            floatField.BindProperty(floatValueProperty);
            intField.BindProperty(intValueProperty);
            vectorField.BindProperty(vectorValueProperty);

            EventCallback<ChangeEvent<Enum>> changeCallback = (e) =>
            {
                if (e.previousValue != e.newValue)
                {
                    floatField.value = 0.0f;
                    intField.value = 0;
                    vectorField.value = Vector4.zero;
                }
                UpdateFieldView(typeField, floatField, intField, vectorField);
            };
            typeField.RegisterValueChangedCallback(changeCallback);
            typeField.userData = changeCallback;
            UpdateFieldView(typeField, floatField, intField, vectorField);
        }

        private void OnItemUnbound(VisualElement itemElement, int index)
        {
            var nameField = itemElement.Q<TextField>("FieldName");
            var typeField = itemElement.Q<EnumField>("FieldTargetType");
            var floatField = itemElement.Q<FloatField>("FieldFloatValue");
            var intField = itemElement.Q<IntegerField>("FieldIntValue");
            var vectorField = itemElement.Q<Vector4Field>("FieldVectorValue");

            var changeCallback = typeField.userData as EventCallback<ChangeEvent<Enum>>;
            typeField.UnregisterValueChangedCallback(changeCallback);

            nameField.Unbind();
            typeField.Unbind();
            floatField.Unbind();
            intField.Unbind();
            vectorField.Unbind();
        }

        private static void UpdateFieldView(
            EnumField typeField,
            FloatField floatField,
            IntegerField intField,
            Vector4Field vectorField
        )
        {
            var currentType = (MaterialPropertyOverrideType)typeField.value;
            floatField.style.display = currentType == MaterialPropertyOverrideType.Float ? DisplayStyle.Flex : DisplayStyle.None;
            intField.style.display = currentType == MaterialPropertyOverrideType.Int ? DisplayStyle.Flex : DisplayStyle.None;
            vectorField.style.display = currentType == MaterialPropertyOverrideType.Vector ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
