using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using KusakaFactory.Zatools.Foundation.Arithmetic;
using KusakaFactory.Zatools.Localization;

namespace KusakaFactory.Zatools.Ndmf.Inspector
{
#if UNITY_6000_0_OR_NEWER
    [UxmlElement]
#endif
    internal sealed partial class ZatoolsZaxExpressionField : VisualElement
    {
#if !UNITY_6000_0_OR_NEWER
        public new class UxmlFactory : UxmlFactory<ZatoolsZaxExpressionField, UxmlTraits>
        {
        }

        public new class UxmlTraits : VisualElement.UxmlTraits
        {
            UxmlStringAttributeDescription _label = new UxmlStringAttributeDescription { name = "label" };
            UxmlStringAttributeDescription _bindingPath = new UxmlStringAttributeDescription { name = "binding-path" };

            public override IEnumerable<UxmlChildElementDescription> uxmlChildElementsDescription
            {
                get
                {
                    yield break;
                }
            }

            public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
            {
                base.Init(ve, bag, cc);
                var field = ve as ZatoolsZaxExpressionField;
                field.LabelKey = _label.GetValueFromBag(bag, cc);
                field.BindingPath = _bindingPath.GetValueFromBag(bag, cc);
            }
        }
#endif

#if UNITY_6000_0_OR_NEWER
        [UxmlAttribute("label")]
#endif
        private string LabelKey
        {
            get => _input.label;
            set
            {
                _input.label = value;
                _input.EnableInClassList("ndmf-tr", !string.IsNullOrEmpty(value));
            }
        }

#if UNITY_6000_0_OR_NEWER
        [UxmlAttribute("binding-path")]
#endif
        private string BindingPath
        {
            get => _input.bindingPath;
            set => _input.bindingPath = value;
        }

        private readonly TextField _input;
        private readonly Label _status;
        private readonly Label _variables;
        private readonly List<ZaxDiagnostic> _diagnostics = new List<ZaxDiagnostic>();
        private Func<ZaxValueType?> _expectedType;
        private ZaxVariable[] _declaredVariables = Array.Empty<ZaxVariable>();
        private bool _revalidated;
        private string _revalidatedSource;
        private ZaxValueType? _revalidatedExpectedType;

        internal ZaxProgram Program { get; private set; }

        public ZatoolsZaxExpressionField()
        {
            _input = new TextField { multiline = true, isDelayed = true };
            _input.AddToClassList("zax-expression__input");
            _status = new Label();
            _status.AddToClassList("zax-expression__status");
            _variables = new Label();
            _variables.AddToClassList("zax-expression__variables");
            _variables.style.display = DisplayStyle.None;

            Add(_input);
            Add(_status);
            Add(_variables);

            _input.RegisterValueChangedCallback((e) => Revalidate());
            _input.RegisterCallback<SerializedPropertyChangeEvent>((e) => Revalidate());
            _input.RegisterCallback<FocusOutEvent>((e) => Revalidate());
            RegisterCallback<AttachToPanelEvent>((e) =>
            {
                ZatoolsLocalization.OnNdmfLanguageChanged += RevalidateForced;
                RevalidateForced();
                schedule.Execute(Revalidate);
            });
            RegisterCallback<DetachFromPanelEvent>((e) => ZatoolsLocalization.OnNdmfLanguageChanged -= RevalidateForced);
        }

        internal void Configure(ZaxValueType? expectedType, IReadOnlyList<ZaxVariable> variables, bool showVariables = true)
        {
            Configure(() => expectedType, variables, showVariables);
        }

        internal void Configure(Func<ZaxValueType?> expectedType, IReadOnlyList<ZaxVariable> variables, bool showVariables = true)
        {
            _expectedType = expectedType;
            _declaredVariables = variables != null ? variables.ToArray() : Array.Empty<ZaxVariable>();

            _variables.text = string.Join("\n", _declaredVariables.Select((v) => $"@{v.Name}: {v.Type.DisplayName()}"));
            _variables.style.display = showVariables && _declaredVariables.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;

            RevalidateForced();
        }

        internal void RequestRevalidate()
        {
            Revalidate();
        }

        private void Revalidate()
        {
            if (_revalidated
                && string.Equals(_revalidatedSource, _input.value, StringComparison.Ordinal)
                && _revalidatedExpectedType == ResolveExpectedType()) return;
            RevalidateForced();
        }

        private void RevalidateForced()
        {
            var source = _input.value;
            var expectedType = ResolveExpectedType();
            _revalidated = true;
            _revalidatedSource = source;
            _revalidatedExpectedType = expectedType;
            if (string.IsNullOrWhiteSpace(source))
            {
                Program = null;
                _status.text = string.Empty;
                _status.EnableInClassList("zax-expression__status--error", false);
                return;
            }

            _diagnostics.Clear();
            var compiled = ZaxCompiler.TryCompile(source, _declaredVariables, expectedType, _diagnostics, out var program);

            Program = compiled ? program : null;
            _status.text = compiled
                ? $"→ {program.ResultType.DisplayName()}"
                : ZatoolsLocalization.LocalizeZaxDiagnostic(_diagnostics[0]);
            _status.EnableInClassList("zax-expression__status--error", !compiled);
        }

        private ZaxValueType? ResolveExpectedType()
        {
            return _expectedType != null ? _expectedType() : null;
        }
    }
}
