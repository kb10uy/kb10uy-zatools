using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using nadena.dev.ndmf.localization;

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

        internal void ApplyLocalizationFor(VisualElement element)
        {
            TraverseAndLocalize(element);
            LanguagePrefs.ApplyFontPreferences(element);
        }

        private void TraverseAndLocalize(VisualElement element)
        {
            var elementType = element.GetType();
            if (element.ClassListContains("ndmf-tr"))
            {
                var localizationOperation = GetLocalizationOperation(elementType);
                if (localizationOperation != null)
                {
                    var updater = localizationOperation(element);
                    LanguagePrefs.RegisterLanguageChangeCallback(element, (e) => updater());
                    updater();
                }
            }

            foreach (var child in element.Children()) TraverseAndLocalize(child);
        }

        private Func<VisualElement, Action> GetLocalizationOperation(Type elementType)
        {
            if (!ElementTypedLocalizationActionCache.TryGetValue(elementType, out var updater))
            {
                var labelProperty = elementType.GetProperty("text") ?? elementType.GetProperty("label");
                if (labelProperty == null)
                {
                    updater = null;
                }
                else
                {
                    updater = (element) =>
                    {
                        var key = labelProperty.GetValue(element) as string;
                        if (key != null)
                        {
                            return () =>
                            {
                                var localizedLabel = _ndmfLocalizer.GetLocalizedString(key);
                                var localizedTooltip = _ndmfLocalizer.TryGetLocalizedString($"{key}:tooltip", out var tooltipText) ? tooltipText : null;
                                labelProperty.SetValue(element, localizedLabel);
                                element.tooltip = localizedTooltip;
                            };
                        }
                        else
                        {
                            return () => { };
                        }
                    };
                }

                ElementTypedLocalizationActionCache[elementType] = updater;
            }

            return updater;
        }
    }
}
