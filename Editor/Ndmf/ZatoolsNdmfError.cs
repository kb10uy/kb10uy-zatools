using System.Linq;
using UnityEngine;
using nadena.dev.ndmf;
using nadena.dev.ndmf.localization;
using KusakaFactory.Zatools.Localization;

namespace KusakaFactory.Zatools.Ndmf
{
    internal sealed class ZatoolsNdmfError : SimpleError
    {
        public override Localizer Localizer => ZatoolsLocalization.NdmfLocalizer;
        public override ErrorSeverity Severity => _severity;
        public override string TitleKey => _titleKey;
        public override string DetailsKey => $"{_titleKey}:description";
        public override string[] DetailsSubst => _descriptionInterpolations;

        private readonly ErrorSeverity _severity;
        private readonly string _titleKey;
        private readonly string[] _descriptionInterpolations;

        public ZatoolsNdmfError(ErrorSeverity severity, string key, params object[] descArgs)
        {
            _titleKey = key;
            _severity = severity;
            _descriptionInterpolations = descArgs.Select((x) => x.ToString()).ToArray();
        }

        public ZatoolsNdmfError(Object target, ErrorSeverity severity, string key, params object[] descArgs)
        {
            _titleKey = key;
            _severity = severity;
            _descriptionInterpolations = descArgs.Select((x) => x.ToString()).ToArray();
            AddReference(ObjectRegistry.GetReference(target));
        }
    }
}
