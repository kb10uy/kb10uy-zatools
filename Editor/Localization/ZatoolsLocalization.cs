using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using nadena.dev.ndmf.localization;

namespace KusakaFactory.Zatools.Localization
{
    [InitializeOnLoad]
    internal static class ZatoolsLocalization
    {
        internal static readonly ImmutableList<string> SupportedLanguages = ImmutableList<string>.Empty
            .Add("en-us")
            .Add("ja-jp");

        internal static readonly ImmutableDictionary<string, string> StringTableGuids = ImmutableDictionary<string, string>.Empty
            .Add("en-us", "7d0e5e80e15dbb54a8d70bb23a19949d")
            .Add("ja-jp", "262c402765bc89847879905644d0d439");

        internal static event Action OnNdmfLanguageChanged;

        internal static Localizer NdmfLocalizer { get; private set; }

        internal static UIElementLocalizer UILocalizer { get; private set; }

        private static Dictionary<string, ImmutableDictionary<string, string>> StringTableCache = new Dictionary<string, ImmutableDictionary<string, string>>();

        static ZatoolsLocalization()
        {
            NdmfLocalizer = new Localizer(SupportedLanguages[0], () => SupportedLanguages.Select((l) =>
            {
                var stringTable = LoadStringTableForLanguage(l);
                Func<string, string> fetcher = (key) => stringTable.GetValueOrDefault(key);
                return (l, fetcher);
            }).ToList());
            UILocalizer = new UIElementLocalizer(NdmfLocalizer);
            LanguagePrefs.RegisterLanguageChangeCallback(typeof(ZatoolsLocalization), (_) => OnNdmfLanguageChanged?.Invoke());
        }

        internal static ImmutableDictionary<string, string> LoadStringTableForLanguage(string languageCode)
        {
            if (StringTableCache.TryGetValue(languageCode, out var cached))
            {
                return cached;
            }

            var stringTableText = ZatoolsResources.LoadTextAssetByGuid(StringTableGuids[languageCode]);
            var stringTable = stringTableText != null ?
                JsonConvert.DeserializeObject<Dictionary<string, string>>(stringTableText) :
                new Dictionary<string, string>();
            var immutableTable = ImmutableDictionary.CreateRange(stringTable);
            StringTableCache.Add(languageCode, immutableTable);
            return immutableTable;
        }

        [MenuItem("Tools/kb10uy's Various Tools/Debug/Reload Localizations")]
        internal static void Invalidate()
        {
            StringTableCache.Clear();
            Localizer.ReloadLocalizations();
        }
    }
}
