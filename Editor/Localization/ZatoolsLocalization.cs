using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using nadena.dev.ndmf.localization;
using UnityEngine;

namespace KusakaFactory.Zatools.Localization
{
    [InitializeOnLoad]
    internal static class ZatoolsLocalization
    {
        internal static readonly ImmutableList<string> SupportedLanguages = ImmutableList<string>.Empty
            .Add("en-us")
            .Add("ja-jp");

        internal static readonly ImmutableDictionary<string, string> LocalizationAssetGuids = ImmutableDictionary<string, string>.Empty
            .Add("en-us", "42e06d554b5d0ed41adee8cd05f68b25")
            .Add("ja-jp", "b67eaa55d7ce15a479bdad858d85dfe8");

        internal static event Action OnNdmfLanguageChanged;

        internal static Localizer NdmfLocalizer { get; private set; }

        internal static UIElementLocalizer UILocalizer { get; private set; }

        private static Dictionary<string, ImmutableDictionary<string, string>> StringTableCache = new Dictionary<string, ImmutableDictionary<string, string>>();

        static ZatoolsLocalization()
        {
            NdmfLocalizer = new Localizer(SupportedLanguages[0], () =>
            {
                var paths = LocalizationAssetGuids.Select((p) => AssetDatabase.GUIDToAssetPath(p.Value));
                return paths.Select((p) => AssetDatabase.LoadAssetAtPath<LocalizationAsset>(p)).ToList();
            });
            UILocalizer = new UIElementLocalizer(NdmfLocalizer);
            LanguagePrefs.RegisterLanguageChangeCallback(typeof(ZatoolsLocalization), (_) => OnNdmfLanguageChanged?.Invoke());
        }

        [MenuItem("Tools/Zatools: kb10uy's Various Tools/Debug/Reload Localizations")]
        internal static void Invalidate()
        {
            StringTableCache.Clear();
            Localizer.ReloadLocalizations();
        }
    }
}
