using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using Unity.Mathematics;
using KusakaFactory.Zatools.Foundation.Arithmetic;
using KusakaFactory.Zatools.Localization;

namespace KusakaFactory.Zatools.EditorExtension
{
    internal sealed class ArithmeticExpressionPlayground : EditorWindow
    {
        [MenuItem("Window/Zatools: kb10uy's Various Tools/Arithmetic Expression Playground")]
        internal static void OpenWindow()
        {
            var window = GetWindow<ArithmeticExpressionPlayground>("Arithmetic Expression Playground");
            window.minSize = new Vector2(420.0f, 480.0f);
        }

        private readonly List<VariableRow> _rows = new List<VariableRow>();
        private readonly List<ZaxDiagnostic> _diagnostics = new List<ZaxDiagnostic>();

        private TextField _expression;
        private Label _status;
        private Label _result;
        private TextField _disassembly;
        private VisualElement _variablesContainer;

        internal void CreateGUI()
        {
            var visualTree = ZatoolsResources.LoadVisualTreeByGuid("935ecee5773c46b68f8da83071c50a50");
            visualTree.CloneTree(rootVisualElement);
            ZatoolsLocalization.UILocalizer.ApplyLocalizationFor(rootVisualElement);

            _expression = rootVisualElement.Q<TextField>("FieldExpression");
            _status = rootVisualElement.Q<Label>("LabelStatus");
            _result = rootVisualElement.Q<Label>("LabelResult");
            _disassembly = rootVisualElement.Q<TextField>("FieldDisassembly");
            _variablesContainer = rootVisualElement.Q<VisualElement>("ContainerVariables");

            _rows.Clear();
            _variablesContainer.Clear();

            _expression.RegisterValueChangedCallback((e) => Reevaluate());
            rootVisualElement.Q<Button>("ButtonAddVariable").clicked +=
                () => AddVariable("value", ZaxValueType.Float, float4.zero);

            ZatoolsLocalization.OnNdmfLanguageChanged += Reevaluate;

            AddVariable("pos", ZaxValueType.Float3, new float4(1.0f, 2.0f, 3.0f, 0.0f));
            _expression.value = "@pos normalize";
            Reevaluate();
        }

        private void OnDestroy()
        {
            ZatoolsLocalization.OnNdmfLanguageChanged -= Reevaluate;
        }

        private void AddVariable(string name, ZaxValueType type, float4 value)
        {
            var row = new VariableRow
            {
                Element = new VisualElement(),
                Name = new TextField { value = name },
                Type = new EnumField(type),
                Components = new FloatField[4],
            };
            row.Element.style.flexDirection = FlexDirection.Row;
            row.Element.style.marginTop = 2.0f;
            row.Name.style.width = 96.0f;
            row.Type.style.width = 80.0f;

            var initial = new[] { value.x, value.y, value.z, value.w };
            for (var i = 0; i < 4; ++i)
            {
                var component = new FloatField { value = initial[i] };
                component.style.width = 56.0f;
                component.RegisterValueChangedCallback((e) => Reevaluate());
                row.Components[i] = component;
            }

            var remove = new Button { text = "zaxp.remove-variable" };
            remove.AddToClassList("ndmf-tr");
            remove.clicked += () =>
            {
                _rows.Remove(row);
                _variablesContainer.Remove(row.Element);
                Reevaluate();
            };

            row.Name.RegisterValueChangedCallback((e) => Reevaluate());
            row.Type.RegisterValueChangedCallback((e) =>
            {
                row.UpdateComponentVisibility();
                Reevaluate();
            });

            row.Element.Add(row.Name);
            row.Element.Add(row.Type);
            foreach (var component in row.Components) row.Element.Add(component);
            row.Element.Add(remove);
            row.UpdateComponentVisibility();

            _rows.Add(row);
            _variablesContainer.Add(row.Element);
            ZatoolsLocalization.UILocalizer.ApplyLocalizationFor(row.Element);

            Reevaluate();
        }

        private void Reevaluate()
        {
            if (_expression == null) return;

            var source = _expression.value;
            if (string.IsNullOrWhiteSpace(source))
            {
                SetStatus(string.Empty, false);
                _result.text = string.Empty;
                _disassembly.SetValueWithoutNotify(string.Empty);
                return;
            }

            var variables = _rows.Select((r) => r.ToVariable()).ToArray();
            _diagnostics.Clear();
            if (!ZaxCompiler.TryCompile(source, variables, null, _diagnostics, out var program))
            {
                SetStatus(ZatoolsLocalization.LocalizeZaxDiagnostic(_diagnostics[0]), true);
                _result.text = string.Empty;
                _disassembly.SetValueWithoutNotify(string.Empty);
                return;
            }

            SetStatus($"→ {program.ResultType.DisplayName()}", false);
            var values = _rows.Select((r) => r.ToValue()).ToArray();
            _result.text = ZaxEvaluator.Evaluate(program, values).ToString();
            _disassembly.SetValueWithoutNotify(program.Disassemble());
        }

        private void SetStatus(string text, bool error)
        {
            _status.text = text;
            _status.style.color = error
                ? new StyleColor(EditorGUIUtility.isProSkin ? new Color(1.0f, 0.45f, 0.45f) : new Color(0.65f, 0.0f, 0.0f))
                : new StyleColor(StyleKeyword.Null);
        }

        private sealed class VariableRow
        {
            internal VisualElement Element;
            internal TextField Name;
            internal EnumField Type;
            internal FloatField[] Components;

            internal ZaxValueType ValueType => (ZaxValueType)Type.value;

            internal ZaxVariable ToVariable() => new ZaxVariable(Name.value, ValueType);

            internal ZaxValue ToValue()
            {
                switch (ValueType)
                {
                    case ZaxValueType.Int:
                        return ZaxValue.FromInt((int)Components[0].value);
                    case ZaxValueType.Float:
                        return ZaxValue.FromFloat(Components[0].value);
                    case ZaxValueType.Float2:
                        return ZaxValue.FromFloat2(new float2(Components[0].value, Components[1].value));
                    case ZaxValueType.Float3:
                        return ZaxValue.FromFloat3(new float3(Components[0].value, Components[1].value, Components[2].value));
                    default:
                        return ZaxValue.FromFloat4(new float4(
                            Components[0].value, Components[1].value, Components[2].value, Components[3].value));
                }
            }

            internal void UpdateComponentVisibility()
            {
                var dimension = ValueType.Dimension();
                for (var i = 0; i < Components.Length; ++i)
                {
                    Components[i].style.display = i < dimension ? DisplayStyle.Flex : DisplayStyle.None;
                }
            }
        }
    }
}
