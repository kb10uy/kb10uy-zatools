using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using nadena.dev.ndmf.localization;

namespace KusakaFactory.Zatools.Localization
{
    internal sealed class UIElementLocalizer
    {
        private Localizer _ndmfLocalizer;
        private Dictionary<Type, Func<VisualElement, Action>> _elementTypedLocalizationActionCache = new Dictionary<Type, Func<VisualElement, Action>>();

        internal UIElementLocalizer(Localizer ndmfLocalizer)
        {
            _ndmfLocalizer = ndmfLocalizer;
        }

        internal void ApplyLocalizationFor(VisualElement element)
        {
            TraverseAndLocalize(element);
            LanguagePrefs.ApplyFontPreferences(element);
        }

        private void TraverseAndLocalize(VisualElement element)
        {
            if (element.ClassListContains("ndmf-tr"))
            {
                var localizationOperation = GetLocalizationOperation(element.GetType());
                if (localizationOperation != null)
                {
                    var updateLocalization = localizationOperation(element);
                    LanguagePrefs.RegisterLanguageChangeCallback(element, (e) => updateLocalization());
                    updateLocalization();
                }
            }

            foreach (var child in element.Children()) TraverseAndLocalize(child);
        }

        private Func<VisualElement, Action> GetLocalizationOperation(Type elementType)
        {
            if (_elementTypedLocalizationActionCache.TryGetValue(elementType, out var action)) return action;

            Func<VisualElement, Action> registeredAction = null;

            var labelProperty = elementType.GetProperty("text") ?? elementType.GetProperty("label");
            if (labelProperty != null)
            {
                registeredAction = (element) =>
                {
                    var key = labelProperty.GetValue(element) as string;
                    Action updater = key != null ? () =>
                    {
                        var localizedLabel = _ndmfLocalizer.GetLocalizedString(key);
                        var localizedTooltip = _ndmfLocalizer.TryGetLocalizedString($"{key}:tooltip", out var tooltipText) ? tooltipText : null;
                        labelProperty.SetValue(element, localizedLabel);
                        element.tooltip = localizedTooltip;
                    }
                    : () => { };
                    return updater;
                };
                _elementTypedLocalizationActionCache[elementType] = registeredAction;
            }

            return registeredAction;
        }
    }
}
