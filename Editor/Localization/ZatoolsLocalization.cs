using System;
using System.Collections.Immutable;
using System.Linq;
using UnityEngine;
using UnityEditor;
using nadena.dev.ndmf.localization;
using KusakaFactory.Zatools.Foundation.Arithmetic;
using KusakaFactory.Zatools.Ndmf.Core;

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

        internal static string LocalizeZaxDiagnostic(ZaxDiagnostic diagnostic)
        {
            return FormatLocalized(diagnostic.LocalizationKey, diagnostic.Arguments);
        }

#if ZATOOLS_HAS_VRCSDK_FACE_TRACKING
        internal static string LocalizeFtdiDiagnostic(FtdiDiagnostic diagnostic)
        {
            var message = FormatLocalized(diagnostic.LocalizationKey, diagnostic.Arguments);
            if (diagnostic.Suggestion == null) return message;
            return message + FormatLocalized("ftdi.diagnostic.suggestion", diagnostic.Suggestion);
        }
#endif

        internal static string FormatLocalized(string key, params string[] arguments)
        {
            var format = NdmfLocalizer.GetLocalizedString(key);
            if (arguments.Length == 0) return format;

            try
            {
                return string.Format(format, arguments);
            }
            catch (FormatException)
            {
                return $"{format} [{string.Join(", ", arguments)}]";
            }
        }

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
