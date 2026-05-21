using System;
using System.Collections.Immutable;
using System.Linq;
using UnityEngine;
using UnityEditor;
using nadena.dev.ndmf.localization;

namespace KusakaFactory.Zatools.Localization
{
    internal static class ZatoolsLocalization
    {
        internal static Localizer NdmfLocalizer
        {
            get
            {
                EnsureLocalizerIntialized();
                return _ndmfLocalizer;
            }
        }

        internal static UIElementLocalizer UILocalizer
        {
            get
            {
                EnsureLocalizerIntialized();
                return _uiLocalizer;
            }
        }

        internal static event Action OnNdmfLanguageChanged;

        private static Localizer _ndmfLocalizer = null;
        private static UIElementLocalizer _uiLocalizer = null;

        private static readonly ImmutableList<string> SupportedLanguages = ImmutableList<string>.Empty
            .Add("en-us")
            .Add("ja-jp");
        private static readonly ImmutableDictionary<string, string> LocalizationAssetGuids = ImmutableDictionary<string, string>.Empty
            .Add("en-us", "42e06d554b5d0ed41adee8cd05f68b25")
            .Add("ja-jp", "b67eaa55d7ce15a479bdad858d85dfe8");

        private static void EnsureLocalizerIntialized()
        {
            if (_ndmfLocalizer != null) return;

            _ndmfLocalizer = new Localizer(SupportedLanguages[0], () =>
            {
                var paths = LocalizationAssetGuids.Select((p) => AssetDatabase.GUIDToAssetPath(p.Value));
                return paths.Select((p) => AssetDatabase.LoadAssetAtPath<LocalizationAsset>(p)).ToList();
            });
            _uiLocalizer = new UIElementLocalizer(NdmfLocalizer);
            LanguagePrefs.RegisterLanguageChangeCallback(typeof(ZatoolsLocalization), (_) => OnNdmfLanguageChanged?.Invoke());
        }
    }
}
