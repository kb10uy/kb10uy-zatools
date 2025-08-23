using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using nadena.dev.ndmf.localization;
using System.Reflection;

namespace KusakaFactory.Zatools.Localization
{
    internal sealed class UIElementLocalizer
    {
        private static Dictionary<Type, Func<VisualElement, Action>> ElementTypedLocalizationActionCache = new Dictionary<Type, Func<VisualElement, Action>>();
        private readonly Localizer _ndmfLocalizer;

        internal UIElementLocalizer(Localizer ndmfLocalizer)
        {
            _ndmfLocalizer = ndmfLocalizer;
        }

        internal void ApplyLocalizationFor(VisualElement element, bool registerCallback = true)
        {
            TraverseAndLocalize(element, registerCallback);
            LanguagePrefs.ApplyFontPreferences(element);
        }

        private void TraverseAndLocalize(VisualElement element, bool registerCallback = true)
        {
            if (element.ClassListContains("ndmf-tr"))
            {
                var operationForType = GetLocalizationOperationForType(element.GetType());
                if (operationForType != null)
                {
                    var updateElement = operationForType(element);
                    if (registerCallback) LanguagePrefs.RegisterLanguageChangeCallback(element, (e) => updateElement());
                    updateElement();
                }
            }

            foreach (var child in element.Children()) TraverseAndLocalize(child, registerCallback);
        }

        private Func<VisualElement, Action> GetLocalizationOperationForType(Type elementType)
        {
            if (ElementTypedLocalizationActionCache.TryGetValue(elementType, out var cachedOperation)) return cachedOperation;

            Func<VisualElement, Action> operation = null;
            // DropdownField とかは label と text 両方あるけど前者しか Set できない
            PropertyInfo labelProperty = elementType.GetProperty("label") ?? elementType.GetProperty("text");
            if (labelProperty != null)
            {
                operation = (element) =>
                {
                    var key = labelProperty.GetValue(element) as string;
                    return key != null ? () =>
                    {
                        var localizedLabel = _ndmfLocalizer.GetLocalizedString(key);
                        var localizedTooltip = _ndmfLocalizer.TryGetLocalizedString($"{key}:tooltip", out var tooltipText) ? tooltipText : null;
                        labelProperty.SetValue(element, localizedLabel);
                        element.tooltip = localizedTooltip;
                    }
                    : () => { };
                };
            }
            ElementTypedLocalizationActionCache[elementType] = operation;
            return operation;
        }
    }
}
