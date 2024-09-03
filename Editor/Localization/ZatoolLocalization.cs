using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using UnityEditor;
using nadena.dev.ndmf.localization;

namespace KusakaFactory.Zatools.Localization
{
    [InitializeOnLoad]
    internal static class ZatoolLocalization
    {
        internal static readonly ImmutableList<string> SupportedLanguages = ImmutableList<string>.Empty
            .Add("en-us")
            .Add("ja-jp");

        internal static event Action OnNdmfLanguageChanged;

        internal static Localizer NdmfLocalizer { get; private set; }

        internal static UIElementLocalizer UILocalizer { get; private set; }

        private static Dictionary<string, ImmutableDictionary<string, string>> StringTableCache = new Dictionary<string, ImmutableDictionary<string, string>>();

        static ZatoolLocalization()
        {
            NdmfLocalizer = new Localizer(SupportedLanguages[0], () => SupportedLanguages.Select((l) =>
            {
                var stringTable = LoadStringTableForLanguage(l);
                Func<string, string> fetcher = (key) => stringTable.GetValueOrDefault(key);
                return (l, fetcher);
            }).ToList());
            UILocalizer = new UIElementLocalizer(NdmfLocalizer);
            LanguagePrefs.RegisterLanguageChangeCallback(typeof(ZatoolLocalization), (_) => OnNdmfLanguageChanged?.Invoke());
        }

        internal static string GetStringTableJsonPath(string languageCode) => $"Packages/org.kb10uy.zatools/Resources/Localizations/{languageCode}.json";

        internal static ImmutableDictionary<string, string> LoadStringTableForLanguage(string languageCode)
        {
            if (StringTableCache.TryGetValue(languageCode, out var cached))
            {
                return cached;
            }

            TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(GetStringTableJsonPath(languageCode));
            var stringTable = asset != null ? JsonConvert.DeserializeObject<Dictionary<string, string>>(asset.text) : new Dictionary<string, string>();
            var immutableTable = ImmutableDictionary.CreateRange(stringTable);
            StringTableCache.Add(languageCode, immutableTable);
            return immutableTable;
        }

        [MenuItem("Tools/kb10uy's Various Tools/Reload Localizations")]
        internal static void Invalidate()
        {
            Localizer.ReloadLocalizations();
            StringTableCache.Clear();
        }
    }
}
