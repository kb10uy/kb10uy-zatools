using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KusakaFactory.Zatools.Ndmf
{
    internal static class ZatoolsExtension
    {
        public static IEnumerable<Transform> EnumerateDirectChildren(this Transform parent)
        {
            return Enumerable.Range(0, parent.childCount).Select(parent.GetChild);
        }

#if ZATOOLS_HAS_VRCSDK_FACE_TRACKING
        internal static string Localize(this Core.FtdiDiagnostic diagnostic)
        {
            return Localization.ZatoolsLocalization.FormatLocalized(diagnostic.LocalizationKey, diagnostic.Arguments);
        }
#endif
    }
}
