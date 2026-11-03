#if ZATOOLS_HAS_VRCSDK_FACE_TRACKING
using KusakaFactory.Zatools.Localization;
using KusakaFactory.Zatools.Ndmf.Core;

namespace KusakaFactory.Zatools.Ndmf
{
    internal static class FtdiDiagnosticEx
    {
        internal static string Localize(this FtdiDiagnostic diagnostic)
        {
            var message = ZatoolsLocalization.FormatLocalized(diagnostic.LocalizationKey, diagnostic.Arguments);
            if (diagnostic.Suggestion == null) return message;
            return message + ZatoolsLocalization.FormatLocalized("ftdi.diagnostic.suggestion", diagnostic.Suggestion);
        }
    }
}
#endif
