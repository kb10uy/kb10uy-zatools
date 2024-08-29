#if KZT_NDMF

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json;
using nadena.dev.ndmf.localization;

namespace KusakaFactory.Zatools
{
    internal static class ZatoolNdmfLocalizer
    {
        private static readonly Dictionary<string, Dictionary<string, string>> LocalizationObjectCache = new Dictionary<string, Dictionary<string, string>>();

        internal static Localizer GetLocalizer()
        {
            var localizations = new List<(string, Func<string, string>)>
            {
                ("en-us", CreateLocalizerFunc("en-us")),
                ("ja-jp", CreateLocalizerFunc("ja-jp")),
            };
            return new Localizer("en-us", () => localizations);
        }

        private static Func<string, string> CreateLocalizerFunc(string locale)
        {
            var localization = GetLocalizationObject(locale);
            return (key) => localization.TryGetValue(key, out var value) ? value : null;
        }

        private static Dictionary<string, string> GetLocalizationObject(string locale)
        {
            if (LocalizationObjectCache.TryGetValue(locale, out var cached))
            {
                return cached;
            }

            TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>($"Packages/org.kb10uy.zatools/Resources/Localizations/{locale}.json");
            var localizationObject = asset != null ? JsonConvert.DeserializeObject<Dictionary<string, string>>(asset.text) : new Dictionary<string, string>();
            LocalizationObjectCache.Add(locale, localizationObject);
            return localizationObject;
        }
    }
}

#endif
